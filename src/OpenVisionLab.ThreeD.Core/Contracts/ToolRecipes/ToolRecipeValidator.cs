namespace OpenVisionLab.ThreeD.Core;

public static class ToolRecipeValidator
{
    private const string GridCellLocatorKind = "grid-cell";

    public static ToolRecipeValidationResult Validate(ToolRecipeDocument? document)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        if (document is null)
        {
            errors.Add("Teaching recipe is required.");
            return new ToolRecipeValidationResult(errors, warnings);
        }

        var isLegacySchema = string.Equals(
            document.SchemaVersion,
            ToolRecipeDocument.LegacySchemaVersion,
            StringComparison.Ordinal);
        var isSelectionSchema = string.Equals(
            document.SchemaVersion,
            ToolRecipeDocument.SelectionSchemaVersion,
            StringComparison.Ordinal);
        var isGenericMeasurementSchema = string.Equals(
            document.SchemaVersion,
            ToolRecipeDocument.GenericMeasurementSchemaVersion,
            StringComparison.Ordinal);
        var isCurrentSchema = string.Equals(
            document.SchemaVersion,
            ToolRecipeDocument.CurrentSchemaVersion,
            StringComparison.Ordinal);
        if (!isLegacySchema && !isSelectionSchema && !isGenericMeasurementSchema && !isCurrentSchema)
        {
            errors.Add($"Unsupported teaching recipe schema: {Clean(document.SchemaVersion)}.");
        }

        if (string.IsNullOrWhiteSpace(document.Name))
        {
            errors.Add("Recipe name is required.");
        }

        var source = document.Source;
        if (source is null)
        {
            errors.Add("Source descriptor is required.");
            return new ToolRecipeValidationResult(errors, warnings);
        }

        if (string.IsNullOrWhiteSpace(source.Id)) errors.Add("Source ID is required.");
        if (string.IsNullOrWhiteSpace(source.Name)) errors.Add("Source name is required.");
        if (string.IsNullOrWhiteSpace(source.Format)) errors.Add("Source format is required.");
        if (string.IsNullOrWhiteSpace(source.Unit)) errors.Add("Source unit is required.");
        if (string.IsNullOrWhiteSpace(source.FrameId)) errors.Add("Source frame ID is required.");
        if (string.IsNullOrWhiteSpace(source.Path)) errors.Add("Source path is required.");
        if (source.ByteLength is <= 0) errors.Add("Source byte length must be positive when recorded.");
        if (source.ContentSha256 is not null && !IsSha256(source.ContentSha256))
        {
            errors.Add("Source SHA-256 must contain exactly 64 hexadecimal characters when recorded.");
        }
        if (source.GridWidth is <= 0 || source.GridHeight is <= 0)
        {
            errors.Add("Source grid dimensions must be positive when recorded.");
        }

        var globalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var routableEntityIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddIdentity(globalIds, source.Id, "source", errors);
        AddRoutableEntity(routableEntityIds, source.Id);

