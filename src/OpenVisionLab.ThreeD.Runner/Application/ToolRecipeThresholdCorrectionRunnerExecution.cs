using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class ToolRecipeThresholdCorrectionRunnerExecution
{
    public static int Run(
        string recipePath,
        string candidateId,
        string reportPath,
        string? manualValues = null)
    {
        try
        {
            var result = Execute(recipePath, candidateId, manualValues);
            var fullReportPath = Path.GetFullPath(reportPath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(fullReportPath)
                ?? Environment.CurrentDirectory);
            File.WriteAllText(
                fullReportPath,
                JsonSerializer.Serialize(
                    result,
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        Converters = { new JsonStringEnumConverter() },
                        WriteIndented = true
                    }));
            Console.WriteLine(result.Evidence.Message);
            Console.WriteLine(
                $"Threshold correction report: {fullReportPath}");
            return result.HeldOutExecution.Status == ResultStatus.Error
                ? 1
                : 0;
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or InvalidDataException
            or InvalidOperationException
            or IOException
            or UnauthorizedAccessException
            or JsonException
            or OverflowException)
        {
            Console.Error.WriteLine(
                $"Threshold correction Runner failed: {exception.Message}");
            return 1;
        }
    }

    internal static ToolRecipeThresholdCorrectionRunnerReport Execute(
        string recipePath,
        string candidateId,
        string? manualValues = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateId);
        var fullRecipePath = Path.GetFullPath(recipePath);
        var document = ToolRecipeDocumentStore.Load(fullRecipePath);
        var definition =
            ToolRecipeValidationSetDefinitionStore.LoadForRecipe(
                fullRecipePath)
            ?? throw new InvalidDataException(
                "The recipe has no Validation Set role manifest.");
        if (!string.Equals(
                definition.RecipeSourceSha256,
                document.Source.ContentSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The Validation Set role manifest belongs to a different recipe source identity.");
        }

        var developmentInputs = definition.Samples
            .Where(sample =>
                sample.Role
                    is ToolRecipeValidationSampleRole.Good
                    or ToolRecipeValidationSampleRole.Bad)
            .OrderBy(sample => sample.Order)
            .Select(sample => new ToolRecipeValidationSampleInput(
                sample.SourcePath,
                sample.Role))
            .ToArray();
        var heldOutInputs = definition.Samples
            .Where(sample =>
                sample.Role == ToolRecipeValidationSampleRole.HeldOut)
            .OrderBy(sample => sample.Order)
            .Select(sample => new ToolRecipeValidationSampleInput(
                sample.SourcePath,
                sample.Role))
            .ToArray();
        if (developmentInputs.Length == 0 || heldOutInputs.Length == 0)
        {
            throw new InvalidDataException(
                "Threshold correction requires development Good/Bad samples and at least one separate HeldOut sample.");
        }

        var developmentExecution =
            ToolRecipeValidationSetExecution.Execute(
                document,
                developmentInputs);
        if (developmentExecution.Status == ResultStatus.Error)
        {
            throw new InvalidDataException(
                $"Development execution failed: {developmentExecution.Message}");
        }
        var candidates =
            ToolRecipeThresholdCandidateAnalyzer.Analyze(
                document,
                developmentExecution);
        var candidate = candidates.Candidates.SingleOrDefault(item =>
            string.Equals(
                item.CandidateId,
                candidateId,
                StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                $"Threshold candidate '{candidateId}' was not generated from the development set.");
        if (!ToolRecipeThresholdCandidateParameterMapper.TryCreateProposal(
                document,
                candidate,
                out var proposal,
                out var mappingMessage)
            || proposal is null)
        {
            throw new InvalidDataException(mappingMessage);
        }

        ToolRecipeValidationSetResult? correctedDevelopmentExecution = null;
        IReadOnlyList<ToolRecipeThresholdManualParameterChange> manualChanges =
            [];
        ToolRecipeDocument projectedDocument;
        if (string.IsNullOrWhiteSpace(manualValues))
        {
            projectedDocument =
                ToolRecipeThresholdCandidateParameterMapper.ApplyProposal(
                    document,
                    proposal);
        }
        else
        {
            var parsedManualValues = ParseManualValues(
                proposal,
                manualValues);
            manualChanges = proposal.Changes.Select(change =>
                new ToolRecipeThresholdManualParameterChange(
                    change.ParameterName,
                    change.ProposedValue,
                    parsedManualValues[change.ParameterName])).ToArray();
            if (manualChanges.All(change => string.Equals(
                    change.SuggestedValue,
                    change.ManualValue,
                    StringComparison.Ordinal)))
            {
                throw new InvalidDataException(
                    "Runner manual threshold values must differ from the deterministic suggestion.");
            }

            var manualProposal = proposal with
            {
                Changes = proposal.Changes.Select(change =>
                    change with
                    {
                        ProposedValue =
                            parsedManualValues[change.ParameterName]
                    }).ToArray()
            };
            projectedDocument =
                ToolRecipeThresholdCandidateParameterMapper.ApplyProposal(
                    document,
                    manualProposal);
            correctedDevelopmentExecution =
                ToolRecipeValidationSetExecution.Execute(
                    projectedDocument,
                    developmentInputs);
        }
        var heldOutExecution =
            ToolRecipeValidationSetExecution.Execute(
                projectedDocument,
                heldOutInputs);
        var evidence = correctedDevelopmentExecution is null
            ? ToolRecipeThresholdCorrectionEvidenceBuilder.Build(
                projectedDocument,
                proposal,
                heldOutExecution)
            : ToolRecipeThresholdCorrectionEvidenceBuilder
                .BuildManualCorrection(
                    projectedDocument,
                    proposal,
                    manualChanges,
                    developmentExecution,
                    correctedDevelopmentExecution,
                    heldOutExecution);
        return new ToolRecipeThresholdCorrectionRunnerReport(
            correctedDevelopmentExecution is null ? "1.0" : "2.0",
            fullRecipePath,
            ToolRecipeValidationSetDefinitionStore.GetPathForRecipe(
                fullRecipePath),
            developmentExecution,
            candidates,
            heldOutExecution,
            evidence,
            correctedDevelopmentExecution);
    }

    private static IReadOnlyDictionary<string, string> ParseManualValues(
        ToolRecipeThresholdParameterProposal proposal,
        string value)
    {
        var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in value.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries
                     | StringSplitOptions.TrimEntries))
        {
            var separator = item.IndexOf('=');
            if (separator <= 0 || separator == item.Length - 1)
            {
                throw new InvalidDataException(
                    $"Invalid manual threshold value '{item}'. Expected Name=Value.");
            }
            var name = item[..separator].Trim();
            var manualValue = item[(separator + 1)..].Trim();
            if (!parsed.TryAdd(name, manualValue))
            {
                throw new InvalidDataException(
                    $"Duplicate manual threshold parameter '{name}'.");
            }
        }

        var expectedNames = proposal.Changes.Select(change =>
            change.ParameterName).ToHashSet(StringComparer.Ordinal);
        if (parsed.Count != expectedNames.Count
            || !parsed.Keys.All(expectedNames.Contains))
        {
            throw new InvalidDataException(
                $"Manual threshold values must provide exactly: {string.Join(", ", expectedNames.OrderBy(name => name, StringComparer.Ordinal))}.");
        }
        return parsed;
    }
}

internal sealed record ToolRecipeThresholdCorrectionRunnerReport(
    string SchemaVersion,
    string RecipePath,
    string ValidationSetManifestPath,
    ToolRecipeValidationSetResult DevelopmentExecution,
    ToolRecipeThresholdCandidateReport Candidates,
    ToolRecipeValidationSetResult HeldOutExecution,
    ToolRecipeThresholdCorrectionEvidence Evidence,
    ToolRecipeValidationSetResult? CorrectedDevelopmentExecution = null);
