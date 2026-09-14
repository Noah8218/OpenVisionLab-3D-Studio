using System.ComponentModel;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Viewer.Recipes;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class C3DRoiEditingSessionVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var directory = Path.GetDirectoryName(fullReportPath)!;
        Directory.CreateDirectory(directory);
        var lines = new List<string> { "C3D ROI editing session verification (no Viewer control or desktop window)" };
        var total = 0;
        var passed = 0;
        void Check(string name, bool condition)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name}");
        }

        try
        {
            var sourcePath = Path.Combine(directory, "roi-two-levels.C3D");
            using (var writer = new BinaryWriter(File.Create(sourcePath)))
            {
                writer.Write(32);
                writer.Write(24);
                for (var row = 0; row < 24; row++)
                for (var column = 0; column < 32; column++) writer.Write(column < 16 ? 100f : 300f);
            }

            var originalHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath)));
            var grid = C3DHeightGrid.Load(sourcePath, 10000);
            C3DHeightGrid? current = grid;
            var vm = new MainWindowViewModel();
            var clearCalls = 0;
            var renderCalls = 0;
            var session = new C3DRoiEditingSession(vm, () => current, () => clearCalls++, () => renderCalls++);
            vm.UseC3DSmokeScene();

            Check("new session has no overlay or saved ROI", session.LeftBounds is null && session.RightBounds is null
                && session.CreateCurrentRoiStepRecipe() is null);
            Check("automatic comparison succeeds", session.UpdateRoiStepMeasurement());
            Check("automatic golden point counts", vm.RoiStepLeftPointCount == 108 && vm.RoiStepRightPointCount == 108);
            Check("automatic golden raw height result", vm.RoiStepLeftRawMean == 100 && vm.RoiStepRightRawMean == 300
                && vm.RoiStepRawHeightDelta == 200);
            Check("automatic golden model height result", Math.Abs(vm.RoiStepModelHeightDelta - 0.12) < 1e-7);
            Check("automatic comparison records both centers", session.LeftCenter is { X: < 0 } && session.RightCenter is { X: > 0 });
            Check("measurement only clears conflicting plane overlay", clearCalls == 1 && renderCalls == 0);
            Check("automatic regions satisfy recipe gate", session.ValidateRecipeState(true, out var warning) && warning == "Validation: OK");

            session.Pick(new Vector3(-2, 0, 0));
            Check("first pick waits for right region", session.LeftBounds is not null && session.RightBounds is null
                && vm.RoiStepSelectionMode == "Interactive");
            var firstCenter = session.LeftCenter;
            session.Pick(new Vector3(2, 0, 0));
            Check("second pick completes pair", session.LeftCenter == firstCenter && session.RightBounds is not null);
            Check("interactive golden raw result", vm.RoiStepRawHeightDelta == 200);
            session.Pick(new Vector3(-3, 0, 0));
            Check("third pick starts a fresh pair", session.LeftCenter != firstCenter && session.RightBounds is null);
            session.Pick(new Vector3(3, 0, 0));

            var saved = session.CreateCurrentRoiStepRecipe()!;
            var serialized = JsonSerializer.Serialize(saved);
            var roundTrip = JsonSerializer.Deserialize<HeightDeviationRecipeRoiStep>(serialized)!;
            session.Reset();
            Check("reset clears all rendered ROI values", session.LeftBounds is null && session.RightBounds is null
                && session.LeftCenter is null && session.RightCenter is null);
            session.ApplyRecipeRoiStep(roundTrip);
            Check("recipe restore preserves regions and sample limit", session.CreateCurrentRoiStepRecipe() == saved);
            Check("recipe restore preserves measurement and mode", vm.RoiStepRawHeightDelta == 200 && vm.RoiStepSelectionMode == "Interactive");
            Check("restore has no render or inspection execution", renderCalls == 0 && vm.PreviewToolResult.Status == ResultStatus.NotRun);

            // Wire the same property classification used by the View. Re-entrant notifications
            // must not convert a restored Auto recipe into an interactive editing operation.
            var notificationCount = 0;
            PropertyChangedEventHandler onChanged = (_, args) =>
            {
                var effects = ViewerViewModelPropertyChangePolicy.Classify(args.PropertyName);
                if ((effects & ViewerViewModelPropertyChangeEffects.SyncRecipeRoiParameters) != 0)
                {
                    notificationCount++;
                    session.ApplyEditedParametersFromPropertyChange();
                }
            };
            vm.PropertyChanged += onChanged;
            try
            {
                session.ApplyRecipeRoiStep(saved with { Mode = "Auto", Left = saved.Left with { CenterX = saved.Left.CenterX + 0.2 } });
                Check("restore synchronization suppresses recursive edits", notificationCount > 0 && vm.RecipeRoiMode == "Auto");
                vm.RecipeRoiLeftCenterX += 0.1;
                Check("next user edit still applies after restore", vm.RecipeRoiMode == "Interactive"
                    && vm.ViewerStatus == "Recipe ROI parameters updated");
            }
            finally { vm.PropertyChanged -= onChanged; }

            // A failing binding listener must release the synchronization guard in finally.
            PropertyChangedEventHandler throwingListener = (_, args) =>
            {
                if ((ViewerViewModelPropertyChangePolicy.Classify(args.PropertyName)
                    & ViewerViewModelPropertyChangeEffects.SyncRecipeRoiParameters) != 0)
                    throw new InvalidOperationException("verification listener failure");
            };
            var listenerFailed = false;
            vm.PropertyChanged += throwingListener;
            try { session.ApplyRecipeRoiStep(saved with { MaxSampledPoints = saved.MaxSampledPoints + 2 }); }
            catch (InvalidOperationException ex) when (ex.Message == "verification listener failure") { listenerFailed = true; }
            finally { vm.PropertyChanged -= throwingListener; }
            session.ApplyEditedParametersFromPropertyChange();
            Check("binding exception is propagated and guard recovers", listenerFailed && vm.ViewerStatus == "Recipe ROI parameters updated");

            session.ApplyRecipeRoiStep(saved);
            var beforeAlignment = vm.C3DModelTransform;
            var reference = (session.LeftCenter!.Value + session.RightCenter!.Value) * 0.5f;
            var deltaBefore = vm.RoiStepRawHeightDelta;
            Check("ROI reference alignment succeeds", session.ApplyRoiReferenceAlignment());
            Check("alignment keeps existing translation contract", vm.C3DModelTransform.TranslateX == beforeAlignment.TranslateX - reference.X
                && vm.C3DModelTransform.TranslateY == beforeAlignment.TranslateY - reference.Y
                && vm.C3DModelTransform.TranslateZ == beforeAlignment.TranslateZ - reference.Z);
            Check("alignment preserves raw measurement", vm.RoiStepRawHeightDelta == deltaBefore);
            Check("alignment renders exactly once", renderCalls == 1);
            Check("alignment does not Preview or Publish", vm.PreviewToolResult.Status == ResultStatus.NotRun);

            vm.SetC3DAlignment(ModelTransform.Identity, "verification", "source");
            session.ApplyRecipeRoiStep(saved);
            void SetRegions(HeightDeviationRecipeRoiRegion left, HeightDeviationRecipeRoiRegion right) =>
                vm.SetRecipeRoiStepEdit("Interactive", left.CenterX, left.CenterZ, left.HalfWidth, left.HalfDepth,
                    right.CenterX, right.CenterZ, right.HalfWidth, right.HalfDepth, 10000);
            var normalLeft = saved.Left;
            var normalRight = saved.Right;
            SetRegions(normalLeft with { CenterX = -100 }, normalRight);
            Check("outside left warning is stable", !session.ValidateRecipeState(true, out warning)
                && warning == "Validation warning: left ROI is outside the visible C3D bounds.");
            SetRegions(normalLeft, normalRight with { CenterX = 100 });
            Check("outside right warning is stable", !session.ValidateRecipeState(true, out warning)
                && warning == "Validation warning: right ROI is outside the visible C3D bounds.");
            SetRegions(normalLeft, normalLeft);
            Check("overlap warning is stable", !session.ValidateRecipeState(true, out warning)
                && warning == "Validation warning: left and right ROI regions overlap.");
            SetRegions(normalLeft with { HalfWidth = 0.0001, HalfDepth = 0.0001 }, normalRight);
            Check("sparse left warning is stable", !session.ValidateRecipeState(true, out warning)
                && warning == "Validation warning: left ROI has too few C3D samples.");
            SetRegions(normalLeft, normalRight with { HalfWidth = 0.0001, HalfDepth = 0.0001 });
            Check("sparse right warning is stable", !session.ValidateRecipeState(true, out warning)
                && warning == "Validation warning: right ROI has too few C3D samples.");
            SetRegions(normalLeft, normalRight);

            var flatness = vm.CreatePlaneFlatnessRecipeStep() with { ReferenceRegion = normalLeft, Tolerance = 1 };
            vm.SetPlaneFlatnessRecipeStep(flatness);
            Check("flatness reference gate accepts sufficient points", session.ValidatePlaneFlatnessRecipeState(out warning));
            vm.SetPlaneFlatnessRecipeStep(flatness with { Tolerance = 0 });
            Check("invalid flatness tolerance warning is stable", !session.ValidatePlaneFlatnessRecipeState(out warning)
                && warning == "Validation warning: flatness reference ROI and tolerance must be finite and positive.");
            vm.SetPlaneFlatnessRecipeStep(flatness with { ReferenceRegion = normalLeft with { CenterX = -100 } });
            Check("missing flatness samples warning is stable", !session.ValidatePlaneFlatnessRecipeState(out warning)
                && warning == "Validation warning: flatness reference ROI contains fewer than three C3D samples.");
            vm.SetPlaneFlatnessRecipeStep(flatness);

            session.ApplyRecipeRoiStep(saved);
            var gap = vm.CreateGapFlushRecipeStep();
            session.ApplyGapFlushPreviewOverlay(gap, new GapFlushRegionStats(20, 100, 1), new GapFlushRegionStats(20, 300, 2));
            Check("Gap/Flush shares ROI outlines with evaluated heights", session.LeftBounds?.MeanY == 1 && session.RightBounds?.MeanY == 2
                && session.LeftCenter?.Y == 1 && session.RightCenter?.Y == 2);
            var leftCenterBeforeVolume = session.LeftCenter;
            session.ApplyVolumeOverlay(normalLeft, 4);
            Check("Volume replaces left outline and preserves legacy centers", session.LeftBounds?.MeanY == 4
                && session.RightBounds is null && session.LeftCenter == leftCenterBeforeVolume);
            session.ClearOverlay();
            Check("overlay clearing clears both centers and outlines", session.LeftBounds is null && session.RightBounds is null
                && session.LeftCenter is null && session.RightCenter is null);

            session.Reset();
            session.UpdateRoiStepMeasurement();
            Check("reset removes interactive and restored regions", vm.RoiStepSelectionMode == "Auto" && vm.RoiStepLeftPointCount == 108);
            var overlayBeforeNullRecipe = session.LeftBounds;
            session.ApplyRecipeRoiStep(null);
            Check("null recipe only clears stored regions as before", session.LeftBounds == overlayBeforeNullRecipe);
            session.ApplySmokeInteractiveRoiStepMeasurement();
            Check("interactive smoke routes through session", vm.RoiStepSelectionMode == "Interactive"
                && vm.ViewerStatus == "Smoke measure: interactive ROI step-height comparison");
            session.ApplySmokeRoiStepMeasurement();
            Check("automatic smoke resets interactive anchors", vm.RoiStepSelectionMode == "Auto" && vm.RoiStepLeftPointCount == 108);

            current = null;
            Check("source getter observes replacement", !session.UpdateRoiStepMeasurement()
                && session.LeftBounds is null && session.RightBounds is null);
            Check("missing grid validation warning is stable", !session.ValidateRecipeState(true, out warning)
                && warning == "Validation warning: ROI validation requires a visible C3D height grid.");
            Check("non-ROI recipe only validates transform", session.ValidateRecipeState(false, out warning));
            Check("missing flatness grid warning is stable", !session.ValidatePlaneFlatnessRecipeState(out warning)
                && warning == "Validation warning: plane flatness requires a visible C3D height grid.");
            var beforeFailedRender = renderCalls;
            Check("invalid alignment fails without rendering", !session.ApplyRoiReferenceAlignment() && renderCalls == beforeFailedRender);
            session.ApplySmokeRoiStepMeasurement();
            Check("missing smoke source retains warning", vm.ViewerStatus == "Smoke measure failed: C3D sample missing");
            current = grid;
            session.ApplyRecipeRoiStep(saved);
            Check("source restoration uses the same session", vm.RoiStepRawHeightDelta == 200 && session.RightBounds is not null);
            Check("source data remains identical", originalHash == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath)))
                && ReferenceEquals(current, grid));
        }
        catch (Exception exception)
        {
            Check("verification completed without unexpected exception", false);
            lines.Add(exception.ToString());
        }

        summary = $"C3D ROI editing session verification: {passed}/{total} passed";
        lines.Add(summary);
        File.WriteAllLines(fullReportPath, lines);
        return passed == total;
    }
}
