using System.Globalization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Reporting.RunRecords;

internal static class PhysicalEdgeGapFlushExperimentVerification
{
    private const int Width = 12;
    private const int Height = 8;

    public static int Run(string reportPath)
    {
        var cases = new[]
        {
            Check("known-positive-gap-positive-flush", () => VerifyKnownFlush(14, 4)),
            Check("known-positive-gap-negative-flush", () => VerifyKnownFlush(6, -4)),
            Check("known-positive-gap-zero-flush", () => VerifyKnownFlush(10, 0)),
            Check("reversed-edge-role-negative-gap", VerifyReversedRoleSign),
            Check("noise-and-missing-support", VerifyNoiseAndMissingSupport),
            Check("reversed-polarity-fails-closed", VerifyReversedPolarity),
            Check("equal-strength-merged-edges-are-ambiguous", VerifyAmbiguousEdges),
            Check("insufficient-line-support-fails-closed", VerifyInsufficientSupport),
            Check("zero-gap-shared-evidence-is-not-independent", VerifySharedEvidence),
            Check("deterministic-replay", VerifyDeterministicReplay),
            Check("product-rule-known-pair", VerifyProductRuleKnownPair),
            Check("product-rule-candidate-evidence-boundary", VerifyProductRuleCandidateEvidenceBoundary),
            Check("product-rule-hidden-equal-candidates-no-acceptance", () => VerifyHiddenCompetingCandidates(10d)),
            Check("product-rule-hidden-near-candidates-no-acceptance", () => VerifyHiddenCompetingCandidates(9.999999d)),
            Check("product-rule-role-reversal", VerifyProductRuleRoleReversal),
            Check("product-rule-duplicate-evidence", VerifyProductRuleDuplicateEvidence),
            Check("product-rule-limited-support", VerifyProductRuleLimitedSupport),
            Check("product-rule-tolerance-withheld-without-candidates", VerifyProductRuleToleranceBoundary),
            Check("product-rule-disabled-tolerance", VerifyProductRuleDisabledTolerance),
            Check("product-rule-recipe-save-reopen-replay", () => VerifyProductRecipeReplay(reportPath)),
            Check("product-rule-contact-policy", VerifyProductRuleContactPolicy),
            Check("product-rule-lineage-mismatch", VerifyProductRuleLineageMismatch),
            Check("product-rule-deterministic-hash", VerifyProductRuleDeterministicHash),
            Check("product-rule-across-rows", VerifyProductRuleAcrossRows),
            Check("product-profile-roundtrip-and-reject", VerifyProductProfileRoundTrip)
        };
        var passed = cases.Count(item => item.Passed);
        var status = passed == cases.Length ? "Pass" : "Fail";
        var lines = new List<string>
        {
            $"PhysicalEdgeGapFlushExperimentVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
            "Classification|PRODUCT_RULE_AND_MEASURE|calibration=NotEvaluated|fieldQualification=NotEvaluated|sourceGridOnly=True",
            "Frame|frame.c3d-grid-index|X=column|Y=raw-height|Z=row|gapSign=secondEdgeX-firstEdgeX|flushSign=secondSurfaceHeight-firstSurfaceHeight",
            "Reuse|C3DHeightDifferenceEdgeRule|C3DHeightDifferenceEdgePointSet|C3DLineFitRule|C3DLineFeature",
            "Finding|PhysicalEdgeGapFlushRule preserves signed measurements but withholds tolerance judgement when strongest-only EdgePointSet evidence cannot establish candidate ambiguity; Limited/Warning and NotEvaluated remain deterministic across recipe save/reopen and ordered replay.",
            "Boundary|GapFlushRule remains authored ROI-boundary gap plus aggregate ROI mean-height flush; this experiment does not change that product contract."
        };
        lines.AddRange(cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        Console.WriteLine($"Physical-edge Gap / Flush experiment: {status} ({passed}/{cases.Length})");
        return passed == cases.Length ? 0 : 5;
    }

    private static (bool Passed, string Evidence) VerifyKnownFlush(double rightHeight, double expectedFlush)
    {
        var fixture = CreateThreeBandFixture(10, 2, rightHeight);
        var left = ExtractAndFit(fixture, "left", new ToolRecipeGridRectangle(0, 2, Height, 3), C3DHeightDifferencePolarity.Falling);
        var right = ExtractAndFit(fixture, "right", new ToolRecipeGridRectangle(0, 5, Height, 4), C3DHeightDifferencePolarity.Rising);
        var signedGap = SignedGap(left.Line.Output, right.Line.Output);
        var sideFlush = SideFlush(left.Edge.Output, right.Edge.Output);
        var midpointDelta = right.Line.Output!.AnchorY - left.Line.Output!.AnchorY;
        var midpointLimitationObserved = expectedFlush == 0
            ? Approximately(midpointDelta, 0)
            : !Approximately(midpointDelta, expectedFlush) && Approximately(midpointDelta, expectedFlush / 2);
        var passed = PipelinePassed(left)
            && PipelinePassed(right)
            && Approximately(signedGap, 3)
            && Approximately(sideFlush, expectedFlush)
            && midpointLimitationObserved
            && SameSourceFrameAndUnit(left, right);
        return (passed,
            $"gapExpected=3;gapActual={Format(signedGap)};flushExpected={Format(expectedFlush)};edgeSideFlush={Format(sideFlush)};lineMidpointDelta={Format(midpointDelta)};left={PipelineEvidence(left)};right={PipelineEvidence(right)}");
    }

    private static (bool Passed, string Evidence) VerifyReversedRoleSign()
    {
        var fixture = CreateThreeBandFixture(10, 2, 14);
        var first = ExtractAndFit(fixture, "first", new ToolRecipeGridRectangle(0, 2, Height, 3), C3DHeightDifferencePolarity.Falling);
        var second = ExtractAndFit(fixture, "second", new ToolRecipeGridRectangle(0, 5, Height, 4), C3DHeightDifferencePolarity.Rising);
        var forward = SignedGap(first.Line.Output, second.Line.Output);
        var reversed = SignedGap(second.Line.Output, first.Line.Output);
        return (PipelinePassed(first) && PipelinePassed(second) && Approximately(forward, 3) && Approximately(reversed, -3),
            $"signConvention=second-first;forward={Format(forward)};reversed={Format(reversed)}");
    }

    private static (bool Passed, string Evidence) VerifyNoiseAndMissingSupport()
    {
        var missingRows = new HashSet<int> { 2, 5 };
        var fixture = CreateThreeBandFixture(10, 2, 14, (row, column, value) =>
        {
            if (missingRows.Contains(row) && column is 3 or 4 or 6 or 7)
            {
                return double.NaN;
            }

            return value + (row % 2 == 0 ? 0.1 : -0.1);
        });
        var left = ExtractAndFit(fixture, "left-noisy", new ToolRecipeGridRectangle(0, 2, Height, 3), C3DHeightDifferencePolarity.Falling, maximumResidual: 0.2);
        var right = ExtractAndFit(fixture, "right-noisy", new ToolRecipeGridRectangle(0, 5, Height, 4), C3DHeightDifferencePolarity.Rising, maximumResidual: 0.2);
        var signedGap = SignedGap(left.Line.Output, right.Line.Output);
        var sideFlush = SideFlush(left.Edge.Output, right.Edge.Output);
        var passed = PipelinePassed(left)
            && PipelinePassed(right)
            && left.Edge.Output?.Points.Count == 6
            && right.Edge.Output?.Points.Count == 6
            && left.Edge.Output.Diagnostics.SkippedMissingPairCount > 0
            && right.Edge.Output.Diagnostics.SkippedMissingPairCount > 0
            && Approximately(signedGap, 3)
            && Approximately(sideFlush, 4)
            && left.Line.Output!.Diagnostics.ResidualMaximum <= 0.2
            && right.Line.Output!.Diagnostics.ResidualMaximum <= 0.2;
        return (passed,
            $"gap={Format(signedGap)};flush={Format(sideFlush)};leftMissingPairs={left.Edge.Output?.Diagnostics.SkippedMissingPairCount};rightMissingPairs={right.Edge.Output?.Diagnostics.SkippedMissingPairCount};left={PipelineEvidence(left)};right={PipelineEvidence(right)}");
    }

    private static (bool Passed, string Evidence) VerifyReversedPolarity()
    {
        var fixture = CreateThreeBandFixture(10, 2, 14);
        var edge = ExtractEdge(fixture, "wrong-polarity", new ToolRecipeGridRectangle(0, 2, Height, 3), C3DHeightDifferencePolarity.Rising);
        return (edge.Result.Status == ResultStatus.Error && edge.Output is null,
            $"status={edge.Result.Status};output={(edge.Output is null ? "none" : "present")};message={edge.Result.Message}");
    }

    private static (bool Passed, string Evidence) VerifyAmbiguousEdges()
    {
        var values = Enumerable.Range(0, Height)
            .SelectMany(_ => new[] { 10d, 10d, 10d, 2d, 10d, 10d })
            .ToArray();
        var root = C3DHeightFieldSnapshot.CreateForVerification("source.synthetic.ambiguous", 6, Height, values);
        var fixture = new SurfaceFixture(root, root.CreateDerived("derived.synthetic.ambiguous", root.Values.Span.ToArray(), "physical-edge-gap-flush-experiment"));
        var edge = ExtractEdge(fixture, "ambiguous", new ToolRecipeGridRectangle(0, 2, Height, 3), C3DHeightDifferencePolarity.Absolute);
        var selectedLowestIndex = edge.Output?.Points.All(point => point.FirstColumn == 2) == true;
        var twoEqualCandidatesPerScanline = Enumerable.Range(0, Height).All(row =>
            Math.Abs(values[(row * 6) + 3] - values[(row * 6) + 2]) == 8
            && Math.Abs(values[(row * 6) + 4] - values[(row * 6) + 3]) == 8);
        return (edge.Result.Status == ResultStatus.Pass
            && edge.Output?.Points.Count == Height
            && selectedLowestIndex
            && twoEqualCandidatesPerScanline,
            $"status={edge.Result.Status};equalCandidatesPerScanline=2;selectedFirstColumn={edge.Output?.Points.FirstOrDefault()?.FirstColumn};diagnosticsExposeAmbiguity=false;message={edge.Result.Message}");
    }

    private static (bool Passed, string Evidence) VerifyInsufficientSupport()
    {
        var validRows = new HashSet<int> { 0, 7 };
        var fixture = CreateThreeBandFixture(10, 2, 14, (row, column, value) =>
            !validRows.Contains(row) && column is 3 or 4 ? double.NaN : value);
        var edge = ExtractEdge(fixture, "insufficient", new ToolRecipeGridRectangle(0, 2, Height, 3), C3DHeightDifferencePolarity.Falling);
        var line = FitLine(edge.Output, "insufficient", minimumInlierCount: 4, minimumInlierSpan: 4);
        return (edge.Result.Status == ResultStatus.Pass
            && edge.Output?.Points.Count == 2
            && line.Result.Status == ResultStatus.Error
            && line.Output is null,
            $"edgeStatus={edge.Result.Status};points={edge.Output?.Points.Count};lineStatus={line.Result.Status};lineMessage={line.Result.Message}");
    }

    private static (bool Passed, string Evidence) VerifySharedEvidence()
    {
        var fixture = CreateThreeBandFixture(10, 2, 14);
        var edge = ExtractEdge(fixture, "shared", new ToolRecipeGridRectangle(0, 2, Height, 3), C3DHeightDifferencePolarity.Falling);
        var first = FitLine(edge.Output, "shared-first");
        var second = FitLine(edge.Output, "shared-second");
        var signedGap = SignedGap(first.Output, second.Output);
        return (first.Result.Status == ResultStatus.Pass
            && second.Result.Status == ResultStatus.Pass
            && Approximately(signedGap, 0)
            && first.Output?.InputContentSha256 == second.Output?.InputContentSha256
            && first.Output?.RootSourceSha256 == second.Output?.RootSourceSha256,
            $"gap={Format(signedGap)};sameEdgeEvidence={first.Output?.InputContentSha256 == second.Output?.InputContentSha256};existingPhysicalPairGate=false;classification=must-fail-closed-downstream");
    }

    private static (bool Passed, string Evidence) VerifyDeterministicReplay()
    {
        var fixture = CreateThreeBandFixture(10, 2, 14);
        var first = ExtractAndFit(fixture, "deterministic", new ToolRecipeGridRectangle(0, 2, Height, 3), C3DHeightDifferencePolarity.Falling);
        var second = ExtractAndFit(fixture, "deterministic", new ToolRecipeGridRectangle(0, 2, Height, 3), C3DHeightDifferencePolarity.Falling);
        return (PipelinePassed(first)
            && PipelinePassed(second)
            && first.Edge.Output?.ContentSha256 == second.Edge.Output?.ContentSha256
            && first.Line.Output?.ContentSha256 == second.Line.Output?.ContentSha256,
            $"edgeFirst={first.Edge.Output?.ContentSha256};edgeSecond={second.Edge.Output?.ContentSha256};lineFirst={first.Line.Output?.ContentSha256};lineSecond={second.Line.Output?.ContentSha256}");
    }

    private static (bool Passed, string Evidence) VerifyProductRuleKnownPair()
    {
        var fixture = CreateThreeBandFixture(10, 2, 14);
        var first = ExtractAndFit(fixture, "product-left", new ToolRecipeGridRectangle(0, 2, Height, 3), C3DHeightDifferencePolarity.Falling);
        var second = ExtractAndFit(fixture, "product-right", new ToolRecipeGridRectangle(0, 5, Height, 4), C3DHeightDifferencePolarity.Rising);
        var profile = C3DPhysicalEdgeGapFlushProfile.Default with
        {
            ExpectedGap = 3d,
            GapTolerance = 0d,
            ExpectedFlush = 4d,
            FlushTolerance = 0d
        };
        var evaluation = EvaluatePhysical(first, second, profile, "result.product-known");
        var passed = evaluation.Output is { MeasurementState: C3DPhysicalEdgeGapFlushMeasurementState.Limited, ToleranceState: C3DPhysicalEdgeGapFlushToleranceState.NotEvaluated }
            && evaluation.Result.Status == ResultStatus.Warning
            && Approximately(evaluation.Output.SignedGap, 3d)
            && Approximately(evaluation.Output.SignedFlush, 4d)
            && evaluation.Output.EvidenceSummary.Contains("sourceSha=", StringComparison.Ordinal)
            && evaluation.Output.EvidenceSummary.Contains("firstLine=", StringComparison.Ordinal);
        return (passed, evaluation.Output?.EvidenceSummary ?? evaluation.Result.Message);
    }

    private static (bool Passed, string Evidence) VerifyProductRuleCandidateEvidenceBoundary()
    {
        var fixture = CreateThreeBandFixture(10, 2, 14);
        var first = ExtractAndFit(fixture, "product-candidate-left", new ToolRecipeGridRectangle(0, 2, Height, 3), C3DHeightDifferencePolarity.Falling);
        var second = ExtractAndFit(fixture, "product-candidate-right", new ToolRecipeGridRectangle(0, 5, Height, 4), C3DHeightDifferencePolarity.Rising);
        var evaluation = EvaluatePhysical(first, second, C3DPhysicalEdgeGapFlushProfile.Default, "result.product-candidate-boundary");
        var output = evaluation.Output;
        var passed = output is not null
            && first.Edge.Output is not null
            && second.Edge.Output is not null
            && first.Edge.Output?.CandidateEvidence.Policy == C3DHeightDifferenceEdgeCandidateEvidence.StrongestPerScanlinePolicy
            && second.Edge.Output?.CandidateEvidence.Policy == C3DHeightDifferenceEdgeCandidateEvidence.StrongestPerScanlinePolicy
            && first.Edge.Output.CandidateEvidence.SecondCandidateCount == 0
            && second.Edge.Output.CandidateEvidence.SecondCandidateCount == 0
            && !first.Edge.Output.CandidateEvidence.HasNumericTopAndSecond
            && !second.Edge.Output.CandidateEvidence.HasNumericTopAndSecond
            && output.TopCandidateEvidence.Contains("numericTopAndSecond=False", StringComparison.Ordinal)
            && output.SecondCandidateEvidence.Contains("unavailable", StringComparison.OrdinalIgnoreCase)
            && output.Provenance.Contains("candidateEvidence=", StringComparison.Ordinal);
        return (passed, $"first={first.Edge.Output?.CandidateEvidence.Summary};second={second.Edge.Output?.CandidateEvidence.Summary};result={output?.SecondCandidateEvidence};provenance={output?.Provenance}");
    }

    private static (bool Passed, string Evidence) VerifyHiddenCompetingCandidates(double competingHeight)
    {
        // Two adjacent candidates in the first search window have magnitudes
        // 8 and competingHeight - 2. The independent second edge is at 6.5.
        var values = Enumerable.Range(0, Height)
            .SelectMany(_ => new[] { 10d, 10d, 10d, 2d, competingHeight, competingHeight, competingHeight, 14d, 14d, 14d, 14d, 14d })
            .ToArray();
        var root = C3DHeightFieldSnapshot.CreateForVerification("source.synthetic.hidden-candidates", Width, Height, values);
        var fixture = new SurfaceFixture(root, root.CreateDerived("derived.synthetic.hidden-candidates", values, "hidden-candidate-reproducer"));
        var first = ExtractAndFit(fixture, "hidden-first", new ToolRecipeGridRectangle(0, 2, Height, 3), C3DHeightDifferencePolarity.Absolute);
        var second = ExtractAndFit(fixture, "hidden-second", new ToolRecipeGridRectangle(0, 6, Height, 3), C3DHeightDifferencePolarity.Rising);
        var evaluation = EvaluatePhysical(first, second, C3DPhysicalEdgeGapFlushProfile.Default with
        {
            ExpectedGap = 4d,
            GapTolerance = 0d,
            ExpectedFlush = 4d,
            FlushTolerance = 0d
        }, "result.hidden-candidates");
        var passed = PipelinePassed(first) && PipelinePassed(second)
            && Approximately(evaluation.Output?.SignedGap ?? double.NaN, 4d)
            && Approximately(evaluation.Output?.SignedFlush ?? double.NaN, 4d)
            && evaluation.Output?.MeasurementState == C3DPhysicalEdgeGapFlushMeasurementState.Limited
            && evaluation.Result.Status == ResultStatus.Warning
            && evaluation.Output.ToleranceState == C3DPhysicalEdgeGapFlushToleranceState.NotEvaluated
            && evaluation.Output.FailureReason.Contains("ambiguity cannot be evaluated", StringComparison.Ordinal)
            && evaluation.Result.Metrics.Where(metric => metric.Name is "Signed gap" or "Signed flush").All(metric => metric.Status == ResultStatus.Warning);
        return (passed, $"knownTop=8;knownSecond={Format(competingHeight - 2d)};{evaluation.Output?.EvidenceSummary ?? evaluation.Result.Message}");
    }

    private static (bool Passed, string Evidence) VerifyProductRuleRoleReversal()
    {
        var fixture = CreateThreeBandFixture(10, 2, 14);
        var left = ExtractAndFit(fixture, "product-reverse-left", new ToolRecipeGridRectangle(0, 2, Height, 3), C3DHeightDifferencePolarity.Falling);
        var right = ExtractAndFit(fixture, "product-reverse-right", new ToolRecipeGridRectangle(0, 5, Height, 4), C3DHeightDifferencePolarity.Rising);
        var profile = C3DPhysicalEdgeGapFlushProfile.Default with
        {
            FirstSurfaceSide = C3DPhysicalEdgeSurfaceSide.Second,
            SecondSurfaceSide = C3DPhysicalEdgeSurfaceSide.First,
            ExpectedGap = -3d,
            GapTolerance = 0d,
            ExpectedFlush = -4d,
            FlushTolerance = 0d
        };
        var evaluation = EvaluatePhysical(right, left, profile, "result.product-reversed");
        var passed = evaluation.Output is not null
            && evaluation.Output.MeasurementState == C3DPhysicalEdgeGapFlushMeasurementState.Limited
            && evaluation.Output.ToleranceState == C3DPhysicalEdgeGapFlushToleranceState.NotEvaluated
            && Approximately(evaluation.Output.SignedGap, -3d)
            && Approximately(evaluation.Output.SignedFlush, -4d);
        return (passed, evaluation.Output?.EvidenceSummary ?? evaluation.Result.Message);
    }

    private static (bool Passed, string Evidence) VerifyProductRuleDuplicateEvidence()
    {
        var fixture = CreateThreeBandFixture(10, 2, 14);
        var pair = ExtractAndFit(fixture, "product-duplicate", new ToolRecipeGridRectangle(0, 2, Height, 3), C3DHeightDifferencePolarity.Falling);
        var evaluation = EvaluatePhysical(pair, pair, C3DPhysicalEdgeGapFlushProfile.Default, "result.product-duplicate");
        var passed = evaluation.Output is not null
            && evaluation.Output.MeasurementState == C3DPhysicalEdgeGapFlushMeasurementState.Rejected
            && evaluation.Result.Status == ResultStatus.Warning;
        return (passed, evaluation.Output?.EvidenceSummary ?? evaluation.Result.Message);
    }

    private static (bool Passed, string Evidence) VerifyProductRuleLimitedSupport()
    {
        var fixture = CreateThreeBandFixture(10, 2, 14);
        var first = ExtractAndFit(fixture, "product-limited-left", new ToolRecipeGridRectangle(0, 2, Height, 3), C3DHeightDifferencePolarity.Falling);
        var second = ExtractAndFit(fixture, "product-limited-right", new ToolRecipeGridRectangle(0, 5, Height, 4), C3DHeightDifferencePolarity.Rising);
        var profile = C3DPhysicalEdgeGapFlushProfile.Default with { MinimumValidPairCount = Height + 1 };
        var evaluation = EvaluatePhysical(first, second, profile, "result.product-limited");
        var passed = evaluation.Output is not null
            && evaluation.Output.MeasurementState == C3DPhysicalEdgeGapFlushMeasurementState.Limited
            && evaluation.Result.Status == ResultStatus.Warning
            && evaluation.Output.ValidPairCount == Height;
        return (passed, evaluation.Output?.EvidenceSummary ?? evaluation.Result.Message);
    }

    private static (bool Passed, string Evidence) VerifyProductRuleToleranceBoundary()
    {
        var fixture = CreateThreeBandFixture(10, 2, 14);
        var first = ExtractAndFit(fixture, "product-tolerance-left", new ToolRecipeGridRectangle(0, 2, Height, 3), C3DHeightDifferencePolarity.Falling);
        var second = ExtractAndFit(fixture, "product-tolerance-right", new ToolRecipeGridRectangle(0, 5, Height, 4), C3DHeightDifferencePolarity.Rising);
        var inclusive = EvaluatePhysical(first, second, C3DPhysicalEdgeGapFlushProfile.Default with
        {
            ExpectedGap = 3d,
            GapTolerance = 0d,
            ExpectedFlush = 4d,
            FlushTolerance = 0d
        }, "result.product-tolerance-inclusive");
        var failing = EvaluatePhysical(first, second, C3DPhysicalEdgeGapFlushProfile.Default with
        {
            ExpectedGap = 3.000001d,
            GapTolerance = 0d,
            ExpectedFlush = 4d,
            FlushTolerance = 0d
        }, "result.product-tolerance-fail");
        var passed = inclusive.Result.Status == ResultStatus.Warning
            && failing.Result.Status == ResultStatus.Warning
            && inclusive.Output?.ToleranceState == C3DPhysicalEdgeGapFlushToleranceState.NotEvaluated
            && failing.Output?.ToleranceState == C3DPhysicalEdgeGapFlushToleranceState.NotEvaluated
            && Approximately(inclusive.Output.SignedGap, 3d)
            && Approximately(failing.Output.SignedGap, 3d);
        return (passed, $"inclusive={inclusive.Output?.EvidenceSummary};failing={failing.Output?.EvidenceSummary}");
    }

    private static (bool Passed, string Evidence) VerifyProductRuleLineageMismatch()
    {
        var firstFixture = CreateThreeBandFixture(10, 2, 14);
        var secondFixture = CreateThreeBandFixture(10, 2, 14, sourceSuffix: "mismatch");
        var first = ExtractAndFit(firstFixture, "product-lineage-left", new ToolRecipeGridRectangle(0, 2, Height, 3), C3DHeightDifferencePolarity.Falling);
        var second = ExtractAndFit(secondFixture, "product-lineage-right", new ToolRecipeGridRectangle(0, 5, Height, 4), C3DHeightDifferencePolarity.Rising);
        var evaluation = EvaluatePhysical(first, second, C3DPhysicalEdgeGapFlushProfile.Default, "result.product-lineage");
        var passed = evaluation.Output is not null
            && evaluation.Output.MeasurementState == C3DPhysicalEdgeGapFlushMeasurementState.Invalid
            && evaluation.Result.Status == ResultStatus.Error
            && evaluation.Output.FailureReason.Contains("root source", StringComparison.OrdinalIgnoreCase);
        return (passed, evaluation.Output?.EvidenceSummary ?? evaluation.Result.Message);
    }

    private static (bool Passed, string Evidence) VerifyProductRuleDisabledTolerance()
    {
        var fixture = CreateThreeBandFixture(10, 2, 14);
        var first = ExtractAndFit(fixture, "disabled-first", new ToolRecipeGridRectangle(0, 2, Height, 3), C3DHeightDifferencePolarity.Falling);
        var second = ExtractAndFit(fixture, "disabled-second", new ToolRecipeGridRectangle(0, 5, Height, 4), C3DHeightDifferencePolarity.Rising);
        var evaluation = EvaluatePhysical(first, second, C3DPhysicalEdgeGapFlushProfile.Default with { ToleranceEnabled = false }, "result.disabled-tolerance");
        return (evaluation.Output is { MeasurementState: C3DPhysicalEdgeGapFlushMeasurementState.Limited, ToleranceState: C3DPhysicalEdgeGapFlushToleranceState.NotConfigured }
            && evaluation.Result.Status == ResultStatus.Warning
            && Approximately(evaluation.Output.SignedGap, 3d)
            && Approximately(evaluation.Output.SignedFlush, 4d), evaluation.Output?.EvidenceSummary ?? evaluation.Result.Message);
    }

    private static (bool Passed, string Evidence) VerifyProductRecipeReplay(string reportPath)
    {
        var directory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "physical-recipe-replay");
        Directory.CreateDirectory(directory);
        var sourcePath = Path.Combine(directory, "source.c3d");
        var recipePath = Path.Combine(directory, "physical.ov3d-teach.json");
        var root = CreateThreeBandFixture(10, 2, 14).Root;
        root.SaveC3D(sourcePath);

        ToolRecipeSelection Selection(string id, int column, int columns) => new(
            id, id, ToolRecipeSelectionKinds.GridRectangle, root.EntityId, root.FrameId,
            new ToolRecipeSelectionSourceBinding("C3D", root.ContentSha256, Width, Height),
            new ToolRecipeGridRectangle(0, column, Height, columns), null, null);
        ToolRecipeStep Edge(string id, string selection, string polarity) => new(
            $"step.edge.{id}", "height-difference-edge", "Height Difference Edge", 1, ["derived.filtered", selection], $"edge.{id}",
            [new("ComparisonAxis", "AcrossColumns"), new("Polarity", polarity), new("MinimumDelta", "3"), new("CandidatePolicy", "StrongestPerScanline"), new("PointPolicy", "PairMidpoint"), new("MissingValuePolicy", "SkipPair"), new("BoundaryPolicy", "WithinSelection")]);
        ToolRecipeStep Line(string id) => new(
            $"step.line.{id}", "three-d-line-fit", "3D Line Fit", 1, [$"edge.{id}"], $"line.{id}",
            [new("FitMethod", "DeterministicConsensusOrthogonalTls"), new("MaximumOrthogonalResidual", "0.001"), new("MinimumInlierCount", "4"), new("MinimumInlierRatio", "0.75"), new("MinimumInlierScanlineSpan", "4"), new("HypothesisPolicy", "Sha256PairSchedule"), new("MaximumHypotheses", "256"), new("RefinementPolicy", "OrthogonalTlsUntilStable10"), new("DirectionPolicy", "PositiveScanlineAxis"), new("EndpointPolicy", "InlierProjectionExtents")]);
        var profile = C3DPhysicalEdgeGapFlushProfile.Default with { ExpectedGap = 3d, ExpectedFlush = 4d, GapTolerance = 0d, FlushTolerance = 0d };
        var document = new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion, "Physical candidate admission replay",
            new ToolRecipeSource(root.EntityId, "Synthetic", "C3D", root.Unit, root.FrameId, sourcePath, root.ByteLength, root.ContentSha256, Width, Height),
            [],
            [
                new ToolRecipeStep("step.filter", "filter", "Filter", 1, [root.EntityId], "derived.filtered",
                    [new("Method", "Median"), new("KernelSize", "3"), new("MissingValuePolicy", "PreserveMask"), new("BoundaryPolicy", "AvailableNeighbors")]),
                Edge("first", "selection.first", "Falling"),
                Edge("second", "selection.second", "Rising"),
                Line("first"),
                Line("second"),
                new ToolRecipeStep("step.physical", "physical-edge-gap-flush", PhysicalEdgeGapFlushRule.ToolName, 4,
                    ["line.first", "line.second", "edge.first", "edge.second"], "result.physical", profile.ToRecipeParameters())
            ],
            [Selection("selection.first", 2, 3), Selection("selection.second", 5, 4)]);

