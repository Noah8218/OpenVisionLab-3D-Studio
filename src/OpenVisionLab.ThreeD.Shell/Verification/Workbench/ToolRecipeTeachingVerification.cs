using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell;

/// <summary>
/// Exercises Tool Recipe teaching from source selection through ordered
/// routing, JSON persistence, reopen, and invalid-route rejection. Execution
/// remains covered separately by the Filter adapter verification.
/// </summary>
internal static class ToolRecipeTeachingVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        var lines = new List<string>
        {
            "OpenVisionLab 3D Tool Recipe teaching verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;
        var total = 0;
        var fixtureRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenVisionLab.ThreeD",
            "ToolRecipeTeachingVerification",
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
            var sourcePath = Path.Combine(fixtureRoot, "teaching-source.C3D");
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.fixture",
                2,
                2,
                [1.0, 2.0, 3.0, 4.0]).SaveC3D(sourcePath);
            var recipePath = Path.Combine(fixtureRoot, "fixture.ov3d-teach.json");
            var emptyRecipePath = Path.Combine(fixtureRoot, "empty.ov3d-recipe.json");

            var routeWorkbench = new ToolWorkbenchViewModel(Path.Combine(fixtureRoot, "recent-route.json"));
            routeWorkbench.SetC3DSource(sourcePath, markDirty: false);
            routeWorkbench.SelectedTool = routeWorkbench.Tools.Single(tool => tool.Id == "thickness");
            routeWorkbench.ToolSearchText = "Thickness";
            var selectedStepSetupRequestCount = 0;
            routeWorkbench.SelectedStepSetupRequested += (_, _) => selectedStepSetupRequestCount++;
            Check(
                "proposed Add route exposes actual input and output types before insertion",
                routeWorkbench.IsSelectedToolProposedRouteCompatible
                && routeWorkbench.SelectedToolProposedRouteDetail.Contains(
                    "source.c3d.height-map [SourceC3D / RawHeightField]",
                    StringComparison.Ordinal)
                && routeWorkbench.SelectedToolProposedRouteDetail.Contains(
                    "MeasurementResult",
                    StringComparison.Ordinal),
                routeWorkbench.SelectedToolProposedRouteDetail);
            var routeActionLogCount = routeWorkbench.RunLog.Count(item =>
                item.Category is "Preview" or "Publish" or "Run");
            var routeThickness = AddTool(routeWorkbench, "Thickness");
            Check(
                "successful Add clears the Tool Library search without execution",
                string.IsNullOrEmpty(routeWorkbench.ToolSearchText)
                && routeWorkbench.RunLog.Count(item =>
                    item.Category is "Preview" or "Publish" or "Run") == routeActionLogCount,
                $"search='{routeWorkbench.ToolSearchText}'; actionLogs={routeActionLogCount}");
            Check(
                "Add requests the selected-step setup surface exactly once without execution",
                selectedStepSetupRequestCount == 1
                && ReferenceEquals(routeThickness, routeWorkbench.SelectedPipelineStep)
                && routeWorkbench.RunLog.All(item => item.Category is not ("Preview" or "Publish" or "Run")),
                $"requests={selectedStepSetupRequestCount}; selected={routeWorkbench.SelectedPipelineStep?.Id}");
            routeWorkbench.SelectedTool = routeWorkbench.Tools.Single(tool => tool.Id == "warpage");
            var routeWarpageProposal = routeWorkbench.SelectedToolProposedRouteDetail;
            var routeWarpage = AddTool(routeWorkbench, "Warpage");
            Check(
                "HeightField Add skips the last MeasurementResult and routes to the compatible source",
                routeWarpage.InputEntityIds.SequenceEqual([routeWorkbench.Source.Id])
                && routeWarpageProposal.Contains(routeWorkbench.Source.Id, StringComparison.Ordinal)
                && !routeWarpageProposal.Contains(routeThickness.OutputEntityId, StringComparison.Ordinal),
                $"proposal={routeWarpageProposal}; route={routeWarpage.InputSummary}");
            routeWorkbench.SelectedTool = routeWorkbench.Tools.Single(tool => tool.Id == "plane-flatness");
            routeWorkbench.ToolSearchText = "Plane Flatness";
            var rejectedAddStepCount = routeWorkbench.PipelineSteps.Count;
            var rejectedAddActionLogCount = routeWorkbench.RunLog.Count(item =>
                item.Category is "Preview" or "Publish" or "Run");
            Check(
                "Add is unavailable when no TransformedHeightField route exists",
                !routeWorkbench.AddSelectedToolCommand.CanExecute(routeWorkbench.SelectedTool)
                && !routeWorkbench.IsSelectedToolProposedRouteCompatible
                && routeWorkbench.SelectedToolProposedRouteDetail.Contains(
                    "TransformedHeightField",
                    StringComparison.Ordinal),
                routeWorkbench.SelectedToolProposedRouteDetail);
            routeWorkbench.AddSelectedToolCommand.Execute(routeWorkbench.SelectedTool);
            Check(
                "rejected Add retains the visible search and changes no recipe or execution state",
                routeWorkbench.ToolSearchText == "Plane Flatness"
                && routeWorkbench.PipelineSteps.Count == rejectedAddStepCount
                && routeWorkbench.RunLog.Count(item =>
                    item.Category is "Preview" or "Publish" or "Run") == rejectedAddActionLogCount,
                $"search='{routeWorkbench.ToolSearchText}'; steps={routeWorkbench.PipelineSteps.Count}; actionLogs={rejectedAddActionLogCount}");
            Check(
                "compatible Add does not invoke Preview, Publish, or Run",
                routeWorkbench.RunLog.Count(item =>
                    item.Category is "Preview" or "Publish" or "Run") == routeActionLogCount,
                $"actionLogs={routeActionLogCount}");

            routeWarpage.InputEntityIdsText = routeThickness.OutputEntityId;
            var legacyRecipePath = Path.Combine(fixtureRoot, "legacy-incompatible-route.ov3d-recipe.json");
            Check(
                "legacy incompatible route is diagnosed and remains loadable for repair",
                routeWorkbench.FlowPortDiagnostics.Any(item =>
                    ReferenceEquals(item.Step, routeWarpage)
                    && item.Status == routeWorkbench.Localization.FlowPortIncompatible)
                && routeWorkbench.ValidationMessages.Any(item => item.Message.Contains(
                    "is MeasurementResult; HeightField is required.",
                    StringComparison.Ordinal))
                && routeWorkbench.TrySaveTeachingRecipe(legacyRecipePath, out _),
                routeWorkbench.FlowProblemsSummary);
            var repairWorkbench = new ToolWorkbenchViewModel(Path.Combine(fixtureRoot, "recent-repair.json"));
            repairWorkbench.ToolSearchText = "Warpage";
            var legacyOpened = repairWorkbench.TryOpenTeachingRecipe(legacyRecipePath, out var legacyOpenMessage);
            var legacyProblem = repairWorkbench.FlowPortDiagnostics.FirstOrDefault(item =>
                item.Step.ToolId == "warpage"
                && item.Status == repairWorkbench.Localization.FlowPortIncompatible);
            repairWorkbench.SelectedPipelineStep = legacyProblem?.Step;
            var directRepairProblem = repairWorkbench.SelectedStepFlowProblem;
            repairWorkbench.FocusFlowProblemStepCommand.Execute(directRepairProblem);
            Check(
                "selected legacy step exposes Repair route and opens input editing without execution",
                legacyOpened
                && legacyProblem is not null
                && repairWorkbench.HasSelectedStepFlowProblem
                && directRepairProblem is not null
                && ReferenceEquals(directRepairProblem, legacyProblem)
                && ReferenceEquals(repairWorkbench.SelectedPipelineStep, directRepairProblem.Step)
                && repairWorkbench.IsSelectedToolInputSectionExpanded
                && repairWorkbench.IsAdvancedInputRouteEditingExpanded
                && repairWorkbench.RunLog.All(item => item.Category is not ("Preview" or "Publish" or "Run")),
                legacyOpenMessage);
            Check(
                "successful recipe open clears the Tool Library search without execution",
                string.IsNullOrEmpty(repairWorkbench.ToolSearchText)
                && repairWorkbench.RunLog.All(item => item.Category is not ("Preview" or "Publish" or "Run")),
                $"search='{repairWorkbench.ToolSearchText}'");
            repairWorkbench.SelectedPipelineStep!.InputEntityIdsText = repairWorkbench.Source.Id;
            var repairedRecipePath = Path.Combine(fixtureRoot, "repaired-valid-route.ov3d-recipe.json");
            var repairedSaved = repairWorkbench.TrySaveTeachingRecipe(repairedRecipePath, out var repairedSaveMessage);
            var reopenedRouteWorkbench = new ToolWorkbenchViewModel(Path.Combine(fixtureRoot, "recent-reopened-route.json"));
            var repairedOpened = reopenedRouteWorkbench.TryOpenTeachingRecipe(repairedRecipePath, out _);
            Check(
                "valid repaired route survives save and reopen",
                repairedSaved
                && repairedOpened
                && reopenedRouteWorkbench.PipelineSteps.Single(step => step.ToolId == "warpage")
                    .InputEntityIds.SequenceEqual([reopenedRouteWorkbench.Source.Id]),
                repairedSaveMessage);

            var alignment = new ToolWorkbenchViewModel();
            Check(
                "alignment summary reports no taught stage initially",
                alignment.AlignmentStatusSummary == "Alignment not taught",
                alignment.AlignmentStatusSummary);
            alignment.SetC3DSource(sourcePath, markDirty: false);
            var legacyTool = new ToolWorkbenchToolItem(
                "Transform", "XYZ Affine Transform", "xyz-affine-transform", 1,
                "Legacy input", "Legacy output", "Verification-only legacy recipe step.", []);
            var legacyStep = new ToolWorkbenchPipelineStepItem(
                "step.legacy-affine", legacyTool, alignment.Source.Id, "legacy.affine");
            alignment.PipelineSteps.Add(legacyStep);
            Check(
                "legacy alignment summary reports the legacy step state",
                alignment.AlignmentStatusSummary == $"Legacy XYZ Affine Transform | {legacyStep.State}",
                alignment.AlignmentStatusSummary);
            var alignmentSolve = AddTool(alignment, "XYZ Affine Solve");
            Check(
                "A1 alignment summary supersedes the legacy stage",
                alignment.AlignmentStatusSummary == $"A1 XYZ Affine Solve | {alignmentSolve.State}",
                alignment.AlignmentStatusSummary);
            var alignmentApply = AddTool(alignment, "Apply XYZ Affine");
            Check(
                "A2 alignment summary supersedes A1",
                alignment.AlignmentStatusSummary == $"A2 Apply XYZ Affine | {alignmentApply.State}",
                alignment.AlignmentStatusSummary);
            var alignmentRegrid = AddTool(alignment, "Re-grid Height Map");
            Check(
                "A3 alignment summary supersedes A2",
                alignment.AlignmentStatusSummary == $"A3 Re-grid Height Map | {alignmentRegrid.State}",
                alignment.AlignmentStatusSummary);
            var alignmentSummaryNotifications = 0;
            alignment.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ToolWorkbenchViewModel.AlignmentStatusSummary))
                {
                    alignmentSummaryNotifications++;
                }
            };
            var actionLogCount = alignment.RunLog.Count(item => item.Category is "Preview" or "Publish" or "Run");
            alignmentRegrid.State = "Published";
            Check(
                "alignment step state change refreshes the header summary",
                alignmentSummaryNotifications == 1
                && alignment.AlignmentStatusSummary == "A3 Re-grid Height Map | Published",
                $"notifications={alignmentSummaryNotifications}; summary={alignment.AlignmentStatusSummary}");
            Check(
                "alignment status refresh causes no Preview, Publish, or Run action",
                alignment.RunLog.Count(item => item.Category is "Preview" or "Publish" or "Run") == actionLogCount,
                $"actionLogs={actionLogCount}");

            var workbench = new ToolWorkbenchViewModel();
            Check(
                "initial empty recipe is clean and unsaved",
                !workbench.IsDirty && string.IsNullOrWhiteSpace(workbench.RecipePath),
                workbench.RecipeStateSummary);
            var sourceLoadCancellationRequested = false;
            workbench.CancelC3DSourceLoadRequested += (_, _) => sourceLoadCancellationRequested = true;
            workbench.BeginC3DSourceLoad(sourcePath);
            workbench.ReportC3DSourceLoadProgress(42.0);
            workbench.CancelC3DSourceLoadCommand.Execute(null);
            Check(
                "C3D source load state exposes progress, disables Open, and routes Cancel",
                workbench.IsC3DSourceLoading
                && Math.Abs(workbench.C3DSourceLoadProgressPercent - 42.0) < 0.001
                && workbench.C3DSourceLoadStatus.Contains(Path.GetFileName(sourcePath), StringComparison.Ordinal)
                && !workbench.LoadC3DSourceCommand.CanExecute(null)
                && sourceLoadCancellationRequested,
                $"loading={workbench.IsC3DSourceLoading}; progress={workbench.C3DSourceLoadProgressPercent:F1}; cancel={sourceLoadCancellationRequested}");
            workbench.CancelC3DSourceLoad(elapsedMilliseconds: 1);
            Check(
                "cancelled C3D load restores Open without modifying the recipe",
                !workbench.IsC3DSourceLoading
                && workbench.LoadC3DSourceCommand.CanExecute(null)
                && !workbench.IsDirty
                && string.IsNullOrWhiteSpace(workbench.RecipePath),
                $"loading={workbench.IsC3DSourceLoading}; open={workbench.LoadC3DSourceCommand.CanExecute(null)}; dirty={workbench.IsDirty}");
            Check(
                "catalog covers intended 3D teaching chain",
                workbench.Tools.Any(tool => tool.Name == "Filter")
                && workbench.Tools.Any(tool => tool.Name == "Height Difference Edge")
                && workbench.Tools.Any(tool => tool.Name == "3D Line Fit")
                && workbench.Tools.Any(tool => tool.Name == "Line Intersection")
                && workbench.Tools.Any(tool => tool.Name == "XYZ Affine Solve")
                && workbench.Tools.Any(tool => tool.Name == "Re-grid Height Map")
                && workbench.Tools.Any(tool => tool.Name == "Thickness")
                && workbench.Tools.Any(tool => tool.Name == "Warpage"),
                string.Join(", ", workbench.Tools.Select(tool => tool.Name)));
            Check(
                "source-less empty recipe can save but cannot execute",
                workbench.CanSaveTeachingRecipe && !workbench.IsTeachingRecipeExecutionReady,
                workbench.ValidationSummary);
            var emptySaved = workbench.TrySaveTeachingRecipe(emptyRecipePath, out var emptySaveMessage);
            Check("source-less empty recipe saves", emptySaved && File.Exists(emptyRecipePath), emptySaveMessage);
            var emptyStored = ToolRecipeDocumentStore.Load(emptyRecipePath);
            Check(
                "source-less empty recipe storage contract preserves zero steps and empty source path",
                emptyStored.Steps.Count == 0
                && string.IsNullOrEmpty(emptyStored.Source.Path)
                && ToolRecipeValidator.ValidateForStorage(emptyStored).IsValid
                && !ToolRecipeValidator.Validate(emptyStored).IsValid,
                $"steps={emptyStored.Steps.Count}; source='{emptyStored.Source.Path}'");
            var emptyReopened = new ToolWorkbenchViewModel();
            var emptyOpened = emptyReopened.TryOpenTeachingRecipe(emptyRecipePath, out var emptyOpenMessage);
            Check(
                "source-less empty recipe reopens as a saved editable draft",
                emptyOpened
                && emptyReopened.PipelineSteps.Count == 0
                && string.IsNullOrEmpty(emptyReopened.Source.Path)
                && emptyReopened.CanSaveTeachingRecipe
                && !emptyReopened.IsTeachingRecipeExecutionReady
                && !emptyReopened.IsDirty,
                emptyOpenMessage);
            var newLifecycle = new ToolWorkbenchViewModel();
            newLifecycle.SetC3DSource(sourcePath, markDirty: false);
            Check("automatic startup source does not create an unsaved-change prompt", !newLifecycle.IsDirty, newLifecycle.RecipeStateSummary);
            var loadedGrid = C3DHeightGrid.Load(sourcePath, maxRenderedPoints: 4);
            var viewerBinding = ToolRecipeSelectionSourceBindingVerifier.FromHeightGrid(loadedGrid);
            var viewerBoundWorkbench = new ToolWorkbenchViewModel();
            viewerBoundWorkbench.SetC3DSourceFromLoadedViewer(sourcePath, viewerBinding, markDirty: false);
            Check(
                "loaded Viewer C3D identity can be reused without changing recipe semantics",
                viewerBoundWorkbench.IsSourceReadyForRecipe
                && !viewerBoundWorkbench.IsDirty
                && string.Equals(
                    viewerBoundWorkbench.Source.Path,
                    Path.GetFullPath(sourcePath),
                    StringComparison.OrdinalIgnoreCase),
                viewerBoundWorkbench.SourceReadinessSummary);
            var invalidViewerBindingRejected = false;
            try
            {
                viewerBoundWorkbench.SetC3DSourceFromLoadedViewer(
                    sourcePath,
                    viewerBinding with { Format = "PLY" },
                    markDirty: false);
            }
            catch (ArgumentException)
            {
                invalidViewerBindingRejected = true;
            }
            Check(
                "loaded Viewer source reuse rejects a non-C3D identity",
                invalidViewerBindingRejected,
                $"rejected={invalidViewerBindingRejected}");
            newLifecycle.RecipeName = "Discarded draft";
            newLifecycle.ToolSearchText = "Filter";
            newLifecycle.CreateNewTeachingRecipe("Created recipe");
            Check(
                "New resets to a named source-less clean zero-step draft",
                newLifecycle.RecipeName == "Created recipe"
                && newLifecycle.PipelineSteps.Count == 0
                && string.IsNullOrWhiteSpace(newLifecycle.Source.Path)
                && !newLifecycle.IsSourceReadyForRecipe
                && !newLifecycle.AddSelectedToolCommand.CanExecute(null)
                && string.IsNullOrEmpty(newLifecycle.ToolSearchText)
                && string.IsNullOrWhiteSpace(newLifecycle.RecipePath)
                && !newLifecycle.IsDirty,
                newLifecycle.RecipeStateSummary);

            var failedOpenWorkbench = new ToolWorkbenchViewModel();
            failedOpenWorkbench.ToolSearchText = "Keep on failure";
            var failedOpenActionLogCount = failedOpenWorkbench.RunLog.Count(item =>
                item.Category is "Preview" or "Publish" or "Run");
            var failedOpen = failedOpenWorkbench.TryOpenTeachingRecipe(
                Path.Combine(fixtureRoot, "missing.ov3d-recipe.json"),
                out _);
            Check(
                "failed recipe open retains the visible search and changes no execution state",
                !failedOpen
                && failedOpenWorkbench.ToolSearchText == "Keep on failure"
                && failedOpenWorkbench.RunLog.Count(item =>
                    item.Category is "Preview" or "Publish" or "Run") == failedOpenActionLogCount,
                $"search='{failedOpenWorkbench.ToolSearchText}'; actionLogs={failedOpenActionLogCount}");

            workbench.RecipeName = "Fixture XYZ Affine Inspection";
            workbench.SetC3DSource(sourcePath);
            var filter = AddTool(workbench, "Filter");
            filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value = "5";
            Check(
                "typed Filter is ready for explicit Preview only",
                filter.State == "Ready"
                && workbench.PreviewSelectedStepCommand.CanExecute(null)
                && !workbench.PublishSelectedStepCommand.CanExecute(null),
                $"state={filter.State}; preview={workbench.PreviewSelectedStepCommand.CanExecute(null)}; publish={workbench.PublishSelectedStepCommand.CanExecute(null)}");
            string? requestedToolLabId = null;
            workbench.ToolLabRequested += (_, args) => requestedToolLabId = args.ToolId;
            Check(
                "selected Filter exposes its routed input/output and focused Tool Lab",
                workbench.SelectedRouteInputIds == workbench.Source.Id
                && workbench.SelectedRouteOutputId == filter.OutputEntityId
                && workbench.IsSelectedToolLabAvailable
                && workbench.OpenSelectedToolLabCommand.CanExecute(null),
                $"input={workbench.SelectedRouteInputIds}; output={workbench.SelectedRouteOutputId}; available={workbench.IsSelectedToolLabAvailable}");
            workbench.OpenSelectedToolLabCommand.Execute(null);
            Check("focused Tool Lab request preserves selected Filter", requestedToolLabId == "filter" && ReferenceEquals(workbench.SelectedPipelineStep, filter), requestedToolLabId ?? "(none)");

            var edge = AddTool(workbench, "Height Difference Edge");
            edge.InputEntityIdsText = filter.OutputEntityId;
            edge.Parameters.Single(parameter => parameter.Name == "ComparisonAxis").Value = "AcrossColumns";

            var firstLine = AddTool(workbench, "3D Line Fit");
            firstLine.InputEntityIdsText = edge.OutputEntityId;
            var secondLine = AddTool(workbench, "3D Line Fit");
            secondLine.InputEntityIdsText = edge.OutputEntityId;

            var corner = AddTool(workbench, "Line Intersection");
            corner.InputEntityIdsText = $"{firstLine.OutputEntityId}; {secondLine.OutputEntityId}";

            workbench.NewReferenceId = "reference.fixture-landmarks";
            workbench.NewReferenceName = "Fixture landmarks";
            workbench.NewReferenceKind = "Reference landmark set";
            workbench.AddReferenceCommand.Execute(null);

            var correspondence = AddTool(workbench, "Landmark Correspondence");
            correspondence.InputEntityIdsText = $"{corner.OutputEntityId}; reference.fixture-landmarks";
            var affine = AddTool(workbench, "XYZ Affine Solve");
            affine.InputEntityIdsText = correspondence.OutputEntityId;
            var regrid = AddTool(workbench, "Re-grid Height Map");
            regrid.InputEntityIdsText = affine.OutputEntityId;
            var binding = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(sourcePath);
            var thicknessReferenceSelection = new OpenVisionLab.ThreeD.Core.ToolRecipeSelection(
                "selection.thickness-reference-roi", "Thickness Reference ROI", OpenVisionLab.ThreeD.Core.ToolRecipeSelectionKinds.GridRectangle,
                workbench.Source.Id, workbench.Source.FrameId, binding,
                new OpenVisionLab.ThreeD.Core.ToolRecipeGridRectangle(0, 0, 2, 2), null, null);
            var measurementSelection = new OpenVisionLab.ThreeD.Core.ToolRecipeSelection(
                "selection.measurement-roi", "Measurement ROI", OpenVisionLab.ThreeD.Core.ToolRecipeSelectionKinds.GridRectangle,
                workbench.Source.Id, workbench.Source.FrameId, binding,
                new OpenVisionLab.ThreeD.Core.ToolRecipeGridRectangle(0, 0, 2, 2), null, null);
            workbench.Selections.Add(thicknessReferenceSelection);
            workbench.Selections.Add(measurementSelection);
            var thickness = AddTool(workbench, "Thickness");
            thickness.ToolName = "Tab 1 Thickness";
            thickness.InputEntityIdsText = $"{workbench.Source.Id}; {thicknessReferenceSelection.Id}; {measurementSelection.Id}";
            thickness.Parameters.Single(parameter => parameter.Name == "MaximumThickness").Value = "120";
            var warpage = AddTool(workbench, "Warpage");
            warpage.InputEntityIdsText = $"{workbench.Source.Id}; {measurementSelection.Id}";
            warpage.Parameters.Single(parameter => parameter.Name == "MaximumPeakToValley").Value = "80";
            var review = AddTool(workbench, "Overlay / Control Review");
            review.InputEntityIdsText = warpage.OutputEntityId;

            workbench.ValidateTeachingRecipeCommand.Execute(null);
            Check(
                "whole Run is blocked while downstream rows have no adapters",
                !workbench.RunTeachingRecipeCommand.CanExecute(null),
                "Only the selected Filter Preview adapter exists in this 11-step fixture.");
            Check(
                "ordered entity routing validates",
                workbench.CanSaveTeachingRecipe && workbench.IsTeachingRecipeExecutionReady && workbench.PipelineSteps.Count == 11,
                workbench.ValidationSummary);
            Check(
                "affine solve keeps its exact numerical contract without implicit execution",
                affine.ToolId == "xyz-affine-solve"
                && affine.Parameters.Single(parameter => parameter.Name == "SolvePolicy").Value == "ExactFourPartialPivot"
                && affine.Parameters.Any(parameter => parameter.Name == "MaximumConditionEstimate")
                && affine.Parameters.Any(parameter => parameter.Name == "ArithmeticResidualWarning"),
                string.Join(" | ", affine.Parameters.Select(parameter => $"{parameter.Name}={parameter.Value}")));

            var saved = workbench.TrySaveTeachingRecipe(recipePath, out var saveMessage);
            Check("save teaching JSON", saved && File.Exists(recipePath), saveMessage);
            Check("save clears modified state", saved && !workbench.IsDirty, workbench.RecipeStateSummary);
            workbench.SetC3DSource(sourcePath);
            Check("reloading the same source path preserves saved state", !workbench.IsDirty, workbench.RecipeStateSummary);

            var stored = ToolRecipeDocumentStore.Load(recipePath);
            Check(
                "saved document preserves source, reference, steps, and parameters",
                stored.Name == "Fixture XYZ Affine Inspection"
                && stored.Source.Path == Path.GetFullPath(sourcePath)
                && stored.References.Single().Id == "reference.fixture-landmarks"
                && stored.Steps.Count == 11
                && stored.Source.ContentSha256?.Length == 64
                && stored.Steps.Single(step => step.ToolId == "filter").Parameters.Single(parameter => parameter.Name == "KernelSize").Value == "5",
                $"steps={stored.Steps.Count}; source={stored.Source.Path}");

            var reopened = new ToolWorkbenchViewModel();
            var opened = reopened.TryOpenTeachingRecipe(recipePath, out var openMessage);
            Check(
                "reopen restores editable teaching graph",
                opened
                && !reopened.IsDirty
                && reopened.CanSaveTeachingRecipe
                && reopened.PipelineSteps.Count == 11
                && reopened.References.Single().Id == "reference.fixture-landmarks"
                && reopened.SelectedPipelineStep?.ToolName == "Filter",
                openMessage);
            Check(
                "recipe session owns reopened identity, lifecycle, and validation state",
                reopened.RecipeSession.SchemaVersion == reopened.RecipeSchemaVersion
                && reopened.RecipeSession.Name == reopened.RecipeName
                && reopened.RecipeSession.Path == reopened.RecipePath
                && reopened.RecipeSession.IsDirty == reopened.IsDirty
                && reopened.RecipeSession.StorageValidation.IsValid == reopened.CanSaveTeachingRecipe
                && reopened.RecipeSession.SourceBindingErrors.Count == 0,
                $"schema={reopened.RecipeSession.SchemaVersion}; path={reopened.RecipeSession.Path}; dirty={reopened.RecipeSession.IsDirty}; storageValid={reopened.RecipeSession.StorageValidation.IsValid}");
            Check(
                "step instance name survives save and reopen",
                reopened.PipelineSteps.Single(step => step.ToolId == "thickness").ToolName == "Tab 1 Thickness",
                reopened.PipelineSteps.Single(step => step.ToolId == "thickness").ToolName);

            var restoredFilter = reopened.PipelineSteps.Single(step => step.ToolId == "filter");
            restoredFilter.InputEntityIdsText = "missing.entity";
            Check("editing reopened recipe marks it modified", reopened.IsDirty, reopened.RecipeStateSummary);
            Check(
                "invalid entity route is blocked",
                !reopened.CanSaveTeachingRecipe
                && reopened.ValidationMessages.Any(message => message.Level == "Error" && message.Message.Contains("missing.entity", StringComparison.OrdinalIgnoreCase)),
                reopened.ValidationSummary);

            restoredFilter.InputEntityIdsText = reopened.Source.Id;
            Check("route correction restores save eligibility", reopened.CanSaveTeachingRecipe, reopened.ValidationSummary);

            var templatePath = Path.Combine(
                Environment.CurrentDirectory,
                "recipes",
                "c3d-xyz-affine-teaching-template.ov3d-teach.json");
            var template = new ToolWorkbenchViewModel();
            var templateOpened = template.TryOpenTeachingRecipe(templatePath, out var templateMessage);
            Check(
                "shipped affine teaching template resolves its relative C3D source",
                templateOpened
                && template.CanSaveTeachingRecipe
                && template.PipelineSteps.Count == 17
                && File.Exists(template.Source.Path),
                templateOpened ? template.Source.Path : templateMessage);

            if (templateOpened)
            {
                var healthCountTotal = template.RecipeHealthReadyCount
                    + template.RecipeHealthNeedsInputCount
                    + template.RecipeHealthNeedsSelectionCount
                    + template.RecipeHealthNeedsParametersCount
                    + template.RecipeHealthStalePreviewCount
                    + template.RecipeHealthPublishedCount;
                Check(
                    "recipe health assigns every long-chain step to exactly one visible state",
                    template.RecipeHealthItems.Count == 17
                    && healthCountTotal == 17
                    && template.RecipeHealthItems.Select(item => item.Step.Id).Distinct(StringComparer.Ordinal).Count() == 17,
                    $"items={template.RecipeHealthItems.Count}; counts={healthCountTotal}; summary={template.RecipeHealthCountsPrimary} | {template.RecipeHealthCountsSecondary}");

                var templateFilter = template.PipelineSteps[0];
                var firstDependent = template.PipelineSteps[1];
                var originalFilterState = templateFilter.State;
                var originalDependentState = firstDependent.State;
                var kernel = templateFilter.Parameters.Single(parameter => parameter.Name == "KernelSize");
                var originalKernel = kernel.Value;
                ToolWorkbenchRecipeHealthCategory CategoryOf(ToolWorkbenchPipelineStepItem step) =>
                    template.RecipeHealthItems.Single(item => ReferenceEquals(item.Step, step)).Category;

                templateFilter.State = "Published";
                var publishedCategory = CategoryOf(templateFilter);
                templateFilter.State = "Preview stale";
                var staleCategory = CategoryOf(templateFilter);
                templateFilter.State = "Ready";
                var readyCategory = CategoryOf(templateFilter);
                firstDependent.State = originalDependentState;
                templateFilter.State = "Published";
                var dependentAfterPublished = CategoryOf(firstDependent);
                templateFilter.State = originalFilterState;
                var dependentWithoutPublishedInput = CategoryOf(firstDependent);
                kernel.Value = "2";
                templateFilter.State = "Ready";
                var parameterCategory = CategoryOf(templateFilter);
                kernel.Value = originalKernel;
                templateFilter.State = originalFilterState;
                firstDependent.State = originalDependentState;

                Check(
                    "recipe health distinguishes Ready, input, selection, parameters, stale Preview, and Published",
                    readyCategory == ToolWorkbenchRecipeHealthCategory.Ready
                    && dependentWithoutPublishedInput == ToolWorkbenchRecipeHealthCategory.NeedsInput
                    && dependentAfterPublished == ToolWorkbenchRecipeHealthCategory.NeedsSelection
                    && parameterCategory == ToolWorkbenchRecipeHealthCategory.NeedsParameters
                    && staleCategory == ToolWorkbenchRecipeHealthCategory.StalePreview
                    && publishedCategory == ToolWorkbenchRecipeHealthCategory.Published,
                    $"ready={readyCategory}; input={dependentWithoutPublishedInput}; selection={dependentAfterPublished}; parameters={parameterCategory}; stale={staleCategory}; published={publishedCategory}");

                var beforeNavigationRecipe = string.Join(
                    "|",
                    template.PipelineSteps.Select(step =>
                        $"{step.Id}:{step.State}:{step.InputEntityIdsText}:{string.Join(',', step.Parameters.Select(parameter => $"{parameter.Name}={parameter.Value}"))}"));
                var beforeNavigationSource = $"{template.Source.Id}|{template.Source.Path}|{template.Source.FrameId}|{template.Source.Unit}";
                var beforeNavigationDirty = template.IsDirty;
                var beforeNavigationActionCount = template.RunLog.Count(item =>
                    item.Category is "Preview" or "Publish" or "Run");
                var revealedRequirements = new List<string>();
                var navigationGuard = 0;
                while (template.NextRecipeHealthIssueCommand.CanExecute(null)
                       && navigationGuard++ < template.PipelineSteps.Count)
                {
                    template.NextRecipeHealthIssueCommand.Execute(null);
                    var revealed = template.SelectedRecipeHealthItem;
                    if (revealed is null
                        || !ReferenceEquals(revealed.Step, template.SelectedPipelineStep)
                        || string.IsNullOrWhiteSpace(revealed.Detail))
                    {
                        break;
                    }

                    revealedRequirements.Add(revealed.Step.Id);
                }

                var lastRequirementStep = template.SelectedPipelineStep;
                template.NextRecipeHealthIssueCommand.Execute(null);
                var nextDidNotWrap = ReferenceEquals(lastRequirementStep, template.SelectedPipelineStep);
                while (template.PreviousRecipeHealthIssueCommand.CanExecute(null)
                       && navigationGuard++ < template.PipelineSteps.Count * 2)
                {
                    template.PreviousRecipeHealthIssueCommand.Execute(null);
                }

                var firstRequirementStep = template.SelectedPipelineStep;
                template.PreviousRecipeHealthIssueCommand.Execute(null);
                var previousDidNotWrap = ReferenceEquals(firstRequirementStep, template.SelectedPipelineStep);
                var afterNavigationRecipe = string.Join(
                    "|",
                    template.PipelineSteps.Select(step =>
                        $"{step.Id}:{step.State}:{step.InputEntityIdsText}:{string.Join(',', step.Parameters.Select(parameter => $"{parameter.Name}={parameter.Value}"))}"));
                var afterNavigationSource = $"{template.Source.Id}|{template.Source.Path}|{template.Source.FrameId}|{template.Source.Unit}";
                var afterNavigationActionCount = template.RunLog.Count(item =>
                    item.Category is "Preview" or "Publish" or "Run");
                Check(
                    "requirement navigation is non-wrapping and reveals the owning step without execution or recipe mutation",
                    revealedRequirements.Count > 0
                    && revealedRequirements.Distinct(StringComparer.Ordinal).Count() == revealedRequirements.Count
                    && nextDidNotWrap
                    && previousDidNotWrap
                    && string.Equals(beforeNavigationRecipe, afterNavigationRecipe, StringComparison.Ordinal)
                    && string.Equals(beforeNavigationSource, afterNavigationSource, StringComparison.Ordinal)
                    && beforeNavigationDirty == template.IsDirty
                    && beforeNavigationActionCount == afterNavigationActionCount,
                    $"revealed={string.Join(',', revealedRequirements)}; nextNoWrap={nextDidNotWrap}; previousNoWrap={previousDidNotWrap}; dirty={beforeNavigationDirty}->{template.IsDirty}; actions={beforeNavigationActionCount}->{afterNavigationActionCount}");
            }
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unexpected exception | {exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(fixtureRoot))
                {
                    Directory.Delete(fixtureRoot, recursive: true);
                }
            }
            catch (IOException exception)
            {
                lines.Add($"FAIL | fixture cleanup | {exception.Message}");
            }
        }

        var reportDirectory = Path.GetDirectoryName(reportPath);
        if (!string.IsNullOrWhiteSpace(reportDirectory))
        {
            Directory.CreateDirectory(reportDirectory);
        }

        var succeeded = passed == total && total > 0 && !lines.Any(line => line.StartsWith("FAIL | unexpected exception", StringComparison.Ordinal));
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        File.WriteAllLines(reportPath, lines);
        summary = $"Tool Recipe teaching verification: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)";
        return succeeded;
    }

    private static ToolWorkbenchPipelineStepItem AddTool(ToolWorkbenchViewModel workbench, string name)
    {
        var tool = workbench.Tools.Single(tool => tool.Name == name);
        if (!workbench.AddSelectedToolCommand.CanExecute(tool))
        {
            throw new InvalidOperationException($"The '{name}' teaching tool cannot be added.");
        }

        workbench.AddSelectedToolCommand.Execute(tool);
        return workbench.SelectedPipelineStep
            ?? throw new InvalidOperationException($"The '{name}' teaching tool was not focused after being added.");
    }
}
