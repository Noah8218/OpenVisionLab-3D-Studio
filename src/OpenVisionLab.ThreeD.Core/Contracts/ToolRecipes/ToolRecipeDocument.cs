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
    public const string CurrentSchemaVersion = DualRoiRoutingSchemaVersion;

    public static bool SupportsArtifactOwnedSelections(string? schemaVersion) =>
        string.Equals(schemaVersion, ArtifactOwnedSelectionSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, OrientedBox3DSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, CurrentSchemaVersion, StringComparison.Ordinal);

    public static bool SupportsOrientedBox3D(string? schemaVersion) =>
        string.Equals(schemaVersion, OrientedBox3DSchemaVersion, StringComparison.Ordinal)
        || string.Equals(schemaVersion, CurrentSchemaVersion, StringComparison.Ordinal);

    public static bool SupportsDualRoiRouting(string? schemaVersion) =>
        string.Equals(schemaVersion, CurrentSchemaVersion, StringComparison.Ordinal);
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
    int? GridHeight = null);

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
    ToolRecipeDualRoiRouting? DualRoiRouting = null);

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
    ToolRecipeOrientedBox3D? OrientedBox3D = null);

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
}

public sealed record ToolRecipeValidationResult(
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public bool IsValid => Errors.Count == 0;
}
