using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Viewer.Recipes;
using OpenVisionLab.ThreeD.Viewer.Localization;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.Rendering;
using OpenVisionLab.ThreeD.Viewer.ViewModels;
using OpenVisionLab.ThreeD.Tools;
using SharpGL;
using SharpGL.WPF;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    private DispatcherTimer? visibleFrameRetryTimer;
    private int visibleFrameRetryGeneration;
    private int visibleFrameRetryAttempt;
    private int visibleFrameRequestGeneration;
    private readonly object visibleFrameRequestOperationGate = new();
    private DispatcherOperation? visibleFrameRequestOperation;
    private int sourceLoadUnloadGeneration;
    private int sourceUnloadCancellationGeneration;
    private DispatcherOperation? sourceUnloadCancellationOperation;
    private int languageRefreshGeneration;
    private DispatcherOperation? languageRefreshOperation;
    private readonly SharpGlRenderContextLifetime renderContextLifetime = new();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        CancelLanguageRefresh();
        CancelSourceUnloadCancellation();
        sourceLoadUnloadGeneration++;
        SubscribeViewModelEvents();
        UpdateOrientationTriad();
        RequestVisibleFrame();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        CancelLanguageRefresh();
        var unloadGeneration = ++sourceLoadUnloadGeneration;
        QueueSourceUnloadCancellation(unloadGeneration);

        visibleFrameRequestGeneration++;
        CancelVisibleFrameRequest();
        StopVisibleFrameRetryTimer();
        StopInteractionWireframeLod();
        UnsubscribeViewModelEvents();
    }

    private void SubscribeViewModelEvents()
    {
        if (viewModelEventsSubscribed)
        {
            return;
        }

        viewModel.FitAllRequested += fitAllRequestedHandler;
        viewModel.FitSelectionRequested += fitSelectionRequestedHandler;
        viewModel.FitRoiRequested += fitRoiRequestedHandler;
        viewModel.TopViewRequested += topViewRequestedHandler;
        viewModel.PerspectiveViewRequested += perspectiveViewRequestedHandler;
        viewModel.ResetRequested += resetRequestedHandler;
        viewModel.OpenRecipeRequested += openRecipeRequestedHandler;
        viewModel.SaveRecipeRequested += saveRecipeRequestedHandler;
        viewModel.ApplyRoiAlignmentRequested += applyRoiAlignmentRequestedHandler;
        viewModel.FitPlaneRequested += fitPlaneRequestedHandler;
        viewModel.PreviewThicknessRequested += previewThicknessRequestedHandler;
        viewModel.PreviewWarpageRequested += previewWarpageRequestedHandler;
        viewModel.PreviewPlaneFlatnessRequested += previewPlaneFlatnessRequestedHandler;
        viewModel.PreviewPointPairDimensionsRequested += previewPointPairDimensionsRequestedHandler;
        viewModel.PreviewGapFlushRequested += previewGapFlushRequestedHandler;
        viewModel.PreviewVolumeRequested += previewVolumeRequestedHandler;
        viewModel.PreviewCrossSectionRequested += previewCrossSectionRequestedHandler;
        viewModel.ScreenshotRequested += screenshotRequestedHandler;
        viewModel.ProfileViewRequested += profileViewRequestedHandler;
        viewModel.PublishPreviewResultRequested += publishPreviewResultRequestedHandler;
        viewModel.NominalActual.PreviewRequested += nominalActualPreviewRequestedHandler;
        viewModel.NominalActual.PublishRequested += nominalActualPublishRequestedHandler;
        viewModel.NominalActual.PropertyChanged += nominalActualPropertyChangedHandler;
        viewModel.PropertyChanged += viewModelPropertyChangedHandler;
        viewModel.CameraChanged += OnViewModelCameraChanged;
        OpenVisionLanguageService.LanguageChanged += languageChangedHandler;
        viewModelEventsSubscribed = true;
    }

    private void UnsubscribeViewModelEvents()
    {
        viewModel.FitAllRequested -= fitAllRequestedHandler;
        viewModel.FitSelectionRequested -= fitSelectionRequestedHandler;
        viewModel.FitRoiRequested -= fitRoiRequestedHandler;
        viewModel.TopViewRequested -= topViewRequestedHandler;
        viewModel.PerspectiveViewRequested -= perspectiveViewRequestedHandler;
        viewModel.ResetRequested -= resetRequestedHandler;
        viewModel.OpenRecipeRequested -= openRecipeRequestedHandler;
        viewModel.SaveRecipeRequested -= saveRecipeRequestedHandler;
        viewModel.ApplyRoiAlignmentRequested -= applyRoiAlignmentRequestedHandler;
        viewModel.FitPlaneRequested -= fitPlaneRequestedHandler;
        viewModel.PreviewThicknessRequested -= previewThicknessRequestedHandler;
        viewModel.PreviewWarpageRequested -= previewWarpageRequestedHandler;
        viewModel.PreviewPlaneFlatnessRequested -= previewPlaneFlatnessRequestedHandler;
        viewModel.PreviewPointPairDimensionsRequested -= previewPointPairDimensionsRequestedHandler;
        viewModel.PreviewGapFlushRequested -= previewGapFlushRequestedHandler;
        viewModel.PreviewVolumeRequested -= previewVolumeRequestedHandler;
        viewModel.PreviewCrossSectionRequested -= previewCrossSectionRequestedHandler;
        viewModel.ScreenshotRequested -= screenshotRequestedHandler;
        viewModel.ProfileViewRequested -= profileViewRequestedHandler;
        viewModel.PublishPreviewResultRequested -= publishPreviewResultRequestedHandler;
        viewModel.NominalActual.PreviewRequested -= nominalActualPreviewRequestedHandler;
        viewModel.NominalActual.PublishRequested -= nominalActualPublishRequestedHandler;
        viewModel.NominalActual.PropertyChanged -= nominalActualPropertyChangedHandler;
        viewModel.PropertyChanged -= viewModelPropertyChangedHandler;
        viewModel.CameraChanged -= OnViewModelCameraChanged;
        OpenVisionLanguageService.LanguageChanged -= languageChangedHandler;
        viewModelEventsSubscribed = false;
    }

    private void OnViewerLanguageChanged(object? sender, EventArgs args)
    {
        if (IsDisposed)
        {
            return;
        }

        if (!Dispatcher.CheckAccess())
        {
            QueueLanguageRefresh();
            return;
        }

        if (IsDisposed)
        {
            return;
        }

        viewModel.RefreshLocalizedPresentation();
    }

    /// <summary>
    /// Stops all Viewer-owned work and detaches the control from its event and
    /// rendering lifetime. The host contract intentionally remains unchanged;
    /// direct consumers may opt into this concrete-control boundary.
    /// </summary>
    public void Dispose()
    {
        if (!Dispatcher.CheckAccess())
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            {
                DisposeCore();
                return;
            }

            try
            {
                Dispatcher.Invoke(DisposeCore);
            }
            catch (InvalidOperationException)
            {
                DisposeCore();
            }

            return;
        }

        DisposeCore();
    }

    private void DisposeCore()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        viewerLifetimeCancellation.Cancel();
        CancelLanguageRefresh();
        CancelSourceUnloadCancellation();
        visibleFrameRequestGeneration++;
        CancelVisibleFrameRequest();
        sourceLoadUnloadGeneration++;
        StopVisibleFrameRetryTimer();
        DisposeInteractionWireframeLod();

        try
        {
            Viewport.ReleaseMouseCapture();
        }
        catch (InvalidOperationException)
        {
            // The Dispatcher may already be shutting down; context teardown
            // remains the owner of any resources unavailable to this thread.
        }

        sourceLoadOperations.Dispose();
        lazPointCloudLoadCoordinator.Dispose();
        UnsubscribeViewModelEvents();
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        Loaded -= SmokeCaptureOnLoaded;

        TryRetireOpenGLResourcesForDispose();
        renderContextLifetime.Dispose(Viewport);
        ClearManagedDataReferencesAfterDispose();
        viewerLifetimeCancellation.Dispose();
        GC.SuppressFinalize(this);
    }

    private void QueueLanguageRefresh()
    {
        if (IsDisposed || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (languageRefreshOperation?.Status == DispatcherOperationStatus.Pending)
        {
            return;
        }

        var refreshGeneration = ++languageRefreshGeneration;
        try
        {
            languageRefreshOperation = Dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                new Action(() => ApplyLanguageRefresh(refreshGeneration)));
        }
        catch (InvalidOperationException)
        {
            languageRefreshOperation = null;
        }
    }

    private void ApplyLanguageRefresh(int refreshGeneration)
    {
        languageRefreshOperation = null;
        if (!IsDisposed
            && refreshGeneration == languageRefreshGeneration
            && IsLoaded)
        {
            viewModel.RefreshLocalizedPresentation();
        }
    }

    private void CancelLanguageRefresh()
    {
        languageRefreshGeneration++;
        var operation = languageRefreshOperation;
        languageRefreshOperation = null;
        if (operation?.Status == DispatcherOperationStatus.Pending)
        {
            operation.Abort();
        }
    }

    private void QueueSourceUnloadCancellation(int unloadGeneration)
    {
        CancelSourceUnloadCancellation();
        sourceUnloadCancellationGeneration = unloadGeneration;
        if (IsDisposed || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        try
        {
            sourceUnloadCancellationOperation = Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(ApplySourceUnloadCancellation));
        }
        catch (InvalidOperationException)
        {
            sourceUnloadCancellationOperation = null;
        }
    }

    private void ApplySourceUnloadCancellation()
    {
        sourceUnloadCancellationOperation = null;
        if (!IsDisposed
            && sourceUnloadCancellationGeneration == sourceLoadUnloadGeneration
            && !IsLoaded)
        {
            sourceLoadOperations.CancelCurrent();
            lazPointCloudLoadCoordinator.CancelCurrent();
        }
    }

    private void CancelSourceUnloadCancellation()
    {
        var operation = sourceUnloadCancellationOperation;
        sourceUnloadCancellationOperation = null;
        if (operation?.Status == DispatcherOperationStatus.Pending)
        {
            operation.Abort();
        }
    }

    private void TryRetireOpenGLResourcesForDispose()
    {
        openGLResourceRetirementAttemptCount++;
        if (!Viewport.IsLoaded)
        {
            openGLResourceRetirementContextUnavailableCount++;
            DropOpenGLResourceReferencesAfterDispose();
            return;
        }

        try
        {
            Viewport.RenderTrigger = RenderTrigger.Manual;
            Viewport.DoRender();
        }
        catch (InvalidOperationException)
        {
            // SharpGL may reject a draw after its context has started closing.
            // Drop managed handles and let context teardown own unavailable GL
            // objects; this path is intentionally not a leak-proof guarantee.
            openGLResourceRetirementFailureCount++;
        }
        finally
        {
            try
            {
                Viewport.RenderTrigger = RenderTrigger.TimerBased;
            }
            catch (InvalidOperationException)
            {
                // The control is already disposed or its Dispatcher is closing.
            }

            DropOpenGLResourceReferencesAfterDispose();
        }
    }

    private void DropOpenGLResourceReferencesAfterDispose()
    {
        c3dRenderResources.ClearManagedReferences();
        importedMeshTextureId = 0;
        importedMeshTextureSource = null;
        importedMeshTextureReleasePending = false;
    }

    /// <summary>
    /// Releases managed source and render snapshots after all context-bound
    /// OpenGL retirement work has completed. The ViewModel presentation state
    /// is intentionally left intact for its existing host contract; this
    /// boundary only drops data owned by the control itself.
    /// </summary>
    private void ClearManagedDataReferencesAfterDispose()
    {
        c3dSample = null;
        c3dRenderProxyCache.Clear();
        c3dRenderPositionCache.Clear();
        importedMesh = null;
        lazSourceState.Clear();
        lazPointCloudCache.Clear();
        lazPointCloudReloadTask = Task.CompletedTask;
        CurrentViewerOnlySourcePath = null;
        CurrentViewerOnlySourceFormat = null;
        selectedImportedMeshPoint = null;
        selectedImportedMeshTriangleIndex = null;
        selectedImportedMeshSurfaceNormal = null;
        selectedLazPoint = null;
        importedMeshTwoPointFirst = null;
        importedMeshTwoPointSecond = null;
        lazTwoPointFirst = null;
        lazTwoPointSecond = null;
        twoPointFirst = null;
        twoPointSecond = null;
        profileFirst = null;
        profileSecond = null;
        profileSamples = [];
        profileSourceSha256 = null;
        linkedHeightCursor = null;
        lastPublishedThreeDGridHover = null;
        planeReferenceMeasurement = null;
        planeFlatnessEvaluation = null;
        teachingOrientedBoxDraft = null;
        teachingOrientedBoxDragStart = null;
        teachingGridRectangleDragStart = null;
        teachingGridRectangleAutomaticHeights.Clear();
        ClearAffineApplyRenderData();
        ClearRegridHeightFieldRenderData();
        ClearSurfaceMatchRenderData();
    }

    internal bool HasManagedDataReferences =>
        c3dSample is not null
        || c3dRenderProxyCache.HasValue
        || c3dRenderPositionCache.HasValue
        || importedMesh is not null
        || lazSample is not null
        || lazPointCloud is not null
        || lazPointCloudCache.HasEntries
        || affineApplyRenderOutput is not null
        || affineApplyLocatorToPointIndex is not null
        || affineApplyRenderedPointIndexes is not null
        || regridHeightFieldRenderOutput is not null
        || regridHeightFieldPositions is not null
        || regridHeightFieldPopulated is not null
        || surfaceMatchRenderExecution is not null
        || surfaceMatchOverlayPositions is not null
        || surfaceMatchOverlayTriangles is not null
        || surfaceMatchScenePositions is not null
        || surfaceMatchCorrespondences is not null
        || surfaceEdgeModelSegments is not null
        || surfaceEdgeSceneSegments is not null;

    public bool SidePanelsVisible
    {
        get => (bool)GetValue(SidePanelsVisibleProperty);
        set => SetValue(SidePanelsVisibleProperty, value);
    }

    public MainWindowViewModel ViewModel => viewModel;

    public event EventHandler? CameraChanged;

    public ViewerCameraState CaptureCameraState() =>
        viewModel.CaptureCameraState();

    public bool TryApplyCameraState(ViewerCameraState state)
    {
        if (IsDisposed)
        {
            return false;
        }

        if (!viewModel.TryApplyCameraState(state))
        {
            return false;
        }

        RequestVisibleFrame();
        return true;
    }

    public int SmokeExitCode => smokeExitCode;

    public int VisibleFrameRequestCount { get; private set; }

    public string HostApiVersion => ViewerHostContract.ApiVersion;

    public ViewerHostState HostState => new(
        viewModel.C3DSampleVisible,
        viewModel.SelectedEntity,
        viewModel.SelectedSelectionMode,
        viewModel.PickCoordinate,
        viewModel.MeasurementSummary,
        viewModel.ResultSummary,
        viewModel.RecipeSummary,
        viewModel.ViewerStatus,
        viewModel.CoordinateFrameSummary);

    public event EventHandler<ViewerHostStateChangedEventArgs>? HostStateChanged;
    public event EventHandler? ProfileViewRequested;

    private void OnViewModelCameraChanged(object? sender, EventArgs args) =>
        CameraChanged?.Invoke(this, args);

    public void FitAll() => ExecuteHostCommand(viewModel.FitAllCommand);

    public void FitSelection() => ExecuteHostCommand(viewModel.FitSelectionCommand);

    public void FitRoi() => ExecuteHostCommand(viewModel.FitRoiCommand);

    public void UseTopView() => ExecuteHostCommand(viewModel.TopViewCommand);

    public void UsePerspectiveView() => ExecuteHostCommand(viewModel.PerspectiveViewCommand);

    public void ResetView() => ExecuteHostCommand(viewModel.ResetCommand);

    public void RequestVisibleFrame()
    {
        if (IsDisposed)
        {
            return;
        }

        if (!Dispatcher.CheckAccess())
        {
            if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
            {
                QueueVisibleFrameRequest();
            }

            return;
        }

        if (IsDisposed)
        {
            return;
        }

        visibleFrameRequestGeneration++;
        StopVisibleFrameRetryTimer();
        RequestVisibleFrameCore(visibleFrameRequestGeneration, attempt: 0);
    }

    private void RequestVisibleFrameCore(int generation, int attempt)
    {
        CancelVisibleFrameRequest();
        if (IsDisposed || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        try
        {
            var operation = Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(() => ApplyVisibleFrameRequest(generation, attempt)));
            lock (visibleFrameRequestOperationGate)
            {
                visibleFrameRequestOperation = operation;
            }
        }
        catch (InvalidOperationException)
        {
            lock (visibleFrameRequestOperationGate)
            {
                visibleFrameRequestOperation = null;
            }
        }
    }

    private void QueueVisibleFrameRequest()
    {
        if (IsDisposed
            || Dispatcher.HasShutdownStarted
            || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        lock (visibleFrameRequestOperationGate)
        {
            if (visibleFrameRequestOperation?.Status
                is DispatcherOperationStatus.Pending
                or DispatcherOperationStatus.Executing)
            {
                return;
            }

            try
            {
                visibleFrameRequestOperation = Dispatcher.BeginInvoke(
                    DispatcherPriority.Loaded,
                    new Action(ApplyQueuedVisibleFrameRequest));
            }
            catch (InvalidOperationException)
            {
                visibleFrameRequestOperation = null;
            }
        }
    }

    private void ApplyQueuedVisibleFrameRequest()
    {
        lock (visibleFrameRequestOperationGate)
        {
            visibleFrameRequestOperation = null;
        }
        RequestVisibleFrame();
    }

    private void ApplyVisibleFrameRequest(int generation, int attempt)
    {
        lock (visibleFrameRequestOperationGate)
        {
            visibleFrameRequestOperation = null;
        }
        if (IsDisposed || generation != visibleFrameRequestGeneration)
        {
            return;
        }

        if (IsLoaded
            && IsVisible
            && Viewport.IsVisible
            && Viewport.ActualWidth >= 2
            && Viewport.ActualHeight >= 2)
        {
            Viewport.UpdateLayout();
            Viewport.RenderTrigger = RenderTrigger.Manual;
            Viewport.DoRender();
            Viewport.RenderTrigger = RenderTrigger.TimerBased;
            Viewport.InvalidateVisual();
            VisibleFrameRequestCount++;
        }

        if (attempt >= 2)
        {
            return;
        }

        StopVisibleFrameRetryTimer();
        visibleFrameRetryGeneration = generation;
        visibleFrameRetryAttempt = attempt;
        visibleFrameRetryTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(attempt == 0 ? 160 : 360)
        };
        visibleFrameRetryTimer.Tick += OnVisibleFrameRetryTimerTick;
        visibleFrameRetryTimer.Start();
    }

    private void CancelVisibleFrameRequest()
    {
        DispatcherOperation? operation;
        lock (visibleFrameRequestOperationGate)
        {
            operation = visibleFrameRequestOperation;
            visibleFrameRequestOperation = null;
        }

        if (operation?.Status == DispatcherOperationStatus.Pending)
        {
            operation.Abort();
        }
    }

    private void OnVisibleFrameRetryTimerTick(object? sender, EventArgs args)
    {
        if (sender is not DispatcherTimer timer
            || !ReferenceEquals(timer, visibleFrameRetryTimer))
        {
            return;
        }

        var generation = visibleFrameRetryGeneration;
        var attempt = visibleFrameRetryAttempt;
        StopVisibleFrameRetryTimer(timer);
        if (IsDisposed)
        {
            return;
        }

        RequestVisibleFrameCore(generation, attempt + 1);
    }

    private void StopVisibleFrameRetryTimer(DispatcherTimer? expectedTimer = null)
    {
        var timer = visibleFrameRetryTimer;
        if (timer is null
            || expectedTimer is not null && !ReferenceEquals(timer, expectedTimer))
        {
            return;
        }

        timer.Stop();
        timer.Tick -= OnVisibleFrameRetryTimerTick;
        visibleFrameRetryTimer = null;
        visibleFrameRetryGeneration = 0;
        visibleFrameRetryAttempt = 0;
    }

    public bool SaveRecipe(string path) => !IsDisposed && SaveCurrentRecipe(path, isSmoke: false);

    public bool PublishCurrentPreviewResult()
    {
        if (IsDisposed)
        {
            return false;
        }

        if (!EnsureRecipeOutputEnabled())
        {
            return false;
        }

        if (viewModel.NominalActualInput is not null)
        {
            if (!viewModel.NominalActual.CanPublish)
            {
                return false;
            }

            viewModel.NominalActual.PublishCommand.Execute(null);
            return viewModel.NominalActual.State == NominalActualComparisonState.Published;
        }

        return viewModel.PublishPreviewResult();
    }

    private static void OnSidePanelsVisibleChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        ((OpenVisionThreeDViewerControl)dependencyObject).UpdateSidePanelsVisibility();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (IsDisposed)
        {
            return;
        }

        if (args.PropertyName == nameof(MainWindowViewModel.DeviationLegendVisible))
        {
            UpdateDeviationLegendVisibility();
        }

        if (args.PropertyName == nameof(MainWindowViewModel.PointCloudColorLegendVisible))
        {
            UpdatePointCloudColorLegendVisibility();
        }

        if (args.PropertyName is nameof(MainWindowViewModel.CubeVisible)
            or nameof(MainWindowViewModel.PointCloudVisible)
            or nameof(MainWindowViewModel.C3DSampleVisible)
            or nameof(MainWindowViewModel.GlbSampleVisible)
            or nameof(MainWindowViewModel.LazSampleVisible)
            or nameof(MainWindowViewModel.MeasurementVisible)
            or nameof(MainWindowViewModel.DisplaySettingsRevision)
            or nameof(MainWindowViewModel.C3DHeightColorRangeRevision)
            or nameof(MainWindowViewModel.PointSize)
            or nameof(MainWindowViewModel.RecipePeakTolerance)
            or nameof(MainWindowViewModel.C3DModelTransform)
            or nameof(MainWindowViewModel.ProjectionMode)
            or nameof(MainWindowViewModel.OrthographicHeight)
            or nameof(MainWindowViewModel.SelectedTeachingRoiDisplayHeightOffset)
            or nameof(MainWindowViewModel.SelectedSelectionMode)
            or nameof(MainWindowViewModel.SelectionOverlayVisible)
            or nameof(MainWindowViewModel.ResultOverlayVisible)
            or nameof(MainWindowViewModel.WorkbenchTwoPointLine)
            or nameof(MainWindowViewModel.IsWorkbenchTwoPointLinePublished)
            or nameof(MainWindowViewModel.WorkbenchThreePointPlane)
            or nameof(MainWindowViewModel.IsWorkbenchThreePointPlanePublished)
            or nameof(MainWindowViewModel.WorkbenchLineFit)
            or nameof(MainWindowViewModel.SelectedWorkbenchLineFitPoint)
            or nameof(MainWindowViewModel.LineFitInliersVisible)
            or nameof(MainWindowViewModel.LineFitOutliersVisible)
            or nameof(MainWindowViewModel.LineFitSegmentVisible)
            or nameof(MainWindowViewModel.LineFitSelectedResidualVisible)
            or nameof(MainWindowViewModel.WorkbenchFirstIntersectionLine)
            or nameof(MainWindowViewModel.WorkbenchSecondIntersectionLine)
            or nameof(MainWindowViewModel.WorkbenchLineIntersection)
            or nameof(MainWindowViewModel.LineIntersectionFirstLineVisible)
            or nameof(MainWindowViewModel.LineIntersectionSecondLineVisible)
            or nameof(MainWindowViewModel.LineIntersectionClosestConnectorVisible)
            or nameof(MainWindowViewModel.LineIntersectionCornerAnchorVisible)
            or nameof(MainWindowViewModel.WorkbenchLandmarkCorrespondenceAnchors)
            or nameof(MainWindowViewModel.WorkbenchLandmarkCorrespondence)
            or nameof(MainWindowViewModel.WorkbenchAffineApply)
            or nameof(MainWindowViewModel.IsWorkbenchAffineApplyPublished)
            or nameof(MainWindowViewModel.WorkbenchRegridHeightField)
            or nameof(MainWindowViewModel.IsWorkbenchRegridHeightFieldPublished)
            or nameof(MainWindowViewModel.WorkbenchSurfaceMatch)
            or nameof(MainWindowViewModel.ResultEntities))
        {
            if (args.PropertyName == nameof(MainWindowViewModel.RecipePeakTolerance))
            {
                if (c3dSample is not null)
                {
                    HeightDeviationRuleCoordinator.ApplyToViewModel(
                        viewModel,
                        c3dSample,
                        viewModel.RecipeSourceName,
                        viewModel.RecipePeakTolerance,
                        viewModel.RecipeSourceUnit);
                }
            }

            if ((args.PropertyName == nameof(MainWindowViewModel.SelectedSelectionMode)
                    || args.PropertyName == nameof(MainWindowViewModel.C3DSampleVisible)
                    || args.PropertyName == nameof(MainWindowViewModel.C3DModelTransform))
                && viewModel.SelectedSelectionMode == RoiStepSelectionMode)
            {
                UpdateRoiStepMeasurement();
            }

            if (args.PropertyName == nameof(MainWindowViewModel.C3DModelTransform)
                && viewModel.SelectedSelectionMode == "Plane Distance"
                && viewModel.PlaneReferenceMeasurementVisible)
            {
                FitC3DReferencePlane();
            }

            if (args.PropertyName == nameof(MainWindowViewModel.C3DModelTransform)
                && viewModel.PlaneFlatnessVisible)
            {
                planeFlatnessEvaluation = null;
                planeReferenceMeasurement = null;
                viewModel.InvalidatePlaneFlatnessPreview("Alignment changed; run Preview Flatness again");
            }

            RenderNow();
        }
        else if (args.PropertyName == nameof(MainWindowViewModel.SelectedRenderDensity))
        {
            ReloadDefaultC3DSample();
            if (!suppressLazPointCloudDensityReload)
            {
                lazPointCloudDensityEventReloadCount++;
                lazPointCloudReloadTask = ReloadCurrentLazPointCloudAsync();
            }
            if (viewModel.SelectedSelectionMode == RoiStepSelectionMode)
            {
                UpdateRoiStepMeasurement();
            }

            RenderNow();
        }
        else if (IsRecipeRoiEditProperty(args.PropertyName))
        {
            if (!suppressRecipeParameterSync)
            {
                ApplyEditedRoiStepParameters();
            }

            RenderNow();
        }

        RaiseHostStateChanged(args.PropertyName);
    }

    private void OnNominalActualPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(NominalActualComparisonViewModel.ActualVisible)
            or nameof(NominalActualComparisonViewModel.NominalVisible)
            or nameof(NominalActualComparisonViewModel.LowerTolerance)
            or nameof(NominalActualComparisonViewModel.UpperTolerance)
            or nameof(NominalActualComparisonViewModel.PreviewResult)
            or nameof(NominalActualComparisonViewModel.SelectedDeviation)
            or nameof(NominalActualComparisonViewModel.State))
        {
            RenderNow();
        }
    }

    private async void OnNominalActualPreviewRequested(
        object? sender,
        NominalActualPreviewRequestedEventArgs args)
    {
        if (IsDisposed)
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
            if (IsDisposed
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
            viewerLifetimeLinkedCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    args.CancellationToken,
                    viewerLifetimeToken);
            operationToken = viewerLifetimeLinkedCancellation.Token;
            var result = await nominalActualComparisonExecutor.ExecuteAsync(
                executionInput,
                args.MaximumDisplaySamples,
                progress,
                operationToken);
            if (IsDisposed || operationToken.IsCancellationRequested)
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
            RenderNow();
        }
        catch (OperationCanceledException) when (operationToken.IsCancellationRequested)
        {
            // The ViewModel already owns the cancelled/stale state transition.
        }
        catch (Exception exception)
        {
            if (IsDisposed)
            {
                return;
            }

            if (comparison.FailPreview(args.RequestId, exception.Message))
            {
                viewModel.ViewerStatus = $"Nominal/actual Preview failed: {exception.Message}";
            }

            if (smokeNominalActualPreview)
            {
                smokeExitCode = 1;
            }

            RenderNow();
        }
        finally
        {
            viewerLifetimeLinkedCancellation?.Dispose();
        }
    }

    private void OnNominalActualPublishRequested(
        object? sender,
        NominalActualPublishRequestedEventArgs args)
    {
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
        RenderNow();
    }

    private void RaiseHostStateChanged(string? viewModelPropertyName)
    {
        var hostPropertyName = viewModelPropertyName switch
        {
            nameof(MainWindowViewModel.C3DSampleVisible) => nameof(ViewerHostState.C3DSampleVisible),
            nameof(MainWindowViewModel.SelectedEntity) => nameof(ViewerHostState.ActiveEntity),
            nameof(MainWindowViewModel.SelectedSelectionMode) => nameof(ViewerHostState.SelectionMode),
            nameof(MainWindowViewModel.PickCoordinate) => nameof(ViewerHostState.PickCoordinate),
            nameof(MainWindowViewModel.MeasurementSummary) => nameof(ViewerHostState.MeasurementSummary),
            nameof(MainWindowViewModel.ResultSummary) => nameof(ViewerHostState.ResultSummary),
            nameof(MainWindowViewModel.RecipeSummary) => nameof(ViewerHostState.RecipeSummary),
            nameof(MainWindowViewModel.ViewerStatus) => nameof(ViewerHostState.ViewerStatus),
            _ => null
        };

        if (hostPropertyName is not null)
        {
            HostStateChanged?.Invoke(this, new ViewerHostStateChangedEventArgs(HostState, hostPropertyName));
        }
    }

    private void ExecuteHostCommand(ICommand command)
    {
        if (!IsDisposed && command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private void UpdateSidePanelsVisibility()
    {
        if (LeftSidePanel is null || RightSidePanel is null)
        {
            return;
        }

        var visibility = SidePanelsVisible ? Visibility.Visible : Visibility.Collapsed;
        LeftSidePanel.Visibility = visibility;
        RightSidePanel.Visibility = visibility;
        UpdateDeviationLegendVisibility();
        UpdatePointCloudColorLegendVisibility();
    }

    private void UpdateDeviationLegendVisibility()
    {
        if (DeviationLegendPanel is null)
        {
            return;
        }

        DeviationLegendPanel.Visibility = viewModel.DeviationLegendVisible && SidePanelsVisible
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void UpdatePointCloudColorLegendVisibility()
    {
        if (PointCloudColorLegendPanel is null)
        {
            return;
        }

        PointCloudColorLegendPanel.Visibility = viewModel.PointCloudColorLegendVisible && SidePanelsVisible
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

}
