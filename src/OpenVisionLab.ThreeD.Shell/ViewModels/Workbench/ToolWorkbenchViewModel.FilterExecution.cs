using System.IO;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    private RelayCommand previewSelectedStepCommand = null!;
    private RelayCommand runTeachingRecipeCommand = null!;
    private RelayCommand publishSelectedStepCommand = null!;
    private RelayCommand cancelFilterPreviewCommand = null!;
    private RelayCommand showFilterSourceCommand = null!;
    private RelayCommand setFilterKernel3Command = null!;
    private RelayCommand setFilterKernel5Command = null!;
    private RelayCommand setFilterKernel7Command = null!;
    private CancellationTokenSource? filterPreviewCancellation;
    private C3DHeightFieldSnapshot? filterPreviewOutput;
    private string? filterPreviewPath;
    private bool isFilterPreviewRunning;
    private bool isFilterPreviewStale;
    private bool isFilterPreviewPublished;
    private string filterExecutionSummary = "Select a Filter step, then Preview explicitly.";

    public event EventHandler<ToolWorkbenchFilterDisplayRequestEventArgs>? FilterDisplayRequested;

    public ICommand PreviewSelectedStepCommand { get; private set; } = null!;
    public ICommand RunTeachingRecipeCommand { get; private set; } = null!;
    public ICommand PublishSelectedStepCommand { get; private set; } = null!;
    public ICommand CancelFilterPreviewCommand { get; private set; } = null!;
    public ICommand CancelSelectedPreviewCommand { get; private set; } = null!;
    public ICommand ShowFilterSourceCommand { get; private set; } = null!;
    public ICommand SetFilterKernel3Command { get; private set; } = null!;
    public ICommand SetFilterKernel5Command { get; private set; } = null!;
    public ICommand SetFilterKernel7Command { get; private set; } = null!;

    public bool IsSelectedStepFilter =>
        string.Equals(SelectedPipelineStep?.ToolId, "filter", StringComparison.Ordinal);

    public bool IsFilterPreviewRunning => isFilterPreviewRunning;
    public bool IsSelectedStepPreviewRunning =>
        IsSelectedStepFilter ? isFilterPreviewRunning
            : IsSelectedStepHeightDifferenceEdge ? IsEdgePreviewRunning
            : IsSelectedStepLineFit ? IsLineFitPreviewRunning
            : IsSelectedStepLineIntersection ? IsLineIntersectionPreviewRunning
            : IsSelectedStepLandmarkCorrespondence && IsLandmarkCorrespondencePreviewRunning;
    public bool HasCurrentFilterPreview => filterPreviewOutput is not null && !isFilterPreviewStale;
    public bool IsFilterPreviewStale => isFilterPreviewStale;
    public bool IsFilterPreviewPublished => isFilterPreviewPublished;
    public string? CurrentFilterPreviewPath => filterPreviewPath;
    public string CurrentFilterPreviewOutputSummary => filterPreviewOutput is null
        ? "No Filter Preview output."
        : $"Filtered Surface | SHA-256 {filterPreviewOutput.ContentSha256[..12]} | {(isFilterPreviewPublished ? "Published" : "Preview only")}";
    public string FilterExecutionSummary => filterExecutionSummary;
    public string FilterKernelSummary
    {
        get
        {
            var size = GetFilterParameter("KernelSize") ?? "-";
            return $"{size} x {size}";
        }
    }
    public string FilterOutputHashSummary => filterPreviewOutput is null
        ? "No output hash until Preview completes."
        : $"Output SHA-256 {filterPreviewOutput.ContentSha256}";

    private void InitializeFilterExecution()
    {
        previewSelectedStepCommand = new RelayCommand(_ => _ = PreviewSelectedStepAsync(), _ => CanPreviewSelectedStep());
        runTeachingRecipeCommand = new RelayCommand(_ => _ = RunTeachingRecipeAsync(), _ => CanRunTeachingRecipe());
        publishSelectedStepCommand = new RelayCommand(_ => PublishSelectedStep(), _ => CanPublishSelectedStep());
        cancelFilterPreviewCommand = new RelayCommand(_ => CancelSelectedPreview(), _ => IsSelectedStepPreviewRunning);
        showFilterSourceCommand = new RelayCommand(_ => ShowFilterSource(), _ => filterPreviewOutput is not null && File.Exists(Source.Path));
        setFilterKernel3Command = new RelayCommand(_ => SetFilterKernel(3), _ => IsSelectedStepFilter && !isFilterPreviewRunning);
        setFilterKernel5Command = new RelayCommand(_ => SetFilterKernel(5), _ => IsSelectedStepFilter && !isFilterPreviewRunning);
        setFilterKernel7Command = new RelayCommand(_ => SetFilterKernel(7), _ => IsSelectedStepFilter && !isFilterPreviewRunning);
        PreviewSelectedStepCommand = previewSelectedStepCommand;
        RunTeachingRecipeCommand = runTeachingRecipeCommand;
        PublishSelectedStepCommand = publishSelectedStepCommand;
        CancelFilterPreviewCommand = cancelFilterPreviewCommand;
        CancelSelectedPreviewCommand = cancelFilterPreviewCommand;
        ShowFilterSourceCommand = showFilterSourceCommand;
        SetFilterKernel3Command = setFilterKernel3Command;
        SetFilterKernel5Command = setFilterKernel5Command;
        SetFilterKernel7Command = setFilterKernel7Command;
    }

    private Task<bool> PreviewSelectedStepAsync() => IsSelectedStepLandmarkCorrespondence
        ? PreviewSelectedLandmarkCorrespondenceAsync()
        : IsSelectedStepLineIntersection
        ? PreviewSelectedLineIntersectionAsync()
        : IsSelectedStepLineFit ? PreviewSelectedLineFitAsync()
        : IsSelectedStepHeightDifferenceEdge ? PreviewSelectedHeightDifferenceEdgeAsync() : PreviewSelectedFilterAsync();

    private bool CanPreviewSelectedStep() => IsSelectedStepLandmarkCorrespondence
        ? CanPreviewSelectedLandmarkCorrespondence()
        : IsSelectedStepLineIntersection
        ? CanPreviewSelectedLineIntersection()
        : IsSelectedStepLineFit ? CanPreviewSelectedLineFit()
        : IsSelectedStepHeightDifferenceEdge ? CanPreviewSelectedHeightDifferenceEdge() : CanPreviewSelectedFilter();

    private void PublishSelectedStep()
    {
        if (IsSelectedStepLandmarkCorrespondence)
        {
            PublishSelectedLandmarkCorrespondence();
        }
        else if (IsSelectedStepLineIntersection)
        {
            PublishSelectedLineIntersection();
        }
        else if (IsSelectedStepLineFit)
        {
            PublishSelectedLineFit();
        }
        else if (IsSelectedStepHeightDifferenceEdge)
        {
            PublishSelectedHeightDifferenceEdge();
        }
        else
        {
            PublishSelectedFilter();
        }
    }

    private bool CanPublishSelectedStep() => IsSelectedStepLandmarkCorrespondence
        ? HasCurrentLandmarkCorrespondencePreview && !IsLandmarkCorrespondencePreviewPublished
        : IsSelectedStepLineIntersection
        ? HasCurrentLineIntersectionPreview && !IsLineIntersectionPreviewPublished
        : IsSelectedStepLineFit
        ? HasCurrentLineFitPreview && !IsLineFitPreviewPublished
        : IsSelectedStepHeightDifferenceEdge ? HasCurrentEdgePreview && !IsEdgePreviewPublished : IsSelectedStepFilter && HasCurrentFilterPreview && !isFilterPreviewPublished;

    private void CancelSelectedPreview()
    {
        if (IsSelectedStepLandmarkCorrespondence)
        {
            CancelLandmarkCorrespondencePreview();
        }
        else if (IsSelectedStepLineIntersection)
        {
            CancelLineIntersectionPreview();
        }
        else if (IsSelectedStepLineFit)
        {
            CancelLineFitPreview();
        }
        else if (IsSelectedStepHeightDifferenceEdge)
        {
            CancelHeightDifferenceEdgePreview();
        }
        else
        {
            filterPreviewCancellation?.Cancel();
        }
    }

    public async Task<bool> PreviewSelectedFilterAsync()
    {
        if (!CanPreviewSelectedFilter() || SelectedPipelineStep is not { } step)
        {
            return false;
        }

        filterPreviewCancellation?.Dispose();
        filterPreviewCancellation = new CancellationTokenSource();
        SetFilterRunning(true);
        isFilterPreviewStale = false;
        isFilterPreviewPublished = false;
        step.State = "Preview running";
        SetFilterSummary("Median Preview is running from the verified C3D source bytes.");
        AppendLog("Preview", $"Filter Preview started: {step.Id}.");

        try
        {
            var document = CreateDocument();
            var recipeDirectory = RecipePath is null
                ? Environment.CurrentDirectory
                : Path.GetDirectoryName(Path.GetFullPath(RecipePath));
            var evaluation = await Task.Run(
                () => ToolRecipeFilterExecution.Execute(document, step.Id, recipeDirectory, filterPreviewCancellation.Token),
                filterPreviewCancellation.Token);
            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                filterPreviewOutput = null;
                filterPreviewPath = null;
                step.State = "Error";
                SetFilterSummary(evaluation.Result.Message);
                AppendLog("Error", $"Filter Preview failed: {evaluation.Result.Message}");
                return false;
            }

            filterPreviewOutput = evaluation.Output;
            filterPreviewPath = CreateFilterPreviewPath(evaluation.Output.ContentSha256);
            evaluation.Output.SaveC3D(filterPreviewPath);
            OnPropertyChanged(nameof(CurrentFilterPreviewPath));
            OnPropertyChanged(nameof(CurrentFilterPreviewOutputSummary));
            step.State = "Preview ready";
            SetFilterSummary($"Preview ready | valid {evaluation.Output.ValidCount:N0} | missing {evaluation.Output.MissingCount:N0} | preprocessing only, no OK/NG");
            AppendLog("Preview", $"Filter Preview ready: {evaluation.Output.ContentSha256}.");
            FilterDisplayRequested?.Invoke(
                this,
                new ToolWorkbenchFilterDisplayRequestEventArgs(
                    filterPreviewPath,
                    evaluation.Output.ContentSha256,
                    false));
            return true;
        }
        catch (OperationCanceledException)
        {
            step.State = "Ready";
            SetFilterSummary("Preview canceled. Source and authored recipe were not changed.");
            AppendLog("Preview", "Filter Preview canceled.");
            return false;
        }
        finally
        {
            SetFilterRunning(false);
        }
    }

    private async Task RunTeachingRecipeAsync()
    {
        var document = CreateDocument();
        if (!ToolRecipeFilterExecution.CanRunWholeRecipe(document, out var message))
        {
            SetFilterSummary(message);
            AppendLog("Run", message);
            return;
        }

        await PreviewSelectedFilterAsync();
    }

    private void PublishSelectedFilter()
    {
        if (SelectedPipelineStep is not { } step || !HasCurrentFilterPreview)
        {
            return;
        }

        isFilterPreviewPublished = true;
        step.State = "Published";
        OnPropertyChanged(nameof(CurrentFilterPreviewOutputSummary));
        SetFilterSummary($"Published output {step.OutputEntityId} | SHA-256 {filterPreviewOutput!.ContentSha256} | preprocessing only, no OK/NG");
        AppendLog("Publish", $"Filter output published without re-running: {step.OutputEntityId}.");
        OnPropertyChanged(nameof(IsFilterPreviewPublished));
        RefreshHeightDifferenceEdgeExecutionState();
        RefreshFilterCommands();
    }

    private void ShowFilterSource()
    {
        if (!File.Exists(Source.Path))
        {
            return;
        }

        FilterDisplayRequested?.Invoke(
            this,
            new ToolWorkbenchFilterDisplayRequestEventArgs(
                Source.Path,
                loadedSourceBinding?.ContentSha256 ?? string.Empty,
                true));
        SetFilterSummary("Showing the original taught C3D source. Preview output remains available.");
    }

    private bool CanPreviewSelectedFilter() =>
        IsSelectedStepFilter
        && IsSourceReadyForRecipe
        && !HasPendingStepParameterChanges
        && !isFilterPreviewRunning
        && !IsEdgePreviewRunning
        && ToolRecipeValidator.Validate(CreateDocument()).IsValid;

    private bool CanRunTeachingRecipe() =>
        IsSourceReadyForRecipe
        && !HasPendingStepParameterChanges
        && !isFilterPreviewRunning
        && !IsEdgePreviewRunning
        && ToolRecipeFilterExecution.CanRunWholeRecipe(CreateDocument(), out _);

    private void SetFilterKernel(int kernelSize)
    {
        var parameter = SelectedPipelineStep?.Parameters.SingleOrDefault(item => item.Name == "KernelSize");
        if (parameter is not null)
        {
            parameter.Value = kernelSize.ToString(System.Globalization.CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(FilterKernelSummary));
        }
    }

    private string? GetFilterParameter(string name) =>
        IsSelectedStepFilter
            ? SelectedPipelineStep!.Parameters.SingleOrDefault(parameter => parameter.Name == name)?.Value
            : null;

    private void MarkFilterPreviewStaleIfNeeded(object? sender)
    {
        if (filterPreviewOutput is null || isFilterPreviewRunning)
        {
            return;
        }

        var selected = SelectedPipelineStep;
        var selectedIsFilter = string.Equals(selected?.ToolId, "filter", StringComparison.Ordinal);
        var isSelectedFilterParameter = selectedIsFilter
            && sender is ToolWorkbenchParameterItem parameter
            && (selected?.Parameters.Contains(parameter) ?? false);
        if (ReferenceEquals(sender, Source)
            || selectedIsFilter && ReferenceEquals(sender, selected)
            || isSelectedFilterParameter)
        {
            isFilterPreviewStale = true;
            isFilterPreviewPublished = false;
            MarkHeightDifferenceEdgePreviewStale("Published Filter input changed. Preview Edge again after Filter is republished.");
            if (selected is not null)
            {
                selected.State = "Preview stale";
            }
            SetFilterSummary("Source, routing, output, or Kernel changed. Preview again before Publish.");
        }
    }

    private void ClearFilterPreview(string summary)
    {
        filterPreviewCancellation?.Cancel();
        filterPreviewOutput = null;
        filterPreviewPath = null;
        OnPropertyChanged(nameof(CurrentFilterPreviewPath));
        OnPropertyChanged(nameof(CurrentFilterPreviewOutputSummary));
        isFilterPreviewStale = false;
        isFilterPreviewPublished = false;
        ClearHeightDifferenceEdgePreview("Published Filter output is unavailable; Edge Preview is required after Filter is republished.");
        SetFilterSummary(summary);
    }

    private void RefreshFilterExecutionState()
    {
        OnPropertyChanged(nameof(IsSelectedStepFilter));
        OnPropertyChanged(nameof(FilterKernelSummary));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        if (SelectedPipelineStep is { } step
            && string.Equals(step.ToolId, "filter", StringComparison.Ordinal)
            && filterPreviewOutput is null
            && !isFilterPreviewRunning
            && step.State == "Taught / pending")
        {
            step.State = ToolRecipeValidator.Validate(CreateDocument()).IsValid
                ? "Ready"
                : "Taught / needs correction";
        }
        RefreshFilterCommands();
        RefreshHeightDifferenceEdgeCommands();
    }

    private void RefreshFilterCommands()
    {
        if (previewSelectedStepCommand is null)
        {
            return;
        }
        previewSelectedStepCommand.RaiseCanExecuteChanged();
        runTeachingRecipeCommand.RaiseCanExecuteChanged();
        publishSelectedStepCommand.RaiseCanExecuteChanged();
        cancelFilterPreviewCommand.RaiseCanExecuteChanged();
        showFilterSourceCommand.RaiseCanExecuteChanged();
        setFilterKernel3Command.RaiseCanExecuteChanged();
        setFilterKernel5Command.RaiseCanExecuteChanged();
        setFilterKernel7Command.RaiseCanExecuteChanged();
    }

    private void SetFilterRunning(bool value)
    {
        isFilterPreviewRunning = value;
        OnPropertyChanged(nameof(IsFilterPreviewRunning));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        RefreshFilterCommands();
        RefreshHeightDifferenceEdgeCommands();
    }

    private void SetFilterSummary(string value)
    {
        filterExecutionSummary = value;
        RebuildEntities();
        OnPropertyChanged(nameof(FilterExecutionSummary));
        OnPropertyChanged(nameof(FilterOutputHashSummary));
        OnPropertyChanged(nameof(HasCurrentFilterPreview));
        OnPropertyChanged(nameof(IsFilterPreviewStale));
        OnPropertyChanged(nameof(IsFilterPreviewPublished));
        RefreshFilterCommands();
        RefreshHeightDifferenceEdgeExecutionState();
    }

    private static string CreateFilterPreviewPath(string hash)
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenVisionLab",
            "3DStudio",
            "Preview");
        return Path.Combine(directory, $"filter-{hash}.c3d");
    }
}

public sealed class ToolWorkbenchFilterDisplayRequestEventArgs : EventArgs
{
    public ToolWorkbenchFilterDisplayRequestEventArgs(string c3DPath, string contentSha256, bool isSource)
    {
        C3DPath = c3DPath;
        ContentSha256 = contentSha256;
        IsSource = isSource;
    }

    public string C3DPath { get; }
    public string ContentSha256 { get; }
    public bool IsSource { get; }
}
