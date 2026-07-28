using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

public enum C3DLineFitMethod
{
    DeterministicConsensusOrthogonalTls
}

public sealed record C3DLineFeaturePointDiagnostic(
    int InputPointIndex,
    int ScanlineIndex,
    double X,
    double Y,
    double Z,
    double ProjectedX,
    double ProjectedY,
    double ProjectedZ,
    double OrthogonalResidual,
    bool IsInlier);

public sealed record C3DLineFeatureDiagnostics(
    int InputPointCount,
    int InlierCount,
    int OutlierCount,
    double InlierRatio,
    int InlierScanlineMinimum,
    int InlierScanlineMaximum,
    int InlierScanlineSpan,
    double ResidualRms,
    double ResidualMaximum,
    double ResidualMedian,
    double ProjectedSegmentLength,
    int HypothesisCount,
    int RefinementIterationCount);

/// <summary>
/// Immutable full-XYZ line feature in the numeric C3D frame: X=column,
/// Y=raw height, Z=row. All residuals are source-coordinate values, not
/// calibrated physical measurements. The hash is replay evidence only.
/// </summary>
public sealed class C3DLineFeature : IC3DLineGeometry
{
    public const string ContractVersion = "1.0";
    private readonly C3DLineFeaturePointDiagnostic[] pointDiagnostics;

    private C3DLineFeature(
        string outputEntityId,
        string inputEdgePointSetEntityId,
        string inputContentSha256,
        string rootSourceEntityId,
        string rootSourceSha256,
        string unit,
        string frameId,
        C3DHeightDifferenceComparisonAxis inputComparisonAxis,
        double maximumOrthogonalResidual,
        int minimumInlierCount,
        double minimumInlierRatio,
        int minimumInlierScanlineSpan,
        double anchorX,
        double anchorY,
        double anchorZ,
        double directionX,
        double directionY,
        double directionZ,
        double segmentStartX,
        double segmentStartY,
        double segmentStartZ,
        double segmentEndX,
        double segmentEndY,
        double segmentEndZ,
        C3DLineFeatureDiagnostics diagnostics,
        C3DLineFeaturePointDiagnostic[] pointDiagnostics,
        string provenance,
        string contentSha256)
    {
        OutputEntityId = outputEntityId;
        InputEdgePointSetEntityId = inputEdgePointSetEntityId;
        InputContentSha256 = inputContentSha256;
        RootSourceEntityId = rootSourceEntityId;
        RootSourceSha256 = rootSourceSha256;
        Unit = unit;
        FrameId = frameId;
        InputComparisonAxis = inputComparisonAxis;
        MaximumOrthogonalResidual = maximumOrthogonalResidual;
        MinimumInlierCount = minimumInlierCount;
        MinimumInlierRatio = minimumInlierRatio;
        MinimumInlierScanlineSpan = minimumInlierScanlineSpan;
        AnchorX = anchorX;
        AnchorY = anchorY;
        AnchorZ = anchorZ;
        DirectionX = directionX;
        DirectionY = directionY;
        DirectionZ = directionZ;
        SegmentStartX = segmentStartX;
        SegmentStartY = segmentStartY;
        SegmentStartZ = segmentStartZ;
        SegmentEndX = segmentEndX;
        SegmentEndY = segmentEndY;
        SegmentEndZ = segmentEndZ;
        Diagnostics = diagnostics;
        this.pointDiagnostics = pointDiagnostics;
        Provenance = provenance;
        ContentSha256 = contentSha256;
    }

    public string OutputEntityId { get; }
    public string InputEdgePointSetEntityId { get; }
    public string InputContentSha256 { get; }
    public string RootSourceEntityId { get; }
    public string RootSourceSha256 { get; }
    public string Unit { get; }
    public string FrameId { get; }
    public string CoordinateConvention => "column-rawHeight-row";
    public C3DLineOriginKind OriginKind => C3DLineOriginKind.FittedEdge;
    public string ResidualUnit => "source-coordinate";
    public C3DLineFitMethod FitMethod => C3DLineFitMethod.DeterministicConsensusOrthogonalTls;
    public C3DHeightDifferenceComparisonAxis InputComparisonAxis { get; }
    public double MaximumOrthogonalResidual { get; }
    public int MinimumInlierCount { get; }
    public double MinimumInlierRatio { get; }
    public int MinimumInlierScanlineSpan { get; }
    public string HypothesisPolicy => "Sha256PairSchedule";
    public int MaximumHypotheses => 256;
    public string RefinementPolicy => "OrthogonalTlsUntilStable10";
    public string DirectionPolicy => "PositiveScanlineAxis";
    public string EndpointPolicy => "InlierProjectionExtents";
    public double AnchorX { get; }
    public double AnchorY { get; }
    public double AnchorZ { get; }
    public double DirectionX { get; }
    public double DirectionY { get; }
    public double DirectionZ { get; }
    public double SegmentStartX { get; }
    public double SegmentStartY { get; }
    public double SegmentStartZ { get; }
    public double SegmentEndX { get; }
    public double SegmentEndY { get; }
    public double SegmentEndZ { get; }
    public C3DLineFeatureDiagnostics Diagnostics { get; }
    public IReadOnlyList<C3DLineFeaturePointDiagnostic> PointDiagnostics => pointDiagnostics;
    public string Provenance { get; }
    public string ContentSha256 { get; }

