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
    IReadOnlyList<ToolRecipeSelection>? Selections = null)
{
    public const string LegacySchemaVersion = "1.0";
    public const string SelectionSchemaVersion = "1.1";
    public const string GenericMeasurementSchemaVersion = "1.2";
    public const string ArtifactOwnedSelectionSchemaVersion = "1.3";
    public const string OrientedBox3DSchemaVersion = "1.4";
    public const string DualRoiRoutingSchemaVersion = "1.5";
    public const string GridCircleSchemaVersion = "1.6";
    public const string GridPolygonSchemaVersion = "1.7";
    public const string OutputPolicySchemaVersion = "1.8";
    public const string CurrentSchemaVersion = OutputPolicySchemaVersion;

    public static bool SupportsArtifactOwnedSelections(string? schemaVersion) =>
        string.Equals(schemaVersion, ArtifactOwnedSelectionSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, OrientedBox3DSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, DualRoiRoutingSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, GridCircleSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, OutputPolicySchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, CurrentSchemaVersion, StringComparison.Ordinal);

    public static bool SupportsOrientedBox3D(string? schemaVersion) =>
        string.Equals(schemaVersion, OrientedBox3DSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, DualRoiRoutingSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, GridCircleSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, OutputPolicySchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, CurrentSchemaVersion, StringComparison.Ordinal);

    public static bool SupportsDualRoiRouting(string? schemaVersion) =>
        string.Equals(schemaVersion, DualRoiRoutingSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, GridCircleSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, OutputPolicySchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, CurrentSchemaVersion, StringComparison.Ordinal);

    public static bool SupportsGridCircle(string? schemaVersion) =>
        string.Equals(schemaVersion, GridCircleSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, GridPolygonSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, OutputPolicySchemaVersion, StringComparison.Ordinal);

    public static bool SupportsGridPolygon(string? schemaVersion) =>
        string.Equals(schemaVersion, GridPolygonSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, OutputPolicySchemaVersion, StringComparison.Ordinal)
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
    ToolRecipeAcquisitionProvenance? AcquisitionProvenance = null);

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
    bool OutputEnabled = true);

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