        var references = document.References ?? [];
        foreach (var reference in references)
        {
            if (reference is null)
            {
                errors.Add("Reference descriptor cannot be null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(reference.Id)) errors.Add("Reference ID is required.");
            if (string.IsNullOrWhiteSpace(reference.Name)) errors.Add($"Reference '{Clean(reference.Id)}' name is required.");
            if (string.IsNullOrWhiteSpace(reference.Kind)) errors.Add($"Reference '{Clean(reference.Id)}' kind is required.");
            AddIdentity(globalIds, reference.Id, "reference", errors);
            AddRoutableEntity(routableEntityIds, reference.Id);
        }

        var selections = document.Selections ?? [];
        if (isLegacySchema && selections.Count > 0)
        {
            errors.Add("Teaching recipe schema 1.0 cannot contain structured selections.");
        }

        var correspondenceRows = new List<(string SelectionId, string SelectionLabel, ToolRecipeLandmarkCorrespondence Row)>();
        var correspondenceSelectionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var selection in selections)
        {
            if (selection is null)
            {
                errors.Add("Selection descriptor cannot be null.");
                continue;
            }

            ValidateSelection(
                selection,
                source,
                isGenericMeasurementSchema || isCurrentSchema,
                isCurrentSchema,
                errors,
                warnings,
                correspondenceRows);
            AddIdentity(globalIds, selection.Id, "selection", errors);
            AddRoutableEntity(routableEntityIds, selection.Id);
            if (string.Equals(selection.Kind, ToolRecipeSelectionKinds.LandmarkCorrespondenceSet, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(selection.Id))
            {
                correspondenceSelectionIds.Add(selection.Id.Trim());
            }
        }

        var steps = document.Steps ?? [];
        if (steps.Count == 0)
        {
            errors.Add("At least one taught tool step is required.");
        }

        var outputStepIndices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var correspondenceConsumerStepIndices = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];
            var label = $"Step {index + 1}";
            if (step is null)
            {
                errors.Add($"{label} is required.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(step.Id))
            {
                errors.Add($"{label} ID is required.");
            }
            else
            {
                AddIdentity(globalIds, step.Id, $"{label} ID", errors);
            }

            if (string.IsNullOrWhiteSpace(step.ToolId)) errors.Add($"{label} tool ID is required.");
            if (string.IsNullOrWhiteSpace(step.ToolName)) errors.Add($"{label} tool name is required.");

            var inputs = step.InputEntityIds?
                .Where(input => !string.IsNullOrWhiteSpace(input))
                .Select(input => input.Trim())
                .ToList() ?? [];
            var minimumInputCount = Math.Max(1, step.MinimumInputCount);
            if (inputs.Count < minimumInputCount)
            {
                errors.Add($"{label} '{Clean(step.ToolName)}' requires {minimumInputCount} input entity ID(s).");
            }

            var uniqueInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var input in inputs)
            {
                if (!uniqueInputs.Add(input))
                {
                    errors.Add($"{label} '{Clean(step.ToolName)}' repeats input '{input}'.");
                }
                else if (!routableEntityIds.Contains(input))
                {
                    errors.Add($"{label} '{Clean(step.ToolName)}' input '{input}' is not a source, declared reference, structured selection, or earlier output.");
                }

                if (correspondenceSelectionIds.Contains(input))
                {
                    if (!correspondenceConsumerStepIndices.TryGetValue(input, out var consumerIndices))
                    {
                        consumerIndices = [];
                        correspondenceConsumerStepIndices.Add(input, consumerIndices);
                    }

                    consumerIndices.Add(index);
                }
            }

            if (string.IsNullOrWhiteSpace(step.OutputEntityId))
            {
                errors.Add($"{label} '{Clean(step.ToolName)}' output entity ID is required.");
            }
            else
            {
                AddIdentity(globalIds, step.OutputEntityId, $"{label} output", errors);
                AddRoutableEntity(routableEntityIds, step.OutputEntityId);
                outputStepIndices.TryAdd(step.OutputEntityId.Trim(), index);
            }

            foreach (var parameter in step.Parameters ?? [])
            {
                if (parameter is null || string.IsNullOrWhiteSpace(parameter.Name))
                {
                    errors.Add($"{label} '{Clean(step.ToolName)}' has a parameter without a name.");
                }
            }

            if (string.Equals(step.ToolId, "filter", StringComparison.OrdinalIgnoreCase))
            {
                ValidateFilterStep(step, inputs, source, label, errors, warnings);
            }

            if (string.Equals(step.ToolId, "xyz-affine-transform", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"{label} XYZ Affine is taught only: execution needs four affine-independent source/reference landmarks or a fixture-constrained contract.");
            }

            if (string.Equals(step.ToolId, "xyz-affine-solve", StringComparison.OrdinalIgnoreCase))
            {
                ValidateXYZAffineSolveStep(step, inputs, label, errors);
            }

            if (string.Equals(step.ToolId, "xyz-affine-apply", StringComparison.OrdinalIgnoreCase))
            {
                ValidateXYZAffineApplyStep(step, inputs, source, label, errors);
            }

            if (string.Equals(step.ToolId, "re-grid-height-map", StringComparison.OrdinalIgnoreCase))
            {
                ValidateRegridHeightMapStep(step, inputs, label, errors);
            }

            if (step.ToolId is "thickness" or "warpage" or "plane-flatness" or "point-pair-dimensions" or "gap-flush")
            {
                ValidateHeightMeasurementStep(step, inputs, source, selections, label, errors);
            }
        }

        foreach (var (selectionId, selectionLabel, row) in correspondenceRows)
        {
            if (string.IsNullOrWhiteSpace(row.SourceEntityId))
            {
                continue;
            }

            var sourceEntityId = row.SourceEntityId.Trim();
            if (!routableEntityIds.Contains(sourceEntityId))
            {
                errors.Add($"{selectionLabel} correspondence source entity '{sourceEntityId}' is not declared by the recipe.");
            }
            else if (outputStepIndices.TryGetValue(sourceEntityId, out var sourceStepIndex)
                && correspondenceConsumerStepIndices.TryGetValue(selectionId, out var consumerIndices)
                && consumerIndices.Count > 0
                && sourceStepIndex >= consumerIndices.Min())
            {
                errors.Add(
                    $"{selectionLabel} correspondence source entity '{sourceEntityId}' must be produced before the step that consumes selection '{selectionId}'.");
            }
        }

        return new ToolRecipeValidationResult(errors, warnings);
    }

