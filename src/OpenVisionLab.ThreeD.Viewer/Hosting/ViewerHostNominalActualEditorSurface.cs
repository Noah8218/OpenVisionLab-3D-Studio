using System.ComponentModel;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Hosting;

/// <summary>
/// WPF binding surface for the Nominal/Actual comparison editor.
/// </summary>
public sealed class ViewerHostNominalActualEditorSurface : INotifyPropertyChanged, IDisposable
{
    private readonly NominalActualComparisonViewModel viewModel;
    private bool disposed;

    internal ViewerHostNominalActualEditorSurface(NominalActualComparisonViewModel viewModel)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        this.viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand PreviewCommand => viewModel.PreviewCommand;
    public ICommand CancelCommand => viewModel.CancelCommand;
    public ICommand PublishCommand => viewModel.PublishCommand;

    public bool CanPreview => viewModel.CanPreview;
    public bool CanCancel => viewModel.CanCancel;
    public bool CanPublish => viewModel.CanPublish;
    public bool InputsReady => viewModel.InputsReady;
    public bool OutputEnabled
    {
        get => viewModel.OutputEnabled;
        set => viewModel.OutputEnabled = value;
    }

    public string ActualSourceSummary => viewModel.ActualSourceSummary;
    public string NominalSourceSummary => viewModel.NominalSourceSummary;
    public string QuerySourceSummary => viewModel.QuerySourceSummary;
    public string FrameSummary => viewModel.FrameSummary;
    public string AlignmentSummary => viewModel.AlignmentSummary;
    public string ValidationSummary => viewModel.ValidationSummary;
    public double ProgressPercent => viewModel.ProgressPercent;
    public bool DisplaySamplingChangePending => viewModel.DisplaySamplingChangePending;
    public string CurrentDisplaySamplingSummary => viewModel.CurrentDisplaySamplingSummary;
    public string NextPreviewSamplingSummary => viewModel.NextPreviewSamplingSummary;

    public bool ActualVisible
    {
        get => viewModel.ActualVisible;
        set => viewModel.ActualVisible = value;
    }

    public bool NominalVisible
    {
        get => viewModel.NominalVisible;
        set => viewModel.NominalVisible = value;
    }

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
