using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public partial class SelectedToolWorkspaceView : UserControl
{
    public SelectedToolWorkspaceView()
    {
        InitializeComponent();
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
            SelectedToolSetupPreviewButton,
        }.Count(button => button.Visibility == Visibility.Visible);

    public bool CommitPendingParameterEdit(out string message) =>
        StepPropertyGrid.CommitPendingEdit(out message);

    public void BringOutputIntoView() => OutputSection.BringIntoView();

    public bool HasThicknessRepeatGridAuthoringControls =>
        ThicknessRepeatGridPanel is not null;

    public bool HasExplicitAuthoringActions =>
        SelectedToolActionBar is not null
        && PreviewActionButton is not null
        && PublishActionButton is not null
        && CancelPreviewActionButton is not null
        && SaveRecipeActionButton is not null;

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

    private void OnApplyParametersClick(object sender, RoutedEventArgs args)
    {
        if (DataContext is not ToolWorkbenchViewModel viewModel)
        {
            return;
        }

        if (!StepPropertyGrid.CommitPendingEdit(out var message))
        {
            viewModel.ReportParameterDraftCommitError(message);
            return;
        }

        if (viewModel.ApplySelectedStepParameterDraftCommand.CanExecute(null))
        {
            viewModel.ApplySelectedStepParameterDraftCommand.Execute(null);
        }
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
