using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.Vision3D.FeatureExtraction;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Builds deterministic source-quality evidence from the full-resolution C3D
/// inspection snapshot. It does not infer acquisition channels or calibration.
/// </summary>
public static class C3DSourceQualityAnalyzer
{
    public const string InvalidCellMaskContractVersion = C3DInvalidCellMap.ContractVersion;
    public const string InvalidCellMaskEncoding = C3DInvalidCellMap.Encoding;
    public const string MissingSamplePolicy = "zero-or-non-finite-is-missing";
    public const string CoordinateConvention = "column-rawHeight-row";

    public static SourceQualityReport Create(
        C3DHeightFieldSnapshot snapshot,
        int distributionBinCount = C3DHeightDistribution.DefaultBinCount)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (distributionBinCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(distributionBinCount),
                distributionBinCount,
                "Source-quality distribution bin count must be positive.");
        }

        var values = snapshot.Values.Span;
        var cellCount = checked((long)snapshot.Width * snapshot.Height);
        if (values.Length != cellCount
            || snapshot.ValidCount < 0
            || snapshot.MissingCount < 0
            || snapshot.ValidCount + snapshot.MissingCount != cellCount)
        {
            throw new InvalidDataException(
                "C3D source-quality analysis requires a complete row-major snapshot with exact valid/missing counts.");
        }

        var invalidCellMap = C3DInvalidCellMap.Create(snapshot);
        var distribution = snapshot.ValidCount == 0
            ? null
            : CreateDistribution(
                snapshot.ValueList,
                snapshot.ValidCount,
                distributionBinCount);

        return new SourceQualityReport(
            SourceQualityReport.CurrentSchemaVersion,
            new SourceQualitySourceIdentity(
                snapshot.EntityId,
                "C3D",
                snapshot.SourcePath,
                snapshot.ByteLength,
                snapshot.ContentSha256,
                snapshot.RootSourceSha256),
            new SourceQualityGrid(snapshot.Width, snapshot.Height, cellCount),
            new SourceQualityCoverage(
                cellCount,
                snapshot.ValidCount,
                snapshot.MissingCount,
                snapshot.ValidCount / (double)cellCount,
                snapshot.MissingCount / (double)cellCount,
                MissingSamplePolicy,
                invalidCellMap.Identity),
            new SourceQualityHeightStatistics(
                snapshot.ScalarMeaning,
                snapshot.ValidCount == 0 ? null : snapshot.Minimum,
                snapshot.ValidCount == 0 ? null : snapshot.Maximum,
                snapshot.ValidCount == 0 ? null : snapshot.Mean,
                distribution),
            new SourceQualityCoordinateContext(
                snapshot.Unit,
                snapshot.FrameId,
                CoordinateConvention),
            snapshot.Provenance,
            snapshot.IsDerived,
            SourceChannelCatalogAnalyzer.CreateForC3DHeightGrid());
    }

    private static SourceQualityDistribution CreateDistribution(
        IReadOnlyList<double> values,
        int expectedValidCount,
        int binCount)
    {
        var result = new HeightDistributionStatisticsTool().Execute(
            values,
            new HeightDistributionStatisticsOptions
            {
                BinCount = binCount,
                ZeroIsMissing = false,
                ExpectedValidSampleCount = expectedValidCount
            });
        if (!result.Success)
        {
            throw new InvalidDataException(
                result.Message.Replace(
                    "Height-distribution",
                    "Source-quality distribution",
                    StringComparison.Ordinal));
        }

        var bins = new long[result.Bins.Count];
        for (var index = 0; index < bins.Length; index++)
        {
            bins[index] = result.Bins[index];
        }

        return new SourceQualityDistribution(
            binCount,
            result.PeakBinIndex,
            Array.AsReadOnly(bins));
    }

}
