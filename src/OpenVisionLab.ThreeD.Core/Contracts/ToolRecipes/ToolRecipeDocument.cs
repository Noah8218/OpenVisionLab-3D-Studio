namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// A teachable 3D tool graph. It records intended entity routing only and is
/// deliberately separate from executable inspection recipes.
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
    public const string CurrentSchemaVersion = "1.2";
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
    IReadOnlyList<ToolRecipeParameter> Parameters);

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
    ToolRecipeLandmarkCorrespondenceDescriptor? CorrespondenceDescriptor = null);

public sealed record ToolRecipeSelectionSourceBinding(
    string Format,
    string ContentSha256,
    int GridWidth,
    int GridHeight);

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
}

public sealed record ToolRecipeValidationResult(
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public bool IsValid => Errors.Count == 0;
}
