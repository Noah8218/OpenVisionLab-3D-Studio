using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Input;
using OpenVisionLab;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// View-only review navigation for deterministic Completeness Grid evidence.
/// Selection is presentation state: it never edits the recipe or executes a tool.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    private static readonly Regex TabThicknessNamePattern = new(
        @"^Tab\s+(?<number>[1-9]\d*)\s+Thickness$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private RelayCommand previousCompletenessFailureCommand = null!;
    private RelayCommand nextCompletenessFailureCommand = null!;
    private RelayCommand selectCompletenessCellCommand = null!;
    private IReadOnlyList<CompletenessCellReviewItem> completenessCellResults = [];
    private string? selectedCompletenessCellId;

    public IReadOnlyList<CompletenessCellReviewItem> CompletenessCellResults =>
        completenessCellResults;
    public bool HasCompletenessCellResults =>
        IsSelectedStepCompletenessGrid && CompletenessCellResults.Count > 0;
    public bool CanNavigateCompletenessFailures =>
        CompletenessCellResults.Count(item => item.Status == ResultStatus.Fail) > 0;
    public string? SelectedCompletenessCellId => selectedCompletenessCellId;
    public ICommand PreviousCompletenessFailureCommand => previousCompletenessFailureCommand;
    public ICommand NextCompletenessFailureCommand => nextCompletenessFailureCommand;
    public ICommand SelectCompletenessCellCommand => selectCompletenessCellCommand;
    public string CompletenessFailureNavigationSummary
    {
        get
        {
            var failed = CompletenessCellResults
                .Where(item => item.Status == ResultStatus.Fail)
                .ToArray();
            if (failed.Length == 0)
            {
                return Localize("실패 셀 없음", "No failed cells");
            }

            var selectedFailureIndex = Array.FindIndex(
                failed,
                item => string.Equals(
                    item.CellId,
                    SelectedCompletenessCellId,
                    StringComparison.OrdinalIgnoreCase));
            return selectedFailureIndex >= 0
                ? Localize(
                    $"실패 {selectedFailureIndex + 1}/{failed.Length}",
                    $"Failure {selectedFailureIndex + 1}/{failed.Length}")
                : Localize(
                    $"실패 {failed.Length}개",
                    $"{failed.Length} failures");
        }
    }

    private void InitializeCompletenessReview()
    {
        previousCompletenessFailureCommand = new RelayCommand(
            _ => NavigateCompletenessFailure(-1),
            _ => CanNavigateCompletenessFailures);
        nextCompletenessFailureCommand = new RelayCommand(
            _ => NavigateCompletenessFailure(1),
            _ => CanNavigateCompletenessFailures);
        selectCompletenessCellCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is CompletenessCellReviewItem item)
                {
                    SelectCompletenessCell(item.CellId);
                }
            },
            parameter => parameter is CompletenessCellReviewItem);
        Localization.PropertyChanged += (_, _) =>
        {
            RefreshCompletenessCellReview();
            OnPropertyChanged(nameof(CompletenessFailureNavigationSummary));
        };
    }

    private void RefreshCompletenessCellReview()
    {
        var output = IsSelectedStepCompletenessGrid && HasCurrentMeasurementPreview
            ? measurementPreviewOutput?.CompletenessGrid
            : null;
        if (output is null)
        {
            completenessCellResults = [];
            SetSelectedCompletenessCellId(null);
            NotifyCompletenessCellReviewState();
            return;
        }

        var tabIdentities = CreateTabThicknessIdentityMap(PipelineSteps);
        var requestedSelection = output.Cells.Any(cell =>
            string.Equals(cell.CellId, selectedCompletenessCellId, StringComparison.OrdinalIgnoreCase))
            ? selectedCompletenessCellId
            : output.Cells.FirstOrDefault(cell => cell.Decision == ResultStatus.Fail)?.CellId
              ?? output.Cells.FirstOrDefault()?.CellId;

        completenessCellResults = output.Cells
            .Select((cell, index) =>
            {
                tabIdentities.TryGetValue(index + 1, out var tab);
                return new CompletenessCellReviewItem(
                    cell.CellId,
                    tab?.DisplayName ?? Localize($"셀 {index + 1}", $"Cell {index + 1}"),
                    tab?.StepId ?? string.Empty,
                    tab?.OutputEntityId ?? string.Empty,
                    cell.Region,
                    cell.Decision ?? ResultStatus.Warning,
                    cell.FiniteCoverageRatio,
                    cell.ReferenceRelativeMeanRawHeight,
                    string.Equals(cell.CellId, requestedSelection, StringComparison.OrdinalIgnoreCase));
            })
            .ToArray();
        SetSelectedCompletenessCellId(requestedSelection, rebuildItems: false);
        NotifyCompletenessCellReviewState();
    }

    private void NavigateCompletenessFailure(int direction)
    {
        var failed = CompletenessCellResults
            .Where(item => item.Status == ResultStatus.Fail)
            .ToArray();
        if (failed.Length == 0)
        {
            return;
        }

        var index = Array.FindIndex(
            failed,
            item => string.Equals(
                item.CellId,
                SelectedCompletenessCellId,
                StringComparison.OrdinalIgnoreCase));
        var next = index < 0
            ? direction < 0 ? failed.Length - 1 : 0
            : (index + direction + failed.Length) % failed.Length;
        SelectCompletenessCell(failed[next].CellId);
    }

    private void SelectCompletenessCell(string cellId) =>
        SetSelectedCompletenessCellId(cellId, rebuildItems: true);

    private void SetSelectedCompletenessCellId(
        string? cellId,
        bool rebuildItems = true)
    {
        if (string.Equals(
            selectedCompletenessCellId,
            cellId,
            StringComparison.OrdinalIgnoreCase))
        {
            HeightImageViewer.SetSelectedCompletenessCellId(cellId);
            return;
        }

        selectedCompletenessCellId = cellId;
        if (rebuildItems && completenessCellResults.Count > 0)
        {
            completenessCellResults = completenessCellResults
                .Select(item => item with
                {
                    IsSelected = string.Equals(
                        item.CellId,
                        cellId,
                        StringComparison.OrdinalIgnoreCase)
                })
                .ToArray();
            OnPropertyChanged(nameof(CompletenessCellResults));
        }

        HeightImageViewer.SetSelectedCompletenessCellId(cellId);
        OnPropertyChanged(nameof(SelectedCompletenessCellId));
        OnPropertyChanged(nameof(CompletenessFailureNavigationSummary));
    }

    private void NotifyCompletenessCellReviewState()
    {
        OnPropertyChanged(nameof(CompletenessCellResults));
        OnPropertyChanged(nameof(HasCompletenessCellResults));
        OnPropertyChanged(nameof(CanNavigateCompletenessFailures));
        OnPropertyChanged(nameof(SelectedCompletenessCellId));
        OnPropertyChanged(nameof(CompletenessFailureNavigationSummary));
        previousCompletenessFailureCommand?.RaiseCanExecuteChanged();
        nextCompletenessFailureCommand?.RaiseCanExecuteChanged();
        selectCompletenessCellCommand?.RaiseCanExecuteChanged();
    }

    internal static IReadOnlyDictionary<int, CompletenessTabIdentity>
        CreateTabThicknessIdentityMap(IEnumerable<ToolWorkbenchPipelineStepItem> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        var result = new Dictionary<int, CompletenessTabIdentity>();
        foreach (var step in steps)
        {
            if (!string.Equals(step.ToolId, "thickness", StringComparison.Ordinal))
            {
                continue;
            }

            var match = TabThicknessNamePattern.Match(step.ToolName);
            if (!match.Success
                || !int.TryParse(
                    match.Groups["number"].Value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var number)
                || result.ContainsKey(number))
            {
                continue;
            }

            result.Add(
                number,
                new CompletenessTabIdentity(
                    number,
                    step.Id,
                    step.ToolName,
                    step.OutputEntityId));
        }

        return result;
    }

}