    public static C3DLineFeature Create(
        string outputEntityId,
        C3DHeightDifferenceEdgePointSet input,
        double maximumOrthogonalResidual,
        int minimumInlierCount,
        double minimumInlierRatio,
        int minimumInlierScanlineSpan,
        double anchorX,
        double anchorY,
        double anchorZ,
        double directionX,
        double directionY,
        double directionZ,
        double segmentStartX,
        double segmentStartY,
        double segmentStartZ,
        double segmentEndX,
        double segmentEndY,
        double segmentEndZ,
        C3DLineFeatureDiagnostics diagnostics,
        IReadOnlyList<C3DLineFeaturePointDiagnostic> pointDiagnostics,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputEntityId);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(pointDiagnostics);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);

        var copy = pointDiagnostics.ToArray();
        var hash = CalculateContentSha256(
            outputEntityId,
            input,
            maximumOrthogonalResidual,
            minimumInlierCount,
            minimumInlierRatio,
            minimumInlierScanlineSpan,
            anchorX,
            anchorY,
            anchorZ,
            directionX,
            directionY,
            directionZ,
            segmentStartX,
            segmentStartY,
            segmentStartZ,
            segmentEndX,
            segmentEndY,
            segmentEndZ,
            diagnostics,
            copy);
        return new C3DLineFeature(
            outputEntityId,
            input.OutputEntityId,
            input.ContentSha256,
            input.RootSourceEntityId,
            input.RootSourceSha256,
            input.Unit,
            input.FrameId,
            input.ComparisonAxis,
            maximumOrthogonalResidual,
            minimumInlierCount,
            minimumInlierRatio,
            minimumInlierScanlineSpan,
            anchorX,
            anchorY,
            anchorZ,
            directionX,
            directionY,
            directionZ,
            segmentStartX,
            segmentStartY,
            segmentStartZ,
            segmentEndX,
            segmentEndY,
            segmentEndZ,
            diagnostics,
            copy,
            provenance,
            hash);
    }

    private static string CalculateContentSha256(
        string outputEntityId,
        C3DHeightDifferenceEdgePointSet input,
        double maximumOrthogonalResidual,
        int minimumInlierCount,
        double minimumInlierRatio,
        int minimumInlierScanlineSpan,
        double anchorX,
        double anchorY,
        double anchorZ,
        double directionX,
        double directionY,
        double directionZ,
        double segmentStartX,
        double segmentStartY,
        double segmentStartZ,
        double segmentEndX,
        double segmentEndY,
        double segmentEndZ,
        C3DLineFeatureDiagnostics diagnostics,
        IReadOnlyList<C3DLineFeaturePointDiagnostic> points)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("OpenVisionLab.C3DLineFeature");
            writer.Write(ContractVersion);
            writer.Write(outputEntityId);
            writer.Write(input.OutputEntityId);
            writer.Write(input.ContentSha256.ToUpperInvariant());
            writer.Write(input.RootSourceEntityId);
            writer.Write(input.RootSourceSha256.ToUpperInvariant());
            writer.Write(input.Unit);
            writer.Write(input.FrameId);
            writer.Write("column-rawHeight-row");
            writer.Write("source-coordinate");
            writer.Write((int)input.ComparisonAxis);
            writer.Write(nameof(C3DLineFitMethod.DeterministicConsensusOrthogonalTls));
            writer.Write(maximumOrthogonalResidual);
            writer.Write(minimumInlierCount);
            writer.Write(minimumInlierRatio);
            writer.Write(minimumInlierScanlineSpan);
            writer.Write("Sha256PairSchedule");
            writer.Write(256);
            writer.Write("OrthogonalTlsUntilStable10");
            writer.Write("PositiveScanlineAxis");
            writer.Write("InlierProjectionExtents");
            writer.Write(anchorX); writer.Write(anchorY); writer.Write(anchorZ);
            writer.Write(directionX); writer.Write(directionY); writer.Write(directionZ);
            writer.Write(segmentStartX); writer.Write(segmentStartY); writer.Write(segmentStartZ);
            writer.Write(segmentEndX); writer.Write(segmentEndY); writer.Write(segmentEndZ);
            writer.Write(diagnostics.InputPointCount);
            writer.Write(diagnostics.InlierCount);
            writer.Write(diagnostics.OutlierCount);
            writer.Write(diagnostics.InlierRatio);
            writer.Write(diagnostics.InlierScanlineMinimum);
            writer.Write(diagnostics.InlierScanlineMaximum);
            writer.Write(diagnostics.InlierScanlineSpan);
            writer.Write(diagnostics.ResidualRms);
            writer.Write(diagnostics.ResidualMaximum);
            writer.Write(diagnostics.ResidualMedian);
            writer.Write(diagnostics.ProjectedSegmentLength);
            writer.Write(diagnostics.HypothesisCount);
            writer.Write(diagnostics.RefinementIterationCount);
            writer.Write(points.Count);
            foreach (var point in points)
            {
                writer.Write(point.InputPointIndex);
                writer.Write(point.ScanlineIndex);
                writer.Write(point.X); writer.Write(point.Y); writer.Write(point.Z);
                writer.Write(point.ProjectedX); writer.Write(point.ProjectedY); writer.Write(point.ProjectedZ);
                writer.Write(point.OrthogonalResidual);
                writer.Write(point.IsInlier);
            }
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}
