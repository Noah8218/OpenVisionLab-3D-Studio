using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace OpenVisionLab.ThreeD.Shell.Views.Shell;

public partial class StudioTitleBarView : UserControl
{
    public static readonly DependencyProperty MenuContentProperty = DependencyProperty.Register(
        nameof(MenuContent), typeof(object), typeof(StudioTitleBarView), new PropertyMetadata(null));

    public static readonly DependencyProperty ActionsContentProperty = DependencyProperty.Register(
        nameof(ActionsContent), typeof(object), typeof(StudioTitleBarView), new PropertyMetadata(null));

    public static readonly DependencyProperty TitleTextProperty = DependencyProperty.Register(
        nameof(TitleText),
        typeof(string),
        typeof(StudioTitleBarView),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubtitleTextProperty = DependencyProperty.Register(
        nameof(SubtitleText),
        typeof(string),
        typeof(StudioTitleBarView),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty StatusTextProperty = DependencyProperty.Register(
        nameof(StatusText),
        typeof(string),
        typeof(StudioTitleBarView),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty RecipeTextProperty = DependencyProperty.Register(
        nameof(RecipeText),
        typeof(string),
        typeof(StudioTitleBarView),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SourceContextTextProperty = DependencyProperty.Register(
        nameof(SourceContextText),
        typeof(string),
        typeof(StudioTitleBarView),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SourceQualityStatusTextProperty = DependencyProperty.Register(
        nameof(SourceQualityStatusText),
        typeof(string),
        typeof(StudioTitleBarView),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SourceQualityStatusToolTipProperty = DependencyProperty.Register(
        nameof(SourceQualityStatusToolTip),
        typeof(string),
        typeof(StudioTitleBarView),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SourceQualityStatusKindProperty = DependencyProperty.Register(
        nameof(SourceQualityStatusKind),
        typeof(string),
        typeof(StudioTitleBarView),
        new PropertyMetadata("Unavailable"));

    public static readonly DependencyProperty IsSourceQualityStatusVisibleProperty = DependencyProperty.Register(
        nameof(IsSourceQualityStatusVisible),
        typeof(bool),
        typeof(StudioTitleBarView),
        new PropertyMetadata(false));

    public static readonly DependencyProperty AlignmentStatusTextProperty = DependencyProperty.Register(
        nameof(AlignmentStatusText),
        typeof(string),
        typeof(StudioTitleBarView),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty RecipeStateTextProperty = DependencyProperty.Register(
        nameof(RecipeStateText),
        typeof(string),
        typeof(StudioTitleBarView),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsRecipeContextVisibleProperty = DependencyProperty.Register(
        nameof(IsRecipeContextVisible),
        typeof(bool),
        typeof(StudioTitleBarView),
        new PropertyMetadata(false));

    public static readonly DependencyProperty RecipeMetaTextProperty = DependencyProperty.Register(nameof(RecipeMetaText), typeof(string), typeof(StudioTitleBarView), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty RecipeDetailTextProperty = DependencyProperty.Register(nameof(RecipeDetailText), typeof(string), typeof(StudioTitleBarView), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty IsRecipeModifiedProperty = DependencyProperty.Register(nameof(IsRecipeModified), typeof(bool), typeof(StudioTitleBarView), new PropertyMetadata(false));
    public static readonly DependencyProperty IsRecipeSaveFailedProperty = DependencyProperty.Register(nameof(IsRecipeSaveFailed), typeof(bool), typeof(StudioTitleBarView), new PropertyMetadata(false));
    private static readonly DependencyPropertyKey IsCompactPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsCompact), typeof(bool), typeof(StudioTitleBarView), new PropertyMetadata(false));
    public static readonly DependencyProperty IsCompactProperty = IsCompactPropertyKey.DependencyProperty;
    private static readonly DependencyPropertyKey RecipeMaximumWidthPropertyKey = DependencyProperty.RegisterReadOnly(nameof(RecipeMaximumWidth), typeof(double), typeof(StudioTitleBarView), new PropertyMetadata(520.0));
    public static readonly DependencyProperty RecipeMaximumWidthProperty = RecipeMaximumWidthPropertyKey.DependencyProperty;

    public StudioTitleBarView()
    {
        InitializeComponent();
        SizeChanged += (_, args) =>
        {
            var compact = args.NewSize.Width <= 1500;
            SetValue(IsCompactPropertyKey, compact);
            SetValue(RecipeMaximumWidthPropertyKey, compact ? 380.0 : 520.0);
            MinHeight = compact ? 36 : 40;
        };
    }

    public event EventHandler? RecipeManagerRequested;
    public bool IsCompact => (bool)GetValue(IsCompactProperty);
    public double RecipeMaximumWidth => (double)GetValue(RecipeMaximumWidthProperty);
    public string RecipeMetaText { get => (string)GetValue(RecipeMetaTextProperty); set => SetValue(RecipeMetaTextProperty, value); }
    public string RecipeDetailText { get => (string)GetValue(RecipeDetailTextProperty); set => SetValue(RecipeDetailTextProperty, value); }
    public bool IsRecipeModified { get => (bool)GetValue(IsRecipeModifiedProperty); set => SetValue(IsRecipeModifiedProperty, value); }
    public bool IsRecipeSaveFailed { get => (bool)GetValue(IsRecipeSaveFailedProperty); set => SetValue(IsRecipeSaveFailedProperty, value); }

    public string TitleText
    {
        get => (string)GetValue(TitleTextProperty);
        set => SetValue(TitleTextProperty, value);
    }

    public object? MenuContent
    {
        get => GetValue(MenuContentProperty);
        set => SetValue(MenuContentProperty, value);
    }

    public object? ActionsContent
    {
        get => GetValue(ActionsContentProperty);
        set => SetValue(ActionsContentProperty, value);
    }

    public string SubtitleText
    {
        get => (string)GetValue(SubtitleTextProperty);
        set => SetValue(SubtitleTextProperty, value);
    }

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public string RecipeText
    {
        get => (string)GetValue(RecipeTextProperty);
        set => SetValue(RecipeTextProperty, value);
    }

    public string SourceContextText
    {
        get => (string)GetValue(SourceContextTextProperty);
        set => SetValue(SourceContextTextProperty, value);
    }

    public string SourceQualityStatusText
    {
        get => (string)GetValue(SourceQualityStatusTextProperty);
        set => SetValue(SourceQualityStatusTextProperty, value);
    }

    public string SourceQualityStatusToolTip
    {
        get => (string)GetValue(SourceQualityStatusToolTipProperty);
        set => SetValue(SourceQualityStatusToolTipProperty, value);
    }

    public string SourceQualityStatusKind
    {
        get => (string)GetValue(SourceQualityStatusKindProperty);
        set => SetValue(SourceQualityStatusKindProperty, value);
    }

    public bool IsSourceQualityStatusVisible
    {
        get => (bool)GetValue(IsSourceQualityStatusVisibleProperty);
        set => SetValue(IsSourceQualityStatusVisibleProperty, value);
    }

    public string AlignmentStatusText
    {
        get => (string)GetValue(AlignmentStatusTextProperty);
        set => SetValue(AlignmentStatusTextProperty, value);
    }

    public string RecipeStateText
    {
        get => (string)GetValue(RecipeStateTextProperty);
        set => SetValue(RecipeStateTextProperty, value);
    }

    public bool IsRecipeContextVisible
    {
        get => (bool)GetValue(IsRecipeContextVisibleProperty);
        set => SetValue(IsRecipeContextVisibleProperty, value);
    }

    public bool HasAccessibleCurrentSourceQualityStatus =>
        AutomationProperties.GetAutomationId(CurrentSourceQualityStatusBadge)
            == "StudioCurrentSourceQualityStatus"
        && CurrentSourceQualityStatusBadge.Visibility
            == (IsSourceQualityStatusVisible ? Visibility.Visible : Visibility.Collapsed)
        && string.Equals(
            CurrentSourceQualityStatusText.Text,
            SourceQualityStatusText,
            StringComparison.Ordinal)
        && string.Equals(
            AutomationProperties.GetName(CurrentSourceQualityStatusBadge),
            SourceQualityStatusText,
            StringComparison.Ordinal)
        && string.Equals(
            AutomationProperties.GetHelpText(CurrentSourceQualityStatusBadge),
            SourceQualityStatusToolTip,
            StringComparison.Ordinal);

    private void RecipeButton_Click(object sender, RoutedEventArgs args) => RecipeManagerRequested?.Invoke(this, EventArgs.Empty);

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs args)
    {
        if (args.OriginalSource is DependencyObject source
            && (FindAncestor<ButtonBase>(source) is not null || FindAncestor<MenuItem>(source) is not null || FindAncestor<ComboBox>(source) is not null))
        {
            return;
        }

        var window = Window.GetWindow(this);
        if (window is null)
        {
            return;
        }

        if (args.ClickCount == 2)
        {
            ToggleMaximizeRestore(window);
            return;
        }

        if (args.ButtonState == MouseButtonState.Pressed)
        {
            window.DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs args)
    {
        if (Window.GetWindow((DependencyObject)sender) is { } window)
        {
            window.WindowState = WindowState.Minimized;
        }
    }

    private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs args)
    {
        if (Window.GetWindow((DependencyObject)sender) is { } window)
        {
            ToggleMaximizeRestore(window);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs args) =>
        Window.GetWindow((DependencyObject)sender)?.Close();

    private static void ToggleMaximizeRestore(Window window)
    {
        if (window.ResizeMode is ResizeMode.NoResize or ResizeMode.CanMinimize)
        {
            return;
        }

        window.WindowState = window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private static T? FindAncestor<T>(DependencyObject source)
        where T : DependencyObject
    {
        for (var current = source; current is not null; current = System.Windows.Media.VisualTreeHelper.GetParent(current))
        {
            if (current is T match)
            {
                return match;
            }
        }

        return null;
    }
}
