extern alias OvlMessageDialogs;

using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.WpfPropertyGrid;
using System.Windows.Media;
using System.Windows.Threading;
using OpenVisionLab;
using WpfMessageDialogButtons = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogButtons;
using WpfMessageDialogControl = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogControl;
using WpfMessageDialogKind = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogKind;
using WpfMessageDialogOptions = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogOptions;
using WpfMessageDialogResult = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogResult;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.PropertyGrid;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell;

internal static class RecipeManagerWpgVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Recipe Manager + WPG verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;
        var total = 0;
        var fixtureRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenVisionLab.ThreeD",
            "RecipeManagerWpgVerification",
            Guid.NewGuid().ToString("N"));

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
            Directory.CreateDirectory(fixtureRoot);
            var sourcePath = Path.Combine(fixtureRoot, "source.C3D");
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.recipe-manager",
                3,
                3,
                [1, 2, 4, 2, 4, 8, 3, 6, 12]).SaveC3D(sourcePath);
            var recentPath = Path.Combine(fixtureRoot, "recent.json");
            var recipePath = Path.Combine(fixtureRoot, "recipe.ov3d-teach.json");

            var workbench = new ToolWorkbenchViewModel(recentPath);
            workbench.RecipeName = "Recipe Manager Fixture";
            workbench.SetC3DSource(sourcePath);
            var originalLanguage = OpenVisionLanguageService.CurrentLanguage;

            var firstUseRoot = Path.Combine(fixtureRoot, "first-use");
            Directory.CreateDirectory(firstUseRoot);
            var firstUseRecentPath = Path.Combine(firstUseRoot, "recent.json");
            var firstUseRecipePath = Path.Combine(firstUseRoot, "First Recipe Fixture.ov3d-recipe.json");
            var firstUse = new ToolWorkbenchViewModel(firstUseRecentPath);
            var firstUseLogCount = firstUse.RunLog.Count;
            firstUse.BeginFirstRecipeSetup();
            Check(
                "first-use setup opens as a visible draft without mutating authored or execution state",
                firstUse.IsFirstRecipeSetupVisible
                && string.IsNullOrWhiteSpace(firstUse.RecipePath)
                && string.IsNullOrWhiteSpace(firstUse.Source.Path)
                && firstUse.PipelineSteps.Count == 0
                && !firstUse.IsDirty
                && firstUse.RunLog.Count == firstUseLogCount,
                $"visible={firstUse.IsFirstRecipeSetupVisible}; recipe={firstUse.RecipePath}; source={firstUse.Source.Path}; steps={firstUse.PipelineSteps.Count}; dirty={firstUse.IsDirty}; logs={firstUse.RunLog.Count}/{firstUseLogCount}");

            firstUse.FirstRecipeName = "First Recipe Fixture";
            firstUse.FirstRecipeFolderPath = firstUseRoot;
            firstUse.FirstRecipeSourcePath = sourcePath;
            firstUse.SelectedFirstRecipeStarter = firstUse.FirstRecipeStarterOptions.Single(option =>
                option.Id == ToolWorkbenchViewModel.ThicknessFirstRecipeStarterId);
            firstUse.RememberFirstRecipeSetup = true;
            var draftReady = firstUse.TryGetFirstRecipeSetup(out var confirmedSetup, out var draftMessage);
            Check(
                "one first-use draft exposes a name, folder, C3D source, optional starter, and exact target before Create",
                draftReady
                && firstUse.IsFirstRecipeSetupValid
                && confirmedSetup.RecipeName == "First Recipe Fixture"
                && string.Equals(confirmedSetup.FolderPath, firstUseRoot, StringComparison.OrdinalIgnoreCase)
                && string.Equals(confirmedSetup.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase)
                && confirmedSetup.StarterId == ToolWorkbenchViewModel.ThicknessFirstRecipeStarterId
                && string.Equals(confirmedSetup.RecipePath, firstUseRecipePath, StringComparison.OrdinalIgnoreCase),
                draftMessage);
            Check(
                "editing a valid first-use draft still does not load a source, add a step, save, Preview, Publish, or Run",
                string.IsNullOrWhiteSpace(firstUse.RecipePath)
                && string.IsNullOrWhiteSpace(firstUse.Source.Path)
                && firstUse.PipelineSteps.Count == 0
                && !firstUse.IsDirty
                && firstUse.RunLog.Count == firstUseLogCount,
                $"recipe={firstUse.RecipePath}; source={firstUse.Source.Path}; steps={firstUse.PipelineSteps.Count}; dirty={firstUse.IsDirty}; logs={firstUse.RunLog.Count}/{firstUseLogCount}");

            firstUse.CreateNewTeachingRecipe(confirmedSetup.RecipeName);
            firstUse.SetC3DSource(confirmedSetup.SourcePath);
            var starterApplied = firstUse.TryApplyFirstRecipeStarter(
                confirmedSetup.StarterId,
                out var starterMessage);
            var firstUseSaveMessage = string.Empty;
            var firstUseSaved = starterApplied
                && firstUse.TrySaveTeachingRecipe(confirmedSetup.RecipePath, out firstUseSaveMessage);
            var firstUsePreferenceMessage = string.Empty;
            var firstUsePreferenceSaved = firstUseSaved
                && firstUse.CompleteFirstRecipeSetup(out firstUsePreferenceMessage);
            var firstUseDocument = firstUseSaved
                ? ToolRecipeDocumentStore.Load(firstUseRecipePath)
                : null;
            Check(
                "confirmed first-use setup creates one source-routed starter and saves without running inspection",
                firstUsePreferenceSaved
                && firstUseDocument is not null
                && string.Equals(firstUseDocument.Source.Path, sourcePath, StringComparison.OrdinalIgnoreCase)
                && firstUseDocument.Steps.Count == 1
                && firstUseDocument.Steps[0].ToolId == "thickness"
                && !firstUse.IsDirty
                && !firstUse.IsFirstRecipeSetupVisible,
                $"starter={starterMessage}; save={firstUseSaveMessage}; preference={firstUsePreferenceMessage}; steps={firstUseDocument?.Steps.Count}");

            var reopenedRecipe = new ToolWorkbenchViewModel(Path.Combine(firstUseRoot, "reopen-recent.json"));
            var recipeReopened = reopenedRecipe.TryOpenTeachingRecipe(firstUseRecipePath, out var reopenMessage);
            Check(
                "created recipe survives a save and reopen round trip with stable source and starter identity",
                recipeReopened
                && string.Equals(reopenedRecipe.Source.Path, sourcePath, StringComparison.OrdinalIgnoreCase)
                && reopenedRecipe.PipelineSteps.Count == 1
                && reopenedRecipe.PipelineSteps[0].ToolId == "thickness"
                && !reopenedRecipe.IsDirty,
                reopenMessage);

            var variantSourcePath = Path.Combine(firstUseRoot, "variant-source.C3D");
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.recipe-manager.variant",
                3,
                3,
                [2, 3, 5, 3, 5, 9, 4, 7, 13]).SaveC3D(variantSourcePath);
            var incompatibleSourcePath = Path.Combine(firstUseRoot, "variant-incompatible.C3D");
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.recipe-manager.variant-incompatible",
                4,
                4,
                Enumerable.Range(1, 16).Select(value => (double)value).ToArray()).SaveC3D(incompatibleSourcePath);
            var variantBasePath = Path.Combine(firstUseRoot, "variant-base.ov3d-recipe.json");
            var variantPath = Path.Combine(firstUseRoot, "variant-copy.ov3d-recipe.json");
            var variantBinding = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(sourcePath);
            var variantReference = new ToolRecipeSelection(
                "selection.variant.reference",
                "Variant reference ROI",
                ToolRecipeSelectionKinds.GridRectangle,
                reopenedRecipe.Source.Id,
                reopenedRecipe.Source.FrameId,
                variantBinding,
                new ToolRecipeGridRectangle(0, 0, 2, 1),
                null,
                null);
            var variantMeasurement = new ToolRecipeSelection(
                "selection.variant.measurement",
                "Variant measurement ROI",
                ToolRecipeSelectionKinds.GridRectangle,
                reopenedRecipe.Source.Id,
                reopenedRecipe.Source.FrameId,
                variantBinding,
                new ToolRecipeGridRectangle(0, 1, 2, 1),
                null,
                null);
            reopenedRecipe.Selections.Add(variantReference);
            reopenedRecipe.Selections.Add(variantMeasurement);
            var variantStep = reopenedRecipe.PipelineSteps.Single();
            variantStep.InputEntityIdsText = string.Join(
                "; ",
                reopenedRecipe.Source.Id,
                variantReference.Id,
                variantMeasurement.Id);
            variantStep.DualRoiRouting = new ToolRecipeDualRoiRouting(
                variantReference.Id,
                variantMeasurement.Id);
            var variantBaseSaved = reopenedRecipe.TrySaveTeachingRecipe(
                variantBasePath,
                out var variantBaseSaveMessage);
            var variantLogCount = reopenedRecipe.RunLog.Count;
            reopenedRecipe.BeginCompatibleSourceVariantSetup();
            Check(
                "compatible variant setup pre-fills a separate draft without mutating or executing the current recipe",
                variantBaseSaved
                && reopenedRecipe.IsFirstRecipeSetupVisible
                && reopenedRecipe.IsCompatibleVariantSetup
                && reopenedRecipe.FirstRecipeName == "First Recipe Fixture-variant"
                && string.Equals(reopenedRecipe.FirstRecipeFolderPath, firstUseRoot, StringComparison.OrdinalIgnoreCase)
                && string.Equals(reopenedRecipe.FirstRecipeSourcePath, sourcePath, StringComparison.OrdinalIgnoreCase)
                && !reopenedRecipe.IsFirstRecipeSetupValid
                && reopenedRecipe.FirstRecipeSetupStatus == reopenedRecipe.Localization.CompatibleVariantSourceMustDiffer
                && reopenedRecipe.RecipePath == variantBasePath
                && !reopenedRecipe.HasCurrentMeasurementPreview
                && reopenedRecipe.RunLog.Count == variantLogCount,
                $"base={variantBaseSaveMessage}; status={reopenedRecipe.FirstRecipeSetupStatus}; logs={variantLogCount}->{reopenedRecipe.RunLog.Count}");

            reopenedRecipe.FirstRecipeName = "variant-copy";
            reopenedRecipe.FirstRecipeSourcePath = incompatibleSourcePath;
            Check(
                "compatible variant setup rejects a different C3D grid before Create",
                !reopenedRecipe.IsFirstRecipeSetupValid
                && reopenedRecipe.FirstRecipeSetupStatus.Contains("3 x 3", StringComparison.Ordinal)
                && reopenedRecipe.FirstRecipeSetupStatus.Contains("4 x 4", StringComparison.Ordinal),
                reopenedRecipe.FirstRecipeSetupStatus);

            reopenedRecipe.FirstRecipeSourcePath = variantSourcePath;
            var variantDraftReady = reopenedRecipe.TryGetFirstRecipeSetup(
                out var variantSetup,
                out var variantDraftMessage);
            var variantCreateMessage = string.Empty;
            var variantCreated = variantDraftReady
                && reopenedRecipe.TryCreateCompatibleSourceVariant(
                    variantSetup,
                    ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(variantSourcePath),
                    out variantCreateMessage);
            var variantCompleted = variantCreated
                && reopenedRecipe.CompleteFirstRecipeSetup(out _);
            var variantDocument = variantCreated ? ToolRecipeDocumentStore.Load(variantPath) : null;
            var originalVariantDocument = variantBaseSaved
                ? ToolRecipeDocumentStore.Load(variantBasePath)
                : null;
            var variantSelections = variantDocument?.Selections ?? Array.Empty<ToolRecipeSelection>();
            var originalVariantSelections = originalVariantDocument?.Selections ?? Array.Empty<ToolRecipeSelection>();
            Check(
                "compatible variant preserves steps, routes, ROI coordinates, and parameters while rebinding source identity",
                variantCompleted
                && variantDocument is not null
                && originalVariantDocument is not null
                && variantSetup.IsCompatibleSourceVariant
                && string.Equals(variantSetup.RecipePath, variantPath, StringComparison.OrdinalIgnoreCase)
                && variantDocument.Name == "variant-copy"
                && string.Equals(variantDocument.Source.Path, variantSourcePath, StringComparison.OrdinalIgnoreCase)
                && variantDocument.Steps.Count == originalVariantDocument.Steps.Count
                && variantDocument.Steps[0].Id == originalVariantDocument.Steps[0].Id
                && variantDocument.Steps[0].ToolId == originalVariantDocument.Steps[0].ToolId
                && variantDocument.Steps[0].InputEntityIds.SequenceEqual(originalVariantDocument.Steps[0].InputEntityIds)
                && variantDocument.Steps[0].DualRoiRouting == originalVariantDocument.Steps[0].DualRoiRouting
                && variantDocument.Steps[0].Parameters.Select(parameter => $"{parameter.Name}={parameter.Value}")
                    .SequenceEqual(originalVariantDocument.Steps[0].Parameters.Select(parameter => $"{parameter.Name}={parameter.Value}"))
                && variantSelections.Select(selection => selection.Id)
                    .SequenceEqual(originalVariantSelections.Select(selection => selection.Id))
                && variantSelections.Select(selection => selection.GridRectangle)
                    .SequenceEqual(originalVariantSelections.Select(selection => selection.GridRectangle))
                && variantSelections.All(selection =>
                    selection.SourceBinding.ContentSha256
                    == ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(variantSourcePath).ContentSha256)
                && originalVariantSelections.All(selection =>
                    selection.SourceBinding.ContentSha256 == variantBinding.ContentSha256)
                && reopenedRecipe.BeginCompatibleSourceVariantCommand.CanExecute(null)
                && !reopenedRecipe.HasCurrentMeasurementPreview
                && !reopenedRecipe.IsMeasurementPreviewPublished
                && !reopenedRecipe.IsFirstRecipeSetupVisible,
                $"draft={variantDraftMessage}; create={variantCreateMessage}; variant={variantDocument?.Source.Path}");

            var restoredSetup = new ToolWorkbenchViewModel(firstUseRecentPath);
            var restoredLogCount = restoredSetup.RunLog.Count;
            restoredSetup.BeginFirstRecipeSetup();
            Check(
                "remembered first-use values restore visibly and editably without opening, adding, or executing",
                restoredSetup.IsFirstRecipeSetupVisible
                && restoredSetup.FirstRecipeName == "First Recipe Fixture"
                && string.Equals(restoredSetup.FirstRecipeFolderPath, firstUseRoot, StringComparison.OrdinalIgnoreCase)
                && string.Equals(restoredSetup.FirstRecipeSourcePath, sourcePath, StringComparison.OrdinalIgnoreCase)
                && restoredSetup.SelectedFirstRecipeStarter?.Id == ToolWorkbenchViewModel.ThicknessFirstRecipeStarterId
                && restoredSetup.RememberFirstRecipeSetup
                && string.IsNullOrWhiteSpace(restoredSetup.RecipePath)
                && string.IsNullOrWhiteSpace(restoredSetup.Source.Path)
                && restoredSetup.PipelineSteps.Count == 0
                && restoredSetup.RunLog.Count == restoredLogCount,
                $"name={restoredSetup.FirstRecipeName}; starter={restoredSetup.SelectedFirstRecipeStarter?.Id}; recipe={restoredSetup.RecipePath}; source={restoredSetup.Source.Path}; logs={restoredSetup.RunLog.Count}/{restoredLogCount}");

            var staleRoot = Path.Combine(firstUseRoot, "stale");
            Directory.CreateDirectory(staleRoot);
            var staleSource = Path.Combine(staleRoot, "stale.C3D");
            File.Copy(sourcePath, staleSource);
            restoredSetup.FirstRecipeName = "Stale Fixture";
            restoredSetup.FirstRecipeFolderPath = staleRoot;
            restoredSetup.FirstRecipeSourcePath = staleSource;
            restoredSetup.RememberFirstRecipeSetup = true;
            var stalePreferenceSaved = restoredSetup.CompleteFirstRecipeSetup(out var stalePreferenceMessage);
            Directory.Delete(staleRoot, recursive: true);
            var staleSetup = new ToolWorkbenchViewModel(firstUseRecentPath);
            var staleLogCount = staleSetup.RunLog.Count;
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
            staleSetup.BeginFirstRecipeSetup();
            Check(
                "stale remembered paths remain visible, explain the unavailable folder, and disable Create",
                stalePreferenceSaved
                && staleSetup.FirstRecipeName == "Stale Fixture"
                && string.Equals(staleSetup.FirstRecipeFolderPath, staleRoot, StringComparison.OrdinalIgnoreCase)
                && string.Equals(staleSetup.FirstRecipeSourcePath, staleSource, StringComparison.OrdinalIgnoreCase)
                && !staleSetup.IsFirstRecipeSetupValid
                && !staleSetup.CreateFirstRecipeCommand.CanExecute(null)
                && staleSetup.FirstRecipeSetupStatus == "The saved recipe folder is unavailable. Select it again."
                && string.IsNullOrWhiteSpace(staleSetup.RecipePath)
                && string.IsNullOrWhiteSpace(staleSetup.Source.Path)
                && staleSetup.PipelineSteps.Count == 0
                && staleSetup.RunLog.Count == staleLogCount,
                $"saved={stalePreferenceSaved}; {stalePreferenceMessage}; status={staleSetup.FirstRecipeSetupStatus}; logs={staleSetup.RunLog.Count}/{staleLogCount}");

            staleSetup.ResetFirstRecipeSetupCommand.Execute(null);
            var resetSetup = new ToolWorkbenchViewModel(firstUseRecentPath);
            resetSetup.BeginFirstRecipeSetup();
            Check(
                "Reset clears remembered first-use values and has no authored or execution side effect",
                staleSetup.FirstRecipeName == "new-inspection"
                && string.IsNullOrWhiteSpace(staleSetup.FirstRecipeFolderPath)
                && string.IsNullOrWhiteSpace(staleSetup.FirstRecipeSourcePath)
                && !staleSetup.RememberFirstRecipeSetup
                && resetSetup.FirstRecipeName == "new-inspection"
                && string.IsNullOrWhiteSpace(resetSetup.FirstRecipeFolderPath)
                && string.IsNullOrWhiteSpace(resetSetup.FirstRecipeSourcePath)
                && string.IsNullOrWhiteSpace(resetSetup.RecipePath)
                && string.IsNullOrWhiteSpace(resetSetup.Source.Path)
                && resetSetup.PipelineSteps.Count == 0,
                $"resetName={resetSetup.FirstRecipeName}; folder={resetSetup.FirstRecipeFolderPath}; source={resetSetup.FirstRecipeSourcePath}");

            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
            var englishFirstUse = $"{staleSetup.Localization.FirstRecipeSetup}|{staleSetup.Localization.FirstRecipeFolder}|{staleSetup.Localization.FirstRecipeSource}|{staleSetup.Localization.FirstRecipeCreate}";
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
            var koreanFirstUse = $"{staleSetup.Localization.FirstRecipeSetup}|{staleSetup.Localization.FirstRecipeFolder}|{staleSetup.Localization.FirstRecipeSource}|{staleSetup.Localization.FirstRecipeCreate}";
            OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
            Check(
                "first-use labels and actions switch between English and Korean",
                englishFirstUse == "New recipe setup|Recipe folder|C3D input|Create recipe"
                && koreanFirstUse == "새 레시피 설정|레시피 폴더|C3D 입력|레시피 만들기",
                $"en={englishFirstUse} | ko={koreanFirstUse}");

            var localizedPropertyChanges = new List<string?>();
            workbench.PropertyChanged += (_, args) => localizedPropertyChanges.Add(args.PropertyName);
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
            var englishSource = workbench.LocalizedSourceReadinessSummary;
            var englishPath = workbench.LocalizedRecipePathSummary;
            var englishState = workbench.LocalizedRecipeStateSummary;
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
            var koreanSource = workbench.LocalizedSourceReadinessSummary;
            var koreanPath = workbench.LocalizedRecipePathSummary;
            var koreanState = workbench.LocalizedRecipeStateSummary;
            OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
            Check(
                "Recipe Center source and save-path status switch between English and Korean",
                englishSource == "Input ready · 3 x 3"
                && englishPath == "Not saved yet"
                && koreanSource == "입력 준비됨 · 3 x 3"
                && koreanPath == "아직 저장하지 않음",
                $"en={englishSource}; {englishPath} | ko={koreanSource}; {koreanPath}");
            Check(
                "saveable empty recipe is labeled as execution-incomplete rather than structurally invalid",
                englishState == "1 execution requirement(s) | Modified"
                && koreanState == "\uC2E4\uD589 \uC900\uBE44 \uD544\uC694 1\uAC1C | \uC218\uC815\uB428",
                $"en={englishState} | ko={koreanState}");
            Check(
                "language change notifies all Recipe Center computed status properties",
                localizedPropertyChanges.Contains(nameof(ToolWorkbenchViewModel.LocalizedSourceReadinessSummary), StringComparer.Ordinal)
                && localizedPropertyChanges.Contains(nameof(ToolWorkbenchViewModel.LocalizedRecipePathSummary), StringComparer.Ordinal)
                && localizedPropertyChanges.Contains(nameof(ToolWorkbenchViewModel.LocalizedRecipeStateSummary), StringComparer.Ordinal),
                string.Join(",", localizedPropertyChanges.Distinct(StringComparer.Ordinal)));
            Check(
                "empty recipe enables Save and Save As before inspection steps exist",
                !workbench.IsRecipeSaveBlocked
                && workbench.CanSaveTeachingRecipe
                && workbench.SaveTeachingRecipeCommand.CanExecute(null)
                && workbench.SaveTeachingRecipeAsCommand.CanExecute(null)
                && !workbench.IsTeachingRecipeExecutionReady,
                $"blocked={workbench.IsRecipeSaveBlocked}; save={workbench.SaveTeachingRecipeCommand.CanExecute(null)}; saveAs={workbench.SaveTeachingRecipeAsCommand.CanExecute(null)}");
            var emptyRecipePath = Path.Combine(fixtureRoot, "empty-recipe.ov3d-recipe.json");
            var emptySaved = workbench.TrySaveTeachingRecipe(emptyRecipePath, out var emptySaveMessage);
            var emptyStored = emptySaved ? ToolRecipeDocumentStore.Load(emptyRecipePath) : null;
            Check(
                "empty recipe saves without fabricating an inspection step",
                emptySaved
                && emptyStored is not null
                && emptyStored.Steps.Count == 0
                && ToolRecipeValidator.ValidateForStorage(emptyStored).IsValid
                && !ToolRecipeValidator.Validate(emptyStored).IsValid,
                emptySaveMessage);
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
            var savedLocalizedState = workbench.LocalizedRecipeStateSummary;
            OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
            Check(
                "successful zero-step save refreshes the localized saved state",
                savedLocalizedState == "1 execution requirement(s) | Saved",
                savedLocalizedState);
            var filter = AddTool(workbench, "filter");
            Check(
                "first valid inspection step keeps Save enabled and makes execution validation ready",
                workbench.CanSaveTeachingRecipe
                && workbench.IsTeachingRecipeExecutionReady
                && workbench.SaveTeachingRecipeCommand.CanExecute(null)
                && workbench.SaveTeachingRecipeAsCommand.CanExecute(null),
                $"valid={workbench.CanSaveTeachingRecipe}; save={workbench.SaveTeachingRecipeCommand.CanExecute(null)}; saveAs={workbench.SaveTeachingRecipeAsCommand.CanExecute(null)}");
            var validationSourceRequestRaised = false;
            EventHandler validationSourceRequestHandler = (_, _) => validationSourceRequestRaised = true;
            workbench.SelectValidationSetSourcesRequested += validationSourceRequestHandler;
            workbench.SelectValidationSetSourcesCommand.Execute(null);
            workbench.SelectValidationSetSourcesRequested -= validationSourceRequestHandler;
            Check(
                "Validation Set source selection is a ViewModel command request",
                validationSourceRequestRaised,
                $"requested={validationSourceRequestRaised}");
            workbench.SelectedReviewTabIndex = 4;
            Check(
                "review-tab presentation state is owned and bounded by the Workbench ViewModel",
                workbench.SelectedReviewTabIndex == 4,
                $"selectedIndex={workbench.SelectedReviewTabIndex}");

            var propertySession = new ToolWorkbenchStepPropertySession();
            propertySession.Refresh(filter);
            var sessionDraft = propertySession.Draft as FilterStepProperties;
            Check(
                "PropertyGrid session owns a detached typed draft",
                sessionDraft is not null
                && propertySession.IsSupported
                && !propertySession.HasPendingChanges
                && sessionDraft.KernelSize == 3,
                $"draft={propertySession.Draft?.GetType().Name}; pending={propertySession.HasPendingChanges}");
            sessionDraft!.KernelSize = 4;
            propertySession.MarkDirty();
            var invalidSessionValues = propertySession.TryCreateParameterValues(filter, out _, out var invalidSessionMessage);
            Check(
                "PropertyGrid session rejects invalid draft values without recipe mutation",
                !invalidSessionValues
                && propertySession.HasPendingChanges
                && filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value == "3",
                invalidSessionMessage);
            sessionDraft.KernelSize = 5;
            var validSessionValues = propertySession.TryCreateParameterValues(filter, out var sessionValues, out var validSessionMessage);
            Check(
                "PropertyGrid session serializes valid values without committing the recipe",
                validSessionValues
                && sessionValues["KernelSize"] == "5"
                && filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value == "3",
                validSessionMessage);

            filter.Parameters.Remove(filter.Parameters.Single(parameter => parameter.Name == "BoundaryPolicy"));
            filter.Parameters.Add(new ToolWorkbenchParameterItem("FuturePolicy", "RetainMe"));
            workbench.DiscardSelectedStepParameterDraft();
            var filterDraft = workbench.SelectedStepPropertyDraft as FilterStepProperties;
            Check(
                "Filter maps to a detached typed draft",
                filterDraft is not null && filterDraft.KernelSize == 3 && filterDraft.UnmappedParameters.Contains("FuturePolicy=RetainMe", StringComparison.Ordinal),
                $"draft={workbench.SelectedStepPropertyDraft?.GetType().Name}; kernel={filterDraft?.KernelSize}; unmapped={filterDraft?.UnmappedParameters}");

            filterDraft!.KernelSize = 5;
            workbench.MarkSelectedStepParameterDraftDirtyCommand.Execute(null);
            Check(
                "WPG change command marks a draft without mutating the recipe or auto-running",
                filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value == "3"
                && workbench.HasPendingStepParameterChanges
                && !workbench.HasCurrentFilterPreview
                && !workbench.PreviewSelectedStepCommand.CanExecute(null),
                $"storedKernel={filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value}; preview={workbench.HasCurrentFilterPreview}");

            workbench.DiscardSelectedStepParameterDraft();
            Check(
                "preparation-preset assistant starts as an explicit Analyze action",
                workbench.AnalyzePreparationPresetsCommand.CanExecute(null)
                && !workbench.IsPreparationPresetAnalysisReady
                && workbench.SelectedPreparationPreset is null
                && !workbench.HasPendingStepParameterChanges
                && filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value == "3",
                $"canAnalyze={workbench.AnalyzePreparationPresetsCommand.CanExecute(null)}; analyzed={workbench.IsPreparationPresetAnalysisReady}; selected={workbench.SelectedPreparationPreset?.Id ?? "(none)"}");
            workbench.AnalyzePreparationPresetsCommand.Execute(null);
            Check(
                "Analyze discovers the bounded Filter presets without changing the draft or recipe",
                workbench.IsPreparationPresetAnalysisReady
                && workbench.PreparationPresetOptions.Count == 3
                && workbench.SelectedPreparationPreset?.KernelSize == 3
                && (workbench.SelectedStepPropertyDraft as FilterStepProperties)?.KernelSize == 3
                && filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value == "3"
                && !workbench.HasPendingStepParameterChanges
                && !workbench.HasCurrentFilterPreview,
                $"options={workbench.PreparationPresetOptions.Count}; selected={workbench.SelectedPreparationPreset?.KernelSize}; draft={(workbench.SelectedStepPropertyDraft as FilterStepProperties)?.KernelSize}; stored={filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value}");
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
            var englishPreparationPresetName = workbench.PreparationPresetOptions
                .Single(option => option.KernelSize == 5)
                .DisplayName;
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
            var koreanPreparationPresetName = workbench.PreparationPresetOptions
                .Single(option => option.KernelSize == 5)
                .DisplayName;
            OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
            Check(
                "Preparation preset options localize while preserving their stable IDs",
                englishPreparationPresetName == "Median 5 x 5"
                && koreanPreparationPresetName == "중앙값 5 × 5"
                && workbench.PreparationPresetOptions.Select(option => option.Id).SequenceEqual(
                    ["filter-median-3", "filter-median-5", "filter-median-7"]),
                $"english={englishPreparationPresetName}; korean={koreanPreparationPresetName}; ids={string.Join(",", workbench.PreparationPresetOptions.Select(option => option.Id))}");
            workbench.SelectedPreparationPreset = workbench.PreparationPresetOptions.Single(option => option.KernelSize == 5);
            workbench.ProposePreparationPresetCommand.Execute(null);
            Check(
                "Propose keeps the selected preset transient and non-mutating",
                workbench.ProposedPreparationPreset?.KernelSize == 5
                && !workbench.IsPreparationPresetReviewActive
                && (workbench.SelectedStepPropertyDraft as FilterStepProperties)?.KernelSize == 3
                && filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value == "3"
                && !workbench.HasPendingStepParameterChanges
                && !workbench.HasCurrentFilterPreview,
                $"proposed={workbench.ProposedPreparationPreset?.KernelSize}; review={workbench.IsPreparationPresetReviewActive}; draft={(workbench.SelectedStepPropertyDraft as FilterStepProperties)?.KernelSize}; stored={filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value}");
            workbench.ReviewPreparationPresetCommand.Execute(null);
            Check(
                "Review is read-only and enables only the draft application boundary",
                workbench.IsPreparationPresetReviewActive
                && workbench.ApplyPreparationPresetDraftCommand.CanExecute(null)
                && (workbench.SelectedStepPropertyDraft as FilterStepProperties)?.KernelSize == 3
                && filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value == "3"
                && !workbench.HasPendingStepParameterChanges
                && !workbench.HasCurrentFilterPreview,
                $"review={workbench.IsPreparationPresetReviewActive}; canApplyDraft={workbench.ApplyPreparationPresetDraftCommand.CanExecute(null)}");
            workbench.CancelPreparationPresetReviewCommand.Execute(null);
            Check(
                "Cancel clears the transient preset review without changing recipe or draft",
                workbench.ProposedPreparationPreset is null
                && !workbench.IsPreparationPresetReviewActive
                && !workbench.IsPreparationPresetDraftApplied
                && (workbench.SelectedStepPropertyDraft as FilterStepProperties)?.KernelSize == 3
                && filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value == "3"
                && !workbench.HasPendingStepParameterChanges
                && !workbench.HasCurrentFilterPreview,
                $"proposed={workbench.ProposedPreparationPreset?.KernelSize}; review={workbench.IsPreparationPresetReviewActive}; draft={(workbench.SelectedStepPropertyDraft as FilterStepProperties)?.KernelSize}");
            workbench.SelectedPreparationPreset = workbench.PreparationPresetOptions.Single(option => option.KernelSize == 5);
            workbench.ProposePreparationPresetCommand.Execute(null);
            workbench.ReviewPreparationPresetCommand.Execute(null);
            workbench.ApplyPreparationPresetDraftCommand.Execute(null);
            Check(
                "Apply draft changes only the typed PropertyGrid draft and leaves normal Apply explicit",
                workbench.IsPreparationPresetDraftApplied
                && !workbench.IsPreparationPresetReviewActive
                && (workbench.SelectedStepPropertyDraft as FilterStepProperties)?.KernelSize == 5
                && filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value == "3"
                && workbench.HasPendingStepParameterChanges
                && workbench.ApplySelectedStepParameterDraftCommand.CanExecute(null)
                && !workbench.HasCurrentFilterPreview,
                $"draftApplied={workbench.IsPreparationPresetDraftApplied}; draft={(workbench.SelectedStepPropertyDraft as FilterStepProperties)?.KernelSize}; stored={filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value}; pending={workbench.HasPendingStepParameterChanges}; canNormalApply={workbench.ApplySelectedStepParameterDraftCommand.CanExecute(null)}");
            workbench.DiscardSelectedStepParameterDraft();
            filterDraft = workbench.SelectedStepPropertyDraft as FilterStepProperties
                ?? throw new InvalidOperationException("Filter draft was not restored after preset cancellation.");

            filterDraft.KernelSize = 4;
            var invalidApplied = workbench.TryApplySelectedStepParameterDraft(out var invalidMessage);
            Check(
                "invalid typed value cannot alter recipe",
                !invalidApplied && filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value == "3",
                invalidMessage);

            filterDraft.KernelSize = 5;
            var filterApplied = workbench.TryApplySelectedStepParameterDraft(out var filterMessage);
            Check(
                "Apply updates known values and preserves unknown values",
                filterApplied
                && filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value == "5"
                && filter.Parameters.Single(parameter => parameter.Name == "BoundaryPolicy").Value == "AvailableNeighbors"
                && filter.Parameters.Single(parameter => parameter.Name == "FuturePolicy").Value == "RetainMe"
                && workbench.IsDirty
                && !workbench.HasCurrentFilterPreview,
                filterMessage);

            var edge = AddTool(workbench, "height-difference-edge");
            edge.InputEntityIdsText = filter.OutputEntityId;
            edge.Parameters.Add(new ToolWorkbenchParameterItem("FutureTiePolicy", "Stable"));
            workbench.SelectPipelineStepCommand.Execute(filter.Id);
            var selectionCommandSelectedFilter = ReferenceEquals(workbench.SelectedPipelineStep, filter);
            workbench.SelectPipelineStepCommand.Execute(edge.Id);
            Check(
                "Tool Lab step activation routes through the ViewModel selection command",
                selectionCommandSelectedFilter && ReferenceEquals(workbench.SelectedPipelineStep, edge),
                $"filterSelected={selectionCommandSelectedFilter}; current={workbench.SelectedPipelineStep?.Id}");

            var actionWorkbench = new ToolWorkbenchViewModel(Path.Combine(fixtureRoot, "step-actions-recent.json"));
            actionWorkbench.SetC3DSource(sourcePath);
            var actionFilter = AddTool(actionWorkbench, "filter");
            var actionEdge = AddTool(actionWorkbench, "height-difference-edge");
            var actionSelection = new ToolRecipeSelection(
                "selection.action-edge",
                "Action Edge ROI",
                ToolRecipeSelectionKinds.GridRectangle,
                actionWorkbench.Source.Id,
                actionWorkbench.Source.FrameId,
                ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(sourcePath),
                new ToolRecipeGridRectangle(0, 0, 2, 2),
                null,
                null);
            actionWorkbench.Selections.Add(actionSelection);
            actionEdge.InputEntityIdsText = $"{actionFilter.OutputEntityId}; {actionSelection.Id}";
            actionWorkbench.MoveSelectedStepUpCommand.Execute(null);
            Check(
                "selected-step move command reorders without inspection execution",
                ReferenceEquals(actionWorkbench.PipelineSteps[0], actionEdge)
                && ReferenceEquals(actionWorkbench.SelectedPipelineStep, actionEdge)
                && !actionWorkbench.HasCurrentFilterPreview
                && !actionWorkbench.HasCurrentEdgePreview,
                $"order={string.Join(",", actionWorkbench.PipelineSteps.Select(step => step.ToolId))}; filterPreview={actionWorkbench.HasCurrentFilterPreview}; edgePreview={actionWorkbench.HasCurrentEdgePreview}");
            ToolWorkbenchStepRemovalRequestEventArgs? removalRequest = null;
            actionWorkbench.RemoveSelectedStepRequested += (_, args) => removalRequest = args;
            var removalDirtyBefore = actionWorkbench.IsDirty;
            var removalLogCountBefore = actionWorkbench.RunLog.Count;
            actionWorkbench.RemoveSelectedStepCommand.Execute(null);
            Check(
                "selected-step remove command requests impact-aware confirmation without mutation",
                removalRequest is
                {
                    StepId: var requestedStepId,
                    StepName: var requestedStepName,
                    OrphanedSelectionNames.Count: 1
                }
                && requestedStepId == actionEdge.Id
                && requestedStepName == actionEdge.ToolName
                && removalRequest.OrphanedSelectionNames[0] == actionSelection.Name
                && actionWorkbench.PipelineSteps.Count == 2
                && actionWorkbench.Selections.Contains(actionSelection)
                && ReferenceEquals(actionWorkbench.SelectedPipelineStep, actionEdge)
                && actionWorkbench.IsDirty == removalDirtyBefore
                && actionWorkbench.RunLog.Count == removalLogCountBefore,
                $"request={removalRequest?.StepId}; selections={removalRequest?.OrphanedSelectionNames.Count}; steps={actionWorkbench.PipelineSteps.Count}; dirty={removalDirtyBefore}->{actionWorkbench.IsDirty}; logs={removalLogCountBefore}->{actionWorkbench.RunLog.Count}");
            var validationExecutionOwnerField = typeof(ToolWorkbenchViewModel).GetField(
                "validationSetExecutionOwner",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var validationExecutionOwner = validationExecutionOwnerField?.GetValue(actionWorkbench)
                as ToolWorkbenchValidationSetExecutionOwner;
            var validationRunningProperty = typeof(ToolWorkbenchValidationSetExecutionOwner).GetProperty(
                nameof(ToolWorkbenchValidationSetExecutionOwner.IsRunning));
            validationRunningProperty?.SetValue(validationExecutionOwner, true);
            var removalBlocked = !actionWorkbench.RemoveSelectedStepCommand.CanExecute(null)
                && !actionWorkbench.ConfirmSelectedStepRemoval(actionEdge.Id)
                && actionWorkbench.PipelineSteps.Count == 2
                && actionWorkbench.Selections.Contains(actionSelection);
            validationRunningProperty?.SetValue(validationExecutionOwner, false);
            Check(
                "selected-step removal is unavailable and fails closed during validation",
                validationExecutionOwner is not null
                && validationRunningProperty is not null
                && removalBlocked,
                $"owner={validationExecutionOwner is not null};runningProperty={validationRunningProperty is not null};blocked={removalBlocked}; steps={actionWorkbench.PipelineSteps.Count}; selections={actionWorkbench.Selections.Count}");
            var removalConfirmed = actionWorkbench.ConfirmSelectedStepRemoval(actionEdge.Id);
            Check(
                "confirmed selected-step removal deletes only the step and its orphaned selection",
                removalConfirmed
                && actionWorkbench.PipelineSteps.Count == 1
                && ReferenceEquals(actionWorkbench.PipelineSteps[0], actionFilter)
                && !actionWorkbench.Selections.Contains(actionSelection)
                && !actionWorkbench.HasCurrentFilterPreview
                && !actionWorkbench.HasCurrentEdgePreview
                && actionWorkbench.RunLog.Count == removalLogCountBefore + 1
                && actionWorkbench.RunLog[0].Category == "Teach",
                $"confirmed={removalConfirmed}; remaining={string.Join(",", actionWorkbench.PipelineSteps.Select(step => step.ToolId))}; selections={actionWorkbench.Selections.Count}; logs={removalLogCountBefore}->{actionWorkbench.RunLog.Count}; filterPreview={actionWorkbench.HasCurrentFilterPreview}; edgePreview={actionWorkbench.HasCurrentEdgePreview}");

            var edgeDraft = (HeightDifferenceEdgeStepProperties)workbench.SelectedStepPropertyDraft!;
            edgeDraft.ComparisonAxis = HeightDifferenceEdgeComparisonAxis.AcrossColumns;
            edgeDraft.Polarity = HeightDifferenceEdgePolarity.Rising;
            edgeDraft.MinimumDelta = 0;
            workbench.MarkSelectedStepParameterDraftDirty();
            var invalidEdgeApplied = workbench.TryApplySelectedStepParameterDraft(out var invalidEdgeMessage);
            Check(
                "Height Difference Edge rejects non-positive delta",
                !invalidEdgeApplied && edge.Parameters.Single(parameter => parameter.Name == "MinimumDelta").Value == "Set explicitly",
                invalidEdgeMessage);

            edgeDraft.MinimumDelta = 2.5;
            var edgeApplied = workbench.TryApplySelectedStepParameterDraft(out var edgeMessage);
            Check(
                "Height Difference Edge applies invariant typed values without execution",
                edgeApplied
                && edge.Parameters.Single(parameter => parameter.Name == "ComparisonAxis").Value == "AcrossColumns"
                && edge.Parameters.Single(parameter => parameter.Name == "MinimumDelta").Value == "2.5"
                && edge.Parameters.Single(parameter => parameter.Name == "FutureTiePolicy").Value == "Stable"
                && !workbench.HasCurrentEdgePreview,
                edgeMessage);

            var affine = AddTool(workbench, "xyz-affine-solve");
            affine.InputEntityIdsText = edge.OutputEntityId;
            var affineDraft = (XYZAffineSolveStepProperties)workbench.SelectedStepPropertyDraft!;
            affineDraft.MaximumConditionEstimate = 500000;
            affineDraft.ArithmeticResidualWarning = 0.0025;
            workbench.MarkSelectedStepParameterDraftDirty();
            var affineApplied = workbench.TryApplySelectedStepParameterDraft(out var affineMessage);
            Check(
                "XYZ Affine Solve maps typed numerical limits without execution",
                affineApplied
                && affine.Parameters.Single(parameter => parameter.Name == "SolvePolicy").Value == "ExactFourPartialPivot"
                && MatchesInvariantNumber(affine.Parameters.Single(parameter => parameter.Name == "MaximumConditionEstimate").Value, 500000)
                && MatchesInvariantNumber(affine.Parameters.Single(parameter => parameter.Name == "ArithmeticResidualWarning").Value, 0.0025)
                && !workbench.HasCurrentAffineSolvePreview,
                affineMessage);

            var unsupported = AddTool(workbench, "overlay-control-review");
            var unsupportedValue = unsupported.Parameters[0].Value;
            Check(
                "unsupported step stays visible and read-only",
                workbench.SelectedStepPropertyDraft is null
                && workbench.SelectedStepAdapterStatus.Contains("Partially supported", StringComparison.Ordinal)
                && workbench.UnsupportedStepCount == 1,
                workbench.RecipeAdapterCoverageSummary);

            var saved = workbench.TrySaveTeachingRecipe(recipePath, out var saveMessage);
            Check(
                "atomic save completes without a temporary sibling",
                saved && File.Exists(recipePath) && !Directory.EnumerateFiles(fixtureRoot, "*.tmp.*").Any(),
                saveMessage);

            var reopened = new ToolWorkbenchViewModel(recentPath);
            var opened = reopened.TryOpenTeachingRecipe(recipePath, out var openMessage);
            var reopenedFilter = reopened.PipelineSteps.Single(step => step.ToolId == "filter");
            var reopenedEdge = reopened.PipelineSteps.Single(step => step.ToolId == "height-difference-edge");
            var reopenedAffine = reopened.PipelineSteps.Single(step => step.ToolId == "xyz-affine-solve");
            var reopenedUnsupported = reopened.PipelineSteps.Single(step => step.ToolId == "overlay-control-review");
            Check(
                "save and reopen preserve typed and unknown parameters",
                opened
                && reopenedFilter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value == "5"
                && reopenedFilter.Parameters.Single(parameter => parameter.Name == "FuturePolicy").Value == "RetainMe"
                && reopenedEdge.Parameters.Single(parameter => parameter.Name == "MinimumDelta").Value == "2.5"
                && reopenedEdge.Parameters.Single(parameter => parameter.Name == "FutureTiePolicy").Value == "Stable"
                && MatchesInvariantNumber(reopenedAffine.Parameters.Single(parameter => parameter.Name == "MaximumConditionEstimate").Value, 500000)
                && MatchesInvariantNumber(reopenedAffine.Parameters.Single(parameter => parameter.Name == "ArithmeticResidualWarning").Value, 0.0025)
                && reopenedUnsupported.Parameters[0].Value == unsupportedValue,
                openMessage);
            Check(
                "open does not create Preview or Publish evidence",
                !reopened.HasCurrentFilterPreview && !reopened.HasCurrentEdgePreview,
                $"filterPreview={reopened.HasCurrentFilterPreview}; edgePreview={reopened.HasCurrentEdgePreview}");

            var priorName = reopened.RecipeName;
            var invalidPath = Path.Combine(fixtureRoot, "invalid.ov3d-teach.json");
            File.WriteAllText(invalidPath, "{ invalid json");
            var invalidOpened = reopened.TryOpenTeachingRecipe(invalidPath, out var invalidOpenMessage);
            Check(
                "invalid candidate leaves active session unchanged",
                !invalidOpened && reopened.RecipeName == priorName && reopened.RecipePath == recipePath,
                invalidOpenMessage);

            var stored = ToolRecipeDocumentStore.Load(recipePath);
            var missingPath = Path.Combine(fixtureRoot, "missing-source.ov3d-teach.json");
            ToolRecipeDocumentStore.Save(
                missingPath,
                stored with { Source = stored.Source with { Path = Path.Combine(fixtureRoot, "missing.C3D") } });
            var missing = new ToolWorkbenchViewModel(Path.Combine(fixtureRoot, "missing-recent.json"));
            var missingOpened = missing.TryOpenTeachingRecipe(missingPath, out _);
            Check(
                "missing source opens in repair state without execution",
                missingOpened && !missing.IsSourceReadyForRecipe && missing.SourceReadinessSummary.Contains("missing", StringComparison.OrdinalIgnoreCase),
                missing.SourceReadinessSummary);

            var mismatchPath = Path.Combine(fixtureRoot, "mismatch-source.ov3d-teach.json");
            ToolRecipeDocumentStore.Save(
                mismatchPath,
                stored with { Source = stored.Source with { ContentSha256 = new string('0', 64) } });
            var mismatch = new ToolWorkbenchViewModel(Path.Combine(fixtureRoot, "mismatch-recent.json"));
            var mismatchOpened = mismatch.TryOpenTeachingRecipe(mismatchPath, out _);
            Check(
                "source identity mismatch blocks readiness",
                mismatchOpened && !mismatch.IsSourceReadyForRecipe && mismatch.SourceReadinessSummary.Contains("mismatch", StringComparison.OrdinalIgnoreCase),
                mismatch.SourceReadinessSummary);

            var recentCandidates = Enumerable.Range(0, 12).Select(index => Path.Combine(fixtureRoot, $"recent-{index:00}.json")).ToArray();
            RecipeRecentFileStore.Save(recentPath, recentCandidates);
            var recent = RecipeRecentFileStore.Load(recentPath);
            Check(
                "Recent recipe persistence is bounded to ten distinct paths",
                recent.Count == RecipeRecentFileStore.MaximumEntries && recent.SequenceEqual(recentCandidates.Take(10)),
                $"count={recent.Count}; max={RecipeRecentFileStore.MaximumEntries}");

            var startupRecentPath = Path.Combine(fixtureRoot, "startup-recent.json");
            RecipeRecentFileStore.Save(startupRecentPath, [Path.Combine(fixtureRoot, "missing-recipe.json"), recipePath]);
            var startupRecent = new ToolWorkbenchViewModel(startupRecentPath);
            Check(
                "startup recipe selection skips missing recent entries",
                string.Equals(startupRecent.MostRecentAvailableRecipePath, recipePath, StringComparison.OrdinalIgnoreCase),
                startupRecent.MostRecentAvailableRecipePath ?? "<none>");

            var appResourcesHadThemeKey = Application.Current.Resources.Contains("Ovl3D.Wpg.SurfaceBrush");
            var host = new RecipeStepPropertyGridHost
            {
                Width = 620,
                Height = 360,
                SelectedObject = new WpgProbeProperties()
            };
            host.Measure(new Size(620, 360));
            host.Arrange(new Rect(0, 0, 620, 360));
            host.UpdateLayout();
            var probeCount = host.VisiblePropertyCount;
            host.SetPropertyFilter("Delta");
            host.UpdateLayout();
            var matchingCount = host.MatchingPropertyCount;
            host.SelectedObject = FilterStepProperties.From(reopenedFilter);
            host.UpdateLayout();
            var swapCount = host.VisiblePropertyCount;
            var committed = host.CommitPendingEdit(out var commitMessage);
            Check(
                "WPG host renders bool, enum, double/range categories and search",
                probeCount == 3 && host.HasCategories && matchingCount == 1,
                $"properties={probeCount}; categories={host.HasCategories}; matchingDelta={matchingCount}");
            Check(
                "WPG SelectedObject swap and CommitPendingEdit succeed",
                swapCount == 5 && committed,
                $"swapProperties={swapCount}; commit={committed}; {commitMessage}");
            Check(
                "WPG theme keys stay view-local",
                !appResourcesHadThemeKey && !Application.Current.Resources.Contains("Ovl3D.Wpg.SurfaceBrush"),
                "Application resources contain no Ovl3D.Wpg.SurfaceBrush key before or after host creation.");
            var themeResourcePairs = new (string LocalKey, string ProductKey)[]
            {
                ("Ovl3D.Wpg.SurfaceBrush", "ThreeD.PanelAlternateBrush"),
                ("Ovl3D.Wpg.PanelBrush", "ThreeD.PanelBrush"),
                ("Ovl3D.Wpg.RowHoverBrush", "ThreeD.SelectedSurfaceBrush"),
                ("Ovl3D.Wpg.NameColumnBrush", "ThreeD.CommandBarBrush"),
                ("Ovl3D.Wpg.LineBrush", "ThreeD.DividerBrush"),
                ("Ovl3D.Wpg.TextBrush", "ThreeD.TextBrush"),
                ("Ovl3D.Wpg.MutedTextBrush", "ThreeD.MutedTextBrush"),
                ("Ovl3D.Wpg.AccentBrush", "ThreeD.AccentBrush"),
                ("Ovl3D.Wpg.EditorFocusBrush", "ThreeD.FocusBrush"),
                ("Ovl3D.Wpg.EditorBackgroundBrush", "ThreeD.ControlBrush"),
                ("Ovl3D.Wpg.EditorReadOnlyBrush", "ThreeD.DisabledSurfaceBrush"),
                ("Ovl3D.Wpg.EditorTextBrush", "ThreeD.PrimaryTextBrush"),
                ("Ovl3D.Wpg.EditorMutedBrush", "ThreeD.DisabledBrush")
            };
            var themeMismatches = themeResourcePairs
                .Where(pair =>
                    host.TryFindResource(pair.LocalKey) is not SolidColorBrush localBrush
                    || Application.Current.TryFindResource(pair.ProductKey) is not SolidColorBrush productBrush
                    || localBrush.Color != productBrush.Color)
                .Select(pair => $"{pair.LocalKey}->{pair.ProductKey}")
                .ToArray();
            Check(
                "WPG surface, text, editor, focus, read-only, and disabled roles alias the product theme",
                themeMismatches.Length == 0,
                themeMismatches.Length == 0
                    ? $"aliases={themeResourcePairs.Length}"
                    : string.Join(",", themeMismatches));

            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
            var localizedRegrid = LocalizedPropertyGridObject.Create(new RegridHeightMapStepProperties());
            var koreanRegridProperties = TypeDescriptor.GetProperties(localizedRegrid);
            var koreanReferenceFrame = koreanRegridProperties[nameof(RegridHeightMapStepProperties.ReferenceFrameId)];
            var koreanCoverage = koreanRegridProperties[nameof(RegridHeightMapStepProperties.MinimumCoverageRatio)];
            var koreanReferenceFrameDisplayName = koreanReferenceFrame?.DisplayName;
            var koreanReferenceFrameCategory = koreanReferenceFrame?.Category;
            var koreanCoverageDisplayName = koreanCoverage?.DisplayName;
            var koreanCoverageCategory = koreanCoverage?.Category;
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
            var englishRegrid = LocalizedPropertyGridObject.Create(new RegridHeightMapStepProperties());
            var englishRegridProperties = TypeDescriptor.GetProperties(englishRegrid);
            var englishReferenceFrame = englishRegridProperties[nameof(RegridHeightMapStepProperties.ReferenceFrameId)];
            var englishReferenceFrameDisplayName = englishReferenceFrame?.DisplayName;
            var englishReferenceFrameCategory = englishReferenceFrame?.Category;
            OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
            Check(
                "WPG display metadata localizes without renaming the typed Re-grid properties",
                koreanReferenceFrame?.Name == nameof(RegridHeightMapStepProperties.ReferenceFrameId)
                && koreanReferenceFrameDisplayName == "기준 프레임 ID"
                && koreanReferenceFrameCategory == "A3 기준 그리드"
                && koreanCoverageDisplayName == "최소 커버리지 비율"
                && koreanCoverageCategory == "A3 게시 정책"
                && englishReferenceFrame?.Name == nameof(RegridHeightMapStepProperties.ReferenceFrameId)
                && englishReferenceFrameDisplayName == "Reference frame ID"
                && englishReferenceFrameCategory == "A3 reference grid",
                $"ko={koreanReferenceFrameDisplayName}/{koreanReferenceFrameCategory}; coverage={koreanCoverageDisplayName}/{koreanCoverageCategory}; en={englishReferenceFrameDisplayName}/{englishReferenceFrameCategory}");

            var messageDialog = new WpfMessageDialogControl();
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
            messageDialog.Configure(new WpfMessageDialogOptions
            {
                Title = "레시피 저장",
                Message = "레시피 파일을 저장할 수 없습니다.",
                Details = "Access to the selected recipe folder was denied.",
                Kind = WpfMessageDialogKind.Warning,
                Buttons = WpfMessageDialogButtons.YesNoCancel
            });
            var koreanDialogButtons = ((StackPanel)messageDialog.FindName("ButtonPanel")).Children
                .OfType<Button>()
                .Select(button => button.Content?.ToString())
                .ToArray();
            var koreanDetailsLabel = ((Button)messageDialog.FindName("DetailsToggleButton")).Content?.ToString();
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
            messageDialog.Configure(new WpfMessageDialogOptions
            {
                Title = "Save Recipe",
                Message = "The recipe file could not be saved.",
                Details = "Access to the selected recipe folder was denied.",
                Kind = WpfMessageDialogKind.Warning,
                Buttons = WpfMessageDialogButtons.YesNoCancel
            });
            var englishDialogButtons = ((StackPanel)messageDialog.FindName("ButtonPanel")).Children
                .OfType<Button>()
                .Select(button => button.Content?.ToString())
                .ToArray();
            var englishDetailsLabel = ((Button)messageDialog.FindName("DetailsToggleButton")).Content?.ToString();
            OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
            Check(
                "shared WPF message dialog localizes buttons and technical-details affordance",
                koreanDialogButtons.SequenceEqual(["예", "아니오", "취소"])
                && koreanDetailsLabel == "상세 정보"
                && englishDialogButtons.SequenceEqual(["Yes", "No", "Cancel"])
                && englishDetailsLabel == "Technical Details",
                $"ko={string.Join('/', koreanDialogButtons)}; details={koreanDetailsLabel} | en={string.Join('/', englishDialogButtons)}; details={englishDetailsLabel}");

            WpfMessageDialogResult requestedResult = WpfMessageDialogResult.None;
            messageDialog.DialogResultRequested += result => requestedResult = result;
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
            messageDialog.Configure(new WpfMessageDialogOptions
            {
                Title = "저장하지 않은 레시피",
                Message = "현재 레시피의 변경 내용을 저장하시겠습니까?",
                Kind = WpfMessageDialogKind.Question,
                Buttons = WpfMessageDialogButtons.YesNoCancel,
                PrimaryButtonText = "저장",
                SecondaryButtonText = "저장 안 함",
                TertiaryButtonText = "취소"
            });
            var lifecycleButtons = ((StackPanel)messageDialog.FindName("ButtonPanel")).Children
                .OfType<Button>()
                .ToArray();
            lifecycleButtons[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
            Check(
                "unsaved recipe dialog maps 저장 안 함 to the No continuation branch",
                lifecycleButtons.Select(button => button.Content?.ToString()).SequenceEqual(["저장", "저장 안 함", "취소"])
                && requestedResult == WpfMessageDialogResult.No,
                $"buttons={string.Join('/', lifecycleButtons.Select(button => button.Content))}; result={requestedResult}");
        }
        catch (Exception exception)
        {
            total++;
            lines.Add($"FAIL | unhandled exception | {exception}");
        }
        finally
        {
            try
            {
                Directory.Delete(fixtureRoot, recursive: true);
            }
            catch
            {
                // The report still records a functional result if temporary cleanup is delayed.
            }
        }

        var success = total > 0 && passed == total;
        lines.Add($"RESULT | {(success ? "PASS" : "FAIL")} | {passed}/{total}");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"RecipeManagerWpg|pass={success}|checks={passed}/{total}|report={fullReportPath}";
        return success;
    }

    private static ToolWorkbenchPipelineStepItem AddTool(ToolWorkbenchViewModel workbench, string toolId)
    {
        var tool = workbench.Tools.Single(item => item.Id == toolId);
        if (SourceQualityToolGate.RequiresSourceQuality(tool.Id))
        {
            WaitForSourceQuality(workbench.SourceQuality);
        }

        workbench.SelectedTool = tool;
        if (!workbench.AddSelectedToolCommand.CanExecute(null))
        {
            throw new InvalidOperationException(
                $"Tool '{toolId}' cannot be added: {workbench.SelectedToolProposedRouteDetail}");
        }

        workbench.AddSelectedToolCommand.Execute(null);
        return workbench.SelectedPipelineStep
            ?? throw new InvalidOperationException($"Tool '{toolId}' was not added.");
    }

    private static SourceQualityReport? WaitForSourceQuality(
        SourceQualityWorkspaceViewModel workspace)
    {
        if (workspace.Report is not null || workspace.HasError)
        {
            return workspace.Report;
        }

        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(10)
        };
        timer.Tick += (_, _) =>
        {
            if (workspace.Report is not null
                || workspace.HasError
                || DateTimeOffset.UtcNow >= deadline)
            {
                frame.Continue = false;
            }
        };
        timer.Start();
        try
        {
            Dispatcher.PushFrame(frame);
        }
        finally
        {
            timer.Stop();
        }

        return workspace.Report;
    }

    private static bool MatchesInvariantNumber(string text, double expected) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var actual)
        && Math.Abs(actual - expected) <= 1e-12;

    [CategoryOrder("Probe", 0)]
    private sealed class WpgProbeProperties
    {
        [Category("Probe")]
        [PropertyOrder(0)]
        public bool Enabled { get; set; } = true;

        [Category("Probe")]
        [PropertyOrder(1)]
        public HeightDifferenceEdgePolarity Polarity { get; set; } = HeightDifferenceEdgePolarity.Rising;

        [Category("Probe")]
        [PropertyOrder(2)]
        [NumberRange(0, 100, 0.5, 2)]
        public double Delta { get; set; } = 2.5;
    }
}
