using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls.WpfPropertyGrid;
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
            var filter = AddTool(workbench, "filter");
            filter.Parameters.Remove(filter.Parameters.Single(parameter => parameter.Name == "BoundaryPolicy"));
            filter.Parameters.Add(new ToolWorkbenchParameterItem("FuturePolicy", "RetainMe"));
            workbench.DiscardSelectedStepParameterDraft();
            var filterDraft = workbench.SelectedStepPropertyDraft as FilterStepProperties;
            Check(
                "Filter maps to a detached typed draft",
                filterDraft is not null && filterDraft.KernelSize == 3 && filterDraft.UnmappedParameters.Contains("FuturePolicy=RetainMe", StringComparison.Ordinal),
                $"draft={workbench.SelectedStepPropertyDraft?.GetType().Name}; kernel={filterDraft?.KernelSize}; unmapped={filterDraft?.UnmappedParameters}");

            filterDraft!.KernelSize = 5;
            workbench.MarkSelectedStepParameterDraftDirty();
            Check(
                "WPG draft does not mutate recipe or auto-run",
                filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value == "3"
                && !workbench.HasCurrentFilterPreview
                && !workbench.PreviewSelectedStepCommand.CanExecute(null),
                $"storedKernel={filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value}; preview={workbench.HasCurrentFilterPreview}");

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

            var unsupported = AddTool(workbench, "three-d-line-fit");
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
            var reopenedUnsupported = reopened.PipelineSteps.Single(step => step.ToolId == "three-d-line-fit");
            Check(
                "save and reopen preserve typed and unknown parameters",
                opened
                && reopenedFilter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value == "5"
                && reopenedFilter.Parameters.Single(parameter => parameter.Name == "FuturePolicy").Value == "RetainMe"
                && reopenedEdge.Parameters.Single(parameter => parameter.Name == "MinimumDelta").Value == "2.5"
                && reopenedEdge.Parameters.Single(parameter => parameter.Name == "FutureTiePolicy").Value == "Stable"
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
        }
        catch (Exception exception)
        {
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
        workbench.SelectedTool = workbench.Tools.Single(tool => tool.Id == toolId);
        workbench.AddSelectedToolCommand.Execute(null);
        return workbench.SelectedPipelineStep
            ?? throw new InvalidOperationException($"Tool '{toolId}' was not added.");
    }

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
