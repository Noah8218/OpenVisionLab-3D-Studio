using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public partial class SelectedToolWorkspaceView : UserControl
{
    public static readonly DependencyProperty IsLabPresentationProperty = DependencyProperty.Register(nameof(IsLabPresentation), typeof(bool), typeof(SelectedToolWorkspaceView), new PropertyMetadata(false));

    public static readonly DependencyProperty ParameterDraftRejectionVersionProperty = DependencyProperty.Register(
        nameof(ParameterDraftRejectionVersion), typeof(int), typeof(SelectedToolWorkspaceView), new PropertyMetadata(0, OnRejectedParameterDraftChanged));

    private RadioButton? returnSettingsPage;

    public SelectedToolWorkspaceView()
    {
        InitializeComponent();
        SetBinding(ParameterDraftRejectionVersionProperty, new Binding("StepParameterDraftRejectionVersion") { Mode = BindingMode.OneWay });
        Loaded += OnLoaded;
    }

    public int ParameterDraftRejectionVersion
    {
        get => (int)GetValue(ParameterDraftRejectionVersionProperty);
        set => SetValue(ParameterDraftRejectionVersionProperty, value);
    }

    private static void OnRejectedParameterDraftChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        var view = (SelectedToolWorkspaceView)owner;
        if (args.NewValue is int version && version != 0 && view.IsLoaded && !view.IsLabPresentation) view.RevealParameterSettings();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (ParameterDraftRejectionVersion != 0 && !IsLabPresentation) RevealParameterSettings();
        else if (EditorHistoryTab.IsChecked != true && !new[] { EditorAllTab, EditorRegionTab, EditorJudgmentTab, EditorDetailTab }
            .Any(tab => tab.IsChecked == true && tab.Visibility == Visibility.Visible)) SelectDefaultEditorPage();
    }

    public bool IsLabPresentation
    {
        get => (bool)GetValue(IsLabPresentationProperty);
        set => SetValue(IsLabPresentationProperty, value);
    }

    public bool IsFailureCorrectionContextVisible =>
        TeachFailureCorrectionContext.Visibility == Visibility.Visible;

    public bool IsContextualStepSetupVisible =>
        SelectedToolSetupCard.Visibility == Visibility.Visible;

    // Inspect each control's own contract even when a headless dock pane is inactive.
    public bool HasDirectToolCatalogReturn =>
        SelectedToolSetupBackToToolsButton.Visibility == Visibility.Visible;

    public bool HasVisibleStepSetupStatus =>
        SelectedToolSetupStatusText.Visibility == Visibility.Visible
        && !string.IsNullOrWhiteSpace(SelectedToolSetupStatusText.Text);

    public int VisibleContextualStepSetupActionCount =>
        new[]
        {
            SelectedToolSetupRepairButton,
            SelectedToolSetupFirstRoiButton,
            SelectedToolSetupSecondRoiButton,
            SelectedToolSetupApplyRoiButton,
            SelectedToolSetupApplyParametersButton,
        }.Count(button => button.Visibility == Visibility.Visible);

    public bool CommitPendingParameterEdit(out string message)
    {
        if (!StepPropertyGrid.CommitPendingEdit(out message))
        {
            RevealParameterSettings();
            return false;
        }
        if (AcceptancePropertyGrid.IsVisible && !AcceptancePropertyGrid.CommitPendingEdit(out message))
        {
            EditorJudgmentTab.IsChecked = true;
            AcceptanceLimitsExpander.IsExpanded = true;
            AcceptanceLimitsExpander.BringIntoView();
            AcceptancePropertyGrid.FocusInvalidEditor();
            return false;
        }
        return true;
    }

    private void OnFooterToolMenuClick(object sender, RoutedEventArgs args)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.IsOpen = true;
        }
    }

    private void OnShowParameterSettingsClicked(object sender, RoutedEventArgs args) => RevealParameterSettings();

    public void RevealParameterSettings()
    {
        EditorDetailTab.IsChecked = true;
        DetailParametersTab.IsChecked = true;
        ParametersExpander.IsExpanded = true;
        ShowAdvancedSamples.IsChecked = true;
        UpdateLayout();
        ParametersExpander.BringIntoView();
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        {
            if (IsLoaded && EditorDetailTab.IsChecked == true && DetailParametersTab.IsChecked == true) StepPropertyGrid.FocusInvalidEditor();
        }));
    }

    public void BringOutputIntoView()
    {
        EditorDetailTab.IsChecked = true;
        DetailIoTab.IsChecked = true;
        SettingsDetails.IsExpanded = true;
        UpdateLayout();
        OutputSection.BringIntoView();
    }

    public bool HasThicknessRepeatGridAuthoringControls =>
        ThicknessRepeatGridPanel is not null;

    public bool HasExplicitAuthoringActions =>
        SelectedToolActionBar is not null
        && PreviewActionButton is not null
        && CancelPreviewActionButton is not null;

    public bool HasVisibleInspectionContext =>
        SelectedToolInspectionContext.IsVisible
        && InspectionContextInputName.IsVisible
        && InspectionContextMainDisplay.IsVisible
        && !string.IsNullOrWhiteSpace(InspectionContextStepId.Text)
        && !string.IsNullOrWhiteSpace(InspectionContextInputName.Text)
        && !string.IsNullOrWhiteSpace(InspectionContextInputId.Text)
        && !string.IsNullOrWhiteSpace(InspectionContextMainDisplay.Text)
        && !string.IsNullOrWhiteSpace(InspectionContextMainDisplayId.Text)
        && InspectionContextRegions.Items.Count > 0
        && InspectionContextRegions.Items
            .Cast<SelectedToolRegionItem>()
            .All(region => !string.IsNullOrWhiteSpace(region.Label)
                           && !string.IsNullOrWhiteSpace(region.State)
                           && !string.IsNullOrWhiteSpace(region.SelectionId));

    public string InspectionContextState =>
        $"step={InspectionContextStepId.Text}; input={InspectionContextInputName.Text}|{InspectionContextInputId.Text}; main={InspectionContextMainDisplay.Text}|{InspectionContextMainDisplayId.Text}; regions={InspectionContextRegions.Items.Count}; detailsExpanded={InspectionContextDetails.IsExpanded}";

    public bool HasExclusiveWorkspaceSurface =>
        !(SourceQualityWorkspace.IsVisible
          && SelectedToolScrollViewer.IsVisible);

    public IReadOnlyList<string> GetVisibleTextLayout()
    {
        UpdateLayout();
        var lines = new List<string>();
        CollectVisibleText(this, lines);
        return lines;
    }

    public void BringThicknessRepeatGridIntoView()
    {
        EditorRegionTab.IsChecked = true;
        ThicknessRepeatGridPanel.BringIntoView();
    }

    private void OnSelectedToolStepSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (EditorAllTab is not null)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(SelectDefaultEditorPage));
        }
    }

    private void SelectDefaultEditorPage()
    {
        if (!IsLoaded) return;
        returnSettingsPage = EditorRegionTab.Visibility == Visibility.Visible ? EditorRegionTab : EditorAllTab;
        returnSettingsPage.IsChecked = true;
    }

    private void OnOpenDiagnosticsClick(object sender, RoutedEventArgs args)
    {
        EditorDetailTab.IsChecked = true;
        DetailParametersTab.IsChecked = true;
    }

    private void OnDetailTopicChecked(object sender, RoutedEventArgs args)
    {
        if (!IsLoaded) return;
        UpdateLayout();
        SelectedToolScrollViewer.ScrollToTop();
    }

    private void OnOpenHistoryClick(object sender, RoutedEventArgs args) => EditorHistoryTab.IsChecked = true;

    private void OnBackToCoreClick(object sender, RoutedEventArgs args)
    {
        (returnSettingsPage ?? (EditorRegionTab.Visibility == Visibility.Visible ? EditorRegionTab : EditorAllTab)).IsChecked = true;
    }

    private void OnEditorPageChecked(object sender, RoutedEventArgs args)
    {
        if (!IsLoaded || sender is not RadioButton { Tag: string page })
        {
            return;
        }

        if (page is "All" or "Region" or "Judgment") returnSettingsPage = (RadioButton)sender;
        if (page == "Judgment") AcceptanceLimitsExpander.IsExpanded = true;

        if (page is "All" or "Detail")
        {
            ParametersExpander.IsExpanded = true;
        }
        if (page is "All" or "Region")
        {
            RegionsExpander.IsExpanded = true;
        }

        UpdateLayout();
        StepPropertyGrid.ScrollToTop();
        SelectedToolScrollViewer.ScrollToTop();
    }

    private void CollectVisibleText(
        DependencyObject owner,
        ICollection<string> lines)
    {
        if (owner is TextBlock textBlock
            && textBlock.IsVisible
            && !string.IsNullOrWhiteSpace(textBlock.Text))
        {
            var bounds = textBlock.TransformToAncestor(this).TransformBounds(
                new Rect(textBlock.RenderSize));
            lines.Add(
                FormattableString.Invariant(
                    $"SelectedToolText|x={bounds.X:F1}|y={bounds.Y:F1}|width={bounds.Width:F1}|height={bounds.Height:F1}|text={textBlock.Text.Replace(Environment.NewLine, " ", StringComparison.Ordinal)}"));
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(owner); index++)
        {
            CollectVisibleText(VisualTreeHelper.GetChild(owner, index), lines);
        }
    }
}
