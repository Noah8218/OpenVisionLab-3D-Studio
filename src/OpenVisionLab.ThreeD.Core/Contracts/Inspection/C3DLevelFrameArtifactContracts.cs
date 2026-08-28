using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// Immutable software coordinate-frame evidence derived from one typed
/// LevelingTransform. It is reusable frame metadata, not a physical
/// calibration or an acquisition pose correction.
/// </summary>
public sealed class C3DLevelFrameArtifact
{
    public const string ContractVersion = "1.0";
    public const string FramePolicy = "HeightPlaneToRightHandedLevelFrame";
    public const string AxisConvention = "U=ProjectedPositiveX;V=HCrossU;H=PositiveYPlaneNormal";
    public const string OriginPolicy = "FittedPlaneAtGridOrigin";
    public const string SourceCoordinateConvention = "GridXHeightGridZ";
    public const string FrameCoordinateConvention = "LevelFrameU_V_H";
    private const double GeometryTolerance = 1e-9;
    private readonly C3DLevelingReferenceRegion[] referenceRegions;

    private C3DLevelFrameArtifact(
        string outputEntityId,
        string levelFrameId,
        string rootSourceEntityId,
        string rootSourceSha256,
        string sourceUnit,
        string sourceFrameId,
        int sourceGridWidth,
        int sourceGridHeight,
        string levelingTransformEntityId,
        string levelingTransformContentSha256,
        C3DAffineMatrix3x4 sourceToFrame,
        C3DReferenceGridVector origin,
        C3DReferenceGridVector uAxis,
        C3DReferenceGridVector vAxis,
        C3DReferenceGridVector hAxis,
        double fittedSlopeX,
        double fittedSlopeZ,
        double fittedIntercept,
        double targetHeight,
        int referenceSampleCount,
        double referenceResidualRms,
        double referenceResidualPeakToValley,
        C3DLevelingReferenceRegion[] referenceRegions,
        string provenance,
        string contentSha256)
    {
        OutputEntityId = outputEntityId;
        LevelFrameId = levelFrameId;
        RootSourceEntityId = rootSourceEntityId;
        RootSourceSha256 = rootSourceSha256;
        SourceUnit = sourceUnit;
        SourceFrameId = sourceFrameId;
        SourceGridWidth = sourceGridWidth;
        SourceGridHeight = sourceGridHeight;
        LevelingTransformEntityId = levelingTransformEntityId;
        LevelingTransformContentSha256 = levelingTransformContentSha256;
        SourceToFrame = sourceToFrame;
        Origin = origin;
        UAxis = uAxis;
        VAxis = vAxis;
        HAxis = hAxis;
        FittedSlopeX = fittedSlopeX;
        FittedSlopeZ = fittedSlopeZ;
        FittedIntercept = fittedIntercept;
        TargetHeight = targetHeight;
        ReferenceSampleCount = referenceSampleCount;
        ReferenceResidualRms = referenceResidualRms;
        ReferenceResidualPeakToValley = referenceResidualPeakToValley;
        this.referenceRegions = referenceRegions;
        Provenance = provenance;
        ContentSha256 = contentSha256;
    }

    public string OutputEntityId { get; }
    public string LevelFrameId { get; }
    public string RootSourceEntityId { get; }
    public string RootSourceSha256 { get; }
    public string SourceUnit { get; }
    public string SourceFrameId { get; }
    public int SourceGridWidth { get; }
    public int SourceGridHeight { get; }
    public string LevelingTransformEntityId { get; }
    public string LevelingTransformContentSha256 { get; }
    public C3DAffineMatrix3x4 SourceToFrame { get; }
    public C3DReferenceGridVector Origin { get; }
    public C3DReferenceGridVector UAxis { get; }
    public C3DReferenceGridVector VAxis { get; }
    public C3DReferenceGridVector HAxis { get; }
    public double FittedSlopeX { get; }
    public double FittedSlopeZ { get; }
    public double FittedIntercept { get; }
    public double TargetHeight { get; }
    public int ReferenceSampleCount { get; }
    public double ReferenceResidualRms { get; }
    public double ReferenceResidualPeakToValley { get; }
    public IReadOnlyList<C3DLevelingReferenceRegion> ReferenceRegions => referenceRegions;
    public string Provenance { get; }
    public string ContentSha256 { get; }

