using System.IO;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

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
