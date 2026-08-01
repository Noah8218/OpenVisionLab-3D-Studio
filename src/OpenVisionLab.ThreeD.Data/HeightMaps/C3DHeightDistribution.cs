using Lib.ThreeD.FeatureExtraction;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Full-source raw-height distribution for a loaded C3D grid.
/// Zero and non-finite cells follow the C3D missing-value contract and are excluded.
/// </summary>
public sealed class C3DHeightDistribution
{
    public const int DefaultBinCount = 32;

    private C3DHeightDistribution(
        double minimum,
        double maximum,
        double mean,
        int validSampleCount,
        int missingSampleCount,
        int[] bins,
        int peakBinIndex,
        double[] binLowerBounds,
        double[] binUpperBounds,
        double peakFraction,
        bool isConstant,
        double peakCenter)
    {
        Minimum = minimum;
        Maximum = maximum;
        Mean = mean;
        ValidSampleCount = validSampleCount;
        MissingSampleCount = missingSampleCount;
        Bins = Array.AsReadOnly(bins);
        PeakBinIndex = peakBinIndex;
        this.binLowerBounds = binLowerBounds;
        this.binUpperBounds = binUpperBounds;
        PeakFraction = peakFraction;
        IsConstant = isConstant;
        PeakCenter = peakCenter;
    }

    private readonly double[] binLowerBounds;
    private readonly double[] binUpperBounds;

    public double Minimum { get; }

    public double Maximum { get; }

    public double Mean { get; }

    public int ValidSampleCount { get; }

    public int MissingSampleCount { get; }

    public IReadOnlyList<int> Bins { get; }

    public int BinCount => Bins.Count;

    public int PeakBinIndex { get; }

    public int PeakSampleCount => Bins[PeakBinIndex];

    public double PeakFraction { get; }

    public bool IsConstant { get; }

    public double PeakLowerBound => GetBinLowerBound(PeakBinIndex);

    public double PeakUpperBound => GetBinUpperBound(PeakBinIndex);

    public double PeakCenter { get; }

    public double GetBinLowerBound(int index)
    {
        ValidateBinIndex(index);
        return binLowerBounds[index];
    }

    public double GetBinUpperBound(int index)
    {
        ValidateBinIndex(index);
        return binUpperBounds[index];
    }

    internal static C3DHeightDistribution Create(
        HeightGridSummaryResult summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        if (!summary.Success
            || !summary.HasFiniteSamples
            || summary.Bins.Count == 0
            || summary.BinLowerBounds.Count != summary.Bins.Count
            || summary.BinUpperBounds.Count != summary.Bins.Count)
        {
            throw new InvalidDataException(
                "C3D height distribution requires a completed Library-Noah height-grid summary.");
        }

        return new C3DHeightDistribution(
            summary.Minimum,
            summary.Maximum,
            summary.Mean,
            summary.ValidSampleCount,
            summary.MissingSampleCount,
            summary.Bins.ToArray(),
            summary.PeakBinIndex,
            summary.BinLowerBounds.ToArray(),
            summary.BinUpperBounds.ToArray(),
            summary.PeakFraction,
            summary.IsConstant,
            summary.PeakCenter);
    }

    private void ValidateBinIndex(int index)
    {
        if (index < 0 || index >= BinCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Height-distribution bin must be between 0 and {BinCount - 1}.");
        }
    }
}
