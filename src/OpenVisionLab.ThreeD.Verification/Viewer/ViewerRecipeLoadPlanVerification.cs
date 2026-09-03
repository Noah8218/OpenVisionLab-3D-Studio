using System.IO;
using System.Security.Cryptography;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.Recipes;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

public static class ViewerRecipeLoadPlanVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer recipe workflow verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;

        try
        {
            var recipePath = Path.GetFullPath(
                Path.Combine("recipes", "c3d-height-deviation.recipe.json"));
            var recipeFile = ViewerRecipeFile.Open(recipePath);
            var recipe = (OpenVisionLab.ThreeD.Tools.HeightDeviationRecipe)recipeFile.LoadDocument();
            var plan = HeightDeviationRecipeLoadPlan.Create(recipeFile, recipe, maxRenderedPoints: 55000);

            Check("recipe path is normalized", plan.FullRecipePath == recipePath, plan.FullRecipePath);
            Check("source path resolves beside recipe", File.Exists(plan.SourcePath), plan.SourcePath);
            Check("grid remains source-backed", plan.Grid.Width == 1280 && plan.Grid.Height == 840, $"grid={plan.Grid.Width}x{plan.Grid.Height}");
            Check("recipe identity is retained", ReferenceEquals(plan.Recipe, recipe), plan.Recipe.RecipeType);

            var normalViewModel = new OpenVisionLab.ThreeD.Viewer.ViewModels.MainWindowViewModel();
            var sampleStatusCalls = 0;
            var roiApplyCalls = 0;
            var planePreviewCalls = 0;
            var volumePreviewCalls = 0;
            var crossSectionPreviewCalls = 0;
            var appliedNormally = HeightDeviationRecipeApplyCoordinator.Apply(
                plan,
                normalViewModel,
                isSmoke: false,
                applySampleStatus: () => sampleStatusCalls++,
                applyRoiStep: _ => roiApplyCalls++,
                previewPlaneFlatness: () => { planePreviewCalls++; return true; },
                previewVolume: () => { volumePreviewCalls++; return true; },
                previewCrossSection: () => { crossSectionPreviewCalls++; return true; });
            Check(
                "normal apply restores state without Preview",
                appliedNormally
                && sampleStatusCalls == 1
                && roiApplyCalls == 1
                && planePreviewCalls == 0
                && volumePreviewCalls == 0
                && crossSectionPreviewCalls == 0
                && normalViewModel.RecipeSourcePath == plan.SourcePath
                && normalViewModel.PreviewToolResult.Status == OpenVisionLab.ThreeD.Core.ResultStatus.NotRun
                && !normalViewModel.ResultOverlayVisible
                && normalViewModel.ViewerStatus.StartsWith("Recipe loaded:", StringComparison.Ordinal),
                $"applied={appliedNormally}|sampleStatus={sampleStatusCalls}|roi={roiApplyCalls}|preview={normalViewModel.PreviewToolResult.Status}|source={normalViewModel.RecipeSourcePath}");

            var smokeViewModel = new OpenVisionLab.ThreeD.Viewer.ViewModels.MainWindowViewModel();
            var smokeApplied = HeightDeviationRecipeApplyCoordinator.Apply(
                plan,
                smokeViewModel,
                isSmoke: true,
                applySampleStatus: () => { },
                applyRoiStep: _ => { },
                previewPlaneFlatness: () => true,
                previewVolume: () => true,
                previewCrossSection: () => true);
            Check(
                "smoke apply keeps Preview explicit to the smoke path",
                smokeApplied
                && smokeViewModel.PreviewToolResult.Status is not OpenVisionLab.ThreeD.Core.ResultStatus.NotRun
                && smokeViewModel.ResultOverlayVisible
                && smokeViewModel.ViewerStatus.StartsWith("Smoke recipe:", StringComparison.Ordinal),
                $"applied={smokeApplied}|preview={smokeViewModel.PreviewToolResult.Status}|overlay={smokeViewModel.ResultOverlayVisible}");

            var volumeRecipePath = Path.GetFullPath(
                Path.Combine("recipes", "c3d-volume.recipe.json"));
            var volumeRecipeFile = ViewerRecipeFile.Open(volumeRecipePath);
            var volumeRecipe = (OpenVisionLab.ThreeD.Tools.HeightDeviationRecipe)volumeRecipeFile.LoadDocument();
            var volumePlan = HeightDeviationRecipeLoadPlan.Create(
                volumeRecipeFile,
                volumeRecipe,
                maxRenderedPoints: 140000);
            var volumeStep = volumeRecipe.Volume;
            Check(
                "Volume recipe plan resolves the authored dual ROI",
                volumeStep is not null
                && volumePlan.Grid.Width == 1280
                && volumePlan.Grid.Height == 840,
                $"source={volumePlan.SourcePath}|grid={volumePlan.Grid.Width}x{volumePlan.Grid.Height}|volume={volumeStep is not null}");

            var volumeViewModel = new MainWindowViewModel();
            var volumeOverlayCalls = 0;
            var volumeRenderCalls = 0;
            HeightDeviationRecipeVolume? appliedVolumeStep = null;
            VolumeEvaluation? volumeEvaluation = null;
            var volumePreviewed = false;
            var volumeMeanY = double.NaN;
            if (volumeStep is not null)
            {
                volumeViewModel.UseC3DSmokeScene();
                volumeViewModel.SetVolumeRecipeStep(volumeStep);
                volumePreviewed = C3DVolumeRuleCoordinator.Preview(
                    volumePlan.Grid,
                    volumeViewModel,
                    (step, evaluation, meanY) =>
                    {
                        appliedVolumeStep = step;
                        volumeEvaluation = evaluation;
                        volumeMeanY = meanY;
                        volumeOverlayCalls++;
                    },
                    () => volumeRenderCalls++);
            }

            Check(
                "C3D Volume coordinator owns explicit Preview boundary",
                volumePreviewed
                && volumeOverlayCalls == 1
                && volumeRenderCalls == 1
                && appliedVolumeStep is not null
                && volumeEvaluation is not null
                && double.IsFinite(volumeMeanY)
                && volumeViewModel.VolumeVisible
                && volumeViewModel.PreviewToolResult.ToolName == VolumeRule.ToolName
                && volumeViewModel.PreviewToolResult.Status != ResultStatus.NotRun,
                $"preview={volumePreviewed}|overlay={volumeOverlayCalls}|render={volumeRenderCalls}|status={volumeViewModel.PreviewToolResult.Status}|meanY={volumeMeanY:G6}");

            var planeFlatnessRecipePath = Path.GetFullPath(
                Path.Combine("recipes", "c3d-plane-flatness.recipe.json"));
            var planeFlatnessRecipeFile = ViewerRecipeFile.Open(planeFlatnessRecipePath);
            var planeFlatnessRecipe = (OpenVisionLab.ThreeD.Tools.HeightDeviationRecipe)planeFlatnessRecipeFile.LoadDocument();
            var planeFlatnessPlan = HeightDeviationRecipeLoadPlan.Create(
                planeFlatnessRecipeFile,
                planeFlatnessRecipe,
                maxRenderedPoints: 140000);
            var planeFlatnessStep = planeFlatnessRecipe.PlaneFlatness;
            Check(
                "Plane Flatness recipe plan resolves the authored reference ROI",
                planeFlatnessStep is not null
                && planeFlatnessPlan.Grid.Width == 1280
                && planeFlatnessPlan.Grid.Height == 840,
                $"source={planeFlatnessPlan.SourcePath}|grid={planeFlatnessPlan.Grid.Width}x{planeFlatnessPlan.Grid.Height}|flatness={planeFlatnessStep is not null}");

            var planeFlatnessViewModel = new MainWindowViewModel();
            var planeFlatnessOverlayCalls = 0;
            var planeFlatnessRenderCalls = 0;
            PlaneFlatnessEvaluation? planeFlatnessEvaluation = null;
            var planeFlatnessPreviewed = false;
            if (planeFlatnessStep is not null)
            {
                planeFlatnessViewModel.UseC3DSmokeScene();
                planeFlatnessViewModel.SetPlaneFlatnessRecipeStep(planeFlatnessStep);
                planeFlatnessPreviewed = C3DPlaneFlatnessRuleCoordinator.Preview(
                    planeFlatnessPlan.Grid,
                    planeFlatnessViewModel,
                    (_, evaluation) =>
                    {
                        planeFlatnessEvaluation = evaluation;
                        planeFlatnessOverlayCalls++;
                    },
                    () => planeFlatnessRenderCalls++);
            }

            Check(
                "C3D Plane Flatness coordinator owns explicit Preview boundary",
                planeFlatnessPreviewed
                && planeFlatnessOverlayCalls == 1
                && planeFlatnessRenderCalls == 1
                && planeFlatnessEvaluation is not null
                && planeFlatnessEvaluation.ReferencePlane is not null
                && planeFlatnessViewModel.PlaneFlatnessVisible
                && planeFlatnessViewModel.PlaneFlatnessReferenceSampleCount >= 3
                && planeFlatnessViewModel.PlaneFlatnessMeasurementSampleCount >= 3
                && planeFlatnessViewModel.PreviewToolResult.ToolName == PlaneFlatnessRule.ToolName
                && planeFlatnessViewModel.PreviewToolResult.Status != ResultStatus.NotRun,
                $"preview={planeFlatnessPreviewed}|overlay={planeFlatnessOverlayCalls}|render={planeFlatnessRenderCalls}|status={planeFlatnessViewModel.PreviewToolResult.Status}|reference={planeFlatnessViewModel.PlaneFlatnessReferenceSampleCount}|measurement={planeFlatnessViewModel.PlaneFlatnessMeasurementSampleCount}");

            var crossSectionRecipePath = Path.GetFullPath(
                Path.Combine("recipes", "c3d-cross-section-dimensions.recipe.json"));
            var crossSectionRecipeFile = ViewerRecipeFile.Open(crossSectionRecipePath);
            var crossSectionRecipe = (OpenVisionLab.ThreeD.Tools.HeightDeviationRecipe)crossSectionRecipeFile.LoadDocument();
            var crossSectionPlan = HeightDeviationRecipeLoadPlan.Create(
                crossSectionRecipeFile,
                crossSectionRecipe,
                maxRenderedPoints: 140000);
            var crossSectionStep = crossSectionRecipe.CrossSection;
            Check(
                "Cross-section recipe plan resolves the authored row range",
                crossSectionStep is not null
                && crossSectionPlan.Grid.Width == 1280
                && crossSectionPlan.Grid.Height == 840,
                $"source={crossSectionPlan.SourcePath}|grid={crossSectionPlan.Grid.Width}x{crossSectionPlan.Grid.Height}|crossSection={crossSectionStep is not null}");

            var crossSectionViewModel = new MainWindowViewModel();
            var crossSectionProfileCalls = 0;
            var crossSectionRenderCalls = 0;
            HeightGridPoint[]? crossSectionPoints = null;
            var crossSectionMinimum = double.NaN;
            var crossSectionMaximum = double.NaN;
            var crossSectionMean = double.NaN;
            var crossSectionPreviewed = false;
            if (crossSectionStep is not null)
            {
                crossSectionViewModel.UseC3DSmokeScene();
                crossSectionViewModel.SetCrossSectionRecipeStep(crossSectionStep);
                crossSectionPreviewed = C3DCrossSectionRuleCoordinator.Preview(
                    crossSectionPlan.Grid,
                    crossSectionViewModel,
                    (_, points, minimum, maximum, mean) =>
                    {
                        crossSectionPoints = points;
                        crossSectionMinimum = minimum;
                        crossSectionMaximum = maximum;
                        crossSectionMean = mean;
                        crossSectionProfileCalls++;
                    },
                    () => crossSectionRenderCalls++);
            }

            Check(
                "C3D Cross-section coordinator owns explicit Preview boundary",
                crossSectionPreviewed
                && crossSectionProfileCalls == 1
                && crossSectionRenderCalls == 1
                && crossSectionPoints is { Length: >= 2 }
                && double.IsFinite(crossSectionMinimum)
                && double.IsFinite(crossSectionMaximum)
                && double.IsFinite(crossSectionMean)
                && crossSectionViewModel.CrossSectionVisible
                && crossSectionViewModel.CrossSectionValidSampleCount == crossSectionPoints!.Length
                && crossSectionViewModel.PreviewToolResult.ToolName == CrossSectionDimensionsRule.ToolName
                && crossSectionViewModel.PreviewToolResult.Status != ResultStatus.NotRun,
                $"preview={crossSectionPreviewed}|profile={crossSectionProfileCalls}|render={crossSectionRenderCalls}|status={crossSectionViewModel.PreviewToolResult.Status}|samples={crossSectionViewModel.CrossSectionValidSampleCount}|range={crossSectionMinimum:G6}..{crossSectionMaximum:G6}");

            var planeFitViewModel = new MainWindowViewModel();
            planeFitViewModel.UseC3DSmokeScene();
            var planeFitViewStateClears = 0;
            var planeFitOverlayCalls = 0;
            var planeFitRenderCalls = 0;
            HeightFieldPlaneFitResult? planeFitResult = null;
            HeightGridPoint? planeFitTarget = null;
            (float MinX, float MaxX, float MinZ, float MaxZ)? planeFitBounds = null;
            var planeFitPreviewed = C3DReferencePlaneFitCoordinator.Fit(
                planeFlatnessPlan.Grid,
                planeFitViewModel,
                () => planeFitViewStateClears++,
                (result, target, bounds) =>
                {
                    planeFitResult = result;
                    planeFitTarget = target;
                    planeFitBounds = bounds;
                    planeFitOverlayCalls++;
                },
                () => planeFitRenderCalls++);
            var planeFitBoundsFinite = planeFitBounds is { } candidate
                && float.IsFinite(candidate.MinX)
                && float.IsFinite(candidate.MaxX)
                && float.IsFinite(candidate.MinZ)
                && float.IsFinite(candidate.MaxZ)
                && candidate.MinX < candidate.MaxX
                && candidate.MinZ < candidate.MaxZ;
            Check(
                "C3D reference-plane coordinator owns explicit Fit boundary",
                planeFitPreviewed
                && planeFitViewStateClears == 1
                && planeFitOverlayCalls == 1
                && planeFitRenderCalls == 1
                && planeFitResult is not null
                && planeFitResult.SampleCount >= 3
                && double.IsFinite(planeFitResult.SlopeX)
                && double.IsFinite(planeFitResult.SlopeZ)
                && double.IsFinite(planeFitResult.Intercept)
                && double.IsFinite(planeFitResult.RootMeanSquareDistance)
                && planeFitTarget.HasValue
                && planeFitBoundsFinite
                && planeFitViewModel.PlaneReferenceMeasurementVisible
                && planeFitViewModel.PlaneReferenceSampleCount == planeFitResult.SampleCount
                && double.IsFinite(planeFitViewModel.PlaneReferenceFitRms)
                && double.IsFinite(planeFitViewModel.PlaneReferenceSignedDistance)
                && planeFitViewModel.SelectedSelectionMode == "Plane Distance"
                && planeFitViewModel.SelectedEntity == "Plane Distance Measurement"
                && planeFitViewModel.ViewerStatus == "Fitted C3D plane and maximum residual measured",
                $"preview={planeFitPreviewed}|clear={planeFitViewStateClears}|overlay={planeFitOverlayCalls}|render={planeFitRenderCalls}|status={planeFitViewModel.ViewerStatus}|samples={planeFitResult?.SampleCount}|rms={planeFitViewModel.PlaneReferenceFitRms:G6}");

            var planeFitGuardViewModel = new MainWindowViewModel();
            var planeFitGuardRenderCalls = 0;
            var planeFitGuarded = C3DReferencePlaneFitCoordinator.Fit(
                null,
                planeFitGuardViewModel,
                () => { },
                (_, _, _) => { },
                () => planeFitGuardRenderCalls++);
            Check(
                "C3D reference-plane coordinator guards missing visible grid",
                !planeFitGuarded
                && planeFitGuardRenderCalls == 0
                && planeFitGuardViewModel.ViewerStatus == "Plane fit requires a visible C3D height grid",
                $"preview={planeFitGuarded}|render={planeFitGuardRenderCalls}|status={planeFitGuardViewModel.ViewerStatus}");

            var viewModel = normalViewModel;

            var reportDirectory = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
            var savedRecipePath = Path.Combine(reportDirectory, "height-deviation-save.recipe.json");
            var saved = HeightDeviationRecipeSaveCoordinator.Save(
                savedRecipePath,
                isSmoke: true,
                viewModel: viewModel,
                sourcePath: plan.SourcePath,
                roiStep: null,
                outputEnabled: true);
            Check(
                "save coordinator persists the recipe",
                saved && File.Exists(savedRecipePath),
                $"saved={saved}|path={savedRecipePath}");

            var savedRecipe = OpenVisionLab.ThreeD.Tools.HeightDeviationRecipe.Load(savedRecipePath);
            var savedRecipeFile = ViewerRecipeFile.Open(savedRecipePath);
            var savedSourcePath = savedRecipeFile.ResolveSourcePath(savedRecipe.Source.Path);
            var expectedSavedSourcePath = Path.GetRelativePath(
                Path.GetDirectoryName(savedRecipePath)!,
                plan.SourcePath).Replace('\\', '/');
            Check(
                "saved recipe retains the correct source mapping",
                savedRecipe.Source.Path == expectedSavedSourcePath,
                $"saved={savedRecipe.Source.Path}|expected={expectedSavedSourcePath}");
            Check(
                "saved recipe source reloads beside the recipe",
                File.Exists(savedSourcePath) && savedSourcePath == plan.SourcePath,
                savedSourcePath);
            Check(
                "save coordinator updates persisted ViewModel state",
                viewModel.RecipeSaveSummary == $"Recipe saved: {Path.GetFullPath(savedRecipePath)}"
                && viewModel.ViewerStatus.StartsWith("Smoke recipe saved:", StringComparison.Ordinal),
                $"summary={viewModel.RecipeSaveSummary}|status={viewModel.ViewerStatus}");

            var failedSavePath = Path.Combine(reportDirectory, "invalid" + '\0' + ".recipe.json");
            var failedSave = HeightDeviationRecipeSaveCoordinator.Save(
                failedSavePath,
                isSmoke: true,
                viewModel: viewModel,
                sourcePath: plan.SourcePath,
                roiStep: null,
                outputEnabled: true);
            Check(
                "save coordinator reports persistence failures",
                !failedSave && viewModel.ViewerStatus.StartsWith("Smoke recipe save failed:", StringComparison.Ordinal),
                $"saved={failedSave}|status={viewModel.ViewerStatus}");

            var invalidFile = new ViewerRecipeFile(
                recipePath,
                recipeFile.RecipeType);
            var invalidRecipe = recipe with
            {
                Source = recipe.Source with { Path = "missing-source.C3D" }
            };
            Check(
                "missing source fails during load plan creation",
                Throws(() => HeightDeviationRecipeLoadPlan.Create(invalidFile, invalidRecipe, 55000)),
                "missing-source.C3D");

            var thicknessRecipePath = Path.GetFullPath(
                Path.Combine("recipes", "c3d-thickness.recipe.json"));
            var thicknessRecipeFile = ViewerRecipeFile.Open(thicknessRecipePath);
            var thicknessRecipe = (C3DThicknessRecipe)thicknessRecipeFile.LoadDocument();
            var thicknessPlan = C3DThicknessRecipeLoadPlan.Create(
                thicknessRecipeFile,
                thicknessRecipe,
                maxRenderedPoints: 55000);
            Check(
                "Thickness plan resolves and validates the authored ROI",
                thicknessPlan.SourcePath == thicknessRecipeFile.ResolveSourcePath(thicknessRecipe.Source.Path)
                && thicknessPlan.Grid.Width == 1280
                && thicknessPlan.Grid.Height == 840
                && C3DThicknessRecipeLoadPlan.IsRoiInside(thicknessRecipe.Step.Roi, thicknessPlan.Grid),
                $"source={thicknessPlan.SourcePath}|grid={thicknessPlan.Grid.Width}x{thicknessPlan.Grid.Height}");

            var normalThicknessViewModel = new MainWindowViewModel();
            var normalThicknessStatusCalls = 0;
            var normalThicknessClearCalls = 0;
            var normalThicknessRenderCalls = 0;
            var normalThicknessApplied = C3DThicknessRecipeApplyCoordinator.Apply(
                thicknessPlan,
                normalThicknessViewModel,
                isSmoke: false,
                applySampleStatus: () => normalThicknessStatusCalls++,
                clearTransientInspectionState: () => normalThicknessClearCalls++,
                requestPreviewRender: () => normalThicknessRenderCalls++);
            Check(
                "normal Thickness apply is state-only",
                normalThicknessApplied
                && normalThicknessStatusCalls == 1
                && normalThicknessClearCalls == 1
                && normalThicknessRenderCalls == 0
                && normalThicknessViewModel.C3DSampleVisible
                && normalThicknessViewModel.ThicknessConfigured
                && !normalThicknessViewModel.ThicknessVisible
                && normalThicknessViewModel.PreviewToolResult.Status == ResultStatus.NotRun
                && normalThicknessViewModel.ViewerStatus.StartsWith("Thickness recipe loaded:", StringComparison.Ordinal),
                $"applied={normalThicknessApplied}|sampleStatus={normalThicknessStatusCalls}|clear={normalThicknessClearCalls}|c3d={normalThicknessViewModel.C3DSampleVisible}|configured={normalThicknessViewModel.ThicknessConfigured}|visible={normalThicknessViewModel.ThicknessVisible}|preview={normalThicknessViewModel.PreviewToolResult.Status}|renders={normalThicknessRenderCalls}|status={normalThicknessViewModel.ViewerStatus}");

            var smokeThicknessViewModel = new MainWindowViewModel();
            var smokeThicknessRenderCalls = 0;
            var smokeThicknessApplied = C3DThicknessRecipeApplyCoordinator.Apply(
                thicknessPlan,
                smokeThicknessViewModel,
                isSmoke: true,
                applySampleStatus: () => { },
                clearTransientInspectionState: () => { },
                requestPreviewRender: () => smokeThicknessRenderCalls++);
            Check(
                "smoke Thickness apply owns the explicit Preview path",
                smokeThicknessApplied
                && smokeThicknessRenderCalls == 1
                && smokeThicknessViewModel.ThicknessVisible
                && smokeThicknessViewModel.PreviewToolResult.ToolName == C3DThicknessRule.ToolName
                && smokeThicknessViewModel.PreviewToolResult.Status != ResultStatus.NotRun
                && smokeThicknessViewModel.ViewerStatus.StartsWith("Smoke Thickness recipe:", StringComparison.Ordinal),
                $"applied={smokeThicknessApplied}|preview={smokeThicknessViewModel.PreviewToolResult.Status}|renders={smokeThicknessRenderCalls}");

            var disabledOutputRecipe = thicknessRecipe with { OutputEnabled = false };
            var disabledOutputPlan = thicknessPlan with { Recipe = disabledOutputRecipe };
            var disabledOutputViewModel = new MainWindowViewModel();
            var disabledOutputRenderCalls = 0;
            var disabledOutputApplied = C3DThicknessRecipeApplyCoordinator.Apply(
                disabledOutputPlan,
                disabledOutputViewModel,
                isSmoke: true,
                applySampleStatus: () => { },
                clearTransientInspectionState: () => { },
                requestPreviewRender: () => disabledOutputRenderCalls++);
            Check(
                "disabled Thickness output suppresses smoke Preview",
                disabledOutputApplied
                && !disabledOutputViewModel.RecipeOutputEnabled
                && disabledOutputRenderCalls == 0
                && disabledOutputViewModel.PreviewToolResult.Status == ResultStatus.NotRun
                && !disabledOutputViewModel.ThicknessVisible,
                $"applied={disabledOutputApplied}|output={disabledOutputViewModel.RecipeOutputEnabled}|preview={disabledOutputViewModel.PreviewToolResult.Status}");

            Check(
                "Thickness save eligibility follows output policy",
                !C3DThicknessRecipeSaveCoordinator.CanSave(thicknessPlan.Grid, normalThicknessViewModel)
                && C3DThicknessRecipeSaveCoordinator.CanSave(thicknessPlan.Grid, smokeThicknessViewModel)
                && C3DThicknessRecipeSaveCoordinator.CanSave(thicknessPlan.Grid, disabledOutputViewModel),
                $"normal={C3DThicknessRecipeSaveCoordinator.CanSave(thicknessPlan.Grid, normalThicknessViewModel)}|smoke={C3DThicknessRecipeSaveCoordinator.CanSave(thicknessPlan.Grid, smokeThicknessViewModel)}|disabled={C3DThicknessRecipeSaveCoordinator.CanSave(thicknessPlan.Grid, disabledOutputViewModel)}");

            var thicknessSavePath = Path.Combine(reportDirectory, "c3d-thickness-save.recipe.json");
            var thicknessSaved = C3DThicknessRecipeSaveCoordinator.Save(
                thicknessSavePath,
                isSmoke: true,
                viewModel: smokeThicknessViewModel,
                grid: thicknessPlan.Grid);
            var savedThicknessRecipe = File.Exists(thicknessSavePath)
                ? C3DThicknessRecipe.Load(thicknessSavePath)
                : null;
            var savedThicknessSourcePath = savedThicknessRecipe is null
                ? string.Empty
                : ViewerRecipeFile.Open(thicknessSavePath).ResolveSourcePath(savedThicknessRecipe.Source.Path);
            Check(
                "Thickness save persists relative source and saved state",
                thicknessSaved
                && savedThicknessRecipe is not null
                && savedThicknessRecipe.OutputEnabled
                && savedThicknessSourcePath == thicknessPlan.SourcePath
                && smokeThicknessViewModel.RecipeSaveSummary == $"Recipe saved: {Path.GetFullPath(thicknessSavePath)}"
                && smokeThicknessViewModel.ViewerStatus.StartsWith("Smoke Thickness recipe saved:", StringComparison.Ordinal),
                $"saved={thicknessSaved}|source={savedThicknessSourcePath}|summary={smokeThicknessViewModel.RecipeSaveSummary}");

            var disabledOutputSavePath = Path.Combine(reportDirectory, "c3d-thickness-disabled-output.recipe.json");
            var disabledOutputSaved = C3DThicknessRecipeSaveCoordinator.Save(
                disabledOutputSavePath,
                isSmoke: false,
                viewModel: disabledOutputViewModel,
                grid: thicknessPlan.Grid);
            var savedDisabledOutputRecipe = File.Exists(disabledOutputSavePath)
                ? C3DThicknessRecipe.Load(disabledOutputSavePath)
                : null;
            Check(
                "disabled-output Thickness recipe saves without Preview",
                disabledOutputSaved
                && savedDisabledOutputRecipe is not null
                && !savedDisabledOutputRecipe.OutputEnabled
                && disabledOutputViewModel.ViewerStatus.StartsWith("Thickness recipe saved:", StringComparison.Ordinal),
                $"saved={disabledOutputSaved}|output={savedDisabledOutputRecipe?.OutputEnabled}");

            var invalidRoiRecipe = thicknessRecipe with
            {
                Step = thicknessRecipe.Step with
                {
                    Roi = thicknessRecipe.Step.Roi with { Row = thicknessPlan.Grid.Height }
                }
            };
            Check(
                "Thickness plan rejects an out-of-grid ROI",
                Throws(() => C3DThicknessRecipeLoadPlan.Create(
                    thicknessRecipeFile,
                    invalidRoiRecipe,
                    maxRenderedPoints: 55000)),
                "row=grid-height");

            var warpageRecipePath = Path.GetFullPath(
                Path.Combine("recipes", "c3d-warpage.recipe.json"));
            var warpageRecipeFile = ViewerRecipeFile.Open(warpageRecipePath);
            var warpageRecipe = (C3DWarpageRecipe)warpageRecipeFile.LoadDocument();
            var warpagePlan = C3DWarpageRecipeLoadPlan.Create(
                warpageRecipeFile,
                warpageRecipe,
                maxRenderedPoints: 55000);
            Check(
                "Warpage plan resolves and validates the authored ROI",
                warpagePlan.SourcePath == warpageRecipeFile.ResolveSourcePath(warpageRecipe.Source.Path)
                && warpagePlan.Grid.Width == 1280
                && warpagePlan.Grid.Height == 840
                && C3DWarpageRecipeLoadPlan.IsRoiInside(warpageRecipe.Step.Roi, warpagePlan.Grid),
                $"source={warpagePlan.SourcePath}|grid={warpagePlan.Grid.Width}x{warpagePlan.Grid.Height}");

            var normalWarpageViewModel = new MainWindowViewModel();
            var normalWarpageStatusCalls = 0;
            var normalWarpageClearCalls = 0;
            var normalWarpageRenderCalls = 0;
            var normalWarpageApplied = C3DWarpageRecipeApplyCoordinator.Apply(
                warpagePlan,
                normalWarpageViewModel,
                isSmoke: false,
                applySampleStatus: () => normalWarpageStatusCalls++,
                clearTransientInspectionState: () => normalWarpageClearCalls++,
                requestPreviewRender: () => normalWarpageRenderCalls++);
            Check(
                "normal Warpage apply is state-only",
                normalWarpageApplied
                && normalWarpageStatusCalls == 1
                && normalWarpageClearCalls == 1
                && normalWarpageRenderCalls == 0
                && normalWarpageViewModel.C3DSampleVisible
                && normalWarpageViewModel.WarpageConfigured
                && !normalWarpageViewModel.WarpageVisible
                && normalWarpageViewModel.PreviewToolResult.Status == ResultStatus.NotRun
                && normalWarpageViewModel.ViewerStatus.StartsWith("Warpage recipe loaded:", StringComparison.Ordinal),
                $"applied={normalWarpageApplied}|sampleStatus={normalWarpageStatusCalls}|clear={normalWarpageClearCalls}|c3d={normalWarpageViewModel.C3DSampleVisible}|configured={normalWarpageViewModel.WarpageConfigured}|visible={normalWarpageViewModel.WarpageVisible}|preview={normalWarpageViewModel.PreviewToolResult.Status}|renders={normalWarpageRenderCalls}|status={normalWarpageViewModel.ViewerStatus}");

            var smokeWarpageViewModel = new MainWindowViewModel();
            var smokeWarpageRenderCalls = 0;
            var smokeWarpageApplied = C3DWarpageRecipeApplyCoordinator.Apply(
                warpagePlan,
                smokeWarpageViewModel,
                isSmoke: true,
                applySampleStatus: () => { },
                clearTransientInspectionState: () => { },
                requestPreviewRender: () => smokeWarpageRenderCalls++);
            Check(
                "smoke Warpage apply owns the explicit Preview path",
                smokeWarpageApplied
                && smokeWarpageRenderCalls == 1
                && smokeWarpageViewModel.WarpageVisible
                && smokeWarpageViewModel.PreviewToolResult.ToolName == C3DWarpageRule.ToolName
                && smokeWarpageViewModel.PreviewToolResult.Status != ResultStatus.NotRun
                && smokeWarpageViewModel.ViewerStatus.StartsWith("Smoke Warpage recipe:", StringComparison.Ordinal),
                $"applied={smokeWarpageApplied}|preview={smokeWarpageViewModel.PreviewToolResult.Status}|renders={smokeWarpageRenderCalls}");

            var disabledWarpageRecipe = warpageRecipe with { OutputEnabled = false };
            var disabledWarpagePlan = warpagePlan with { Recipe = disabledWarpageRecipe };
            var disabledWarpageViewModel = new MainWindowViewModel();
            var disabledWarpageRenderCalls = 0;
            var disabledWarpageApplied = C3DWarpageRecipeApplyCoordinator.Apply(
                disabledWarpagePlan,
                disabledWarpageViewModel,
                isSmoke: true,
                applySampleStatus: () => { },
                clearTransientInspectionState: () => { },
                requestPreviewRender: () => disabledWarpageRenderCalls++);
            Check(
                "disabled Warpage output suppresses smoke Preview",
                disabledWarpageApplied
                && !disabledWarpageViewModel.RecipeOutputEnabled
                && disabledWarpageRenderCalls == 0
                && disabledWarpageViewModel.PreviewToolResult.Status == ResultStatus.NotRun
                && !disabledWarpageViewModel.WarpageVisible,
                $"applied={disabledWarpageApplied}|output={disabledWarpageViewModel.RecipeOutputEnabled}|preview={disabledWarpageViewModel.PreviewToolResult.Status}");

            Check(
                "Warpage save eligibility follows output policy",
                !C3DWarpageRecipeSaveCoordinator.CanSave(warpagePlan.Grid, normalWarpageViewModel)
                && C3DWarpageRecipeSaveCoordinator.CanSave(warpagePlan.Grid, smokeWarpageViewModel)
                && C3DWarpageRecipeSaveCoordinator.CanSave(warpagePlan.Grid, disabledWarpageViewModel),
                $"normal={C3DWarpageRecipeSaveCoordinator.CanSave(warpagePlan.Grid, normalWarpageViewModel)}|smoke={C3DWarpageRecipeSaveCoordinator.CanSave(warpagePlan.Grid, smokeWarpageViewModel)}|disabled={C3DWarpageRecipeSaveCoordinator.CanSave(warpagePlan.Grid, disabledWarpageViewModel)}");

            var warpageSavePath = Path.Combine(reportDirectory, "c3d-warpage-save.recipe.json");
            var warpageSaved = C3DWarpageRecipeSaveCoordinator.Save(
                warpageSavePath,
                isSmoke: true,
                viewModel: smokeWarpageViewModel,
                grid: warpagePlan.Grid);
            var savedWarpageRecipe = File.Exists(warpageSavePath)
                ? C3DWarpageRecipe.Load(warpageSavePath)
                : null;
            var savedWarpageSourcePath = savedWarpageRecipe is null
                ? string.Empty
                : ViewerRecipeFile.Open(warpageSavePath).ResolveSourcePath(savedWarpageRecipe.Source.Path);
            Check(
                "Warpage save persists relative source and saved state",
                warpageSaved
                && savedWarpageRecipe is not null
                && savedWarpageRecipe.OutputEnabled
                && savedWarpageSourcePath == warpagePlan.SourcePath
                && smokeWarpageViewModel.RecipeSaveSummary == $"Recipe saved: {Path.GetFullPath(warpageSavePath)}"
                && smokeWarpageViewModel.ViewerStatus.StartsWith("Smoke Warpage recipe saved:", StringComparison.Ordinal),
                $"saved={warpageSaved}|source={savedWarpageSourcePath}|summary={smokeWarpageViewModel.RecipeSaveSummary}");

            var disabledWarpageSavePath = Path.Combine(reportDirectory, "c3d-warpage-disabled-output.recipe.json");
            var disabledWarpageSaved = C3DWarpageRecipeSaveCoordinator.Save(
                disabledWarpageSavePath,
                isSmoke: false,
                viewModel: disabledWarpageViewModel,
                grid: warpagePlan.Grid);
            var savedDisabledWarpageRecipe = File.Exists(disabledWarpageSavePath)
                ? C3DWarpageRecipe.Load(disabledWarpageSavePath)
                : null;
            Check(
                "disabled-output Warpage recipe saves without Preview",
                disabledWarpageSaved
                && savedDisabledWarpageRecipe is not null
                && !savedDisabledWarpageRecipe.OutputEnabled
                && disabledWarpageViewModel.ViewerStatus.StartsWith("Warpage recipe saved:", StringComparison.Ordinal),
                $"saved={disabledWarpageSaved}|output={savedDisabledWarpageRecipe?.OutputEnabled}");

            var invalidWarpageRecipe = warpageRecipe with
            {
                Step = warpageRecipe.Step with
                {
                    Roi = warpageRecipe.Step.Roi with { Row = warpagePlan.Grid.Height }
                }
            };
            Check(
                "Warpage plan rejects an out-of-grid ROI",
                Throws(() => C3DWarpageRecipeLoadPlan.Create(
                    warpageRecipeFile,
                    invalidWarpageRecipe,
                    maxRenderedPoints: 55000)),
                "row=grid-height");

            var pointPairRecipePath = Path.GetFullPath(
                Path.Combine("recipes", "c3d-point-pair-dimensions.recipe.json"));
            var pointPairRecipeFile = ViewerRecipeFile.Open(pointPairRecipePath);
            var pointPairRecipe = (C3DPointPairDimensionsRecipe)pointPairRecipeFile.LoadDocument();
            var pointPairPlan = C3DPointPairDimensionsRecipeLoadPlan.Create(
                pointPairRecipeFile,
                pointPairRecipe,
                maxRenderedPoints: 55000);
            Check(
                "Point Pair plan resolves and reads both authored source cells",
                pointPairPlan.SourcePath == pointPairRecipeFile.ResolveSourcePath(pointPairRecipe.Source.Path)
                && pointPairPlan.Grid.Width == 1280
                && pointPairPlan.Grid.Height == 840
                && pointPairPlan.First.Row == pointPairRecipe.Step.First.Row
                && pointPairPlan.First.Column == pointPairRecipe.Step.First.Column
                && pointPairPlan.Second.Row == pointPairRecipe.Step.Second.Row
                && pointPairPlan.Second.Column == pointPairRecipe.Step.Second.Column,
                $"source={pointPairPlan.SourcePath}|first=({pointPairPlan.First.Row},{pointPairPlan.First.Column})|second=({pointPairPlan.Second.Row},{pointPairPlan.Second.Column})");

            var transformedPointPairRecipe = pointPairRecipe with
            {
                Transform = new ModelTransform(1.25, -0.5, 2.0, 0.0, 0.0, 15.0, 1.1)
            };
            var transformedPointPairPlan = pointPairPlan with { Recipe = transformedPointPairRecipe };
            var normalPointPairViewModel = new MainWindowViewModel();
            var normalPointPairStatusCalls = 0;
            var normalPointPairClearCalls = 0;
            var normalPointPairRoiClearCalls = 0;
            var normalPointPairMeasurementCalls = 0;
            var normalPointPairRenderCalls = 0;
            var normalPointPairApplied = C3DPointPairDimensionsRecipeApplyCoordinator.Apply(
                transformedPointPairPlan,
                normalPointPairViewModel,
                isSmoke: false,
                applySampleStatus: () => normalPointPairStatusCalls++,
                clearTransientInspectionState: () => normalPointPairClearCalls++,
                clearRecipeRoiStep: () => normalPointPairRoiClearCalls++,
                applyPointPairMeasurement: (_, _) => normalPointPairMeasurementCalls++,
                requestPreviewRender: () => normalPointPairRenderCalls++);
            Check(
                "normal Point Pair apply is state-only and restores transform",
                normalPointPairApplied
                && normalPointPairStatusCalls == 1
                && normalPointPairClearCalls == 1
                && normalPointPairRoiClearCalls == 1
                && normalPointPairMeasurementCalls == 1
                && normalPointPairRenderCalls == 0
                && normalPointPairViewModel.C3DSampleVisible
                && normalPointPairViewModel.PointPairDimensionsConfigured
                && normalPointPairViewModel.HasPointPairReferences
                && !normalPointPairViewModel.PointPairDimensionsVisible
                && normalPointPairViewModel.PreviewToolResult.Status == ResultStatus.NotRun
                && normalPointPairViewModel.C3DModelTransform == transformedPointPairRecipe.Transform
                && normalPointPairViewModel.SelectedSelectionMode == MainWindowViewModel.PointPairSelectionMode
                && normalPointPairViewModel.ViewerStatus.StartsWith("Point pair recipe loaded:", StringComparison.Ordinal),
                $"applied={normalPointPairApplied}|statusCalls={normalPointPairStatusCalls}|clear={normalPointPairClearCalls}|roiClear={normalPointPairRoiClearCalls}|measure={normalPointPairMeasurementCalls}|renders={normalPointPairRenderCalls}|preview={normalPointPairViewModel.PreviewToolResult.Status}|transform={normalPointPairViewModel.C3DModelTransform}");

            var smokePointPairViewModel = new MainWindowViewModel();
            var smokePointPairMeasurementCalls = 0;
            var smokePointPairRenderCalls = 0;
            var smokePointPairApplied = C3DPointPairDimensionsRecipeApplyCoordinator.Apply(
                pointPairPlan,
                smokePointPairViewModel,
                isSmoke: true,
                applySampleStatus: () => { },
                clearTransientInspectionState: () => { },
                clearRecipeRoiStep: () => { },
                applyPointPairMeasurement: (_, _) => smokePointPairMeasurementCalls++,
                requestPreviewRender: () => smokePointPairRenderCalls++);
            Check(
                "smoke Point Pair apply owns the explicit Preview path",
                smokePointPairApplied
                && smokePointPairMeasurementCalls == 2
                && smokePointPairRenderCalls == 1
                && smokePointPairViewModel.PointPairDimensionsVisible
                && smokePointPairViewModel.PreviewToolResult.ToolName == PointPairDimensionsRule.ToolName
                && smokePointPairViewModel.PreviewToolResult.Status != ResultStatus.NotRun
                && smokePointPairViewModel.ViewerStatus.StartsWith("Smoke point pair recipe:", StringComparison.Ordinal),
                $"applied={smokePointPairApplied}|measure={smokePointPairMeasurementCalls}|preview={smokePointPairViewModel.PreviewToolResult.Status}|renders={smokePointPairRenderCalls}");

            var disabledPointPairRecipe = pointPairRecipe with { OutputEnabled = false };
            var disabledPointPairPlan = pointPairPlan with { Recipe = disabledPointPairRecipe };
            var disabledPointPairViewModel = new MainWindowViewModel();
            var disabledPointPairMeasurementCalls = 0;
            var disabledPointPairRenderCalls = 0;
            var disabledPointPairApplied = C3DPointPairDimensionsRecipeApplyCoordinator.Apply(
                disabledPointPairPlan,
                disabledPointPairViewModel,
                isSmoke: true,
                applySampleStatus: () => { },
                clearTransientInspectionState: () => { },
                clearRecipeRoiStep: () => { },
                applyPointPairMeasurement: (_, _) => disabledPointPairMeasurementCalls++,
                requestPreviewRender: () => disabledPointPairRenderCalls++);
            Check(
                "disabled Point Pair output suppresses smoke Preview",
                disabledPointPairApplied
                && !disabledPointPairViewModel.RecipeOutputEnabled
                && disabledPointPairMeasurementCalls == 1
                && disabledPointPairRenderCalls == 0
                && disabledPointPairViewModel.PreviewToolResult.Status == ResultStatus.NotRun
                && !disabledPointPairViewModel.PointPairDimensionsVisible,
                $"applied={disabledPointPairApplied}|output={disabledPointPairViewModel.RecipeOutputEnabled}|measure={disabledPointPairMeasurementCalls}|preview={disabledPointPairViewModel.PreviewToolResult.Status}");

            Check(
                "Point Pair save eligibility preserves the existing reference-based policy",
                C3DPointPairDimensionsRecipeSaveCoordinator.CanSave(pointPairPlan.Grid, normalPointPairViewModel)
                && C3DPointPairDimensionsRecipeSaveCoordinator.CanSave(pointPairPlan.Grid, smokePointPairViewModel)
                && C3DPointPairDimensionsRecipeSaveCoordinator.CanSave(pointPairPlan.Grid, disabledPointPairViewModel),
                $"normal={C3DPointPairDimensionsRecipeSaveCoordinator.CanSave(pointPairPlan.Grid, normalPointPairViewModel)}|smoke={C3DPointPairDimensionsRecipeSaveCoordinator.CanSave(pointPairPlan.Grid, smokePointPairViewModel)}|disabled={C3DPointPairDimensionsRecipeSaveCoordinator.CanSave(pointPairPlan.Grid, disabledPointPairViewModel)}");

            var pointPairSavePath = Path.Combine(reportDirectory, "c3d-point-pair-dimensions-save.recipe.json");
            var pointPairSaved = C3DPointPairDimensionsRecipeSaveCoordinator.Save(
                pointPairSavePath,
                isSmoke: true,
                viewModel: smokePointPairViewModel,
                grid: pointPairPlan.Grid);
            var savedPointPairRecipe = File.Exists(pointPairSavePath)
                ? C3DPointPairDimensionsRecipe.Load(pointPairSavePath)
                : null;
            var savedPointPairSourcePath = savedPointPairRecipe is null
                ? string.Empty
                : ViewerRecipeFile.Open(pointPairSavePath).ResolveSourcePath(savedPointPairRecipe.Source.Path);
            Check(
                "Point Pair save persists relative source, transform, references, and saved state",
                pointPairSaved
                && savedPointPairRecipe is not null
                && savedPointPairRecipe.OutputEnabled
                && savedPointPairRecipe.Step.First == pointPairRecipe.Step.First
                && savedPointPairRecipe.Step.Second == pointPairRecipe.Step.Second
                && savedPointPairRecipe.Transform == pointPairRecipe.Transform
                && savedPointPairSourcePath == pointPairPlan.SourcePath
                && smokePointPairViewModel.RecipeSaveSummary == $"Recipe saved: {Path.GetFullPath(pointPairSavePath)}"
                && smokePointPairViewModel.ViewerStatus.StartsWith("Smoke point pair recipe saved:", StringComparison.Ordinal),
                $"saved={pointPairSaved}|source={savedPointPairSourcePath}|transform={savedPointPairRecipe?.Transform}|summary={smokePointPairViewModel.RecipeSaveSummary}");

            var disabledPointPairSavePath = Path.Combine(reportDirectory, "c3d-point-pair-dimensions-disabled-output.recipe.json");
            var disabledPointPairSaved = C3DPointPairDimensionsRecipeSaveCoordinator.Save(
                disabledPointPairSavePath,
                isSmoke: false,
                viewModel: disabledPointPairViewModel,
                grid: disabledPointPairPlan.Grid);
            var savedDisabledPointPairRecipe = File.Exists(disabledPointPairSavePath)
                ? C3DPointPairDimensionsRecipe.Load(disabledPointPairSavePath)
                : null;
            Check(
                "disabled-output Point Pair recipe saves without Preview",
                disabledPointPairSaved
                && savedDisabledPointPairRecipe is not null
                && !savedDisabledPointPairRecipe.OutputEnabled
                && disabledPointPairViewModel.ViewerStatus.StartsWith("Point pair recipe saved:", StringComparison.Ordinal),
                $"saved={disabledPointPairSaved}|output={savedDisabledPointPairRecipe?.OutputEnabled}");

            var invalidPointPairRecipe = pointPairRecipe with
            {
                Step = pointPairRecipe.Step with
                {
                    First = pointPairRecipe.Step.First with { Row = pointPairPlan.Grid.Height }
                }
            };
            Check(
                "Point Pair plan rejects an out-of-grid source cell",
                Throws(() => C3DPointPairDimensionsRecipeLoadPlan.Create(
                    pointPairRecipeFile,
                    invalidPointPairRecipe,
                    maxRenderedPoints: 55000)),
                "first-row=grid-height");

            var gapFlushRecipePath = Path.GetFullPath(
                Path.Combine("recipes", "c3d-gap-flush.recipe.json"));
            var gapFlushRecipeFile = ViewerRecipeFile.Open(gapFlushRecipePath);
            var gapFlushRecipe = (C3DGapFlushRecipe)gapFlushRecipeFile.LoadDocument();
            var gapFlushPlan = C3DGapFlushRecipeLoadPlan.Create(
                gapFlushRecipeFile,
                gapFlushRecipe,
                maxRenderedPoints: 55000);
            Check(
                "Gap / Flush plan resolves the authored C3D source",
                gapFlushPlan.SourcePath == gapFlushRecipeFile.ResolveSourcePath(gapFlushRecipe.Source.Path)
                && gapFlushPlan.Grid.Width == 1280
                && gapFlushPlan.Grid.Height == 840
                && ReferenceEquals(gapFlushPlan.Recipe, gapFlushRecipe),
                $"source={gapFlushPlan.SourcePath}|grid={gapFlushPlan.Grid.Width}x{gapFlushPlan.Grid.Height}");

            var identityGapLeft = C3DGapFlushRuleCoordinator.CalculateStats(
                gapFlushPlan.Grid.Points,
                gapFlushRecipe.Step.LeftRegion,
                ModelTransform.Identity);
            var shiftedGapLeft = C3DGapFlushRuleCoordinator.CalculateStats(
                gapFlushPlan.Grid.Points,
                gapFlushRecipe.Step.LeftRegion,
                new ModelTransform(0.0, 2.0, 0.0, 0.0, 0.0, 0.0, 1.0));
            Check(
                "Gap / Flush statistics honor the authored model transform",
                identityGapLeft.PointCount > 0
                && shiftedGapLeft.PointCount == identityGapLeft.PointCount
                && Math.Abs(shiftedGapLeft.ModelYMean - identityGapLeft.ModelYMean - 2.0) < 0.0001,
                $"identityCount={identityGapLeft.PointCount}|shiftedCount={shiftedGapLeft.PointCount}|identityY={identityGapLeft.ModelYMean}|shiftedY={shiftedGapLeft.ModelYMean}");

            var transformedGapFlushRecipe = gapFlushRecipe with
            {
                Transform = new ModelTransform(0.0, 2.0, 0.0, 0.0, 0.0, 0.0, 1.0)
            };
            var transformedGapFlushPlan = gapFlushPlan with { Recipe = transformedGapFlushRecipe };
            var normalGapFlushViewModel = new MainWindowViewModel();
            var normalGapFlushStatusCalls = 0;
            var normalGapFlushClearCalls = 0;
            var normalGapFlushRoiCalls = 0;
            var normalGapFlushOverlayCalls = 0;
            var normalGapFlushRenderCalls = 0;
            var normalGapFlushApplied = C3DGapFlushRecipeApplyCoordinator.Apply(
                transformedGapFlushPlan,
                normalGapFlushViewModel,
                isSmoke: false,
                applySampleStatus: () => normalGapFlushStatusCalls++,
                clearTransientInspectionState: () => normalGapFlushClearCalls++,
                applyRecipeRoiState: _ => normalGapFlushRoiCalls++,
                applyPreviewOverlay: (_, _, _) => normalGapFlushOverlayCalls++,
                requestPreviewRender: () => normalGapFlushRenderCalls++);
            Check(
                "normal Gap / Flush apply is state-only and restores transform",
                normalGapFlushApplied
                && normalGapFlushStatusCalls == 1
                && normalGapFlushClearCalls == 1
                && normalGapFlushRoiCalls == 1
                && normalGapFlushOverlayCalls == 0
                && normalGapFlushRenderCalls == 0
                && normalGapFlushViewModel.C3DSampleVisible
                && normalGapFlushViewModel.GapFlushConfigured
                && !normalGapFlushViewModel.GapFlushVisible
                && normalGapFlushViewModel.PreviewToolResult.Status == ResultStatus.NotRun
                && normalGapFlushViewModel.C3DModelTransform == transformedGapFlushRecipe.Transform
                && normalGapFlushViewModel.ViewerStatus.StartsWith("Gap / Flush recipe loaded:", StringComparison.Ordinal),
                $"applied={normalGapFlushApplied}|statusCalls={normalGapFlushStatusCalls}|clear={normalGapFlushClearCalls}|roi={normalGapFlushRoiCalls}|overlay={normalGapFlushOverlayCalls}|renders={normalGapFlushRenderCalls}|preview={normalGapFlushViewModel.PreviewToolResult.Status}|transform={normalGapFlushViewModel.C3DModelTransform}");

            var smokeGapFlushViewModel = new MainWindowViewModel();
            var smokeGapFlushRoiCalls = 0;
            var smokeGapFlushOverlayCalls = 0;
            var smokeGapFlushRenderCalls = 0;
            var smokeGapFlushApplied = C3DGapFlushRecipeApplyCoordinator.Apply(
                gapFlushPlan,
                smokeGapFlushViewModel,
                isSmoke: true,
                applySampleStatus: () => { },
                clearTransientInspectionState: () => { },
                applyRecipeRoiState: _ => smokeGapFlushRoiCalls++,
                applyPreviewOverlay: (_, _, _) => smokeGapFlushOverlayCalls++,
                requestPreviewRender: () => smokeGapFlushRenderCalls++);
            Check(
                "smoke Gap / Flush apply owns the explicit Preview path",
                smokeGapFlushApplied
                && smokeGapFlushRoiCalls == 1
                && smokeGapFlushOverlayCalls == 1
                && smokeGapFlushRenderCalls == 1
                && smokeGapFlushViewModel.GapFlushVisible
                && smokeGapFlushViewModel.PreviewToolResult.ToolName == GapFlushRule.ToolName
                && smokeGapFlushViewModel.PreviewToolResult.Status != ResultStatus.NotRun
                && smokeGapFlushViewModel.ViewerStatus.StartsWith("Smoke Gap / Flush recipe:", StringComparison.Ordinal),
                $"applied={smokeGapFlushApplied}|roi={smokeGapFlushRoiCalls}|overlay={smokeGapFlushOverlayCalls}|preview={smokeGapFlushViewModel.PreviewToolResult.Status}|renders={smokeGapFlushRenderCalls}");

            var disabledGapFlushPlan = gapFlushPlan with { Recipe = gapFlushRecipe with { OutputEnabled = false } };
            var disabledGapFlushViewModel = new MainWindowViewModel();
            var disabledGapFlushOverlayCalls = 0;
            var disabledGapFlushRenderCalls = 0;
            var disabledGapFlushApplied = C3DGapFlushRecipeApplyCoordinator.Apply(
                disabledGapFlushPlan,
                disabledGapFlushViewModel,
                isSmoke: true,
                applySampleStatus: () => { },
                clearTransientInspectionState: () => { },
                applyRecipeRoiState: _ => { },
                applyPreviewOverlay: (_, _, _) => disabledGapFlushOverlayCalls++,
                requestPreviewRender: () => disabledGapFlushRenderCalls++);
            Check(
                "disabled Gap / Flush output suppresses smoke Preview",
                disabledGapFlushApplied
                && !disabledGapFlushViewModel.RecipeOutputEnabled
                && disabledGapFlushOverlayCalls == 0
                && disabledGapFlushRenderCalls == 0
                && disabledGapFlushViewModel.PreviewToolResult.Status == ResultStatus.NotRun
                && !disabledGapFlushViewModel.GapFlushVisible,
                $"applied={disabledGapFlushApplied}|output={disabledGapFlushViewModel.RecipeOutputEnabled}|preview={disabledGapFlushViewModel.PreviewToolResult.Status}|overlay={disabledGapFlushOverlayCalls}");

            Check(
                "Gap / Flush save eligibility follows the existing output policy",
                !C3DGapFlushRecipeSaveCoordinator.CanSave(gapFlushPlan.Grid, normalGapFlushViewModel)
                && C3DGapFlushRecipeSaveCoordinator.CanSave(gapFlushPlan.Grid, smokeGapFlushViewModel)
                && C3DGapFlushRecipeSaveCoordinator.CanSave(gapFlushPlan.Grid, disabledGapFlushViewModel),
                $"normal={C3DGapFlushRecipeSaveCoordinator.CanSave(gapFlushPlan.Grid, normalGapFlushViewModel)}|smoke={C3DGapFlushRecipeSaveCoordinator.CanSave(gapFlushPlan.Grid, smokeGapFlushViewModel)}|disabled={C3DGapFlushRecipeSaveCoordinator.CanSave(gapFlushPlan.Grid, disabledGapFlushViewModel)}");

            var gapFlushSavePath = Path.Combine(reportDirectory, "c3d-gap-flush-save.recipe.json");
            var gapFlushSaved = C3DGapFlushRecipeSaveCoordinator.Save(
                gapFlushSavePath,
                isSmoke: true,
                viewModel: smokeGapFlushViewModel,
                grid: gapFlushPlan.Grid);
            var savedGapFlushRecipe = File.Exists(gapFlushSavePath)
                ? C3DGapFlushRecipe.Load(gapFlushSavePath)
                : null;
            var savedGapFlushSourcePath = savedGapFlushRecipe is null
                ? string.Empty
                : ViewerRecipeFile.Open(gapFlushSavePath).ResolveSourcePath(savedGapFlushRecipe.Source.Path);
            Check(
                "Gap / Flush save persists relative source, transform, step, and saved state",
                gapFlushSaved
                && savedGapFlushRecipe is not null
                && savedGapFlushRecipe.OutputEnabled
                && savedGapFlushRecipe.Step == gapFlushRecipe.Step
                && savedGapFlushRecipe.Transform == gapFlushRecipe.Transform
                && savedGapFlushSourcePath == gapFlushPlan.SourcePath
                && smokeGapFlushViewModel.RecipeSaveSummary == $"Recipe saved: {Path.GetFullPath(gapFlushSavePath)}"
                && smokeGapFlushViewModel.ViewerStatus.StartsWith("Smoke Gap / Flush recipe saved:", StringComparison.Ordinal),
                $"saved={gapFlushSaved}|source={savedGapFlushSourcePath}|transform={savedGapFlushRecipe?.Transform}|summary={smokeGapFlushViewModel.RecipeSaveSummary}");

            var disabledGapFlushSavePath = Path.Combine(reportDirectory, "c3d-gap-flush-disabled-output.recipe.json");
            var disabledGapFlushSaved = C3DGapFlushRecipeSaveCoordinator.Save(
                disabledGapFlushSavePath,
                isSmoke: false,
                viewModel: disabledGapFlushViewModel,
                grid: disabledGapFlushPlan.Grid);
            var savedDisabledGapFlushRecipe = File.Exists(disabledGapFlushSavePath)
                ? C3DGapFlushRecipe.Load(disabledGapFlushSavePath)
                : null;
            Check(
                "disabled-output Gap / Flush recipe saves without Preview",
                disabledGapFlushSaved
                && savedDisabledGapFlushRecipe is not null
                && !savedDisabledGapFlushRecipe.OutputEnabled
                && disabledGapFlushViewModel.ViewerStatus.StartsWith("Gap / Flush recipe saved:", StringComparison.Ordinal),
                $"saved={disabledGapFlushSaved}|output={savedDisabledGapFlushRecipe?.OutputEnabled}");

            var invalidGapFlushRecipe = gapFlushRecipe with
            {
                Source = gapFlushRecipe.Source with { Path = "missing-gap-flush-source.C3D" }
            };
            Check(
                "Gap / Flush plan rejects a missing source",
                Throws(() => C3DGapFlushRecipeLoadPlan.Create(
                    gapFlushRecipeFile,
                    invalidGapFlushRecipe,
                    maxRenderedPoints: 55000)),
                "missing-gap-flush-source.C3D");

            var nominalSourcePath = Path.GetFullPath(
                Path.Combine("3D", "PublicSamples", "STL", "Tetrahedron.stl"));
            var nominalSourceInfo = new FileInfo(nominalSourcePath);
            var nominalSourceSha = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(nominalSourcePath)));
            var nominalInput = new NominalActualComparisonInput(
                "step.nominal-actual-owner-verification",
                new NominalActualFileIdentity(
                    "source.actual-verification",
                    "Actual verification STL",
                    nominalSourcePath,
                    nominalSourceInfo.Length,
                    nominalSourceSha),
                new NominalActualFileIdentity(
                    "source.nominal-verification",
                    "Nominal verification STL",
                    nominalSourcePath,
                    nominalSourceInfo.Length,
                    nominalSourceSha),
                new NominalActualFileIdentity(
                    "source.query-verification",
                    "Query verification STL",
                    nominalSourcePath,
                    nominalSourceInfo.Length,
                    nominalSourceSha),
                "mm",
                "frame.nominal-actual-verification",
                "alignment.identity-source-provided",
                -0.1,
                0.1);
            var nominalRecipePath = Path.Combine(
                reportDirectory,
                "nominal-actual-owner.recipe.json");
            var nominalRecipe = NominalActualComparisonRecipe.FromInput(
                nominalInput,
                nominalRecipePath);
            nominalRecipe.Save(nominalRecipePath);
            var nominalRecipeFile = ViewerRecipeFile.Open(nominalRecipePath);
            var loadedNominalRecipe =
                (NominalActualComparisonRecipe)nominalRecipeFile.LoadDocument();
            var nominalPlan = NominalActualComparisonRecipeLoadPlan.Create(
                nominalRecipeFile,
                loadedNominalRecipe);
            Check(
                "Nominal/Actual load plan resolves and retains source identities",
                nominalPlan.FullRecipePath == Path.GetFullPath(nominalRecipePath)
                && ReferenceEquals(nominalPlan.Recipe, loadedNominalRecipe)
                && nominalPlan.Input.NominalSource.Path == nominalSourcePath
                && nominalPlan.Input.ExecutionFingerprint == nominalInput.ExecutionFingerprint,
                $"recipe={nominalPlan.FullRecipePath}|nominal={nominalPlan.Input.NominalSource.Path}|fingerprint={nominalPlan.Input.ExecutionFingerprint}");

            var normalNominalViewModel = new MainWindowViewModel();
            var normalNominalMeshLoads = 0;
            var normalNominalPreviewRequests = 0;
            var normalNominalApplied = NominalActualComparisonRecipeApplyCoordinator.Apply(
                nominalPlan,
                normalNominalViewModel,
                isSmoke: false,
                loadNominalMesh: path =>
                {
                    normalNominalMeshLoads++;
                    return path == nominalSourcePath;
                },
                requestPreview: () => normalNominalPreviewRequests++);
            Check(
                "normal Nominal/Actual apply is state-only",
                normalNominalApplied
                && normalNominalMeshLoads == 1
                && normalNominalPreviewRequests == 0
                && normalNominalViewModel.NominalActualInput == nominalPlan.Input
                && normalNominalViewModel.NominalActual.State == NominalActualComparisonState.InputsReady
                && normalNominalViewModel.NominalActual.PreviewResult is null
                && normalNominalViewModel.ViewerStatus.StartsWith(
                    "Nominal/actual recipe loaded:",
                    StringComparison.Ordinal),
                $"applied={normalNominalApplied}|meshLoads={normalNominalMeshLoads}|previewRequests={normalNominalPreviewRequests}|state={normalNominalViewModel.NominalActual.State}");

            var smokeNominalViewModel = new MainWindowViewModel();
            var smokeNominalMeshLoads = 0;
            var smokeNominalPreviewRequests = 0;
            var smokeNominalApplied = NominalActualComparisonRecipeApplyCoordinator.Apply(
                nominalPlan,
                smokeNominalViewModel,
                isSmoke: true,
                loadNominalMesh: _ =>
                {
                    smokeNominalMeshLoads++;
                    return true;
                },
                requestPreview: () => smokeNominalPreviewRequests++);
            Check(
                "Smoke Nominal/Actual apply requests exactly one explicit Preview",
                smokeNominalApplied
                && smokeNominalMeshLoads == 1
                && smokeNominalPreviewRequests == 1
                && smokeNominalViewModel.RecipeOutputEnabled
                && smokeNominalViewModel.NominalActual.State == NominalActualComparisonState.InputsReady
                && smokeNominalViewModel.ViewerStatus.StartsWith(
                    "Smoke nominal/actual recipe:",
                    StringComparison.Ordinal),
                $"applied={smokeNominalApplied}|meshLoads={smokeNominalMeshLoads}|previewRequests={smokeNominalPreviewRequests}|state={smokeNominalViewModel.NominalActual.State}");

            var disabledNominalPlan = nominalPlan with
            {
                Recipe = nominalPlan.Recipe with { OutputEnabled = false }
            };
            var disabledNominalViewModel = new MainWindowViewModel();
            var disabledNominalPreviewRequests = 0;
            var disabledNominalApplied = NominalActualComparisonRecipeApplyCoordinator.Apply(
                disabledNominalPlan,
                disabledNominalViewModel,
                isSmoke: true,
                loadNominalMesh: _ => true,
                requestPreview: () => disabledNominalPreviewRequests++);
            Check(
                "disabled Nominal/Actual output suppresses Smoke Preview",
                disabledNominalApplied
                && !disabledNominalViewModel.RecipeOutputEnabled
                && disabledNominalPreviewRequests == 0
                && disabledNominalViewModel.NominalActual.State == NominalActualComparisonState.InputsReady
                && disabledNominalViewModel.NominalActual.PreviewResult is null,
                $"applied={disabledNominalApplied}|output={disabledNominalViewModel.RecipeOutputEnabled}|previewRequests={disabledNominalPreviewRequests}|state={disabledNominalViewModel.NominalActual.State}");

            var nominalPreviewRequiredSavePath = Path.Combine(
                reportDirectory,
                "nominal-actual-preview-required.recipe.json");
            var nominalPreviewRequiredSave = NominalActualComparisonRecipeSaveCoordinator.Save(
                nominalPreviewRequiredSavePath,
                isSmoke: false,
                normalNominalViewModel);
            Check(
                "output-enabled Nominal/Actual save rejects missing Preview",
                !nominalPreviewRequiredSave
                && normalNominalViewModel.ViewerStatus ==
                    "Nominal/actual recipe save requires a current completed Preview",
                $"saved={nominalPreviewRequiredSave}|status={normalNominalViewModel.ViewerStatus}");

            var nominalDisabledSavePath = Path.Combine(
                reportDirectory,
                "nominal-actual-disabled-output-save.recipe.json");
            var nominalDisabledSaved = NominalActualComparisonRecipeSaveCoordinator.Save(
                nominalDisabledSavePath,
                isSmoke: true,
                disabledNominalViewModel);
            var savedNominalDisabledRecipe = File.Exists(nominalDisabledSavePath)
                ? NominalActualComparisonRecipe.Load(nominalDisabledSavePath)
                : null;
            var savedNominalDisabledSourcePath = savedNominalDisabledRecipe is null
                ? string.Empty
                : ViewerRecipeFile.Open(nominalDisabledSavePath)
                    .ResolveSourcePath(savedNominalDisabledRecipe.Step.NominalSource.Path);
            Check(
                "disabled-output Nominal/Actual save round-trips identity and state",
                nominalDisabledSaved
                && savedNominalDisabledRecipe is not null
                && !savedNominalDisabledRecipe.OutputEnabled
                && savedNominalDisabledSourcePath == nominalSourcePath
                && disabledNominalViewModel.RecipeSaveSummary ==
                    $"Recipe saved: {Path.GetFullPath(nominalDisabledSavePath)}"
                && disabledNominalViewModel.ViewerStatus.StartsWith(
                    "Smoke nominal/actual recipe saved:",
                    StringComparison.Ordinal),
                $"saved={nominalDisabledSaved}|source={savedNominalDisabledSourcePath}|summary={disabledNominalViewModel.RecipeSaveSummary}");

            var invalidNominalRecipe = nominalPlan.Recipe with
            {
                Step = nominalPlan.Recipe.Step with
                {
                    NominalSource = nominalPlan.Recipe.Step.NominalSource with
                    {
                        Path = "missing-nominal-verification.stl"
                    }
                }
            };
            Check(
                "Nominal/Actual load plan rejects a missing source",
                Throws(() => NominalActualComparisonRecipeLoadPlan.Create(
                    nominalRecipeFile,
                    invalidNominalRecipe)),
                "missing-nominal-verification.stl");

            var lazSourcePath = Path.GetFullPath(
                Path.Combine("3D", "PublicSamples", "PointCloud", "interesting.las"));
            var lazRecipePath = Path.Combine(
                reportDirectory,
                "laz-owner.recipe.json");
            var lazSourceRecipePath = Path.GetRelativePath(
                Path.GetDirectoryName(lazRecipePath)!,
                lazSourcePath).Replace('\\', '/');
            var lazRecipe = new LazTwoPointMeasurementRecipe(
                LazTwoPointMeasurementRecipe.SupportedRecipeType,
                "1.0",
                new HeightDeviationRecipeSource(
                    MainWindowViewModel.LazEntityId,
                    "LAS owner verification sample",
                    lazSourceRecipePath,
                    "source-units"),
                new LazTwoPointMeasurementRecipeMeasurement(
                    "sample-extreme-x",
                    128,
                    "source-z-units"),
                new LazTwoPointMeasurementRecipeAcceptance(
                    1.0,
                    0.1,
                    0.5,
                    0.1),
                OutputEnabled: true);
            lazRecipe.Save(lazRecipePath);
            var lazRecipeFile = ViewerRecipeFile.Open(lazRecipePath);
            var loadedLazRecipe =
                (LazTwoPointMeasurementRecipe)lazRecipeFile.LoadDocument();
            var lazPlan = LazTwoPointRecipeLoadPlan.Create(
                lazRecipeFile,
                loadedLazRecipe);
            var lazPointCloud = LazPointCloud.Load(
                lazPlan.SourcePath,
                loadedLazRecipe.Measurement.MaxSampledPoints);
            Check(
                "LAZ load plan resolves recipe-relative source and decoded input",
                lazPlan.FullRecipePath == Path.GetFullPath(lazRecipePath)
                && lazPlan.SourcePath == lazSourcePath
                && ReferenceEquals(lazPlan.Recipe, loadedLazRecipe)
                && lazPointCloud.SourcePath == lazSourcePath
                && lazPointCloud.SampledPoints.Length > 1,
                $"recipe={lazPlan.FullRecipePath}|source={lazPlan.SourcePath}|sampled={lazPointCloud.SampledPoints.Length}");

            var normalLazViewModel = new MainWindowViewModel();
            var normalLazClearCalls = 0;
            var normalLazSmokeCalls = 0;
            var normalLazApplied = LazTwoPointRecipeApplyCoordinator.Apply(
                lazPlan,
                lazPointCloud,
                normalLazViewModel,
                isSmoke: false,
                clearTransientMeasurement: () => normalLazClearCalls++,
                applySmokeMeasurement: _ => normalLazSmokeCalls++);
            Check(
                "normal LAZ apply is state-only and clears transient points",
                normalLazApplied
                && normalLazClearCalls == 1
                && normalLazSmokeCalls == 0
                && normalLazViewModel.LazSampleVisible
                && normalLazViewModel.LazSampleSourcePath == lazSourcePath
                && normalLazViewModel.ViewerStatus.StartsWith(
                    "LAZ/LAS recipe loaded:",
                    StringComparison.Ordinal),
                $"applied={normalLazApplied}|clear={normalLazClearCalls}|smoke={normalLazSmokeCalls}|visible={normalLazViewModel.LazSampleVisible}");

            var smokeLazViewModel = new MainWindowViewModel();
            var smokeLazClearCalls = 0;
            var smokeLazMeasurementCalls = 0;
            var smokeLazApplied = LazTwoPointRecipeApplyCoordinator.Apply(
                lazPlan,
                lazPointCloud,
                smokeLazViewModel,
                isSmoke: true,
                clearTransientMeasurement: () => smokeLazClearCalls++,
                applySmokeMeasurement: _ =>
                {
                    smokeLazMeasurementCalls++;
                    smokeLazViewModel.UseLazPointSmokeScene();
                });
            Check(
                "Smoke LAZ apply requests exactly one explicit measurement",
                smokeLazApplied
                && smokeLazClearCalls == 0
                && smokeLazMeasurementCalls == 1
                && smokeLazViewModel.RecipeOutputEnabled
                && smokeLazViewModel.LazSampleVisible
                && smokeLazViewModel.ViewerStatus.StartsWith(
                    "Smoke LAZ/LAS recipe:",
                    StringComparison.Ordinal),
                $"applied={smokeLazApplied}|clear={smokeLazClearCalls}|measurement={smokeLazMeasurementCalls}|visible={smokeLazViewModel.LazSampleVisible}");

            var disabledLazPlan = lazPlan with
            {
                Recipe = lazPlan.Recipe with { OutputEnabled = false }
            };
            var disabledLazViewModel = new MainWindowViewModel();
            var disabledLazClearCalls = 0;
            var disabledLazMeasurementCalls = 0;
            var disabledLazApplied = LazTwoPointRecipeApplyCoordinator.Apply(
                disabledLazPlan,
                lazPointCloud,
                disabledLazViewModel,
                isSmoke: true,
                clearTransientMeasurement: () => disabledLazClearCalls++,
                applySmokeMeasurement: _ => disabledLazMeasurementCalls++);
            Check(
                "disabled LAZ output suppresses Smoke measurement and resets points",
                disabledLazApplied
                && !disabledLazViewModel.RecipeOutputEnabled
                && disabledLazClearCalls == 1
                && disabledLazMeasurementCalls == 0
                && disabledLazViewModel.LazSampleVisible,
                $"applied={disabledLazApplied}|output={disabledLazViewModel.RecipeOutputEnabled}|clear={disabledLazClearCalls}|measurement={disabledLazMeasurementCalls}");

            var lazPreviewRequiredSavePath = Path.Combine(
                reportDirectory,
                "laz-preview-required.recipe.json");
            var lazPreviewRequiredSave = LazTwoPointRecipeSaveCoordinator.Save(
                lazPreviewRequiredSavePath,
                isSmoke: false,
                normalLazViewModel,
                lazPointCloud,
                hasMeasuredPair: false,
                setValidationOk: () => { });
            Check(
                "output-enabled LAZ save rejects an unmeasured pair",
                !lazPreviewRequiredSave
                && normalLazViewModel.ViewerStatus ==
                    "LAZ/LAS two-point recipe save requires a measured LAZ/LAS pair",
                $"saved={lazPreviewRequiredSave}|status={normalLazViewModel.ViewerStatus}");

            var lazDisabledSavePath = Path.Combine(
                reportDirectory,
                "laz-disabled-output-save.recipe.json");
            var lazValidationCalls = 0;
            var lazDisabledSaved = LazTwoPointRecipeSaveCoordinator.Save(
                lazDisabledSavePath,
                isSmoke: true,
                disabledLazViewModel,
                lazPointCloud,
                hasMeasuredPair: false,
                setValidationOk: () => lazValidationCalls++);
            var savedLazDisabledRecipe = File.Exists(lazDisabledSavePath)
                ? LazTwoPointMeasurementRecipe.Load(lazDisabledSavePath)
                : null;
            var savedLazSourcePath = savedLazDisabledRecipe is null
                ? string.Empty
                : ViewerRecipeFile.Open(lazDisabledSavePath)
                    .ResolveSourcePath(savedLazDisabledRecipe.Source.Path);
            Check(
                "disabled-output LAZ save round-trips source and validation state",
                lazDisabledSaved
                && lazValidationCalls == 1
                && savedLazDisabledRecipe is not null
                && !savedLazDisabledRecipe.OutputEnabled
                && savedLazSourcePath == lazSourcePath
                && disabledLazViewModel.RecipeSaveSummary ==
                    $"Recipe saved: {Path.GetFullPath(lazDisabledSavePath)}"
                && disabledLazViewModel.ViewerStatus.StartsWith(
                    "Smoke LAZ recipe saved:",
                    StringComparison.Ordinal),
                $"saved={lazDisabledSaved}|validation={lazValidationCalls}|source={savedLazSourcePath}");

            var missingLazPath = Path.Combine(
                reportDirectory,
                "missing-owner.las");
            var corruptLazPath = Path.GetFullPath(
                Path.Combine("3D", "PublicSamples", "Invalid", "corrupt.laz"));
            Check(
                "LAZ data bridge fails closed for missing and corrupt sources",
                Throws(() => LazPointCloud.Load(missingLazPath, 128))
                && Throws(() => LazPointCloud.Load(corruptLazPath, 128)),
                $"missing={missingLazPath}|corrupt={corruptLazPath}");

            summary = $"Viewer recipe workflow verification: Pass ({passed} checks)";
            lines.Add(summary);
            WriteReport(reportPath, lines);
            return true;
        }
        catch (Exception exception)
        {
            summary = $"Viewer recipe workflow verification: Fail after {passed} checks: {exception.Message}";
            lines.Add(summary);
            WriteReport(reportPath, lines);
            return false;
        }

        void Check(string name, bool condition, string detail)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"{name}: {detail}");
            }

            passed++;
            lines.Add($"PASS|{name}|{detail}");
        }
    }

    private static bool Throws(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }

    private static void WriteReport(string reportPath, IEnumerable<string> lines)
    {
        var fullPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllLines(fullPath, lines);
    }
}
