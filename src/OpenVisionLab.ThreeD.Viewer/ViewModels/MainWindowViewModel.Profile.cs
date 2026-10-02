using System.Globalization;
using System.Numerics;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

public sealed partial class MainWindowViewModel
{
    internal HeightProfileSession HeightProfile { get; } = new();
    internal IReadOnlyList<ViewerHeightProfileTraceCell> ProfileTrace { get; private set; } = Array.Empty<ViewerHeightProfileTraceCell>();
    private bool profileVisible;
    private string profileSummary = "Profile: choose P1 and P2 on the C3D height grid.";
    private string profileEndpointSummary = "P1: not set | P2: not set";
    private string profileRange = "Height range: pending";
    private string profilePathData = "M 0,30 L 240,30";
    private int profileValidSampleCount;
    private int profileMissingSampleCount;
    private bool profileLinkedCursorVisible;
    private double profileLinkedCursorX;
    private double profileLinkedCursorY;
    private double profileLinkedCursorMarkerLeft;
    private double profileLinkedCursorMarkerTop;
    private string profileLinkedCursorSummary = "Linked cursor: unavailable until P1–P2 trace is ready.";

    public bool ProfileIsCollapsed => HeightProfile.IsCollapsed;
    public bool ProfileHasEndpoints => HeightProfile.HasEndpoints;
    public bool ProfileIsPicking => HeightProfile.IsPicking;
    public string ProfileGridDistance => HeightProfile.HasEndpoints ? HeightProfile.GridDistance.ToString("F3", CultureInfo.InvariantCulture) : "—";
    public string ProfileHeightDelta => HeightProfile.HasEndpoints ? HeightProfile.HeightDelta.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture) : "—";

    private bool CanRequestHeightProfile(ViewerHeightProfileAction action) => action switch
    {
        ViewerHeightProfileAction.Begin or ViewerHeightProfileAction.Repick => C3DSampleVisible && !IsTeachingCaptureActive,
        ViewerHeightProfileAction.Cancel => HeightProfile.IsPicking,
        ViewerHeightProfileAction.ToggleCollapsed or ViewerHeightProfileAction.Clear => HeightProfile.IsVisible,
        ViewerHeightProfileAction.Left or ViewerHeightProfileAction.Right or ViewerHeightProfileAction.Up or ViewerHeightProfileAction.Down => HeightProfile.HasEndpoints && !HeightProfile.IsPicking && SelectedSelectionMode == ProfileSelectionMode,
        _ => HeightProfile.HasEndpoints && !HeightProfile.IsPicking
    };

    private void PickHeightProfile(HeightGridPoint point)
    {
        if (!HeightProfile.Pick(point)) return;
        SelectedEntity = "C3D Height Profile";
        PickCoordinate = string.Create(CultureInfo.InvariantCulture, $"column {point.Column} / row {point.Row} / H {point.RawValue:F3} raw-height");
    }

    private void RequestHeightProfile(ViewerHeightProfileAction action)
    {
        switch (action)
        {
            case ViewerHeightProfileAction.Begin:
            case ViewerHeightProfileAction.Repick: HeightProfile.Begin(); break;
            case ViewerHeightProfileAction.Cancel: HeightProfile.Cancel(); break;
            case ViewerHeightProfileAction.Swap: HeightProfile.Swap(); break;
            case ViewerHeightProfileAction.Clear: HeightProfile.Clear(); break;
            case ViewerHeightProfileAction.ToggleCollapsed: HeightProfile.ToggleCollapsed(); break;
            case ViewerHeightProfileAction.SelectA: HeightProfile.SelectEndpoint(1); break;
            case ViewerHeightProfileAction.SelectB: HeightProfile.SelectEndpoint(2); break;
            case ViewerHeightProfileAction.Left: HeightProfile.Nudge(-1, 0); break;
            case ViewerHeightProfileAction.Right: HeightProfile.Nudge(1, 0); break;
            case ViewerHeightProfileAction.Up: HeightProfile.Nudge(0, -1); break;
            case ViewerHeightProfileAction.Down: HeightProfile.Nudge(0, 1); break;
        }
    }

    private void RefreshHeightProfile(object? sender, EventArgs args)
    {
        var session = HeightProfile;
        ProfileTrace = Array.AsReadOnly(session.Samples.Select(point => new ViewerHeightProfileTraceCell(point.Row, point.Column)).ToArray());
        if (!session.IsVisible) ClearProfile();
        else if (session.First is { } first && session.Second is { } second)
        {
            SetProfile(first.Row, first.Column, first.Position, first.RawValue, second.Row, second.Column, second.Position, second.RawValue,
                session.Samples.Length, Math.Max(0, session.ExpectedSampleCount - session.Samples.Length), session.Minimum, session.Maximum, session.Mean,
                C3DSectionProfilePathBuilder.Build(session.Samples, session.Minimum, session.Maximum));
            ProfileEndpointSummary = string.Create(CultureInfo.InvariantCulture, $"A ({first.Column}, {first.Row}) → B ({second.Column}, {second.Row})");
            ProfileRange = string.Create(CultureInfo.InvariantCulture, $"H {session.Minimum:F3}–{session.Maximum:F3} · {session.Samples.Length} / {session.ExpectedSampleCount}");
        }
        else if (session.First is { } start)
        {
            SetProfileStart(start.Row, start.Column, start.Position, start.RawValue);
            ProfilePathData = string.Empty;
            ProfileRange = string.Empty;
            ProfileEndpointSummary = string.Create(CultureInfo.InvariantCulture, $"A ({start.Column}, {start.Row}) → B —");
        }
        else
        {
            ClearProfile();
            ProfileVisible = true;
            ProfileSummary = "Choose A, then B.";
            ProfilePathData = string.Empty;
            ProfileRange = string.Empty;
            ProfileEndpointSummary = "A — · B —";
        }
        OnPropertyChanged(nameof(ProfileIsCollapsed));
        OnPropertyChanged(nameof(ProfileHasEndpoints));
        OnPropertyChanged(nameof(ProfileIsPicking));
        OnPropertyChanged(nameof(ProfileGridDistance));
        OnPropertyChanged(nameof(ProfileHeightDelta));
        profileActionCommand?.RaiseCanExecuteChanged();
    }

    public bool ProfileVisible
    {
        get => profileVisible;
        private set => SetField(ref profileVisible, value);
    }

    public string ProfileSummary
    {
        get => profileSummary;
        private set => SetField(ref profileSummary, value);
    }

    public string ProfileEndpointSummary
    {
        get => profileEndpointSummary;
        private set => SetField(ref profileEndpointSummary, value);
    }

    public string ProfileRange
    {
        get => profileRange;
        private set => SetField(ref profileRange, value);
    }

    public string ProfilePathData
    {
        get => profilePathData;
        private set => SetField(ref profilePathData, value);
    }

    public int ProfileValidSampleCount
    {
        get => profileValidSampleCount;
        private set => SetField(ref profileValidSampleCount, value);
    }

    public int ProfileMissingSampleCount
    {
        get => profileMissingSampleCount;
        private set => SetField(ref profileMissingSampleCount, value);
    }

    public bool ProfileLinkedCursorVisible
    {
        get => profileLinkedCursorVisible;
        private set => SetField(ref profileLinkedCursorVisible, value);
    }

    public double ProfileLinkedCursorX
    {
        get => profileLinkedCursorX;
        private set => SetField(ref profileLinkedCursorX, value);
    }

    public double ProfileLinkedCursorY
    {
        get => profileLinkedCursorY;
        private set => SetField(ref profileLinkedCursorY, value);
    }

    public double ProfileLinkedCursorMarkerLeft
    {
        get => profileLinkedCursorMarkerLeft;
        private set => SetField(ref profileLinkedCursorMarkerLeft, value);
    }

    public double ProfileLinkedCursorMarkerTop
    {
        get => profileLinkedCursorMarkerTop;
        private set => SetField(ref profileLinkedCursorMarkerTop, value);
    }

    public string ProfileLinkedCursorSummary
    {
        get => profileLinkedCursorSummary;
        private set => SetField(ref profileLinkedCursorSummary, value);
    }

    public void SetProfileStart(int row, int column, Vector3 position, float rawHeight)
    {
        ProfileVisible = true;
        ProfileValidSampleCount = 1;
        ProfileMissingSampleCount = 0;
        ProfileSummary = "Profile: P1 set; choose P2.";
        ProfileEndpointSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"P1 ({row},{column}) raw {rawHeight:F3} | P2: not set");
        ProfileRange = string.Create(
            CultureInfo.InvariantCulture,
            $"Viewer P1: X {position.X:F3}, Y {position.Y:F3}, Z {position.Z:F3}");
        ProfilePathData = "M 0,30 L 240,30";
        ClearProfileLinkedCursor();
    }

    public void SetProfile(
        int firstRow,
        int firstColumn,
        Vector3 firstPosition,
        float firstRawHeight,
        int secondRow,
        int secondColumn,
        Vector3 secondPosition,
        float secondRawHeight,
        int validSampleCount,
        int missingSampleCount,
        double minimum,
        double maximum,
        double mean,
        string pathData)
    {
        var distance = Vector3.Distance(firstPosition, secondPosition);
        var rawDelta = secondRawHeight - firstRawHeight;
        ProfileVisible = true;
        ProfileValidSampleCount = validSampleCount;
        ProfileMissingSampleCount = missingSampleCount;
        ProfileSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"P1-P2 profile | distance {distance:F3} viewer | ΔH {rawDelta:F3} raw-height");
        ProfileEndpointSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"P1 ({firstRow},{firstColumn}) {firstRawHeight:F3} → P2 ({secondRow},{secondColumn}) {secondRawHeight:F3}");
        ProfileRange = string.Create(
            CultureInfo.InvariantCulture,
            $"Min {minimum:F3} | Max {maximum:F3} | Mean {mean:F3} raw-height | valid {validSampleCount:N0} | missing {missingSampleCount:N0}");
        ProfilePathData = string.IsNullOrWhiteSpace(pathData) ? "M 0,30 L 240,30" : pathData;
        ClearProfileLinkedCursor();
    }

    public void SetProfileLinkedCursor(
        int row,
        int column,
        double rawHeight,
        int sampleIndex,
        int sampleCount,
        double minimum,
        double maximum)
    {
        const double chartWidth = 240.0;
        const double chartHeight = 54.0;
        const double chartPadding = 3.0;
        const double markerRadius = 3.0;

        if (!ProfileVisible
            || sampleIndex < 0
            || sampleIndex >= sampleCount
            || sampleCount <= 0
            || !double.IsFinite(rawHeight)
            || !double.IsFinite(minimum)
            || !double.IsFinite(maximum))
        {
            ClearProfileLinkedCursor();
            return;
        }

        var x = sampleCount == 1
            ? 0.0
            : chartWidth * sampleIndex / (sampleCount - 1);
        var span = Math.Max(0.001, maximum - minimum);
        var y = chartPadding
            + (1.0 - Math.Clamp((rawHeight - minimum) / span, 0.0, 1.0))
                * (chartHeight - chartPadding * 2.0);

        ProfileLinkedCursorX = x;
        ProfileLinkedCursorY = y;
        ProfileLinkedCursorMarkerLeft = x - markerRadius;
        ProfileLinkedCursorMarkerTop = y - markerRadius;
        ProfileLinkedCursorSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"Linked cursor: ({row},{column}) raw {rawHeight:F3} | sample {sampleIndex + 1:N0}/{sampleCount:N0}");
        if (HeightProfile.HasEndpoints) ProfileLinkedCursorSummary = string.Create(CultureInfo.InvariantCulture, $"X {column} · Y {row} · H {rawHeight:F3} raw-height");
        ProfileLinkedCursorVisible = true;
    }

    public void SetProfileLinkedCursorUnavailable()
    {
        ProfileLinkedCursorVisible = false;
        ProfileLinkedCursorSummary = "Linked cursor: current cell is outside the P1–P2 trace.";
    }

    public void ClearProfileLinkedCursor()
    {
        ProfileLinkedCursorVisible = false;
        ProfileLinkedCursorX = 0.0;
        ProfileLinkedCursorY = 0.0;
        ProfileLinkedCursorMarkerLeft = 0.0;
        ProfileLinkedCursorMarkerTop = 0.0;
        ProfileLinkedCursorSummary = "Linked cursor: unavailable until P1–P2 trace is ready.";
    }

    public void ClearProfile()
    {
        ProfileVisible = false;
        ProfileValidSampleCount = 0;
        ProfileMissingSampleCount = 0;
        ProfileSummary = "Profile: choose P1 and P2 on the C3D height grid.";
        ProfileEndpointSummary = "P1: not set | P2: not set";
        ProfileRange = "Height range: pending";
        ProfilePathData = "M 0,30 L 240,30";
        ClearProfileLinkedCursor();
    }
}