public sealed record CompletenessCellReviewItem(
    string CellId,
    string DisplayName,
    string MappedThicknessStepId,
    string MappedThicknessOutputEntityId,
    ToolRecipeGridRectangle Region,
    ResultStatus Status,
    double FiniteCoverageRatio,
    double? ReferenceRelativeMeanRawHeight,
    bool IsSelected)
{
    public string StatusText => Status.ToString();
    public string EvidenceSummary => ReferenceRelativeMeanRawHeight is { } relative
        ? string.Create(
            CultureInfo.InvariantCulture,
            $"coverage {FiniteCoverageRatio:P1} | relative mean {relative:+0.###;-0.###;0}")
        : string.Create(
            CultureInfo.InvariantCulture,
            $"coverage {FiniteCoverageRatio:P1} | relative mean unavailable");
    public string IdentitySummary => string.IsNullOrWhiteSpace(MappedThicknessStepId)
        ? string.Empty
        : $"{MappedThicknessStepId} → {MappedThicknessOutputEntityId}";
    public bool HasMappedThicknessIdentity =>
        !string.IsNullOrWhiteSpace(MappedThicknessStepId);
}

internal sealed record CompletenessTabIdentity(
    int Number,
    string StepId,
    string DisplayName,
    string OutputEntityId);