    private static void ValidateXYZAffineSolveStep(
        ToolRecipeStep step,
        IReadOnlyList<string> inputs,
        string label,
        List<string> errors)
    {
        if (inputs.Count != 1)
        {
            errors.Add($"{label} XYZ Affine Solve v1 requires exactly one CorrespondenceSet input.");
        }
        var parameters = step.Parameters ?? [];
        var expected = new HashSet<string>(
            ["SolvePolicy", "MaximumConditionEstimate", "ArithmeticResidualWarning"],
            StringComparer.Ordinal);
        if (parameters.Count != expected.Count || expected.Any(name => parameters.Count(parameter => parameter.Name == name) != 1))
        {
            errors.Add($"{label} XYZ Affine Solve v1 requires SolvePolicy, MaximumConditionEstimate, and ArithmeticResidualWarning exactly once.");
            return;
        }
        var solvePolicy = parameters.Single(parameter => parameter.Name == "SolvePolicy").Value;
        var maximumText = parameters.Single(parameter => parameter.Name == "MaximumConditionEstimate").Value;
        var warningText = parameters.Single(parameter => parameter.Name == "ArithmeticResidualWarning").Value;
        if (!string.Equals(solvePolicy, "ExactFourPartialPivot", StringComparison.Ordinal))
        {
            errors.Add($"{label} XYZ Affine Solve v1 requires SolvePolicy ExactFourPartialPivot.");
        }
        if (!double.TryParse(maximumText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var maximum)
            || !double.IsFinite(maximum) || maximum <= 0d)
        {
            errors.Add($"{label} XYZ Affine Solve maximum condition estimate must be a finite positive invariant number.");
        }
        if (!double.TryParse(warningText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var warning)
            || !double.IsFinite(warning) || warning < 0d)
        {
            errors.Add($"{label} XYZ Affine Solve arithmetic residual warning must be a finite non-negative invariant number.");
        }
    }

