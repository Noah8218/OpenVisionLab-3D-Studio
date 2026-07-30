using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

public sealed record C3DLevelingReferenceRegion(
    string SelectionId,
    int Row,
    int Column,
    int RowCount,
    int ColumnCount,
    int ValidSampleCount);

/// <summary>
/// Immutable raw-height leveling evidence. The transform preserves X/Z grid
/// coordinates and detrends only Y; it is not a rigid-body pose or a re-grid.
/// </summary>
public sealed class C3DLevelingTransform
{
    public const string ContractVersion = "1.0";
    public const string ReferenceFitPolicy = "LeastSquaresHeightPlane";
    public const string LevelingPolicy = "HeightDetrendToReferenceMean";
    public const string MissingValuePolicy = "PreserveMask";
    public const string GridPolicy = "PreserveSourceGrid";
    private readonly C3DLevelingReferenceRegion[] referenceRegions;

    private C3DLevelingTransform(
        string outputEntityId,
        string rootSourceEntityId,
        string rootSourceSha256,
        string sourceUnit,
        string sourceFrameId,
        int sourceGridWidth,
        int sourceGridHeight,
        C3DAffineMatrix3x4 matrix,
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
        RootSourceEntityId = rootSourceEntityId;
        RootSourceSha256 = rootSourceSha256;
        SourceUnit = sourceUnit;
        SourceFrameId = sourceFrameId;
        SourceGridWidth = sourceGridWidth;
        SourceGridHeight = sourceGridHeight;
        Matrix = matrix;
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
    public string RootSourceEntityId { get; }
    public string RootSourceSha256 { get; }
    public string SourceUnit { get; }
    public string SourceFrameId { get; }
    public int SourceGridWidth { get; }
    public int SourceGridHeight { get; }
    public C3DAffineMatrix3x4 Matrix { get; }
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

    public double TransformHeight(int row, int column, double rawHeight) =>
        Matrix.Transform(column, rawHeight, row).Y;

    public static C3DLevelingTransform Create(
        string outputEntityId,
        string rootSourceEntityId,
        string rootSourceSha256,
        string sourceUnit,
        string sourceFrameId,
        int sourceGridWidth,
        int sourceGridHeight,
        double fittedSlopeX,
        double fittedSlopeZ,
        double fittedIntercept,
        double targetHeight,
        int referenceSampleCount,
        double referenceResidualRms,
        double referenceResidualPeakToValley,
        IReadOnlyList<C3DLevelingReferenceRegion> referenceRegions,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootSourceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootSourceSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUnit);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFrameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        ArgumentNullException.ThrowIfNull(referenceRegions);
        if (sourceGridWidth <= 0 || sourceGridHeight <= 0
            || referenceSampleCount < 3
            || !double.IsFinite(fittedSlopeX)
            || !double.IsFinite(fittedSlopeZ)
            || !double.IsFinite(fittedIntercept)
            || !double.IsFinite(targetHeight)
            || !double.IsFinite(referenceResidualRms)
            || referenceResidualRms < 0
            || !double.IsFinite(referenceResidualPeakToValley)
            || referenceResidualPeakToValley < 0)
        {
            throw new InvalidDataException("LevelingTransform requires a finite plane, residual evidence, positive grid dimensions, and at least three samples.");
        }

        var regions = referenceRegions.ToArray();
        if (regions.Length == 0
            || regions.Any(region =>
                string.IsNullOrWhiteSpace(region.SelectionId)
                || region.Row < 0
                || region.Column < 0
                || region.RowCount <= 0
                || region.ColumnCount <= 0
                || region.ValidSampleCount < 0
                || region.Row > sourceGridHeight - region.RowCount
                || region.Column > sourceGridWidth - region.ColumnCount)
            || regions.Select(region => region.SelectionId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != regions.Length)
        {
            throw new InvalidDataException("LevelingTransform reference regions must be unique valid source-grid rectangles.");
        }

        var matrix = new C3DAffineMatrix3x4(
            1, 0, 0, 0,
            -fittedSlopeX, 1, -fittedSlopeZ, targetHeight - fittedIntercept,
            0, 0, 1, 0);
        var hash = CalculateContentSha256(
            outputEntityId,
            rootSourceEntityId,
            rootSourceSha256,
            sourceUnit,
            sourceFrameId,
            sourceGridWidth,
            sourceGridHeight,
            matrix,
            fittedSlopeX,
            fittedSlopeZ,
            fittedIntercept,
            targetHeight,
            referenceSampleCount,
            referenceResidualRms,
            referenceResidualPeakToValley,
            regions);
        return new C3DLevelingTransform(
            outputEntityId,
            rootSourceEntityId,
            rootSourceSha256.ToUpperInvariant(),
            sourceUnit,
            sourceFrameId,
            sourceGridWidth,
            sourceGridHeight,
            matrix,
            fittedSlopeX,
            fittedSlopeZ,
            fittedIntercept,
            targetHeight,
            referenceSampleCount,
            referenceResidualRms,
            referenceResidualPeakToValley,
            regions,
            provenance,
            hash);
    }

    private static string CalculateContentSha256(
        string outputEntityId,
        string rootSourceEntityId,
        string rootSourceSha256,
        string sourceUnit,
        string sourceFrameId,
        int width,
        int height,
        C3DAffineMatrix3x4 matrix,
        double slopeX,
        double slopeZ,
        double intercept,
        double targetHeight,
        int sampleCount,
        double residualRms,
        double residualPeakToValley,
        IReadOnlyList<C3DLevelingReferenceRegion> regions)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("OpenVisionLab.C3DLevelingTransform");
        writer.Write(ContractVersion);
        writer.Write(outputEntityId);
        writer.Write(rootSourceEntityId);
        writer.Write(rootSourceSha256.ToUpperInvariant());
        writer.Write(sourceUnit);
        writer.Write(sourceFrameId);
        writer.Write(width);
        writer.Write(height);
        writer.Write(ReferenceFitPolicy);
        writer.Write(LevelingPolicy);
        writer.Write(MissingValuePolicy);
        writer.Write(GridPolicy);
        foreach (var value in matrix.Values) writer.Write(value);
        writer.Write(slopeX);
        writer.Write(slopeZ);
        writer.Write(intercept);
        writer.Write(targetHeight);
        writer.Write(sampleCount);
        writer.Write(residualRms);
        writer.Write(residualPeakToValley);
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
}
