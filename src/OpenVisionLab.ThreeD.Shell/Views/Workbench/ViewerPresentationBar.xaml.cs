using System.Windows;
using System.Windows.Controls;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public partial class ViewerPresentationBar : UserControl
{
    public static readonly DependencyProperty ViewerViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewerViewModel),
            typeof(MainWindowViewModel),
            typeof(ViewerPresentationBar),
            new PropertyMetadata(null, OnViewerViewModelChanged));

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

    public MainWindowViewModel? ViewerViewModel
    {
        get => (MainWindowViewModel?)GetValue(ViewerViewModelProperty);
        set => SetValue(ViewerViewModelProperty, value);
    }

    public string SlotLabel
    {
        get => (string)GetValue(SlotLabelProperty);
        set => SetValue(SlotLabelProperty, value);
    }

    private static void OnViewerViewModelChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args) =>
        ((ViewerPresentationBar)dependencyObject).UpdatePresentationVisibility();

    private void UpdatePresentationVisibility() =>
        Visibility = ViewerViewModel is null
            ? Visibility.Collapsed
            : Visibility.Visible;

    private void DecreaseHeightMinimum_Click(object sender, RoutedEventArgs args) =>
        ViewerViewModel?.ShiftC3DHeightColorMinimum(-1);

    private void IncreaseHeightMinimum_Click(object sender, RoutedEventArgs args) =>
        ViewerViewModel?.ShiftC3DHeightColorMinimum(1);

    private void DecreaseHeightMaximum_Click(object sender, RoutedEventArgs args) =>
        ViewerViewModel?.ShiftC3DHeightColorMaximum(-1);

    private void IncreaseHeightMaximum_Click(object sender, RoutedEventArgs args) =>
        ViewerViewModel?.ShiftC3DHeightColorMaximum(1);

    private void ResetHeightRange_Click(object sender, RoutedEventArgs args) =>
        ViewerViewModel?.ResetC3DHeightColorRange();
}
