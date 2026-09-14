using System.ComponentModel;
using OpenVisionLab.ThreeD.Viewer.Hosting;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// WPF binding adapter for the read-only Height Profile surface. The Viewer
/// remains the state owner; this type only projects the public Host snapshot
/// and owns the HostStateChanged subscription lifetime.
/// </summary>
public sealed class HeightProfileViewModel : INotifyPropertyChanged, IDisposable
{
    private static readonly string[] BindingPropertyNames =
    [
        nameof(ProfileVisible),
        nameof(ProfileSummary),
        nameof(ProfileEndpointSummary),
        nameof(ProfileRange),
        nameof(ProfilePathData),
        nameof(ProfileValidSampleCount),
        nameof(ProfileMissingSampleCount),
        nameof(ProfileLinkedCursorVisible),
        nameof(ProfileLinkedCursorX),
        nameof(ProfileLinkedCursorY),
        nameof(ProfileLinkedCursorMarkerLeft),
        nameof(ProfileLinkedCursorMarkerTop),
        nameof(ProfileLinkedCursorSummary)
    ];

    private IOpenVisionThreeDViewerHost? viewerHost;
    private bool isDisposed;

    public HeightProfileViewModel(IOpenVisionThreeDViewerHost viewerHost)
    {
        this.viewerHost = viewerHost ?? throw new ArgumentNullException(nameof(viewerHost));
        viewerHost.HostStateChanged += OnHostStateChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool ProfileVisible => Profile.Visible;

    public string ProfileSummary => Profile.Summary;

    public string ProfileEndpointSummary => Profile.EndpointSummary;

    public string ProfileRange => Profile.Range;

    public string ProfilePathData => Profile.PathData;

    public int ProfileValidSampleCount => Profile.ValidSampleCount;

    public int ProfileMissingSampleCount => Profile.MissingSampleCount;

    public bool ProfileLinkedCursorVisible => Profile.LinkedCursorVisible;

    public double ProfileLinkedCursorX => Profile.LinkedCursorX;

    public double ProfileLinkedCursorY => Profile.LinkedCursorY;

    public double ProfileLinkedCursorMarkerLeft => Profile.LinkedCursorMarkerLeft;

    public double ProfileLinkedCursorMarkerTop => Profile.LinkedCursorMarkerTop;

    public string ProfileLinkedCursorSummary => Profile.LinkedCursorSummary;

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        if (viewerHost is { } host)
        {
            host.HostStateChanged -= OnHostStateChanged;
        }

        viewerHost = null;
    }

    private ViewerHostProfileState Profile =>
        viewerHost?.HostState.Profile ?? ViewerHostProfileState.Empty;

    private void OnHostStateChanged(object? sender, ViewerHostStateChangedEventArgs args)
    {
        if (isDisposed || args.PropertyName != nameof(ViewerHostState.Profile))
        {
            return;
        }

        foreach (var propertyName in BindingPropertyNames)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
