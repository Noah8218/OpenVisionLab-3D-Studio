using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class CompletenessReviewOwnerVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D Workbench Completeness Review owner verification"
        };
        var passed = 0;
        var total = 0;
        var fullReportPath = Path.GetFullPath(reportPath);

        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition)
            {
                passed++;
            }

            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }

        try
        {
            var region = new ToolRecipeGridRectangle(1, 1, 1, 1);
            var profile = new C3DCompletenessGridProfile(
                2,
                2,
                1,
                1,
                1,
                1,
                C3DCompletenessCellShape.GridRectangle);
            var cells = new[]
            {
                new C3DCompletenessCellMetric(
                    "r001.c001",
                    1,
                    1,
                    region,
                    4,
                    4,
                    0,
                    1d,
                    10d,
                    10d,
                    0.1d,
                    ResultStatus.Pass,
                    "pass"),
                new C3DCompletenessCellMetric(
                    "r001.c002",
                    1,
                    2,
                    region with { Column = 2 },
                    4,
                    3,
                    1,
                    0.75d,
                    9.5d,
                    10d,
                    0.05d,
                    ResultStatus.Pass,
                    "pass"),
                new C3DCompletenessCellMetric(
                    "r002.c001",
                    2,
                    1,
                    region with { Row = 2 },
                    4,
                    2,
                    2,
                    0.5d,
                    8d,
                    10d,
                    -0.2d,
                    ResultStatus.Fail,
                    "coverage"),
                new C3DCompletenessCellMetric(
                    "r002.c002",
                    2,
                    2,
                    region with { Row = 2, Column = 2 },
                    4,
                    1,
                    3,
                    0.25d,
                    7d,
                    10d,
                    -0.3d,
                    ResultStatus.Fail,
                    "coverage")
            };
            var overlays = cells
                .Select(cell => new C3DCompletenessCellOverlay(
                    $"overlay.{cell.CellId}",
                    cell.CellId,
                    cell.Region,
                    cell.Decision ?? ResultStatus.Warning))
                .ToArray();
            var output = new C3DCompletenessGridMetricOutput(
                "output.completeness",
                "source.c3d.height-map",
                "input.c3d.height-map",
                new string('a', 64),
                "µm",
                "frame.c3d-grid-index",
                "selection.reference",
                region,
                4,
                10d,
                "selection.inspection-grid",
                region,
                profile,
                cells,
                new string('b', 64),
                PassedCellCount: 2,
                FailedCellCount: 2,
                AggregateStatus: ResultStatus.Fail,
                CellOverlays: overlays,
                InspectionRegionArtifactId: "artifact.inspection-grid",
                InspectionRegionArtifactContentSha256: new string('c', 64));
            var tabs = Enumerable.Range(1, 8)
                .Select(number => new ToolWorkbenchCompletenessTabSnapshot(
                    $"step.tab-{number}",
                    "thickness",
                    $"Tab {number} Thickness",
                    $"output.tab-{number}"))
                .Append(new ToolWorkbenchCompletenessTabSnapshot(
                    "step.ignored-tool",
                    "filter",
                    "Tab 9 Thickness",
                    "output.ignored-tool"))
                .Append(new ToolWorkbenchCompletenessTabSnapshot(
                    "step.duplicate-tab-one",
                    "thickness",
                    "TAB 1 THICKNESS",
                    "output.duplicate-tab-one"))
                .ToArray();
            var snapshot = new ToolWorkbenchCompletenessReviewSnapshot(
                true,
                true,
                output,
                tabs);
            var callbackSelectedCellId = "initial";
            var callbackCount = 0;
            var owner = new ToolWorkbenchCompletenessReviewOwner(
                cellId =>
                {
                    callbackSelectedCellId = cellId ?? string.Empty;
                    callbackCount++;
                },
                (_, english) => english);

            owner.Rebuild(snapshot);
            Check(
                "owner projects output cells in source row-major order and selects the first failure",
                owner.CompletenessCellResults.Select(item => item.CellId)
                    .SequenceEqual(cells.Select(cell => cell.CellId))
                && owner.SelectedCompletenessCellId == "r002.c001"
                && owner.CompletenessCellResults.Count(item => item.IsSelected) == 1
                && owner.CompletenessCellResults.Single(item => item.IsSelected).CellId
                    == "r002.c001",
                $"ids={string.Join(",", owner.CompletenessCellResults.Select(item => item.CellId))}; selected={owner.SelectedCompletenessCellId}");
            Check(
                "owner preserves cell metadata, quality values, and mapped Tab identity",
                owner.CompletenessCellResults[2] is
                {
                    DisplayName: "Tab 3 Thickness",
                    MappedThicknessStepId: "step.tab-3",
                    MappedThicknessOutputEntityId: "output.tab-3",
                    Status: ResultStatus.Fail,
                    FiniteCoverageRatio: 0.5d,
                    ReferenceRelativeMeanRawHeight: -0.2d,
                    Region.Row: 2,
                    Region.Column: 1,
                    HasMappedThicknessIdentity: true
                }
                && owner.CompletenessCellResults[2].EvidenceSummary
                    == "coverage 50.0 % | relative mean -0.2",
                owner.CompletenessCellResults[2].EvidenceSummary);
            Check(
                "Tab 1..8 mapping ignores other tools and duplicate numbers without mutating input",
                ToolWorkbenchCompletenessReviewOwner.CreateTabThicknessIdentityMap(tabs)
                    is { Count: 8 }
                && ToolWorkbenchCompletenessReviewOwner.CreateTabThicknessIdentityMap(tabs)[1]
                    is { StepId: "step.tab-1", OutputEntityId: "output.tab-1" }
                && tabs[0].Id == "step.tab-1"
                && tabs[^1].Id == "step.duplicate-tab-one",
                "map=8; duplicate=first-wins; snapshot=tabs-unchanged");

            owner.NextCompletenessFailureCommand.Execute(null);
            var secondFailure = owner.SelectedCompletenessCellId;
            owner.NextCompletenessFailureCommand.Execute(null);
            var wrappedFailure = owner.SelectedCompletenessCellId;
            owner.PreviousCompletenessFailureCommand.Execute(null);
            Check(
                "Previous and Next failed-cell commands wrap deterministically and use the presentation callback",
                secondFailure == "r002.c002"
                && wrappedFailure == "r002.c001"
                && owner.SelectedCompletenessCellId == "r002.c002"
                && callbackSelectedCellId == "r002.c002"
                && owner.CompletenessFailureNavigationSummary == "Failure 2/2",
                $"second={secondFailure}; wrapped={wrappedFailure}; previous={owner.SelectedCompletenessCellId}; callbacks={callbackCount}");

            var uppercaseSelection = owner.CompletenessCellResults
                .Single(item => item.CellId == "r002.c002") with
            {
                CellId = "R002.C002"
            };
            owner.SelectCompletenessCellCommand.Execute(uppercaseSelection);
            Check(
                "direct selection uses case-insensitive identity and updates only review presentation state",
                string.Equals(
                    owner.SelectedCompletenessCellId,
                    "R002.C002",
                    StringComparison.OrdinalIgnoreCase)
                && owner.CompletenessCellResults.Single(item => item.IsSelected).CellId
                    == "r002.c002"
                && owner.CompletenessFailureNavigationSummary == "Failure 2/2"
                && callbackSelectedCellId == "R002.C002",
                $"selected={owner.SelectedCompletenessCellId}; callback={callbackSelectedCellId}");

            owner.ClearSelection();
            var allPassOutput = output with
            {
                Cells = cells
                    .Select(cell => cell with { Decision = ResultStatus.Pass })
                    .ToArray(),
                PassedCellCount = cells.Length,
                FailedCellCount = 0,
                AggregateStatus = ResultStatus.Pass
            };
            owner.Rebuild(snapshot with { CompletenessGrid = allPassOutput });
            Check(
                "all-pass review remains visible, selects the first cell, and disables failed navigation",
                owner.HasCompletenessCellResults
                && owner.CompletenessCellResults.Count == cells.Length
                && owner.SelectedCompletenessCellId == "r001.c001"
                && !owner.CanNavigateCompletenessFailures
                && !owner.PreviousCompletenessFailureCommand.CanExecute(null)
                && !owner.NextCompletenessFailureCommand.CanExecute(null)
                && owner.CompletenessFailureNavigationSummary == "No failed cells",
                $"count={owner.CompletenessCellResults.Count}; selected={owner.SelectedCompletenessCellId}; summary={owner.CompletenessFailureNavigationSummary}");

            owner.Rebuild(snapshot with
            {
                IsSelectedStepCompletenessGrid = false,
                CompletenessGrid = output
            });
            Check(
                "non-Completeness snapshots clear review results and presentation selection without execution",
                !owner.HasCompletenessCellResults
                && owner.CompletenessCellResults.Count == 0
                && owner.SelectedCompletenessCellId is null
                && !owner.NextCompletenessFailureCommand.CanExecute(null)
                && callbackSelectedCellId == string.Empty,
                $"count={owner.CompletenessCellResults.Count}; selected={owner.SelectedCompletenessCellId ?? "null"}; callbacks={callbackCount}; preview/publish/run/validation=not invoked");
            Check(
                "rebuild does not mutate the supplied output, cells, or snapshot identity",
                ReferenceEquals(snapshot.CompletenessGrid, output)
                && ReferenceEquals(output.Cells, cells)
                && output.Cells[2].Decision == ResultStatus.Fail
                && snapshot.ThicknessTabs.Count == 10
                && callbackCount > 0,
                "output=unchanged; cells=unchanged; snapshot=unchanged; execution=not invoked");
        }
        catch (Exception exception)
        {
            total++;
            lines.Add($"FAIL | unexpected exception | {exception}");
        }

        var success = total > 0 && passed == total;
        summary = $"CompletenessReviewOwner|pass={success}|checks={passed}/{total}|report={fullReportPath}";
        lines.Insert(1, summary);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        return success;
    }
}
