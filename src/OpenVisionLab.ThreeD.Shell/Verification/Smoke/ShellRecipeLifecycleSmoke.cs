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
             && (!File.Exists(createdPath)
                 || viewModel.Workbench.IsFirstRecipeSetupVisible
                 || !string.Equals(viewModel.Workbench.Source.Path, fullSourcePath, StringComparison.OrdinalIgnoreCase)); attempt++)
        {
            await Task.Delay(25);
        }
        ToolRecipeDocument? createdDocument = null;
        if (File.Exists(createdPath))
        {
            createdDocument = ToolRecipeDocumentStore.Load(createdPath);
        }

        var persistedSourcePath = createdDocument is null || string.IsNullOrWhiteSpace(createdDocument.Source.Path)
            ? string.Empty
            : Path.IsPathFullyQualified(createdDocument.Source.Path)
                ? Path.GetFullPath(createdDocument.Source.Path)
                : Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(createdPath) ?? Environment.CurrentDirectory,
                    createdDocument.Source.Path));
        var passed = clickedDoNotSave
            && createdDocument is not null
            && createdDocument.Steps.Count == 0
            && string.IsNullOrWhiteSpace(persistedSourcePath)
            && string.Equals(viewModel.Workbench.Source.Path, fullSourcePath, StringComparison.OrdinalIgnoreCase)
            && (starterId == ToolWorkbenchViewModel.EmptyFirstRecipeStarterId
                || viewModel.Workbench.PipelineSteps.Any(step => step.ToolId == "thickness"))
            && string.Equals(viewModel.Workbench.RecipePath, createdPath, StringComparison.OrdinalIgnoreCase)
            && viewModel.Workbench.IsSourceReadyForRecipe
            && string.Equals(viewer.CurrentC3DSourcePath, fullSourcePath, StringComparison.OrdinalIgnoreCase)
            && !viewModel.Workbench.IsFirstRecipeSetupVisible
            && viewModel.Workbench.IsDirty;
        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            var fullReportPath = PrepareReportPath(reportPath);
            File.WriteAllLines(fullReportPath,
            [
                "OpenVisionLab 3D actual EXE new-recipe lifecycle smoke",
                $"Result: {(passed ? "Pass" : "Fail")}",
                $"DoNotSaveButtonClicked: {clickedDoNotSave}",
                $"RecipePath: {viewModel.Workbench.RecipePath}",
                $"RecipeExists: {File.Exists(createdPath)}",
                $"StarterId: {starterId}",
                $"SavedStepCount: {createdDocument?.Steps.Count}",
                $"CurrentStepCount: {viewModel.Workbench.PipelineSteps.Count}",
                $"SourcePath: {createdDocument?.Source.Path}",
                $"CurrentSourcePath: {viewModel.Workbench.Source.Path}",
                $"SourceReady: {viewModel.Workbench.IsSourceReadyForRecipe}",
                $"ViewerSourcePath: {viewer.CurrentC3DSourcePath}",
                $"SetupVisible: {viewModel.Workbench.IsFirstRecipeSetupVisible}",
                $"IsDirty: {viewModel.Workbench.IsDirty}",
                $"PersistedSourcePath: {persistedSourcePath}"
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
            var fullReportPath = PrepareReportPath(reportPath);
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
        Action hideRecipeManager,
        Action closeRecipeManager,
        Func<object?> getRecipeManagerWindow,
        Func<bool> isRecipeManagerVisible,
        Func<double> getOwnerOpacity,
        Func<bool> isOwnerEnabled,
        Action disposeLifecycle,
        string? reportPath)
    {
        closeRecipeManager();
        var initialOwnerOpacity = getOwnerOpacity();
        var initialOwnerEnabled = isOwnerEnabled();
        var ownerStartsRestored = initialOwnerEnabled
            && Math.Abs(initialOwnerOpacity - 1d) < 0.001;
        showRecipeManager();
        var firstWindow = getRecipeManagerWindow();
        var firstShowDimsOwner = isRecipeManagerVisible()
            && !isOwnerEnabled()
            && Math.Abs(getOwnerOpacity() - 0.4) < 0.001;

        hideRecipeManager();
        var hiddenKeepsInstance = firstWindow is not null
            && ReferenceEquals(firstWindow, getRecipeManagerWindow())
            && !isRecipeManagerVisible()
            && isOwnerEnabled() == initialOwnerEnabled
            && Math.Abs(getOwnerOpacity() - initialOwnerOpacity) < 0.001;

        showRecipeManager();
        var reopenedSameInstance = firstWindow is not null
            && ReferenceEquals(firstWindow, getRecipeManagerWindow())
            && isRecipeManagerVisible()
            && !isOwnerEnabled()
            && Math.Abs(getOwnerOpacity() - 0.4) < 0.001;

        closeRecipeManager();
        var closeHidesAndRestoresOwner = firstWindow is not null
            && ReferenceEquals(firstWindow, getRecipeManagerWindow())
            && !isRecipeManagerVisible()
            && isOwnerEnabled() == initialOwnerEnabled
            && Math.Abs(getOwnerOpacity() - initialOwnerOpacity) < 0.001;

        showRecipeManager();
        var closeReopensSameWindow = firstWindow is not null
            && ReferenceEquals(firstWindow, getRecipeManagerWindow())
            && isRecipeManagerVisible()
            && !isOwnerEnabled()
            && Math.Abs(getOwnerOpacity() - 0.4) < 0.001;

        disposeLifecycle();
        var disposedClearsWindow = getRecipeManagerWindow() is null
            && isOwnerEnabled() == initialOwnerEnabled
            && Math.Abs(getOwnerOpacity() - initialOwnerOpacity) < 0.001;
        showRecipeManager();
        var disposedRejectsShow = getRecipeManagerWindow() is null;
        var passed = ownerStartsRestored
            && firstShowDimsOwner
            && hiddenKeepsInstance
            && reopenedSameInstance
            && closeHidesAndRestoresOwner
            && closeReopensSameWindow
            && disposedClearsWindow
            && disposedRejectsShow;
        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            var fullReportPath = PrepareReportPath(reportPath);
            File.AppendAllLines(fullReportPath,
            [
                $"RecipeManagerWindowLifetime|ownerStartsRestored={ownerStartsRestored};opacity={initialOwnerOpacity:F3};enabled={initialOwnerEnabled}|firstShowDimsOwner={firstShowDimsOwner}|hiddenKeepsInstance={hiddenKeepsInstance}|reopenedSameInstance={reopenedSameInstance}|closeHidesAndRestoresOwner={closeHidesAndRestoresOwner}|closeReopensSameWindow={closeReopensSameWindow}|disposedClearsWindow={disposedClearsWindow}|disposedRejectsShow={disposedRejectsShow}|pass={passed}"
            ]);
        }

        return passed;
    }

    private static string PrepareReportPath(string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        return fullReportPath;
    }
}
