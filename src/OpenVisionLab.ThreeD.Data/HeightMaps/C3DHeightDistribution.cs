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
        int peakBinIndex)
    {
        Minimum = minimum;
        Maximum = maximum;
        Mean = mean;
        ValidSampleCount = validSampleCount;
        MissingSampleCount = missingSampleCount;
        Bins = Array.AsReadOnly(bins);
        PeakBinIndex = peakBinIndex;
    }

    public double Minimum { get; }

    public double Maximum { get; }

    public double Mean { get; }

    public int ValidSampleCount { get; }

    public int MissingSampleCount { get; }

    public IReadOnlyList<int> Bins { get; }

    public int BinCount => Bins.Count;

    public int PeakBinIndex { get; }

    public int PeakSampleCount => Bins[PeakBinIndex];

    public double PeakFraction => PeakSampleCount / (double)ValidSampleCount;

    public bool IsConstant => Minimum == Maximum;

    public double PeakLowerBound => GetBinLowerBound(PeakBinIndex);

    public double PeakUpperBound => GetBinUpperBound(PeakBinIndex);

    public double PeakCenter => IsConstant
        ? Minimum
        : (PeakLowerBound + PeakUpperBound) * 0.5;

    public double GetBinLowerBound(int index)
    {
        ValidateBinIndex(index);
        return IsConstant
            ? Minimum
            : Minimum + (Maximum - Minimum) * index / BinCount;
    }

    public double GetBinUpperBound(int index)
    {
        ValidateBinIndex(index);
        return IsConstant
            ? Maximum
            : Minimum + (Maximum - Minimum) * (index + 1) / BinCount;
    }

    internal static C3DHeightDistribution Create(
        ReadOnlySpan<float> samples,
        float minimum,
        float maximum,
        double mean,
        int validSampleCount,
        int binCount = DefaultBinCount)
    {
        if (binCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(binCount), binCount, "C3D height-distribution bin count must be positive.");
        }

        if (validSampleCount <= 0
            || !float.IsFinite(minimum)
            || !float.IsFinite(maximum)
            || !double.IsFinite(mean)
            || maximum < minimum)
        {
            throw new ArgumentException("C3D height distribution requires finite full-source statistics and at least one valid sample.");
        }

        var bins = new int[binCount];
        var observedValidCount = 0;
        var span = (double)maximum - minimum;
        foreach (var value in samples)
        {
            if (!float.IsFinite(value) || value == 0.0f)
            {
                continue;
            }

            observedValidCount++;
            var index = span == 0.0
                ? 0
                : Math.Min(binCount - 1, (int)(((double)value - minimum) / span * binCount));
            bins[index]++;
        }

        if (observedValidCount != validSampleCount)
        {
            throw new InvalidDataException(
                $"C3D height-distribution valid-count mismatch: expected {validSampleCount}, observed {observedValidCount}.");
        }

        var peakBinIndex = 0;
        for (var index = 1; index < bins.Length; index++)
        {
            if (bins[index] > bins[peakBinIndex])
            {
                peakBinIndex = index;
            }
        }

        return new C3DHeightDistribution(
            minimum,
            maximum,
            mean,
            validSampleCount,
            samples.Length - validSampleCount,
            bins,
            peakBinIndex);
    }

    private void ValidateBinIndex(int index)
    {
        if (index < 0 || index >= BinCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Height-distribution bin must be between 0 and {BinCount - 1}.");
        }
    }
}
