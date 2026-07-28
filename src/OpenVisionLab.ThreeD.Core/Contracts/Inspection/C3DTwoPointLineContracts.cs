using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// Immutable raw-C3D, recipe-owned two-point line evidence. The full-XYZ
/// geometry is constructed by Library-Noah; this contract owns Studio source,
/// selection, locator, role, and canonical replay identity only.
/// </summary>
public sealed class C3DTwoPointLineFeature : IC3DLineGeometry
{
    public const string ContractVersion = "1.0";
    public const string ConstructionPolicyName = "OrderedPointsDefineSegment";

    private C3DTwoPointLineFeature(
        string outputEntityId,
        string rootSourceEntityId,
        string rootSourceSha256,
        string unit,
        string frameId,
        string inputSelectionId,
        string inputSelectionContentSha256,
        int firstRow,
        int firstColumn,
        int secondRow,
        int secondColumn,
        double anchorX,
        double anchorY,
        double anchorZ,
        double directionX,
        double directionY,
        double directionZ,
        double segmentEndX,
        double segmentEndY,
        double segmentEndZ,
        double segmentLength,
        string outputRole,
        string provenance,
        string contentSha256)
    {
        OutputEntityId = outputEntityId;
        RootSourceEntityId = rootSourceEntityId;
        RootSourceSha256 = rootSourceSha256;
        Unit = unit;
        FrameId = frameId;
        InputSelectionId = inputSelectionId;
        InputSelectionContentSha256 = inputSelectionContentSha256;
        FirstRow = firstRow;
        FirstColumn = firstColumn;
        SecondRow = secondRow;
        SecondColumn = secondColumn;
        AnchorX = anchorX;
        AnchorY = anchorY;
        AnchorZ = anchorZ;
        DirectionX = directionX;
        DirectionY = directionY;
        DirectionZ = directionZ;
        SegmentEndX = segmentEndX;
        SegmentEndY = segmentEndY;
        SegmentEndZ = segmentEndZ;
        SegmentLength = segmentLength;
        OutputRole = outputRole;
        Provenance = provenance;
        ContentSha256 = contentSha256;
    }

    public string OutputEntityId { get; }
    public string RootSourceEntityId { get; }
    public string RootSourceSha256 { get; }
    public string Unit { get; }
    public string FrameId { get; }
    public string CoordinateConvention => "column-rawHeight-row";
    public C3DLineOriginKind OriginKind => C3DLineOriginKind.PickedPoints;
    public string InputSelectionId { get; }
    public string InputSelectionContentSha256 { get; }
    public int FirstRow { get; }
    public int FirstColumn { get; }
    public int SecondRow { get; }
    public int SecondColumn { get; }
    public double AnchorX { get; }
    public double AnchorY { get; }
    public double AnchorZ { get; }
    public double DirectionX { get; }
    public double DirectionY { get; }
    public double DirectionZ { get; }
    public double SegmentStartX => AnchorX;
    public double SegmentStartY => AnchorY;
    public double SegmentStartZ => AnchorZ;
    public double SegmentEndX { get; }
    public double SegmentEndY { get; }
    public double SegmentEndZ { get; }
    public double SegmentLength { get; }
    public string OutputRole { get; }
    public string ConstructionPolicy => ConstructionPolicyName;
    public string Provenance { get; }
    public string ContentSha256 { get; }

