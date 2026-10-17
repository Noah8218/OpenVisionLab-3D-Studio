using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// Canonical 3D inspection recipe document. It records the ordered tool graph,
/// typed entity routing, recipe-owned selections, and authored parameters used
/// by explicit Preview, Publish, save/reopen, and Runner adapters.
/// </summary>
public sealed record ToolRecipeDocument(
    string SchemaVersion,
    string Name,
    ToolRecipeSource Source,
    IReadOnlyList<ToolRecipeReference> References,
    IReadOnlyList<ToolRecipeStep> Steps,
    IReadOnlyList<ToolRecipeSelection>? Selections = null,
    string? Category = null,
    string? InspectionTarget = null,
    string? Description = null)
{
    public const int MaximumMetadataFieldLength = 500;
    public const string LegacySchemaVersion = "1.0";
    public const string SelectionSchemaVersion = "1.1";
    public const string GenericMeasurementSchemaVersion = "1.2";
    public const string ArtifactOwnedSelectionSchemaVersion = "1.3";
    public const string OrientedBox3DSchemaVersion = "1.4";
    public const string DualRoiRoutingSchemaVersion = "1.5";
    public const string GridCircleSchemaVersion = "1.6";
    public const string GridPolygonSchemaVersion = "1.7";
    public const string OutputPolicySchemaVersion = "1.8";
    public const string RecipeMetadataSchemaVersion = "1.9";
    public const string AcceptanceLimitsSchemaVersion = "1.10";
    public const string GroupSchemaVersion = "1.11";
    public const string CurrentSchemaVersion = GroupSchemaVersion;

    /// <summary>Calculation context for one step and its upstream graph, excluding descriptive metadata.</summary>
    public string CreateStepExecutionContext(string stepId)
    {
        var selected = Steps.FirstOrDefault(step => string.Equals(step.Id, stepId, StringComparison.OrdinalIgnoreCase));
        if (selected is null) return string.Empty;
        var producers = Steps.ToLookup(step => step.OutputEntityId, StringComparer.OrdinalIgnoreCase);
        var selections = (Selections ?? []).ToLookup(selection => selection.Id, StringComparer.OrdinalIgnoreCase);
        var stepIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entityIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pendingSteps = new Queue<ToolRecipeStep>();
        pendingSteps.Enqueue(selected);
        while (pendingSteps.TryDequeue(out var step))
        {
            if (!stepIds.Add(step.Id)) continue;
            var inputs = new Queue<string>(step.InputEntityIds.Concat(new[] { step.DualRoiRouting?.FirstRegionSelectionId, step.DualRoiRouting?.SecondRegionSelectionId }
                .Where(id => id is not null).Select(id => id!)));
            while (inputs.TryDequeue(out var inputId))
            {
                if (!entityIds.Add(inputId)) continue;
                foreach (var producer in producers[inputId]) pendingSteps.Enqueue(producer);
                foreach (var selection in selections[inputId])
                    if (selection.SourceBinding.OwnerEntityId is { } ownerId) inputs.Enqueue(ownerId);
            }
        }
        return JsonSerializer.Serialize(new
        {
            Source = Source with { Name = string.Empty },
            Steps = Steps.Where(step => stepIds.Contains(step.Id)).OrderBy(step => step.Id, StringComparer.OrdinalIgnoreCase)
                .Select(step => step with { ToolName = string.Empty, Group = null }),
            Selections = (Selections ?? []).Where(selection => entityIds.Contains(selection.Id)).OrderBy(selection => selection.Id, StringComparer.OrdinalIgnoreCase)
                .Select(selection => selection with { Name = string.Empty }),
            References = References.Where(reference => entityIds.Contains(reference.Id)).OrderBy(reference => reference.Id, StringComparer.OrdinalIgnoreCase)
                .Select(reference => reference with { Name = string.Empty })
        });
    }

    public static bool SupportsArtifactOwnedSelections(string? schemaVersion) =>
        string.Equals(schemaVersion, ArtifactOwnedSelectionSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, OrientedBox3DSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, DualRoiRoutingSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, GridCircleSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, GridPolygonSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, OutputPolicySchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, RecipeMetadataSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, AcceptanceLimitsSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, CurrentSchemaVersion, StringComparison.Ordinal);

    public static bool SupportsOrientedBox3D(string? schemaVersion) =>
        string.Equals(schemaVersion, OrientedBox3DSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, DualRoiRoutingSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, GridCircleSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, GridPolygonSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, OutputPolicySchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, RecipeMetadataSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, AcceptanceLimitsSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, CurrentSchemaVersion, StringComparison.Ordinal);

    public static bool SupportsDualRoiRouting(string? schemaVersion) =>
        string.Equals(schemaVersion, DualRoiRoutingSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, GridCircleSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, GridPolygonSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, OutputPolicySchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, RecipeMetadataSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, AcceptanceLimitsSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, CurrentSchemaVersion, StringComparison.Ordinal);

    public static bool SupportsGridCircle(string? schemaVersion) =>
        string.Equals(schemaVersion, GridCircleSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, GridPolygonSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, OutputPolicySchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, RecipeMetadataSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, AcceptanceLimitsSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, CurrentSchemaVersion, StringComparison.Ordinal);

    public static bool SupportsGridPolygon(string? schemaVersion) =>
        string.Equals(schemaVersion, GridPolygonSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, OutputPolicySchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, RecipeMetadataSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, AcceptanceLimitsSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, CurrentSchemaVersion, StringComparison.Ordinal);
}