    public (double U, double V, double H) Transform(
        double sourceX,
        double sourceY,
        double sourceZ) =>
        (SourceToFrame.M11 * sourceX
            + SourceToFrame.M12 * sourceY
            + SourceToFrame.M13 * sourceZ
            + SourceToFrame.M14,
         SourceToFrame.M21 * sourceX
            + SourceToFrame.M22 * sourceY
            + SourceToFrame.M23 * sourceZ
            + SourceToFrame.M24,
         SourceToFrame.M31 * sourceX
            + SourceToFrame.M32 * sourceY
            + SourceToFrame.M33 * sourceZ
            + SourceToFrame.M34);

    public (double U, double V, double H) TransformGridHeight(
        int row,
        int column,
        double rawHeight) =>
        Transform(column, rawHeight, row);

    public static C3DLevelFrameArtifact Create(
        string outputEntityId,
        string levelFrameId,
        C3DLevelingTransform levelingTransform,
        C3DAffineMatrix3x4 sourceToFrame,
        C3DReferenceGridVector origin,
        C3DReferenceGridVector uAxis,
        C3DReferenceGridVector vAxis,
        C3DReferenceGridVector hAxis,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(levelFrameId);
        ArgumentNullException.ThrowIfNull(levelingTransform);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);

        if (!origin.IsFinite
            || !uAxis.IsFinite
            || !vAxis.IsFinite
            || !hAxis.IsFinite
            || sourceToFrame.Values.Any(value => !double.IsFinite(value)))
        {
            throw new InvalidDataException(
                "LevelFrameArtifact requires finite origin, axes, and source-to-frame values.");
        }

        if (!IsUnit(uAxis)
            || !IsUnit(vAxis)
            || !IsUnit(hAxis)
            || Math.Abs(Dot(uAxis, vAxis)) > GeometryTolerance
            || Math.Abs(Dot(uAxis, hAxis)) > GeometryTolerance
            || Math.Abs(Dot(vAxis, hAxis)) > GeometryTolerance
            || Math.Abs(Determinant(sourceToFrame) - 1.0) > GeometryTolerance
            || !MatchesLinearRows(sourceToFrame, uAxis, vAxis, hAxis)
            || !MatchesTranslation(sourceToFrame, origin, uAxis, vAxis, hAxis))
        {
            throw new InvalidDataException(
                "LevelFrameArtifact axes and matrix must be orthonormal, right-handed, and mutually consistent.");
        }

        var planeHeight = (levelingTransform.FittedSlopeX * origin.X)
            + (levelingTransform.FittedSlopeZ * origin.Z)
            + levelingTransform.FittedIntercept;
        if (Math.Abs(origin.Y - planeHeight)
            > GeometryTolerance * Math.Max(1.0, Math.Abs(planeHeight)))
        {
            throw new InvalidDataException(
                "LevelFrameArtifact origin must lie on the fitted Level Surface plane.");
        }

