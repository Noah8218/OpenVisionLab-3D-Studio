using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Viewer.Models;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// WPF binding adapter for the Viewer presentation bar. It owns only binding
/// notification and forwards mutations to the public Viewer Host contract.
/// </summary>
public sealed class ViewerPresentationBarViewModel : INotifyPropertyChanged, IDisposable
{
    private static readonly string[] BindingPropertyNames =
    [
        nameof(ProfileState),
        nameof(HasProfileEndpoints),
        nameof(IsRotateMode),
        nameof(IsMoveMode),
        nameof(IsProfileMode),
        nameof(AvailableColorMaps),
        nameof(SelectedColorMap),
        nameof(CanSelectColorMap),
        nameof(DiagnosticChannelOptions),
        nameof(SelectedDiagnosticChannel),
        nameof(CanSelectDiagnosticChannel),
        nameof(DiagnosticChannelSummary),
        nameof(SelectionOverlayVisible),
        nameof(ResultOverlayVisible),
        nameof(MeasurementVisible),
        nameof(C3DHeightDistributionVisible),
        nameof(C3DHeightColorRangeSummary),
        nameof(C3DHeightEmphasis),
        nameof(HasC3DHeightColorRange)
    ];

    private IOpenVisionThreeDViewerHost? viewerHost;
    private bool isDisposed;
    private bool isDisplaySettingsOpen;
    private bool hasDisplaySettingsRangeError;
    private bool hasDisplaySettingsEmphasisError;
    private bool hasDisplaySettingsStaleError;
    private string draftHeightRangeMode = "auto";
    private string draftHeightRangeMinimum = string.Empty;
    private string draftHeightRangeMaximum = string.Empty;
    private string draftHeightEmphasis = "1";
    private string displaySettingsSourceSha256 = string.Empty;
    private int displaySettingsRangeRevision;
    private readonly RelayCommand profileActionCommand;
    private readonly RelayCommand profilePickCommand;
    private readonly RelayCommand rotateCommand;
    private readonly RelayCommand moveCommand;
    private readonly RelayCommand measureCommand;
    private readonly RelayCommand fitAllCommand;
    private readonly RelayCommand openDisplaySettingsCommand;
    private readonly RelayCommand applyDisplaySettingsCommand;
    private readonly RelayCommand cancelDisplaySettingsCommand;
    private readonly RelayCommand decreaseHeightMinimumCommand;
    private readonly RelayCommand increaseHeightMinimumCommand;
    private readonly RelayCommand decreaseHeightMaximumCommand;
    private readonly RelayCommand increaseHeightMaximumCommand;
    private readonly RelayCommand resetHeightColorRangeCommand;