        var before = ToolRecipeOrderedGraphExecution.Execute(document, sourcePath);
        ToolRecipeDocumentStore.Save(recipePath, document);
        var storedBytes = File.ReadAllBytes(recipePath);
        var filesBeforeLoad = Directory.GetFiles(directory).OrderBy(path => path).ToArray();
        var reopened = ToolRecipeDocumentStore.Load(recipePath);
        var loadPreservedFiles = storedBytes.SequenceEqual(File.ReadAllBytes(recipePath))
            && filesBeforeLoad.SequenceEqual(Directory.GetFiles(directory).OrderBy(path => path));
        var restoredProfile = C3DPhysicalEdgeGapFlushProfile.TryParse(reopened.Steps.Last().Parameters, out var parsed, out _) && parsed == profile;
        var after = ToolRecipeOrderedGraphExecution.Execute(reopened, sourcePath);
        var lastBefore = before.Steps.LastOrDefault();
        var lastAfter = after.Steps.LastOrDefault();
        var identity = OrderedRunRecordIdentity.Create(recipePath, sourcePath);
        var record = OrderedRunRecordFactory.Create(identity, reopened, after, "NotCompared",
            new InspectionRunArtifacts("recorded.txt", null, null, null, null, null));
        var recordPath = Path.Combine(directory, "physical.record.json");
        InspectionRunRecordJson.Write(recordPath, record);
        var roundtrip = InspectionRunRecordJson.Read(recordPath);
        var htmlPath = Path.Combine(directory, "physical.html");
        var csvPath = Path.Combine(directory, "physical.csv");
        InspectionRunRecordReports.WriteHtml(htmlPath, roundtrip);
        InspectionRunRecordReports.WriteCsv(csvPath, roundtrip);
        var message = lastAfter?.Result.Message ?? string.Empty;
        var reportsPreserveAdmission = message.Contains("state=Limited;tolerance=NotEvaluated;gap=3;flush=4", StringComparison.Ordinal)
            && roundtrip.Steps?.LastOrDefault()?.Status == ResultStatus.Warning
            && roundtrip.Steps.Last().Message == message
            && File.ReadAllText(htmlPath).Contains(System.Net.WebUtility.HtmlEncode(message), StringComparison.Ordinal)
            && File.ReadAllText(csvPath).Contains("\"" + message.Replace("\"", "\"\"") + "\"", StringComparison.Ordinal)
            && identity.SourceSha256 == OrderedRunRecordIdentity.Create(recipePath, sourcePath).SourceSha256
            && storedBytes.SequenceEqual(File.ReadAllBytes(recipePath));
        var passed = before.Steps.Count == 6 && after.Steps.Count == 6
            && before.Status == ResultStatus.Warning && after.Status == ResultStatus.Warning
            && lastAfter?.Result.Status == ResultStatus.Warning
            && lastAfter.Evidence.Contains("state=Limited;tolerance=NotEvaluated;gap=3;flush=4", StringComparison.Ordinal)
            && lastAfter.Evidence.Contains("ambiguity cannot be evaluated", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(lastAfter.OutputContentSha256)
            && lastAfter.OutputContentSha256 == lastBefore?.OutputContentSha256
            && lastAfter.Evidence == lastBefore?.Evidence
            && restoredProfile && loadPreservedFiles && reportsPreserveAdmission;
        return (passed, $"before={before.Status}/{before.Steps.Count};after={after.Status}/{after.Steps.Count};restoredProfile={restoredProfile};loadPreservedFiles={loadPreservedFiles};reportsPreserveAdmission={reportsPreserveAdmission};hash={lastAfter?.OutputContentSha256};result={lastAfter?.Evidence};message={after.Message}");
    }

