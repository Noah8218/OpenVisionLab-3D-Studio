using System.ComponentModel;
using System.IO;
using System.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
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
            "Scope: managed disposal/callback boundary plus isolated DIB active-context timer lifetime; no desktop Window or native navigation"
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

                    var workerCaptureException = CaptureWorkerException(
                        () => control.CaptureCameraState());
                    var workerMutationException = CaptureWorkerException(
                        () => control.TrySetSelectionMode("Point"));
                    var workerAsyncException = CaptureWorkerException(
                        () => control.LoadC3DSourceAsync(
                            "worker-thread.C3D",
                            CancellationToken.None)
                            .GetAwaiter()
                            .GetResult());
                    var workerEventException = CaptureWorkerException(
                        () => control.HostStateChanged += (_, _) => { });
                    var workerExceptions = new[]
                    {
                        workerCaptureException,
                        workerMutationException,
                        workerAsyncException,
                        workerEventException
                    };
                    var workerErrorContract = workerExceptions.All(exception =>
                        exception is InvalidOperationException
                        && exception.Message.Contains(
                            "must be called on the Viewer Dispatcher thread",
                            StringComparison.Ordinal));
                    Check(
                        "Host API worker calls fail with one Dispatcher contract",
                        workerErrorContract,
                        string.Join(
                            "|",
                            workerExceptions.Select(exception =>
                                $"{exception?.GetType().Name ?? "none"}:{exception?.Message ?? "none"}")));

                    var versionCompatibility = ViewerHostContract.IsCompatibleVersion(
                            "1.1",
                            control.HostApiVersion)
                        && ViewerHostContract.IsCompatibleVersion("1.1", "1.2")
                        && !ViewerHostContract.IsCompatibleVersion("1.1", "1.0")
                        && !ViewerHostContract.IsCompatibleVersion("1.1", "2.0");
                    Check(
                        "Host API version policy accepts additive minor versions and rejects mismatches",
                        versionCompatibility,
                        $"current={control.HostApiVersion}|compatibleMinor={ViewerHostContract.IsCompatibleVersion("1.1", "1.2")}|older={ViewerHostContract.IsCompatibleVersion("1.1", "1.0")}|major={ViewerHostContract.IsCompatibleVersion("1.1", "2.0")}");

                    var hostStateNotificationCount = 0;
                    var eventRaisedOnDispatcher = true;
                    ViewerHostStateChangedEventArgs? latestHostStateNotification = null;
                    EventHandler<ViewerHostStateChangedEventArgs> hostStateChanged = (_, args) =>
                    {
                        hostStateNotificationCount++;
                        eventRaisedOnDispatcher &= control.Dispatcher.CheckAccess();
                        latestHostStateNotification = args;
                    };
                    var postDisposeHostStateNotificationCount = 0;
                    EventHandler<ViewerHostStateChangedEventArgs> postDisposeHostStateChanged = (_, _) =>
                        postDisposeHostStateNotificationCount++;
                    control.HostStateChanged += hostStateChanged;
                    control.HostStateChanged += postDisposeHostStateChanged;
                    var selectionHostUpdateSucceeded = control.TrySetSelectionMode("Box ROI");
                    Check(
                        "HostState dependency property follows selection notification",
                        selectionHostUpdateSucceeded
                        && control.HostState.Selection.SelectionMode == "Box ROI"
                        && control.HostState.Selection.Summary == "Box ROI: viewer state only"
                        && hostStateNotificationCount > 0
                        && eventRaisedOnDispatcher
                        && latestHostStateNotification is not null
                        && latestHostStateNotification.State == control.HostState,
                        $"selection={control.HostState.Selection.SelectionMode}|summary={control.HostState.Selection.Summary}|notifications={hostStateNotificationCount}|eventOnDispatcher={eventRaisedOnDispatcher}|eventMatchesProperty={latestHostStateNotification?.State == control.HostState}");
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

                    var postDisposeNotificationCountBeforeMutation = postDisposeHostStateNotificationCount;
                    control.ViewModel.SelectedSelectionMode = "Point";
                    Check(
                        "HostStateChanged is suppressed after Dispose",
                        postDisposeHostStateNotificationCount == postDisposeNotificationCountBeforeMutation,
                        $"before={postDisposeNotificationCountBeforeMutation}|after={postDisposeHostStateNotificationCount}");
                    control.HostStateChanged -= postDisposeHostStateChanged;

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
                    VerifyActiveRenderLifetime(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, Check);
                    VerifySourceAutoFitLayout(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, Check);
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


    private static void VerifySourceAutoFitLayout(string root, Action<string, bool, string> check)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "fit29.C3D");
        using (var stream = new BinaryWriter(File.Create(path)))
        {
            stream.Write(16);
            stream.Write(8);
            for (var index = 0; index < 128; index++) stream.Write(1000f + index * 2f);
        }
        var grid = OpenVisionLab.ThreeD.Data.C3DHeightGrid.Load(path);
        var positions = grid.Points.Select(point => point.Position).ToArray();
        using var control = new OpenVisionThreeDViewerControl(loadDefaultSamples: false) { SidePanelsVisible = false };
        var viewport = (SharpGL.WPF.OpenGLControl)control.FindName("Viewport");
        var sourceHost = new System.Windows.Controls.Grid();
        sourceHost.Children.Add(control);
        void Arrange(double width, double height)
        {
            sourceHost.Measure(new System.Windows.Size(width, height));
            sourceHost.Arrange(new System.Windows.Rect(0, 0, width, height));
            sourceHost.UpdateLayout();
        }
        double ExpectedDistance()
        {
            var state = control.CaptureCameraState();
            return OpenVisionLab.ThreeD.Viewer.Rendering.CameraMath.FitPositions(positions, state.YawDegrees, state.PitchDegrees,
                OpenVisionLab.ThreeD.Viewer.Rendering.CameraMath.DefaultFieldOfViewDegrees, viewport.ActualWidth / viewport.ActualHeight).Distance;
        }
        void CheckFit(string name)
        {
            var actual = control.CaptureCameraState().Distance;
            var expected = ExpectedDistance();
            check(name, Math.Abs(actual - expected) < 0.00001, $"actual={actual:R};expected={expected:R};viewport={viewport.ActualWidth}x{viewport.ActualHeight};Windows=0");
        }
        Arrange(950, 340);
        sourceHost.Visibility = System.Windows.Visibility.Collapsed;
        control.LoadC3DSource(path);
        var hiddenInitial = control.CaptureCameraState();
        PumpRenderDispatcher();
        sourceHost.Visibility = System.Windows.Visibility.Visible;
        Arrange(300, 600);
        check("hidden source load has distinct reopened aspect expectation", Math.Abs(hiddenInitial.Distance - ExpectedDistance()) > 1,
            $"initial={hiddenInitial.Distance:R};expected={ExpectedDistance():R};host={sourceHost.Visibility};viewport={viewport.ActualWidth}x{viewport.ActualHeight};Windows=0");
        PumpRenderDispatcher();
        CheckFit("hidden source load fits first reopened narrower viewport");
        Arrange(950, 340);
        sourceHost.Visibility = System.Windows.Visibility.Collapsed;
        control.ShowC3DWorkbenchResult(path, "hidden workbench source");
        hiddenInitial = control.CaptureCameraState();
        PumpRenderDispatcher();
        sourceHost.Visibility = System.Windows.Visibility.Visible;
        Arrange(300, 600);
        check("hidden workbench load has distinct reopened aspect expectation", Math.Abs(hiddenInitial.Distance - ExpectedDistance()) > 1,
            $"initial={hiddenInitial.Distance:R};expected={ExpectedDistance():R};host={sourceHost.Visibility};viewport={viewport.ActualWidth}x{viewport.ActualHeight};Windows=0");
        PumpRenderDispatcher();
        CheckFit("hidden workbench source fits first reopened narrower viewport");
        bool HasPendingLayout(OpenVisionThreeDViewerControl viewer) => typeof(OpenVisionThreeDViewerControl)
            .GetField("pendingSourceAutoFitLayoutUpdated", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(viewer) is not null;
        check("first reopened source releases its one-shot layout wait", !HasPendingLayout(control), "No wait remains for ordinary resize");
        sourceHost.Visibility = System.Windows.Visibility.Collapsed;
        control.ShowC3DWorkbenchResult(path, "hidden unchanged aspect");
        var sameAspect = control.CaptureCameraState();
        PumpRenderDispatcher();
        check("hidden source retains wait after initial Loaded callback", HasPendingLayout(control), "Actual collapsed visual ancestor; Windows0 does not claim IsVisible");
        sourceHost.Visibility = System.Windows.Visibility.Visible;
        Arrange(300, 600);
        PumpRenderDispatcher();
        check("unchanged aspect reopening releases wait without camera mutation", !HasPendingLayout(control) && control.CaptureCameraState() == sameAspect, "single source completion");
        Arrange(950, 340);
        PumpRenderDispatcher();
        check("ordinary resize after hidden source completion preserves camera", control.CaptureCameraState() == sameAspect, "No global resize refit");
        sourceHost.Visibility = System.Windows.Visibility.Collapsed;
        control.ShowC3DWorkbenchResult(path, "hidden user override");
        var hiddenEdited = control.CaptureCameraState() with { Distance = 37, TargetX = 1.25 };
        control.TryApplyCameraState(hiddenEdited);
        PumpRenderDispatcher();
        sourceHost.Visibility = System.Windows.Visibility.Visible;
        Arrange(300, 600);
        PumpRenderDispatcher();
        check("hidden source wait cannot replace user camera", control.CaptureCameraState() == hiddenEdited && !HasPendingLayout(control), "User camera identity ends source wait");
        Arrange(300, 600);
        sourceHost.Visibility = System.Windows.Visibility.Collapsed;
        control.ShowC3DWorkbenchResult(path, "hidden before Viewer-only failure");
        var hiddenImportCamera = control.CaptureCameraState();
        PumpRenderDispatcher();
        var hiddenImport = control.LoadViewerOnlySourceAsync(Path.Combine(root, "missing35.glb"), CancellationToken.None);
        sourceHost.Visibility = System.Windows.Visibility.Visible;
        Arrange(950, 340);
        PumpRenderDispatcher();
        check("hidden Viewer-only failed request cancels wait and keeps prior source camera", hiddenImport.IsCompleted && !hiddenImport.GetAwaiter().GetResult()
            && control.CurrentC3DSourcePath == path && control.CaptureCameraState() == hiddenImportCamera && !HasPendingLayout(control)
            && Math.Abs(hiddenImportCamera.Distance - ExpectedDistance()) > 1, $"initial={hiddenImportCamera.Distance:R};currentAspectExpected={ExpectedDistance():R};no late fit");
        sourceHost.Visibility = System.Windows.Visibility.Collapsed;
        control.ShowC3DWorkbenchResult(path, "hidden before clear");
        PumpRenderDispatcher();
        control.ClearC3DTeachingSource("hidden clear");
        var hiddenClearCamera = control.CaptureCameraState();
        sourceHost.Visibility = System.Windows.Visibility.Visible;
        Arrange(300, 600);
        PumpRenderDispatcher();
        check("clear releases hidden layout wait without late source camera", !HasPendingLayout(control) && control.CurrentC3DSourcePath is null
            && control.CaptureCameraState() == hiddenClearCamera, "no retained source handler");
        Arrange(300, 600);
        check("source auto-fit load returns synchronously", control.LoadC3DSource(path), "public source load, real C3D");
        var initial = control.CaptureCameraState();
        Arrange(950, 340);
        check("source grow case has distinct old and settled fit", Math.Abs(initial.Distance - ExpectedDistance()) > 1, $"old={initial.Distance:R};settled={ExpectedDistance():R}");
        PumpRenderDispatcher();
        CheckFit("source automatic fit uses settled wider viewport");
        Arrange(950, 340);
        check("workbench result returns synchronously", control.ShowC3DWorkbenchResult(path, "fit29"), "public result load");
        Arrange(300, 600);
        PumpRenderDispatcher();
        CheckFit("workbench automatic fit uses settled narrower viewport");
        control.ShowC3DWorkbenchResult(path, "duplicate1");
        control.ShowC3DWorkbenchResult(path, "duplicate2");
        Arrange(950, 340);
        PumpRenderDispatcher();
        CheckFit("duplicate same path keeps latest settled source fit");
        var settled = control.CaptureCameraState();
        Arrange(300, 600);
        PumpRenderDispatcher();
        check("ordinary resize after source completion preserves camera", control.CaptureCameraState() == settled, "no global resize refit");
        control.ShowC3DWorkbenchResult(path, "camera override");
        var edited = control.CaptureCameraState() with { Distance = 37, TargetX = 1.25 };
        check("user camera mutation accepted", control.TryApplyCameraState(edited), "public camera API");
        Arrange(950, 340);
        PumpRenderDispatcher();
        check("pending source fit preserves user camera mutation", control.CaptureCameraState() == edited, $"camera={control.CaptureCameraState()}");
        control.ShowC3DWorkbenchResult(path, "unchanged aspect");
        var unchanged = control.CaptureCameraState();
        PumpRenderDispatcher();
        check("unchanged viewport preserves initial synchronous fit", control.CaptureCameraState() == unchanged, "no second fit required");
        var replacement = Path.Combine(root, "fit29-replacement.C3D");
        using (var stream = new BinaryWriter(File.Create(replacement)))
        {
            stream.Write(32);
            stream.Write(4);
            for (var index = 0; index < 128; index++) stream.Write(2000f + index * 50f);
        }
        sourceHost.Visibility = System.Windows.Visibility.Collapsed;
        control.ShowC3DWorkbenchResult(path, "old source");
        control.ShowC3DWorkbenchResult(replacement, "new source");
        positions = OpenVisionLab.ThreeD.Data.C3DHeightGrid.Load(replacement).Points.Select(point => point.Position).ToArray();
        PumpRenderDispatcher();
        sourceHost.Visibility = System.Windows.Visibility.Visible;
        Arrange(300, 600);
        PumpRenderDispatcher();
        CheckFit("rapid source switch fits latest source only");
        check("rapid hidden replacement releases old source wait", !HasPendingLayout(control), "latest source only; no retained event");
        check("rapid source switch preserves latest path", control.CurrentC3DSourcePath == replacement, $"path={control.CurrentC3DSourcePath}");
        positions = grid.Points.Select(point => point.Position).ToArray();
        control.ShowC3DWorkbenchResult(path, "transient unload");
        control.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.UnloadedEvent));
        Arrange(950, 340);
        control.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.LoadedEvent));
        PumpRenderDispatcher();
        CheckFit("transient routed unload reload retains pending source fit");
        Arrange(300, 600);
        control.ShowC3DWorkbenchResult(path, "pending before import failure");
        var retainedCamera = control.CaptureCameraState();
        var failedImport = control.LoadViewerOnlySourceAsync(Path.Combine(root, "missing29.glb"), CancellationToken.None);
        check("Viewer-only missing input returns failure synchronously", failedImport.IsCompleted && !failedImport.GetAwaiter().GetResult(), "missing owned GLB; no source mutation");
        Arrange(950, 340);
        PumpRenderDispatcher();
        check("Viewer-only cancellation case has distinct initial and settled fit", Math.Abs(retainedCamera.Distance - ExpectedDistance()) > 1, $"initial={retainedCamera.Distance:R};settled={ExpectedDistance():R}");
        check("Viewer-only failed request cancels older C3D fit and retains camera", control.CurrentC3DSourcePath == path
            && control.CaptureCameraState() == retainedCamera, $"path={control.CurrentC3DSourcePath};camera={control.CaptureCameraState()}");
        check("C3D source recovers after Viewer-only failure", control.LoadC3DSource(path), "valid public load after missing input");
        Arrange(300, 600);
        PumpRenderDispatcher();
        CheckFit("recovered C3D uses settled viewport after import failure");
        control.ShowC3DWorkbenchResult(path, "clear pending");
        control.ClearC3DTeachingSource("fit29 clear");
        var cleared = control.CaptureCameraState();
        Arrange(950, 340);
        PumpRenderDispatcher();
        check("clear cancels pending fit and retains empty scene", control.CurrentC3DSourcePath is null && control.CaptureCameraState() == cleared, $"path={control.CurrentC3DSourcePath}");
        sourceHost.Visibility = System.Windows.Visibility.Collapsed;
        control.ShowC3DWorkbenchResult(path, "dispose pending");
        PumpRenderDispatcher();
        check("hidden disposal begins with a live layout wait", HasPendingLayout(control), "non-vacuous cancellation");
        control.Dispose();
        PumpRenderDispatcher();
        check("dispose cancels pending source fit", control.IsDisposed && !control.HasManagedDataReferences && !HasPendingLayout(control), "Dispatcher pumped after disposal");
        using var workerControl = new OpenVisionThreeDViewerControl(loadDefaultSamples: false) { SidePanelsVisible = false };
        workerControl.Measure(new System.Windows.Size(300, 600));
        workerControl.Arrange(new System.Windows.Rect(0, 0, 300, 600));
        workerControl.UpdateLayout();
        workerControl.Visibility = System.Windows.Visibility.Collapsed;
        workerControl.LoadC3DSource(path);
        PumpRenderDispatcher();
        check("worker disposal begins with a hidden source wait", HasPendingLayout(workerControl), "non-vacuous pending event");
        var workerDispose = Task.Run(workerControl.Dispose);
        var disposeWait = System.Diagnostics.Stopwatch.StartNew();
        while (!workerDispose.IsCompleted && disposeWait.Elapsed < TimeSpan.FromSeconds(5)) PumpRenderDispatcher();
        if (!workerDispose.IsCompleted) throw new TimeoutException("Off-Dispatcher pending-fit disposal exceeded five seconds.");
        workerDispose.GetAwaiter().GetResult();
        PumpRenderDispatcher();
        check("off-Dispatcher dispose releases pending source fit safely", workerControl.IsDisposed && !workerControl.HasManagedDataReferences && !HasPendingLayout(workerControl),
            "existing Dispatcher.Invoke disposal contract; pump after worker completion; Windows=0");
        var app = System.Windows.Application.Current ?? new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new global::Wpf.Ui.Markup.ThemesDictionary { Theme = global::Wpf.Ui.Appearance.ApplicationTheme.Dark });
        app.Resources.MergedDictionaries.Add(new global::Wpf.Ui.Markup.ControlsDictionary());
        app.Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary { Source = new Uri("pack://application:,,,/OpenVisionLab.ThreeD.Shell;component/Themes/OpenVisionThreeDTheme.xaml") });
        var binding = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(path);
        var recipeSource = new ToolRecipeSource("fit.source", "Fit source", "C3D", "raw-height", "fit.frame", path,
            new FileInfo(path).Length, binding.ContentSha256, binding.GridWidth, binding.GridHeight);
        var reference = new ToolRecipeSelection("fit.reference", "Reference", ToolRecipeSelectionKinds.GridRectangle,
            recipeSource.Id, recipeSource.FrameId, binding, new ToolRecipeGridRectangle(0, 0, 8, 8), null, null);
        var measurement = new ToolRecipeSelection("fit.measurement", "Measurement", ToolRecipeSelectionKinds.GridRectangle,
            recipeSource.Id, recipeSource.FrameId, binding, new ToolRecipeGridRectangle(0, 8, 8, 8), null, null);
        var step = new ToolRecipeStep("fit.step", "thickness", "Fit fixture Thickness", 3, [recipeSource.Id, reference.Id, measurement.Id], "fit.output",
            [new ToolRecipeParameter("MinimumThickness", "0"), new ToolRecipeParameter("MaximumThickness", "10000"), new ToolRecipeParameter("MinimumValidSampleCount", "1")]);
        var recipePath = Path.Combine(root, "detached-source-fit36.ov3d-recipe.json");
        ToolRecipeDocumentStore.Save(recipePath, new ToolRecipeDocument(ToolRecipeDocument.CurrentSchemaVersion, "Detached fit fixture", recipeSource, [], [step], [reference, measurement]));
        using (var workbench = new OpenVisionLab.ThreeD.Shell.ViewModels.Workbench.ToolWorkbenchViewModel(Path.Combine(root, "detached-fit-recent36.json")))
        using (var detachedViewer = new OpenVisionThreeDViewerControl(loadDefaultSamples: false) { SidePanelsVisible = false })
        {
            check("Shell fit fixture opens source without execution", workbench.TryOpenTeachingRecipe(recipePath, out var openMessage)
                && !workbench.IsValidationSetRunning, openMessage);
            detachedViewer.Measure(new System.Windows.Size(950, 340));
            detachedViewer.Arrange(new System.Windows.Rect(0, 0, 950, 340));
            detachedViewer.UpdateLayout();
            var detachedViewport = (SharpGL.WPF.OpenGLControl)detachedViewer.FindName("Viewport");
            detachedViewer.ShowC3DWorkbenchResult(path, "detached before Section host attachment");
            var detachedCamera = detachedViewer.CaptureCameraState();
            check("positive-size source is visually disconnected before Shell attachment", System.Windows.Media.VisualTreeHelper.GetParent(detachedViewer) is null
                && detachedViewport.ActualWidth > 0 && detachedViewport.ActualHeight > 0, $"viewport={detachedViewport.ActualWidth}x{detachedViewport.ActualHeight};Windows0");
            PumpRenderDispatcher();
            workbench.MainViewerPane.SetKindCommand.Execute(OpenVisionLab.ThreeD.Shell.ViewModels.Workbench.ViewerPaneKind.Section);
            workbench.SplitViewerVerticallyCommand.Execute(null);
            using var shellSurface = new OpenVisionLab.ThreeD.Shell.Views.Workbench.ViewerWorkspaceView { DataContext = workbench, MainViewerContent = detachedViewer };
            shellSurface.Measure(new System.Windows.Size(600, 650));
            shellSurface.Arrange(new System.Windows.Rect(0, 0, 600, 650));
            shellSurface.UpdateLayout();
            PumpRenderDispatcher();
            check("actual Shell Section host hides MainThreeDSurface", ((System.Windows.FrameworkElement)shellSurface.FindName("MainThreeDSurface")).Visibility == System.Windows.Visibility.Collapsed
                && workbench.MainViewerPane.IsSection, $"layout={workbench.EffectiveViewerLayout};source={detachedViewer.CurrentC3DSourcePath};pending={HasPendingLayout(detachedViewer)}");
            var logs = workbench.RunLog.Count;
            workbench.MainViewerPane.SetKindCommand.Execute(OpenVisionLab.ThreeD.Shell.ViewModels.Workbench.ViewerPaneKind.ThreeD);
            shellSurface.Measure(new System.Windows.Size(600, 650));
            shellSurface.Arrange(new System.Windows.Rect(0, 0, 600, 650));
            shellSurface.UpdateLayout();
            PumpRenderDispatcher();
            var state = detachedViewer.CaptureCameraState();
            var expected = OpenVisionLab.ThreeD.Viewer.Rendering.CameraMath.FitPositions(grid.Points.Select(point => point.Position).ToArray(), state.YawDegrees, state.PitchDegrees,
                OpenVisionLab.ThreeD.Viewer.Rendering.CameraMath.DefaultFieldOfViewDegrees, detachedViewport.ActualWidth / detachedViewport.ActualHeight).Distance;
            check("Shell reopened 3D has a distinct positive comparison viewport", detachedViewport.ActualWidth > 0 && detachedViewport.ActualHeight > 0
                && Math.Abs(detachedCamera.Distance - expected) > 1, $"before={detachedCamera.Distance:R};expected={expected:R};viewport={detachedViewport.ActualWidth}x{detachedViewport.ActualHeight}");
            check("disconnected source fits actual Shell Section-to-3D comparison viewport", Math.Abs(state.Distance - expected) < 0.00001
                && workbench.MainViewerPane.IsThreeD && ((System.Windows.FrameworkElement)shellSurface.FindName("MainThreeDSurface")).Visibility == System.Windows.Visibility.Visible
                && detachedViewer.CurrentC3DSourcePath == path && workbench.RunLog.Count == logs && !workbench.IsValidationSetRunning,
                $"actual={state.Distance:R};expected={expected:R};viewport={detachedViewport.ActualWidth}x{detachedViewport.ActualHeight};kind={workbench.MainViewerPane.Kind};layout={workbench.EffectiveViewerLayout};Windows0;no execution");
        }
        using var shutdownControl = new OpenVisionThreeDViewerControl(loadDefaultSamples: false) { SidePanelsVisible = false, Visibility = System.Windows.Visibility.Collapsed };
        shutdownControl.LoadC3DSource(path);
        PumpRenderDispatcher();
        check("dispatcher shutdown begins with a hidden source wait", HasPendingLayout(shutdownControl), "Window0; pending Loaded phase completed");
        System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
        shutdownControl.Dispose();
        check("dispatcher shutdown releases hidden fit subscription and source", !HasPendingLayout(shutdownControl) && shutdownControl.IsDisposed
            && !shutdownControl.HasManagedDataReferences, "No late dispatcher callback; no native window");
        check("source-fit verification never creates native windows", (System.Windows.Application.Current?.Windows.Count ?? 0) == 0
            && System.Windows.Application.Current?.MainWindow is null, "Windows=0; MainWindow=null; no Show");
    }

    private static void VerifyActiveRenderLifetime(string root, Action<string, bool, string> check)
    {
        OpenVisionThreeDViewerControl.UseSoftwareRenderingForProcess();
        using var control = new OpenVisionThreeDViewerControl(loadDefaultSamples: false) { SidePanelsVisible = false };
        var viewport = (SharpGL.WPF.OpenGLControl)control.FindName("Viewport");
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
        var timer = (System.Windows.Threading.DispatcherTimer)typeof(SharpGL.WPF.OpenGLControl).GetField("timer", flags)!.GetValue(viewport)!;
        var loaded = typeof(OpenVisionThreeDViewerControl).GetMethod("OnLoaded", flags)!;
        var unloaded = typeof(OpenVisionThreeDViewerControl).GetMethod("OnUnloaded", flags)!;
        check("pre-template Viewer keeps real SharpGL timer inactive", viewport.RenderTrigger == SharpGL.RenderTrigger.Manual && !timer.IsEnabled
            && viewport.OpenGL.RenderContextProvider is null, "actual shipped timer; Windows=0; no native navigation");
        loaded.Invoke(control, [control, new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.LoadedEvent)]);
        check("owner loaded callback without context does not start timer", viewport.RenderTrigger == SharpGL.RenderTrigger.Manual && !timer.IsEnabled,
            "isolated owner callback, not native Loaded/InputHitTest proof");
        viewport.ApplyTemplate();
        control.Measure(new System.Windows.Size(480, 320));
        control.Arrange(new System.Windows.Rect(0, 0, 480, 320));
        control.UpdateLayout();
        var provider = viewport.OpenGL.RenderContextProvider;
        check("late template creates the real DIB context before automatic rendering", provider is SharpGL.RenderContextProviders.DIBSectionRenderContextProvider
            && provider.RenderContextHandle != IntPtr.Zero && !timer.IsEnabled && viewport.ActualWidth >= 2 && viewport.ActualHeight >= 2, $"provider={provider?.GetType().Name};handle={provider?.RenderContextHandle};timer={timer.IsEnabled}");
        var draws = 0;
        viewport.OpenGLDraw += (_, _) => draws++;
        viewport.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.LoadedEvent));
        loaded.Invoke(control, [control, new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.LoadedEvent)]);
        PumpRenderDispatcher();
        check("late ready-context loaded callback restores actual timer draws", viewport.RenderTrigger == SharpGL.RenderTrigger.TimerBased && timer.IsEnabled && draws > 0,
            $"draws={draws};timer={timer.IsEnabled};manually routed Loaded/owner callback; active DIB, not native Shell acceptance");
        var render = new System.Windows.Media.Imaging.RenderTargetBitmap(480, 320, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        render.Render(control);
        var png = new System.Windows.Media.Imaging.PngBitmapEncoder();
        png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(render));
        using (var stream = File.Create(Path.Combine(root, "viewer-active-context21.png"))) png.Save(stream);
        unloaded.Invoke(control, [control, new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.UnloadedEvent)]);
        var beforeUnload = draws;
        PumpRenderDispatcher();
        check("unloaded owner stops active SharpGL timer and further frames", viewport.RenderTrigger == SharpGL.RenderTrigger.Manual && !timer.IsEnabled && draws == beforeUnload,
            $"before={beforeUnload};after={draws};context still owned for reversible reload");
        viewport.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.UnloadedEvent));
        viewport.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.LoadedEvent));
        loaded.Invoke(control, [control, new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.LoadedEvent)]);
        PumpRenderDispatcher();
        check("reloaded live context resumes timer frames", timer.IsEnabled && draws > beforeUnload, $"before={beforeUnload};after={draws};same provider={ReferenceEquals(provider, viewport.OpenGL.RenderContextProvider)}");
        control.Dispose();
        var beforeDispose = draws;
        PumpRenderDispatcher();
        check("disposing active context leaves timer stopped after provider teardown", control.IsDisposed && viewport.RenderTrigger == SharpGL.RenderTrigger.Manual && !timer.IsEnabled && draws == beforeDispose,
            $"before={beforeDispose};after={draws};timer={timer.IsEnabled};Windows=0;no exception swallowing");
    }

    private static void PumpRenderDispatcher()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        var stop = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(80)
        };
        stop.Tick += (_, _) => { stop.Stop(); frame.Continue = false; };
        stop.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    private static Exception? CaptureWorkerException(Action action)
    {
        try
        {
            Task.Run(action).GetAwaiter().GetResult();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }
}
