using System.IO;
using System.Windows;
using System.Threading;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.Coordination;
using OpenVisionLab.ThreeD.Shell.Dialogs;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Verification;

internal static class ShellSourceReloadVerification
{
    public static bool VerifyOpenedRecipeSource(string reportPath, out string summary)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        Directory.CreateDirectory(root);
        var lines = new List<string> { "Boundary|Existing recipe/source owners and attached WPF bindings; no inspection or SDK numerical evaluation. Fixtures are retained." };
        var passed = 0;
        var total = 0;
        void Check(string name, bool condition, string detail)
        {
            total++;if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }
        try
        {
            foreach (var mode in new[] { ShellWorkspaceMode.Inspect, ShellWorkspaceMode.Validate })
            {
                var folder = Path.Combine(root, mode.ToString());Directory.CreateDirectory(folder);
                var oldPath = Path.Combine(folder, "previous.C3D");var incomingPath = Path.Combine(folder, "incoming.C3D");
                CreateSource([1, 2, 3, 4, 5, 6, 7, 8, 9]).SaveC3D(oldPath);
                CreateSource([21, 22, 23, 24, 25, 26, 27, 28, 29]).SaveC3D(incomingPath);
                var oldBytes = File.ReadAllBytes(oldPath);var incomingBytes = File.ReadAllBytes(incomingPath);
                using var viewer = new OpenVisionThreeDViewerControl(loadDefaultSamples: false);
                using var model = new ShellMainWindowViewModel(recentRecipesPath: Path.Combine(folder, "recent.json"));
                using var sourceLoad = new ShellWorkbenchSourceLoadCoordinator(new ShellSourceFileDialogService(() => throw new InvalidOperationException("No modal dialog is expected.")), viewer, model,
                    new ShellWorkbenchSourceLoadCallbacks { ShowLoadSourceFailure = message => throw new InvalidOperationException(message) });
                using var teaching = new WorkbenchViewerTeachingCoordinator(model.Workbench, viewer, () => { });
                model.Workbench.SetC3DSource(oldPath, markDirty: false);
                var previousRecipe = Path.Combine(folder, "previous.ov3d-recipe.json");
                Check($"{mode} previous recipe saves", model.Workbench.TrySaveTeachingRecipe(previousRecipe, out var message), message);
                var binding = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(incomingPath);
                var document = ToolRecipeDocumentStore.Load(previousRecipe);
                var incoming = document with { Name = "Incoming recipe", Source = document.Source with { Id = "source.incoming", Name = "Declared incoming source", Path = incomingPath,
                    ByteLength = incomingBytes.Length, ContentSha256 = binding.ContentSha256, GridWidth = binding.GridWidth, GridHeight = binding.GridHeight } };
                var incomingRecipe = Path.Combine(folder, "incoming.ov3d-recipe.json");ToolRecipeDocumentStore.Save(incomingRecipe, incoming);
                model.Workbench.InspectionQueue.AddSources([oldPath]);model.Workbench.InspectionQueue.SelectedSample = model.Workbench.InspectionQueue.Samples[0];
                model.Workbench.SetValidationSetSources([oldPath]);
                model.SelectWorkspaceCommand.Execute(mode);
                using var workspace = new ViewerWorkspaceView { DataContext = model.Workbench, MainViewerContent = viewer };
                sourceLoad.LoadC3DSource(oldPath);
                void Fail(string text) => lines.Add("INFO | lifecycle failure callback | " + text);
                using var lifecycle = new ShellWorkbenchLifecycleController(null!, model, new RecipeFileDialogService(() => throw new InvalidOperationException("No modal dialog is expected.")), new ShellSourceFileDialogService(() => throw new InvalidOperationException("No modal dialog is expected.")), teaching, sourceLoad,
                    new ShellWorkbenchLifecycleCallbacks { ShowLoadSourceFailure = Fail, ShowRecipeSaveFailure = Fail, ShowFirstRecipeCreateFailure = Fail,
                        ShowFirstRecipeSetupPersistenceFailure = Fail, ShowRecipeFileUnavailable = _ => { }, ShowRecipeOpenFailure = Fail,
                        ShowRecipeSourceNotReady = () => Fail("Source not ready"), ShowRecipeSourceLoadFailure = Fail, ShowParameterApplyFailure = Fail,
                        ConfirmUnsavedRecipeChanges = () => ShellLifecycleDialogChoice.Cancel, ConfirmPendingParameterChanges = () => ShellLifecycleDialogChoice.Cancel,
                        CommitPendingParameterEdit = () => (true, ""), DiscardPendingParameterChanges = () => { }, ActivateWorkbench = () => { }, DialogText = (_, _, english) => english });
                lifecycle.OpenWorkbenchRecipe(incomingRecipe);
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                Check($"{mode} successful open selects Teach before applying the source", model.IsTeachWorkspaceSelected && !model.Workbench.IsReadOnlySampleView, model.SelectedWorkspaceMode.ToString());
                sourceLoad.TryGetCurrentC3DSourceBinding(incomingPath, out var observedBinding);
                lines.Add("INFO | targets=" + string.Join(";", model.Workbench.MainViewerCandidates.Select(item => item.Id + "=" + item.SourcePath)) + ";artifacts=" + string.Join(";", model.Workbench.ArtifactRegistry.Select(item => item.Id + "=" + item.State + "/" + item.ContentSha256)));
                lines.Add($"INFO | expected={System.Text.Json.JsonSerializer.Serialize(model.Workbench.SourceSession.SourceBinding)};actual={System.Text.Json.JsonSerializer.Serialize(observedBinding)};viewer={viewer.CurrentC3DSourcePath}");
                Check($"{mode} declared input and Viewer agree", model.Workbench.Source.Path == incomingPath && viewer.CurrentC3DSourcePath == incomingPath
                    && model.Workbench.Source.Name == incoming.Source.Name && model.Workbench.Source.Id == incoming.Source.Id && !model.Workbench.IsDirty,
                    $"source={model.Workbench.Source.Path};viewer={viewer.CurrentC3DSourcePath};dirty={model.Workbench.IsDirty}");
                var savedPath = Path.Combine(folder, "saved.ov3d-recipe.json");
                Check($"{mode} source survives Save As", model.Workbench.TrySaveTeachingRecipe(savedPath, out message)
                    && ToolRecipeDocumentStore.Load(savedPath).Source.ContentSha256 == binding.ContentSha256
                    && model.Workbench.Source.Unit == incoming.Source.Unit && model.Workbench.Source.FrameId == incoming.Source.FrameId, message);
                Check($"{mode} save/reopen retains the incoming source", model.Workbench.TryOpenTeachingRecipe(savedPath, out message)
                    && model.Workbench.Source.Path == incomingPath && model.Workbench.SourceSession.SourceBinding?.ContentSha256 == binding.ContentSha256, message);
                var queued = model.Workbench.InspectionQueue.SelectedSample;
                var sourceAdapter = new ShellWorkbenchRecipeSourceCoordinator(sourceLoad, model.Workbench, teaching, model.UpdateC3DSampleVisible);
                CreateSource([41, 42, 43, 44, 45, 46, 47, 48, 49]).SaveC3D(incomingPath);
                var changed = sourceAdapter.ApplyOpenedRecipeSource();
                Check($"{mode} changed bytes cannot replace the opened recipe identity", !changed.IsReady
                    && model.Workbench.SourceSession.SourceBinding?.ContentSha256 == binding.ContentSha256 && !model.Workbench.IsDirty
                    && model.Workbench.Source.Path == incomingPath, changed.FailureMessage ?? "Controlled source rejection");
                File.WriteAllBytes(incomingPath, [0, 1, 2]);
                var unreadable = sourceAdapter.ApplyOpenedRecipeSource();
                Check($"{mode} unreadable source is rejected without adopting the old Viewer input", !unreadable.IsReady
                    && model.Workbench.Source.Path == incomingPath && model.Workbench.SourceSession.SourceBinding?.ContentSha256 == binding.ContentSha256, unreadable.FailureMessage ?? "Not ready");
                File.WriteAllBytes(incomingPath, incomingBytes);
                var recovered = sourceAdapter.ApplyOpenedRecipeSource();
                Check($"{mode} original bytes recover without Run or queue mutation", recovered.IsReady && viewer.CurrentC3DSourcePath == incomingPath
                    && ReferenceEquals(queued, model.Workbench.InspectionQueue.SelectedSample) && oldBytes.SequenceEqual(File.ReadAllBytes(oldPath))
                    && incomingBytes.SequenceEqual(File.ReadAllBytes(incomingPath)) && !model.Workbench.IsOrderedRunRunning
                    && model.Workbench.RunLog.All(row => row.Category is not ("Run" or "Preview" or "Publish")), recovered.FailureMessage ?? "Recovered");
                var modeBeforeFailure = model.SelectedWorkspaceMode;var sourceBeforeFailure = model.Workbench.Source.Path;
                lifecycle.OpenWorkbenchRecipe(Path.Combine(folder, "missing.ov3d-recipe.json"));
                Check($"{mode} missing recipe does not change the current input or mode", model.SelectedWorkspaceMode == modeBeforeFailure && model.Workbench.Source.Path == sourceBeforeFailure, "Preflight failure retains context");
            }
        }
        catch (Exception exception) { total++;lines.Add("FAIL | unexpected | " + exception); }
        var ok = passed == total;
        summary = $"Opened recipe source verification: {(ok ? "Pass" : "Fail")} ({passed} checks)";
        File.WriteAllLines(reportPath, lines.Append(summary));return ok;
    }

    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var fullReportPath = Path.GetFullPath(reportPath);
        var reportDirectory = Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory;
        var fixtureRoot = Path.Combine(reportDirectory, $"source-reload-{Guid.NewGuid():N}");
        var lines = new List<string>
        {
            "OpenVisionLab 3D same-path C3D reload verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Boundary|Shell admits the existing source only when the current file identity still matches the Viewer binding; a changed or unreadable file is reloaded through the existing Viewer owner.",
        };
        var passed = 0;
        var total = 0;

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        try
        {
            Check(
                "verification-runs-on-sta",
                Thread.CurrentThread.GetApartmentState() == ApartmentState.STA,
                Thread.CurrentThread.GetApartmentState().ToString());

            Directory.CreateDirectory(fixtureRoot);
            var sourcePath = Path.Combine(fixtureRoot, "same-path.c3d");
            var original = CreateSource([1, 2, 3, 4, 5, 6, 7, 8, 9]);
            original.SaveC3D(sourcePath);
            var sameByteLength = new FileInfo(sourcePath).Length;

            using var viewer = new OpenVisionThreeDViewerControl(loadDefaultSamples: false);
            using var viewModel = new ShellMainWindowViewModel(
                recentRecipesPath: Path.Combine(fixtureRoot, "recent-recipes.json"));
            using var coordinator = new ShellWorkbenchSourceLoadCoordinator(
                new ShellSourceFileDialogService(() => throw new InvalidOperationException("The dialog is not used by this verification.")),
                viewer,
                viewModel,
                new ShellWorkbenchSourceLoadCallbacks
                {
                    ShowLoadSourceFailure = message => lines.Add($"INFO | load-failure-callback | {message}"),
                });

            var initialLoad = coordinator.LoadC3DSource(sourcePath);
            var currentBindingAvailable = viewer.TryGetCurrentC3DSourceBinding(sourcePath, out var initialBinding);
            Check(
                "initial-source-load-establishes-viewer-binding",
                initialLoad && currentBindingAvailable,
                $"loaded={initialLoad};binding={currentBindingAvailable};sha={initialBinding?.ContentSha256 ?? "(none)"}");

            var sameBytesShortcut = coordinator.IsViewerSourceAlreadyLoaded(sourcePath);
            Check(
                "same-path-and-same-bytes-uses-existing-binding",
                sameBytesShortcut,
                $"shortcut={sameBytesShortcut};fileLength={new FileInfo(sourcePath).Length}");

            var sameContent = CreateSource([1, 2, 3, 4, 5, 6, 7, 8, 9]);
            sameContent.SaveC3D(sourcePath);
            var sameBytesAfterRewrite = coordinator.IsViewerSourceAlreadyLoaded(sourcePath);
            Check(
                "same-path-byte-identical-rewrite-remains-current",
                sameBytesAfterRewrite,
                $"shortcut={sameBytesAfterRewrite};fileLength={new FileInfo(sourcePath).Length};sameLength={new FileInfo(sourcePath).Length == sameByteLength}");

            var changedContent = CreateSource([101, 102, 103, 104, 105, 106, 107, 108, 109]);
            changedContent.SaveC3D(sourcePath);
            var changedFileLength = new FileInfo(sourcePath).Length;
            var changedPathRequiresReload = !coordinator.IsViewerSourceAlreadyLoaded(sourcePath);
            Check(
                "same-path-different-content-invalidates-shortcut",
                changedPathRequiresReload && changedFileLength == sameByteLength,
                $"reloadRequired={changedPathRequiresReload};changedLength={changedFileLength};originalLength={sameByteLength}");

            var reloaded = coordinator.LoadC3DSource(sourcePath);
            var reloadedBindingAvailable = viewer.TryGetCurrentC3DSourceBinding(sourcePath, out var reloadedBinding);
            var diskBindingAfterReload = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(sourcePath);
            Check(
                "changed-source-reloads-through-existing-viewer-owner",
                reloaded
                && reloadedBindingAvailable
                && initialBinding is not null
                && !ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(initialBinding, reloadedBinding)
                && ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(reloadedBinding, diskBindingAfterReload),
                $"reloaded={reloaded};binding={reloadedBindingAvailable};oldSha={initialBinding?.ContentSha256 ?? "(none)"};newSha={reloadedBinding?.ContentSha256 ?? "(none)"}");

            var retainedBinding = reloadedBinding;
            coordinator.SetWorkbenchC3DSourceFromViewer(sourcePath);
            using (var workspaceView = new ViewerWorkspaceView { DataContext = viewModel.Workbench, MainViewerContent = viewer })
            {
                var replacementPath = Path.Combine(fixtureRoot, "replacement.C3D");
                CreateSource([21, 22, 23, 24, 25, 26, 27, 28, 29]).SaveC3D(replacementPath);
                var failureCount = lines.Count(line => line.StartsWith("INFO | load-failure-callback", StringComparison.Ordinal));
                var replacementLoaded = Await(coordinator.LoadWorkbenchC3DSourceAsync(replacementPath));
                Check("attached workspace admits an explicit source replacement without an old-source refresh",
                    replacementLoaded && viewer.CurrentC3DSourcePath == replacementPath && viewModel.Workbench.Source.Path == replacementPath
                    && !viewModel.Workbench.IsC3DSourceLoading && !viewModel.Workbench.IsOrderedRunRunning
                    && lines.Count(line => line.StartsWith("INFO | load-failure-callback", StringComparison.Ordinal)) == failureCount,
                    $"loaded={replacementLoaded};viewer={viewer.CurrentC3DSourcePath};source={viewModel.Workbench.Source.Path}");
                var restored = Await(coordinator.LoadWorkbenchC3DSourceAsync(sourcePath));
                Check("attached workspace can restore the prior source without execution",
                    restored && viewer.CurrentC3DSourcePath == sourcePath && viewModel.Workbench.Source.Path == sourcePath
                    && !viewModel.Workbench.IsC3DSourceLoading && !viewModel.Workbench.IsSelectedStepPreviewRunning,
                    $"restored={restored};viewer={viewer.CurrentC3DSourcePath};source={viewModel.Workbench.Source.Path}");
            }
            viewModel.Workbench.Source.Id = "source.variant.coordinator";
            var originalRecipePath = Path.Combine(fixtureRoot, "original.ov3d-recipe.json");
            viewModel.Workbench.TrySaveTeachingRecipe(originalRecipePath, out _);
            using var teaching = new WorkbenchViewerTeachingCoordinator(viewModel.Workbench, viewer, () => { });
            var recipeSource = new ShellWorkbenchRecipeSourceCoordinator(coordinator, viewModel.Workbench, teaching, viewModel.UpdateC3DSampleVisible);
            var variantPath = Path.Combine(fixtureRoot, "variant.C3D");
            CreateSource([11, 12, 13, 14, 15, 16, 17, 18, 19]).SaveC3D(variantPath);
            viewModel.Workbench.BeginCompatibleSourceVariantSetup();
            viewModel.Workbench.FirstRecipeName = "variant";
            viewModel.Workbench.FirstRecipeSourcePath = variantPath;
            viewModel.Workbench.IsVariantRoiReuseConfirmed = true;
            if (!viewModel.Workbench.TryGetFirstRecipeSetup(out var setup, out var setupMessage)) throw new InvalidOperationException(setupMessage);
            var preparation = Await(recipeSource.PrepareNewRecipeSourceAsync(setup, CancellationToken.None));
            Check("variant preparation reads identity without replacing Viewer or Workbench input",
                preparation.IsReady && preparation.VariantBinding?.ContentSha256 == ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(variantPath).ContentSha256
                && viewer.CurrentC3DSourcePath == sourcePath && viewModel.Workbench.Source.Path == sourcePath
                && viewModel.Workbench.RecipePath == originalRecipePath && coordinator.IsViewerSourceAlreadyLoaded(sourcePath), setupMessage);
            var malformedPath = Path.Combine(fixtureRoot, "malformed.C3D");
            File.WriteAllBytes(malformedPath, [0, 1, 2]);
            var malformed = Await(recipeSource.PrepareNewRecipeSourceAsync(setup with { SourcePath = malformedPath }, CancellationToken.None));
            Check("unreadable variant preparation preserves both current inputs", !malformed.IsReady
                && malformed.FailureMessage is not null && viewer.CurrentC3DSourcePath == sourcePath && viewModel.Workbench.Source.Path == sourcePath,
                malformed.FailureMessage ?? "No failure message");
            var canceled = false;
            try { Await(recipeSource.PrepareNewRecipeSourceAsync(setup, new CancellationToken(true))); }
            catch (OperationCanceledException) { canceled = true; }
            Check("canceled variant preparation performs no source transition", canceled
                && viewer.CurrentC3DSourcePath == sourcePath && viewModel.Workbench.RecipePath == originalRecipePath, $"canceled={canceled}");
            var copied = viewModel.Workbench.TryCreateCompatibleSourceVariant(setup, preparation.VariantBinding!, out var copyMessage);
            Check("copy commits before the Viewer source changes", copied && File.Exists(setup.RecipePath)
                && viewer.CurrentC3DSourcePath == sourcePath && viewModel.Workbench.Source.Path == variantPath, copyMessage);
            var displayed = Await(recipeSource.ApplyCreatedVariantSourceAsync(CancellationToken.None));
            Check("committed copy loads through the Viewer without rebinding the source entity ID",
                displayed.IsReady && viewer.CurrentC3DSourcePath == variantPath && viewModel.Workbench.Source.Id == "source.variant.coordinator"
                && coordinator.IsViewerSourceAlreadyLoaded(variantPath), displayed.FailureMessage ?? "Ready");
            File.Delete(variantPath);
            var missingAfterCommit = Await(recipeSource.ApplyCreatedVariantSourceAsync(CancellationToken.None));
            Check("post-commit input failure keeps the saved copy but removes misleading Viewer content", !missingAfterCommit.IsReady
                && viewer.CurrentC3DSourcePath is null && viewModel.Workbench.RecipePath == setup.RecipePath && File.Exists(setup.RecipePath),
                missingAfterCommit.FailureMessage ?? "No failure message");
            CreateSource([11, 12, 13, 14, 15, 16, 17, 18, 19]).SaveC3D(variantPath);
            var recovered = Await(recipeSource.ApplyCreatedVariantSourceAsync(CancellationToken.None));
            Check("valid retry recovers the committed copy without execution", recovered.IsReady && viewer.CurrentC3DSourcePath == variantPath
                && !viewModel.Workbench.IsDirty && !viewModel.Workbench.IsOrderedRunRunning && !viewModel.Workbench.IsSelectedStepPreviewRunning,
                recovered.FailureMessage ?? "Ready");
            var inspectionInputPath = Path.Combine(fixtureRoot, "inspection-input-ng.c3d");
            CreateSource([21, 22, 23, 24, 25, 26, 27, 28, 29]).SaveC3D(inspectionInputPath);
            viewModel.Workbench.InspectionQueue.AddSources([inspectionInputPath]);
            viewModel.Workbench.InspectionQueue.SelectedSample = viewModel.Workbench.InspectionQueue.Samples.Single(item => item.SourcePath == inspectionInputPath);
            var retainedQueueInput = viewModel.Workbench.InspectionQueue.SelectedSample;
            var sourceBeforeImport = viewModel.Workbench.Source.Path;
            var invalidInspectionInput = Path.Combine(fixtureRoot, "inspection-truncated.c3d");
            File.WriteAllBytes(invalidInspectionInput, [0, 1, 2]);
            var rejectedInspectionInput = !Await(coordinator.UseInspectionInputForTeachingAsync(invalidInspectionInput, null));
            Check("teaching import failure preserves the current input and inspection selection", rejectedInspectionInput
                && viewModel.Workbench.Source.Path == sourceBeforeImport && viewModel.Workbench.InspectionQueue.SelectedSample == retainedQueueInput
                && !viewModel.Workbench.IsC3DSourceLoading, invalidInspectionInput);
            var inputImported = Await(coordinator.UseInspectionInputForTeachingAsync(inspectionInputPath, null));
            var importedBindingAvailable = viewer.TryGetCurrentC3DSourceBinding(inspectionInputPath, out var importedBinding);
            Check("inspection input import uses the existing loader and switches to teaching without inspection", inputImported && importedBindingAvailable
                && viewModel.Workbench.Source.Path == inspectionInputPath && viewModel.IsTeachWorkspaceSelected
                && viewModel.Workbench.InspectionQueue.SelectedSample == retainedQueueInput && !viewModel.Workbench.HasOrderedRunResult
                && !viewModel.Workbench.IsSelectedStepPreviewRunning && !viewModel.Workbench.HasCurrentMeasurementPreview, importedBinding?.ContentSha256 ?? "missing binding");
            var importedRecipePath = Path.Combine(fixtureRoot, "imported-inspection-reference.ov3d-recipe.json");
            var importedSaved = viewModel.Workbench.TrySaveTeachingRecipe(importedRecipePath, out var importedSaveMessage);
            viewModel.Workbench.CreateNewTeachingRecipe();
            viewer.ClearC3DTeachingSource("verification reset");
            var importedReopened = viewModel.Workbench.TryOpenTeachingRecipe(importedRecipePath, out var importedOpenMessage);
            var restoredInspectionSource = Await(recipeSource.ApplyCreatedVariantSourceAsync(CancellationToken.None));
            Check("imported inspection input survives recipe save, reopen and reference reload", importedSaved && importedReopened && restoredInspectionSource.IsReady
                && viewModel.Workbench.Source.Path == inspectionInputPath && viewer.CurrentC3DSourcePath == inspectionInputPath
                && !viewModel.Workbench.IsDirty && !viewModel.Workbench.HasOrderedRunResult && !viewModel.Workbench.HasCurrentMeasurementPreview,
                $"save={importedSaveMessage};open={importedOpenMessage};reload={restoredInspectionSource.FailureMessage ?? "ready"}");
            coordinator.LoadC3DSource(sourcePath);
            File.WriteAllBytes(sourcePath, [0, 1, 2]);
            var unreadableSourceRequiresReload = !coordinator.IsViewerSourceAlreadyLoaded(sourcePath);
            var failedReload = !coordinator.LoadC3DSource(sourcePath);
            var retainedAfterFailure = viewer.TryGetCurrentC3DSourceBinding(sourcePath, out var bindingAfterFailure)
                && retainedBinding is not null
                && ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(retainedBinding, bindingAfterFailure);
            Check(
                "unreadable-replacement-fails-closed-and-retains-current-viewer-source",
                unreadableSourceRequiresReload && failedReload && retainedAfterFailure,
                $"reloadRequired={unreadableSourceRequiresReload};failedReload={failedReload};retained={retainedAfterFailure};status={viewer.HostState.ViewerStatus}");
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unexpected | {exception}");
        }
        finally
        {
            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot, recursive: true);
            }
        }

        var succeeded = passed == total && total > 0 && !lines.Any(line => line.StartsWith("FAIL | unexpected", StringComparison.Ordinal));
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        Directory.CreateDirectory(reportDirectory);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"ShellSourceReload|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }

    private static C3DHeightFieldSnapshot CreateSource(IReadOnlyList<double> values) =>
        C3DHeightFieldSnapshot.CreateForVerification(
            "source.same-path-reload",
            3,
            3,
            values,
            "fixture-unit",
            "frame.raw");

    private static T Await<T>(Task<T> task)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!task.IsCompleted && DateTime.UtcNow < deadline)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }
        if (!task.IsCompleted) throw new TimeoutException("Recipe source verification did not finish within 20 seconds.");
        return task.GetAwaiter().GetResult();
    }
}
