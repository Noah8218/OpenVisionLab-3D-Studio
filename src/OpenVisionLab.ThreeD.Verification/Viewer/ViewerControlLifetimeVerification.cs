using System.IO;
using System.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Viewer;

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
                        "host API remains version 1.0",
                        control.HostApiVersion == "1.0",
                        $"api={control.HostApiVersion}");
                    Check(
                        "default Viewer source data is owned before disposal",
                        control.HasManagedDataReferences,
                        $"hasManagedData={control.HasManagedDataReferences}");

                    var cameraState = control.CaptureCameraState();
                    Check(
                        "camera state remains usable before disposal",
                        control.TryApplyCameraState(cameraState),
                        $"yaw={cameraState.YawDegrees:G6}|pitch={cameraState.PitchDegrees:G6}|distance={cameraState.Distance:G6}");

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
