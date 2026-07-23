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
            newLifecycle.RecipeName = "Discarded draft";
            newLifecycle.CreateNewTeachingRecipe("Created recipe");
            Check(
                "New resets to a named source-less clean zero-step draft",
                newLifecycle.RecipeName == "Created recipe"
                && newLifecycle.PipelineSteps.Count == 0
                && string.IsNullOrWhiteSpace(newLifecycle.Source.Path)
                && !newLifecycle.IsSourceReadyForRecipe
                && !newLifecycle.AddSelectedToolCommand.CanExecute(null)
                && string.IsNullOrWhiteSpace(newLifecycle.RecipePath)
                && !newLifecycle.IsDirty,
                newLifecycle.RecipeStateSummary);

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
            var measurementSelection = new OpenVisionLab.ThreeD.Core.ToolRecipeSelection(
                "selection.measurement-roi", "Measurement ROI", OpenVisionLab.ThreeD.Core.ToolRecipeSelectionKinds.GridRectangle,
                workbench.Source.Id, workbench.Source.FrameId, binding,
                new OpenVisionLab.ThreeD.Core.ToolRecipeGridRectangle(0, 0, 2, 2), null, null);
            workbench.Selections.Add(measurementSelection);
            var thickness = AddTool(workbench, "Thickness");
            thickness.InputEntityIdsText = $"{workbench.Source.Id}; {measurementSelection.Id}";
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
        workbench.SelectedTool = workbench.Tools.Single(tool => tool.Name == name);
        if (!workbench.AddSelectedToolCommand.CanExecute(null))
        {
            throw new InvalidOperationException($"The '{name}' teaching tool cannot be added.");
        }

        workbench.AddSelectedToolCommand.Execute(null);
        return workbench.SelectedPipelineStep
            ?? throw new InvalidOperationException($"The '{name}' teaching tool was not selected after being added.");
    }
}