    private static (bool Passed, string Evidence) VerifyProductRuleContactPolicy()
    {
        var fixture = CreateThreeBandFixture(10, 2, 14);
        var first = ExtractAndFit(fixture, "product-contact-first", new ToolRecipeGridRectangle(0, 2, Height, 3), C3DHeightDifferencePolarity.Falling);
        var second = ExtractAndFit(fixture, "product-contact-second", new ToolRecipeGridRectangle(0, 2, Height, 3), C3DHeightDifferencePolarity.Falling);
        var rejected = EvaluatePhysical(first, second, C3DPhysicalEdgeGapFlushProfile.Default, "result.product-contact-rejected");
        var allowed = EvaluatePhysical(
            first,
            second,
            C3DPhysicalEdgeGapFlushProfile.Default with
            {
                ContactAllowed = true,
                ExpectedGap = 0d,
                ExpectedFlush = -8d
            },
            "result.product-contact-allowed");
        var passed = rejected.Output?.MeasurementState == C3DPhysicalEdgeGapFlushMeasurementState.Rejected
            && allowed.Output?.MeasurementState == C3DPhysicalEdgeGapFlushMeasurementState.Limited
            && rejected.Result.Status == ResultStatus.Warning
            && allowed.Result.Status == ResultStatus.Warning
            && allowed.Output.ToleranceState == C3DPhysicalEdgeGapFlushToleranceState.NotEvaluated
            && Approximately(allowed.Output.SignedGap, 0d);
        return (passed, $"rejected={rejected.Output?.EvidenceSummary};allowed={allowed.Output?.EvidenceSummary}");
    }

