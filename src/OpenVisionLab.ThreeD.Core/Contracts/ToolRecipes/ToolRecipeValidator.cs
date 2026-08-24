namespace OpenVisionLab.ThreeD.Core;

public static class ToolRecipeValidator
{
    private const string GridCellLocatorKind = "grid-cell";

    public static ToolRecipeValidationResult Validate(ToolRecipeDocument? document) =>
        ValidateCore(document, requireSourcePath: true, requireInspectionStep: true, allowIncompleteSteps: false, requiredStepId: null);

    public static ToolRecipeValidationResult ValidateForStorage(ToolRecipeDocument? document) =>
        ValidateCore(document, requireSourcePath: false, requireInspectionStep: false, allowIncompleteSteps: true, requiredStepId: null);

    public static ToolRecipeValidationResult ValidateForStepExecution(
        ToolRecipeDocument? document,
        string stepId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
        return ValidateCore(
            document,
            requireSourcePath: true,
            requireInspectionStep: true,
            allowIncompleteSteps: true,
            requiredStepId: stepId.Trim());
    }

    private static ToolRecipeValidationResult ValidateCore(
        ToolRecipeDocument? document,
        bool requireSourcePath,
        bool requireInspectionStep,
        bool allowIncompleteSteps,
        string? requiredStepId)
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
        var isArtifactOwnedSelectionSchema = string.Equals(
            document.SchemaVersion,
            ToolRecipeDocument.ArtifactOwnedSelectionSchemaVersion,
            StringComparison.Ordinal);
        var isOrientedBox3DSchema = string.Equals(
            document.SchemaVersion,
            ToolRecipeDocument.OrientedBox3DSchemaVersion,
            StringComparison.Ordinal);
        var isDualRoiRoutingSchema = string.Equals(
            document.SchemaVersion,
            ToolRecipeDocument.DualRoiRoutingSchemaVersion,
            StringComparison.Ordinal);
        var isGridCircleSchema = string.Equals(
            document.SchemaVersion,
            ToolRecipeDocument.GridCircleSchemaVersion,
            StringComparison.Ordinal);
        var isCurrentSchema = string.Equals(
            document.SchemaVersion,
            ToolRecipeDocument.CurrentSchemaVersion,
            StringComparison.Ordinal);
        if (!isLegacySchema
            && !isSelectionSchema
            && !isGenericMeasurementSchema
            && !isArtifactOwnedSelectionSchema
            && !isOrientedBox3DSchema
            && !isDualRoiRoutingSchema
            && !isGridCircleSchema
            && !isCurrentSchema)
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
        if (requireSourcePath && string.IsNullOrWhiteSpace(source.Path)) errors.Add("Source path is required.");
        if (source.ByteLength is <= 0) errors.Add("Source byte length must be positive when recorded.");
        if (source.ContentSha256 is not null && !IsSha256(source.ContentSha256))
        {
            errors.Add("Source SHA-256 must contain exactly 64 hexadecimal characters when recorded.");
        }
        if (source.GridWidth is <= 0 || source.GridHeight is <= 0)
        {
            errors.Add("Source grid dimensions must be positive when recorded.");
        }
        if (source.AcquisitionProvenance is { } acquisition)
        {
            if (!Enum.IsDefined(acquisition.State))
            {
                errors.Add("Acquisition provenance state must be Available or Unavailable.");
            }
            if (string.IsNullOrWhiteSpace(acquisition.Evidence))
            {
                errors.Add("Acquisition provenance evidence is required when the contract is recorded.");
            }
            if (string.IsNullOrWhiteSpace(acquisition.LimitationNotes))
            {
                errors.Add("Acquisition provenance limitation notes are required when the contract is recorded.");
            }
            if (acquisition.AcquisitionDirection is { } direction)
            {
                ValidateAcquisitionDirection(source, acquisition, direction, errors);
            }
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
                isGenericMeasurementSchema || ToolRecipeDocument.SupportsArtifactOwnedSelections(document.SchemaVersion),
                ToolRecipeDocument.SupportsArtifactOwnedSelections(document.SchemaVersion),
                ToolRecipeDocument.SupportsOrientedBox3D(document.SchemaVersion),
                ToolRecipeDocument.SupportsGridCircle(document.SchemaVersion),
                ToolRecipeDocument.SupportsGridPolygon(document.SchemaVersion),
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
        if (requireInspectionStep && steps.Count == 0)
        {
            errors.Add("At least one taught tool step is required.");
        }
        if (requiredStepId is not null
            && steps.Count(step => string.Equals(step?.Id, requiredStepId, StringComparison.OrdinalIgnoreCase)) != 1)
        {
            errors.Add($"Teaching recipe must contain exactly one step with ID '{requiredStepId}'.");
        }

        var outputStepIndices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var outputContracts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
            var hasMinimumInputs = inputs.Count >= minimumInputCount;
            var requiresCompleteStep = !allowIncompleteSteps
                || string.Equals(step.Id, requiredStepId, StringComparison.OrdinalIgnoreCase);
            var validateStepContract = !allowIncompleteSteps || hasMinimumInputs;
            if (!hasMinimumInputs && requiresCompleteStep)
            {
                errors.Add($"{label} '{Clean(step.ToolName)}' requires {minimumInputCount} input entity ID(s).");
            }
            else if (!hasMinimumInputs)
            {
                warnings.Add($"{label} '{Clean(step.ToolName)}' is saved as an incomplete draft ({inputs.Count}/{minimumInputCount} inputs).");
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

            ValidateDualRoiRouting(
                step,
                inputs,
                selections,
                ToolRecipeDocument.SupportsDualRoiRouting(document.SchemaVersion),
                label,
                errors);

            if (validateStepContract)
            {
                foreach (var selectionError in ToolRecipeSelectionContract.Validate(
                             step,
                             selections,
                             requireAllRoles: requiresCompleteStep))
                {
                    errors.Add($"{label} {selectionError}");
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
                outputContracts.TryAdd(
                    step.OutputEntityId.Trim(),
                    ToolRecipePrimaryInputContract.GetProducedContract(step.ToolId));
            }

            foreach (var parameter in step.Parameters ?? [])
            {
                if (parameter is null || string.IsNullOrWhiteSpace(parameter.Name))
                {
                    errors.Add($"{label} '{Clean(step.ToolName)}' has a parameter without a name.");
                }
            }

            if (validateStepContract && string.Equals(step.ToolId, "filter", StringComparison.OrdinalIgnoreCase))
            {
                ValidateFilterStep(step, inputs, source, label, errors, warnings);
            }

            if (validateStepContract
                && string.Equals(
                    step.ToolId,
                    "remove-outlier-pixels",
                    StringComparison.OrdinalIgnoreCase))
            {
                ValidateRemoveOutlierPixelsStep(
                    step,
                    inputs,
                    source,
                    label,
                    errors,
                    warnings);
            }

            if (validateStepContract
                && string.Equals(
                    step.ToolId,
                    "level-surface",
                    StringComparison.OrdinalIgnoreCase))
            {
                ValidateLevelSurfaceStep(
                    step,
                    inputs,
                    source,
                    selections,
                    label,
                    errors,
                    warnings);
            }

            if (validateStepContract
                && string.Equals(
                    step.ToolId,
                    "roi-crop",
                    StringComparison.OrdinalIgnoreCase))
            {
                ValidateRoiCropStep(
                    step,
                    inputs,
                    source,
                    selections,
                    label,
                    errors);
            }

            if (string.Equals(step.ToolId, "xyz-affine-transform", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"{label} XYZ Affine is taught only: execution needs four affine-independent source/reference landmarks or a fixture-constrained contract.");
            }

            if (validateStepContract && string.Equals(step.ToolId, "xyz-affine-solve", StringComparison.OrdinalIgnoreCase))
            {
                ValidateXYZAffineSolveStep(step, inputs, label, errors);
            }

            if (validateStepContract && string.Equals(step.ToolId, "xyz-affine-apply", StringComparison.OrdinalIgnoreCase))
            {
                ValidateXYZAffineApplyStep(step, inputs, source, label, errors);
            }

            if (validateStepContract && string.Equals(step.ToolId, "re-grid-height-map", StringComparison.OrdinalIgnoreCase))
            {
                ValidateRegridHeightMapStep(step, inputs, label, errors);
            }

            if (validateStepContract
                && step.ToolId is "thickness" or "warpage" or "plane-flatness" or "point-pair-dimensions" or "gap-flush" or "volume" or "cross-section-dimensions" or "completeness-grid")
            {
                ValidateHeightMeasurementPrimaryInput(
                    step,
                    inputs,
                    source,
                    outputContracts,
                    label,
                    allowIncompleteSteps,
                    errors,
                    warnings);
                ValidateHeightMeasurementStep(
                    step,
                    inputs,
                    source,
                    selections,
                    label,
                    ToolRecipeDocument.SupportsArtifactOwnedSelections(document.SchemaVersion),
                    errors);
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

    private static void ValidateAcquisitionDirection(
        ToolRecipeSource source,
        ToolRecipeAcquisitionProvenance acquisition,
        ToolRecipeAcquisitionDirection direction,
        List<string> errors)
    {
        if (!Enum.IsDefined(direction.State))
        {
            errors.Add("Acquisition direction state must be Available or Unavailable.");
        }
        if (!Enum.IsDefined(direction.Convention))
        {
            errors.Add("Acquisition direction convention must be SensorToScene.");
        }
        if (string.IsNullOrWhiteSpace(direction.FrameId)
            || !string.Equals(direction.FrameId, source.FrameId, StringComparison.Ordinal))
        {
            errors.Add("Acquisition direction frame must exactly match the source frame.");
        }

        if (direction.State == ToolRecipeAcquisitionDirectionState.Unavailable)
        {
            if (direction.Vector is not null)
            {
                errors.Add("Unavailable acquisition direction must not contain a vector.");
            }
            return;
        }

        if (acquisition.State != ToolRecipeAcquisitionProvenanceState.Available)
        {
            errors.Add("Available acquisition direction requires available acquisition provenance.");
        }
        if (direction.Vector is not { } vector
            || !double.IsFinite(vector.X)
            || !double.IsFinite(vector.Y)
            || !double.IsFinite(vector.Z))
        {
            errors.Add("Available acquisition direction requires a finite vector.");
            return;
        }

        var length = Math.Sqrt(
            vector.X * vector.X
            + vector.Y * vector.Y
            + vector.Z * vector.Z);
        if (!double.IsFinite(length) || Math.Abs(length - 1.0) > 1e-9)
        {
            errors.Add("Available acquisition direction vector must be normalized to unit length.");
        }
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

    private static void ValidateRemoveOutlierPixelsStep(
        ToolRecipeStep step,
        IReadOnlyList<string> inputs,
        ToolRecipeSource source,
        string label,
        List<string> errors,
        List<string> warnings)
    {
        if (inputs.Count != 1
            || !string.Equals(inputs[0], source.Id, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                $"{label} Remove Outlier Pixels v1 requires exactly the recipe C3D source as input.");
        }

        if (!string.Equals(source.Format, "C3D", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(source.Unit, "raw-height", StringComparison.Ordinal))
        {
            errors.Add($"{label} Remove Outlier Pixels v1 requires a C3D raw-height source.");
        }

        if (source.ByteLength is null
            || source.ContentSha256 is null
            || source.GridWidth is null
            || source.GridHeight is null)
        {
            errors.Add(
                $"{label} Remove Outlier Pixels v1 requires source byte length, SHA-256, width, and height identity.");
        }

        var parameters = step.Parameters ?? [];
        var expectedNames = new HashSet<string>(
            [
                "Rule",
                "WindowSize",
                "MaximumAbsoluteDeviation",
                "MinimumValidNeighbors",
                "MissingValuePolicy",
                "BoundaryPolicy",
                "OutlierPolicy"
            ],
            StringComparer.Ordinal);
        if (expectedNames.Any(
                name => parameters.Count(
                    parameter => parameter is not null && parameter.Name == name) != 1))
        {
            errors.Add(
                $"{label} Remove Outlier Pixels v1 requires Rule, WindowSize, MaximumAbsoluteDeviation, MinimumValidNeighbors, MissingValuePolicy, BoundaryPolicy, and OutlierPolicy.");
            return;
        }

        var unknownNames = parameters
            .Where(parameter => parameter is not null && !expectedNames.Contains(parameter.Name))
            .Select(parameter => parameter.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (unknownNames.Length > 0)
        {
            warnings.Add(
                $"{label} preserves unmapped Remove Outlier Pixels parameter(s): {string.Join(", ", unknownNames)}.");
        }

        string Value(string name) =>
            parameters.Single(parameter => parameter.Name == name).Value;

        if (!string.Equals(
                Value("Rule"),
                "LocalMedianAbsoluteDeviation",
                StringComparison.Ordinal))
        {
            errors.Add(
                $"{label} Remove Outlier Pixels v1 Rule must be 'LocalMedianAbsoluteDeviation'.");
        }

        if (Value("WindowSize") is not ("3" or "5" or "7"))
        {
            errors.Add(
                $"{label} Remove Outlier Pixels v1 WindowSize must be 3, 5, or 7.");
        }

        if (!double.TryParse(
                Value("MaximumAbsoluteDeviation"),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var maximumDeviation)
            || !double.IsFinite(maximumDeviation)
            || maximumDeviation <= 0d)
        {
            errors.Add(
                $"{label} Remove Outlier Pixels v1 MaximumAbsoluteDeviation must be finite and greater than zero.");
        }

        var maximumNeighbors = Value("WindowSize") switch
        {
            "3" => 8,
            "5" => 24,
            "7" => 48,
            _ => 0
        };
        if (!int.TryParse(
                Value("MinimumValidNeighbors"),
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var minimumNeighbors)
            || minimumNeighbors < 1
            || minimumNeighbors > maximumNeighbors)
        {
            errors.Add(
                $"{label} Remove Outlier Pixels v1 MinimumValidNeighbors must fit the selected WindowSize.");
        }

        if (!string.Equals(Value("MissingValuePolicy"), "PreserveMask", StringComparison.Ordinal))
        {
            errors.Add(
                $"{label} Remove Outlier Pixels v1 MissingValuePolicy must be 'PreserveMask'.");
        }

        if (!string.Equals(Value("BoundaryPolicy"), "AvailableNeighbors", StringComparison.Ordinal))
        {
            errors.Add(
                $"{label} Remove Outlier Pixels v1 BoundaryPolicy must be 'AvailableNeighbors'.");
        }

        if (!string.Equals(Value("OutlierPolicy"), "SetMissing", StringComparison.Ordinal))
        {
            errors.Add(
                $"{label} Remove Outlier Pixels v1 OutlierPolicy must be 'SetMissing'.");
        }
    }

    private static void ValidateLevelSurfaceStep(
        ToolRecipeStep step,
        IReadOnlyList<string> inputs,
        ToolRecipeSource source,
        IReadOnlyList<ToolRecipeSelection> selections,
        string label,
        List<string> errors,
        List<string> warnings)
    {
        if (inputs.Count < 2
            || !string.Equals(inputs[0], source.Id, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                $"{label} Level Surface v1 requires the recipe C3D source followed by one or more GridRectangle selections.");
        }
        else
        {
            foreach (var selectionId in inputs.Skip(1))
            {
                var selection = selections.SingleOrDefault(candidate =>
                    string.Equals(candidate.Id, selectionId, StringComparison.OrdinalIgnoreCase));
                if (selection?.GridRectangle is null
                    || !string.Equals(
                        selection.Kind,
                        ToolRecipeSelectionKinds.GridRectangle,
                        StringComparison.Ordinal))
                {
                    errors.Add(
                        $"{label} Level Surface reference input '{selectionId}' must be a recipe-owned GridRectangle.");
                }
            }
        }

        if (!string.Equals(source.Format, "C3D", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(source.Unit, "raw-height", StringComparison.Ordinal)
            || source.ByteLength is null
            || source.ContentSha256 is null
            || source.GridWidth is null
            || source.GridHeight is null)
        {
            errors.Add(
                $"{label} Level Surface v1 requires a fully identified C3D raw-height source.");
        }

        var parameters = step.Parameters ?? [];
        var expectedNames = new HashSet<string>(
            [
                "ReferenceFitPolicy",
                "LevelingPolicy",
                "MissingValuePolicy",
                "GridPolicy",
                "MinimumValidSampleCount",
                "MaximumReferenceRmsResidual"
            ],
            StringComparer.Ordinal);
        if (expectedNames.Any(name => parameters.Count(
                parameter => parameter is not null && parameter.Name == name) != 1))
        {
            errors.Add(
                $"{label} Level Surface v1 requires exactly ReferenceFitPolicy, LevelingPolicy, MissingValuePolicy, GridPolicy, MinimumValidSampleCount, and MaximumReferenceRmsResidual.");
            return;
        }
        var unknownNames = parameters
            .Where(parameter => parameter is not null && !expectedNames.Contains(parameter.Name))
            .Select(parameter => parameter.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (unknownNames.Length > 0)
        {
            warnings.Add(
                $"{label} preserves unmapped Level Surface parameter(s): {string.Join(", ", unknownNames)}.");
        }
        string Value(string name) =>
            parameters.Single(parameter => parameter.Name == name).Value;
        if (Value("ReferenceFitPolicy") != "LeastSquaresHeightPlane"
            || Value("LevelingPolicy") != "HeightDetrendToReferenceMean"
            || Value("MissingValuePolicy") != "PreserveMask"
            || Value("GridPolicy") != "PreserveSourceGrid")
        {
            errors.Add($"{label} Level Surface v1 fixed policies are invalid.");
        }
        if (!int.TryParse(
                Value("MinimumValidSampleCount"),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var minimumSamples)
            || minimumSamples < 3)
        {
            errors.Add(
                $"{label} Level Surface v1 MinimumValidSampleCount must be at least three.");
        }
        if (!double.TryParse(
                Value("MaximumReferenceRmsResidual"),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var maximumRms)
            || !double.IsFinite(maximumRms)
            || maximumRms <= 0)
        {
            errors.Add(
                $"{label} Level Surface v1 MaximumReferenceRmsResidual must be finite and greater than zero.");
        }
    }

    private static void ValidateDualRoiRouting(
        ToolRecipeStep step,
        IReadOnlyList<string> inputs,
        IReadOnlyList<ToolRecipeSelection> selections,
        bool supportsDualRoiRouting,
        string label,
        List<string> errors)
    {
        if (step.DualRoiRouting is not { } routing)
        {
            return;
        }

        if (!supportsDualRoiRouting)
        {
            errors.Add(
                $"{label} dual-ROI role routing requires teaching recipe schema "
                + $"{ToolRecipeDocument.DualRoiRoutingSchemaVersion}.");
            return;
        }

        if (step.ToolId is not ("thickness" or "plane-flatness" or "gap-flush" or "volume" or "completeness-grid"))
        {
            errors.Add($"{label} '{Clean(step.ToolName)}' cannot declare dual-ROI role routing.");
            return;
        }

        var firstId = CleanOptionalIdentity(routing.FirstRegionSelectionId);
        var secondId = CleanOptionalIdentity(routing.SecondRegionSelectionId);
        if (firstId is not null
            && secondId is not null
            && string.Equals(firstId, secondId, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"{label} dual-ROI first and second roles must use distinct selections.");
        }

        var expectedRegionInputs = new[] { firstId, secondId }
            .Where(id => id is not null)
            .Cast<string>()
            .ToArray();
        var actualRegionInputs = inputs.Skip(1).ToArray();
        if (!actualRegionInputs.SequenceEqual(expectedRegionInputs, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add(
                $"{label} dual-ROI route must be ordered as the first input, "
                + "then the declared first and second region selections.");
        }

        foreach (var (role, selectionId) in new[]
                 {
                     ("first", firstId),
                     ("second", secondId)
                 })
        {
            if (selectionId is null)
            {
                continue;
            }

            var selection = selections.SingleOrDefault(candidate =>
                candidate is not null
                && string.Equals(candidate.Id, selectionId, StringComparison.OrdinalIgnoreCase));
            if (selection?.Kind != ToolRecipeSelectionKinds.GridRectangle
                || selection.GridRectangle is null)
            {
                errors.Add(
                    $"{label} dual-ROI {role} role '{selectionId}' must reference "
                    + "one declared GridRectangle.");
            }
        }
    }

    private static string? CleanOptionalIdentity(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateHeightMeasurementPrimaryInput(
        ToolRecipeStep step,
        IReadOnlyList<string> inputs,
        ToolRecipeSource source,
        IReadOnlyDictionary<string, string> outputContracts,
        string label,
        bool preserveRepairableDraft,
        List<string> errors,
        List<string> warnings)
    {
        if (inputs.Count == 0
            || !ToolRecipePrimaryInputContract.TryGetRequiredContract(
                step.ToolId,
                out var requiredContract))
        {
            return;
        }

        var inputId = inputs[0];
        var actualContract = string.Equals(inputId, source.Id, StringComparison.OrdinalIgnoreCase)
            ? "SourceC3D / RawHeightField"
            : outputContracts.GetValueOrDefault(inputId);
        if (actualContract is null
            || ToolRecipePrimaryInputContract.IsCompatible(step.ToolId, actualContract))
        {
            return;
        }

        var message = $"{label} {Clean(step.ToolName)} first input '{inputId}' is {actualContract}; {requiredContract} is required.";
        (preserveRepairableDraft ? warnings : errors).Add(message);
    }

    private static void ValidateRoiCropStep(
        ToolRecipeStep step,
        IReadOnlyList<string> inputs,
        ToolRecipeSource source,
        IReadOnlyList<ToolRecipeSelection> selections,
        string label,
        List<string> errors)
    {
        if (inputs.Count != 2
            || !string.Equals(inputs[0], source.Id, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                $"{label} ROI / Crop v1 requires the recipe C3D source followed by one GridRectangle selection.");
        }
        else
        {
            var selection = selections.SingleOrDefault(candidate =>
                string.Equals(candidate.Id, inputs[1], StringComparison.OrdinalIgnoreCase));
            if (selection?.GridRectangle is null
                || !string.Equals(
                    selection.Kind,
                    ToolRecipeSelectionKinds.GridRectangle,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    $"{label} ROI / Crop input '{inputs[1]}' must be a recipe-owned GridRectangle.");
            }
        }

        if (!string.Equals(source.Format, "C3D", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(source.Unit, "raw-height", StringComparison.Ordinal)
            || source.ByteLength is null
            || source.ContentSha256 is null
            || source.GridWidth is null
            || source.GridHeight is null)
        {
            errors.Add(
                $"{label} ROI / Crop v1 requires a fully identified C3D raw-height source.");
        }

        var parameters = step.Parameters ?? [];
        if (parameters.Count != 2
            || parameters.Count(parameter => parameter is not null
                && parameter.Name == "ROI"
                && parameter.Value == "Select in Viewer") != 1
            || parameters.Count(parameter => parameter is not null
                && parameter.Name == "Output frame"
                && parameter.Value == "Keep source frame") != 1)
        {
            errors.Add(
                $"{label} ROI / Crop v1 requires its fixed ROI and source-frame policies without unknown parameters.");
        }
    }

    private static void ValidateHeightMeasurementStep(
        ToolRecipeStep step,
        IReadOnlyList<string> inputs,
        ToolRecipeSource source,
        IReadOnlyList<ToolRecipeSelection> selections,
        string label,
        bool supportsArtifactOwnedSelections,
        List<string> errors)
    {
        var isThickness = step.ToolId == "thickness";
        var isPlaneFlatness = step.ToolId == "plane-flatness";
        var isPointPair = step.ToolId == "point-pair-dimensions";
        var isGapFlush = step.ToolId == "gap-flush";
        var isVolume = step.ToolId == "volume";
        var isCrossSection = step.ToolId == "cross-section-dimensions";
        var isCompleteness = step.ToolId == "completeness-grid";
        var isDualRoi = isThickness || isPlaneFlatness || isGapFlush || isVolume || isCompleteness;
        var expectedInputCount = isDualRoi ? 3 : 2;
        if (inputs.Count != expectedInputCount)
        {
            errors.Add(isThickness && inputs.Count == 2 && !supportsArtifactOwnedSelections
                ? $"{label} legacy one-ROI Thickness preserves its Measurement ROI but requires a Reference ROI first."
                : isDualRoi
                ? $"{label} {Clean(step.ToolName)} v1 requires one HeightField and two ordered GridRectangles: Reference ROI, then {(isCompleteness ? "Inspection Grid ROI" : "Measurement ROI")}."
                : isPointPair
                    ? $"{label} Point Pair Dimensions v1 requires one TransformedHeightField and one ordered PointSet(2)."
                : $"{label} {Clean(step.ToolName)} v1 requires one HeightField first and one GridRectangle second.");
            return;
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
            else if (isCrossSection && selection!.GridRectangle is not { RowCount: 1, ColumnCount: >= 2 })
            {
                errors.Add($"{label} Cross-section Dimensions v1 requires one GridRectangle spanning exactly one row and at least two columns.");
            }
            else if (string.Equals(inputs[0], source.Id, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(selection!.SourceBinding.Format, "C3D", StringComparison.OrdinalIgnoreCase)
                    || !string.IsNullOrWhiteSpace(selection.SourceBinding.OwnerEntityId))
                {
                    errors.Add($"{label} {Clean(step.ToolName)} raw C3D input requires a source-owned C3D selection.");
                }
            }
            else if (!ToolRecipePrimaryInputContract.TryGetRequiredContract(step.ToolId, out var requiredContract)
                || (string.Equals(requiredContract, "TransformedHeightField", StringComparison.Ordinal)
                    ? !string.Equals(selection!.SourceBinding.Format, "TransformedHeightField", StringComparison.Ordinal)
                    : selection!.SourceBinding.Format is not ("HeightField" or "TransformedHeightField"))
                || !string.Equals(selection.SourceBinding.OwnerEntityId, inputs[0], StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{label} {Clean(step.ToolName)} artifact input requires selections owned by its compatible first-input HeightField.");
            }
        }
        var expected = step.ToolId switch
        {
            "thickness" => new[] { "MinimumThickness", "MaximumThickness", "MinimumValidSampleCount" },
            "warpage" => new[] { "MaximumPeakToValley", "MaximumRms", "MinimumValidSampleCount" },
            "point-pair-dimensions" => new[] { "ExpectedDistance", "DistanceTolerance", "ExpectedPlanarWidth", "PlanarWidthTolerance", "ExpectedElevationAngleDegrees", "ElevationAngleToleranceDegrees" },
            "gap-flush" => new[] { "ExpectedGap", "GapTolerance", "ExpectedFlush", "FlushTolerance" },
            "volume" => new[] { "ExpectedNetVolume", "VolumeTolerance" },
            "cross-section-dimensions" => new[] { "ExpectedWidth", "WidthTolerance", "ExpectedHeightRange", "HeightTolerance" },
            "completeness-grid" => [],
            _ => new[] { "MaximumFlatness", "MinimumReferenceSampleCount", "MinimumMeasurementSampleCount" }
        };
        var parameters = step.Parameters ?? [];
        if (!isCompleteness
            && (parameters.Count != expected.Length
                || expected.Any(name =>
                    parameters.Count(parameter => parameter.Name == name) != 1)))
        {
            errors.Add($"{label} {Clean(step.ToolName)} v1 requires exactly {string.Join(", ", expected)}.");
        }
        else if (isCompleteness)
        {
            try
            {
                _ = C3DCompletenessGridProfile.FromRecipeParameters(parameters);
                _ = C3DCompletenessPresencePolicy.FromOptionalRecipeParameters(
                    parameters);
            }
            catch (Exception exception) when (
                exception is InvalidDataException
                or ArgumentException
                or OverflowException)
            {
                errors.Add($"{label} {exception.Message}");
            }
        }
    }

    private static void ValidateSelection(
        ToolRecipeSelection selection,
        ToolRecipeSource source,
        bool hasCorrespondenceDescriptor,
        bool supportsArtifactOwnedSelections,
        bool supportsOrientedBox3D,
        bool supportsGridCircle,
        bool supportsGridPolygon,
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
        var isArtifactBinding = binding.Format is "HeightField" or "TransformedHeightField";
        if (!isRawSourceBinding && !isArtifactBinding)
        {
            errors.Add($"{label} binding format must be C3D, HeightField, or TransformedHeightField.");
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
                errors.Add($"{label} artifact-owned binding requires recipe schema {ToolRecipeDocument.ArtifactOwnedSelectionSchemaVersion} or newer.");
            }
            if (selection.Kind is not (
                    ToolRecipeSelectionKinds.GridRectangle
                    or ToolRecipeSelectionKinds.PointSet
                    or ToolRecipeSelectionKinds.OrientedBox3D
                    or ToolRecipeSelectionKinds.GridCircle
                    or ToolRecipeSelectionKinds.GridPolygon))
            {
                errors.Add($"{label} artifact HeightField binding supports GridRectangle, GridCircle, GridPolygon, PointSet, or OrientedBox3D geometry only.");
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
                errors.Add($"{label} frame must match the owned artifact HeightField frame.");
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

        if (string.Equals(selection.Kind, ToolRecipeSelectionKinds.OrientedBox3D, StringComparison.Ordinal))
        {
            ValidateOrientedBox3D(selection, label, supportsOrientedBox3D, errors);
            return;
        }

        if (string.Equals(selection.Kind, ToolRecipeSelectionKinds.GridCircle, StringComparison.Ordinal))
        {
            ValidateGridCircle(selection, binding, label, supportsGridCircle, errors);
            return;
        }

        if (string.Equals(selection.Kind, ToolRecipeSelectionKinds.GridPolygon, StringComparison.Ordinal))
        {
            ValidateGridPolygon(selection, binding, label, supportsGridPolygon, errors);
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
        if (HasItems(selection.Points)
            || HasItems(selection.Rows)
            || selection.OrientedBox3D is not null
            || selection.GridCircle is not null
            || selection.GridPolygon is not null)
        {
            errors.Add($"{label} grid rectangle cannot contain circle, polygon, point-set, correspondence, or oriented-box payloads.");
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
        if (selection.GridRectangle is not null
            || HasItems(selection.Rows)
            || selection.OrientedBox3D is not null
            || selection.GridCircle is not null
            || selection.GridPolygon is not null)
        {
            errors.Add($"{label} point set cannot contain rectangle, circle, polygon, correspondence, or oriented-box payloads.");
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

    private static void ValidateOrientedBox3D(
        ToolRecipeSelection selection,
        string label,
        bool supportsOrientedBox3D,
        List<string> errors)
    {
        if (!supportsOrientedBox3D)
        {
            errors.Add(
                $"{label} OrientedBox3D requires teaching recipe schema {ToolRecipeDocument.OrientedBox3DSchemaVersion} or newer.");
        }

        if (selection.GridRectangle is not null
            || HasItems(selection.Points)
            || HasItems(selection.Rows)
            || selection.CorrespondenceDescriptor is not null
            || selection.GridCircle is not null
            || selection.GridPolygon is not null)
        {
            errors.Add(
                $"{label} oriented box cannot contain rectangle, circle, polygon, point-set, or correspondence payloads.");
        }

        foreach (var geometryError in ToolRecipeOrientedBox3DGeometry.Validate(selection.OrientedBox3D))
        {
            errors.Add($"{label} {geometryError}.");
        }
    }

    private static void ValidateGridCircle(
        ToolRecipeSelection selection,
        ToolRecipeSelectionSourceBinding binding,
        string label,
        bool supportsGridCircle,
        List<string> errors)
    {
        if (!supportsGridCircle)
        {
            errors.Add(
                $"{label} GridCircle requires teaching recipe schema {ToolRecipeDocument.GridCircleSchemaVersion} or newer.");
        }

        if (selection.GridRectangle is not null
            || HasItems(selection.Points)
            || HasItems(selection.Rows)
            || selection.CorrespondenceDescriptor is not null
            || selection.OrientedBox3D is not null
            || selection.GridPolygon is not null)
        {
            errors.Add(
                $"{label} grid circle cannot contain rectangle, polygon, point-set, correspondence, or oriented-box payloads.");
        }

        foreach (var geometryError in ToolRecipeGridCircleGeometry.Validate(
                     selection.GridCircle,
                     binding.GridWidth,
                     binding.GridHeight))
        {
            errors.Add($"{label} {geometryError}.");
        }
    }

    private static void ValidateGridPolygon(
        ToolRecipeSelection selection,
        ToolRecipeSelectionSourceBinding binding,
        string label,
        bool supportsGridPolygon,
        List<string> errors)
    {
        if (!supportsGridPolygon)
        {
            errors.Add(
                $"{label} GridPolygon requires teaching recipe schema {ToolRecipeDocument.GridPolygonSchemaVersion} or newer.");
        }

        if (selection.GridRectangle is not null
            || HasItems(selection.Points)
            || HasItems(selection.Rows)
            || selection.CorrespondenceDescriptor is not null
            || selection.OrientedBox3D is not null
            || selection.GridCircle is not null)
        {
            errors.Add(
                $"{label} grid polygon cannot contain rectangle, circle, point-set, correspondence, or oriented-box payloads.");
        }

        foreach (var geometryError in ToolRecipeGridPolygonGeometry.Validate(
                     selection.GridPolygon,
                     binding.GridWidth,
                     binding.GridHeight))
        {
            errors.Add($"{label} {geometryError}.");
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
        if (selection.GridRectangle is not null
            || HasItems(selection.Points)
            || selection.OrientedBox3D is not null
            || selection.GridCircle is not null
            || selection.GridPolygon is not null)
        {
            errors.Add($"{label} correspondence set cannot contain rectangle, circle, polygon, point-set, or oriented-box payloads.");
        }

        var descriptor = selection.CorrespondenceDescriptor;
        if (isCurrentSchema)
        {
            ValidateCorrespondenceDescriptor(descriptor, label, errors);
        }
        else if (descriptor is not null)
        {
            errors.Add($"{label} correspondence descriptor requires teaching recipe schema {ToolRecipeDocument.GenericMeasurementSchemaVersion} or newer.");
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
            errors.Add($"{label} Landmark Correspondence v1 descriptor is required in schema {ToolRecipeDocument.GenericMeasurementSchemaVersion} or newer.");
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
