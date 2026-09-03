using System.Diagnostics;
using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

internal static class ShellRecipeLifecycleSmoke
{
    public static async Task<bool> RunNewAsync(
        ShellMainWindowViewModel viewModel,
        OpenVisionThreeDViewerControl viewer,
        string createdRecipePath,
        string sourcePath,
        string starterId,
        string? reportPath,
        Action showRecipeManager,
        Func<Task<bool>> clickDoNotSaveAsync)
    {
        showRecipeManager();
        viewModel.Workbench.RecipeName = "Discard this current draft";
        viewModel.Workbench.NewTeachingRecipeCommand.Execute(null);
        var createdPath = Path.GetFullPath(createdRecipePath);
        var fullSourcePath = Path.GetFullPath(sourcePath);
        viewModel.Workbench.FirstRecipeName = GetRecipeName(createdPath);
        viewModel.Workbench.FirstRecipeFolderPath = Path.GetDirectoryName(createdPath)!;
        viewModel.Workbench.FirstRecipeSourcePath = fullSourcePath;
        viewModel.Workbench.SelectedFirstRecipeStarter = viewModel.Workbench.FirstRecipeStarterOptions
            .Single(option => option.Id == starterId);
        viewModel.Workbench.RememberFirstRecipeSetup = false;
        var doNotSaveClick = clickDoNotSaveAsync();
        viewModel.Workbench.CreateFirstRecipeCommand.Execute(null);
        var clickedDoNotSave = await doNotSaveClick;
        for (var attempt = 0; attempt < 200
             && (!File.Exists(createdPath) || viewModel.Workbench.IsFirstRecipeSetupVisible); attempt++)
        {
            await Task.Delay(25);
        }
        ToolRecipeDocument? createdDocument = null;
        if (File.Exists(createdPath))
        {
            createdDocument = ToolRecipeDocumentStore.Load(createdPath);
        }

        var expectedStepCount = starterId == ToolWorkbenchViewModel.EmptyFirstRecipeStarterId ? 0 : 1;
        var passed = clickedDoNotSave
            && createdDocument is not null
            && createdDocument.Steps.Count == expectedStepCount
            && (expectedStepCount == 0 || createdDocument.Steps[0].ToolId == "thickness")
            && string.Equals(createdDocument.Source.Path, fullSourcePath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(viewModel.Workbench.RecipePath, createdPath, StringComparison.OrdinalIgnoreCase)
            && viewModel.Workbench.IsSourceReadyForRecipe
            && string.Equals(viewer.CurrentC3DSourcePath, fullSourcePath, StringComparison.OrdinalIgnoreCase)
            && !viewModel.Workbench.IsFirstRecipeSetupVisible
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
                $"StarterId: {starterId}",
                $"StepCount: {createdDocument?.Steps.Count}",
                $"FirstStepToolId: {createdDocument?.Steps.FirstOrDefault()?.ToolId}",
                $"SourcePath: {createdDocument?.Source.Path}",
                $"ExpectedSourcePath: {fullSourcePath}",
                $"SourceReady: {viewModel.Workbench.IsSourceReadyForRecipe}",
                $"ViewerSourcePath: {viewer.CurrentC3DSourcePath}",
                $"SetupVisible: {viewModel.Workbench.IsFirstRecipeSetupVisible}",
                $"IsDirty: {viewModel.Workbench.IsDirty}"
            ]);
        }

        return passed;
    }

    private static string GetRecipeName(string path)
    {
        const string suffix = ".ov3d-recipe.json";
        var fileName = Path.GetFileName(path);
        return fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? fileName[..^suffix.Length]
            : Path.GetFileNameWithoutExtension(fileName);
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

    public static bool RunWindowLifetime(
        Action showRecipeManager,
        Action closeRecipeManager,
        Func<object?> getRecipeManagerWindow,
        Func<bool> isRecipeManagerVisible,
        Action disposeLifecycle,
        string? reportPath)
    {
        showRecipeManager();
        var firstWindow = getRecipeManagerWindow();
        closeRecipeManager();
        var hiddenKeepsInstance = firstWindow is not null
            && ReferenceEquals(firstWindow, getRecipeManagerWindow())
            && !isRecipeManagerVisible();

        showRecipeManager();
        var reopenedSameInstance = firstWindow is not null
            && ReferenceEquals(firstWindow, getRecipeManagerWindow())
            && isRecipeManagerVisible();

        disposeLifecycle();
        var disposedClearsWindow = getRecipeManagerWindow() is null;
        showRecipeManager();
        var disposedRejectsShow = getRecipeManagerWindow() is null;
        var passed = hiddenKeepsInstance && reopenedSameInstance && disposedClearsWindow && disposedRejectsShow;
        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            var fullReportPath = Path.GetFullPath(reportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
            File.AppendAllLines(fullReportPath,
            [
                $"RecipeManagerWindowLifetime|hiddenKeepsInstance={hiddenKeepsInstance}|reopenedSameInstance={reopenedSameInstance}|disposedClearsWindow={disposedClearsWindow}|disposedRejectsShow={disposedRejectsShow}|pass={passed}"
            ]);
        }

        return passed;
    }
}
