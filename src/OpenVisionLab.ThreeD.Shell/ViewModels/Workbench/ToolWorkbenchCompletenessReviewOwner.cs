using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using OpenVisionLab;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the view-only Completeness Review projection, selection, failed-cell
/// navigation, and Tab Thickness identity mapping. The composition facade
/// supplies an immutable review snapshot and a presentation-only selection
/// callback; recipe and execution state remain outside this owner.
/// </summary>
internal sealed class ToolWorkbenchCompletenessReviewOwner : INotifyPropertyChanged
{
    private static readonly Regex TabThicknessNamePattern = new(
        @"^Tab\s+(?<number>[1-9]\d*)\s+Thickness$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly Action<string?> setSelectedCompletenessCellId;
    private readonly Func<string, string, string> localize;
    private readonly RelayCommand previousCompletenessFailureCommand;
    private readonly RelayCommand nextCompletenessFailureCommand;
    private readonly RelayCommand selectCompletenessCellCommand;
    private IReadOnlyList<CompletenessCellReviewItem> completenessCellResults = [];
    private string? selectedCompletenessCellId;
    private bool isReviewVisible;

    public ToolWorkbenchCompletenessReviewOwner(
        Action<string?> setSelectedCompletenessCellId,
        Func<string, string, string> localize)
    {
        this.setSelectedCompletenessCellId = setSelectedCompletenessCellId
            ?? throw new ArgumentNullException(nameof(setSelectedCompletenessCellId));
        this.localize = localize
            ?? throw new ArgumentNullException(nameof(localize));

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
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<CompletenessCellReviewItem> CompletenessCellResults =>
        completenessCellResults;

    public bool HasCompletenessCellResults =>
        isReviewVisible && CompletenessCellResults.Count > 0;

    public bool CanNavigateCompletenessFailures =>
        CompletenessCellResults.Any(item => item.Status == ResultStatus.Fail);

    public string? SelectedCompletenessCellId => selectedCompletenessCellId;

    public ICommand PreviousCompletenessFailureCommand =>
        previousCompletenessFailureCommand;

    public ICommand NextCompletenessFailureCommand =>
        nextCompletenessFailureCommand;

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
                return localize("실패 셀 없음", "No failed cells");
            }

            var selectedFailureIndex = Array.FindIndex(
                failed,
                item => string.Equals(
                    item.CellId,
                    SelectedCompletenessCellId,
                    StringComparison.OrdinalIgnoreCase));
            return selectedFailureIndex >= 0
                ? localize(
                    $"실패 {selectedFailureIndex + 1}/{failed.Length}",
                    $"Failure {selectedFailureIndex + 1}/{failed.Length}")
                : localize(
                    $"실패 {failed.Length}개",
                    $"{failed.Length} failures");
        }
    }

    public void Rebuild(ToolWorkbenchCompletenessReviewSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshot.ThicknessTabs);

        var output = snapshot.IsSelectedStepCompletenessGrid
            && snapshot.HasCurrentMeasurementPreview
            ? snapshot.CompletenessGrid
            : null;
        isReviewVisible = output is not null;
        if (output is null)
        {
            completenessCellResults = [];
            SetSelectedCompletenessCellId(null);
            NotifyCompletenessCellReviewState();
            return;
        }

        var tabIdentities = CreateTabThicknessIdentityMap(snapshot.ThicknessTabs);
        var requestedSelection = output.Cells.Any(cell =>
                string.Equals(
                    cell.CellId,
                    selectedCompletenessCellId,
                    StringComparison.OrdinalIgnoreCase))
            ? selectedCompletenessCellId
            : output.Cells.FirstOrDefault(cell => cell.Decision == ResultStatus.Fail)?.CellId
              ?? output.Cells.FirstOrDefault()?.CellId;

        completenessCellResults = output.Cells
            .Select((cell, index) =>
            {
                tabIdentities.TryGetValue(index + 1, out var tab);
                return new CompletenessCellReviewItem(
                    cell.CellId,
                    tab?.DisplayName ?? localize($"셀 {index + 1}", $"Cell {index + 1}"),
                    tab?.StepId ?? string.Empty,
                    tab?.OutputEntityId ?? string.Empty,
                    cell.Region,
                    cell.Decision ?? ResultStatus.Warning,
                    cell.FiniteCoverageRatio,
                    cell.ReferenceRelativeMeanRawHeight,
                    string.Equals(
                        cell.CellId,
                        requestedSelection,
                        StringComparison.OrdinalIgnoreCase));
            })
            .ToArray();
        SetSelectedCompletenessCellId(requestedSelection, rebuildItems: false);
        NotifyCompletenessCellReviewState();
    }

    public void ClearSelection() => SetSelectedCompletenessCellId(null);

    internal static IReadOnlyDictionary<int, CompletenessTabIdentity>
        CreateTabThicknessIdentityMap(
            IEnumerable<ToolWorkbenchCompletenessTabSnapshot> tabs)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        var result = new Dictionary<int, CompletenessTabIdentity>();
        foreach (var tab in tabs)
        {
            if (!string.Equals(tab.ToolId, "thickness", StringComparison.Ordinal))
            {
                continue;
            }

            var match = TabThicknessNamePattern.Match(tab.ToolName);
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
                    tab.Id,
                    tab.ToolName,
                    tab.OutputEntityId));
        }

        return result;
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
        SetSelectedCompletenessCellId(cellId);

    private void SetSelectedCompletenessCellId(
        string? cellId,
        bool rebuildItems = true)
    {
        if (string.Equals(
                selectedCompletenessCellId,
                cellId,
                StringComparison.OrdinalIgnoreCase))
        {
            setSelectedCompletenessCellId(cellId);
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

        setSelectedCompletenessCellId(cellId);
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
        previousCompletenessFailureCommand.RaiseCanExecuteChanged();
        nextCompletenessFailureCommand.RaiseCanExecuteChanged();
        selectCompletenessCellCommand.RaiseCanExecuteChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

internal sealed record ToolWorkbenchCompletenessReviewSnapshot(
    bool IsSelectedStepCompletenessGrid,
    bool HasCurrentMeasurementPreview,
    C3DCompletenessGridMetricOutput? CompletenessGrid,
    IReadOnlyList<ToolWorkbenchCompletenessTabSnapshot> ThicknessTabs);

internal sealed record ToolWorkbenchCompletenessTabSnapshot(
    string Id,
    string ToolId,
    string ToolName,
    string OutputEntityId);

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
