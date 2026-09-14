using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Viewer.Automation;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class ViewerSmokeScenarioVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var lines = new List<string> { "Viewer Smoke scenario ownership verification (no Window/OpenGL)" };
        var passed = 0;
        var total = 0;
        void Check(string name, bool condition)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")}|{name}");
        }

        try
        {
            VerifyAsync(Check).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            Check("unexpected exception", false);
            lines.Add(exception.ToString());
        }

        summary = $"Viewer Smoke scenario: {passed}/{total} checks passed.";
        lines.Add(summary);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        return passed == total;
    }

    private static async Task VerifyAsync(Action<string, bool> check)
    {
        using (var fixture = new Fixture())
        {
            fixture.Runner.Configure([]);
            check("empty options do not render, execute or save", fixture.Host.Calls.SequenceEqual(["teaching-options"]));
            check("initial exit and screenshot state", fixture.Runner.ExitCode == 0 && fixture.Runner.ScreenshotPath is null);
        }

        foreach (var value in new[] { "15", "201", "bad", "" })
        {
            using var fixture = new Fixture();
            fixture.Runner.Configure(value.Length == 0 ? ["--smoke-render-frames"] : ["--smoke-render-frames", value]);
            check($"invalid render frames {value}", fixture.Runner.RenderFrameCount == 0 && fixture.Runner.ExitCode == 1
                && fixture.Host.ViewerStatus == "Smoke render frames must be an integer from 16 through 200.");
        }
        foreach (var value in new[] { "16", "200" })
        {
            using var fixture = new Fixture();
            fixture.Runner.Configure(["--smoke-render-frames", value]);
            check($"accepted render frames {value}", fixture.Runner.RenderFrameCount == int.Parse(value) && fixture.Runner.ExitCode == 0);
        }

        using (var fixture = new Fixture())
        {
            fixture.Runner.Configure([
                "--smoke-screenshot", "first.png", "--smoke-screenshot", "second.png",
                "--smoke-screenshot-quality-report", "quality.txt", "--SMOKE-INTERACTION-LOD",
                "--smoke-next-density", "Detailed", "--smoke-pick", "C3D",
                "--smoke-laz-progress-screenshot", "progress.png", "--smoke-pointer-input-report", "pointer.txt"]);
            check("first valued option and normalized pick", fixture.Runner.ScreenshotPath == "first.png" && fixture.Runner.PickTarget == "c3d");
            check("retained capture and density options", fixture.Runner.InteractionLodRequested
                && fixture.Runner.NextRenderDensity == "Detailed" && fixture.Runner.PointerInputReportPath == "pointer.txt"
                && fixture.Runner.LazProgressScreenshotPath == "progress.png");
            await fixture.Runner.CaptureConfiguredSmokeViewAsync();
            check("capture adapter receives configured paths", fixture.Host.Calls.Contains("capture:first.png:quality.txt"));
            check("direct consumer capture does not shut down", !fixture.Host.Calls.Any(call => call.StartsWith("shutdown:")));
        }

        using (var fixture = new Fixture())
        {
            fixture.Runner.Configure(["--SMOKE-SCREENSHOT", "ignored.png", "--smoke-screenshot"]);
            check("valued flag case and missing value remain unchanged", fixture.Runner.ScreenshotPath is null);
        }

        using (var fixture = new Fixture())
        {
            fixture.Runner.Configure(["--smoke-c3d", "--smoke-glb", "--smoke-stl", "mesh.stl",
                "--smoke-laz", "--smoke-laz-points", "points.laz", "--smoke-recipe", "recipe.json",
                "--smoke-selection", "two-point", "--smoke-measure", "laz-two-point",
                "--smoke-save-recipe", "output.json", "--smoke-screenshot", "view.png", "--smoke-publish-result"]);
            check("source loading preserves CLI stage order", fixture.Host.Calls.Take(5).SequenceEqual([
                "load:C3D:", "load:Glb:", "load:Stl:mesh.stl", "load:LazMetadata:", "load:LazPoints:points.laz"]));
            check("selection reapplied after recipe", fixture.Host.Calls.Count(call => call == "measure:C3DTwoPoint") == 2
                && fixture.Host.Calls.IndexOf("recipe:recipe.json") > fixture.Host.Calls.IndexOf("measure:C3DTwoPoint"));
            check("explicit measurement route", fixture.Host.Calls.Contains("measure:LazTwoPoint"));
            check("screenshot defers publish and save until run", !fixture.Host.Calls.Contains("publish")
                && !fixture.Host.Calls.Any(call => call.StartsWith("save:")));
        }

        foreach (var (alias, expected) in new[]
        {
            ("width-distance-angle", ViewerSmokeMeasurement.PointPairDimensions),
            ("mesh-distance-height", ViewerSmokeMeasurement.MeshTwoPoint),
            ("step-height", ViewerSmokeMeasurement.RoiStep),
            ("interactive-roi", ViewerSmokeMeasurement.InteractiveRoiStep),
            ("distance-to-plane", ViewerSmokeMeasurement.PlaneReference),
            ("reference-roi-flatness", ViewerSmokeMeasurement.PlaneFlatness),
            ("gapflush", ViewerSmokeMeasurement.GapFlush),
            ("volume", ViewerSmokeMeasurement.Volume),
            ("cross-section-dimensions", ViewerSmokeMeasurement.CrossSection)
        })
        {
            using var fixture = new Fixture();
            fixture.Runner.Configure(["--smoke-measure", alias]);
            check($"measurement alias {alias}", fixture.Host.Calls.Contains($"measure:{expected}"));
        }

        using (var fixture = new Fixture())
        {
            fixture.Runner.Configure(["--smoke-action", "normal-color", "--smoke-tolerance", "0.25",
                "--smoke-flatness-tolerance", "0.5", "--smoke-hud", "details"]);
            check("presentation retains source capability fallback and tolerance options", fixture.Host.SelectedColorMode == "Height"
                && fixture.Host.RecipePeakTolerance == 0.25 && fixture.Host.PlaneFlatnessTolerance == 0.5
                && fixture.Host.HudDetailsVisible);
            check("presentation options do not publish or save", fixture.Host.Calls.SequenceEqual(["teaching-options"]));
        }
        using (var fixture = new Fixture())
        {
            fixture.Runner.Configure(["--smoke-publish-result"]);
            check("explicit standalone publish is preserved", fixture.Host.Calls.SequenceEqual(["teaching-options", "publish"]));
        }
        using (var fixture = new Fixture())
        {
            fixture.Runner.Configure(["--smoke-nominal-actual", "missing"]);
            check("invalid nominal input records failure", fixture.Runner.NominalActualPreviewRequested
                && fixture.Runner.ExitCode == 1 && fixture.Host.ViewerStatus ==
                "Nominal/actual smoke requires <actual.stl> <validation-query.ply> <nominal.stl>.");
        }
        using (var fixture = new Fixture())
        {
            fixture.Host.RecipeSucceeds = false;
            fixture.Runner.Configure(["--smoke-recipe", "rejected.json"]);
            check("recipe apply failure marks exit", fixture.Runner.ExitCode == 1);
        }

        using (var fixture = new Fixture())
        {
            fixture.Runner.Configure(["--smoke-screenshot", "result.png", "--smoke-contracts", "scene.txt",
                "--smoke-reload-imported-mesh-texture", "--smoke-race-laz-density-loads",
                "--smoke-reload-laz-cache", "--smoke-pick", "c3d", "--smoke-publish-result",
                "--smoke-save-recipe", "saved.json", "--smoke-render-frames", "16", "--smoke-interaction-lod"]);
            fixture.Host.HasLazPointCloud = true;
            fixture.Host.Calls.Clear();
            await fixture.Runner.RunAsync();
            var operations = fixture.Host.Calls.Where(call => call != "render" && call != "frame").ToArray();
            check("full scenario stage order", operations.SequenceEqual([
                "load:Glb:" + fixture.Host.GlbSampleSourcePath, "density-race", "reload-laz", "pick",
                "publish", "save:saved.json", "pointer", "delay:900", "reset-performance", "interaction-lod",
                "contracts:scene.txt", "capture:result.png:", "delay:100", "shutdown:0:True"]));
            check("forced frames recorded without native GPU", fixture.Runner.RenderFramesCompleted == 16
                && fixture.Host.Calls.Count(call => call == "frame") == 16);
            check("successful scenario exit", fixture.Runner.ExitCode == 0);
        }

        using (var fixture = new Fixture())
        {
            fixture.Host.PublishSucceeds = false;
            fixture.Host.SaveSucceeds = false;
            fixture.Host.CaptureSucceeds = false;
            fixture.Runner.Configure(["--smoke-screenshot", "bad.png", "--smoke-publish-result", "--smoke-save-recipe", "bad.json"]);
            await fixture.Runner.RunAsync();
            check("failed stages retain failure exit through shutdown", fixture.Host.Calls.Contains("save:bad.json")
                && fixture.Host.Calls.Contains("shutdown:1:True") && fixture.Runner.ExitCode == 1);
            check("screenshot exhaustion message preserved", fixture.Host.ViewerStatus ==
                "Viewer screenshot remained blank or invalid after 3 attempts.");
        }

        foreach (var stopAt in new[] { "render", "density-next", "publish", "save:save.json", "pointer", "capture:stop.png:" })
        {
            using var fixture = new Fixture();
            fixture.Runner.Configure(["--smoke-screenshot", "stop.png", "--smoke-publish-result", "--smoke-save-recipe", "save.json"]);
            fixture.Host.AfterCall = call => { if (call == stopAt) fixture.Cancellation.Cancel(); };
            await fixture.Runner.RunAsync();
            check($"cancellation after {stopAt} prevents shutdown", !fixture.Host.Calls.Any(call => call.StartsWith("shutdown:")));
        }

        using (var fixture = new Fixture())
        {
            fixture.Runner.Configure(["--smoke-contracts", "terminal.txt", "--smoke-screenshot", "unused.png"]);
            fixture.Runner.Dispose();
            fixture.Host.Calls.Clear();
            check("disposed configured capture rejected", !await fixture.Runner.CaptureConfiguredSmokeViewAsync());
            check("disposed consumer still receives terminal contract", fixture.Host.Calls.SequenceEqual(["contracts:terminal.txt"]));
            fixture.Runner.Configure(["--smoke-publish-result"]);
            check("disposed runner cannot restart or reconfigure", !fixture.Runner.Start() && fixture.Host.Calls.Count == 1);
        }

        using (var fixture = new Fixture())
        {
            var render = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Host.RenderWait = render.Task;
            check("first Loaded run starts", fixture.Runner.Start());
            check("duplicate Loaded run rejected while active", !fixture.Runner.Start());
            var completion = fixture.Runner.Completion;
            fixture.Runner.Dispose();
            fixture.Cancellation.Cancel();
            render.SetException(new IOException("late render failure"));
            await completion;
            check("disposed late failure does not project or shut down", fixture.Runner.ExitCode == 0
                && !fixture.Host.Calls.Any(call => call.StartsWith("shutdown:")));
        }
        using (var fixture = new Fixture())
        {
            fixture.Host.RenderWait = Task.FromException(new IOException("render failed"));
            check("asynchronous failure is observed by lifetime", fixture.Runner.Start());
            check("observer projects failure and optional shutdown", fixture.Runner.ExitCode == 1
                && fixture.Host.Calls.Contains("shutdown:1:False")
                && fixture.Host.ViewerStatus == "Viewer Smoke capture failed: render failed");
        }
        using (var fixture = new Fixture())
        {
            fixture.Host.IsDispatcherStopping = true;
            fixture.Host.RenderWait = Task.FromException(new InvalidOperationException("dispatcher closed"));
            await fixture.Runner.RunAsync();
            check("dispatcher shutdown race remains suppressed", fixture.Runner.ExitCode == 0
                && !fixture.Host.Calls.Any(call => call.StartsWith("shutdown:")));
        }
    }

    private sealed class Fixture : IDisposable
    {
        public CancellationTokenSource Cancellation { get; } = new();
        public FakeHost Host { get; }
        public ViewerSmokeScenarioRunner Runner { get; }

        public Fixture()
        {
            Host = new FakeHost();
            Runner = new ViewerSmokeScenarioRunner(Host, Cancellation.Token, (milliseconds, token) =>
            {
                token.ThrowIfCancellationRequested();
                Host.Record($"delay:{milliseconds}");
                return Task.CompletedTask;
            });
        }

        public void Dispose()
        {
            Cancellation.Cancel();
            Runner.Dispose();
            Cancellation.Dispose();
        }
    }

    private sealed class FakeHost : IViewerSmokeHost
    {
        public List<string> Calls { get; } = [];
        public Action<string>? AfterCall { get; set; }
        public Task RenderWait { get; set; } = Task.CompletedTask;
        public bool IsDisposed { get; set; }
        public bool IsDispatcherStopping { get; set; }
        public bool C3DSampleVisible { get; set; }
        public bool GlbSampleVisible { get; set; }
        public string GlbSampleSourcePath { get; set; } = "glb.glb";
        public bool LazSampleVisible { get; set; }
        public bool RoiStepMeasurementVisible { get; set; }
        public NominalActualComparisonState NominalActualState { get; set; }
        public double NominalActualLowerTolerance { get; set; } = 0.1;
        public double NominalActualUpperTolerance { get; set; } = 0.1;
        public double TwoPointDistance { get; set; } = double.NaN;
        public double TwoPointRawHeightDelta { get; set; } = double.NaN;
        public double ViewportFps { get; private set; } = 60;
        public double ViewportDrawMilliseconds { get; private set; } = 2;
        public double RecipeTransformTranslateX { get; set; }
        public double RecipeTransformTranslateY { get; set; }
        public double RecipeRoiLeftCenterX { get; set; }
        public double RecipeRoiLeftCenterZ { get; set; }
        public double RecipeRoiLeftHalfWidth { get; set; } = 1;
        public double RecipeRoiRightCenterX { get; set; }
        public double RecipeRoiRightCenterZ { get; set; }
        public double RecipeRoiRightHalfDepth { get; set; } = 1;
        public double RecipePeakTolerance { get; set; } = 1;
        public double PlaneFlatnessTolerance { get; set; } = 1;
        public double LazTwoPointExpectedDistance { get; set; } = double.NaN;
        public double LazTwoPointExpectedHeightDelta { get; set; } = double.NaN;
        public double LazTwoPointDistanceTolerance { get; set; } = 0.1;
        public double LazTwoPointHeightDeltaTolerance { get; set; } = 0.1;
        private string selectedColorMode = "Height";
        public string SelectedColorMode
        {
            get => selectedColorMode;
            set => selectedColorMode = value.Equals("Normal", StringComparison.OrdinalIgnoreCase)
                ? "Height"
                : value;
        }
        public string SelectedGeometryStyle { get; set; } = "Surface";
        public string SelectedRenderDensity { get; set; } = "Balanced";
        public double PointSize { get; set; } = 1;
        public bool HudDetailsVisible { get; set; }
        public string SelectedEntity { get; set; } = string.Empty;
        public string ViewerStatus { get; set; } = string.Empty;
        public bool HasLazPointCloud { get; set; }
        public bool HasImportedMesh => false;
        public bool HasLazTwoPointMeasurement => true;
        public int ImportedMeshTextureUploads => 2;
        public int ImportedMeshTextureReleases => 1;
        public bool RecipeSucceeds { get; set; } = true;
        public bool SaveSucceeds { get; set; } = true;
        public bool PublishSucceeds { get; set; } = true;
        public bool CaptureSucceeds { get; set; } = true;

        public void Record(string call) { Calls.Add(call); AfterCall?.Invoke(call); }
        public void LoadSource(ViewerSmokeSource source, string? path) => Record($"load:{source}:{path}");
        public void Measure(ViewerSmokeMeasurement measurement) => Record($"measure:{measurement}");
        public bool LoadRecipe(string path) { Record($"recipe:{path}"); return RecipeSucceeds; }
        public bool SaveRecipe(string path) { Record($"save:{path}"); return SaveSucceeds; }
        public bool PublishPreview() { Record("publish"); return PublishSucceeds; }
        public void ApplyEditedRoiParameters() => Record("edit-roi");
        public bool ValidateRoi(out string warning) { warning = "invalid ROI"; return false; }
        public bool AlignRoiReference() { Record("align-roi"); return true; }
        public void ConfigureTeachingPointer(string[] args) => Record("teaching-options");
        public void FitSelection() => Record("fit-selection");
        public void Pan(double deltaX, double deltaY, double deltaZ) => Record("pan");
        public void UseSelectionSmokeScene(string mode) { SelectedEntity = mode; }
        public void UsePointCloudSmokeScene() { SelectedEntity = "Point Cloud"; }
        public void UseC3DHeightDeviationRuleSmokeScene() { SelectedEntity = "C3D Height Deviation Rule"; }
        public void UseResultSmokeScene() { SelectedEntity = "Published Result"; }
        public void ConfigureNominalActualComparison(NominalActualComparisonInput input) => NominalActualState = NominalActualComparisonState.InputsReady;
        public void PreviewNominalActual() => NominalActualState = NominalActualComparisonState.PreviewReady;
        public void ClearNominalActualComparison(string validationIssue) => NominalActualState = NominalActualComparisonState.Failed;
        public void SetRecipeValidationSummary(string summary) { }
        public void SetC3DAlignment(ModelTransform transform, string alignmentName, string referenceName) { }
        public void Render() => Record("render");
        public Task RenderAsync(bool atRenderPriority)
        {
            Record(atRenderPriority ? "frame" : "render");
            ViewportFps = 60;
            ViewportDrawMilliseconds = 2;
            return RenderWait;
        }
        public void ResetRenderPerformance()
        {
            Record("reset-performance");
            ViewportFps = double.NaN;
            ViewportDrawMilliseconds = double.NaN;
        }
        public void BeginInteractionLod() => Record("interaction-lod");
        public Task ApplyDensityRaceAsync() { Record("density-race"); return Task.CompletedTask; }
        public Task ApplyNextDensityAsync() { Record("density-next"); return Task.CompletedTask; }
        public Task ReloadLazPointCloudAsync() { Record("reload-laz"); return Task.CompletedTask; }
        public bool ApplyPick() { Record("pick"); return true; }
        public Task<bool> RunPointerRegressionAsync() { Record("pointer"); return Task.FromResult(true); }
        public void WriteSceneContracts(string path) => Record($"contracts:{path}");
        public Task<bool> CaptureScreenshotAsync(string path, string? qualityReportPath)
        {
            Record($"capture:{path}:{qualityReportPath}");
            return Task.FromResult(CaptureSucceeds);
        }
        public void Shutdown(int exitCode, bool requireApplication) => Record($"shutdown:{exitCode}:{requireApplication}");
    }
}
