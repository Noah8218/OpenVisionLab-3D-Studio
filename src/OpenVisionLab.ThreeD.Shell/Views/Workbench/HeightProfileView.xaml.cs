using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
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

    public static readonly DependencyProperty TraceCursorCommandProperty = DependencyProperty.Register(nameof(TraceCursorCommand), typeof(ICommand), typeof(HeightProfileView));
    public ICommand? TraceCursorCommand { get => (ICommand?)GetValue(TraceCursorCommandProperty); set => SetValue(TraceCursorCommandProperty, value); }
    private HeightProfileViewModel? viewModel;

    public HeightProfileView()
    {
        InitializeComponent();
        SetBinding(TraceCursorCommandProperty, new Binding(nameof(HeightProfileViewModel.TraceCursorCommand)));
    }

    public IOpenVisionThreeDViewerHost? ViewerHost
    {
        get => (IOpenVisionThreeDViewerHost?)GetValue(ViewerHostProperty);
        set => SetValue(ViewerHostProperty, value);
    }

    private void OnTraceMouseMove(object sender, MouseEventArgs args)
    {
        if (sender is not Canvas chart || chart.ActualWidth <= 0) return;
        var progress = Math.Clamp(args.GetPosition(chart).X / chart.ActualWidth, 0, 1);
        if (TraceCursorCommand?.CanExecute(progress) == true) TraceCursorCommand.Execute(progress);
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