    private static void ValidateXYZAffineApplyStep(
        ToolRecipeStep step,
        IReadOnlyList<string> inputs,
        ToolRecipeSource source,
        string label,
        List<string> errors)
    {
        if (inputs.Count != 2 || !string.Equals(inputs[0], source.Id, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"{label} Apply XYZ Affine v1 requires the recipe raw C3D source first and one AffineTransform3D second.");
        }
        if (!string.Equals(source.Format, "C3D", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(source.Unit, "raw-height", StringComparison.Ordinal))
        {
            errors.Add($"{label} Apply XYZ Affine v1 requires a C3D raw-height source.");
        }
        if (source.ByteLength is null || source.ContentSha256 is null
            || source.GridWidth is null || source.GridHeight is null)
        {
            errors.Add($"{label} Apply XYZ Affine v1 requires source byte length, SHA-256, width, and height identity.");
        }
        if ((step.Parameters ?? []).Count != 0)
        {
            errors.Add($"{label} Apply XYZ Affine v1 has no authored parameters.");
        }
    }

    private static void ValidateRegridHeightMapStep(
        ToolRecipeStep step,
        IReadOnlyList<string> inputs,
        string label,
        List<string> errors)
    {
        if (inputs.Count != 1)
        {
            errors.Add($"{label} Re-grid Height Map v1 requires exactly one Published TransformedPointCloud input.");
        }
        try
        {
            _ = C3DReferenceGridProfile.FromRecipeParameters(step.Parameters ?? []);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or InvalidOperationException or OverflowException)
        {
            errors.Add($"{label} Re-grid Height Map v1 ReferenceGridProfile is invalid: {exception.Message}");
        }
    }

    private static void ValidateFilterStep(
        ToolRecipeStep step,
        IReadOnlyList<string> inputs,
        ToolRecipeSource source,
        string label,
        List<string> errors,
        List<string> warnings)
    {
        if (inputs.Count != 1 || !string.Equals(inputs[0], source.Id, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"{label} Filter v1 requires exactly the recipe C3D source as input.");
        }

        if (!string.Equals(source.Format, "C3D", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(source.Unit, "raw-height", StringComparison.Ordinal))
        {
            errors.Add($"{label} Filter v1 requires a C3D raw-height source.");
        }

        if (source.ByteLength is null || source.ContentSha256 is null
            || source.GridWidth is null || source.GridHeight is null)
        {
            errors.Add($"{label} Filter v1 requires source byte length, SHA-256, width, and height identity.");
        }

        var parameters = step.Parameters ?? [];
        var expectedNames = new HashSet<string>(
            ["Method", "KernelSize", "MissingValuePolicy", "BoundaryPolicy"],
            StringComparer.Ordinal);
        if (expectedNames.Any(name => parameters.Count(parameter => parameter is not null && parameter.Name == name) != 1))
        {
            errors.Add($"{label} Filter v1 requires one each of Method, KernelSize, MissingValuePolicy, and BoundaryPolicy.");
            return;
        }

        var unknownNames = parameters
            .Where(parameter => parameter is not null && !expectedNames.Contains(parameter.Name))
            .Select(parameter => parameter.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (unknownNames.Length > 0)
        {
            warnings.Add($"{label} preserves unmapped Filter parameter(s): {string.Join(", ", unknownNames)}.");
        }

        string Value(string name) => parameters.Single(parameter => parameter.Name == name).Value;
        if (!string.Equals(Value("Method"), "Median", StringComparison.Ordinal))
        {
            errors.Add($"{label} Filter v1 Method must be 'Median'.");
        }

        if (Value("KernelSize") is not ("3" or "5" or "7"))
        {
            errors.Add($"{label} Filter v1 KernelSize must be 3, 5, or 7.");
        }

        if (!string.Equals(Value("MissingValuePolicy"), "PreserveMask", StringComparison.Ordinal))
        {
            errors.Add($"{label} Filter v1 MissingValuePolicy must be 'PreserveMask'.");
        }

        if (!string.Equals(Value("BoundaryPolicy"), "AvailableNeighbors", StringComparison.Ordinal))
        {
            errors.Add($"{label} Filter v1 BoundaryPolicy must be 'AvailableNeighbors'.");
        }
    }

    private static void ValidateHeightMeasurementStep(
        ToolRecipeStep step,
        IReadOnlyList<string> inputs,
        ToolRecipeSource source,
        IReadOnlyList<ToolRecipeSelection> selections,
        string label,
        List<string> errors)
    {
        var isPlaneFlatness = step.ToolId == "plane-flatness";
        var isPointPair = step.ToolId == "point-pair-dimensions";
        var isGapFlush = step.ToolId == "gap-flush";
        var expectedInputCount = isPlaneFlatness || isGapFlush ? 3 : 2;
        if (inputs.Count != expectedInputCount)
        {
            errors.Add(isPlaneFlatness || isGapFlush
                ? $"{label} {Clean(step.ToolName)} v1 requires one TransformedHeightField and two ordered GridRectangles."
                : isPointPair
                    ? $"{label} Point Pair Dimensions v1 requires one TransformedHeightField and one ordered PointSet(2)."
                : $"{label} {Clean(step.ToolName)} v1 requires one HeightField first and one GridRectangle second.");
            return;
        }
        if ((isPlaneFlatness || isPointPair || isGapFlush) && string.Equals(inputs[0], source.Id, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"{label} {Clean(step.ToolName)} v1 requires a Published TransformedHeightField first input.");
        }
        for (var inputIndex = 1; inputIndex < inputs.Count; inputIndex++)
        {
            var selection = selections.SingleOrDefault(candidate =>
                candidate is not null && string.Equals(candidate.Id, inputs[inputIndex], StringComparison.OrdinalIgnoreCase));
            var validSelection = isPointPair
                ? selection?.Kind == ToolRecipeSelectionKinds.PointSet && selection.Points?.Count == 2
                : selection?.Kind == ToolRecipeSelectionKinds.GridRectangle && selection.GridRectangle is not null;
            if (!validSelection)
            {
                errors.Add(isPointPair
                    ? $"{label} {Clean(step.ToolName)} v1 input {inputIndex + 1} must be one recipe-owned ordered PointSet(2)."
                    : $"{label} {Clean(step.ToolName)} v1 input {inputIndex + 1} must be one recipe-owned GridRectangle.");
            }
            else if (string.Equals(inputs[0], source.Id, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(selection!.SourceBinding.Format, "C3D", StringComparison.OrdinalIgnoreCase)
                    || !string.IsNullOrWhiteSpace(selection.SourceBinding.OwnerEntityId))
                {
                    errors.Add($"{label} {Clean(step.ToolName)} raw C3D input requires a source-owned C3D selection.");
                }
            }
            else if (!string.Equals(selection!.SourceBinding.Format, "TransformedHeightField", StringComparison.Ordinal)
                || !string.Equals(selection.SourceBinding.OwnerEntityId, inputs[0], StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{label} {Clean(step.ToolName)} transformed input requires selections owned by the first-input TransformedHeightField.");
            }
        }
        var expected = step.ToolId switch
        {
            "thickness" => new[] { "MinimumThickness", "MaximumThickness", "MinimumValidSampleCount" },
            "warpage" => new[] { "MaximumPeakToValley", "MaximumRms", "MinimumValidSampleCount" },
            "point-pair-dimensions" => new[] { "ExpectedDistance", "DistanceTolerance", "ExpectedPlanarWidth", "PlanarWidthTolerance", "ExpectedElevationAngleDegrees", "ElevationAngleToleranceDegrees" },
            "gap-flush" => new[] { "ExpectedGap", "GapTolerance", "ExpectedFlush", "FlushTolerance" },
            _ => new[] { "MaximumFlatness", "MinimumReferenceSampleCount", "MinimumMeasurementSampleCount" }
        };
        var parameters = step.Parameters ?? [];
        if (parameters.Count != expected.Length || expected.Any(name => parameters.Count(parameter => parameter.Name == name) != 1))
        {
            errors.Add($"{label} {Clean(step.ToolName)} v1 requires exactly {string.Join(", ", expected)}.");
        }
    }

    private static void ValidateSelection(
        ToolRecipeSelection selection,
        ToolRecipeSource source,
        bool hasCorrespondenceDescriptor,
        bool supportsArtifactOwnedSelections,
        List<string> errors,
        List<string> warnings,
        List<(string SelectionId, string SelectionLabel, ToolRecipeLandmarkCorrespondence Row)> correspondenceRows)
    {
        var label = $"Selection '{Clean(selection.Id)}'";
        if (string.IsNullOrWhiteSpace(selection.Id)) errors.Add("Selection ID is required.");
        if (string.IsNullOrWhiteSpace(selection.Name)) errors.Add($"{label} name is required.");
        if (string.IsNullOrWhiteSpace(selection.Kind)) errors.Add($"{label} kind is required.");
        if (string.IsNullOrWhiteSpace(selection.RootSourceId)) errors.Add($"{label} root source ID is required.");
        if (string.IsNullOrWhiteSpace(selection.FrameId)) errors.Add($"{label} frame ID is required.");

        if (!string.IsNullOrWhiteSpace(selection.RootSourceId)
            && !string.Equals(selection.RootSourceId.Trim(), source.Id?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"{label} root source '{selection.RootSourceId.Trim()}' does not match recipe source '{Clean(source.Id)}'.");
        }

        var binding = selection.SourceBinding;
        if (binding is null)
        {
            errors.Add($"{label} source binding is required.");
            return;
        }

        var isRawSourceBinding = string.Equals(binding.Format, "C3D", StringComparison.OrdinalIgnoreCase);
        var isArtifactBinding = string.Equals(binding.Format, "TransformedHeightField", StringComparison.Ordinal);
        if (!isRawSourceBinding && !isArtifactBinding)
        {
            errors.Add($"{label} binding format must be C3D or TransformedHeightField.");
        }
        if (isRawSourceBinding)
        {
            if (!string.Equals(selection.FrameId?.Trim(), source.FrameId?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{label} frame '{Clean(selection.FrameId)}' does not match source frame '{Clean(source.FrameId)}'.");
            }
            if (!string.Equals(binding.Format?.Trim(), source.Format?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{label} source binding format '{Clean(binding.Format)}' does not match source format '{Clean(source.Format)}'.");
            }
            if (!string.IsNullOrWhiteSpace(binding.OwnerEntityId)
                || !string.IsNullOrWhiteSpace(binding.RootSourceContentSha256)
                || !string.IsNullOrWhiteSpace(binding.Unit)
                || !string.IsNullOrWhiteSpace(binding.FrameId))
            {
                errors.Add($"{label} raw C3D binding cannot declare artifact ownership fields.");
            }
        }
        if (isArtifactBinding)
        {
            if (!supportsArtifactOwnedSelections)
            {
                errors.Add($"{label} artifact-owned binding requires recipe schema {ToolRecipeDocument.CurrentSchemaVersion}.");
            }
            if (selection.Kind is not (ToolRecipeSelectionKinds.GridRectangle or ToolRecipeSelectionKinds.PointSet))
            {
                errors.Add($"{label} TransformedHeightField binding supports GridRectangle or PointSet geometry only.");
            }
            if (string.IsNullOrWhiteSpace(binding.OwnerEntityId)) errors.Add($"{label} artifact owner entity ID is required.");
            if (!IsSha256(binding.RootSourceContentSha256)) errors.Add($"{label} artifact root-source SHA-256 is required.");
            if (!string.Equals(binding.RootSourceContentSha256, source.ContentSha256, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{label} artifact root-source SHA-256 does not match the recipe source.");
            }
            if (string.IsNullOrWhiteSpace(binding.Unit)) errors.Add($"{label} artifact unit is required.");
            if (string.IsNullOrWhiteSpace(binding.FrameId)) errors.Add($"{label} artifact frame ID is required.");
            if (!string.Equals(selection.FrameId, binding.FrameId, StringComparison.Ordinal))
            {
                errors.Add($"{label} frame must match the owned TransformedHeightField frame.");
            }
        }

        if (!IsSha256(binding.ContentSha256))
        {
            errors.Add($"{label} source binding SHA-256 must contain exactly 64 hexadecimal characters.");
        }

        if (binding.GridWidth <= 0 || binding.GridHeight <= 0)
        {
            errors.Add($"{label} source binding grid dimensions must be positive.");
        }

        if (string.Equals(selection.Kind, ToolRecipeSelectionKinds.GridRectangle, StringComparison.Ordinal))
        {
            ValidateGridRectangle(selection, binding, label, errors);
            return;
        }

        if (string.Equals(selection.Kind, ToolRecipeSelectionKinds.PointSet, StringComparison.Ordinal))
        {
            ValidatePointSet(selection, binding, label, errors);
            return;
        }

        if (string.Equals(selection.Kind, ToolRecipeSelectionKinds.LandmarkCorrespondenceSet, StringComparison.Ordinal))
        {
            ValidateCorrespondenceSet(selection, label, hasCorrespondenceDescriptor, errors, warnings, correspondenceRows);
            return;
        }

        if (!string.IsNullOrWhiteSpace(selection.Kind))
        {
            errors.Add($"{label} kind '{selection.Kind.Trim()}' is not supported.");
        }
    }

    private static void ValidateGridRectangle(
        ToolRecipeSelection selection,
        ToolRecipeSelectionSourceBinding binding,
        string label,
        List<string> errors)
    {
        if (HasItems(selection.Points) || HasItems(selection.Rows))
        {
            errors.Add($"{label} grid rectangle cannot contain point-set or correspondence payloads.");
        }

        var rectangle = selection.GridRectangle;
        if (rectangle is null)
        {
            errors.Add($"{label} grid rectangle payload is required.");
            return;
        }

        if (rectangle.Row < 0 || rectangle.Column < 0
            || rectangle.RowCount <= 0 || rectangle.ColumnCount <= 0)
        {
            errors.Add($"{label} grid rectangle must have a non-negative origin and positive dimensions.");
            return;
        }

        if (binding.GridWidth > 0 && binding.GridHeight > 0
            && (rectangle.RowCount > binding.GridHeight
                || rectangle.ColumnCount > binding.GridWidth
                || rectangle.Row > binding.GridHeight - rectangle.RowCount
                || rectangle.Column > binding.GridWidth - rectangle.ColumnCount))
        {
            errors.Add($"{label} grid rectangle is outside the recorded {binding.GridWidth} x {binding.GridHeight} bound grid.");
        }
    }

    private static void ValidatePointSet(
        ToolRecipeSelection selection,
        ToolRecipeSelectionSourceBinding binding,
        string label,
        List<string> errors)
    {
        if (selection.GridRectangle is not null || HasItems(selection.Rows))
        {
            errors.Add($"{label} point set cannot contain rectangle or correspondence payloads.");
        }

        var points = selection.Points ?? [];
        if (points.Count is not (2 or 3))
        {
            errors.Add($"{label} point set must contain exactly two or three points.");
        }

        var cells = new HashSet<(int Row, int Column)>();
        var finitePositions = new List<ToolRecipeXyz>();
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            var pointLabel = $"{label} point {index + 1}";
            if (point is null)
            {
                errors.Add($"{pointLabel} is required.");
                continue;
            }

            var locator = point.Locator;
            if (locator is null)
            {
                errors.Add($"{pointLabel} locator is required.");
            }
            else
            {
                if (!string.Equals(locator.Kind, GridCellLocatorKind, StringComparison.Ordinal))
                {
                    errors.Add($"{pointLabel} locator kind must be '{GridCellLocatorKind}'.");
                }

                if (locator.Row < 0 || locator.Column < 0
                    || locator.Row >= binding.GridHeight || locator.Column >= binding.GridWidth)
                {
                    errors.Add($"{pointLabel} locator is outside the recorded {binding.GridWidth} x {binding.GridHeight} C3D grid.");
                }

                if (!cells.Add((locator.Row, locator.Column)))
                {
                    errors.Add($"{label} repeats grid cell ({locator.Row}, {locator.Column}).");
                }
            }

            if (point.CapturedPosition is null || !IsFinite(point.CapturedPosition))
            {
                errors.Add($"{pointLabel} captured XYZ position must be finite.");
            }
            else
            {
                finitePositions.Add(point.CapturedPosition);
            }

            if (!double.IsFinite(point.RawHeight))
            {
                errors.Add($"{pointLabel} raw height must be finite.");
            }
        }

        if (points.Count == 3 && finitePositions.Count == 3 && AreCollinear(finitePositions[0], finitePositions[1], finitePositions[2]))
        {
            errors.Add($"{label} three captured XYZ positions must not be collinear.");
        }
    }

    private static void ValidateCorrespondenceSet(
        ToolRecipeSelection selection,
        string label,
        bool isCurrentSchema,
        List<string> errors,
        List<string> warnings,
        List<(string SelectionId, string SelectionLabel, ToolRecipeLandmarkCorrespondence Row)> correspondenceRows)
    {
        if (selection.GridRectangle is not null || HasItems(selection.Points))
        {
            errors.Add($"{label} correspondence set cannot contain rectangle or point-set payloads.");
        }

        var descriptor = selection.CorrespondenceDescriptor;
        if (isCurrentSchema)
        {
            ValidateCorrespondenceDescriptor(descriptor, label, errors);
        }
        else if (descriptor is not null)
        {
            errors.Add($"{label} correspondence descriptor requires teaching recipe schema {ToolRecipeDocument.CurrentSchemaVersion}.");
        }

        var rows = selection.Rows ?? [];
        var sourceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var referenceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var referenceFrames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var rowLabel = $"{label} correspondence row {index + 1}";
            if (row is null)
            {
                errors.Add($"{rowLabel} is required.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.SourceEntityId))
            {
                errors.Add($"{rowLabel} source entity ID is required.");
            }
            else if (!sourceIds.Add(row.SourceEntityId.Trim()))
            {
                errors.Add($"{label} repeats correspondence source entity '{row.SourceEntityId.Trim()}'.");
            }

            if (string.IsNullOrWhiteSpace(row.ReferenceLandmarkId))
            {
                errors.Add($"{rowLabel} reference landmark ID is required.");
            }
            else if (!referenceIds.Add(row.ReferenceLandmarkId.Trim()))
            {
                errors.Add($"{label} repeats reference landmark '{row.ReferenceLandmarkId.Trim()}'.");
            }

            if (row.ReferencePosition is null || !IsFinite(row.ReferencePosition))
            {
                errors.Add($"{rowLabel} reference XYZ position must be finite.");
            }

            if (string.IsNullOrWhiteSpace(row.ReferenceFrameId))
            {
                errors.Add($"{rowLabel} reference frame ID is required.");
            }
            else
            {
                referenceFrames.Add(row.ReferenceFrameId.Trim());
                if (descriptor is not null
                    && !string.Equals(row.ReferenceFrameId.Trim(), descriptor.ReferenceFrameId.Trim(), StringComparison.Ordinal))
                {
                    errors.Add($"{rowLabel} reference frame must match the correspondence descriptor.");
                }
            }

            correspondenceRows.Add((selection.Id, label, row));
        }

        if (referenceFrames.Count > 1)
        {
            errors.Add($"{label} correspondence rows must use one explicit reference frame.");
        }

        if (isCurrentSchema && rows.Count != 4)
        {
            errors.Add($"{label} Landmark Correspondence v1 requires exactly four rows.");
        }
        else if (rows.Count < 4)
        {
            warnings.Add($"{label} is taught only: at least four correspondence rows are required before XYZ affine execution.");
        }
    }

    private static void ValidateCorrespondenceDescriptor(
        ToolRecipeLandmarkCorrespondenceDescriptor? descriptor,
        string label,
        List<string> errors)
    {
        if (descriptor is null)
        {
            errors.Add($"{label} Landmark Correspondence v1 descriptor is required in schema {ToolRecipeDocument.CurrentSchemaVersion}.");
            return;
        }

        if (string.IsNullOrWhiteSpace(descriptor.ReferenceFrameId)) errors.Add($"{label} reference frame ID is required.");
        if (string.IsNullOrWhiteSpace(descriptor.ReferenceUnit)) errors.Add($"{label} reference unit is required.");
        if (string.IsNullOrWhiteSpace(descriptor.ReferenceProvenance)) errors.Add($"{label} reference provenance is required.");
        if (string.IsNullOrWhiteSpace(descriptor.ReferenceRevision)) errors.Add($"{label} reference revision is required.");
        if (!string.Equals(descriptor.PairCountPolicy, "ExactlyFour", StringComparison.Ordinal)) errors.Add($"{label} PairCountPolicy must be ExactlyFour.");
        if (!string.Equals(descriptor.SourceArtifactPolicy, "CurrentPublishedCornerAnchor", StringComparison.Ordinal)) errors.Add($"{label} SourceArtifactPolicy must be CurrentPublishedCornerAnchor.");
        if (!string.Equals(descriptor.AffineIndependencePolicy, "RequireNonDegenerateTetrahedra", StringComparison.Ordinal)) errors.Add($"{label} AffineIndependencePolicy must be RequireNonDegenerateTetrahedra.");
        if (descriptor.MinimumNormalizedTetrahedronVolume is not { } minimum
            || !double.IsFinite(minimum) || minimum <= 0d || minimum >= 1d)
        {
            errors.Add($"{label} MinimumNormalizedTetrahedronVolume must be finite, greater than zero, and less than one.");
        }
    }

    private static bool AreCollinear(ToolRecipeXyz first, ToolRecipeXyz second, ToolRecipeXyz third)
    {
        var abX = second.X - first.X;
        var abY = second.Y - first.Y;
        var abZ = second.Z - first.Z;
        var acX = third.X - first.X;
        var acY = third.Y - first.Y;
        var acZ = third.Z - first.Z;
        var crossX = abY * acZ - abZ * acY;
        var crossY = abZ * acX - abX * acZ;
        var crossZ = abX * acY - abY * acX;
        return crossX == 0.0 && crossY == 0.0 && crossZ == 0.0;
    }

    private static bool IsFinite(ToolRecipeXyz point) =>
        double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z);

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static bool HasItems<T>(IReadOnlyList<T>? values) => values is { Count: > 0 };

    private static void AddIdentity(HashSet<string> identities, string? id, string kind, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        if (!identities.Add(id.Trim()))
        {
            errors.Add($"ID '{id.Trim()}' is duplicated ({kind}).");
        }
    }

    private static void AddRoutableEntity(HashSet<string> available, string? id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            available.Add(id.Trim());
        }
    }

    private static string Clean(string? value) => string.IsNullOrWhiteSpace(value) ? "<missing>" : value.Trim();
}
