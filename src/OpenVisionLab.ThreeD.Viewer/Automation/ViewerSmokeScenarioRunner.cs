using System.Globalization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Automation;

/// <summary>
/// Owns command-line Smoke setup, scenario order, completion state and capture
/// lifetime. The host performs native operations; ordinary recipe commands do
/// not enter this runner. Preserve CLI ordering and explicit execution points.
/// </summary>
internal sealed class ViewerSmokeScenarioRunner : IDisposable
{
    private readonly IViewerSmokeHost host;
    private readonly ViewerSmokeScenarioActionRouter actionRouter;
    private readonly ViewerSmokeNominalActualConfigurator nominalActualConfigurator;
    private readonly CancellationToken viewerLifetimeToken;
    private readonly Func<int, CancellationToken, Task> delay;
    private readonly ViewerSmokeCaptureLifetime captureLifetime;
    private bool disposed;
    private string? smokeScreenshotPath;
    private string? smokeScreenshotQualityReportPath;
    private string? smokeContractsPath;
    private string? smokePointerInputReportPath;
    private string? smokeSaveRecipePath;
    private bool smokePublishResult;
    private bool smokeNominalActualPreview;
    private bool smokeReloadImportedMeshTexture;
    private bool smokeReloadLazPointCloudCache;
    private bool smokeRaceLazPointCloudDensityLoads;
    private string? smokeLazProgressScreenshotPath;
    private int smokeRenderFrameCount;
    private int smokeRenderFramesCompleted;
    private int smokeExitCode;
    private string? smokePickTarget;
    private string? smokeMeasureMode;
    private string? smokeNextRenderDensity;
    private bool smokeInteractionLodRequested;

    public ViewerSmokeScenarioRunner(IViewerSmokeHost host,
        CancellationToken lifetimeToken, Func<int, CancellationToken, Task>? delay = null)
    {
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        actionRouter = new ViewerSmokeScenarioActionRouter(this.host);
        nominalActualConfigurator = new ViewerSmokeNominalActualConfigurator(this.host);
        viewerLifetimeToken = lifetimeToken;
        this.delay = delay ?? Task.Delay;
        captureLifetime = new ViewerSmokeCaptureLifetime(lifetimeToken, () => IsDisposed, HandleSmokeCaptureFailure);
    }

    private bool IsDisposed => disposed || host.IsDisposed;

    public string? ScreenshotPath => smokeScreenshotPath;
    public string? PointerInputReportPath => smokePointerInputReportPath;
    public string? LazProgressScreenshotPath => smokeLazProgressScreenshotPath;
    public string? PickTarget => smokePickTarget;
    public string? NextRenderDensity => smokeNextRenderDensity;
    public int ExitCode => smokeExitCode;
    public int RenderFrameCount => smokeRenderFrameCount;
    public int RenderFramesCompleted => smokeRenderFramesCompleted;
    public bool NominalActualPreviewRequested => smokeNominalActualPreview;
    public bool RaceLazDensityLoads => smokeRaceLazPointCloudDensityLoads;
    public bool InteractionLodRequested => smokeInteractionLodRequested;
    internal Task Completion => captureLifetime.Completion;

    public void Configure(string[] args)
    {
        if (IsDisposed) return;
        var smokeIndex = Array.IndexOf(args, "--smoke-screenshot");
        if (smokeIndex >= 0 && smokeIndex + 1 < args.Length)
        {
            smokeScreenshotPath = args[smokeIndex + 1];
        }

        var screenshotQualityIndex = Array.IndexOf(args, "--smoke-screenshot-quality-report");
        if (screenshotQualityIndex >= 0 && screenshotQualityIndex + 1 < args.Length)
        {
            smokeScreenshotQualityReportPath = args[screenshotQualityIndex + 1];
        }

        ApplySmokeArguments(args);
    }

    public bool Start() => captureLifetime.Start(RunAsync);

    public void MarkFailed() => smokeExitCode = 1;