public sealed record ToolRecipeSource(
    string Id,
    string Name,
    string Format,
    string Unit,
    string FrameId,
    string Path,
    long? ByteLength = null,
    string? ContentSha256 = null,
    int? GridWidth = null,
    int? GridHeight = null,
    ToolRecipeAcquisitionProvenance? AcquisitionProvenance = null,
    HeightMeasurementEvidence? MeasurementEvidence = null,
    string? SensorId = null);

[JsonConverter(typeof(JsonStringEnumConverter<ToolRecipeAcquisitionProvenanceState>))]
public enum ToolRecipeAcquisitionProvenanceState
{
    Available,
    Unavailable
}

[JsonConverter(typeof(JsonStringEnumConverter<ToolRecipeAcquisitionLimitationKind>))]
public enum ToolRecipeAcquisitionLimitationKind
{
    Reflective,
    Transparent,
    Textureless,
    Clipped,
    LowCoverage
}

[JsonConverter(typeof(JsonStringEnumConverter<ToolRecipeAcquisitionLimitationOrigin>))]
public enum ToolRecipeAcquisitionLimitationOrigin
{
    OperatorAuthored,
    Imported
}

public sealed record ToolRecipeAcquisitionLimitationFlag(
    ToolRecipeAcquisitionLimitationKind Kind,
    ToolRecipeAcquisitionLimitationOrigin Origin);

/// <summary>
/// Operator-authored or imported acquisition evidence for one recipe source.
/// Text is retained verbatim as declared evidence; it does not establish a
/// camera pose, calibration, or inferred acquisition viewpoint.
/// </summary>
public sealed record ToolRecipeAcquisitionProvenance(
    ToolRecipeAcquisitionProvenanceState State,
    string Evidence,
    string LimitationNotes,
    ToolRecipeAcquisitionDirection? AcquisitionDirection = null,
    IReadOnlyList<ToolRecipeAcquisitionLimitationFlag>? LimitationFlags = null)
{
    public static ToolRecipeAcquisitionProvenance CreateUnavailable() => new(
        ToolRecipeAcquisitionProvenanceState.Unavailable,
        "No acquisition provenance was supplied for this source.",
        "Acquisition viewpoint, direction, sensor pose, calibration, and capture conditions are unavailable.");
}

[JsonConverter(typeof(JsonStringEnumConverter<ToolRecipeAcquisitionDirectionState>))]
public enum ToolRecipeAcquisitionDirectionState
{
    Available,
    Unavailable
}

[JsonConverter(typeof(JsonStringEnumConverter<ToolRecipeAcquisitionDirectionConvention>))]
public enum ToolRecipeAcquisitionDirectionConvention
{
    SensorToScene
}

/// <summary>
/// Explicit source-frame direction from the sensor toward the acquired scene.
/// It is operator-authored or imported evidence, not an inferred camera pose.
/// </summary>
public sealed record ToolRecipeAcquisitionDirection(
    ToolRecipeAcquisitionDirectionState State,
    ToolRecipeAcquisitionDirectionConvention Convention,
    string FrameId,
    ToolRecipeXyz? Vector)
{
    public static ToolRecipeAcquisitionDirection CreateUnavailable(string frameId) => new(
        ToolRecipeAcquisitionDirectionState.Unavailable,
        ToolRecipeAcquisitionDirectionConvention.SensorToScene,
        frameId,
        null);
}

public sealed record ToolRecipeReference(
    string Id,
    string Name,
    string Kind);

