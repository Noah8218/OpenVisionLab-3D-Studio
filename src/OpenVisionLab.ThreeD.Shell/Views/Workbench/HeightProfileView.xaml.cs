using System.Windows;
using System.Windows.Controls;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer.Hosting;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public sealed partial class HeightProfileView : UserControl, IDisposable
{
    public static readonly DependencyProperty ViewerHostProperty =
        DependencyProperty.Register(
            nameof(ViewerHost),
            typeof(IOpenVisionThreeDViewerHost),
            typeof(HeightProfileView),
            new PropertyMetadata(null, OnViewerHostChanged));

    private HeightProfileViewModel? viewModel;

    public HeightProfileView()
    {
        InitializeComponent();
    }

    public IOpenVisionThreeDViewerHost? ViewerHost
    {
        get => (IOpenVisionThreeDViewerHost?)GetValue(ViewerHostProperty);
        set => SetValue(ViewerHostProperty, value);
    }

    public void Dispose()
    {
        ViewerHost = null;
        DataContext = null;
    }

    private static void OnViewerHostChanged(
        DependencyObject owner,
        DependencyPropertyChangedEventArgs args)
    {
        if (owner is not HeightProfileView view)
        {
            return;
        }

        view.viewModel?.Dispose();
        view.viewModel = args.NewValue is IOpenVisionThreeDViewerHost host
            ? new HeightProfileViewModel(host)
            : null;
        view.DataContext = view.viewModel;
    }
}
