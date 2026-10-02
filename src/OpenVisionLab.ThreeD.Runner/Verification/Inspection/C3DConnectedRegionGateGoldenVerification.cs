using System.Globalization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DConnectedRegionGateGoldenVerification
{
    public static int Run(string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var directory = Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);

        var source = CreateFixture();
        var sourcePath = Path.Combine(directory, "connected-region-gate-fixture.c3d");
        var recipePath = Path.Combine(directory, "connected-region-gate-fixture.ov3d-recipe.json");
        source.SaveC3D(sourcePath);
        var recipe = CreateRecipe(source, Path.GetFileName(sourcePath));
        ToolRecipeDocumentStore.Save(recipePath, recipe);
        var reopenedRecipe = ToolRecipeDocumentStore.Load(recipePath);

        var remove = ToolRecipeRemoveOutlierPixelsExecution.Execute(reopenedRecipe, "step.remove", directory);
        var connected = remove.Output is { } filtered && remove.OutlierMask is { } mask
            ? ToolRecipeConnectedRegionExecution.Execute(reopenedRecipe, "step.connected", filtered, mask)
            : null;
        var checks = new List<(string Name, bool Passed, string Evidence)>
        {
            Check(
                "upstream-detection-and-region-fixture",
                remove.Result.Status == ResultStatus.Pass
                && connected?.Result.Status == ResultStatus.Pass
                && connected.Output is { Regions.Count: > 0 },
                $"remove={remove.Result.Status};connected={connected?.Result.Status};regions={connected?.Output?.Regions.Count}")
        };

        C3DConnectedRegionGateOutput? directOutput = null;
        if (remove.Output is { } detection
            && connected?.Output is { } regionArtifact
            && regionArtifact.Regions.Count > 0)
        {
            var direct = ToolRecipeConnectedRegionGateExecution.Execute(
                reopenedRecipe,
                "step.gate",
                source,
                detection,
                regionArtifact,
                directory);
            directOutput = direct.Output?.ConnectedRegionGate;
            checks.Add(Check(
                "direct-presence-gate-passes-and-retains-provenance",
                direct.Result.Status == ResultStatus.Pass
                && directOutput?.Decision == C3DConnectedRegionGateDecision.Accepted
                && directOutput.RawSourceEntityId == source.EntityId
                && directOutput.DetectionEntityId == detection.EntityId
                && directOutput.RegionArtifactId == regionArtifact.ArtifactId
                && directOutput.ContentSha256 == C3DConnectedRegionGateOutput.CalculateContentSha256(directOutput),
                direct.Output is null
                    ? direct.Result.Message
                    : $"status={direct.Result.Status};decision={directOutput?.Decision};coverage={directOutput?.FiniteCoverageRatio:R};mean={directOutput?.MeanHeight:R};sha256={directOutput?.ContentSha256}"));

            var unconfiguredRecipe = reopenedRecipe with { Steps = reopenedRecipe.Steps.Select(step => step.Id == "step.gate" ? step with { AcceptanceLimitsConfigured = false } : step).ToArray() };
            var rejectedInspection = ToolRecipeOrderedGraphExecution.Execute(unconfiguredRecipe, sourcePath);
            checks.Add(Check("unconfigured-limits-reject-inspection-before-any-step", !ToolRecipeOrderedGraphExecution.CanExecute(unconfiguredRecipe, out _)
                && rejectedInspection.Status == ResultStatus.Error && rejectedInspection.Steps.Count == 0, rejectedInspection.Message));
            var configuredRecipe = unconfiguredRecipe with { Steps = unconfiguredRecipe.Steps.Select(step => step.Id == "step.gate" ? step with { AcceptanceLimitsConfigured = true } : step).ToArray() };
            var configuredInspection = ToolRecipeOrderedGraphExecution.Execute(configuredRecipe, sourcePath);
            var configuredGate = configuredInspection.Steps.SingleOrDefault(step => step.StepId == "step.gate");
            checks.Add(Check("configured-gate-replays-with-native-artifact-identity", configuredInspection.Status == ResultStatus.Pass
                && configuredGate?.Result.Status == ResultStatus.Pass && configuredGate.ConnectedRegionGate is { } configuredArtifact
                && configuredGate.OutputContentSha256 == configuredArtifact.ContentSha256, configuredInspection.Message));
            var unjudged = ToolRecipeConnectedRegionGateExecution.Execute(unconfiguredRecipe, "step.gate", source, detection, regionArtifact, directory);
            checks.Add(Check("unconfigured-gate-keeps-values-and-distinct-policy-identity", unjudged.Result.Status == ResultStatus.NotRun
                && unjudged.Output is { } measured && measured.ContentSha256 != direct.Output!.ContentSha256
                && measured.ConnectedRegionGate!.MeanHeight == directOutput!.MeanHeight
                && measured.ConnectedRegionGate.FiniteCoverageRatio == directOutput.FiniteCoverageRatio
                && measured.EvidenceSummary == ToolRecipeStep.UnconfiguredAcceptanceMessage,
                unjudged.Result.Message));

            var fillRecipe = WithGatePolicy(reopenedRecipe, new C3DConnectedRegionGatePolicy(
                C3DConnectedRegionGateMode.FillHeight,
                regionArtifact.Regions[0].Index,
                1d,
                50d,
                150d,
                0d,
                0d,
                0d,
                source.Unit));
            var fill = ToolRecipeConnectedRegionGateExecution.Execute(
                fillRecipe,
                "step.gate",
                source,
                detection,
                regionArtifact,
                directory);
            checks.Add(Check(
                "direct-fill-height-gate-passes",
                fill.Result.Status == ResultStatus.Pass
                && fill.Output?.ConnectedRegionGate?.Decision == C3DConnectedRegionGateDecision.Accepted
                && fill.Output.ConnectedRegionGate.Policy.Mode == C3DConnectedRegionGateMode.FillHeight
                && fill.Output.ConnectedRegionGate.MinimumHeight is not null
                && fill.Output.ConnectedRegionGate.MaximumHeight is not null,
                fill.Output is null
                    ? fill.Result.Message
                    : $"status={fill.Result.Status};decision={fill.Output.ConnectedRegionGate?.Decision};mean={fill.Output.ConnectedRegionGate?.MeanHeight:R};min={fill.Output.ConnectedRegionGate?.MinimumHeight:R};max={fill.Output.ConnectedRegionGate?.MaximumHeight:R}"));

            var rejectedRecipe = WithGatePolicy(reopenedRecipe, new C3DConnectedRegionGatePolicy(
                C3DConnectedRegionGateMode.Presence,
                regionArtifact.Regions[0].Index,
                1d,
                1000d,
                2000d,
                0d,
                0d,
                0d,
                source.Unit));
            var rejected = ToolRecipeConnectedRegionGateExecution.Execute(
                rejectedRecipe,
                "step.gate",
                source,
                detection,
                regionArtifact,
                directory);
            checks.Add(Check(
                "height-gate-reject-is-not-object-absence",
                rejected.Result.Status == ResultStatus.Fail
                && rejected.Output?.ConnectedRegionGate?.Decision == C3DConnectedRegionGateDecision.Rejected
                && rejected.Output.ConnectedRegionGate.ReasonCode == "CoverageOrMeanHeightRejected"
                && rejected.Output.ConnectedRegionGate.Message.Contains("not an expected-object absence", StringComparison.Ordinal),
                rejected.Output is null
                    ? rejected.Result.Message
                    : $"status={rejected.Result.Status};decision={rejected.Output.ConnectedRegionGate?.Decision};reason={rejected.Output.ConnectedRegionGate?.ReasonCode}"));

            var missingRecipe = WithGatePolicy(reopenedRecipe, new C3DConnectedRegionGatePolicy(
                C3DConnectedRegionGateMode.Presence,
                999,
                0d,
                -1000d,
                1000d,
                0d,
                0d,
                0d,
                source.Unit));
            var missing = ToolRecipeConnectedRegionGateExecution.Execute(
                missingRecipe,
                "step.gate",
                source,
                detection,
                regionArtifact,
                directory);
            checks.Add(Check(
                "missing-selected-region-is-explicit-warning",
                missing.Result.Status == ResultStatus.Warning
                && missing.Output?.ConnectedRegionGate?.Decision == C3DConnectedRegionGateDecision.Missing
                && missing.Output.ConnectedRegionGate.ReasonCode == "SelectedRegionNotDetected"
                && missing.Output.ConnectedRegionGate.Message.Contains("Expected-object absence is not inferred", StringComparison.Ordinal),
                missing.Output is null
                    ? missing.Result.Message
                    : $"status={missing.Result.Status};decision={missing.Output.ConnectedRegionGate?.Decision};reason={missing.Output.ConnectedRegionGate?.ReasonCode}"));

            var staleDetection = C3DHeightFieldSnapshot.CreateForVerification(
                "stale.detection",
                source.Width,
                source.Height,
                source.Values.ToArray());
            var stale = ToolRecipeConnectedRegionGateExecution.Execute(
                reopenedRecipe,
                "step.gate",
                source,
                staleDetection,
                regionArtifact,
                directory);
            checks.Add(Check(
                "stale-provenance-fails-closed",
                stale.Result.Status == ResultStatus.Error
                && stale.Output is null,
                $"status={stale.Result.Status};message={stale.Result.Message}"));
        }

        var ordered = ToolRecipeOrderedGraphExecution.Execute(reopenedRecipe, sourcePath);
        var orderedGate = ordered.Steps.SingleOrDefault(step => step.StepId == "step.gate");
        var runRecordSteps = ordered.Status is ResultStatus.Pass or ResultStatus.Fail
            ? ToolRecipeOrderedGraphRunRecordProjection.Create(reopenedRecipe, ordered)
            : [];
        var runGate = runRecordSteps.SingleOrDefault(step => step.Id == "step.gate");
        checks.Add(Check(
            "ordered-run-and-run-record-preserve-gate-output",
            ordered.Status == ResultStatus.Pass
            && orderedGate?.Result.Status == ResultStatus.Pass
            && orderedGate.ConnectedRegionGate?.Decision == C3DConnectedRegionGateDecision.Accepted
            && orderedGate.OutputContentSha256 == orderedGate.ConnectedRegionGate.ContentSha256
            && runGate?.ConnectedRegionGate?.ContentSha256 == orderedGate.ConnectedRegionGate.ContentSha256
            && runGate.ConnectedRegionGate?.RawSourceEntityId == reopenedRecipe.Source.Id,
            $"run={ordered.Status};gate={orderedGate?.Result.Status};output={orderedGate?.OutputContentSha256};record={runGate?.ConnectedRegionGate?.ContentSha256}"));

        var passed = checks.Count(item => item.Passed);
        var lines = new List<string>
        {
            $"C3DConnectedRegionGateGoldenVerification|{(passed == checks.Count ? "PASS" : "FAIL")}|cases={checks.Count}|passed={passed}|failed={checks.Count - passed}",
            $"Fixture|path={sourcePath}|width={source.Width}|height={source.Height}|sourceSha256={source.ContentSha256}",
            $"Direct|status={directOutput?.Decision}|coverage={directOutput?.FiniteCoverageRatio:R}|hash={directOutput?.ContentSha256}",
            $"Ordered|status={ordered.Status}|gate={orderedGate?.Result.Status}|hash={orderedGate?.ConnectedRegionGate?.ContentSha256}",
        };
        lines.AddRange(checks.Select(item => $"{item.Name}|{(item.Passed ? "PASS" : "FAIL")}|{item.Evidence}"));
        File.WriteAllLines(fullReportPath, lines);
        Console.WriteLine($"Connected-region coverage/height gate verification: {(passed == checks.Count ? "PASS" : "FAIL")} ({passed}/{checks.Count})");
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
        return C3DHeightFieldSnapshot.CreateForVerification("source.connected-region-gate", width, height, values);
    }

    private static ToolRecipeDocument CreateRecipe(C3DHeightFieldSnapshot source, string sourcePath)
    {
        var sourceContract = new ToolRecipeSource(
            source.EntityId,
            "Connected Region Gate Fixture",
            "C3D",
            source.Unit,
            source.FrameId,
            sourcePath,
            source.ByteLength,
            source.ContentSha256,
            source.Width,
            source.Height);
        return new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Connected Region Gate Fixture",
            sourceContract,
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
                    ]),
                new ToolRecipeStep(
                    "step.gate",
                    "connected-region-gate",
                    "Connected Region Coverage / Height Gate",
                    3,
                    [source.EntityId, "filtered.height-field", "connected.regions"],
                    "connected.region.gate",
                    new C3DConnectedRegionGatePolicy(
                        C3DConnectedRegionGateMode.Presence,
                        0,
                        0d,
                        -1000d,
                        1000d,
                        0d,
                        0d,
                        0d,
                        source.Unit).ToRecipeParameters())
            ],
            []);
    }

    private static ToolRecipeDocument WithGatePolicy(
        ToolRecipeDocument recipe,
        C3DConnectedRegionGatePolicy policy) =>
        recipe with
        {
            Steps = recipe.Steps
                .Select(step => step.Id == "step.gate"
                    ? step with { Parameters = policy.ToRecipeParameters() }
                    : step)
                .ToArray()
        };

    private static (string Name, bool Passed, string Evidence) Check(
        string name,
        bool passed,
        string evidence) =>
        (name, passed, evidence);
}
