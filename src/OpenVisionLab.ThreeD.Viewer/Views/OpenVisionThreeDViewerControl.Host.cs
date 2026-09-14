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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Viewer.Localization;
using OpenVisionLab.ThreeD.Viewer.Recipes;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.Rendering;
using OpenVisionLab.ThreeD.Viewer.ViewModels;
using OpenVisionLab.ThreeD.Tools;
using SharpGL;
using SharpGL.WPF;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    private readonly SharpGlRenderContextLifetime renderContextLifetime = new();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        languageRefresh.Cancel();
        sourceUnloadCancellation.MarkLoaded();
        viewerEventSubscription.Attach();
        UpdateOrientationTriad();
        RequestVisibleFrame();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        languageRefresh.Cancel();
        sourceUnloadCancellation.ScheduleAfterUnload();

        visibleFrameRequests.Invalidate();
        StopInteractionWireframeLod();
        viewerEventSubscription.Detach();
    }

    private void OnViewerLanguageChanged(object? sender, EventArgs args)
    {
        languageRefresh.Request();
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

        ViewerLocalizationScope.Detach(this);
        viewerLifetimeCancellation.Cancel();
        nominalActualComparisonCoordinator.Dispose();
        recipeLoadWorkflow.Dispose();
        smokeScenario.Dispose();
        languageRefresh.Cancel();
        languageRefresh.Dispose();
        sourceUnloadCancellation.Dispose();
        visibleFrameRequests.Invalidate();
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

        visibleFrameRequests.Dispose();
        sourceLoadOperations.Dispose();
        lazPointCloudSession.Dispose();
        viewerEventSubscription.Dispose();
        nominalActualEditor.Dispose();
        editor.Dispose();
        displayEditor.Dispose();
        linkedView.Dispose();
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        Loaded -= SmokeCaptureOnLoaded;

        TryRetireOpenGLResourcesForDispose();
        renderContextLifetime.Dispose(Viewport);
        ClearManagedDataReferencesAfterDispose();
        viewerLifetimeCancellation.Dispose();
        GC.SuppressFinalize(this);
    }

    private void TryRetireOpenGLResourcesForDispose()
    {
        openGLResourceRetirementTelemetry.RecordAttempt();
        if (!Viewport.IsLoaded)
        {
            openGLResourceRetirementTelemetry.RecordContextUnavailable();
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
            openGLResourceRetirementTelemetry.RecordFailure();
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
        importedMeshTextureState.ClearManagedReferencesAfterDispose();
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
        lazPointCloudSession.Clear();
        lazPointCloudLoadTelemetry.ClearReloadTask();
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
        workbenchOverlayRenderer.Clear();
        ClearSurfaceMatchRenderData();
    }

    internal bool HasManagedDataReferences =>
        c3dSample is not null
        || c3dRenderProxyCache.HasValue
        || c3dRenderPositionCache.HasValue
        || importedMesh is not null
        || lazPointCloudSession.HasManagedData
        || workbenchOverlayRenderer.HasManagedData
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

    /// <summary>
    /// Compatibility facade for existing Shell WPF composition. New hosts
    /// should use <see cref="IOpenVisionThreeDViewerHost"/> state and
    /// operations instead of reaching into the concrete ViewModel.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public MainWindowViewModel ViewModel => viewModel;

    /// <summary>
    /// Stable task and inspection editor bindings for WPF hosts. The surface
    /// forwards to Viewer-owned state and does not create a second state store.
    /// </summary>
    public ViewerHostEditorSurface Editor => editor;

    /// <summary>
    /// Stable WPF bindings for the Viewer display-settings editor.
    /// </summary>
    public ViewerHostDisplayEditorSurface DisplayEditor => displayEditor;

    /// <summary>
    /// Stable WPF bindings for the Nominal/Actual comparison editor.
    /// </summary>
    public ViewerHostNominalActualEditorSurface NominalActualEditor => nominalActualEditor;

    /// <summary>
    /// Stable WPF bindings for the Linked View height-map presentation.
    /// </summary>
    public ViewerHostLinkedViewSurface LinkedView => linkedView;

    public event EventHandler? CameraChanged;

    public ViewerCameraState CaptureCameraState() => hostOperations.CaptureCameraState();

    public bool TryApplyCameraState(ViewerCameraState state) => hostOperations.TryApplyCameraState(state);

    public bool TrySetSelectionMode(string selectionMode) => hostOperations.TrySetSelectionMode(selectionMode);

    public bool TrySetSelectionOverlayVisible(bool visible) => hostOperations.TrySetSelectionOverlayVisible(visible);

    public bool TrySetHudDetailsVisible(bool visible) => hostOperations.TrySetHudDetailsVisible(visible);

    public bool TrySetC3DSampleVisible(bool visible) => hostOperations.TrySetC3DSampleVisible(visible);

    public bool TrySetSelectedColorMap(string colorMap) => hostOperations.TrySetSelectedColorMap(colorMap);

    public bool TrySetSelectedDiagnosticChannel(ViewerDiagnosticChannelOption? channel) =>
        hostOperations.TrySetSelectedDiagnosticChannel(channel);

    public bool TrySetResultOverlayVisible(bool visible) => hostOperations.TrySetResultOverlayVisible(visible);

    public bool TrySetMeasurementVisible(bool visible) => hostOperations.TrySetMeasurementVisible(visible);

    public bool TrySetC3DHeightColorMinimumRaw(double value) => hostOperations.TrySetC3DHeightColorMinimumRaw(value);

    public bool TrySetC3DHeightColorMaximumRaw(double value) => hostOperations.TrySetC3DHeightColorMaximumRaw(value);

    public bool TryShiftC3DHeightColorMinimum(int direction) => hostOperations.TryShiftC3DHeightColorMinimum(direction);

    public bool TryShiftC3DHeightColorMaximum(int direction) => hostOperations.TryShiftC3DHeightColorMaximum(direction);

    public bool TryResetC3DHeightColorRange() => hostOperations.TryResetC3DHeightColorRange();

    public bool TryApplyLinkedC3DHeightColorRange(double minimum, double maximum) =>
        hostOperations.TryApplyLinkedC3DHeightColorRange(minimum, maximum);

    public int SmokeExitCode => smokeScenario.ExitCode;

    public int VisibleFrameRequestCount => visibleFrameRequests.RequestCount;

    public string HostApiVersion => ViewerHostContract.ApiVersion;

    public ViewerHostState HostState =>
        (ViewerHostState)GetValue(HostStateProperty);

    public event EventHandler<ViewerHostStateChangedEventArgs>? HostStateChanged;
    public event EventHandler? ProfileViewRequested;

    private void PublishHostStateChanged(ViewerHostStateChangedEventArgs args)
    {
        SetCurrentValue(HostStateProperty, args.State);
        HostStateChanged?.Invoke(this, args);
    }

    private void OnViewModelCameraChanged(object? sender, EventArgs args) =>
        CameraChanged?.Invoke(this, args);

    public void FitAll() => hostOperations.FitAll();

    public void FitSelection() => hostOperations.FitSelection();

    public void FitRoi() => hostOperations.FitRoi();

    public void UseTopView() => hostOperations.UseTopView();

    public void UsePerspectiveView() => hostOperations.UsePerspectiveView();

    public void ResetView() => hostOperations.ResetView();

    public void RequestVisibleFrame() => visibleFrameRequests.Request();

    public bool SaveRecipe(string path) => hostOperations.SaveRecipe(path);

    public bool PublishCurrentPreviewResult() => hostOperations.PublishCurrentPreviewResult();

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

        var effects = ViewerViewModelPropertyChangePolicy.Classify(args.PropertyName);
        if ((effects & ViewerViewModelPropertyChangeEffects.UpdateDeviationLegendVisibility) != 0)
        {
            UpdateDeviationLegendVisibility();
        }

        if ((effects & ViewerViewModelPropertyChangeEffects.UpdatePointCloudColorLegendVisibility) != 0)
        {
            UpdatePointCloudColorLegendVisibility();
        }

        if ((effects & ViewerViewModelPropertyChangeEffects.ReloadRenderDensity) != 0)
        {
            ReloadDefaultC3DSample();
            if (!lazPointCloudLoadTelemetry.IsDensityReloadSuppressed)
            {
                lazPointCloudLoadTelemetry.RecordDensityEventReload(ReloadCurrentLazPointCloudAsync);
            }

            if (viewModel.SelectedSelectionMode == RoiStepSelectionMode)
            {
                roiEditingSession.UpdateRoiStepMeasurement();
            }

            RenderNow();
        }
        else if ((effects & ViewerViewModelPropertyChangeEffects.SyncRecipeRoiParameters) != 0)
        {
            roiEditingSession.ApplyEditedParametersFromPropertyChange();

            RenderNow();
        }
        else if ((effects & ViewerViewModelPropertyChangeEffects.Render) != 0)
        {
            if ((effects & ViewerViewModelPropertyChangeEffects.ApplyHeightDeviationRule) != 0
                && c3dSample is not null)
            {
                HeightDeviationRuleCoordinator.ApplyToViewModel(
                    viewModel,
                    c3dSample,
                    viewModel.RecipeSourceName,
                    viewModel.RecipePeakTolerance,
                    viewModel.RecipeSourceUnit);
            }

            if ((effects & ViewerViewModelPropertyChangeEffects.UpdateRoiStepMeasurement) != 0
                && viewModel.SelectedSelectionMode == RoiStepSelectionMode)
            {
                roiEditingSession.UpdateRoiStepMeasurement();
            }

            if ((effects & ViewerViewModelPropertyChangeEffects.FitReferencePlane) != 0
                && viewModel.SelectedSelectionMode == "Plane Distance"
                && viewModel.PlaneReferenceMeasurementVisible)
            {
                FitC3DReferencePlane();
            }

            if ((effects & ViewerViewModelPropertyChangeEffects.InvalidatePlaneFlatness) != 0
                && viewModel.PlaneFlatnessVisible)
            {
                planeFlatnessEvaluation = null;
                planeReferenceMeasurement = null;
                viewModel.InvalidatePlaneFlatnessPreview("Alignment changed; run Preview Flatness again");
            }

            RenderNow();
        }

        hostStateCoordinator.Notify(args.PropertyName);
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

        if (args.PropertyName == nameof(NominalActualComparisonViewModel.State))
        {
            hostStateCoordinator.Notify("NominalActual.State");
        }

        if (args.PropertyName is nameof(NominalActualComparisonViewModel.InputsReady)
            or nameof(NominalActualComparisonViewModel.EvidenceSummary)
            or nameof(NominalActualComparisonViewModel.StateSummary)
            or nameof(NominalActualComparisonViewModel.DirectionSummary)
            or nameof(NominalActualComparisonViewModel.CurrentDisplaySamplingSummary)
            or nameof(NominalActualComparisonViewModel.NextPreviewSamplingSummary)
            or nameof(NominalActualComparisonViewModel.DisplaySamplingChangePending)
            or nameof(NominalActualComparisonViewModel.ProgressPercent)
            or nameof(NominalActualComparisonViewModel.DistributionVisible)
            or nameof(NominalActualComparisonViewModel.DistributionSummary))
        {
            hostStateCoordinator.Notify($"NominalActual.{args.PropertyName}");
        }
    }

    private void OnNominalActualPreviewRequested(
        object? sender,
        NominalActualPreviewRequestedEventArgs args) =>
        nominalActualComparisonCoordinator.StartPreview(args);

    private void OnNominalActualPublishRequested(
        object? sender,
        NominalActualPublishRequestedEventArgs args) =>
        nominalActualComparisonCoordinator.HandlePublish(args);

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
