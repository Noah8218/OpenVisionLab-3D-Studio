using System.ComponentModel;
using System.IO;
using System.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class ViewerControlLifetimeVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer control lifetime verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Scope: managed disposal/callback boundary without an active OpenGL context"
        };
        var passed = 0;
        var total = 0;
        Exception? failure = null;

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        var thread = new Thread(
            () =>
            {
                try
                {
                    var control = new OpenVisionThreeDViewerControl(loadDefaultSamples: true);
                    Check(
                        "concrete control exposes IDisposable",
                        control is IDisposable,
                        $"type={control.GetType().FullName}|disposable={control is IDisposable}");
                    Check(
                        "host API matches the current contract metadata",
                        control.HostApiVersion == ViewerHostContract.ApiVersion,
                        $"api={control.HostApiVersion}|contract={ViewerHostContract.ApiVersion}");
                    Check(
                        "default Viewer source data is owned before disposal",
                        control.HasManagedDataReferences,
                        $"hasManagedData={control.HasManagedDataReferences}");

                    var editorPropertyChangedCount = 0;
                    PropertyChangedEventHandler editorPropertyChanged = (_, _) => editorPropertyChangedCount++;
                    control.Editor.PropertyChanged += editorPropertyChanged;
                    var originalThicknessRow = control.Editor.ThicknessRoiRow;
                    control.Editor.ThicknessRoiRow = originalThicknessRow + 1;
                    var editorRoundTripSucceeded =
                        control.Editor.ThicknessRoiRow == originalThicknessRow + 1
                        && control.ViewModel.ThicknessRoiRow == originalThicknessRow + 1
                        && editorPropertyChangedCount > 0;
                    control.Editor.ThicknessRoiRow = originalThicknessRow;
                    control.Editor.PropertyChanged -= editorPropertyChanged;
                    Check(
                        "task editor surface forwards mutable state and notifications",
                        editorRoundTripSucceeded,
                        $"editorRow={control.Editor.ThicknessRoiRow}|viewModelRow={control.ViewModel.ThicknessRoiRow}|notifications={editorPropertyChangedCount}");
                    Check(
                        "task editor surface forwards existing commands",
                        ReferenceEquals(control.Editor.PreviewThicknessCommand, control.ViewModel.PreviewThicknessCommand)
                        && ReferenceEquals(control.Editor.PreviewWarpageCommand, control.ViewModel.PreviewWarpageCommand),
                        $"thicknessCommand={control.Editor.PreviewThicknessCommand.GetType().Name}|warpageCommand={control.Editor.PreviewWarpageCommand.GetType().Name}");

                    var recipePropertyChangedCount = 0;
                    PropertyChangedEventHandler recipePropertyChanged = (_, args) =>
                    {
                        if (args.PropertyName == nameof(MainWindowViewModel.RecipePeakTolerance))
                        {
                            recipePropertyChangedCount++;
                        }
                    };
                    control.Editor.PropertyChanged += recipePropertyChanged;
                    var originalRecipePeakTolerance = control.Editor.RecipePeakTolerance;
                    control.Editor.RecipePeakTolerance = originalRecipePeakTolerance + 0.125;
                    var recipeEditorSurfaceSucceeded =
                        control.Editor.RecipePeakTolerance == originalRecipePeakTolerance + 0.125
                        && control.ViewModel.RecipePeakTolerance == originalRecipePeakTolerance + 0.125
                        && recipePropertyChangedCount > 0;
                    control.Editor.RecipePeakTolerance = originalRecipePeakTolerance;
                    control.Editor.PropertyChanged -= recipePropertyChanged;
                    Check(
                        "C3D recipe editor surface forwards mutable state and notifications",
                        recipeEditorSurfaceSucceeded,
                        $"peakTolerance={control.Editor.RecipePeakTolerance:G6}|viewModelPeakTolerance={control.ViewModel.RecipePeakTolerance:G6}|notifications={recipePropertyChangedCount}");
                    Check(
                        "C3D recipe editor surface forwards the Shell save command",
                        ReferenceEquals(control.Editor.SaveRecipeCommand, control.ViewModel.SaveRecipeCommand),
                        $"saveCommand={control.Editor.SaveRecipeCommand.GetType().Name}");

                    var displayPropertyChangedCount = 0;
                    PropertyChangedEventHandler displayPropertyChanged = (_, args) =>
                    {
                        if (args.PropertyName == nameof(ViewerDisplaySettingsViewModel.PointSize))
                        {
                            displayPropertyChangedCount++;
                        }
                    };
                    control.DisplayEditor.PropertyChanged += displayPropertyChanged;
                    var originalPointSize = control.DisplayEditor.PointSize;
                    control.DisplayEditor.PointSize = originalPointSize + 0.5;
                    var displayEditorSurfaceSucceeded =
                        control.DisplayEditor.PointSize == originalPointSize + 0.5
                        && control.ViewModel.PointSize == originalPointSize + 0.5
                        && displayPropertyChangedCount > 0;
                    control.DisplayEditor.PointSize = originalPointSize;
                    control.DisplayEditor.PropertyChanged -= displayPropertyChanged;
                    Check(
                        "display settings surface forwards mutable state and notifications",
                        displayEditorSurfaceSucceeded,
                        $"pointSize={control.DisplayEditor.PointSize:G6}|viewModelPointSize={control.ViewModel.PointSize:G6}|notifications={displayPropertyChangedCount}");
                    Check(
                        "display settings surface forwards existing choices",
                        control.DisplayEditor.AvailableGeometryStyles.Count > 0
                        && control.DisplayEditor.AvailableColorMaps.Count > 0
                        && control.DisplayEditor.RenderDensityModes.Count == 3
                        && control.DisplayEditor.SelectedColorMap == control.ViewModel.Display.SelectedColorMap,
                        $"geometryStyles={control.DisplayEditor.AvailableGeometryStyles.Count}|colorMaps={control.DisplayEditor.AvailableColorMaps.Count}|densityModes={control.DisplayEditor.RenderDensityModes.Count}|selectedColorMap={control.DisplayEditor.SelectedColorMap}");

                    var nominalActualPropertyChangedCount = 0;
                    PropertyChangedEventHandler nominalActualPropertyChanged = (_, _) => nominalActualPropertyChangedCount++;
                    control.NominalActualEditor.PropertyChanged += nominalActualPropertyChanged;
                    var originalOutputEnabled = control.NominalActualEditor.OutputEnabled;
                    control.NominalActualEditor.OutputEnabled = !originalOutputEnabled;
                    var nominalActualSurfaceSucceeded =
                        control.NominalActualEditor.OutputEnabled == !originalOutputEnabled
                        && control.ViewModel.NominalActual.OutputEnabled == !originalOutputEnabled
                        && nominalActualPropertyChangedCount > 0;
                    control.NominalActualEditor.OutputEnabled = originalOutputEnabled;
                    control.NominalActualEditor.PropertyChanged -= nominalActualPropertyChanged;
                    Check(
                        "Nominal/Actual editor surface forwards state and notifications",
                        nominalActualSurfaceSucceeded,
                        $"outputEnabled={control.NominalActualEditor.OutputEnabled}|viewModelOutputEnabled={control.ViewModel.NominalActual.OutputEnabled}|notifications={nominalActualPropertyChangedCount}");
                    Check(
                        "Nominal/Actual editor surface forwards existing commands",
                        ReferenceEquals(control.NominalActualEditor.PreviewCommand, control.ViewModel.NominalActual.PreviewCommand)
                        && ReferenceEquals(control.NominalActualEditor.CancelCommand, control.ViewModel.NominalActual.CancelCommand)
                        && ReferenceEquals(control.NominalActualEditor.PublishCommand, control.ViewModel.NominalActual.PublishCommand),
                        $"previewCommand={control.NominalActualEditor.PreviewCommand.GetType().Name}|cancelCommand={control.NominalActualEditor.CancelCommand.GetType().Name}|publishCommand={control.NominalActualEditor.PublishCommand.GetType().Name}");

                    var toolEditorPropertyChangedCount = 0;
                    PropertyChangedEventHandler toolEditorPropertyChanged = (_, args) =>
                    {
                        if (args.PropertyName == nameof(MainWindowViewModel.PlaneFlatnessTolerance)
                            || args.PropertyName == nameof(MainWindowViewModel.CrossSectionHeightTolerance))
                        {
                            toolEditorPropertyChangedCount++;
                        }
                    };
                    control.Editor.PropertyChanged += toolEditorPropertyChanged;
                    var originalPlaneFlatnessTolerance = control.Editor.PlaneFlatnessTolerance;
                    var originalCrossSectionHeightTolerance = control.Editor.CrossSectionHeightTolerance;
                    control.Editor.PlaneFlatnessTolerance = originalPlaneFlatnessTolerance + 0.125;
                    control.Editor.CrossSectionHeightTolerance = originalCrossSectionHeightTolerance + 0.125;
                    var toolEditorSurfaceSucceeded =
                        control.Editor.PlaneFlatnessTolerance == originalPlaneFlatnessTolerance + 0.125
                        && control.ViewModel.PlaneFlatnessTolerance == originalPlaneFlatnessTolerance + 0.125
                        && control.Editor.CrossSectionHeightTolerance == originalCrossSectionHeightTolerance + 0.125
                        && control.ViewModel.CrossSectionHeightTolerance == originalCrossSectionHeightTolerance + 0.125
                        && toolEditorPropertyChangedCount >= 2;
                    control.Editor.PlaneFlatnessTolerance = originalPlaneFlatnessTolerance;
                    control.Editor.CrossSectionHeightTolerance = originalCrossSectionHeightTolerance;
                    control.Editor.PropertyChanged -= toolEditorPropertyChanged;
                    Check(
                        "Tool Inspector editor surface forwards C3D tool state and notifications",
                        toolEditorSurfaceSucceeded,
                        $"planeTolerance={control.Editor.PlaneFlatnessTolerance:G6}|crossSectionTolerance={control.Editor.CrossSectionHeightTolerance:G6}|notifications={toolEditorPropertyChangedCount}");
                    Check(
                        "Tool Inspector editor surface forwards the existing C3D command instances",
                        ReferenceEquals(control.Editor.FitPlaneCommand, control.ViewModel.FitPlaneCommand)
                        && ReferenceEquals(control.Editor.PreviewPlaneFlatnessCommand, control.ViewModel.PreviewPlaneFlatnessCommand)
                        && ReferenceEquals(control.Editor.PreviewPointPairDimensionsCommand, control.ViewModel.PreviewPointPairDimensionsCommand)
                        && ReferenceEquals(control.Editor.PreviewGapFlushCommand, control.ViewModel.PreviewGapFlushCommand)
                        && ReferenceEquals(control.Editor.PreviewVolumeCommand, control.ViewModel.PreviewVolumeCommand)
                        && ReferenceEquals(control.Editor.PreviewCrossSectionCommand, control.ViewModel.PreviewCrossSectionCommand),
                        $"fitPlane={control.Editor.FitPlaneCommand.GetType().Name}|plane={control.Editor.PreviewPlaneFlatnessCommand.GetType().Name}|pointPair={control.Editor.PreviewPointPairDimensionsCommand.GetType().Name}|gapFlush={control.Editor.PreviewGapFlushCommand.GetType().Name}|volume={control.Editor.PreviewVolumeCommand.GetType().Name}|crossSection={control.Editor.PreviewCrossSectionCommand.GetType().Name}");

                    var hostLayerSnapshot = control.HostState.EntityLayers;
                    var hostLayerProjectionSucceeded =
                        hostLayerSnapshot.Count == control.ViewModel.EntityLayers.Count
                        && hostLayerSnapshot.Count > 0
                        && hostLayerSnapshot.Any(layer => layer.Id == "layer.source.c3d-thickness")
                        && !ReferenceEquals(hostLayerSnapshot, control.ViewModel.EntityLayers);
                    Check(
                        "HostState projects immutable entity layer descriptors",
                        hostLayerProjectionSucceeded,
                        $"hostLayers={hostLayerSnapshot.Count}|viewModelLayers={control.ViewModel.EntityLayers.Count}|sameReference={ReferenceEquals(hostLayerSnapshot, control.ViewModel.EntityLayers)}");

                    var linkedViewPropertyChangedCount = 0;
                    PropertyChangedEventHandler linkedViewPropertyChanged = (_, args) =>
                    {
                        if (args.PropertyName == nameof(MainWindowViewModel.C3DSampleVisible))
                        {
                            linkedViewPropertyChangedCount++;
                        }
                    };
                    control.LinkedView.PropertyChanged += linkedViewPropertyChanged;
                    var originalC3DSampleVisible = control.LinkedView.C3DSampleVisible;
                    var linkedViewToggleSucceeded = control.TrySetC3DSampleVisible(!originalC3DSampleVisible)
                        && control.LinkedView.C3DSampleVisible == !originalC3DSampleVisible
                        && control.LinkedView.C3DSampleVisible == control.ViewModel.C3DSampleVisible
                        && control.LinkedView.HeightMapSummary == control.ViewModel.HeightMapSummary
                        && linkedViewPropertyChangedCount > 0;
                    control.TrySetC3DSampleVisible(originalC3DSampleVisible);
                    control.LinkedView.PropertyChanged -= linkedViewPropertyChanged;
                    Check(
                        "Linked View surface forwards height-map visibility and notifications",
                        linkedViewToggleSucceeded,
                        $"c3dVisible={control.LinkedView.C3DSampleVisible}|heightMap={control.LinkedView.HeightMapSummary}|notifications={linkedViewPropertyChangedCount}");

                    var cameraState = control.CaptureCameraState();
                    Check(
                        "camera state remains usable before disposal",
                        control.TryApplyCameraState(cameraState),
                        $"yaw={cameraState.YawDegrees:G6}|pitch={cameraState.PitchDegrees:G6}|distance={cameraState.Distance:G6}");

                    var hostStateNotificationCount = 0;
                    ViewerHostStateChangedEventArgs? latestHostStateNotification = null;
                    EventHandler<ViewerHostStateChangedEventArgs> hostStateChanged = (_, args) =>
                    {
                        hostStateNotificationCount++;
                        latestHostStateNotification = args;
                    };
                    control.HostStateChanged += hostStateChanged;
                    var selectionHostUpdateSucceeded = control.TrySetSelectionMode("Box ROI");
                    Check(
                        "HostState dependency property follows selection notification",
                        selectionHostUpdateSucceeded
                        && control.HostState.Selection.SelectionMode == "Box ROI"
                        && control.HostState.Selection.Summary == "Box ROI: viewer state only"
                        && hostStateNotificationCount > 0
                        && latestHostStateNotification is not null
                        && latestHostStateNotification.State == control.HostState,
                        $"selection={control.HostState.Selection.SelectionMode}|summary={control.HostState.Selection.Summary}|notifications={hostStateNotificationCount}|eventMatchesProperty={latestHostStateNotification?.State == control.HostState}");
                    control.HostStateChanged -= hostStateChanged;

                    var disposable = (IDisposable)control;
                    disposable.Dispose();
                    Check(
                        "Dispose marks the concrete control disposed",
                        control.IsDisposed,
                        $"disposed={control.IsDisposed}");
                    Check(
                        "Dispose releases control-owned managed source/render data",
                        !control.HasManagedDataReferences,
                        $"hasManagedData={control.HasManagedDataReferences}");

                    var repeatedDisposeSucceeded = true;
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception exception)
                    {
                        repeatedDisposeSucceeded = false;
                        lines.Add($"INFO | repeated Dispose exception | {exception.GetType().Name}: {exception.Message}");
                    }

                    Check(
                        "Dispose is idempotent",
                        repeatedDisposeSucceeded,
                        $"secondDispose={repeatedDisposeSucceeded}");

                    var frameRequestSucceeded = true;
                    try
                    {
                        control.RequestVisibleFrame();
                    }
                    catch (Exception exception)
                    {
                        frameRequestSucceeded = false;
                        lines.Add($"INFO | post-dispose frame request exception | {exception.GetType().Name}: {exception.Message}");
                    }

                    Check(
                        "post-dispose frame scheduling is ignored",
                        frameRequestSucceeded && control.VisibleFrameRequestCount == 0,
                        $"requestSucceeded={frameRequestSucceeded}|visibleFrameRequests={control.VisibleFrameRequestCount}");

                    Check(
                        "post-dispose camera apply is rejected",
                        !control.TryApplyCameraState(cameraState),
                        $"applied={!control.IsDisposed && control.TryApplyCameraState(cameraState)}");

                    var c3dLoadRejected = false;
                    try
                    {
                        control.LoadC3DSource("post-dispose.C3D");
                    }
                    catch (ObjectDisposedException)
                    {
                        c3dLoadRejected = true;
                    }

                    Check(
                        "post-dispose C3D source operation is rejected",
                        c3dLoadRejected,
                        $"rejected={c3dLoadRejected}");

                    var viewerOnlyLoadRejected = false;
                    try
                    {
                        control.LoadViewerOnlySourceAsync(
                                "post-dispose.glb",
                                CancellationToken.None)
                            .GetAwaiter()
                            .GetResult();
                    }
                    catch (ObjectDisposedException)
                    {
                        viewerOnlyLoadRejected = true;
                    }

                    Check(
                        "post-dispose Viewer-only source operation is rejected",
                        viewerOnlyLoadRejected,
                        $"rejected={viewerOnlyLoadRejected}");

                    Check(
                        "post-dispose recipe save is rejected",
                        !control.SaveRecipe(Path.Combine(Path.GetTempPath(), "post-dispose.recipe.json")),
                        "saved=False");

                    var smokeCaptureRejected = false;
                    try
                    {
                        smokeCaptureRejected = !control.CaptureConfiguredSmokeViewAsync()
                            .GetAwaiter()
                            .GetResult();
                    }
                    catch (Exception exception)
                    {
                        lines.Add($"INFO | post-dispose Smoke capture exception | {exception.GetType().Name}: {exception.Message}");
                    }

                    Check(
                        "post-dispose configured Smoke capture is rejected",
                        smokeCaptureRejected,
                        $"rejected={smokeCaptureRejected}");

                    Check(
                        "post-dispose Smoke pick is rejected",
                        !control.ApplyConfiguredSmokePick(),
                        "applied=False");

                    Check(
                        "post-dispose Smoke density change is rejected",
                        !control.ApplyConfiguredSmokeNextDensityAsync()
                            .GetAwaiter()
                            .GetResult(),
                        "applied=False");

                    Check(
                        "post-dispose pointer regression is rejected",
                        !control.RunConfiguredPointerInputRegressionAsync()
                            .GetAwaiter()
                            .GetResult(),
                        "executed=False");

                    var profileSmokeRejected = false;
                    try
                    {
                        profileSmokeRejected = !control.RunProfilePointerSmokeAsync(
                                Path.Combine(Path.GetTempPath(), "post-dispose.profile-smoke.txt"))
                            .GetAwaiter()
                            .GetResult();
                    }
                    catch (Exception exception)
                    {
                        lines.Add($"INFO | post-dispose profile Smoke exception | {exception.GetType().Name}: {exception.Message}");
                    }

                    Check(
                        "post-dispose profile pointer Smoke is rejected",
                        profileSmokeRejected,
                        $"rejected={profileSmokeRejected}");

                    var orientedBoxSmokeRejected = false;
                    try
                    {
                        orientedBoxSmokeRejected = !control.RunTeachingOrientedBoxPointerSmokeAsync(
                                Path.Combine(Path.GetTempPath(), "post-dispose.oriented-box-smoke.txt"))
                            .GetAwaiter()
                            .GetResult();
                    }
                    catch (Exception exception)
                    {
                        lines.Add($"INFO | post-dispose OrientedBox3D Smoke exception | {exception.GetType().Name}: {exception.Message}");
                    }

                    Check(
                        "post-dispose OrientedBox3D pointer Smoke is rejected",
                        orientedBoxSmokeRejected,
                        $"rejected={orientedBoxSmokeRejected}");

                    var teachingCaptureSmokeRejected = false;
                    try
                    {
                        teachingCaptureSmokeRejected = !control.RunTeachingCapturePointerSmokeAsync(
                                cancelWhenReady: false,
                                reportPath: Path.Combine(Path.GetTempPath(), "post-dispose.teaching-capture-smoke.txt"),
                                exerciseNavigationGestures: false)
                            .GetAwaiter()
                            .GetResult();
                    }
                    catch (Exception exception)
                    {
                        lines.Add($"INFO | post-dispose teaching-capture Smoke exception | {exception.GetType().Name}: {exception.Message}");
                    }

                    Check(
                        "post-dispose teaching-capture pointer Smoke is rejected",
                        teachingCaptureSmokeRejected,
                        $"rejected={teachingCaptureSmokeRejected}");

                    var teachingRectangleSmokeRejected = false;
                    try
                    {
                        teachingRectangleSmokeRejected = !control.RunTeachingRectangleDragPointerSmokeAsync(
                                reportPath: null)
                            .GetAwaiter()
                            .GetResult();
                    }
                    catch (Exception exception)
                    {
                        lines.Add($"INFO | post-dispose teaching rectangle Smoke exception | {exception.GetType().Name}: {exception.Message}");
                    }

                    Check(
                        "post-dispose teaching rectangle pointer Smoke is rejected",
                        teachingRectangleSmokeRejected,
                        $"rejected={teachingRectangleSmokeRejected}");

                    var teachingTargetRectangleSmokeRejected = false;
                    try
                    {
                        var targetSmoke = control.RunTeachingTargetRectanglePointerSmokeAsync(
                                new ToolRecipeGridRectangle(0, 0, 1, 1),
                                reportPath: null)
                            .GetAwaiter()
                            .GetResult();
                        teachingTargetRectangleSmokeRejected = !targetSmoke.Passed;
                    }
                    catch (Exception exception)
                    {
                        lines.Add($"INFO | post-dispose target teaching rectangle Smoke exception | {exception.GetType().Name}: {exception.Message}");
                    }

                    Check(
                        "post-dispose target teaching rectangle pointer Smoke is rejected",
                        teachingTargetRectangleSmokeRejected,
                        $"rejected={teachingTargetRectangleSmokeRejected}");

                    Check(
                        "post-dispose Preview/Publish is rejected",
                        !control.PublishCurrentPreviewResult(),
                        "published=False");
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            lines.Add($"FAIL | verifier exception | {failure.GetType().Name}: {failure.Message}");
        }

        var succeeded = failure is null && passed == total && total > 0;
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"ViewerControlLifetime|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }
}
