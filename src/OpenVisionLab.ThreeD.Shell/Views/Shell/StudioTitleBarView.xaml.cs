using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OpenVisionLab.ThreeD.Shell.Views.Shell;

public partial class StudioTitleBarView : UserControl
{
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

    public StudioTitleBarView()
    {
        InitializeComponent();
    }

    public string TitleText
    {
        get => (string)GetValue(TitleTextProperty);
        set => SetValue(TitleTextProperty, value);
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

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs args)
    {
        if (args.OriginalSource is DependencyObject source
            && FindAncestor<Button>(source) is not null)
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
