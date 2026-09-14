using System.Windows;
using System.Windows.Controls;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer.Hosting;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public partial class ViewerPresentationBar : UserControl
{
    public static readonly DependencyProperty ViewerHostProperty =
        DependencyProperty.Register(
            nameof(ViewerHost),
            typeof(IOpenVisionThreeDViewerHost),
            typeof(ViewerPresentationBar),
            new PropertyMetadata(null, OnViewerHostChanged));

    public static readonly DependencyProperty PresentationProperty =
        DependencyProperty.Register(
            nameof(Presentation),
            typeof(ViewerPresentationBarViewModel),
            typeof(ViewerPresentationBar),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SlotLabelProperty =
        DependencyProperty.Register(
            nameof(SlotLabel),
            typeof(string),
            typeof(ViewerPresentationBar),
            new PropertyMetadata(string.Empty));

    public ViewerPresentationBar()
    {
        InitializeComponent();
        UpdatePresentationVisibility();
    }

    public IOpenVisionThreeDViewerHost? ViewerHost
    {
        get => (IOpenVisionThreeDViewerHost?)GetValue(ViewerHostProperty);
        set => SetValue(ViewerHostProperty, value);
    }

    public ViewerPresentationBarViewModel? Presentation
    {
        get => (ViewerPresentationBarViewModel?)GetValue(PresentationProperty);
        private set => SetValue(PresentationProperty, value);
    }

    public string SlotLabel
    {
        get => (string)GetValue(SlotLabelProperty);
        set => SetValue(SlotLabelProperty, value);
    }

    private static void OnViewerHostChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        var bar = (ViewerPresentationBar)dependencyObject;
        bar.Presentation?.Dispose();
        bar.Presentation = args.NewValue is IOpenVisionThreeDViewerHost viewerHost
            ? new ViewerPresentationBarViewModel(viewerHost)
            : null;
        bar.UpdatePresentationVisibility();
    }

    private void UpdatePresentationVisibility() =>
        Visibility = Presentation is null
            ? Visibility.Collapsed
            : Visibility.Visible;
}
