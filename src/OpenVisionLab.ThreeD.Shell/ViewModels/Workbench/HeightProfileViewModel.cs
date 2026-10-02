using System.ComponentModel;
using System.Windows.Input;
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
        nameof(ProfileBodyVisible),
        nameof(ProfileHasEndpoints),
        nameof(ProfileIsPicking),
        nameof(ProfileGridDistance),
        nameof(ProfileHeightDelta),
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
    private readonly RelayCommand profileActionCommand;
    private readonly RelayCommand traceCursorCommand;

    public HeightProfileViewModel(IOpenVisionThreeDViewerHost viewerHost)
    {
        this.viewerHost = viewerHost ?? throw new ArgumentNullException(nameof(viewerHost));
        traceCursorCommand = new RelayCommand(parameter => this.viewerHost?.TrySetHeightProfileCursor((double)parameter!),
            parameter => !isDisposed && parameter is double && Profile.HasEndpoints);
        profileActionCommand = new RelayCommand(parameter => this.viewerHost?.TryRequestHeightProfile((ViewerHeightProfileAction)parameter!),
            parameter => !isDisposed && parameter is ViewerHeightProfileAction action && CanExecute(action));
        viewerHost.HostStateChanged += OnHostStateChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand TraceCursorCommand => traceCursorCommand;
    public ICommand ProfileActionCommand => profileActionCommand;
    public bool ProfileVisible => Profile.Visible;
    public bool ProfileBodyVisible => Profile.Visible && !Profile.IsCollapsed;
    public bool ProfileHasEndpoints => Profile.HasEndpoints;
    public bool ProfileIsPicking => Profile.IsPicking;
    public string ProfileGridDistance => Profile.GridDistance;
    public string ProfileHeightDelta => Profile.HeightDelta;

    private bool CanExecute(ViewerHeightProfileAction action) => action switch
    {
        ViewerHeightProfileAction.Begin or ViewerHeightProfileAction.Repick => viewerHost?.HostState.C3DSampleVisible == true,
        ViewerHeightProfileAction.Cancel => Profile.IsPicking,
        ViewerHeightProfileAction.Clear or ViewerHeightProfileAction.ToggleCollapsed => Profile.Visible,
        _ => Profile.HasEndpoints && !Profile.IsPicking
    };

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
        profileActionCommand.RaiseCanExecuteChanged();
        traceCursorCommand.RaiseCanExecuteChanged();
    }

    private ViewerHostProfileState Profile =>
        viewerHost?.HostState.Profile ?? ViewerHostProfileState.Empty;

    private void OnHostStateChanged(object? sender, ViewerHostStateChangedEventArgs args)
    {
        if (isDisposed || args.PropertyName != nameof(ViewerHostState.Profile))
        {
            return;
        }

        profileActionCommand.RaiseCanExecuteChanged();
        foreach (var propertyName in BindingPropertyNames)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
