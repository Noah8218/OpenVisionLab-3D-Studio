using System.Windows;
using System.Windows.Controls;

namespace OpenVisionLab.ThreeD.Shell.Views.Recipe;

public partial class RecipeManagerView : UserControl
{
    private const double CompactLayoutBreakpoint = 1050;
    private bool? compactLayout;

    public static readonly DependencyProperty IsCompactProperty =
        DependencyProperty.Register(
            nameof(IsCompact),
            typeof(bool),
            typeof(RecipeManagerView),
            new PropertyMetadata(false));

    public RecipeManagerView()
    {
        InitializeComponent();
    }

    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        private set => SetCurrentValue(IsCompactProperty, value);
    }

    internal void ApplyResponsiveWidthForVerification(double width) => UpdateResponsiveLayout(width);

    private void OnViewSizeChanged(object sender, SizeChangedEventArgs args) => UpdateResponsiveLayout(args.NewSize.Width);

    private void UpdateResponsiveLayout(double width)
    {
        if (width <= 0)
        {
            return;
        }

        var compact = width <= CompactLayoutBreakpoint;
        if (compactLayout == compact)
        {
            return;
        }

        compactLayout = compact;
        IsCompact = compact;
        var columns = RecipeBrowserColumns.ColumnDefinitions;
        var rows = RecipeBrowserColumns.RowDefinitions;
        if (compact)
        {
            columns[0].Width = new GridLength(280);
            columns[0].MinWidth = 280;
            columns[1].Width = new GridLength(1, GridUnitType.Star);
            columns[1].MinWidth = 0;
            columns[2].Width = new GridLength(0);
            columns[2].MinWidth = 0;
            rows[0].Height = GridLength.Auto;
            rows[1].Height = new GridLength(1, GridUnitType.Star);
            Grid.SetColumn(RecipeSectionsPanel, 1);
            Grid.SetColumnSpan(RecipeSectionsPanel, 2);
            Grid.SetRow(RecipeSectionsPanel, 0);
            Grid.SetRowSpan(RecipeSectionsPanel, 1);
            Grid.SetColumn(RecipeDetailsPanel, 1);
            Grid.SetColumnSpan(RecipeDetailsPanel, 2);
            Grid.SetRow(RecipeDetailsPanel, 1);
            Grid.SetRowSpan(RecipeDetailsPanel, 1);
            RecipeSectionsPanel.Padding = new Thickness(8, 4, 8, 4);
            RecipeSectionsPanel.BorderThickness = new Thickness(0, 0, 0, 1);
            RecipeSectionsTitle.Visibility = Visibility.Collapsed;
            RecipeSectionsHint.Visibility = Visibility.Collapsed;
            RecipeBrowserSections.ItemsPanel = (ItemsPanelTemplate)FindResource("RecipeBrowserCompactSectionsPanel");
        }
        else
        {
            columns[0].Width = new GridLength(1.76, GridUnitType.Star);
            columns[0].MinWidth = 285;
            columns[1].Width = new GridLength(1, GridUnitType.Star);
            columns[1].MinWidth = 155;
            columns[2].Width = new GridLength(5.6, GridUnitType.Star);
            columns[2].MinWidth = 300;
            rows[0].Height = new GridLength(0);
            rows[1].Height = new GridLength(1, GridUnitType.Star);
            Grid.SetColumn(RecipeSectionsPanel, 1);
            Grid.SetColumnSpan(RecipeSectionsPanel, 1);
            Grid.SetRow(RecipeSectionsPanel, 0);
            Grid.SetRowSpan(RecipeSectionsPanel, 2);
            Grid.SetColumn(RecipeDetailsPanel, 2);
            Grid.SetColumnSpan(RecipeDetailsPanel, 1);
            Grid.SetRow(RecipeDetailsPanel, 0);
            Grid.SetRowSpan(RecipeDetailsPanel, 2);
            RecipeSectionsPanel.Padding = new Thickness(12, 10, 12, 10);
            RecipeSectionsPanel.BorderThickness = new Thickness(0, 0, 1, 0);
            RecipeSectionsTitle.Visibility = Visibility.Visible;
            RecipeSectionsHint.Visibility = Visibility.Visible;
            RecipeBrowserSections.ItemsPanel = (ItemsPanelTemplate)FindResource("RecipeBrowserWideSectionsPanel");
        }
    }

    private void OnRecipeSelectionChanged(object sender, SelectionChangedEventArgs args) =>
        RecipePreviewScroll?.ScrollToTop();

    private void OnCancelRequested(object sender, RoutedEventArgs args) =>
        Window.GetWindow(this)?.Close();
}
