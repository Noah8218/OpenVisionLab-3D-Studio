using System.Diagnostics;
using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

internal static class ShellRecipeLifecycleSmoke
{
    public static async Task<bool> RunNewAsync(
        ShellMainWindowViewModel viewModel,
        OpenVisionThreeDViewerControl viewer,
        string createdRecipePath,
        string? reportPath,
        Action showRecipeManager,
        Func<Task<bool>> clickDoNotSaveAsync)
    {
        showRecipeManager();
        viewModel.Workbench.RecipeName = "Discard this current draft";
        var doNotSaveClick = clickDoNotSaveAsync();
        viewModel.Workbench.NewTeachingRecipeCommand.Execute(null);
        var clickedDoNotSave = await doNotSaveClick;
        var createdPath = Path.GetFullPath(createdRecipePath);
        ToolRecipeDocument? createdDocument = null;
        if (File.Exists(createdPath))
        {
            createdDocument = ToolRecipeDocumentStore.Load(createdPath);
        }

        var passed = clickedDoNotSave
            && createdDocument is not null
            && createdDocument.Steps.Count == 0
            && string.IsNullOrWhiteSpace(createdDocument.Source.Path)
            && string.Equals(viewModel.Workbench.RecipePath, createdPath, StringComparison.OrdinalIgnoreCase)
            && !viewModel.Workbench.IsSourceReadyForRecipe
            && viewer.CurrentC3DSourcePath is null
            && !viewModel.Workbench.IsDirty;
        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            var fullReportPath = Path.GetFullPath(reportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
            File.WriteAllLines(fullReportPath,
            [
                "OpenVisionLab 3D actual EXE new-recipe lifecycle smoke",
                $"Result: {(passed ? "Pass" : "Fail")}",
                $"DoNotSaveButtonClicked: {clickedDoNotSave}",
                $"RecipePath: {viewModel.Workbench.RecipePath}",
                $"RecipeExists: {File.Exists(createdPath)}",
                $"StepCount: {createdDocument?.Steps.Count}",
                $"SourcePath: {createdDocument?.Source.Path}",
                $"SourceReady: {viewModel.Workbench.IsSourceReadyForRecipe}",
                $"ViewerSourcePath: {viewer.CurrentC3DSourcePath}",
                $"IsDirty: {viewModel.Workbench.IsDirty}"
            ]);
        }

        return passed;
    }

    public static bool RunOpen(
        ShellMainWindowViewModel viewModel,
        string recipePath,
        string? reportPath,
        Action showRecipeManager,
        Action<string> openRecipe,
        Func<bool> isRecipeManagerVisible,
        Func<string, bool> isViewerSourceAlreadyLoaded)
    {
        showRecipeManager();
        var stopwatch = Stopwatch.StartNew();
        openRecipe(recipePath);
        stopwatch.Stop();

        var expectedPath = Path.GetFullPath(recipePath);
        var passed = string.Equals(viewModel.Workbench.RecipePath, expectedPath, StringComparison.OrdinalIgnoreCase)
            && !viewModel.Workbench.IsDirty
            && !isRecipeManagerVisible();
        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            var fullReportPath = Path.GetFullPath(reportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
            File.WriteAllLines(fullReportPath,
            [
                "OpenVisionLab 3D actual EXE open-recipe lifecycle smoke",
                $"Result: {(passed ? "Pass" : "Fail")}",
                $"OpenWorkbenchRecipeMilliseconds: {stopwatch.ElapsedMilliseconds}",
                $"RecipePath: {viewModel.Workbench.RecipePath}",
                $"IsDirty: {viewModel.Workbench.IsDirty}",
                $"RecipeManagerVisible: {isRecipeManagerVisible()}",
                $"ViewerSourceReused: {isViewerSourceAlreadyLoaded(viewModel.Workbench.Source.Path)}"
            ]);
        }

        return passed;
    }
}