    private static (bool Passed, string Evidence) VerifyProductRuleDeterministicHash()
    {
        var fixture = CreateThreeBandFixture(10, 2, 14);
        var first = ExtractAndFit(fixture, "product-hash-left", new ToolRecipeGridRectangle(0, 2, Height, 3), C3DHeightDifferencePolarity.Falling);
        var second = ExtractAndFit(fixture, "product-hash-right", new ToolRecipeGridRectangle(0, 5, Height, 4), C3DHeightDifferencePolarity.Rising);
        var profile = C3DPhysicalEdgeGapFlushProfile.Default;
        var one = EvaluatePhysical(first, second, profile, "result.product-hash");
        var two = EvaluatePhysical(first, second, profile, "result.product-hash");
        var passed = one.Output is not null
            && two.Output is not null
            && one.Output.ContentSha256 == two.Output.ContentSha256
            && one.Output.EvidenceSummary == two.Output.EvidenceSummary;
        return (passed, $"first={one.Output?.ContentSha256};second={two.Output?.ContentSha256}");
    }

    private static (bool Passed, string Evidence) VerifyProductRuleAcrossRows()
    {
        var fixture = CreateHorizontalBandFixture(10, 2, 14);
        var first = ExtractAndFit(
            fixture,
            "product-rows-first",
            new ToolRecipeGridRectangle(1, 0, 3, Width),
            C3DHeightDifferencePolarity.Falling,
            axis: C3DHeightDifferenceComparisonAxis.AcrossRows);
        var second = ExtractAndFit(
            fixture,
            "product-rows-second",
            new ToolRecipeGridRectangle(4, 0, 3, Width),
            C3DHeightDifferencePolarity.Rising,
            axis: C3DHeightDifferenceComparisonAxis.AcrossRows);
        var profile = C3DPhysicalEdgeGapFlushProfile.Default with
        {
            ComparisonAxis = C3DHeightDifferenceComparisonAxis.AcrossRows,
            ExpectedGap = 3d,
            GapTolerance = 0d,
            ExpectedFlush = 4d,
            FlushTolerance = 0d
        };
        var evaluation = EvaluatePhysical(first, second, profile, "result.product-rows");
        var passed = evaluation.Output is not null
            && evaluation.Output.MeasurementState == C3DPhysicalEdgeGapFlushMeasurementState.Limited
            && evaluation.Output.ToleranceState == C3DPhysicalEdgeGapFlushToleranceState.NotEvaluated
            && evaluation.Result.Status == ResultStatus.Warning
            && Approximately(evaluation.Output.SignedGap, 3d)
            && Approximately(evaluation.Output.SignedFlush, 4d);
        return (passed, evaluation.Output?.EvidenceSummary ?? evaluation.Result.Message);
    }

