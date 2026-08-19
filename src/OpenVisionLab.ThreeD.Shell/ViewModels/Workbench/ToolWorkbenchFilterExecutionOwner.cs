using System;
using System.Threading.Tasks;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

internal sealed class ToolWorkbenchFilterExecutionOwner
{
    private readonly RelayCommand previewSelectedStepCommand;
    private readonly RelayCommand runTeachingRecipeCommand;
    private readonly RelayCommand publishSelectedStepCommand;
    private readonly RelayCommand cancelFilterPreviewCommand;
    private readonly RelayCommand showFilterSourceCommand;
    private readonly RelayCommand setFilterKernel3Command;
    private readonly RelayCommand setFilterKernel5Command;
    private readonly RelayCommand setFilterKernel7Command;

    private CancellationTokenSource? filterPreviewCancellation;
    private C3DHeightFieldSnapshot? filterPreviewOutput;
    private string? filterPreviewPath;
    private bool isFilterPreviewRunning;
    private bool isFilterPreviewStale;
    private bool isFilterPreviewPublished;
    private string filterExecutionSummary;
    private bool isOrderedRunRunning;
    private ToolRecipeOrderedGraphExecutionResult? orderedRunResult;
    private string? orderedRunRecordPath;
    private string orderedRunSummary;

    public ToolWorkbenchFilterExecutionOwner(
        Func<Task> previewSelectedStepAsync,
        Func<bool> canPreviewSelectedStep,
        Func<Task> runTeachingRecipeAsync,
        Func<bool> canRunTeachingRecipe,
        Action publishSelectedStep,
        Func<bool> canPublishSelectedStep,
        Action cancelSelectedPreview,
        Func<bool> canCancelSelectedPreview,
        Func<bool> canShowFilterSource,
        Action showFilterSource,
        Action setFilterKernel3,
        Func<bool> canSetFilterKernel3,
        Action setFilterKernel5,
        Func<bool> canSetFilterKernel5,
        Action setFilterKernel7,
        Func<bool> canSetFilterKernel7)
    {
        previewSelectedStepCommand = new RelayCommand(_ => _ = previewSelectedStepAsync(), _ => canPreviewSelectedStep());
        runTeachingRecipeCommand = new RelayCommand(_ => _ = runTeachingRecipeAsync(), _ => canRunTeachingRecipe());
        publishSelectedStepCommand = new RelayCommand(_ => publishSelectedStep(), _ => canPublishSelectedStep());
        cancelFilterPreviewCommand = new RelayCommand(_ => cancelSelectedPreview(), _ => canCancelSelectedPreview());
        showFilterSourceCommand = new RelayCommand(_ => showFilterSource(), _ => canShowFilterSource());
        setFilterKernel3Command = new RelayCommand(_ => setFilterKernel3(), _ => canSetFilterKernel3());
        setFilterKernel5Command = new RelayCommand(_ => setFilterKernel5(), _ => canSetFilterKernel5());
        setFilterKernel7Command = new RelayCommand(_ => setFilterKernel7(), _ => canSetFilterKernel7());

        filterExecutionSummary = "Select a Filter step, then Preview explicitly.";
        orderedRunSummary = "Run the saved current recipe explicitly to create a Run Record.";
    }

    public RelayCommand PreviewSelectedStepCommand => previewSelectedStepCommand;
    public RelayCommand RunTeachingRecipeCommand => runTeachingRecipeCommand;
    public RelayCommand PublishSelectedStepCommand => publishSelectedStepCommand;
    public RelayCommand CancelFilterPreviewCommand => cancelFilterPreviewCommand;
    public RelayCommand ShowFilterSourceCommand => showFilterSourceCommand;
    public RelayCommand SetFilterKernel3Command => setFilterKernel3Command;
    public RelayCommand SetFilterKernel5Command => setFilterKernel5Command;
    public RelayCommand SetFilterKernel7Command => setFilterKernel7Command;

    public CancellationTokenSource? FilterPreviewCancellation
    {
        get => filterPreviewCancellation;
        set => filterPreviewCancellation = value;
    }

    public C3DHeightFieldSnapshot? FilterPreviewOutput
    {
        get => filterPreviewOutput;
        set => filterPreviewOutput = value;
    }

    public string? FilterPreviewPath
    {
        get => filterPreviewPath;
        set => filterPreviewPath = value;
    }

    public bool IsFilterPreviewRunning
    {
        get => isFilterPreviewRunning;
        set => isFilterPreviewRunning = value;
    }

    public bool IsFilterPreviewStale
    {
        get => isFilterPreviewStale;
        set => isFilterPreviewStale = value;
    }

    public bool IsFilterPreviewPublished
    {
        get => isFilterPreviewPublished;
        set => isFilterPreviewPublished = value;
    }

    public string FilterExecutionSummary
    {
        get => filterExecutionSummary;
        set => filterExecutionSummary = value;
    }

    public bool IsOrderedRunRunning
    {
        get => isOrderedRunRunning;
        set => isOrderedRunRunning = value;
    }

    public ToolRecipeOrderedGraphExecutionResult? OrderedRunResult
    {
        get => orderedRunResult;
        set => orderedRunResult = value;
    }

    public string? OrderedRunRecordPath
    {
        get => orderedRunRecordPath;
        set => orderedRunRecordPath = value;
    }

    public string OrderedRunSummary
    {
        get => orderedRunSummary;
        set => orderedRunSummary = value;
    }
}
