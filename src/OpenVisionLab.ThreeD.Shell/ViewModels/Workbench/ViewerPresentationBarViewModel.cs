using System.ComponentModel;
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
        nameof(C3DHeightColorRangeSummary)
    ];

    private IOpenVisionThreeDViewerHost? viewerHost;
    private bool isDisposed;
    private readonly RelayCommand decreaseHeightMinimumCommand;
    private readonly RelayCommand increaseHeightMinimumCommand;
    private readonly RelayCommand decreaseHeightMaximumCommand;
    private readonly RelayCommand increaseHeightMaximumCommand;
    private readonly RelayCommand resetHeightColorRangeCommand;

    public ViewerPresentationBarViewModel(IOpenVisionThreeDViewerHost viewerHost)
    {
        this.viewerHost = viewerHost ?? throw new ArgumentNullException(nameof(viewerHost));
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
        if (viewerHost is { } host)
        {
            host.HostStateChanged -= OnHostStateChanged;
        }

        viewerHost = null;
        decreaseHeightMinimumCommand.RaiseCanExecuteChanged();
        increaseHeightMinimumCommand.RaiseCanExecuteChanged();
        decreaseHeightMaximumCommand.RaiseCanExecuteChanged();
        increaseHeightMaximumCommand.RaiseCanExecuteChanged();
        resetHeightColorRangeCommand.RaiseCanExecuteChanged();
    }

    private ViewerHostPresentationState Presentation =>
        viewerHost?.HostState.Presentation ?? ViewerHostPresentationState.Empty;

    private bool CanExecuteHostCommand() => !isDisposed && viewerHost is not null;

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
        foreach (var propertyName in BindingPropertyNames)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