    public void RequireNominalActualPreview() => smokeNominalActualPreview = true;

    public void SetFailure(string message)
    {
        smokeExitCode = 1;
        host.ViewerStatus = message;
    }

    public void Dispose()
    {
        disposed = true;
        captureLifetime.Dispose();
    }

    internal async Task RunAsync()
    {
        try
        {
            await host.RenderAsync(atRenderPriority: false);
            if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
            {
                return;
            }

            if (smokeReloadImportedMeshTexture)
            {
                var sourcePath = host.GlbSampleSourcePath;
                host.LoadSource(ViewerSmokeSource.Glb, sourcePath);
                await host.RenderAsync(atRenderPriority: false);
                if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
                {
                    return;
                }

                if (host.ImportedMeshTextureUploads < 2 || host.ImportedMeshTextureReleases < 1)
                {
                    SetFailure(
                        $"Imported mesh texture reload failed: uploads={host.ImportedMeshTextureUploads}, releases={host.ImportedMeshTextureReleases}");
                }
            }

            if (smokeNominalActualPreview
                && !await WaitForNominalActualPreviewAsync(TimeSpan.FromMinutes(10)))
            {
                if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
                {
                    return;
                }

                smokeExitCode = 1;
                if (host.NominalActualState == NominalActualComparisonState.PreviewRunning)
                {
                    host.ViewerStatus = "Nominal/actual Preview timed out before screenshot capture.";
                }

                await host.RenderAsync(atRenderPriority: false);
            }

            if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
            {
                return;
            }

            if (smokeRaceLazPointCloudDensityLoads)
            {
                await host.ApplyDensityRaceAsync();
            }
            else
            {
                await host.ApplyNextDensityAsync();
            }
            if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
            {
                return;
            }

            if (smokeReloadLazPointCloudCache && host.HasLazPointCloud)
            {
                await host.ReloadLazPointCloudAsync();
                if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
                {
                    return;
                }
            }
            await host.RenderAsync(atRenderPriority: false);
            if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
            {
                return;
            }

            if (smokePickTarget is not null)
            {
                host.ApplyPick();
                await host.RenderAsync(atRenderPriority: false);
                if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
                {
                    return;
                }
            }

            if (smokePublishResult)
            {
                if (!host.PublishPreview())
                {
                    smokeExitCode = 1;
                    host.ViewerStatus = "Smoke Publish failed: current Preview evidence is unavailable";
                }

                await host.RenderAsync(atRenderPriority: false);
                if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
                {
                    return;
                }
            }

            if (smokeSaveRecipePath is not null)
            {
                if (!host.SaveRecipe(smokeSaveRecipePath))
                {
                    smokeExitCode = 1;
                }

                await host.RenderAsync(atRenderPriority: false);
                if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
                {
                    return;
                }
            }

            await host.RunPointerRegressionAsync();
            if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
            {
                return;
            }
            await host.RenderAsync(atRenderPriority: false);
            if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
            {
                return;
            }

            await delay(900, viewerLifetimeToken);
            if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
            {
                return;
            }
            await CaptureConfiguredSmokeViewAsync();
            if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
            {
                return;
            }

            await delay(100, viewerLifetimeToken);
            if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
            {
                return;
            }

            host.Shutdown(smokeExitCode, requireApplication: true);
        }
        catch (OperationCanceledException) when (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
        {
            // Control disposal owns cancellation; do not continue smoke work or
            // shut down the host after the View has closed.
        }
        catch (InvalidOperationException) when (IsDisposed || host.IsDispatcherStopping)
        {
            // Dispatcher shutdown can race the final View callback during host close.
        }
    }

    private void HandleSmokeCaptureFailure(Exception exception)
    {
        if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
        {
            return;
        }

        SetFailure($"Viewer Smoke capture failed: {exception.Message}");
        try
        {
            host.Shutdown(smokeExitCode, requireApplication: false);
        }
        catch (InvalidOperationException) when (host.IsDispatcherStopping)
        {
            // Dispatcher shutdown can race the observer's failure projection.
        }
    }

    private async Task<bool> WaitForNominalActualPreviewAsync(TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (host.NominalActualState == NominalActualComparisonState.PreviewRunning
            && DateTimeOffset.UtcNow < deadline)
        {
            await delay(100, viewerLifetimeToken);
        }

        return host.NominalActualState is NominalActualComparisonState.PreviewReady
            or NominalActualComparisonState.Published;
    }

    public async Task<bool> CaptureConfiguredSmokeViewAsync()
    {
        if (IsDisposed)
        {
            // A terminal contract is still useful to an independent consumer
            // that is verifying disposal. Rendering is intentionally rejected,
            // but the contract must describe the disposed control rather than
            // leaving the previous stage's file in place.
            if (smokeContractsPath is not null)
            {
                host.WriteSceneContracts(smokeContractsPath);
            }

            return false;
        }

        if (viewerLifetimeToken.IsCancellationRequested)
        {
            return false;
        }

        try
        {
            await RunConfiguredSmokeRenderFramesAsync();
            if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
            {
                return false;
            }

            if (smokeContractsPath is not null)
            {
                host.WriteSceneContracts(smokeContractsPath);
            }

            if (smokeScreenshotPath is null)
            {
                return smokeExitCode == 0;
            }

            if (!await host.CaptureScreenshotAsync(smokeScreenshotPath, smokeScreenshotQualityReportPath))
            {
                if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
                {
                    return false;
                }

                SetFailure("Viewer screenshot remained blank or invalid after 3 attempts.");
            }

            return !IsDisposed && !viewerLifetimeToken.IsCancellationRequested && smokeExitCode == 0;
        }
        catch (OperationCanceledException) when (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private async Task RunConfiguredSmokeRenderFramesAsync()
    {
        if (smokeRenderFrameCount == 0)
        {
            return;
        }

        host.ResetRenderPerformance();
        smokeRenderFramesCompleted = 0;
        if (smokeInteractionLodRequested)
        {
            host.BeginInteractionLod();
        }

        for (var frame = 0; frame < smokeRenderFrameCount; frame++)
        {
            await host.RenderAsync(atRenderPriority: true);
            if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
            {
                return;
            }

            smokeRenderFramesCompleted++;
        }

        if (smokeMeasureMode is not null)
        {
            actionRouter.ApplyMeasure(smokeMeasureMode);
            await host.RenderAsync(atRenderPriority: true);
            if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
            {
                return;
            }
        }

        if (!double.IsFinite(host.ViewportFps)
            || !double.IsFinite(host.ViewportDrawMilliseconds))
        {
            SetFailure(
                $"Render performance remained pending after {smokeRenderFramesCompleted} forced frames.");
        }
    }

    private void ApplySmokeArguments(string[] args)
    {
        var renderFramesIndex = Array.IndexOf(args, "--smoke-render-frames");
        if (renderFramesIndex >= 0)
        {
            if (renderFramesIndex + 1 >= args.Length
                || !int.TryParse(
                    args[renderFramesIndex + 1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out smokeRenderFrameCount)
                || smokeRenderFrameCount is < 16 or > 200)
            {
                smokeRenderFrameCount = 0;
                SetFailure("Smoke render frames must be an integer from 16 through 200.");
            }
        }

        smokeInteractionLodRequested = args.Contains(
            "--smoke-interaction-lod",
            StringComparer.OrdinalIgnoreCase);
        smokeReloadImportedMeshTexture = args.Contains(
            "--smoke-reload-imported-mesh-texture",
            StringComparer.OrdinalIgnoreCase);
        smokeReloadLazPointCloudCache = args.Contains(
            "--smoke-reload-laz-cache",
            StringComparer.OrdinalIgnoreCase);
        smokeRaceLazPointCloudDensityLoads = args.Contains(
            "--smoke-race-laz-density-loads",
            StringComparer.OrdinalIgnoreCase);

        var lazProgressScreenshotIndex = Array.IndexOf(args, "--smoke-laz-progress-screenshot");
        if (lazProgressScreenshotIndex >= 0 && lazProgressScreenshotIndex + 1 < args.Length)
        {
            smokeLazProgressScreenshotPath = args[lazProgressScreenshotIndex + 1];
        }

        var densityIndex = Array.IndexOf(args, "--smoke-density");
        if (densityIndex >= 0 && densityIndex + 1 < args.Length)
        {
            host.SelectedRenderDensity = args[densityIndex + 1];
        }

        var nextDensityIndex = Array.IndexOf(args, "--smoke-next-density");
        if (nextDensityIndex >= 0 && nextDensityIndex + 1 < args.Length)
        {
            smokeNextRenderDensity = args[nextDensityIndex + 1];
        }

        var pointSizeIndex = Array.IndexOf(args, "--smoke-point-size");
        if (pointSizeIndex >= 0
            && pointSizeIndex + 1 < args.Length
            && double.TryParse(args[pointSizeIndex + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var pointSize))
        {
            host.PointSize = pointSize;
        }

        actionRouter.ApplyTolerance(args);

        var sceneIndex = Array.IndexOf(args, "--smoke-scene");
        if (sceneIndex >= 0 && sceneIndex + 1 < args.Length && args[sceneIndex + 1].Equals("pointcloud", StringComparison.OrdinalIgnoreCase))
        {
            host.UsePointCloudSmokeScene();
        }

        var c3dIndex = Array.IndexOf(args, "--smoke-c3d");
        if (c3dIndex >= 0)
        {
            host.LoadSource(ViewerSmokeSource.C3D, null);
        }

        var glbIndex = Array.IndexOf(args, "--smoke-glb");
        if (glbIndex >= 0)
        {
            var glbPath = glbIndex + 1 < args.Length && !args[glbIndex + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[glbIndex + 1]
                : null;
            host.LoadSource(ViewerSmokeSource.Glb, glbPath);
        }

        var stlIndex = Array.IndexOf(args, "--smoke-stl");
        if (stlIndex >= 0)
        {
            var stlPath = stlIndex + 1 < args.Length && !args[stlIndex + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[stlIndex + 1]
                : null;
            host.LoadSource(ViewerSmokeSource.Stl, stlPath);
        }

        var lazIndex = Array.IndexOf(args, "--smoke-laz");
        if (lazIndex >= 0)
        {
            var lazPath = lazIndex + 1 < args.Length && !args[lazIndex + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[lazIndex + 1]
                : null;
            host.LoadSource(ViewerSmokeSource.LazMetadata, lazPath);
        }

        var lazPointsIndex = Array.IndexOf(args, "--smoke-laz-points");
        if (lazPointsIndex >= 0)
        {
            var lazPath = lazPointsIndex + 1 < args.Length && !args[lazPointsIndex + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[lazPointsIndex + 1]
                : null;
            host.LoadSource(ViewerSmokeSource.LazPoints, lazPath);
        }

        var nominalActualConfiguration = nominalActualConfigurator.Configure(args);
        if (nominalActualConfiguration.Requested)
        {
            smokeNominalActualPreview = true;
            if (nominalActualConfiguration.Failure is not null)
            {
                SetFailure(nominalActualConfiguration.Failure);
            }
        }

        var actionIndex = Array.IndexOf(args, "--smoke-action");
        if (actionIndex >= 0 && actionIndex + 1 < args.Length)
        {
            actionRouter.ApplyAction(args[actionIndex + 1]);
        }

        var selectionIndex = Array.IndexOf(args, "--smoke-selection");
        if (selectionIndex >= 0 && selectionIndex + 1 < args.Length)
        {
            actionRouter.ApplySelection(args[selectionIndex + 1]);
        }

        var overlayIndex = Array.IndexOf(args, "--smoke-overlay");
        if (overlayIndex >= 0 && overlayIndex + 1 < args.Length)
        {
            actionRouter.ApplyOverlay(args[overlayIndex + 1]);
        }

        var ruleIndex = Array.IndexOf(args, "--smoke-rule");
        if (ruleIndex >= 0 && ruleIndex + 1 < args.Length)
        {
            actionRouter.ApplyRule(args[ruleIndex + 1]);
        }

        var recipeIndex = Array.IndexOf(args, "--smoke-recipe");
        if (recipeIndex >= 0 && recipeIndex + 1 < args.Length)
        {
            if (!actionRouter.ApplyRecipe(args[recipeIndex + 1]))
            {
                smokeExitCode = 1;
            }
        }

        actionRouter.ApplyTolerance(args);

        if (recipeIndex >= 0 && selectionIndex >= 0 && selectionIndex + 1 < args.Length)
        {
            actionRouter.ApplySelection(args[selectionIndex + 1]);
        }

        var pickIndex = Array.IndexOf(args, "--smoke-pick");
        if (pickIndex >= 0 && pickIndex + 1 < args.Length)
        {
            smokePickTarget = args[pickIndex + 1].ToLowerInvariant();
        }

        var alignmentIndex = Array.IndexOf(args, "--smoke-alignment");
        if (alignmentIndex >= 0 && alignmentIndex + 1 < args.Length)
        {
            actionRouter.ApplyAlignment(args[alignmentIndex + 1]);
        }

        var measureIndex = Array.IndexOf(args, "--smoke-measure");
        if (measureIndex >= 0 && measureIndex + 1 < args.Length)
        {
            smokeMeasureMode = args[measureIndex + 1];
            actionRouter.ApplyMeasure(smokeMeasureMode);
        }

        var hudIndex = Array.IndexOf(args, "--smoke-hud");
        if (hudIndex >= 0 && hudIndex + 1 < args.Length)
        {
            host.HudDetailsVisible = args[hudIndex + 1].Equals("details", StringComparison.OrdinalIgnoreCase);
        }

        var editParametersIndex = Array.IndexOf(args, "--smoke-edit-parameters");
        if (editParametersIndex >= 0 && editParametersIndex + 1 < args.Length)
        {
            actionRouter.ApplyRecipeParameterEdit(args[editParametersIndex + 1]);
        }

        var invalidRoiIndex = Array.IndexOf(args, "--smoke-invalid-roi");
        if (invalidRoiIndex >= 0 && invalidRoiIndex + 1 < args.Length)
        {
            actionRouter.ApplyInvalidRoi(args[invalidRoiIndex + 1]);
        }

        if (Array.IndexOf(args, "--smoke-align-from-roi") >= 0)
        {
            host.AlignRoiReference();
        }

        var contractsIndex = Array.IndexOf(args, "--smoke-contracts");
        if (contractsIndex >= 0 && contractsIndex + 1 < args.Length)
        {
            smokeContractsPath = args[contractsIndex + 1];
        }

        var pointerInputReportIndex = Array.IndexOf(args, "--smoke-pointer-input-report");
        if (pointerInputReportIndex >= 0 && pointerInputReportIndex + 1 < args.Length)
        {
            smokePointerInputReportPath = args[pointerInputReportIndex + 1];
        }

        var saveRecipeIndex = Array.IndexOf(args, "--smoke-save-recipe");
        if (saveRecipeIndex >= 0 && saveRecipeIndex + 1 < args.Length)
        {
            smokeSaveRecipePath = args[saveRecipeIndex + 1];
        }

        host.ConfigureTeachingPointer(args);

        smokePublishResult = Array.IndexOf(args, "--smoke-publish-result") >= 0;
        if (smokePublishResult && smokeScreenshotPath is null && !smokeNominalActualPreview)
        {
            host.PublishPreview();
        }
    }

}