    private static (bool Passed, string Evidence) VerifyProductProfileRoundTrip()
    {
        var parameters = C3DPhysicalEdgeGapFlushProfile.Default.ToRecipeParameters();
        var roundTrip = C3DPhysicalEdgeGapFlushProfile.TryParse(parameters, out var parsed, out var parseMessage);
        var malformed = parameters
            .Select((parameter, index) => index == 0
                ? new ToolRecipeParameter("UnknownParameter", parameter.Value)
                : parameter)
            .ToArray();
        var rejected = !C3DPhysicalEdgeGapFlushProfile.TryParse(malformed, out _, out var rejectMessage);
        var passed = roundTrip
            && parsed is not null
            && parsed == C3DPhysicalEdgeGapFlushProfile.Default
            && rejected;
        return (passed, $"roundTrip={roundTrip};parsed={parsed?.SectionId};parseMessage={parseMessage};malformedRejected={rejected};rejectMessage={rejectMessage}");
    }

    private static C3DPhysicalEdgeGapFlushEvaluation EvaluatePhysical(
        PipelineResult first,
        PipelineResult second,
        C3DPhysicalEdgeGapFlushProfile profile,
        string outputEntityId)
    {
        if (first.Line.Output is null || second.Line.Output is null || first.Edge.Output is null || second.Edge.Output is null)
        {
            return new C3DPhysicalEdgeGapFlushEvaluation(
                new ToolResult(PhysicalEdgeGapFlushRule.ToolName, ResultStatus.Error, "Synthetic edge/line pipeline did not produce both published pairs.", TimeSpan.Zero, [], []),
                null);
        }

        return PhysicalEdgeGapFlushRule.Evaluate(new C3DPhysicalEdgeGapFlushInput(
            $"step.product.{outputEntityId}",
            outputEntityId,
            first.Line.Output,
            second.Line.Output,
            first.Edge.Output,
            second.Edge.Output,
            profile));
    }

