using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// Immutable raw-C3D, recipe-owned three-point plane evidence. Full-XYZ plane
/// construction belongs to Library-Noah; this contract owns Studio source,
/// selection, locator, role, and canonical replay identity only.
/// </summary>
public sealed class C3DThreePointPlaneFeature
{
    public const string ContractVersion = "1.0";
    public const string ConstructionPolicyName = "OrderedPointsDefineOrientedPlane";

    private C3DThreePointPlaneFeature(
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
        int thirdRow,
        int thirdColumn,
        double anchorX,
        double anchorY,
        double anchorZ,
        double normalX,
        double normalY,
        double normalZ,
        double planeOffset,
        double secondX,
        double secondY,
        double secondZ,
        double thirdX,
        double thirdY,
        double thirdZ,
        double normalizedCrossMagnitude,
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
        ThirdRow = thirdRow;
        ThirdColumn = thirdColumn;
        AnchorX = anchorX;
        AnchorY = anchorY;
        AnchorZ = anchorZ;
        NormalX = normalX;
        NormalY = normalY;
        NormalZ = normalZ;
        PlaneOffset = planeOffset;
        SecondX = secondX;
        SecondY = secondY;
        SecondZ = secondZ;
        ThirdX = thirdX;
        ThirdY = thirdY;
        ThirdZ = thirdZ;
        NormalizedCrossMagnitude = normalizedCrossMagnitude;
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
    public string InputSelectionId { get; }
    public string InputSelectionContentSha256 { get; }
    public int FirstRow { get; }
    public int FirstColumn { get; }
    public int SecondRow { get; }
    public int SecondColumn { get; }
    public int ThirdRow { get; }
    public int ThirdColumn { get; }
    public double AnchorX { get; }
    public double AnchorY { get; }
    public double AnchorZ { get; }
    public double NormalX { get; }
    public double NormalY { get; }
    public double NormalZ { get; }
    public double PlaneOffset { get; }
    public double SecondX { get; }
    public double SecondY { get; }
    public double SecondZ { get; }
    public double ThirdX { get; }
    public double ThirdY { get; }
    public double ThirdZ { get; }
    public double NormalizedCrossMagnitude { get; }
    public string OutputRole { get; }
    public string ConstructionPolicy => ConstructionPolicyName;
    public string Provenance { get; }
    public string ContentSha256 { get; }

    public static C3DThreePointPlaneFeature Create(
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
        int thirdRow,
        int thirdColumn,
        double anchorX,
        double anchorY,
        double anchorZ,
        double normalX,
        double normalY,
        double normalZ,
        double planeOffset,
        double secondX,
        double secondY,
        double secondZ,
        double thirdX,
        double thirdY,
        double thirdZ,
        double normalizedCrossMagnitude,
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
        var locators = new[] { (firstRow, firstColumn), (secondRow, secondColumn), (thirdRow, thirdColumn) };
        if (locators.Any(locator => locator.Item1 < 0 || locator.Item2 < 0)
            || locators.Distinct().Count() != 3)
        {
            throw new ArgumentException("Three-point plane requires three distinct non-negative grid cell locators.");
        }
        var values = new[]
        {
            anchorX, anchorY, anchorZ, normalX, normalY, normalZ, planeOffset,
            secondX, secondY, secondZ, thirdX, thirdY, thirdZ, normalizedCrossMagnitude
        };
        if (values.Any(value => !double.IsFinite(value)) || normalizedCrossMagnitude <= 0d)
        {
            throw new ArgumentException("Three-point plane requires finite non-degenerate geometry.");
        }
        var hash = CalculateContentSha256(
            outputEntityId, rootSourceEntityId, rootSourceSha256, unit, frameId,
            inputSelectionId, inputSelectionContentSha256, firstRow, firstColumn,
            secondRow, secondColumn, thirdRow, thirdColumn, anchorX, anchorY,
            anchorZ, normalX, normalY, normalZ, planeOffset, secondX, secondY,
            secondZ, thirdX, thirdY, thirdZ, normalizedCrossMagnitude, outputRole);
        return new C3DThreePointPlaneFeature(
            outputEntityId, rootSourceEntityId, rootSourceSha256, unit, frameId,
            inputSelectionId, inputSelectionContentSha256, firstRow, firstColumn,
            secondRow, secondColumn, thirdRow, thirdColumn, anchorX, anchorY,
            anchorZ, normalX, normalY, normalZ, planeOffset, secondX, secondY,
            secondZ, thirdX, thirdY, thirdZ, normalizedCrossMagnitude, outputRole,
            provenance, hash);
    }

    public static string CalculateSelectionContentSha256(ToolRecipeSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("OpenVisionLab.C3DThreePointPlaneSelection");
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
        int thirdRow, int thirdColumn, double anchorX, double anchorY,
        double anchorZ, double normalX, double normalY, double normalZ,
        double planeOffset, double secondX, double secondY, double secondZ,
        double thirdX, double thirdY, double thirdZ, double normalizedCrossMagnitude,
        string outputRole)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("OpenVisionLab.C3DThreePointPlaneFeature"); writer.Write(ContractVersion);
        writer.Write(outputEntityId); writer.Write(rootSourceEntityId); writer.Write(rootSourceSha256.ToUpperInvariant());
        writer.Write(unit); writer.Write(frameId); writer.Write("column-rawHeight-row");
        writer.Write(selectionId); writer.Write(selectionHash.ToUpperInvariant());
        writer.Write(firstRow); writer.Write(firstColumn); writer.Write(secondRow); writer.Write(secondColumn);
        writer.Write(thirdRow); writer.Write(thirdColumn);
        writer.Write(anchorX); writer.Write(anchorY); writer.Write(anchorZ);
        writer.Write(normalX); writer.Write(normalY); writer.Write(normalZ); writer.Write(planeOffset);
        writer.Write(secondX); writer.Write(secondY); writer.Write(secondZ);
        writer.Write(thirdX); writer.Write(thirdY); writer.Write(thirdZ);
        writer.Write(normalizedCrossMagnitude); writer.Write(outputRole); writer.Write(ConstructionPolicyName);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}
