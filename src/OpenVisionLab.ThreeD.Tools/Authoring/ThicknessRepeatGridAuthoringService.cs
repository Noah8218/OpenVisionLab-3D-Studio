using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools.Authoring;

/// <summary>
/// Builds a reviewable recipe draft by translating one complete dual-ROI
/// Thickness step. The original document remains untouched until the caller
/// explicitly accepts the returned candidate document.
/// </summary>
public static class ThicknessRepeatGridAuthoringService
{
    public const int MaximumInstanceCount = 64;

    public static ThicknessRepeatGridAuthoringResult CreateCandidate(
        ToolRecipeDocument document,
        string selectedStepId,
        ThicknessRepeatGridRequest request)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedStepId);
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<string>();
        ValidateRequest(request, errors);

        var selectedStep = document.Steps.FirstOrDefault(step =>
            string.Equals(step.Id, selectedStepId, StringComparison.OrdinalIgnoreCase));
        if (selectedStep is null)
        {
            errors.Add($"The selected recipe step does not exist: {selectedStepId}.");
            return Failure(errors);
        }

        if (!string.Equals(selectedStep.ToolId, "thickness", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Repeat as grid requires a Thickness step.");
        }

        if (selectedStep.InputEntityIds.Count < 3)
        {
            errors.Add("Thickness repeat requires HeightField, Reference ROI, and Measurement ROI inputs.");
            return Failure(errors);
        }

        var selections = document.Selections ?? [];
        var reference = selections.FirstOrDefault(selection =>
            string.Equals(
                selection.Id,
                selectedStep.InputEntityIds[1],
                StringComparison.OrdinalIgnoreCase));
        var measurement = selections.FirstOrDefault(selection =>
            string.Equals(
                selection.Id,
                selectedStep.InputEntityIds[2],
                StringComparison.OrdinalIgnoreCase));
        if (reference?.GridRectangle is null)
        {
            errors.Add("The selected Thickness step has no applied Reference ROI.");
        }
        if (measurement?.GridRectangle is null)
        {
            errors.Add("The selected Thickness step has no applied Measurement ROI.");
        }
        if (errors.Count > 0)
        {
            return Failure(errors);
        }

        var usedIds = new HashSet<string>(
            document.Steps.Select(step => step.Id)
                .Concat(document.Steps.Select(step => step.OutputEntityId))
                .Concat(selections.Select(selection => selection.Id))
                .Concat(document.References.Select(referenceItem => referenceItem.Id))
                .Append(document.Source.Id),
            StringComparer.OrdinalIgnoreCase);
        var groupSlug = NormalizeId(request.NamePattern.Replace("{n}", string.Empty, StringComparison.Ordinal));
        if (groupSlug.Length == 0)
        {
            groupSlug = "thickness-grid";
        }

        var candidates = new List<ThicknessRepeatGridCandidate>();
        for (var gridRow = 0; gridRow < request.Rows; gridRow++)
        {
            for (var gridColumn = 0; gridColumn < request.Columns; gridColumn++)
            {
                var instanceNumber = (gridRow * request.Columns) + gridColumn + 1;
                ToolRecipeGridRectangle referenceRectangle;
                ToolRecipeGridRectangle measurementRectangle;
                try
                {
                    var rowOffset = checked(gridRow * request.RowPitch);
                    var columnOffset = checked(gridColumn * request.ColumnPitch);
                    referenceRectangle = Translate(reference!.GridRectangle!, rowOffset, columnOffset);
                    measurementRectangle = Translate(measurement!.GridRectangle!, rowOffset, columnOffset);
                }
                catch (OverflowException)
                {
                    errors.Add($"Instance {instanceNumber}: translated ROI coordinates overflow Int32.");
                    continue;
                }
                var instanceLabel = request.NamePattern.Replace(
                    "{n}",
                    instanceNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    StringComparison.Ordinal);
                var toolName = instanceLabel.EndsWith("Thickness", StringComparison.OrdinalIgnoreCase)
                    ? instanceLabel
                    : $"{instanceLabel} Thickness";

                var stepId = instanceNumber == 1
                    ? selectedStep.Id
                    : CreateUniqueId($"step.{groupSlug}-thickness.{instanceNumber:00}", usedIds);
                var outputId = instanceNumber == 1
                    ? selectedStep.OutputEntityId
                    : CreateUniqueId($"derived.{groupSlug}-thickness.{instanceNumber:00}", usedIds);
                var referenceId = instanceNumber == 1
                    ? reference.Id
                    : CreateUniqueId($"selection.{groupSlug}-{instanceNumber:00}.reference-roi", usedIds);
                var measurementId = instanceNumber == 1
                    ? measurement.Id
                    : CreateUniqueId($"selection.{groupSlug}-{instanceNumber:00}.measurement-roi", usedIds);

                var candidateErrors = new List<string>();
                ValidateRectangle(
                    referenceRectangle,
                    reference.SourceBinding.GridWidth,
                    reference.SourceBinding.GridHeight,
                    "Reference ROI",
                    candidateErrors);
                ValidateRectangle(
                    measurementRectangle,
                    measurement.SourceBinding.GridWidth,
                    measurement.SourceBinding.GridHeight,
                    "Measurement ROI",
                    candidateErrors);

                var candidateReference = reference with
                {
                    Id = referenceId,
                    Name = $"{instanceLabel} Reference ROI",
                    GridRectangle = referenceRectangle
                };
                var candidateMeasurement = measurement with
                {
                    Id = measurementId,
                    Name = $"{instanceLabel} Measurement ROI",
                    GridRectangle = measurementRectangle
                };
                var candidateStep = selectedStep with
                {
                    Id = stepId,
                    ToolName = toolName,
                    InputEntityIds =
                    [
                        selectedStep.InputEntityIds[0],
                        referenceId,
                        measurementId
                    ],
                    OutputEntityId = outputId,
                    DualRoiRouting = new ToolRecipeDualRoiRouting(
                        referenceId,
                        measurementId),
                    Parameters = selectedStep.Parameters
                        .Select(parameter => parameter with { })
                        .ToArray()
                };
                candidates.Add(new ThicknessRepeatGridCandidate(
                    instanceNumber,
                    gridRow + 1,
                    gridColumn + 1,
                    toolName,
                    candidateStep,
                    candidateReference,
                    candidateMeasurement,
                    candidateErrors.Count == 0,
                    candidateErrors.Count == 0
                        ? "Inside the recorded source grid."
                        : string.Join(" ", candidateErrors)));
                foreach (var candidateError in candidateErrors)
                {
                    errors.Add($"Instance {instanceNumber}: {candidateError}");
                }
            }
        }

        if (errors.Count > 0)
        {
            return new ThicknessRepeatGridAuthoringResult(null, candidates, errors);
        }

        var stepIndex = document.Steps
            .Select((step, index) => (step, index))
            .Single(item => ReferenceEquals(item.step, selectedStep))
            .index;
        var candidateSteps = document.Steps.Take(stepIndex)
            .Concat(candidates.Select(candidate => candidate.Step))
            .Concat(document.Steps.Skip(stepIndex + 1))
            .ToArray();
        var replacedSelectionIds = new HashSet<string>(
            [reference!.Id, measurement!.Id],
            StringComparer.OrdinalIgnoreCase);
        var candidateSelections = selections
            .Where(selection => !replacedSelectionIds.Contains(selection.Id))
            .Concat(candidates.SelectMany(candidate =>
                new[] { candidate.ReferenceSelection, candidate.MeasurementSelection }))
            .ToArray();
        var candidateDocument = document with
        {
            SchemaVersion = ToolRecipeDocument.CurrentSchemaVersion,
            Steps = candidateSteps,
            Selections = candidateSelections
        };
        var storageValidation = ToolRecipeValidator.ValidateForStorage(candidateDocument);
        if (!storageValidation.IsValid)
        {
            errors.AddRange(storageValidation.Errors.Select(error => $"Candidate recipe: {error}"));
            return new ThicknessRepeatGridAuthoringResult(null, candidates, errors);
        }

        return new ThicknessRepeatGridAuthoringResult(
            new ThicknessRepeatGridDraft(
                document,
                candidateDocument,
                selectedStep.Id,
                candidates[0].Step.Id,
                candidates),
            candidates,
            []);
    }

    private static void ValidateRequest(
        ThicknessRepeatGridRequest request,
        ICollection<string> errors)
    {
        if (request.Columns <= 0 || request.Rows <= 0)
        {
            errors.Add("Rows and columns must both be greater than zero.");
        }
        else if ((long)request.Columns * request.Rows > MaximumInstanceCount)
        {
            errors.Add($"Repeat as grid supports at most {MaximumInstanceCount} instances.");
        }
        if (request.Columns > 1 && request.ColumnPitch == 0)
        {
            errors.Add("Column pitch must be non-zero when more than one column is requested.");
        }
        if (request.Rows > 1 && request.RowPitch == 0)
        {
            errors.Add("Row pitch must be non-zero when more than one row is requested.");
        }
        if (string.IsNullOrWhiteSpace(request.NamePattern)
            || !request.NamePattern.Contains("{n}", StringComparison.Ordinal))
        {
            errors.Add("Name pattern must contain {n}.");
        }
    }

    private static ToolRecipeGridRectangle Translate(
        ToolRecipeGridRectangle rectangle,
        int rowOffset,
        int columnOffset) =>
        rectangle with
        {
            Row = checked(rectangle.Row + rowOffset),
            Column = checked(rectangle.Column + columnOffset)
        };

    private static void ValidateRectangle(
        ToolRecipeGridRectangle rectangle,
        int gridWidth,
        int gridHeight,
        string label,
        ICollection<string> errors)
    {
        if (rectangle.Row < 0
            || rectangle.Column < 0
            || rectangle.RowCount <= 0
            || rectangle.ColumnCount <= 0
            || rectangle.Row > gridHeight - rectangle.RowCount
            || rectangle.Column > gridWidth - rectangle.ColumnCount)
        {
            errors.Add(
                $"{label} row={rectangle.Row}, column={rectangle.Column}, "
                + $"rows={rectangle.RowCount}, columns={rectangle.ColumnCount} "
                + $"is outside {gridWidth} x {gridHeight}.");
        }
    }

    private static string CreateUniqueId(string requestedId, ISet<string> usedIds)
    {
        var candidate = requestedId;
        var suffix = 2;
        while (!usedIds.Add(candidate))
        {
            candidate = $"{requestedId}-{suffix++}";
        }
        return candidate;
    }

    private static string NormalizeId(string value)
    {
        var characters = value.Trim().ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray();
        var normalized = new string(characters).Trim('-');
        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }
        return normalized;
    }

    private static ThicknessRepeatGridAuthoringResult Failure(IReadOnlyList<string> errors) =>
        new(null, [], errors);
}

public sealed record ThicknessRepeatGridRequest(
    int Columns,
    int Rows,
    int ColumnPitch,
    int RowPitch,
    string NamePattern);

public sealed record ThicknessRepeatGridCandidate(
    int InstanceNumber,
    int GridRow,
    int GridColumn,
    string ToolName,
    ToolRecipeStep Step,
    ToolRecipeSelection ReferenceSelection,
    ToolRecipeSelection MeasurementSelection,
    bool IsValid,
    string ValidationSummary);

public sealed record ThicknessRepeatGridDraft(
    ToolRecipeDocument OriginalDocument,
    ToolRecipeDocument CandidateDocument,
    string SourceStepId,
    string FirstGeneratedStepId,
    IReadOnlyList<ThicknessRepeatGridCandidate> Candidates);

public sealed record ThicknessRepeatGridAuthoringResult(
    ThicknessRepeatGridDraft? Draft,
    IReadOnlyList<ThicknessRepeatGridCandidate> Candidates,
    IReadOnlyList<string> Errors)
{
    public bool IsValid => Draft is not null && Errors.Count == 0;
}
