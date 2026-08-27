using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the session-only presentation of a verified connected-region result.
/// Detection and metric arithmetic stay in the Tools adapter; this boundary
/// only validates lineage, exposes typed metrics, and manages selection.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    private C3DConnectedRegionEvaluation? connectedRegionEvaluation;
    private string? selectedConnectedRegionId;
    private RelayCommand selectConnectedRegionCommand = null!;
    private RelayCommand showConnectedRegionOutputCommand = null!;

    public ResettableObservableCollection<ToolWorkbenchConnectedRegionReviewItem> ConnectedRegionReviewItems { get; } = [];

    public C3DConnectedRegionEvaluation? CurrentConnectedRegionEvaluation => connectedRegionEvaluation;

    public ToolResult? CurrentConnectedRegionResult => connectedRegionEvaluation?.Result;

    public C3DConnectedRegionOutput? CurrentConnectedRegionOutput => connectedRegionEvaluation?.Output;

    public bool HasConnectedRegionOutput => CurrentConnectedRegionOutput is { } output
        && IsCurrentConnectedRegionOutput(output, out _);

    public string? SelectedConnectedRegionId => selectedConnectedRegionId;

    public string ConnectedRegionSummary => CurrentConnectedRegionOutput is not { } output
        || !HasConnectedRegionOutput
        ? Localization.ConnectedRegionNoOutput
        : string.Format(
            CultureInfo.InvariantCulture,
            Localization.ConnectedRegionSummaryFormat,
            output.RegionCount,
            output.ForegroundCellCount,
            output.Regions.Sum(region => region.Area));

    public string SelectedConnectedRegionSummary => CurrentConnectedRegionOutput is not { } output
        || !HasConnectedRegionOutput
        ? Localization.ConnectedRegionNoSelection
        : ConnectedRegionReviewItems.FirstOrDefault(item => item.IsSelected) is { } item
            ? string.Format(
                CultureInfo.InvariantCulture,
                Localization.ConnectedRegionSelectedSummaryFormat,
                item.RegionId,
                item.CellCount,
                item.AreaText,
                item.CenterText)
            : Localization.ConnectedRegionNoSelection;

    public ICommand SelectConnectedRegionCommand => selectConnectedRegionCommand;

    public ICommand ShowConnectedRegionOutputCommand => showConnectedRegionOutputCommand;

    private void InitializeConnectedRegionPresentation()
    {
        selectConnectedRegionCommand = new RelayCommand(
            parameter => SelectConnectedRegion(parameter as ToolWorkbenchConnectedRegionReviewItem),
            parameter => parameter is ToolWorkbenchConnectedRegionReviewItem && HasConnectedRegionOutput);
        showConnectedRegionOutputCommand = new RelayCommand(
            parameter => ShowConnectedRegionOutput(parameter as ToolWorkbenchConnectedRegionReviewItem),
            parameter => parameter is ToolWorkbenchConnectedRegionReviewItem && HasConnectedRegionOutput);
    }

    /// <summary>
    /// Accepts a completed G-11 evaluation for presentation. This method never
    /// calls the SDK and never changes the authored recipe or execution state.
    /// </summary>
    internal bool SetConnectedRegionPreview(
        C3DConnectedRegionEvaluation evaluation,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        if (evaluation.Result.Status is ResultStatus.Error or ResultStatus.NotRun
            || evaluation.Output is not { } output)
        {
            message = evaluation.Result.Message;
            return false;
        }

        if (!IsCurrentConnectedRegionOutput(output, out message))
        {
            return false;
        }

        connectedRegionEvaluation = evaluation;
        selectedConnectedRegionId = output.Regions.FirstOrDefault()?.RegionId;
        RebuildConnectedRegionReviewItems();
        OnPropertyChanged(nameof(CurrentConnectedRegionEvaluation));
        OnPropertyChanged(nameof(CurrentConnectedRegionResult));
        OnPropertyChanged(nameof(CurrentConnectedRegionOutput));
        OnPropertyChanged(nameof(HasConnectedRegionOutput));
        OnPropertyChanged(nameof(SelectedConnectedRegionId));
        OnPropertyChanged(nameof(ConnectedRegionSummary));
        OnPropertyChanged(nameof(SelectedConnectedRegionSummary));
        RebuildArtifactRegistryAndNavigator();
        AppendLog(
            "Results",
            $"Connected Region output accepted for display | output={output.OutputEntityId} | regions={output.RegionCount} | source={output.InputContentSha256} | recipeChanged=false | inspectionRun=false");
        message = evaluation.Result.Message;
        return true;
    }

    private void SelectConnectedRegion(ToolWorkbenchConnectedRegionReviewItem? item)
    {
        if (item is null
            || !HasConnectedRegionOutput
            || !ConnectedRegionReviewItems.Contains(item))
        {
            return;
        }

        selectedConnectedRegionId = item.RegionId;
        foreach (var reviewItem in ConnectedRegionReviewItems)
        {
            reviewItem.SetSelected(
                string.Equals(
                    reviewItem.RegionId,
                    selectedConnectedRegionId,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (CurrentConnectedRegionOutput is { } output)
        {
            WorkspaceSelection.SelectOutput(output.OutputEntityId);
        }

        OnPropertyChanged(nameof(SelectedConnectedRegionId));
        OnPropertyChanged(nameof(SelectedConnectedRegionSummary));
    }

    private void ShowConnectedRegionOutput(ToolWorkbenchConnectedRegionReviewItem? item)
    {
        if (item is not null)
        {
            SelectConnectedRegion(item);
        }

        if (!HasConnectedRegionOutput || CurrentConnectedRegionOutput is not { } output)
        {
            return;
        }

        var displayed = DisplayedOutputs.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, output.OutputEntityId, StringComparison.OrdinalIgnoreCase));
        RequestDisplayedOutputInViewer(displayed);
    }

    private void RebuildConnectedRegionReviewItems()
    {
        var items = CurrentConnectedRegionOutput?.Regions
            .Select(region => new ToolWorkbenchConnectedRegionReviewItem(
                region,
                string.Equals(
                    region.RegionId,
                    selectedConnectedRegionId,
                    StringComparison.OrdinalIgnoreCase)))
            .ToArray()
            ?? [];
        ConnectedRegionReviewItems.ReplaceAll(items);
    }

    private void ClearConnectedRegionPreviewCore(string reason)
    {
        if (connectedRegionEvaluation is null
            && ConnectedRegionReviewItems.Count == 0
            && selectedConnectedRegionId is null)
        {
            return;
        }

        connectedRegionEvaluation = null;
        selectedConnectedRegionId = null;
        ConnectedRegionReviewItems.ReplaceAll([]);
        OnPropertyChanged(nameof(CurrentConnectedRegionEvaluation));
        OnPropertyChanged(nameof(CurrentConnectedRegionResult));
        OnPropertyChanged(nameof(CurrentConnectedRegionOutput));
        OnPropertyChanged(nameof(HasConnectedRegionOutput));
        OnPropertyChanged(nameof(SelectedConnectedRegionId));
        OnPropertyChanged(nameof(ConnectedRegionSummary));
        OnPropertyChanged(nameof(SelectedConnectedRegionSummary));
        AppendLog("Results", $"Connected Region output cleared | reason={reason} | recipeChanged=false");
    }

    private bool IsCurrentConnectedRegionOutput(
        C3DConnectedRegionOutput output,
        out string message)
    {
        if (!IsSourceReadyForRecipe || SourceSession.SourceBinding is not { } binding)
        {
            message = "A current C3D source binding is required before connected-region output can be displayed.";
            return false;
        }

        if (!string.Equals(output.InputEntityId, Source.Id, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(output.InputContentSha256, binding.ContentSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(output.MaskSourceEntityId, Source.Id, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(output.MaskSourceContentSha256, binding.ContentSha256, StringComparison.OrdinalIgnoreCase)
            || output.GridWidth != binding.GridWidth
            || output.GridHeight != binding.GridHeight
            || !string.Equals(output.Unit, Source.Unit, StringComparison.Ordinal)
            || !string.Equals(output.FrameId, Source.FrameId, StringComparison.Ordinal))
        {
            message = "Connected-region output is not bound to the exact current C3D source identity, grid, unit, or frame.";
            return false;
        }

        if (output.RegionCount < 1
            || output.ForegroundCellCount < 1
            || output.VisitedCellCount != output.ForegroundCellCount
            || output.ContentSha256.Length != 64
            || output.MaskContentSha256.Length != 64)
        {
            message = "Connected-region output counts or content identities are invalid.";
            return false;
        }

        foreach (var region in output.Regions)
        {
            if (region.Cells.Count != region.CellCount
                || region.CellCount < 1
                || region.Cells.Select(cell => (cell.Row, cell.Column)).Distinct().Count() != region.Cells.Count
                || region.Cells.Any(cell => cell.Row < 0
                    || cell.Row >= output.GridHeight
                    || cell.Column < 0
                    || cell.Column >= output.GridWidth)
                || region.MinimumRow < 0
                || region.MinimumColumn < 0
                || region.MaximumRow >= output.GridHeight
                || region.MaximumColumn >= output.GridWidth
                || region.MinimumRow > region.MaximumRow
                || region.MinimumColumn > region.MaximumColumn
                || !double.IsFinite(region.Area)
                || region.Area <= 0
                || !double.IsFinite(region.CenterX)
                || !double.IsFinite(region.CenterY)
                || !double.IsFinite(region.MinimumX)
                || !double.IsFinite(region.MinimumY)
                || !double.IsFinite(region.MaximumX)
                || !double.IsFinite(region.MaximumY)
                || !double.IsFinite(region.Width)
                || !double.IsFinite(region.Height)
                || region.Width <= 0
                || region.Height <= 0
                || region.HasOrientation && !double.IsFinite(region.OrientationDegrees)
                || string.IsNullOrWhiteSpace(region.CoordinateConvention))
            {
                message = $"Connected-region geometry is invalid for {region.RegionId}.";
                return false;
            }
        }

        message = string.Empty;
        return true;
    }
}

public sealed class ToolWorkbenchConnectedRegionReviewItem
    : INotifyPropertyChanged
{
    private bool isSelected;

    public ToolWorkbenchConnectedRegionReviewItem(
        C3DConnectedRegionMetricOutput region,
        bool isSelected)
    {
        ArgumentNullException.ThrowIfNull(region);
        RegionId = region.RegionId;
        Index = region.Index;
        CellCount = region.CellCount;
        Area = region.Area;
        AreaUnit = "grid-index²";
        CenterX = region.CenterX;
        CenterY = region.CenterY;
        HasOrientation = region.HasOrientation;
        OrientationDegrees = region.OrientationDegrees;
        MinimumX = region.MinimumX;
        MinimumY = region.MinimumY;
        MaximumX = region.MaximumX;
        MaximumY = region.MaximumY;
        Width = region.Width;
        Height = region.Height;
        CoordinateConvention = region.CoordinateConvention;
        AreaText = region.Area.ToString("0.###", CultureInfo.InvariantCulture);
        CenterText = string.Create(
            CultureInfo.InvariantCulture,
            $"({region.CenterX:0.###}, {region.CenterY:0.###}) grid-index");
        OrientationText = region.HasOrientation
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{region.OrientationDegrees:0.###}°")
            : "—";
        BoundsText = string.Create(
            CultureInfo.InvariantCulture,
            $"[{region.MinimumX:0.###}, {region.MinimumY:0.###}] → [{region.MaximumX:0.###}, {region.MaximumY:0.###}] grid-index");
        this.isSelected = isSelected;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string RegionId { get; }
    public int Index { get; }
    public int CellCount { get; }
    public double Area { get; }
    public string AreaUnit { get; }
    public double CenterX { get; }
    public double CenterY { get; }
    public bool HasOrientation { get; }
    public double OrientationDegrees { get; }
    public double MinimumX { get; }
    public double MinimumY { get; }
    public double MaximumX { get; }
    public double MaximumY { get; }
    public double Width { get; }
    public double Height { get; }
    public string CoordinateConvention { get; }
    public string AreaText { get; }
    public string CenterText { get; }
    public string OrientationText { get; }
    public string BoundsText { get; }

    public bool IsSelected => isSelected;

    internal void SetSelected(bool value)
    {
        if (isSelected == value)
        {
            return;
        }

        isSelected = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
    }
}