        var regions = levelingTransform.ReferenceRegions.ToArray();
        var hash = CalculateContentSha256(
            outputEntityId,
            levelFrameId,
            levelingTransform,
            sourceToFrame,
            origin,
            uAxis,
            vAxis,
            hAxis,
            regions);
        return new C3DLevelFrameArtifact(
            outputEntityId,
            levelFrameId,
            levelingTransform.RootSourceEntityId,
            levelingTransform.RootSourceSha256.ToUpperInvariant(),
            levelingTransform.SourceUnit,
            levelingTransform.SourceFrameId,
            levelingTransform.SourceGridWidth,
            levelingTransform.SourceGridHeight,
            levelingTransform.OutputEntityId,
            levelingTransform.ContentSha256,
            sourceToFrame,
            origin,
            uAxis,
            vAxis,
            hAxis,
            levelingTransform.FittedSlopeX,
            levelingTransform.FittedSlopeZ,
            levelingTransform.FittedIntercept,
            levelingTransform.TargetHeight,
            levelingTransform.ReferenceSampleCount,
            levelingTransform.ReferenceResidualRms,
            levelingTransform.ReferenceResidualPeakToValley,
            regions,
            provenance,
            hash);
    }

    private static string CalculateContentSha256(
        string outputEntityId,
        string levelFrameId,
        C3DLevelingTransform transform,
        C3DAffineMatrix3x4 sourceToFrame,
        C3DReferenceGridVector origin,
        C3DReferenceGridVector uAxis,
        C3DReferenceGridVector vAxis,
        C3DReferenceGridVector hAxis,
        IReadOnlyList<C3DLevelingReferenceRegion> regions)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("OpenVisionLab.C3DLevelFrameArtifact");
        writer.Write(ContractVersion);
        writer.Write(FramePolicy);
        writer.Write(AxisConvention);
        writer.Write(OriginPolicy);
        writer.Write(SourceCoordinateConvention);
        writer.Write(FrameCoordinateConvention);
        writer.Write(outputEntityId);
        writer.Write(levelFrameId);
        writer.Write(transform.OutputEntityId);
        writer.Write(transform.ContentSha256);
        writer.Write(transform.RootSourceEntityId);
        writer.Write(transform.RootSourceSha256.ToUpperInvariant());
        writer.Write(transform.SourceUnit);
        writer.Write(transform.SourceFrameId);
        writer.Write(transform.SourceGridWidth);
        writer.Write(transform.SourceGridHeight);
        WriteVector(writer, origin);
        WriteVector(writer, uAxis);
        WriteVector(writer, vAxis);
        WriteVector(writer, hAxis);
        foreach (var value in sourceToFrame.Values)
        {
            writer.Write(value);
        }
        writer.Write(transform.FittedSlopeX);
        writer.Write(transform.FittedSlopeZ);
        writer.Write(transform.FittedIntercept);
        writer.Write(transform.TargetHeight);
        writer.Write(transform.ReferenceSampleCount);
        writer.Write(transform.ReferenceResidualRms);
        writer.Write(transform.ReferenceResidualPeakToValley);
        writer.Write(regions.Count);
        foreach (var region in regions)
        {
            writer.Write(region.SelectionId);
            writer.Write(region.Row);
            writer.Write(region.Column);
            writer.Write(region.RowCount);
            writer.Write(region.ColumnCount);
            writer.Write(region.ValidSampleCount);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static bool MatchesLinearRows(
        C3DAffineMatrix3x4 matrix,
        C3DReferenceGridVector uAxis,
        C3DReferenceGridVector vAxis,
        C3DReferenceGridVector hAxis) =>
        NearlyEqual(matrix.M11, uAxis.X)
        && NearlyEqual(matrix.M12, uAxis.Y)
        && NearlyEqual(matrix.M13, uAxis.Z)
        && NearlyEqual(matrix.M21, vAxis.X)
        && NearlyEqual(matrix.M22, vAxis.Y)
        && NearlyEqual(matrix.M23, vAxis.Z)
        && NearlyEqual(matrix.M31, hAxis.X)
        && NearlyEqual(matrix.M32, hAxis.Y)
        && NearlyEqual(matrix.M33, hAxis.Z);

    private static bool MatchesTranslation(
        C3DAffineMatrix3x4 matrix,
        C3DReferenceGridVector origin,
        C3DReferenceGridVector uAxis,
        C3DReferenceGridVector vAxis,
        C3DReferenceGridVector hAxis) =>
        NearlyEqual(matrix.M14, -Dot(uAxis, origin))
        && NearlyEqual(matrix.M24, -Dot(vAxis, origin))
        && NearlyEqual(matrix.M34, -Dot(hAxis, origin));

    private static bool IsUnit(C3DReferenceGridVector vector) =>
        Math.Abs(Length(vector) - 1.0) <= GeometryTolerance;

    private static double Length(C3DReferenceGridVector vector) =>
        Math.Sqrt(Dot(vector, vector));

    private static double Dot(
        C3DReferenceGridVector left,
        C3DReferenceGridVector right) =>
        (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);

    private static double Determinant(C3DAffineMatrix3x4 matrix) =>
        matrix.M11 * ((matrix.M22 * matrix.M33) - (matrix.M23 * matrix.M32))
        - matrix.M12 * ((matrix.M21 * matrix.M33) - (matrix.M23 * matrix.M31))
        + matrix.M13 * ((matrix.M21 * matrix.M32) - (matrix.M22 * matrix.M31));

    private static bool NearlyEqual(double actual, double expected) =>
        Math.Abs(actual - expected)
            <= GeometryTolerance * Math.Max(1.0, Math.Max(Math.Abs(actual), Math.Abs(expected)));

    private static void WriteVector(BinaryWriter writer, C3DReferenceGridVector vector)
    {
        writer.Write(vector.X);
        writer.Write(vector.Y);
        writer.Write(vector.Z);
    }
}
