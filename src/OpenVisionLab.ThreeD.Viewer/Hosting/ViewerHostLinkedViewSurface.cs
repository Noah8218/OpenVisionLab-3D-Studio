using System.ComponentModel;
using System.Windows.Media;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Hosting;

/// <summary>
/// WPF binding surface for the Linked View height-map presentation.
/// <para>
/// The surface forwards the existing ViewModel presentation values. It does
/// not own a second image or visibility state; the ViewModel remains the
/// mutable writer and the Viewer control remains the lifetime owner.
/// </para>
/// </summary>
public sealed class ViewerHostLinkedViewSurface : INotifyPropertyChanged, IDisposable
{
    private readonly MainWindowViewModel viewModel;
    private bool disposed;

    internal ViewerHostLinkedViewSurface(MainWindowViewModel viewModel)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        this.viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool C3DSampleVisible => viewModel.C3DSampleVisible;
    public bool LazSampleVisible => viewModel.LazSampleVisible;
    public bool GlbSampleVisible => viewModel.GlbSampleVisible;
    public ImageSource? HeightMapImageSource => viewModel.HeightMapImageSource;
    public string HeightMapSummary => viewModel.HeightMapSummary;
    public string HeightMapRange => viewModel.HeightMapRange;

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