    public static C3DTwoPointLineFeature Create(
        string outputEntityId,
        string rootSourceEntityId,
        string rootSourceSha256,
        string unit,
        string frameId,
        string inputSelectionId,
        string inputSelectionContentSha256,
        int firstRow,
        int firstColumn,
        int secondRow,
        int secondColumn,
        double anchorX,
        double anchorY,
        double anchorZ,
        double directionX,
        double directionY,
        double directionZ,
        double segmentEndX,
        double segmentEndY,
        double segmentEndZ,
        double segmentLength,
        string outputRole,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootSourceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootSourceSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputSelectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputSelectionContentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRole);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (firstRow < 0 || firstColumn < 0 || secondRow < 0 || secondColumn < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(firstRow), "Two-point line locators must be non-negative.");
        }
        if (firstRow == secondRow && firstColumn == secondColumn)
        {
            throw new ArgumentException("Two-point line requires two distinct grid cell locators.");
        }
        var values = new[]
        {
            anchorX, anchorY, anchorZ, directionX, directionY, directionZ,
            segmentEndX, segmentEndY, segmentEndZ, segmentLength
        };
        if (values.Any(value => !double.IsFinite(value)) || segmentLength <= 0d)
        {
            throw new ArgumentException("Two-point line requires finite non-zero geometry.");
        }
        var hash = CalculateContentSha256(
            outputEntityId, rootSourceEntityId, rootSourceSha256, unit, frameId,
            inputSelectionId, inputSelectionContentSha256, firstRow, firstColumn,
            secondRow, secondColumn, anchorX, anchorY, anchorZ, directionX,
            directionY, directionZ, segmentEndX, segmentEndY, segmentEndZ,
            segmentLength, outputRole);
        return new C3DTwoPointLineFeature(
            outputEntityId, rootSourceEntityId, rootSourceSha256, unit, frameId,
            inputSelectionId, inputSelectionContentSha256, firstRow, firstColumn,
            secondRow, secondColumn, anchorX, anchorY, anchorZ, directionX,
            directionY, directionZ, segmentEndX, segmentEndY, segmentEndZ,
            segmentLength, outputRole, provenance, hash);
    }

    public static string CalculateSelectionContentSha256(ToolRecipeSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("OpenVisionLab.C3DTwoPointLineSelection");
        writer.Write(selection.Id); writer.Write(selection.Kind); writer.Write(selection.RootSourceId);
        writer.Write(selection.FrameId); writer.Write(selection.SourceBinding.Format);
        writer.Write(selection.SourceBinding.ContentSha256.ToUpperInvariant());
        writer.Write(selection.SourceBinding.GridWidth); writer.Write(selection.SourceBinding.GridHeight);
        var points = selection.Points ?? [];
        writer.Write(points.Count);
        foreach (var point in points)
        {
            writer.Write(point.Locator.Kind); writer.Write(point.Locator.Row); writer.Write(point.Locator.Column);
            writer.Write(point.CapturedPosition.X); writer.Write(point.CapturedPosition.Y); writer.Write(point.CapturedPosition.Z);
            writer.Write(point.RawHeight);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static string CalculateContentSha256(
        string outputEntityId, string rootSourceEntityId, string rootSourceSha256,
        string unit, string frameId, string selectionId, string selectionHash,
        int firstRow, int firstColumn, int secondRow, int secondColumn,
        double anchorX, double anchorY, double anchorZ, double directionX,
        double directionY, double directionZ, double segmentEndX,
        double segmentEndY, double segmentEndZ, double segmentLength,
        string outputRole)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("OpenVisionLab.C3DTwoPointLineFeature"); writer.Write(ContractVersion);
        writer.Write(outputEntityId); writer.Write(rootSourceEntityId); writer.Write(rootSourceSha256.ToUpperInvariant());
        writer.Write(unit); writer.Write(frameId); writer.Write("column-rawHeight-row");
        writer.Write(C3DLineOriginKind.PickedPoints.ToString()); writer.Write(selectionId); writer.Write(selectionHash.ToUpperInvariant());
        writer.Write(firstRow); writer.Write(firstColumn); writer.Write(secondRow); writer.Write(secondColumn);
        writer.Write(anchorX); writer.Write(anchorY); writer.Write(anchorZ);
        writer.Write(directionX); writer.Write(directionY); writer.Write(directionZ);
        writer.Write(segmentEndX); writer.Write(segmentEndY); writer.Write(segmentEndZ); writer.Write(segmentLength);
        writer.Write(outputRole); writer.Write(ConstructionPolicyName);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}