    private static SurfaceFixture CreateThreeBandFixture(
        double leftHeight,
        double gapHeight,
        double rightHeight,
        Func<int, int, double, double>? transform = null,
        string sourceSuffix = "")
    {
        var values = new double[Width * Height];
        for (var row = 0; row < Height; row++)
        {
            for (var column = 0; column < Width; column++)
            {
                var value = column <= 3 ? leftHeight : column <= 6 ? gapHeight : rightHeight;
                values[(row * Width) + column] = transform?.Invoke(row, column, value) ?? value;
            }
        }

        var rootId = $"source.synthetic.physical-edge-gap-flush{sourceSuffix}";
        var root = C3DHeightFieldSnapshot.CreateForVerification(rootId, Width, Height, values);
        return new SurfaceFixture(root, root.CreateDerived($"derived.synthetic.physical-edge-gap-flush{sourceSuffix}", root.Values.Span.ToArray(), "physical-edge-gap-flush-experiment"));
    }

    private static SurfaceFixture CreateHorizontalBandFixture(
        double firstHeight,
        double gapHeight,
        double secondHeight)
    {
        var values = new double[Width * Height];
        for (var row = 0; row < Height; row++)
        {
            var value = row <= 2 ? firstHeight : row <= 5 ? gapHeight : secondHeight;
            for (var column = 0; column < Width; column++)
            {
                values[(row * Width) + column] = value;
            }
        }

        var root = C3DHeightFieldSnapshot.CreateForVerification(
            "source.synthetic.physical-edge-gap-flush-rows",
            Width,
            Height,
            values);
        return new SurfaceFixture(
            root,
            root.CreateDerived(
                "derived.synthetic.physical-edge-gap-flush-rows",
                root.Values.Span.ToArray(),
                "physical-edge-gap-flush-experiment"));
    }

