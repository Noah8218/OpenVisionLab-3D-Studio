using System.IO;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.Vision3D.FeatureExtraction;
using OpenVisionLab.Vision3D.Inspection;

namespace OpenVisionLab.ThreeD.Viewer.Models;

/// <summary>Owns one temporary A/B trace. It never writes a recipe or executes an inspection.</summary>
internal sealed class HeightProfileSession
{
    private C3DHeightGrid? source;
    private (HeightGridPoint? First, HeightGridPoint? Second, bool Collapsed)? backup;
    private int pickPhase;

    public event EventHandler? Changed;
    public HeightGridPoint? First { get; private set; }
    public HeightGridPoint? Second { get; private set; }
    public HeightGridPoint[] Samples { get; private set; } = [];
    public string? SourceSha256 => source?.ContentSha256;
    public bool IsVisible { get; private set; }
    public bool IsCollapsed { get; private set; }
    public bool IsPicking => pickPhase != 0;
    public bool HasEndpoints => First is not null && Second is not null;
    public int ActiveEndpoint { get; private set; } = 1;
    public int ExpectedSampleCount { get; private set; }
    public double Minimum { get; private set; }
    public double Maximum { get; private set; }
    public double Mean { get; private set; }
    public double GridDistance { get; private set; }
    public double HeightDelta { get; private set; }

    public void Attach(C3DHeightGrid? value)
    {
        if (ReferenceEquals(source, value)) return;
        var same = source is not null && value is not null
            && string.Equals(source.ContentSha256, value.ContentSha256, StringComparison.OrdinalIgnoreCase)
            && source.Width == value.Width && source.Height == value.Height;
        source = value;
        if (!same) Clear();
    }

    public bool Begin()
    {
        if (source is null) return false;
        if (!IsPicking) backup = (First, Second, IsCollapsed);
        pickPhase = 1;
        IsVisible = true;
        IsCollapsed = false;
        Notify();
        return true;
    }

    public bool Pick(HeightGridPoint point)
    {
        if (!IsPicking || ReadExactPoint(point) is not { } exact || exact.RawValue != point.RawValue) return false;
        if (pickPhase == 1)
        {
            First = exact;
            Second = null;
            Samples = [exact];
            ExpectedSampleCount = 1;
            ActiveEndpoint = 1;
            pickPhase = 2;
        }
        else
        {
            if (First is not { } first || SameCell(first, exact)) return false;
            Second = exact;
            ActiveEndpoint = 2;
            pickPhase = 0;
            backup = null;
            Rebuild();
        }
        Notify();
        return true;
    }

    public bool Move(int endpoint, HeightGridPoint point)
    {
        if (IsPicking || !HasEndpoints || ReadExactPoint(point) is not { } exact || exact.RawValue != point.RawValue) return false;
        var other = endpoint == 1 ? Second : First;
        var current = endpoint == 1 ? First : Second;
        if (endpoint is not (1 or 2) || other is { } opposite && SameCell(opposite, exact) || current is { } retained && SameCell(retained, exact)) return false;
        if (endpoint == 1) First = exact;
        else Second = exact;
        ActiveEndpoint = endpoint;
        Rebuild();
        Notify();
        return true;
    }

    public void SelectEndpoint(int endpoint)
    {
        if (endpoint is 1 or 2) ActiveEndpoint = endpoint;
    }

    public bool Nudge(int columns, int rows)
    {
        if (source is null || (ActiveEndpoint == 1 ? First : Second) is not { } endpoint) return false;
        var row = endpoint.Row + rows;
        var column = endpoint.Column + columns;
        return row >= 0 && row < source.Height && column >= 0 && column < source.Width
            && ReadPoint(row, column) is { } point && Move(ActiveEndpoint, point);
    }

    public void Cancel()
    {
        if (!IsPicking) return;
        if (backup is { } previous)
        {
            First = previous.First;
            Second = previous.Second;
            IsCollapsed = previous.Collapsed;
            IsVisible = First is not null;
        }
        else IsVisible = false;
        pickPhase = 0;
        backup = null;
        Rebuild();
        Notify();
    }

    public void Swap()
    {
        if (!HasEndpoints || IsPicking) return;
        (First, Second) = (Second, First);
        ActiveEndpoint = ActiveEndpoint == 1 ? 2 : 1;
        Rebuild();
        Notify();
    }

    public void ToggleCollapsed()
    {
        if (!IsVisible) return;
        IsCollapsed = !IsCollapsed;
        Notify();
    }

    public void Clear()
    {
        First = null;
        Second = null;
        Samples = [];
        backup = null;
        pickPhase = 0;
        IsVisible = false;
        IsCollapsed = false;
        ExpectedSampleCount = 0;
        Minimum = Maximum = Mean = GridDistance = HeightDelta = 0;
        Notify();
    }

    public int GetSampleStep(HeightGridPoint point) => First is { } first
        ? Math.Max(Math.Abs(point.Row - first.Row), Math.Abs(point.Column - first.Column)) : 0;

    private HeightGridPoint? ReadExactPoint(HeightGridPoint point) => ReadPoint(point.Row, point.Column);

    private HeightGridPoint? ReadPoint(int row, int column)
    {
        if (source is null || row < 0 || row >= source.Height || column < 0 || column >= source.Width) return null;
        try { return source.ReadPoint(row, column); }
        catch (InvalidDataException) { return null; }
    }

    internal HeightGridPoint? GetTracePoint(double progress)
    {
        if (!HasEndpoints || !double.IsFinite(progress) || progress < 0 || progress > 1) return null;
        var step = (int)Math.Round(progress * (ExpectedSampleCount - 1));
        foreach (var point in Samples) if (GetSampleStep(point) == step) return point;
        return null;
    }

    private void Rebuild()
    {
        Samples = First is { } firstOnly ? [firstOnly] : [];
        ExpectedSampleCount = Samples.Length;
        if (source is null || First is not { } first || Second is not { } second) return;
        Samples = source.ReadLineProfile(first.Row, first.Column, second.Row, second.Column);
        ExpectedSampleCount = Math.Max(Math.Abs(second.Row - first.Row), Math.Abs(second.Column - first.Column)) + 1;
        var statistics = new HeightDistributionStatisticsTool().Execute(Samples.Select(point => (double)point.RawValue).ToArray(),
            new HeightDistributionStatisticsOptions { BinCount = 1, ZeroIsMissing = false });
        if (!statistics.Success) throw new InvalidDataException(statistics.Message);
        Minimum = statistics.Minimum;
        Maximum = statistics.Maximum;
        Mean = statistics.Mean;
        // Grid pitch and raw height have different units. The SDK's planar result is in cells;
        // a physical 3D length needs an authored coordinate/calibration contract.
        var dimensions = new PointPairDimensionsInspectionTool().Execute(
            new ThreeDPoint(first.Column, 0, first.Row), new ThreeDPoint(second.Column, 0, second.Row),
            new ThreeDPoint(0, 1, 0), first.RawValue, second.RawValue, new PointPairDimensionsInspectionOptions());
        GridDistance = dimensions.PlanarWidth;
        HeightDelta = dimensions.ScalarHeightDelta;
    }

    private void Notify() => Changed?.Invoke(this, EventArgs.Empty);
    private static bool SameCell(HeightGridPoint first, HeightGridPoint second) => first.Row == second.Row && first.Column == second.Column;
}