public sealed record ToolRecipeStep(
    string Id,
    string ToolId,
    string ToolName,
    int MinimumInputCount,
    IReadOnlyList<string> InputEntityIds,
    string OutputEntityId,
    IReadOnlyList<ToolRecipeParameter> Parameters,
    ToolRecipeDualRoiRouting? DualRoiRouting = null,
    bool OutputEnabled = true,
    ToolRecipeSignedDeltaSidecarReference? SignedDeltaSidecar = null,
    bool? AcceptanceLimitsConfigured = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ToolRecipeStepGroup? Group = null)
{
    public static bool RequiresAcceptanceLimits(string toolId) => toolId is "thickness" or "warpage" or "plane-flatness"
        or "datum-plane-raw-height-deviation" or "point-pair-dimensions" or "gap-flush" or "volume"
        or "cross-section-dimensions" or "completeness-grid" or "connected-region-gate";

    public const string UnconfiguredAcceptanceMessage = "Acceptance limits are not configured. Measurement values are available without a judgement.";

    public ToolResult ApplyAcceptancePolicy(ToolResult result) => AcceptanceLimitsConfigured != false
        || result.Status is ResultStatus.Error or ResultStatus.Warning ? result : result with
    {
        Status = ResultStatus.NotRun,
        Message = UnconfiguredAcceptanceMessage,
        Metrics = result.Metrics.Select(metric => metric.Status is ResultStatus.Pass or ResultStatus.Fail
            ? metric with { Status = ResultStatus.NotRun } : metric).ToArray(),
        Overlays = result.Overlays.Select(overlay => overlay.Status is ResultStatus.Pass or ResultStatus.Fail
            ? overlay with { Status = ResultStatus.NotRun } : overlay).ToArray()
    };
}

public sealed record ToolRecipeStepGroup(string Id, string Name);

