using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer.Hosting;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public partial class ViewerPresentationBar : UserControl
{
    private Window? displaySettingsKeyboardOwner;

    public static readonly DependencyProperty ViewerHostProperty =
        DependencyProperty.Register(
            nameof(ViewerHost),
            typeof(IOpenVisionThreeDViewerHost),
            typeof(ViewerPresentationBar),
            new PropertyMetadata(null, OnViewerHostChanged));

    public static readonly DependencyProperty DialogPlacementTargetProperty =
        DependencyProperty.Register(
            nameof(DialogPlacementTarget),
            typeof(FrameworkElement),
            typeof(ViewerPresentationBar),
            new PropertyMetadata(null));

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

    public static readonly DependencyProperty RoiCaptureCommandProperty = DependencyProperty.Register(nameof(RoiCaptureCommand), typeof(ICommand), typeof(ViewerPresentationBar));
    public static readonly DependencyProperty HeightImageProperty = DependencyProperty.Register(nameof(HeightImage), typeof(HeightImageViewerViewModel), typeof(ViewerPresentationBar), new PropertyMetadata(null, OnViewerHostChanged));
    public static readonly DependencyProperty FitRoiCommandProperty = DependencyProperty.Register(nameof(FitRoiCommand), typeof(ICommand), typeof(ViewerPresentationBar));

    public ViewerPresentationBar()
    {
        InitializeComponent();
        UpdatePresentationVisibility();
        Unloaded += OnPresentationBarUnloaded;
    }

    public IOpenVisionThreeDViewerHost? ViewerHost
    {
        get => (IOpenVisionThreeDViewerHost?)GetValue(ViewerHostProperty);
        set => SetValue(ViewerHostProperty, value);
    }

    public FrameworkElement? DialogPlacementTarget
    {
        get => (FrameworkElement?)GetValue(DialogPlacementTargetProperty);
        set => SetValue(DialogPlacementTargetProperty, value);
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

    public ICommand? RoiCaptureCommand
    {
        get => (ICommand?)GetValue(RoiCaptureCommandProperty);
        set => SetValue(RoiCaptureCommandProperty, value);
    }

    public ICommand? FitRoiCommand
    {
        get => (ICommand?)GetValue(FitRoiCommandProperty);
        set => SetValue(FitRoiCommandProperty, value);
    }

    public HeightImageViewerViewModel? HeightImage
    {
        get => (HeightImageViewerViewModel?)GetValue(HeightImageProperty);
        set => SetValue(HeightImageProperty, value);
    }

    private static void OnViewerHostChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        var bar = (ViewerPresentationBar)dependencyObject;
        bar.Presentation?.Dispose();
        bar.Presentation = bar.ViewerHost is IOpenVisionThreeDViewerHost viewerHost
            ? new ViewerPresentationBarViewModel(viewerHost, bar.HeightImage, new OpenVisionLab.ThreeD.Shell.Dialogs.ShellSourceFileDialogService(() => Window.GetWindow(bar)).SelectC3DTextureImage)
            : null;
        bar.UpdatePresentationVisibility();
    }

    private void UpdatePresentationVisibility() =>
        Visibility = Presentation is null
            ? Visibility.Collapsed
            : Visibility.Visible;

    private void OnViewerDisplaySettingsPopupOpened(object? sender, EventArgs args)
    {
        ReleaseDisplaySettingsKeyboardOwner();
        displaySettingsKeyboardOwner = Window.GetWindow(this);
        if (displaySettingsKeyboardOwner is not null) displaySettingsKeyboardOwner.PreviewKeyDown += OnDisplaySettingsPreviewKeyDown;
        Keyboard.Focus(ViewerHeightRangeAuto);
    }

    private void OnDisplaySettingsPreviewKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Key != Key.Escape) return;
        if (ViewerHeightBandCountSelector.IsDropDownOpen)
        {
            ViewerHeightBandCountSelector.SetCurrentValue(ComboBox.IsDropDownOpenProperty, false);
            Keyboard.Focus(ViewerHeightBandCountSelector);
            args.Handled = true;
            return;
        }
        ViewerDisplaySettingsPopup.SetCurrentValue(System.Windows.Controls.Primitives.Popup.IsOpenProperty, false);
        args.Handled = true;
    }

    private void OnViewerDisplaySettingsPopupClosed(object? sender, EventArgs args)
    {
        ReleaseDisplaySettingsKeyboardOwner();
        if (DialogPlacementTarget is { IsVisible: true } anchor) Keyboard.Focus(anchor);
        else if (ViewerDisplaySettingsButton.IsLoaded)
        {
            Keyboard.Focus(ViewerDisplaySettingsButton);
        }
    }

    private void OnPresentationBarUnloaded(object sender, RoutedEventArgs args)
    {
        ViewerDisplaySettingsPopup.SetCurrentValue(System.Windows.Controls.Primitives.Popup.IsOpenProperty, false);
        ReleaseDisplaySettingsKeyboardOwner();
    }

    private void ReleaseDisplaySettingsKeyboardOwner()
    {
        if (displaySettingsKeyboardOwner is not null) displaySettingsKeyboardOwner.PreviewKeyDown -= OnDisplaySettingsPreviewKeyDown;
        displaySettingsKeyboardOwner = null;
    }
}
