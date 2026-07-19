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
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.Rendering;
using OpenVisionLab.ThreeD.Viewer.ViewModels;
using OpenVisionLab.ThreeD.Tools;
using SharpGL;
using SharpGL.WPF;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SubscribeViewModelEvents();
        UpdateOrientationTriad();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
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
        viewModelEventsSubscribed = true;
    }

    private void UnsubscribeViewModelEvents()
    {
        viewModel.FitAllRequested -= fitAllRequestedHandler;
        viewModel.FitSelectionRequested -= fitSelectionRequestedHandler;
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
        viewModelEventsSubscribed = false;
    }

    public bool SidePanelsVisible
    {
        get => (bool)GetValue(SidePanelsVisibleProperty);
        set => SetValue(SidePanelsVisibleProperty, value);
    }

    public MainWindowViewModel ViewModel => viewModel;

    public int SmokeExitCode => smokeExitCode;

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

    public void FitAll() => ExecuteHostCommand(viewModel.FitAllCommand);

    public void FitSelection() => ExecuteHostCommand(viewModel.FitSelectionCommand);

    public void ResetView() => ExecuteHostCommand(viewModel.ResetCommand);

    public bool SaveRecipe(string path) => SaveCurrentRecipe(path, isSmoke: false);

    public bool PublishCurrentPreviewResult()
    {
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
            or nameof(MainWindowViewModel.PointSize)
            or nameof(MainWindowViewModel.RecipePeakTolerance)
            or nameof(MainWindowViewModel.C3DModelTransform)
            or nameof(MainWindowViewModel.SelectedSelectionMode)
            or nameof(MainWindowViewModel.SelectionOverlayVisible)
            or nameof(MainWindowViewModel.ResultOverlayVisible)
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
            or nameof(MainWindowViewModel.ResultEntities))
        {
            if (args.PropertyName == nameof(MainWindowViewModel.RecipePeakTolerance))
            {
                ConfigureC3DHeightDeviationRule();
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
            ReloadCurrentLazPointCloud();
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
        var comparison = viewModel.NominalActual;
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
            comparison.ReportPreviewProgress(
                args.RequestId,
                value.ProcessedPointCount,
                value.TotalPointCount,
                value.Elapsed,
                value.Stage));

        try
        {
            var result = await nominalActualComparisonExecutor.ExecuteAsync(
                executionInput,
                args.MaximumDisplaySamples,
                progress,
                args.CancellationToken);
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
        catch (OperationCanceledException) when (args.CancellationToken.IsCancellationRequested)
        {
            // The ViewModel already owns the cancelled/stale state transition.
        }
        catch (Exception exception)
        {
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
    }

    private void OnNominalActualPublishRequested(
        object? sender,
        NominalActualPublishRequestedEventArgs args)
    {
        var comparison = viewModel.NominalActual;
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

    private static void ExecuteHostCommand(ICommand command)
    {
        if (command.CanExecute(null))
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