    private static PipelineResult ExtractAndFit(
        SurfaceFixture fixture,
        string id,
        ToolRecipeGridRectangle selection,
        C3DHeightDifferencePolarity polarity,
        double maximumResidual = 0.001,
        C3DHeightDifferenceComparisonAxis axis = C3DHeightDifferenceComparisonAxis.AcrossColumns)
    {
        var edge = ExtractEdge(fixture, id, selection, polarity, axis);
        var line = FitLine(edge.Output, id, maximumResidual);
        return new PipelineResult(edge, line);
    }

    private static C3DHeightDifferenceEdgeEvaluation ExtractEdge(
        SurfaceFixture fixture,
        string id,
        ToolRecipeGridRectangle selection,
        C3DHeightDifferencePolarity polarity,
        C3DHeightDifferenceComparisonAxis axis = C3DHeightDifferenceComparisonAxis.AcrossColumns) =>
        C3DHeightDifferenceEdgeRule.Evaluate(new C3DHeightDifferenceEdgeInput(
            $"step.edge.{id}",
            fixture.Derived,
            fixture.Root.EntityId,
            $"selection.edge.{id}",
            selection,
            $"derived.edge.{id}",
            axis,
            polarity,
            3));

    private static C3DLineFitEvaluation FitLine(
        C3DHeightDifferenceEdgePointSet? edge,
        string id,
        double maximumResidual = 0.001,
        int minimumInlierCount = 4,
        int minimumInlierSpan = 4)
    {
        if (edge is null)
        {
            return new C3DLineFitEvaluation(new ToolResult("3D Line Fit", ResultStatus.Error, "EdgePointSet is required.", TimeSpan.Zero, [], []), null);
        }

        return C3DLineFitRule.Evaluate(new C3DLineFitInput(
            $"step.line.{id}",
            edge,
            $"derived.line.{id}",
            maximumResidual,
            minimumInlierCount,
            0.75,
            minimumInlierSpan));
    }

