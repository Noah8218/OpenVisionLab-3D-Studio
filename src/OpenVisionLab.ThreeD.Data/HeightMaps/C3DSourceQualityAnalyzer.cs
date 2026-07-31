using OpenVisionLab.ThreeD.Core;

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
                values,
                snapshot.Minimum,
                snapshot.Maximum,
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
        ReadOnlySpan<double> values,
        double minimum,
        double maximum,
        int expectedValidCount,
        int binCount)
    {
        var bins = new long[binCount];
        var observedValidCount = 0;
        var span = maximum - minimum;
        foreach (var value in values)
        {
            if (!double.IsFinite(value))
            {
                continue;
            }

            observedValidCount++;
            var binIndex = span == 0.0
                ? 0
                : Math.Min(binCount - 1, (int)((value - minimum) / span * binCount));
            bins[binIndex]++;
        }

        if (observedValidCount != expectedValidCount)
        {
            throw new InvalidDataException(
                $"Source-quality distribution valid-count mismatch: expected {expectedValidCount}, observed {observedValidCount}.");
        }

        var peakBinIndex = 0;
        for (var index = 1; index < bins.Length; index++)
        {
            if (bins[index] > bins[peakBinIndex])
            {
                peakBinIndex = index;
            }
        }

        return new SourceQualityDistribution(
            binCount,
            peakBinIndex,
            Array.AsReadOnly(bins));
    }

}
