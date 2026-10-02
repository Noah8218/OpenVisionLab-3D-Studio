using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public partial class SelectedToolWorkspaceView : UserControl
{
    public static readonly DependencyProperty IsLabPresentationProperty = DependencyProperty.Register(nameof(IsLabPresentation), typeof(bool), typeof(SelectedToolWorkspaceView), new PropertyMetadata(false));

    public SelectedToolWorkspaceView()
    {
        InitializeComponent();
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
        if (StepPropertyGrid.CommitPendingEdit(out message)) return true;
        RevealParameterSettings();
        return false;
    }

    private void OnShowParameterSettingsClicked(object sender, RoutedEventArgs args) => RevealParameterSettings();

    public void RevealParameterSettings()
    {
        EditorAllTab.IsChecked = true;
        ParametersExpander.IsExpanded = true;
        ShowAdvancedSamples.IsChecked = true;
        UpdateLayout();
        ParametersExpander.BringIntoView();
        StepPropertyGrid.FocusInvalidEditor();
    }

    public void BringOutputIntoView()
    {
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

    public void BringThicknessRepeatGridIntoView() =>
        ThicknessRepeatGridPanel.BringIntoView();

    private void OnSelectedToolStepSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (EditorAllTab is not null)
        {
            EditorAllTab.IsChecked = true;
        }
    }

    private void OnEditorPageChecked(object sender, RoutedEventArgs args)
    {
        if (!IsLoaded || sender is not RadioButton { Tag: string page })
        {
            return;
        }

        if (page is "All" or "Judgment" or "Region" or "Detail")
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
