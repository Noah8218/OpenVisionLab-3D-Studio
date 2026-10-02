using System.IO;
using System.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.Logging;
using OpenVisionLab.ThreeD.Shell.Dialogs;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Coordination;

/// <summary>
/// Owns the Shell workflow around Viewer source loading. The Viewer decodes and
/// applies source data, while this owner admits user requests, reports progress
/// to the Workbench, binds a loaded C3D source, and suppresses stale callbacks.
/// Recipe-manager window and recipe persistence remain with the lifecycle owner.
/// </summary>
internal sealed class ShellWorkbenchSourceLoadCoordinator : IDisposable
{
    private readonly ShellSourceFileDialogService sourceFileDialogs;
    private readonly OpenVisionThreeDViewerControl viewer;
    private readonly ShellMainWindowViewModel viewModel;
    private readonly ShellWorkbenchSourceLoadCallbacks callbacks;
    private readonly ShellSourceLoadOperationCoordinator sourceLoadOperations = new();
    private readonly ShellWorkbenchRequestOwner sourceRequestOwner;
    private int disposalState;

    public ShellWorkbenchSourceLoadCoordinator(
        ShellSourceFileDialogService sourceFileDialogs,
        OpenVisionThreeDViewerControl viewer,
        ShellMainWindowViewModel viewModel,
        ShellWorkbenchSourceLoadCallbacks callbacks)
    {
        this.sourceFileDialogs = sourceFileDialogs ?? throw new ArgumentNullException(nameof(sourceFileDialogs));
        this.viewer = viewer ?? throw new ArgumentNullException(nameof(viewer));
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        this.callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
        sourceRequestOwner = new(ReportSourceRequestFailure);
    }

    internal bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    public double LastWorkbenchSourceBindingMilliseconds { get; private set; }

    public string? CurrentC3DSourcePath => viewer.CurrentC3DSourcePath;

    public string ViewerStatus => viewer.HostState.ViewerStatus;

    public bool LoadC3DSource(string path) => viewer.LoadC3DSource(path);

    public void ClearC3DTeachingSource(string message) => viewer.ClearC3DTeachingSource(message);

    public bool TryGetCurrentC3DSourceBinding(
        string path,
        out ToolRecipeSelectionSourceBinding binding) =>
        viewer.TryGetCurrentC3DSourceBinding(path, out binding);

    public async Task<bool> LoadWorkbenchC3DSourceAsync(
        string path,
        bool showFailureDialog = true,
        bool bindToWorkbench = true,
        CancellationToken cancellationToken = default)
    {
        if (IsDisposed || cancellationToken.IsCancellationRequested || viewModel.Workbench.IsRecipeMutationBlocked)
        {
            return false;
        }

        using var operation = sourceLoadOperations.Begin(cancellationToken);
        LastWorkbenchSourceBindingMilliseconds = 0.0;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        viewModel.Workbench.BeginC3DSourceLoad(path);
        var progress = new Progress<double>(viewModel.Workbench.ReportC3DSourceLoadProgress);

        try
        {
            if (await viewer.LoadC3DSourceAsync(path, operation.Token, progress)
                && operation.IsCurrent
                && !operation.IsCancellationRequested
                && viewer.CurrentC3DSourcePath is { } sourcePath)
            {
                if (bindToWorkbench)
                {
                    SetWorkbenchC3DSourceFromViewer(sourcePath);
                }

                viewer.TrySetHudDetailsVisible(false);
                viewModel.Workbench.CompleteC3DSourceLoad(sourcePath, stopwatch.ElapsedMilliseconds);
                return true;
            }

            if (!operation.IsCurrent || operation.IsCancellationRequested)
            {
                return false;
            }

            viewModel.Workbench.FailC3DSourceLoad(path, stopwatch.ElapsedMilliseconds);
            if (showFailureDialog)
            {
                callbacks.ShowLoadSourceFailure(viewer.HostState.ViewerStatus);
            }

            return false;
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            if (operation.IsCurrent)
            {
                viewModel.Workbench.CancelC3DSourceLoad(stopwatch.ElapsedMilliseconds);
            }

            return false;
        }
    }

    public void CancelC3DSourceLoad() => sourceLoadOperations.CancelCurrent();

    public void LoadC3DSourceRequested(object? sender, EventArgs args) =>
        sourceRequestOwner.TryStart(LoadC3DSourceRequestAsync);

