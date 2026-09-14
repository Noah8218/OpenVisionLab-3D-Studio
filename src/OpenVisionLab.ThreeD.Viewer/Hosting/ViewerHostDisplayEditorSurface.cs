using System.ComponentModel;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Hosting;

/// <summary>
/// WPF binding surface for the Viewer display-settings editor.
/// <para>
/// The surface forwards the existing display contract. It does not create a
/// second state store or move display policy out of
/// <see cref="ViewerDisplaySettingsViewModel"/>.
/// </para>
/// </summary>
public sealed class ViewerHostDisplayEditorSurface : INotifyPropertyChanged, IDisposable
{
    private readonly ViewerDisplaySettingsViewModel viewModel;
    private bool disposed;

    internal ViewerHostDisplayEditorSurface(ViewerDisplaySettingsViewModel viewModel)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        this.viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<string> AvailableGeometryStyles => viewModel.AvailableGeometryStyles;

    public string SelectedGeometryStyle
    {
        get => viewModel.SelectedGeometryStyle;
        set => viewModel.SelectedGeometryStyle = value;
    }

    public bool CanSelectGeometryStyle => viewModel.CanSelectGeometryStyle;

    public IReadOnlyList<string> AvailableColorMaps => viewModel.AvailableColorMaps;

    public string SelectedColorMap
    {
        get => viewModel.SelectedColorMap;
        set => viewModel.SelectedColorMap = value;
    }

    public bool CanSelectColorMap => viewModel.CanSelectColorMap;

    public IReadOnlyList<ViewerDiagnosticChannelOption> DiagnosticChannelOptions => viewModel.DiagnosticChannelOptions;

    public ViewerDiagnosticChannelOption? SelectedDiagnosticChannel
    {
        get => viewModel.SelectedDiagnosticChannel;
        set => viewModel.SelectedDiagnosticChannel = value;
    }

    public bool CanSelectDiagnosticChannel => viewModel.CanSelectDiagnosticChannel;

    public string DiagnosticChannelSummary => viewModel.DiagnosticChannelSummary;

    public double PointSize
    {
        get => viewModel.PointSize;
        set => viewModel.PointSize = value;
    }

    public IReadOnlyList<string> RenderDensityModes => viewModel.RenderDensityModes;

    public string SelectedRenderDensity
    {
        get => viewModel.SelectedRenderDensity;
        set => viewModel.SelectedRenderDensity = value;
    }

    public string RenderDensitySummary => viewModel.RenderDensitySummary;

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (!disposed)
        {
            PropertyChanged?.Invoke(this, args);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        PropertyChanged = null;
        GC.SuppressFinalize(this);
    }
}
