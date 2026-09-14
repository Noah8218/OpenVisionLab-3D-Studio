using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace OpenVisionLab.ThreeD.Shell.Views.Shell;

/// <summary>
/// Stable responsibility navigation for the Workbench v4 shell.
/// </summary>
public partial class StudioNavigationRailView : UserControl
{
    private const double CompactWindowWidth = 1500;
    private Window? ownerWindow;

    public StudioNavigationRailView()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty IsCompactProperty =
        DependencyProperty.Register(
            nameof(IsCompact),
            typeof(bool),
            typeof(StudioNavigationRailView),
            new PropertyMetadata(false));

    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        private set => SetValue(IsCompactProperty, value);
    }

    public event EventHandler? RecipeManagerRequested;
    public event EventHandler? LayoutResetRequested;
    public event EventHandler<StudioToolLabRequestEventArgs>? ToolLabRequested;

    public bool HasAccessibleResponsibilityRoutes =>
        new[]
        {
            AuthoringModeButton,
            ValidateModeButton,
            ResultsModeButton,
            CalibrateModeButton,
            AdvancedModeButton,
        }.All(route =>
            !string.IsNullOrWhiteSpace(AutomationProperties.GetName(route))
            && route.ReadLocalValue(ToolTipProperty) != DependencyProperty.UnsetValue);

    public bool HasAccessibleUtilityRoutes =>
        !string.IsNullOrWhiteSpace(AutomationProperties.GetName(RecipeManagerButton))
        && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(ToolLabsMenuButton))
        && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(ResetLayoutButton))
        && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(LanguageSelector));

    public void ApplyResponsiveWidthForVerification(double width) =>
        UpdateResponsiveState(width);

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        ownerWindow = Window.GetWindow(this);
        if (ownerWindow is not null)
        {
            ownerWindow.SizeChanged += OnOwnerWindowSizeChanged;
            UpdateResponsiveState(ownerWindow.ActualWidth);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (ownerWindow is not null)
        {
            ownerWindow.SizeChanged -= OnOwnerWindowSizeChanged;
            ownerWindow = null;
        }
    }

    private void OnOwnerWindowSizeChanged(object sender, SizeChangedEventArgs args) =>
        UpdateResponsiveState(args.NewSize.Width);

    private void UpdateResponsiveState(double width)
    {
        IsCompact = width < CompactWindowWidth;
        Width = IsCompact ? 60 : 140;
    }

    private void OpenRecipeManagerButton_Click(object sender, RoutedEventArgs args) =>
        RecipeManagerRequested?.Invoke(this, EventArgs.Empty);

    private void ResetLayoutButton_Click(object sender, RoutedEventArgs args) =>
        LayoutResetRequested?.Invoke(this, EventArgs.Empty);

    private void OpenToolLabsMenuButton_Click(object sender, RoutedEventArgs args)
    {
        if (ToolLabsMenuButton.ContextMenu is { } menu)
        {
            menu.PlacementTarget = ToolLabsMenuButton;
            menu.IsOpen = true;
        }
    }

    private void OpenToolLabMenuItem_Click(object sender, RoutedEventArgs args)
    {
        if (sender is MenuItem { Tag: string toolId })
        {
            ToolLabRequested?.Invoke(this, new StudioToolLabRequestEventArgs(toolId));
        }
    }
}

public sealed class StudioToolLabRequestEventArgs(string toolId) : EventArgs
{
    public string ToolId { get; } = toolId;
}