    public void UseSelectedInspectionInputForTeachingRequested(object? sender, InspectionQueueSelectionChangedEventArgs args)
    {
        if (args.Sample is not { } sample || !viewModel.Workbench.InspectionQueue.CanUseSelectedInputForTeaching) return;
        var path = sample.SourcePath;
        var stepId = viewModel.Workbench.InspectionQueue.SelectedStepResult?.StepId
            ?? sample.Steps.FirstOrDefault(step => step.Status == nameof(ResultStatus.Fail))?.StepId;
        sourceRequestOwner.TryStart(async cancellationToken => { await UseInspectionInputForTeachingAsync(path, stepId, cancellationToken); });
    }

    internal async Task<bool> UseInspectionInputForTeachingAsync(string path, string? stepId, CancellationToken cancellationToken = default)
    {
        if (IsDisposed || viewModel.Workbench.HasPendingStepParameterChanges || viewModel.Workbench.IsRecipeMutationBlocked
            || viewModel.Workbench.IsC3DSourceLoading || cancellationToken.IsCancellationRequested) return false;
        if (!await LoadWorkbenchC3DSourceAsync(path, cancellationToken: cancellationToken)) return false;
        if (stepId is not null) viewModel.Workbench.SelectPipelineStep(stepId);
        if (viewModel.SelectWorkspaceCommand.CanExecute(ShellWorkspaceMode.Teach)) viewModel.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Teach);
        return true;
    }

    private async Task LoadC3DSourceRequestAsync(CancellationToken cancellationToken)
    {
        if (!sourceFileDialogs.TrySelectC3DSourcePath(out var path))
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (IsViewerSourceAlreadyLoaded(path))
        {
            SetWorkbenchC3DSourceFromViewer(Path.GetFullPath(path));
            viewer.TrySetHudDetailsVisible(false);
            return;
        }

        await LoadWorkbenchC3DSourceAsync(path, cancellationToken: cancellationToken);
    }

    public void Import3DDataRequested(object? sender, EventArgs args) =>
        sourceRequestOwner.TryStart(Import3DDataRequestAsync);

    private async Task Import3DDataRequestAsync(CancellationToken cancellationToken)
    {
        OVLog.Write(LogCategory.UI, LogLevel.Info, "Workbench[Import] Opening verified 3D data dialog.");
        if (!sourceFileDialogs.TrySelect3DDataPath(out var path))
        {
            OVLog.Write(LogCategory.UI, LogLevel.Info, "Workbench[Import] Dialog closed without a file selection.");
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var extension = Path.GetExtension(path);
        if (string.Equals(extension, ".c3d", StringComparison.OrdinalIgnoreCase))
        {
            if (IsViewerSourceAlreadyLoaded(path))
            {
                SetWorkbenchC3DSourceFromViewer(Path.GetFullPath(path));
                viewer.TrySetHudDetailsVisible(false);
                return;
            }

            await LoadWorkbenchC3DSourceAsync(path, cancellationToken: cancellationToken);
            return;
        }

        await LoadViewerOnlySourceAsync(path, cancellationToken: cancellationToken);
    }

    public async Task<bool> LoadViewerOnlySourceAsync(
        string path,
        bool showFailureDialog = true,
        CancellationToken cancellationToken = default)
    {
        if (IsDisposed || cancellationToken.IsCancellationRequested || viewModel.Workbench.IsRecipeMutationBlocked)
        {
            return false;
        }

        var extension = Path.GetExtension(path);
        var format = extension.TrimStart('.').ToUpperInvariant();
        if (format is not ("GLB" or "STL" or "LAS" or "LAZ"))
        {
            throw new NotSupportedException(FormatUnsupportedViewerOnlyInput(format));
        }

        using var operation = sourceLoadOperations.Begin(cancellationToken);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        viewModel.Workbench.Begin3DDataImport(path, format);
        var progress = new Progress<double>(viewModel.Workbench.ReportC3DSourceLoadProgress);

        try
        {
            if (await viewer.LoadViewerOnlySourceAsync(path, operation.Token, progress)
                && operation.IsCurrent
                && !operation.IsCancellationRequested)
            {
                viewer.TrySetHudDetailsVisible(false);
                viewModel.Workbench.CompleteViewerOnlyImport(path, format, stopwatch.ElapsedMilliseconds);
                return true;
            }

            if (!operation.IsCurrent || operation.IsCancellationRequested)
            {
                return false;
            }

            viewModel.Workbench.FailC3DSourceLoad(path, stopwatch.ElapsedMilliseconds);
            if (showFailureDialog)
            {
                callbacks.ShowLoadSourceFailure(viewer.HostState.ViewerStatus);
            }

            return false;
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            if (operation.IsCurrent)
            {
                viewModel.Workbench.CancelC3DSourceLoad(stopwatch.ElapsedMilliseconds);
            }

            return false;
        }
    }

    public bool IsViewerSourceAlreadyLoaded(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (viewer.CurrentC3DSourcePath is not { } currentPath)
        {
            return false;
        }

        if (!string.Equals(
            Path.GetFullPath(currentPath),
            Path.GetFullPath(path),
            StringComparison.OrdinalIgnoreCase)
            || !viewer.TryGetCurrentC3DSourceBinding(path, out var currentBinding))
        {
            return false;
        }

        var verification = ToolRecipeSelectionSourceBindingVerifier.Verify(path, currentBinding);
        if (!verification.IsCurrent)
        {
            OVLog.Write(
                LogCategory.UI,
                LogLevel.Info,
                $"Workbench[source reload] same path has changed; reloading '{path}': {verification.Message}");
        }

        return verification.IsCurrent;
    }

    public void SetWorkbenchC3DSourceFromViewer(string path, bool markDirty = true)
    {
        if (viewModel.Workbench.IsRecipeMutationBlocked)
        {
            throw new InvalidOperationException("The C3D source cannot change while a recipe or tool operation is running.");
        }

        var sourceBindingStart = System.Diagnostics.Stopwatch.GetTimestamp();
        if (!viewer.TryGetCurrentC3DSourceBinding(path, out var sourceBinding))
        {
            throw new InvalidOperationException(
                "The Viewer source identity is unavailable or does not match the requested C3D path.");
        }

        viewModel.Workbench.SetC3DSourceFromLoadedViewer(path, sourceBinding, markDirty);
        if (markDirty)
        {
            viewModel.ClearCurrentRunEvidenceForRecipeContext();
        }

        LastWorkbenchSourceBindingMilliseconds =
            System.Diagnostics.Stopwatch.GetElapsedTime(sourceBindingStart).TotalMilliseconds;
    }

    public void SyncWorkbenchSourceFromViewer()
    {
        if (viewer.CurrentC3DSourcePath is { } sourcePath
            && string.IsNullOrWhiteSpace(viewModel.Workbench.Source.Path))
        {
            SetWorkbenchC3DSourceFromViewer(sourcePath, markDirty: false);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        sourceLoadOperations.Dispose();
        sourceRequestOwner.Dispose();
        GC.SuppressFinalize(this);
    }

    private void ReportSourceRequestFailure(Exception exception)
    {
        OVLog.Write(
            LogCategory.UI,
            LogLevel.Error,
            $"Workbench[source request] request failed: {exception}");
        if (IsDisposed)
        {
            return;
        }

        try
        {
            // ViewerStatus is mutable presentation state and may still describe a prior request.
            callbacks.ShowLoadSourceFailure(FormatSourceRequestFailure(exception));
        }
        catch (Exception dialogException)
        {
            OVLog.Write(
                LogCategory.UI,
                LogLevel.Error,
                $"Workbench[source request] failure dialog failed: {dialogException}");
        }
    }

    internal static string FormatSourceRequestFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return string.IsNullOrWhiteSpace(exception.Message)
            ? exception.GetType().Name
            : exception.Message;
    }

    internal static string FormatUnsupportedViewerOnlyInput(string format)
    {
        var koreanSubject = string.IsNullOrWhiteSpace(format) ? "이 파일은" : $"{format} 파일은";
        var englishSubject = string.IsNullOrWhiteSpace(format) ? "This file" : $"The {format} file";
        return ThreeDLocalization.Shared.Resolve(
            "ThreeD.Dialog.LoadSource.UnsupportedFormat",
            $"{koreanSubject} 3D 입력 형식으로 지원하지 않습니다. C3D, GLB, STL, LAS, LAZ 파일을 선택하세요.",
            $"{englishSubject} is not supported as a 3D input. Choose C3D, GLB, STL, LAS, or LAZ.");
    }
}

internal sealed record ShellWorkbenchSourceLoadCallbacks
{
    public required Action<string> ShowLoadSourceFailure { get; init; }
}
