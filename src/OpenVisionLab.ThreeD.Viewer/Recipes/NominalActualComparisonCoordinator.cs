using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Owns the Nominal/Actual Preview and Publish workflow. The Viewer control
/// remains responsible for event wiring and rendering, while this coordinator
/// keeps comparison validation, cancellation, result projection, and failure
/// handling in one non-WPF boundary.
/// </summary>
internal sealed class NominalActualComparisonCoordinator
{
    private readonly MainWindowViewModel viewModel;
    private readonly NominalActualComparisonExecutor executor;
    private readonly CancellationToken viewerLifetimeToken;
    private readonly Func<bool> isDisposed;
    private readonly Action render;
    private readonly Action markSmokeFailure;
    private Task? previewTask;
    private Task? previewObservationTask;

    public NominalActualComparisonCoordinator(
        MainWindowViewModel viewModel,
        NominalActualComparisonExecutor executor,
        CancellationToken viewerLifetimeToken,
        Func<bool> isDisposed,
        Action render,
        Action markSmokeFailure)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        this.viewerLifetimeToken = viewerLifetimeToken;
        this.isDisposed = isDisposed ?? throw new ArgumentNullException(nameof(isDisposed));
        this.render = render ?? throw new ArgumentNullException(nameof(render));
        this.markSmokeFailure = markSmokeFailure ?? throw new ArgumentNullException(nameof(markSmokeFailure));
    }

    public void StartPreview(NominalActualPreviewRequestedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (isDisposed())
        {
            return;
        }

        var task = HandlePreviewAsync(args);
        Volatile.Write(ref previewTask, task);
        Volatile.Write(ref previewObservationTask, ObservePreviewAsync(task, args));
    }

    public void Dispose()
    {
        Volatile.Write(ref previewTask, null);
        Volatile.Write(ref previewObservationTask, null);
    }

    public async Task HandlePreviewAsync(NominalActualPreviewRequestedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (isDisposed())
        {
            return;
        }

        var comparison = viewModel.NominalActual;
        if (!viewModel.RecipeOutputEnabled)
        {
            comparison.FailPreview(args.RequestId, "Recipe output is disabled; Preview did not run.");
            viewModel.ViewerStatus = "Recipe output is disabled; Preview did not run";
            return;
        }

        if (viewModel.NominalActualInput is not { } configuredInput)
        {
            comparison.FailPreview(args.RequestId, "Comparison inputs are not connected.");
            return;
        }

        var executionInput = configuredInput with
        {
            LowerTolerance = comparison.LowerTolerance,
            UpperTolerance = comparison.UpperTolerance
        };
        if (!executionInput.ExecutionFingerprint.Equals(args.Fingerprint, StringComparison.Ordinal))
        {
            comparison.FailPreview(args.RequestId, "Comparison input fingerprint changed before execution.");
            return;
        }

        var progress = new Progress<NominalActualComparisonProgress>(value =>
        {
            if (isDisposed()
                || viewerLifetimeToken.IsCancellationRequested
                || args.CancellationToken.IsCancellationRequested)
            {
                return;
            }

            comparison.ReportPreviewProgress(
                args.RequestId,
                value.ProcessedPointCount,
                value.TotalPointCount,
                value.Elapsed,
                value.Stage);
        });
        CancellationTokenSource? viewerLifetimeLinkedCancellation = null;
        var operationToken = args.CancellationToken;

        try
        {
            viewerLifetimeLinkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                args.CancellationToken,
                viewerLifetimeToken);
            operationToken = viewerLifetimeLinkedCancellation.Token;
            var result = await executor.ExecuteAsync(
                executionInput,
                args.MaximumDisplaySamples,
                progress,
                operationToken);
            if (isDisposed() || operationToken.IsCancellationRequested)
            {
                return;
            }

            if (!comparison.CompletePreview(args.RequestId, result))
            {
                return;
            }

            viewModel.SelectedEntity = "Nominal / Actual Surface Deviation";
            viewModel.MeasurementSummary = result.Message;
            viewModel.ViewerStatus =
                $"Nominal/actual Preview complete: {result.Status}, {result.ComparedPointCount:N0} full-query points";
            render();
        }
        catch (OperationCanceledException) when (operationToken.IsCancellationRequested)
        {
            // The ViewModel already owns the cancelled/stale state transition.
        }
        catch (Exception exception)
        {
            HandlePreviewFailure(args, exception);
        }
        finally
        {
            viewerLifetimeLinkedCancellation?.Dispose();
        }
    }

    private async Task ObservePreviewAsync(
        Task task,
        NominalActualPreviewRequestedEventArgs args)
    {
        try
        {
            await task.ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (
            isDisposed()
            || viewerLifetimeToken.IsCancellationRequested
            || args.CancellationToken.IsCancellationRequested)
        {
            // The ViewModel or control lifetime owns Preview cancellation.
        }
        catch (Exception exception)
        {
            HandlePreviewFailure(args, exception);
        }
        finally
        {
            if (ReferenceEquals(Volatile.Read(ref previewTask), task))
            {
                Volatile.Write(ref previewTask, null);
            }
        }
    }

    public void HandlePreviewFailure(NominalActualPreviewRequestedEventArgs args, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(exception);
        if (isDisposed())
        {
            return;
        }

        var comparison = viewModel.NominalActual;
        if (comparison.FailPreview(args.RequestId, exception.Message))
        {
            viewModel.ViewerStatus = $"Nominal/actual Preview failed: {exception.Message}";
        }

        markSmokeFailure();
        render();
    }

    public void HandlePublish(NominalActualPublishRequestedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (isDisposed())
        {
            return;
        }

        var comparison = viewModel.NominalActual;
        if (!viewModel.RecipeOutputEnabled)
        {
            viewModel.ViewerStatus = "Recipe output is disabled; Publish did not run";
            return;
        }

        var result = comparison.PreviewResult;
        if (result is null
            || !result.Input.ExecutionFingerprint.Equals(args.Fingerprint, StringComparison.Ordinal)
            || !viewModel.PublishNominalActualComparison(result))
        {
            viewModel.ViewerStatus = "Nominal/actual Publish failed: current Preview evidence is unavailable";
            return;
        }

        comparison.ConfirmPublished(
            $"Published result entity {NominalActualComparisonContract.ResultEntityId} | fingerprint {args.Fingerprint}");
        render();
    }
}
