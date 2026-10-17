namespace OpenVisionLab.ThreeD.Core;

public static class ToolRecipeValidator
{
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
        var isGridPolygonSchema = string.Equals(
            document.SchemaVersion,
            ToolRecipeDocument.GridPolygonSchemaVersion,
            StringComparison.Ordinal);
        var isOutputPolicySchema = string.Equals(
            document.SchemaVersion,
            ToolRecipeDocument.OutputPolicySchemaVersion,
            StringComparison.Ordinal);
        var isRecipeMetadataSchema = string.Equals(
            document.SchemaVersion,
            ToolRecipeDocument.RecipeMetadataSchemaVersion,
            StringComparison.Ordinal);
        var isAcceptanceLimitsSchema = document.SchemaVersion == ToolRecipeDocument.AcceptanceLimitsSchemaVersion;
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
            && !isGridPolygonSchema
            && !isOutputPolicySchema
            && !isRecipeMetadataSchema
            && !isAcceptanceLimitsSchema
            && !isCurrentSchema)
        {
            errors.Add($"Unsupported teaching recipe schema: {Clean(document.SchemaVersion)}.");
        }

        if (string.IsNullOrWhiteSpace(document.Name))
        {
            errors.Add("Recipe name is required.");
        }

        if (document.Category?.Length > ToolRecipeDocument.MaximumMetadataFieldLength
            || document.InspectionTarget?.Length > ToolRecipeDocument.MaximumMetadataFieldLength
            || document.Description?.Length > ToolRecipeDocument.MaximumMetadataFieldLength)
        {
            errors.Add($"Recipe category, inspection target, and description must each be {ToolRecipeDocument.MaximumMetadataFieldLength} characters or fewer.");
        }

        if (!isRecipeMetadataSchema && !isAcceptanceLimitsSchema && !isCurrentSchema
            && (document.Category is not null || document.InspectionTarget is not null || document.Description is not null))
        {
            errors.Add($"Recipe metadata requires teaching recipe schema {ToolRecipeDocument.RecipeMetadataSchemaVersion}.");
        }

        var groupNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var step in document.Steps ?? [])
        {
            if (step?.Group is not { } group) continue;
            if (!isCurrentSchema) errors.Add($"Step groups require teaching recipe schema {ToolRecipeDocument.GroupSchemaVersion}.");
            if (string.IsNullOrWhiteSpace(group.Id) || string.IsNullOrWhiteSpace(group.Name) || group.Name.Length > ToolRecipeDocument.MaximumMetadataFieldLength)
                errors.Add("Step group ID and name must be nonempty; name must fit the metadata field limit.");
            else if (groupNames.TryGetValue(group.Id, out var name) && name != group.Name)
                errors.Add($"Step group '{group.Id}' has conflicting names.");
            else groupNames[group.Id] = group.Name;
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
            if (acquisition.LimitationFlags is { } limitationFlags)
            {
                ValidateAcquisitionLimitationFlags(limitationFlags, errors);
            }
            if (acquisition.AcquisitionDirection is { } direction)
            {
                ValidateAcquisitionDirection(source, acquisition, direction, errors);
            }
        }
        if (source.MeasurementEvidence is { } measurementEvidence
            && !measurementEvidence.TryValidate(
                source.Unit,
                source.FrameId,
                source.SensorId,
                DateTimeOffset.UtcNow,
                out var measurementValidationMessage))
        {
            errors.Add($"Source measurement evidence is invalid: {measurementValidationMessage}");
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

            ToolRecipeSelectionDocumentValidator.Validate(
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
            var isLegacyOneRoiThickness = step.ToolId == "thickness"
                && inputs.Count == 2
                && !ToolRecipeDocument.SupportsArtifactOwnedSelections(document.SchemaVersion);
            if (!hasMinimumInputs
                && requiresCompleteStep
                && !(validateStepContract && isLegacyOneRoiThickness))
            {
                errors.Add(
                    isLegacyOneRoiThickness
                        ? $"{label} legacy one-ROI Thickness preserves its Measurement ROI but requires a Reference ROI first."
                        : $"{label} '{Clean(step.ToolName)}' requires {minimumInputCount} input entity ID(s).");
            }
            else if (!hasMinimumInputs)
            {
                warnings.Add($"{label} '{Clean(step.ToolName)}' is saved as an incomplete draft ({inputs.Count}/{minimumInputCount} inputs).");
            }

            if (step.AcceptanceLimitsConfigured is not null && (!(isCurrentSchema || isAcceptanceLimitsSchema) || !ToolRecipeStep.RequiresAcceptanceLimits(step.ToolId)))
            {
                errors.Add($"{label} acceptance limits require a measurement tool and schema {ToolRecipeDocument.AcceptanceLimitsSchemaVersion}.");
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

            if (step.SignedDeltaSidecar is { } signedDeltaSidecar
                && !signedDeltaSidecar.TryValidateContract(
                    step.Id,
                    step.ToolId,
                    step.OutputEntityId,
                    artifact: null,
                    out var signedDeltaSidecarMessage))
            {
                errors.Add($"{label} signed-delta sidecar reference is invalid: {signedDeltaSidecarMessage}");
            }

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
                    "height-threshold-background-removal",
                    StringComparison.OrdinalIgnoreCase))
            {
                ValidateHeightThresholdBackgroundRemovalStep(
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
                    C3DSignedHeightDeltaRecipePolicy.ToolId,
                    StringComparison.OrdinalIgnoreCase))
            {
                ValidateSignedHeightDeltaBackgroundSubtractionStep(
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
                    "connected-region",
                    StringComparison.OrdinalIgnoreCase))
            {
                ValidateConnectedRegionStep(
                    step,
                    inputs,
                    outputContracts,
                    label,
                    errors,
                    warnings);
            }

            if (validateStepContract
                && string.Equals(
                    step.ToolId,
                    "editable-region",
                    StringComparison.OrdinalIgnoreCase))
            {
                ValidateEditableRegionStep(
                    step,
                    inputs,
                    outputContracts,
                    label,
                    errors,
                    warnings);
            }

            if (validateStepContract
                && string.Equals(
                    step.ToolId,
                    "domain-mask",
                    StringComparison.OrdinalIgnoreCase))
            {
                ValidateDomainMaskStep(
                    step,
                    inputs,
                    source,
                    outputContracts,
                    label,
                    errors);
            }

            if (validateStepContract
                && string.Equals(
                    step.ToolId,
                    "connected-region-gate",
                    StringComparison.OrdinalIgnoreCase))
            {
                ValidateConnectedRegionGateStep(
                    step,
                    inputs,
                    source,
                    outputContracts,
                    label,
                    errors);
            }

            if (validateStepContract
                && string.Equals(
                    step.ToolId,
                    "grid-polygon-domain",
                    StringComparison.OrdinalIgnoreCase))
            {
                ValidateGridPolygonDomainStep(
                    step,
                    inputs,
                    source,
                    selections,
                    outputContracts,
                    label,
                    errors);
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
                    outputContracts,
                    label,
                    ToolRecipeDocument.SupportsArtifactOwnedSelections(document.SchemaVersion),
                    errors);
            }

            if (validateStepContract
                && string.Equals(step.ToolId, "physical-edge-gap-flush", StringComparison.Ordinal))
            {
                ValidatePhysicalEdgeGapFlushStep(step, inputs, outputContracts, label, errors);
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

    private static void ValidateHeightThresholdBackgroundRemovalStep(
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
                $"{label} Height Threshold Background Removal v1 requires exactly the recipe C3D source as input.");
        }

        if (!string.Equals(source.Format, "C3D", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(source.Unit, "raw-height", StringComparison.Ordinal))
        {
            errors.Add($"{label} Height Threshold Background Removal v1 requires a C3D raw-height source.");
        }

        if (source.ByteLength is null
            || source.ContentSha256 is null
            || source.GridWidth is null
            || source.GridHeight is null)
        {
            errors.Add(
                $"{label} Height Threshold Background Removal v1 requires source byte length, SHA-256, width, and height identity.");
        }

        var parameters = step.Parameters ?? [];
        var expectedNames = new HashSet<string>(
            [
                "Threshold",
                "Mode",
                "ComparisonPolicy",
                "MissingValuePolicy",
                "BackgroundPolicy"
            ],
            StringComparer.Ordinal);
        if (expectedNames.Any(
                name => parameters.Count(
                    parameter => parameter is not null && parameter.Name == name) != 1))
        {
            errors.Add(
                $"{label} Height Threshold Background Removal v1 requires Threshold, Mode, ComparisonPolicy, MissingValuePolicy, and BackgroundPolicy exactly once.");
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
                $"{label} preserves unmapped Height Threshold Background Removal parameter(s): {string.Join(", ", unknownNames)}.");
        }

        string Value(string name) =>
            parameters.Single(parameter => parameter.Name == name).Value;

        if (!double.TryParse(
                Value("Threshold"),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var threshold)
            || !double.IsFinite(threshold))
        {
            errors.Add($"{label} Height Threshold Background Removal v1 Threshold must be finite.");
        }

        if (!Enum.TryParse<C3DHeightThresholdBackgroundRemovalMode>(
                Value("Mode"),
                ignoreCase: false,
                out var mode)
            || !Enum.IsDefined(mode))
        {
            errors.Add($"{label} Height Threshold Background Removal v1 Mode is invalid.");
        }

        if (!string.Equals(
                Value("ComparisonPolicy"),
                C3DHeightThresholdBackgroundRemovalEvidence.ComparisonPolicyName,
                StringComparison.Ordinal))
        {
            errors.Add(
                $"{label} Height Threshold Background Removal v1 ComparisonPolicy must be '{C3DHeightThresholdBackgroundRemovalEvidence.ComparisonPolicyName}'.");
        }

        if (!string.Equals(
                Value("MissingValuePolicy"),
                C3DHeightThresholdBackgroundRemovalEvidence.MissingValuePolicyName,
                StringComparison.Ordinal))
        {
            errors.Add(
                $"{label} Height Threshold Background Removal v1 MissingValuePolicy must be '{C3DHeightThresholdBackgroundRemovalEvidence.MissingValuePolicyName}'.");
        }

        if (!string.Equals(
                Value("BackgroundPolicy"),
                C3DHeightThresholdBackgroundRemovalEvidence.BackgroundPolicyName,
                StringComparison.Ordinal))
        {
            errors.Add(
                $"{label} Height Threshold Background Removal v1 BackgroundPolicy must be '{C3DHeightThresholdBackgroundRemovalEvidence.BackgroundPolicyName}'.");
        }
    }

    private static void ValidateSignedHeightDeltaBackgroundSubtractionStep(
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
                $"{label} Signed Delta Background Subtraction requires exactly the recipe C3D source as its current input.");
        }

        if (!string.Equals(source.Format, "C3D", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(source.Unit, "raw-height", StringComparison.Ordinal))
        {
            errors.Add($"{label} Signed Delta Background Subtraction requires a C3D raw-height source.");
        }

        if (source.ByteLength is null
            || source.ContentSha256 is null
            || source.GridWidth is null
            || source.GridHeight is null)
        {
            errors.Add(
                $"{label} Signed Delta Background Subtraction requires source byte length, SHA-256, width, and height identity.");
        }

        if (!C3DSignedHeightDeltaRecipePolicy.TryRead(step.Parameters, out var reference, out var message))
        {
            errors.Add($"{label} {message}");
            return;
        }

        if (string.Equals(reference.EntityId, source.Id, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"{label} Signed Delta Background Subtraction background entity ID must differ from the current source ID.");
        }

        if (reference.Width != source.GridWidth || reference.Height != source.GridHeight)
        {
            errors.Add($"{label} Signed Delta Background Subtraction background dimensions must match the recipe source grid.");
        }

        if (!string.Equals(reference.Unit, source.Unit, StringComparison.Ordinal)
            || !string.Equals(reference.FrameId, source.FrameId, StringComparison.Ordinal))
        {
            errors.Add($"{label} Signed Delta Background Subtraction background unit and frame must match the recipe source.");
        }

        var unknownNames = (step.Parameters ?? [])
            .Where(parameter => parameter is not null && !C3DSignedHeightDeltaRecipePolicy.ParameterNames.Contains(parameter.Name, StringComparer.Ordinal))
            .Select(parameter => parameter.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (unknownNames.Length > 0)
        {
            warnings.Add($"{label} preserves unmapped Signed Delta Background Subtraction parameter(s): {string.Join(", ", unknownNames)}.");
        }
    }

    private static void ValidateConnectedRegionStep(
        ToolRecipeStep step,
        IReadOnlyList<string> inputs,
        IReadOnlyDictionary<string, string> outputContracts,
        string label,
        List<string> errors,
        List<string> warnings)
    {
        if (inputs.Count != 1)
        {
            errors.Add(
                $"{label} Connected Region v1 requires exactly one earlier FilteredHeightField input.");
        }
        else if (!outputContracts.TryGetValue(inputs[0], out var contract)
            || !string.Equals(contract, "FilteredHeightField", StringComparison.Ordinal))
        {
            errors.Add(
                $"{label} Connected Region v1 input '{inputs[0]}' must be an earlier FilteredHeightField artifact.");
        }

        var parameters = step.Parameters ?? [];
        var expectedNames = new HashSet<string>(
            [
                "Connectivity",
                "OriginX",
                "OriginY",
                "ColumnPitch",
                "RowPitch",
                "AreaUnit"
            ],
            StringComparer.Ordinal);
        if (expectedNames.Any(
                name => parameters.Count(
                    parameter => parameter is not null && parameter.Name == name) != 1))
        {
            errors.Add(
                $"{label} Connected Region v1 requires Connectivity, OriginX, OriginY, ColumnPitch, RowPitch, and AreaUnit.");
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
                $"{label} preserves unmapped Connected Region parameter(s): {string.Join(", ", unknownNames)}.");
        }

        string Value(string name) =>
            parameters.Single(parameter => parameter.Name == name).Value;

        if (Value("Connectivity") is not ("Four" or "Eight"))
        {
            errors.Add(
                $"{label} Connected Region v1 Connectivity must be Four or Eight.");
        }

        if (!double.TryParse(
                Value("OriginX"),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var originX)
            || !double.IsFinite(originX))
        {
            errors.Add(
                $"{label} Connected Region v1 OriginX must be finite.");
        }

        if (!double.TryParse(
                Value("OriginY"),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var originY)
            || !double.IsFinite(originY))
        {
            errors.Add(
                $"{label} Connected Region v1 OriginY must be finite.");
        }

        if (!double.TryParse(
                Value("ColumnPitch"),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var columnPitch)
            || !double.IsFinite(columnPitch)
            || columnPitch <= 0d)
        {
            errors.Add(
                $"{label} Connected Region v1 ColumnPitch must be finite and greater than zero.");
        }

        if (!double.TryParse(
                Value("RowPitch"),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var rowPitch)
            || !double.IsFinite(rowPitch)
            || rowPitch <= 0d)
        {
            errors.Add(
                $"{label} Connected Region v1 RowPitch must be finite and greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(Value("AreaUnit")))
        {
            errors.Add(
                $"{label} Connected Region v1 AreaUnit must not be empty.");
        }
    }

    private static void ValidateAcquisitionLimitationFlags(
        IReadOnlyList<ToolRecipeAcquisitionLimitationFlag> limitationFlags,
        List<string> errors)
    {
        var kinds = new HashSet<ToolRecipeAcquisitionLimitationKind>();
        foreach (var flag in limitationFlags)
        {
            if (flag is null
                || !Enum.IsDefined(flag.Kind)
                || !Enum.IsDefined(flag.Origin))
            {
                errors.Add("Acquisition limitation flags must contain defined kinds and origins.");
                continue;
            }

            if (!kinds.Add(flag.Kind))
            {
                errors.Add($"Acquisition limitation flag '{flag.Kind}' must not be repeated.");
            }
        }
    }

    private static void ValidateEditableRegionStep(
        ToolRecipeStep step,
        IReadOnlyList<string> inputs,
        IReadOnlyDictionary<string, string> outputContracts,
        string label,
        List<string> errors,
        List<string> warnings)
    {
        if (inputs.Count != 1)
        {
            errors.Add(
                $"{label} Editable Region v1 requires exactly one earlier ConnectedRegionArtifact input.");
        }
        else if (!outputContracts.TryGetValue(inputs[0], out var contract)
            || !string.Equals(contract, "ConnectedRegionArtifact", StringComparison.Ordinal))
        {
            errors.Add(
                $"{label} Editable Region v1 input '{inputs[0]}' must be an earlier ConnectedRegionArtifact.");
        }

        var parameters = step.Parameters ?? [];
        if (parameters.Count(parameter => parameter is not null
                && string.Equals(parameter.Name, "SelectedRegionIndex", StringComparison.Ordinal)) != 1)
        {
            errors.Add(
                $"{label} Editable Region v1 requires exactly one SelectedRegionIndex parameter.");
            return;
        }

        var unknownNames = parameters
            .Where(parameter => parameter is not null
                && !string.Equals(parameter.Name, "SelectedRegionIndex", StringComparison.Ordinal))
            .Select(parameter => parameter.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (unknownNames.Length > 0)
        {
            warnings.Add(
                $"{label} preserves unmapped Editable Region parameter(s): {string.Join(", ", unknownNames)}.");
        }

        var value = parameters.Single(parameter =>
            string.Equals(parameter.Name, "SelectedRegionIndex", StringComparison.Ordinal)).Value;
        if (!int.TryParse(
                value,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var index)
            || index < 0)
        {
            errors.Add(
                $"{label} Editable Region v1 SelectedRegionIndex must be a non-negative invariant integer.");
        }
    }

    private static void ValidateDomainMaskStep(
        ToolRecipeStep step,
        IReadOnlyList<string> inputs,
        ToolRecipeSource source,
        IReadOnlyDictionary<string, string> outputContracts,
        string label,
        List<string> errors)
    {
        if (inputs.Count == 2)
        {
            var sourceInput = string.Equals(inputs[0], source.Id, StringComparison.OrdinalIgnoreCase)
                || (outputContracts.TryGetValue(inputs[0], out var sourceContract)
                    && ToolRecipePrimaryInputContract.IsCompatible("domain-mask", sourceContract));
            if (!sourceInput)
            {
                errors.Add(
                    $"{label} Domain / Mask v1 first input '{inputs[0]}' must be a compatible HeightField artifact.");
            }

            if (!outputContracts.TryGetValue(inputs[1], out var domainContract)
                || (domainContract is not "ConnectedRegionArtifact" and not "GridDomainArtifact"))
            {
                errors.Add(
                    $"{label} Domain / Mask v1 second input '{inputs[1]}' must be an earlier ConnectedRegionArtifact or GridDomainArtifact.");
            }

            if ((step.Parameters ?? []).Count != 0)
            {
                errors.Add($"{label} Domain / Mask v1 does not accept parameters; the complete upstream domain is the contract.");
            }

            return;
        }

        if (inputs.Count != 3)
        {
            errors.Add(
                $"{label} Domain / Mask requires either the v1 HeightField + domain route or the v2 raw HeightField + filtered HeightField + ConnectedRegionArtifact route.");
            return;
        }

        if (!string.Equals(inputs[0], source.Id, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                $"{label} Domain / Mask v2 first input '{inputs[0]}' must be the recipe raw C3D source '{source.Id}'.");
        }

        if (!outputContracts.TryGetValue(inputs[1], out var detectionContract)
            || !string.Equals(detectionContract, "FilteredHeightField", StringComparison.Ordinal))
        {
            errors.Add(
                $"{label} Domain / Mask v2 second input '{inputs[1]}' must be an earlier Published FilteredHeightField used for detection.");
        }

        if (!outputContracts.TryGetValue(inputs[2], out var regionContract)
            || !string.Equals(regionContract, "ConnectedRegionArtifact", StringComparison.Ordinal))
        {
            errors.Add(
                $"{label} Domain / Mask v2 third input '{inputs[2]}' must be an earlier ConnectedRegionArtifact.");
        }

        var parameters = step.Parameters ?? [];
        if (parameters.Count(parameter => parameter is not null
                && string.Equals(parameter.Name, "SelectedRegionIndex", StringComparison.Ordinal)) != 1)
        {
            errors.Add(
                $"{label} Domain / Mask v2 requires exactly one SelectedRegionIndex parameter.");
            return;
        }

        if (!int.TryParse(
                parameters.Single(parameter => string.Equals(parameter.Name, "SelectedRegionIndex", StringComparison.Ordinal)).Value,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var regionIndex)
            || regionIndex < 0)
        {
            errors.Add(
                $"{label} Domain / Mask v2 SelectedRegionIndex must be a non-negative invariant integer.");
        }
    }

    private static void ValidateConnectedRegionGateStep(
        ToolRecipeStep step,
        IReadOnlyList<string> inputs,
        ToolRecipeSource source,
        IReadOnlyDictionary<string, string> outputContracts,
        string label,
        List<string> errors)
    {
        if (inputs.Count != 3)
        {
            errors.Add(
                $"{label} Connected Region Coverage/Height Gate v1 requires raw C3D, filtered detection HeightField, and ConnectedRegionArtifact inputs.");
            return;
        }

        if (!string.Equals(inputs[0], source.Id, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(source.Format, "C3D", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(source.Unit, "raw-height", StringComparison.Ordinal)
            || source.ByteLength is null
            || source.ContentSha256 is null
            || source.GridWidth is null
            || source.GridHeight is null)
        {
            errors.Add(
                $"{label} Connected Region Coverage/Height Gate v1 first input must be the fully identified raw C3D source '{source.Id}'.");
        }

        if (!outputContracts.TryGetValue(inputs[1], out var detectionContract)
            || !string.Equals(detectionContract, "FilteredHeightField", StringComparison.Ordinal))
        {
            errors.Add(
                $"{label} Connected Region Coverage/Height Gate v1 second input must be an earlier Published FilteredHeightField used for detection.");
        }

        if (!outputContracts.TryGetValue(inputs[2], out var regionContract)
            || !string.Equals(regionContract, "ConnectedRegionArtifact", StringComparison.Ordinal))
        {
            errors.Add(
                $"{label} Connected Region Coverage/Height Gate v1 third input must be an earlier ConnectedRegionArtifact.");
        }

        try
        {
            _ = C3DConnectedRegionGatePolicy.FromRecipeParameters(
                step.Parameters ?? [],
                source.Unit);
        }
        catch (Exception exception) when (
            exception is InvalidDataException
            or ArgumentException
            or OverflowException)
        {
            errors.Add($"{label} {exception.Message}");
        }
    }

    private static void ValidateGridPolygonDomainStep(
        ToolRecipeStep step,
        IReadOnlyList<string> inputs,
        ToolRecipeSource source,
        IReadOnlyList<ToolRecipeSelection> selections,
        IReadOnlyDictionary<string, string> outputContracts,
        string label,
        List<string> errors)
    {
        if (inputs.Count != 2)
        {
            errors.Add(
                $"{label} Grid Polygon Domain v1 requires exactly one HeightField input followed by one GridPolygon selection.");
        }
        else
        {
            var sourceInputIsRaw = string.Equals(inputs[0], source.Id, StringComparison.OrdinalIgnoreCase);
            var sourceInputIsCompatible = sourceInputIsRaw
                || (outputContracts.TryGetValue(inputs[0], out var sourceContract)
                    && ToolRecipePrimaryInputContract.IsCompatible("grid-polygon-domain", sourceContract));
            if (!sourceInputIsCompatible)
            {
                errors.Add(
                    $"{label} Grid Polygon Domain v1 first input '{inputs[0]}' must be a compatible HeightField artifact.");
            }

            var selection = selections.SingleOrDefault(candidate =>
                string.Equals(candidate.Id, inputs[1], StringComparison.OrdinalIgnoreCase));
            if (selection is null)
            {
                errors.Add(
                    $"{label} Grid Polygon Domain v1 second input '{inputs[1]}' must be a recipe-owned GridPolygon selection.");
            }
            else
            {
                if (!string.Equals(selection.Kind, ToolRecipeSelectionKinds.GridPolygon, StringComparison.Ordinal))
                {
                    errors.Add(
                        $"{label} Grid Polygon Domain v1 selection '{selection.Id}' must be a GridPolygon.");
                }

                var binding = selection.SourceBinding;
                if (sourceInputIsRaw)
                {
                    if (!string.Equals(binding.Format, source.Format, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(selection.RootSourceId, source.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add(
                            $"{label} Grid Polygon Domain v1 raw selection '{selection.Id}' must remain bound to the recipe C3D source.");
                    }
                }
                else if (!string.Equals(binding.Format, "HeightField", StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(binding.OwnerEntityId, inputs[0], StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(
                        $"{label} Grid Polygon Domain v1 derived selection '{selection.Id}' must bind to HeightField owner '{inputs[0]}'.");
                }
            }
        }

        if ((step.Parameters ?? []).Count != 0)
        {
            errors.Add($"{label} Grid Polygon Domain v1 does not accept parameters; the recipe-owned polygon is the contract.");
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
        IReadOnlyDictionary<string, string> outputContracts,
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
        var usesEditableRegionArtifact = isCompleteness
            && inputs.Count > 2
            && outputContracts.TryGetValue(inputs[2], out var inspectionContract)
            && string.Equals(inspectionContract, "EditableRegionArtifact", StringComparison.Ordinal);
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
            if (usesEditableRegionArtifact && inputIndex == 2)
            {
                continue;
            }
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
        if (usesEditableRegionArtifact
            && (!outputContracts.TryGetValue(inputs[2], out var contract)
                || !string.Equals(contract, "EditableRegionArtifact", StringComparison.Ordinal)))
        {
            errors.Add(
                $"{label} Completeness Grid editable-region input '{inputs[2]}' must be an earlier EditableRegionArtifact.");
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

    private static void ValidatePhysicalEdgeGapFlushStep(
        ToolRecipeStep step,
        IReadOnlyList<string> inputs,
        IReadOnlyDictionary<string, string> outputContracts,
        string label,
        List<string> errors)
    {
        if (step.MinimumInputCount != 4 || inputs.Count != 4)
        {
            errors.Add($"{label} {Clean(step.ToolName)} v1 requires four ordered inputs: FirstLine, SecondLine, FirstEdgePointSet, SecondEdgePointSet.");
        }
        else
        {
            if (string.Equals(inputs[0], inputs[1], StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{label} {Clean(step.ToolName)} FirstLine and SecondLine inputs must be distinct.");
            }
            if (string.Equals(inputs[2], inputs[3], StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{label} {Clean(step.ToolName)} FirstEdgePointSet and SecondEdgePointSet inputs must be distinct.");
            }

            for (var inputIndex = 0; inputIndex < inputs.Count; inputIndex++)
            {
                var input = inputs[inputIndex];
                var expectedContract = inputIndex < 2 ? "LineFeature" : "EdgePointSet";
                if (!outputContracts.TryGetValue(input, out var contract)
                    || !string.Equals(contract, expectedContract, StringComparison.Ordinal))
                {
                    errors.Add($"{label} {Clean(step.ToolName)} input '{Clean(input)}' must be an earlier {expectedContract} output.");
                }
            }

            if (outputContracts.TryGetValue(inputs[0], out var firstLineContract)
                && string.Equals(firstLineContract, "LineFeature", StringComparison.Ordinal)
                && outputContracts.TryGetValue(inputs[2], out var firstEdgeContract)
                && string.Equals(firstEdgeContract, "EdgePointSet", StringComparison.Ordinal)
                && string.Equals(inputs[0], inputs[2], StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{label} {Clean(step.ToolName)} FirstLine and FirstEdgePointSet inputs must be distinct artifacts.");
            }
            if (outputContracts.TryGetValue(inputs[1], out var secondLineContract)
                && string.Equals(secondLineContract, "LineFeature", StringComparison.Ordinal)
                && outputContracts.TryGetValue(inputs[3], out var secondEdgeContract)
                && string.Equals(secondEdgeContract, "EdgePointSet", StringComparison.Ordinal)
                && string.Equals(inputs[1], inputs[3], StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{label} {Clean(step.ToolName)} SecondLine and SecondEdgePointSet inputs must be distinct artifacts.");
            }
        }

        if (!C3DPhysicalEdgeGapFlushProfile.TryParse(step.Parameters, out _, out var message))
        {
            errors.Add($"{label} {message}");
        }
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);


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
