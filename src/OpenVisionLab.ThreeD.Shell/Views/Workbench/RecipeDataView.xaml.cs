using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public partial class RecipeDataView : UserControl
{
    public static readonly DependencyProperty IsTeachingGalleryProperty = DependencyProperty.Register(
        nameof(IsTeachingGallery), typeof(bool), typeof(RecipeDataView), new PropertyMetadata(true));

    public bool IsTeachingGallery
    {
        get => (bool)GetValue(IsTeachingGalleryProperty);
        set => SetValue(IsTeachingGalleryProperty, value);
    }

    private Window? _keyboardOwner;
    private ToolTip? _galleryToolTip;

    public static CustomPopupPlacementCallback GalleryPreviewPlacement { get; } = PlaceGalleryPreview;

    public RecipeDataView()
    {
        InitializeComponent();
        SetBinding(IsTeachingGalleryProperty, new Binding("DataContext.IsTeachWorkspaceSelected")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ToolRecipeWorkbenchView), 1),
            FallbackValue = true
        });
        PreviewKeyDown += OnOwnerPreviewKeyDown;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        var owner = Window.GetWindow(this);
        if (ReferenceEquals(_keyboardOwner, owner)) return;
        if (_keyboardOwner is not null) _keyboardOwner.PreviewKeyDown -= OnOwnerPreviewKeyDown;
        _keyboardOwner = owner;
        if (_keyboardOwner is not null) _keyboardOwner.PreviewKeyDown += OnOwnerPreviewKeyDown;
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (_keyboardOwner is not null) _keyboardOwner.PreviewKeyDown -= OnOwnerPreviewKeyDown;
        _keyboardOwner = null;
        if (_galleryToolTip is { IsOpen: true }) _galleryToolTip.IsOpen = false;
        _galleryToolTip = null;
    }

    private void OnGalleryToolTipOpened(object sender, RoutedEventArgs args)
    {
        if (sender is ToolTip toolTip) _galleryToolTip = toolTip;
    }

    private void OnGalleryToolTipClosed(object sender, RoutedEventArgs args)
    {
        if (ReferenceEquals(_galleryToolTip, sender)) _galleryToolTip = null;
    }

    private void OnOwnerPreviewKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Key == Key.Escape && _galleryToolTip is { IsOpen: true })
        {
            _galleryToolTip.IsOpen = false;
            args.Handled = true;
        }
    }

    private static CustomPopupPlacement[] PlaceGalleryPreview(Size popupSize, Size targetSize, Point offset) =>
    [
        new(new Point(-popupSize.Width - 10 + offset.X, (targetSize.Height - popupSize.Height) / 2 + offset.Y), PopupPrimaryAxis.Vertical),
        new(new Point(targetSize.Width + 10 + offset.X, (targetSize.Height - popupSize.Height) / 2 + offset.Y), PopupPrimaryAxis.Vertical)
    ];
}
