using System.ComponentModel;
using System.Windows.Input;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Compatibility facade for the independently owned, view-only Completeness
/// Review projection and navigation state.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    private ToolWorkbenchCompletenessReviewOwner completenessReviewOwner = null!;

    public IReadOnlyList<CompletenessCellReviewItem> CompletenessCellResults =>
        completenessReviewOwner.CompletenessCellResults;

    public bool HasCompletenessCellResults =>
        completenessReviewOwner.HasCompletenessCellResults;

    public bool CanNavigateCompletenessFailures =>
        completenessReviewOwner.CanNavigateCompletenessFailures;

    public string? SelectedCompletenessCellId =>
        completenessReviewOwner.SelectedCompletenessCellId;

    public ICommand PreviousCompletenessFailureCommand =>
        completenessReviewOwner.PreviousCompletenessFailureCommand;

    public ICommand NextCompletenessFailureCommand =>
        completenessReviewOwner.NextCompletenessFailureCommand;

    public ICommand SelectCompletenessCellCommand =>
        completenessReviewOwner.SelectCompletenessCellCommand;

    public string CompletenessFailureNavigationSummary =>
        completenessReviewOwner.CompletenessFailureNavigationSummary;

    private void InitializeCompletenessReview()
    {
        completenessReviewOwner = new ToolWorkbenchCompletenessReviewOwner(
            cellId => HeightImageViewer.SetSelectedCompletenessCellId(cellId),
            Localize);
        completenessReviewOwner.PropertyChanged += (_, args) =>
            OnPropertyChanged(args.PropertyName);
    }

    private void OnCompletenessLocalizationChanged(
        object? sender,
        PropertyChangedEventArgs args) =>
        RefreshCompletenessCellReview();

    private void RefreshCompletenessCellReview() =>
        completenessReviewOwner.Rebuild(CreateCompletenessReviewSnapshot());

    private ToolWorkbenchCompletenessReviewSnapshot CreateCompletenessReviewSnapshot() =>
        new(
            IsSelectedStepCompletenessGrid,
            HasCurrentMeasurementPreview,
            IsSelectedStepCompletenessGrid && HasCurrentMeasurementPreview
                ? CurrentMeasurementOutput?.CompletenessGrid
                : null,
            PipelineSteps
                .Select(step => new ToolWorkbenchCompletenessTabSnapshot(
                    step.Id,
                    step.ToolId,
                    step.ToolName,
                    step.OutputEntityId))
                .ToArray());
}