/// <summary>
/// Explicit, recipe-relative reference to a published signed-delta evidence
/// sidecar. The artifact content and value hashes bind the reference to the
/// output that was selected for Save; opening a recipe never discovers a
/// sidecar by convention when this reference is absent.
/// </summary>
public sealed record ToolRecipeSignedDeltaSidecarReference(
    string Path,
    string StepId,
    string OutputEntityId,
    string ContentSha256,
    string OutputContentSha256,
    string CurrentSourceEntityId,
    string CurrentSourceContentSha256,
    string CurrentRootSourceSha256,
    string BackgroundEntityId,
    string BackgroundContentSha256,
    string BackgroundRootSourceSha256,
    int Width,
    int Height,
    int GridOriginColumn,
    int GridOriginRow,
    string Unit,
    string FrameId)
{
    private const string SidecarExtension = ".sdelta.json";

    /// <summary>
    /// Validates the persisted reference shape and, when supplied, its binding
    /// to the recipe step and signed-delta artifact. File existence and
    /// recipe-directory containment remain the Save/Open owner policy.
    /// </summary>
    public bool TryValidateContract(
        string? expectedStepId,
        string? expectedToolId,
        string? expectedOutputEntityId,
        C3DSignedHeightDeltaArtifact? artifact,
        out string message)
    {
        if (string.IsNullOrWhiteSpace(Path)
            || System.IO.Path.IsPathFullyQualified(Path)
            || System.IO.Path.IsPathRooted(Path)
            || !Path.EndsWith(SidecarExtension, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(StepId)
            || string.IsNullOrWhiteSpace(OutputEntityId)
            || !IsSha256(ContentSha256)
            || !IsSha256(OutputContentSha256)
            || string.IsNullOrWhiteSpace(CurrentSourceEntityId)
            || !IsSha256(CurrentSourceContentSha256)
            || !IsSha256(CurrentRootSourceSha256)
            || string.IsNullOrWhiteSpace(BackgroundEntityId)
            || !IsSha256(BackgroundContentSha256)
            || !IsSha256(BackgroundRootSourceSha256)
            || Width <= 0
            || Height <= 0
            || string.IsNullOrWhiteSpace(Unit)
            || string.IsNullOrWhiteSpace(FrameId))
        {
            message = "Signed-delta sidecar reference has an invalid path, identity, hash, grid, unit, or frame.";
            return false;
        }

        if (expectedToolId is not null
            && !string.Equals(expectedToolId, C3DSignedHeightDeltaRecipePolicy.ToolId, StringComparison.OrdinalIgnoreCase))
        {
            message = "Signed-delta sidecar reference is owned by a non-signed-delta step.";
            return false;
        }

        if (expectedStepId is not null
            && !string.Equals(expectedStepId, StepId, StringComparison.OrdinalIgnoreCase))
        {
            message = "Signed-delta sidecar reference step identity does not match the recipe or Run Record.";
            return false;
        }

        if (expectedOutputEntityId is not null
            && !string.Equals(expectedOutputEntityId, OutputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            message = "Signed-delta sidecar reference output identity does not match the recipe or Run Record.";
            return false;
        }

        if (artifact is not null
            && (!string.Equals(StepId, artifact.StepId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(OutputEntityId, artifact.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(ContentSha256, artifact.ContentSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(OutputContentSha256, artifact.OutputContentSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(CurrentSourceEntityId, artifact.CurrentSourceEntityId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(CurrentSourceContentSha256, artifact.CurrentSourceContentSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(CurrentRootSourceSha256, artifact.CurrentRootSourceSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(BackgroundEntityId, artifact.BackgroundEntityId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(BackgroundContentSha256, artifact.BackgroundContentSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(BackgroundRootSourceSha256, artifact.BackgroundRootSourceSha256, StringComparison.OrdinalIgnoreCase)
                || Width != artifact.Width
                || Height != artifact.Height
                || GridOriginColumn != artifact.GridOriginColumn
                || GridOriginRow != artifact.GridOriginRow
                || !string.Equals(Unit, artifact.Unit, StringComparison.Ordinal)
                || !string.Equals(FrameId, artifact.FrameId, StringComparison.Ordinal)))
        {
            message = "Signed-delta sidecar reference does not match the artifact identity.";
            return false;
        }

        message = "Signed-delta sidecar reference contract is valid.";
        return true;
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);
}

/// <summary>
/// Persists the semantic region roles owned by one dual-ROI inspection step.
/// A missing role remains null while the other role can stay routed in a
/// storage-valid incomplete draft.
/// </summary>
public sealed record ToolRecipeDualRoiRouting(
    string? FirstRegionSelectionId,
    string? SecondRegionSelectionId);

public sealed record ToolRecipeParameter(string Name, string Value);

public sealed record ToolRecipeSelection(
    string Id,
    string Name,
    string Kind,
    string RootSourceId,
    string FrameId,
    ToolRecipeSelectionSourceBinding SourceBinding,
    ToolRecipeGridRectangle? GridRectangle,
    IReadOnlyList<ToolRecipeSelectionPoint>? Points,
    IReadOnlyList<ToolRecipeLandmarkCorrespondence>? Rows,
    ToolRecipeLandmarkCorrespondenceDescriptor? CorrespondenceDescriptor = null,
    ToolRecipeOrientedBox3D? OrientedBox3D = null,
    ToolRecipeGridCircle? GridCircle = null,
    ToolRecipeGridPolygon? GridPolygon = null);

public sealed record ToolRecipeSelectionSourceBinding(
    string Format,
    string ContentSha256,
    int GridWidth,
    int GridHeight,
    string? OwnerEntityId = null,
    string? RootSourceContentSha256 = null,
    string? Unit = null,
    string? FrameId = null);

public sealed record ToolRecipeGridRectangle(
    int Row,
    int Column,
    int RowCount,
    int ColumnCount);

/// <summary>
/// Circular footprint in height-field grid coordinates. CenterRow maps to Z,
/// CenterColumn maps to X, and Radius is measured between grid-cell centers.
/// </summary>
public sealed record ToolRecipeGridCircle(
    int CenterRow,
    int CenterColumn,
    double Radius);

/// <summary>
/// Ordered polygon footprint in height-field grid coordinates. Row maps to Z
/// and column maps to X. This is an authoring boundary; it does not imply a
/// generated mask or an inspection consumer.
/// </summary>
public sealed record ToolRecipeGridPolygon(
    IReadOnlyList<ToolRecipeGridPolygonVertex> Vertices);

public sealed record ToolRecipeGridPolygonVertex(
    double Row,
    double Column);

public sealed record ToolRecipeGridCellLocator(
    string Kind,
    int Row,
    int Column);

public sealed record ToolRecipeXyz(double X, double Y, double Z);

public sealed record ToolRecipeOrientedBox3D(
    ToolRecipeXyz Center,
    ToolRecipeXyz AxisX,
    ToolRecipeXyz AxisY,
    ToolRecipeXyz AxisZ,
    ToolRecipeXyz HalfExtents);

public sealed record ToolRecipeSelectionPoint(
    ToolRecipeGridCellLocator Locator,
    ToolRecipeXyz CapturedPosition,
    double RawHeight);

public sealed record ToolRecipeLandmarkCorrespondence(
    string SourceEntityId,
    string ReferenceLandmarkId,
    ToolRecipeXyz ReferencePosition,
    string ReferenceFrameId);

public sealed record ToolRecipeLandmarkCorrespondenceDescriptor(
    string ReferenceFrameId,
    string ReferenceUnit,
    string ReferenceProvenance,
    string ReferenceRevision,
    string PairCountPolicy,
    string SourceArtifactPolicy,
    string AffineIndependencePolicy,
    double? MinimumNormalizedTetrahedronVolume);

public static class ToolRecipeSelectionKinds
{
    public const string GridRectangle = "grid-rectangle";
    public const string PointSet = "point-set";
    public const string LandmarkCorrespondenceSet = "landmark-correspondence-set";
    public const string OrientedBox3D = "oriented-box-3d";
    public const string GridCircle = "grid-circle";
    public const string GridPolygon = "grid-polygon";
}

public sealed record ToolRecipeValidationResult(
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public bool IsValid => Errors.Count == 0;
}