    public ViewerPresentationBarViewModel(IOpenVisionThreeDViewerHost viewerHost)
    {
        this.viewerHost = viewerHost ?? throw new ArgumentNullException(nameof(viewerHost));
        profileActionCommand = new RelayCommand(parameter => Execute(host => host.TryRequestHeightProfile((ViewerHeightProfileAction)parameter!)),
            parameter => parameter is ViewerHeightProfileAction action && CanRequestProfile(action));
        profilePickCommand = new RelayCommand(parameter => Execute(host => host.TryPickHeightProfileCell((ViewerHeightProfileCell)parameter!)),
            parameter => CanExecuteHostCommand() && parameter is ViewerHeightProfileCell && ProfileState.IsActive);
        rotateCommand = new RelayCommand(_ => Execute(host => host.TrySetSelectionMode("Point")), _ => CanNavigate());
        moveCommand = new RelayCommand(_ => Execute(host => host.TrySetSelectionMode("Move")), _ => CanNavigate());
        measureCommand = new RelayCommand(_ => Execute(host => host.TryRequestHeightProfile(ViewerHeightProfileAction.Begin)), _ => CanExecuteHostCommand() && this.viewerHost?.HostState is { C3DSampleVisible: true, IsTeachingCaptureActive: false });
        fitAllCommand = new RelayCommand(_ => Execute(host => { host.FitAll(); return true; }), _ => CanNavigate());
        openDisplaySettingsCommand = new RelayCommand(_ => IsDisplaySettingsOpen = true, _ => CanExecuteHostCommand());
        applyDisplaySettingsCommand = new RelayCommand(_ => ApplyDisplaySettings(), _ => CanExecuteHostCommand());
        cancelDisplaySettingsCommand = new RelayCommand(_ => IsDisplaySettingsOpen = false, _ => CanExecuteHostCommand());
        decreaseHeightMinimumCommand = new RelayCommand(
            _ => ShiftC3DHeightColorMinimum(-1),
            _ => CanExecuteHostCommand());
        increaseHeightMinimumCommand = new RelayCommand(
            _ => ShiftC3DHeightColorMinimum(1),
            _ => CanExecuteHostCommand());
        decreaseHeightMaximumCommand = new RelayCommand(
            _ => ShiftC3DHeightColorMaximum(-1),
            _ => CanExecuteHostCommand());
        increaseHeightMaximumCommand = new RelayCommand(
            _ => ShiftC3DHeightColorMaximum(1),
            _ => CanExecuteHostCommand());
        resetHeightColorRangeCommand = new RelayCommand(
            _ => ResetC3DHeightColorRange(),
            _ => CanExecuteHostCommand());
        viewerHost.HostStateChanged += OnHostStateChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ViewerHostProfileState ProfileState => viewerHost?.HostState.Profile ?? ViewerHostProfileState.Empty;
    public bool HasProfileEndpoints => ProfileState.HasEndpoints && !ProfileState.IsPicking;
    public ICommand ProfileActionCommand => profileActionCommand;
    public ICommand ProfilePickCommand => profilePickCommand;
    public ICommand RotateCommand => rotateCommand;
    public ICommand MoveCommand => moveCommand;
    public ICommand MeasureCommand => measureCommand;
    public ICommand FitAllCommand => fitAllCommand;
    public bool IsRotateMode => viewerHost?.HostState.SelectionMode == "Point";
    public bool IsMoveMode => viewerHost?.HostState.SelectionMode == "Move";
    public bool IsProfileMode => viewerHost?.HostState.SelectionMode == "Profile";
    private bool CanNavigate() => CanExecuteHostCommand() && viewerHost?.HostState is { } state && !state.IsTeachingCaptureActive && (state.C3DSampleVisible || state.GlbSampleVisible || state.LazSampleVisible);

    private bool CanRequestProfile(ViewerHeightProfileAction action)
    {
        if (!CanExecuteHostCommand() || viewerHost is not { } host) return false;
        var profile = host.HostState.Profile;
        return action switch
        {
            ViewerHeightProfileAction.Begin or ViewerHeightProfileAction.Repick => host.HostState is { C3DSampleVisible: true, IsTeachingCaptureActive: false },
            ViewerHeightProfileAction.Cancel => profile.IsPicking,
            ViewerHeightProfileAction.Clear or ViewerHeightProfileAction.ToggleCollapsed => profile.Visible,
            ViewerHeightProfileAction.Left or ViewerHeightProfileAction.Right or ViewerHeightProfileAction.Up or ViewerHeightProfileAction.Down => profile.IsActive && HasProfileEndpoints,
            _ => Enum.IsDefined(action) && HasProfileEndpoints
        };
    }

    public IReadOnlyList<string> AvailableColorMaps => Presentation.AvailableColorMaps;

    public string SelectedColorMap
    {
        get => Presentation.SelectedColorMap;
        set => Execute(host => host.TrySetSelectedColorMap(value));
    }

    public bool CanSelectColorMap => Presentation.CanSelectColorMap;

    public IReadOnlyList<ViewerDiagnosticChannelOption> DiagnosticChannelOptions =>
        Presentation.DiagnosticChannelOptions;

    public ViewerDiagnosticChannelOption? SelectedDiagnosticChannel
    {
        get => Presentation.SelectedDiagnosticChannel;
        set => Execute(host => host.TrySetSelectedDiagnosticChannel(value));
    }

    public bool CanSelectDiagnosticChannel => Presentation.CanSelectDiagnosticChannel;

    public string DiagnosticChannelSummary => Presentation.DiagnosticChannelSummary;

    public bool SelectionOverlayVisible
    {
        get => viewerHost?.HostState.SelectionOverlayVisible ?? false;
        set => Execute(host => host.TrySetSelectionOverlayVisible(value));
    }

    public bool ResultOverlayVisible
    {
        get => Presentation.ResultOverlayVisible;
        set => Execute(host => host.TrySetResultOverlayVisible(value));
    }

    public bool MeasurementVisible
    {
        get => Presentation.MeasurementVisible;
        set => Execute(host => host.TrySetMeasurementVisible(value));
    }

    public bool C3DHeightDistributionVisible => Presentation.C3DHeightDistributionVisible;

    public string C3DHeightColorRangeSummary => Presentation.C3DHeightColorRangeSummary;

    public int C3DHeightEmphasis => Presentation.C3DHeightEmphasis;

    public bool HasC3DHeightColorRange =>
        viewerHost?.HostState.Presentation.C3DHeightDistributionVisible == true;

    public bool IsDisplaySettingsOpen
    {
        get => isDisplaySettingsOpen;
        set
        {
            if (isDisplaySettingsOpen == value)
            {
                return;
            }

            if (value)
            {
                BeginDisplaySettingsEdit();
            }
            else
            {
                ClearDisplaySettingsDraft();
            }

            isDisplaySettingsOpen = value;
            OnPropertyChanged(nameof(IsDisplaySettingsOpen));
        }
    }

    public string DraftHeightRangeMode
    {
        get => draftHeightRangeMode;
        set
        {
            if (draftHeightRangeMode == value)
            {
                return;
            }

            draftHeightRangeMode = value;
            hasDisplaySettingsRangeError = false;
            OnPropertyChanged(nameof(DraftHeightRangeMode));
            OnPropertyChanged(nameof(HasDisplaySettingsRangeError));
            OnPropertyChanged(nameof(CanEditManualHeightRange));
        }
    }

    public bool CanEditManualHeightRange => DraftHeightRangeMode == "manual";

    public string DraftHeightRangeMinimum
    {
        get => draftHeightRangeMinimum;
        set
        {
            if (draftHeightRangeMinimum == value)
            {
                return;
            }

            draftHeightRangeMinimum = value;
            hasDisplaySettingsRangeError = false;
            OnPropertyChanged(nameof(DraftHeightRangeMinimum));
            OnPropertyChanged(nameof(HasDisplaySettingsRangeError));
        }
    }

    public string DraftHeightRangeMaximum
    {
        get => draftHeightRangeMaximum;
        set
        {
            if (draftHeightRangeMaximum == value)
            {
                return;
            }

            draftHeightRangeMaximum = value;
            hasDisplaySettingsRangeError = false;
            OnPropertyChanged(nameof(DraftHeightRangeMaximum));
            OnPropertyChanged(nameof(HasDisplaySettingsRangeError));
        }
    }

    public string DraftHeightEmphasis
    {
        get => draftHeightEmphasis;
        set
        {
            if (draftHeightEmphasis == value)
            {
                return;
            }

            draftHeightEmphasis = value;
            OnPropertyChanged(nameof(DraftHeightEmphasis));
        }
    }

    public bool HasDisplaySettingsRangeError
    {
        get => hasDisplaySettingsRangeError;
        private set
        {
            if (hasDisplaySettingsRangeError == value)
            {
                return;
            }

            hasDisplaySettingsRangeError = value;
            OnPropertyChanged(nameof(HasDisplaySettingsRangeError));
        }
    }

    public bool HasDisplaySettingsEmphasisError
    {
        get => hasDisplaySettingsEmphasisError;
        private set
        {
            if (hasDisplaySettingsEmphasisError == value)
            {
                return;
            }

            hasDisplaySettingsEmphasisError = value;
            OnPropertyChanged(nameof(HasDisplaySettingsEmphasisError));
        }
    }

    public bool HasDisplaySettingsStaleError
    {
        get => hasDisplaySettingsStaleError;
        private set
        {
            if (hasDisplaySettingsStaleError == value)
            {
                return;
            }

            hasDisplaySettingsStaleError = value;
            OnPropertyChanged(nameof(HasDisplaySettingsStaleError));
        }
    }

    public ICommand OpenDisplaySettingsCommand => openDisplaySettingsCommand;

    public ICommand ApplyDisplaySettingsCommand => applyDisplaySettingsCommand;

    public ICommand CancelDisplaySettingsCommand => cancelDisplaySettingsCommand;

    public ICommand DecreaseHeightMinimumCommand => decreaseHeightMinimumCommand;

    public ICommand IncreaseHeightMinimumCommand => increaseHeightMinimumCommand;

    public ICommand DecreaseHeightMaximumCommand => decreaseHeightMaximumCommand;

    public ICommand IncreaseHeightMaximumCommand => increaseHeightMaximumCommand;

    public ICommand ResetHeightColorRangeCommand => resetHeightColorRangeCommand;

    public void ShiftC3DHeightColorMinimum(int direction) =>
        Execute(host => host.TryShiftC3DHeightColorMinimum(direction));

    public void ShiftC3DHeightColorMaximum(int direction) =>
        Execute(host => host.TryShiftC3DHeightColorMaximum(direction));

    public void ResetC3DHeightColorRange() =>
        Execute(host => host.TryResetC3DHeightColorRange());

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        IsDisplaySettingsOpen = false;
        if (viewerHost is { } host)
        {
            host.HostStateChanged -= OnHostStateChanged;
        }

        viewerHost = null;
        rotateCommand.RaiseCanExecuteChanged();
        moveCommand.RaiseCanExecuteChanged();
        measureCommand.RaiseCanExecuteChanged();
        fitAllCommand.RaiseCanExecuteChanged();
        profileActionCommand.RaiseCanExecuteChanged();
        profilePickCommand.RaiseCanExecuteChanged();
        openDisplaySettingsCommand.RaiseCanExecuteChanged();
        applyDisplaySettingsCommand.RaiseCanExecuteChanged();
        cancelDisplaySettingsCommand.RaiseCanExecuteChanged();
        decreaseHeightMinimumCommand.RaiseCanExecuteChanged();
        increaseHeightMinimumCommand.RaiseCanExecuteChanged();
        decreaseHeightMaximumCommand.RaiseCanExecuteChanged();
        increaseHeightMaximumCommand.RaiseCanExecuteChanged();
        resetHeightColorRangeCommand.RaiseCanExecuteChanged();
    }

