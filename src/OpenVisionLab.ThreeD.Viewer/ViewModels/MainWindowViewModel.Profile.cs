using System.Globalization;
using System.Numerics;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

public sealed partial class MainWindowViewModel
{
    private bool profileVisible;
    private string profileSummary = "Profile: choose P1 and P2 on the C3D height grid.";
    private string profileEndpointSummary = "P1: not set | P2: not set";
    private string profileRange = "Height range: pending";
    private string profilePathData = "M 0,30 L 240,30";
    private int profileValidSampleCount;
    private int profileMissingSampleCount;

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
    }
}