    private static bool PipelinePassed(PipelineResult result) =>
        result.Edge.Result.Status == ResultStatus.Pass
        && result.Edge.Output is not null
        && result.Line.Result.Status == ResultStatus.Pass
        && result.Line.Output is not null;

    private static bool SameSourceFrameAndUnit(PipelineResult first, PipelineResult second) =>
        first.Edge.Output?.RootSourceSha256 == second.Edge.Output?.RootSourceSha256
        && first.Edge.Output?.FrameId == second.Edge.Output?.FrameId
        && first.Edge.Output?.Unit == second.Edge.Output?.Unit
        && first.Line.Output?.RootSourceSha256 == second.Line.Output?.RootSourceSha256
        && first.Line.Output?.FrameId == second.Line.Output?.FrameId
        && first.Line.Output?.Unit == second.Line.Output?.Unit;

    private static double SignedGap(C3DLineFeature? first, C3DLineFeature? second) =>
        first is null || second is null ? double.NaN : second.AnchorX - first.AnchorX;

    private static double SideFlush(C3DHeightDifferenceEdgePointSet? left, C3DHeightDifferenceEdgePointSet? right) =>
        left is null || right is null || left.Points.Count == 0 || right.Points.Count == 0
            ? double.NaN
            : right.Points.Average(point => point.SecondHeight) - left.Points.Average(point => point.FirstHeight);

    private static bool Approximately(double actual, double expected, double tolerance = 1e-9) =>
        double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance;

    private static string PipelineEvidence(PipelineResult result) =>
        $"edge={result.Edge.Result.Status}/{result.Edge.Output?.Points.Count};line={result.Line.Result.Status}/{result.Line.Output?.Diagnostics.InlierCount};frame={result.Line.Output?.FrameId};unit={result.Line.Output?.Unit}";

    private static string Format(double value) => value.ToString("F6", CultureInfo.InvariantCulture);
    private static string Clean(string value) => value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');

    private static VerificationCase Check(string name, Func<(bool Passed, string Evidence)> verify)
    {
        try
        {
            var result = verify();
            return new VerificationCase(name, result.Passed, result.Evidence);
        }
        catch (Exception exception)
        {
            return new VerificationCase(name, false, $"unexpected {exception.GetType().Name}: {exception.Message}");
        }
    }

    private sealed record SurfaceFixture(C3DHeightFieldSnapshot Root, C3DHeightFieldSnapshot Derived);
    private sealed record PipelineResult(C3DHeightDifferenceEdgeEvaluation Edge, C3DLineFitEvaluation Line);
    private sealed record VerificationCase(string Name, bool Passed, string Evidence);
}
