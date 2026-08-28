using System.Globalization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DRegionCompletenessOutputStateVerification
{
    public static int Run(string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var directory = Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);

        var source = CreateFixture();
        var sourcePath = Path.Combine(directory, "region-completeness-state-fixture.c3d");
        source.SaveC3D(sourcePath);
        var baseRecipe = CreateConnectedRecipe(source, Path.GetFileName(sourcePath));
        var remove = ToolRecipeRemoveOutlierPixelsExecution.Execute(baseRecipe, "step.remove", directory);
        var connected = remove.Output is { } filtered && remove.OutlierMask is { } mask
            ? ToolRecipeConnectedRegionExecution.Execute(baseRecipe, "step.connected", filtered, mask)
            : null;

        var checks = new List<(string Name, bool Passed, string Evidence)>();
        checks.Add(Check(
            "upstream-connected-region-fixture",
            remove.Result.Status == ResultStatus.Pass
            && connected?.Result.Status == ResultStatus.Pass
            && connected.Output is { Regions.Count: > 0 },
            $"remove={remove.Result.Status};connected={connected?.Result.Status};regions={connected?.Output?.Regions.Count}"));

        C3DEditableRegionArtifact? editable = null;
        C3DEditableRegionArtifact? restored = null;
        ToolRecipeHeightMeasurementOutput? directCompleteness = null;
        ToolRecipeOrderedGraphExecutionResult? ordered = null;
        ToolRecipeDocument? routeRecipe = null;
        if (connected?.Output is { } connectedArtifact)
        {
            routeRecipe = CreateRouteRecipe(
                source,
                Path.GetFileName(sourcePath),
                remove.Output!,
                connectedArtifact.Regions[0]);
            var routeValidation = ToolRecipeValidator.Validate(routeRecipe);
            checks.Add(Check(
                "typed-region-completeness-route-validates",
                routeValidation.IsValid,
                string.Join(" / ", routeValidation.Errors.Concat(routeValidation.Warnings))));

            var editableEvaluation = ToolRecipeEditableRegionExecution.Execute(
                routeRecipe,
                "step.editable",
                connectedArtifact);
            editable = editableEvaluation.Output;
            checks.Add(Check(
                "editable-region-selects-exact-cells",
                editableEvaluation.Result.Status == ResultStatus.Pass
                && editable is { Cells.Count: > 0 }
                && editable?.Cells.SequenceEqual(
                    connectedArtifact.Regions[0].Cells.Select(cell => new C3DConnectedRegionArtifactCell(cell.Row, cell.Column))) == true
                && editable?.SourceConnectedRegionContentSha256 == connectedArtifact.ContentSha256,
                editable is null
                    ? editableEvaluation.Result.Message
                    : $"index={editable.RegionIndex};cells={editable.Cells.Count};artifact={editable.ArtifactId};sha256={editable.ContentSha256}"));

            var artifactPath = Path.Combine(directory, "region-completeness-state.editable-region.json");
            if (editable is not null)
            {
                C3DEditableRegionArtifactStore.Save(artifactPath, editable);
                restored = C3DEditableRegionArtifactStore.Load(artifactPath);
            }
            checks.Add(Check(
                "editable-region-sidecar-round-trip",
                restored is not null
                && restored.ContentSha256 == editable?.ContentSha256
                && restored.Cells.SequenceEqual(editable?.Cells ?? []),
                $"saved={File.Exists(artifactPath)};restored={restored?.ArtifactId};sha256={restored?.ContentSha256}"));

            var invalidRecipe = routeRecipe with
            {
                Steps = routeRecipe.Steps
                    .Select(step => string.Equals(step.Id, "step.editable", StringComparison.Ordinal)
                        ? step with
                        {
                            Parameters = [new ToolRecipeParameter(
                                ToolRecipeEditableRegionExecution.SelectedRegionIndexParameter,
                                "9999")]
                        }
                        : step)
                    .ToArray()
            };
            var invalid = ToolRecipeEditableRegionExecution.Execute(invalidRecipe, "step.editable", connectedArtifact);
            checks.Add(Check(
                "invalid-region-index-fails-closed",
                invalid.Result.Status == ResultStatus.Error && invalid.Output is null,
                $"status={invalid.Result.Status};message={invalid.Result.Message}"));

            if (restored is not null)
            {
                var direct = ToolRecipeHeightMeasurementExecution.Execute(
                    routeRecipe,
                    "step.completeness",
                    remove.Output,
                    null,
                    restored,
                    directory);
                directCompleteness = direct.Output;
                checks.Add(Check(
                    "completeness-consumes-typed-editable-region",
                    direct.Result.Status is ResultStatus.Pass or ResultStatus.Fail
                    && direct.Output?.CompletenessGrid is
                    {
                        InspectionRegionArtifactId: not null,
                        InspectionRegionArtifactContentSha256: not null
                    } completeness
                    && completeness.InspectionRegionArtifactId == restored.ArtifactId
                    && completeness.InspectionRegionArtifactContentSha256 == restored.ContentSha256,
                    direct.Output is null
                        ? direct.Result.Message
                        : $"status={direct.Result.Status};selection={direct.Output.SelectionId};artifact={direct.Output.CompletenessGrid?.InspectionRegionArtifactId};hash={direct.Output.CompletenessGrid?.InspectionRegionArtifactContentSha256}"));
                var directCell = direct.Output?.CompletenessGrid?.Cells.SingleOrDefault();
                checks.Add(Check(
                    "completeness-uses-exact-mask-membership",
                    directCell is not null
                    && directCell.TotalCellCount == editable?.Cells.Count
                    && directCell.FiniteCellCount == 0
                    && directCell.MissingCellCount == editable?.Cells.Count
                    && directCell.FiniteCoverageRatio == 0d,
                    directCell is null
                        ? "no completeness cell"
                        : $"selected={editable?.Cells.Count};total={directCell.TotalCellCount};finite={directCell.FiniteCellCount};missing={directCell.MissingCellCount};coverage={directCell.FiniteCoverageRatio:R}"));
            }

            ordered = ToolRecipeOrderedGraphExecution.Execute(routeRecipe, sourcePath);
            var orderedCompleteness = ordered.Steps.LastOrDefault()?.CompletenessGrid;
            checks.Add(Check(
                "ordered-graph-preserves-editable-and-completeness-output",
                ordered.Status is ResultStatus.Pass or ResultStatus.Fail
                && ordered.Steps.Count == 4
                && ordered.Steps[2].Result.Status == ResultStatus.Pass
                && ordered.Steps[2].OutputContentSha256 == restored?.ContentSha256
                && orderedCompleteness?.InspectionRegionArtifactId == restored?.ArtifactId
                && orderedCompleteness?.InspectionRegionArtifactContentSha256 == restored?.ContentSha256
                && orderedCompleteness?.ContentSha256 == directCompleteness?.CompletenessGrid?.ContentSha256,
                $"overall={ordered.Status};steps={ordered.Steps.Count};editable={ordered.Steps.ElementAtOrDefault(2)?.OutputContentSha256};completeness={orderedCompleteness?.ContentSha256}"));

            var disabledRecipe = routeRecipe with
            {
                Steps = routeRecipe.Steps
                    .Select(step => string.Equals(step.Id, "step.editable", StringComparison.Ordinal)
                        ? step with { OutputEnabled = false }
                        : step)
                    .ToArray()
            };
            var disabledPath = Path.Combine(directory, "region-completeness-state.disabled-recipe.json");
            ToolRecipeDocumentStore.Save(disabledPath, disabledRecipe);
            var reopenedDisabled = ToolRecipeDocumentStore.Load(disabledPath);
            var disabledRun = ToolRecipeOrderedGraphExecution.Execute(reopenedDisabled, sourcePath);
            var disabledStep = disabledRun.Steps.ElementAtOrDefault(2);
            var disabledDownstreamStep = disabledRun.Steps.ElementAtOrDefault(3);
            checks.Add(Check(
                "disabled-output-is-declared-but-not-created",
                reopenedDisabled.Steps.Single(step => step.Id == "step.editable").OutputEnabled == false
                && disabledStep?.Result.Status == ResultStatus.Warning
                && disabledStep.OutputContentSha256 is null
                && disabledRun.Status == ResultStatus.Error
                && disabledRun.Steps.Count == 3
                && disabledDownstreamStep is null,
                $"persisted={reopenedDisabled.Steps.Single(step => step.Id == "step.editable").OutputEnabled};disabledStep={disabledStep?.Result.Status};overall={disabledRun.Status};message={disabledRun.Message}"));
        }

        var states = InspectionStepStateMatrix.All;
        var expectedKeys = new[] { "empty", "incomplete", "stale", "ready", "running", "pass", "fail", "error" };
        checks.Add(Check(
            "common-state-matrix-is-complete",
            states.Count == 8 && states.Select(state => state.Key).SequenceEqual(expectedKeys)
            && InspectionStepStateMatrix.Classify("Preview running") == InspectionStepState.Running
            && InspectionStepStateMatrix.Classify("Preview stale") == InspectionStepState.Stale
            && InspectionStepStateMatrix.Classify("Published") == InspectionStepState.Ready
            && InspectionStepStateMatrix.Classify("ignored", ResultStatus.Pass) == InspectionStepState.Pass,
            string.Join(",", states.Select(state => state.Key))));

        var passed = checks.Count(item => item.Passed);
        var lines = new List<string>
        {
            $"C3DRegionCompletenessOutputStateVerification|{(passed == checks.Count ? "PASS" : "FAIL")}|cases={checks.Count}|passed={passed}|failed={checks.Count - passed}",
            $"Fixture|path={sourcePath}|width={source.Width}|height={source.Height}|sourceSha256={source.ContentSha256}",
            $"Editable|artifact={editable?.ArtifactId}|region={editable?.RegionIndex}|cells={editable?.Cells.Count}|sha256={editable?.ContentSha256}",
            $"Completeness|direct={directCompleteness?.CompletenessGrid?.ContentSha256}|ordered={ordered?.Steps.LastOrDefault()?.CompletenessGrid?.ContentSha256}",
            $"OutputPolicy|schema={routeRecipe?.SchemaVersion}|disabledDownstreamStatus={ordered?.Status}"
        };
        lines.AddRange(checks.Select(item => $"{item.Name}|{(item.Passed ? "PASS" : "FAIL")}|{item.Evidence}"));
        File.WriteAllLines(fullReportPath, lines);
        Console.WriteLine($"Region / Completeness / output-state verification: {(passed == checks.Count ? "PASS" : "FAIL")} ({passed}/{checks.Count})");
        return passed == checks.Count ? 0 : 1;
    }

    private static C3DHeightFieldSnapshot CreateFixture()
    {
        const int width = 6;
        const int height = 6;
        var values = Enumerable.Repeat(10d, width * height).ToArray();
        values[2 * width + 2] = 100d;
        values[2 * width + 3] = 100d;
        values[3 * width + 2] = 100d;
        return C3DHeightFieldSnapshot.CreateForVerification("source.region-completeness", width, height, values);
    }

    private static ToolRecipeDocument CreateConnectedRecipe(
        C3DHeightFieldSnapshot source,
        string sourcePath) =>
        new(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Region Completeness State Fixture",
            new ToolRecipeSource(
                source.EntityId,
                "Region Completeness State Fixture",
                "C3D",
                source.Unit,
                source.FrameId,
                sourcePath,
                source.ByteLength,
                source.ContentSha256,
                source.Width,
                source.Height),
            [],
            [
                new ToolRecipeStep(
                    "step.remove",
                    "remove-outlier-pixels",
                    "Remove Outlier Pixels",
                    1,
                    [source.EntityId],
                    "filtered.height-field",
                    [
                        new("Rule", "LocalMedianAbsoluteDeviation"),
                        new("WindowSize", "3"),
                        new("MaximumAbsoluteDeviation", "20"),
                        new("MinimumValidNeighbors", "3"),
                        new("MissingValuePolicy", "PreserveMask"),
                        new("BoundaryPolicy", "AvailableNeighbors"),
                        new("OutlierPolicy", "SetMissing")
                    ]),
                new ToolRecipeStep(
                    "step.connected",
                    "connected-region",
                    "Connected Region",
                    1,
                    ["filtered.height-field"],
                    "connected.regions",
                    [
                        new("Connectivity", "Four"),
                        new("OriginX", "0"),
                        new("OriginY", "0"),
                        new("ColumnPitch", "1"),
                        new("RowPitch", "1"),
                        new("AreaUnit", "grid-unit^2")
                    ])
            ],
            []);

    private static ToolRecipeDocument CreateRouteRecipe(
        C3DHeightFieldSnapshot source,
        string sourcePath,
        C3DHeightFieldSnapshot filtered,
        C3DConnectedRegionArtifactRegion region)
    {
        var baseRecipe = CreateConnectedRecipe(source, sourcePath);
        var width = region.MaximumColumn - region.MinimumColumn + 1;
        var height = region.MaximumRow - region.MinimumRow + 1;
        var profile = new C3DCompletenessGridProfile(
            1,
            1,
            width,
            height,
            width,
            height,
            C3DCompletenessCellShape.GridRectangle);
        var parameters = profile.ToRecipeParameters()
            .Concat(new C3DCompletenessPresencePolicy(1d, -1000d, 1000d).ToRecipeParameters())
            .ToArray();
        var binding = new ToolRecipeSelectionSourceBinding(
            "HeightField",
            filtered.ContentSha256,
            filtered.Width,
            filtered.Height,
            filtered.EntityId,
            filtered.RootSourceSha256,
            filtered.Unit,
            filtered.FrameId);
        return baseRecipe with
        {
            Steps = baseRecipe.Steps.Concat([
                new ToolRecipeStep(
                    "step.editable",
                    "editable-region",
                    "Editable Region",
                    1,
                    ["connected.regions"],
                    "editable.region",
                    [new(ToolRecipeEditableRegionExecution.SelectedRegionIndexParameter, region.Index.ToString(CultureInfo.InvariantCulture))]),
                new ToolRecipeStep(
                    "step.completeness",
                    "completeness-grid",
                    "Completeness Grid",
                    3,
                    [filtered.EntityId, "selection.reference", "editable.region"],
                    "completeness.metrics",
                    parameters)
            ]).ToArray(),
            Selections = [
                new ToolRecipeSelection(
                    "selection.reference",
                    "Reference ROI",
                    ToolRecipeSelectionKinds.GridRectangle,
                    source.EntityId,
                    source.FrameId,
                    binding,
                    new ToolRecipeGridRectangle(0, 0, 1, 1),
                    null,
                    null)
            ]
        };
    }

    private static (string Name, bool Passed, string Evidence) Check(
        string name,
        bool passed,
        string evidence) =>
        (name, passed, evidence);
}