    private ViewerHostPresentationState Presentation =>
        viewerHost?.HostState.Presentation ?? ViewerHostPresentationState.Empty;

    private bool CanExecuteHostCommand() => !isDisposed && viewerHost is not null;

    private void BeginDisplaySettingsEdit()
    {
        if (viewerHost is not { } host)
        {
            return;
        }

        var presentation = host.HostState.Presentation;
        DraftHeightRangeMode = presentation.C3DHeightColorRangeAuto ? "auto" : "manual";
        DraftHeightRangeMinimum = FormatRawValue(presentation.C3DHeightColorMinimumRaw);
        DraftHeightRangeMaximum = FormatRawValue(presentation.C3DHeightColorMaximumRaw);
        DraftHeightEmphasis = presentation.C3DHeightEmphasis.ToString(CultureInfo.InvariantCulture);
        displaySettingsSourceSha256 = presentation.C3DHeightDistributionSourceSha256;
        displaySettingsRangeRevision = presentation.C3DHeightColorRangeRevision;
        HasDisplaySettingsRangeError = false;
        HasDisplaySettingsEmphasisError = false;
        HasDisplaySettingsStaleError = false;
    }

    private void ApplyDisplaySettings()
    {
        if (!isDisplaySettingsOpen || isDisposed || viewerHost is not { } host)
        {
            return;
        }

        HasDisplaySettingsRangeError = false;
        HasDisplaySettingsStaleError = false;
        var presentation = host.HostState.Presentation;
        if (!string.Equals(
                displaySettingsSourceSha256,
                presentation.C3DHeightDistributionSourceSha256,
                StringComparison.Ordinal)
            || displaySettingsRangeRevision != presentation.C3DHeightColorRangeRevision)
        {
            HasDisplaySettingsStaleError = true;
            return;
        }

        var isAutoRange = DraftHeightRangeMode == "auto";
        if (!presentation.C3DHeightDistributionVisible || DraftHeightRangeMode is not ("auto" or "manual"))
        {
            HasDisplaySettingsStaleError = true;
            return;
        }

        if (!int.TryParse(DraftHeightEmphasis, NumberStyles.None, CultureInfo.InvariantCulture, out var heightEmphasis)
            || heightEmphasis is not (1 or 2 or 3 or 5))
        {
            HasDisplaySettingsEmphasisError = true;
            return;
        }

        var minimum = double.NaN;
        var maximum = double.NaN;
        if (!isAutoRange
            && (!double.TryParse(DraftHeightRangeMinimum, NumberStyles.Float, CultureInfo.InvariantCulture, out minimum)
                || !double.IsFinite(minimum)
                || !double.TryParse(DraftHeightRangeMaximum, NumberStyles.Float, CultureInfo.InvariantCulture, out maximum)
                || !double.IsFinite(maximum)
                || minimum >= maximum))
        {
            HasDisplaySettingsRangeError = true;
            return;
        }

        var rangeApplied = isAutoRange
            ? presentation.C3DHeightColorRangeAuto || host.TryResetC3DHeightColorRange()
            : (!presentation.C3DHeightColorRangeAuto
                && minimum == presentation.C3DHeightColorMinimumRaw
                && maximum == presentation.C3DHeightColorMaximumRaw)
                || host.TryApplyLinkedC3DHeightColorRange(minimum, maximum);
        if (rangeApplied)
        {
            if (heightEmphasis != presentation.C3DHeightEmphasis && !host.TrySetC3DHeightEmphasis(heightEmphasis))
            {
                HasDisplaySettingsStaleError = true;
                return;
            }

            IsDisplaySettingsOpen = false;
            return;
        }

        HasDisplaySettingsStaleError = true;
    }

    private void ClearDisplaySettingsDraft()
    {
        HasDisplaySettingsRangeError = false;
        HasDisplaySettingsEmphasisError = false;
        HasDisplaySettingsStaleError = false;
    }

    private static string FormatRawValue(double value) =>
        double.IsFinite(value) ? value.ToString("G", CultureInfo.InvariantCulture) : string.Empty;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void Execute(Func<IOpenVisionThreeDViewerHost, bool> operation)
    {
        if (isDisposed || viewerHost is not { } host)
        {
            return;
        }

        operation(host);
        RaiseBindingProperties();
    }

    private void OnHostStateChanged(object? sender, ViewerHostStateChangedEventArgs args) =>
        RaiseBindingProperties();

    private void RaiseBindingProperties()
    {
        profileActionCommand.RaiseCanExecuteChanged();
        profilePickCommand.RaiseCanExecuteChanged();
        rotateCommand.RaiseCanExecuteChanged();
        moveCommand.RaiseCanExecuteChanged();
        measureCommand.RaiseCanExecuteChanged();
        fitAllCommand.RaiseCanExecuteChanged();
        foreach (var propertyName in BindingPropertyNames)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
