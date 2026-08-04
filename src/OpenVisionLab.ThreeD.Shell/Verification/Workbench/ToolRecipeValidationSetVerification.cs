using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell;

internal static class ToolRecipeValidationSetVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string> { "Validation Set ordered graph verification" };
        var passed = 0;
        var total = 0;
        var artifactRoot = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(reportPath))!,
            "validation-set-fixture");

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition) passed++;
        }

        try
        {
            Directory.CreateDirectory(artifactRoot);
            var taughtPath = Path.Combine(artifactRoot, "taught.C3D");
            var passPath = Path.Combine(artifactRoot, "sample-pass.C3D");
            var failPath = Path.Combine(artifactRoot, "sample-fail.C3D");
            var mismatchPath = Path.Combine(artifactRoot, "sample-grid-mismatch.C3D");
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.validation", 4, 4,
                [10, 11, 12, 13, 11, 12, 13, 14, 12, 13, 14, 15, 13, 14, 15, 16]).SaveC3D(taughtPath);
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.validation", 4, 4,
                [9, 10, 11, 12, 10, 11, 12, 13, 11, 12, 13, 14, 12, 13, 14, 15]).SaveC3D(passPath);
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.validation", 4, 4,
                [10, 11, 12, 13, 11, 12, 13, 14, 32, 33, 34, 35, 33, 34, 35, 36]).SaveC3D(failPath);
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.validation", 3, 3,
                [10, 11, 12, 11, 12, 13, 12, 13, 14]).SaveC3D(mismatchPath);

            var binding = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(taughtPath);
            var sourceInfo = new FileInfo(taughtPath);
            var source = new ToolRecipeSource(
                "source.validation",
                "Validation taught source",
                "C3D",
                "model",
                "frame.c3d-grid-index",
                taughtPath,
                sourceInfo.Length,
                binding.ContentSha256,
                binding.GridWidth,
                binding.GridHeight);
            var referenceSelection = new ToolRecipeSelection(
                "selection.validation.reference-roi",
                "Validation Reference ROI",
                ToolRecipeSelectionKinds.GridRectangle,
                source.Id,
                source.FrameId,
                binding,
                new ToolRecipeGridRectangle(0, 0, 2, 4),
                null,
                null);
            var measurementSelection = new ToolRecipeSelection(
                "selection.validation.measurement-roi",
                "Validation Measurement ROI",
                ToolRecipeSelectionKinds.GridRectangle,
                source.Id,
                source.FrameId,
                binding,
                new ToolRecipeGridRectangle(2, 0, 2, 4),
                null,
                null);
            var step = new ToolRecipeStep(
                "step.validation.measurement",
                "thickness",
                "Dual-surface Thickness",
                3,
                [source.Id, referenceSelection.Id, measurementSelection.Id],
                "result.validation.measurement",
                [
                    new ToolRecipeParameter("MinimumThickness", "0"),
                    new ToolRecipeParameter("MaximumThickness", "10"),
                    new ToolRecipeParameter("MinimumValidSampleCount", "1")
                ]);
            var document = new ToolRecipeDocument(
                ToolRecipeDocument.CurrentSchemaVersion,
                "Validation Set fixture",
                source,
                [],
                [step],
                [referenceSelection, measurementSelection]);
            var recipePath = Path.Combine(artifactRoot, "validation-set-fixture.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(recipePath, document);

            Check(
                "supported contract is explicit",
                ToolRecipeValidationSetExecution.CanExecute(document, out var capability),
                capability);

            var originalPath = document.Source.Path;
            var originalHash = document.Source.ContentSha256;
            var result = ToolRecipeValidationSetExecution.Execute(
                document,
                [passPath, failPath, mismatchPath]);
            Check(
                "all selected samples complete",
                result.Samples.Count == 3,
                result.Message);
            Check(
                "passing sample remains Pass",
                result.Samples[0].Status == ResultStatus.Pass,
                $"{result.Samples[0].Status} | {result.Samples[0].Message}");
            Check(
                "out-of-tolerance sample is Fail",
                result.Samples[1].Status == ResultStatus.Fail,
                $"{result.Samples[1].Status} | {result.Samples[1].Steps.Single().Evidence}");
            Check(
                "grid mismatch fails closed",
                result.Samples[2].Status == ResultStatus.Error
                && result.Samples[2].Message.Contains("Grid mismatch", StringComparison.Ordinal),
                $"{result.Samples[2].Status} | {result.Samples[2].Message}");
            Check(
                "failure does not stop later evidence",
                result.Samples[2].Order == 3,
                string.Join(",", result.Samples.Select(sample => $"{sample.Order}:{sample.Status}")));
            Check(
                "aggregate preserves failure and error",
                result.Status == ResultStatus.Error,
                result.Status.ToString());
            Check(
                "authored recipe is not mutated",
                string.Equals(document.Source.Path, originalPath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(document.Source.ContentSha256, originalHash, StringComparison.OrdinalIgnoreCase),
                $"{document.Source.Path} | {document.Source.ContentSha256}");

            var labeledResult = ToolRecipeValidationSetExecution.Execute(
                document,
                [
                    new ToolRecipeValidationSampleInput(
                        passPath,
                        ToolRecipeValidationSampleRole.Good),
                    new ToolRecipeValidationSampleInput(
                        failPath,
                        ToolRecipeValidationSampleRole.Bad),
                    new ToolRecipeValidationSampleInput(
                        taughtPath,
                        ToolRecipeValidationSampleRole.HeldOut)
                ]);
            var labeledEvidence =
                ToolRecipeLabeledEvidenceAnalyzer.Analyze(
                    document,
                    labeledResult);
            Check(
                "Good Bad Held-out roles survive ordered execution",
                labeledResult.Samples.Select(sample => sample.Role)
                    .SequenceEqual(
                    [
                        ToolRecipeValidationSampleRole.Good,
                        ToolRecipeValidationSampleRole.Bad,
                        ToolRecipeValidationSampleRole.HeldOut
                    ]),
                string.Join(
                    ",",
                    labeledResult.Samples.Select(sample => sample.Role)));
            Check(
                "labeled evidence reports all three role counts",
                labeledEvidence.GoodSampleCount == 1
                && labeledEvidence.BadSampleCount == 1
                && labeledEvidence.HeldOutSampleCount == 1
                && labeledEvidence.Warnings.Count == 0,
                labeledEvidence.Message);
            var stepDistribution = labeledEvidence.Distributions.FirstOrDefault(
                distribution =>
                    distribution.Scope == ToolRecipeEvidenceScope.StepMetric
                    && distribution.OwnerId == step.Id
                    && distribution.MetricName == "Mean");
            Check(
                "per-step metric distribution is reproducible by role",
                stepDistribution is not null
                && stepDistribution.RoleStatistics.All(
                    statistics => statistics.ValueCount == 1),
                stepDistribution is null
                    ? "missing"
                    : string.Join(
                        ";",
                        stepDistribution.RoleStatistics.Select(
                            statistics =>
                                $"{statistics.Role}:{statistics.Mean:R}")));
            Check(
                "per-region raw-height distributions cover both ROIs",
                labeledEvidence.Distributions.Count(distribution =>
                    distribution.Scope == ToolRecipeEvidenceScope.RegionMetric
                    && distribution.MetricName == "Mean raw height") == 2,
                string.Join(
                    ",",
                    labeledEvidence.Distributions
                        .Where(distribution =>
                            distribution.Scope
                            == ToolRecipeEvidenceScope.RegionMetric)
                        .Select(distribution => distribution.OwnerId)
                        .Distinct()));
            Check(
                "Held-out statistics are visible but excluded from development",
                stepDistribution?.RoleStatistics.Single(statistics =>
                    statistics.Role
                    == ToolRecipeValidationSampleRole.HeldOut) is
                    {
                        ValueCount: 1,
                        IncludedInDevelopment: false
                    }
                && stepDistribution.RoleStatistics.Where(statistics =>
                        statistics.Role
                        != ToolRecipeValidationSampleRole.HeldOut)
                    .All(statistics => statistics.IncludedInDevelopment),
                stepDistribution is null
                    ? "missing"
                    : string.Join(
                        ",",
                        stepDistribution.RoleStatistics.Select(
                            statistics =>
                                $"{statistics.Role}:included={statistics.IncludedInDevelopment}")));

            var roleDefinition = new ToolRecipeValidationSetDefinition(
                ToolRecipeValidationSetDefinition.CurrentSchemaVersion,
                document.Name,
                document.Source.ContentSha256!,
                labeledResult.Samples.Select(sample =>
                    new ToolRecipeValidationSampleDefinition(
                        sample.Order,
                        sample.SourcePath,
                        sample.Role)).ToArray());
            ToolRecipeValidationSetDefinitionStore.SaveForRecipe(
                recipePath,
                roleDefinition);
            var reopenedRoles =
                ToolRecipeValidationSetDefinitionStore.LoadForRecipe(recipePath);
            Check(
                "role manifest save and reopen preserves paths and roles",
                reopenedRoles?.Samples.Select(sample => sample.Role)
                    .SequenceEqual(roleDefinition.Samples.Select(sample => sample.Role))
                == true
                && reopenedRoles.Samples.All(sample =>
                    Path.IsPathFullyQualified(sample.SourcePath)),
                ToolRecipeValidationSetDefinitionStore.GetPathForRecipe(recipePath));

            var thresholdGoodLowPath =
                Path.Combine(artifactRoot, "threshold-good-low.C3D");
            var thresholdGoodHighPath =
                Path.Combine(artifactRoot, "threshold-good-high.C3D");
            var thresholdBadLowPath =
                Path.Combine(artifactRoot, "threshold-bad-low.C3D");
            var thresholdBadHighPath =
                Path.Combine(artifactRoot, "threshold-bad-high.C3D");
            var thresholdHeldOutPath =
                Path.Combine(artifactRoot, "threshold-held-out.C3D");
            CreateThicknessFixture(thresholdGoodLowPath, 2);
            CreateThicknessFixture(thresholdGoodHighPath, 4);
            CreateThicknessFixture(thresholdBadLowPath, -10);
            CreateThicknessFixture(thresholdBadHighPath, 20);
            CreateThicknessFixture(thresholdHeldOutPath, 3);
            var thresholdResult = ToolRecipeValidationSetExecution.Execute(
                document,
                [
                    new ToolRecipeValidationSampleInput(
                        thresholdGoodLowPath,
                        ToolRecipeValidationSampleRole.Good),
                    new ToolRecipeValidationSampleInput(
                        thresholdGoodHighPath,
                        ToolRecipeValidationSampleRole.Good),
                    new ToolRecipeValidationSampleInput(
                        thresholdBadLowPath,
                        ToolRecipeValidationSampleRole.Bad),
                    new ToolRecipeValidationSampleInput(
                        thresholdBadHighPath,
                        ToolRecipeValidationSampleRole.Bad),
                    new ToolRecipeValidationSampleInput(
                        thresholdHeldOutPath,
                        ToolRecipeValidationSampleRole.HeldOut)
                ]);
            Check(
                "threshold fixture executes two Good two Bad and one Held-out sample",
                thresholdResult.Samples.Count == 5
                && thresholdResult.Samples.Count(sample =>
                    sample.Role == ToolRecipeValidationSampleRole.Good
                    && sample.Status == ResultStatus.Pass) == 2
                && thresholdResult.Samples.Count(sample =>
                    sample.Role == ToolRecipeValidationSampleRole.Bad
                    && sample.Status == ResultStatus.Fail) == 2
                && thresholdResult.Samples.Count(sample =>
                    sample.Role == ToolRecipeValidationSampleRole.HeldOut
                    && sample.Status == ResultStatus.Pass) == 1,
                string.Join(
                    ",",
                    thresholdResult.Samples.Select(sample =>
                        $"{sample.Role}:{sample.Status}")));
            var thresholdReport =
                ToolRecipeThresholdCandidateAnalyzer.Analyze(
                    document,
                    thresholdResult);
            var thresholdHeldOutIdentity = thresholdResult.Samples.Single(
                sample =>
                    sample.Role == ToolRecipeValidationSampleRole.HeldOut)
                .SourceContentSha256;
            var thresholdRecipePath = Path.Combine(
                artifactRoot,
                "threshold-candidate-fixture.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(thresholdRecipePath, document);
            var staleThresholdCorrectionPath =
                ToolRecipeThresholdCorrectionEvidenceStore.GetPathForRecipe(
                    thresholdRecipePath);
            if (File.Exists(staleThresholdCorrectionPath))
            {
                File.Delete(staleThresholdCorrectionPath);
            }
            ToolRecipeValidationSetDefinitionStore.SaveForRecipe(
                thresholdRecipePath,
                new ToolRecipeValidationSetDefinition(
                    ToolRecipeValidationSetDefinition.CurrentSchemaVersion,
                    document.Name,
                    document.Source.ContentSha256!,
                    thresholdResult.Samples.Select(sample =>
                        new ToolRecipeValidationSampleDefinition(
                            sample.Order,
                            sample.SourcePath,
                            sample.Role)).ToArray()));
            Check(
                "candidate report uses four development samples and excludes one Held-out",
                thresholdReport.Status == ResultStatus.Pass
                && thresholdReport.DevelopmentSampleCount == 4
                && thresholdReport.HeldOutSampleCount == 1
                && thresholdReport.Candidates.Count > 0
                && thresholdReport.EvidenceWarnings.Count == 0,
                thresholdReport.Message);
            var missingBadResult = ToolRecipeValidationSetExecution.Execute(
                document,
                [
                    new ToolRecipeValidationSampleInput(
                        thresholdGoodLowPath,
                        ToolRecipeValidationSampleRole.Good),
                    new ToolRecipeValidationSampleInput(
                        thresholdHeldOutPath,
                        ToolRecipeValidationSampleRole.HeldOut)
                ]);
            var missingBadReport =
                ToolRecipeThresholdCandidateAnalyzer.Analyze(
                    document,
                    missingBadResult);
            Check(
                "missing class and insufficient repeat evidence emit typed exact-identity warnings",
                missingBadReport.Status == ResultStatus.Warning
                && missingBadReport.Candidates.Count == 0
                && missingBadReport.EvidenceWarnings.Any(warning =>
                    warning.Kind
                    == ToolRecipeThresholdEvidenceWarningKind.MissingBadSamples)
                && missingBadReport.EvidenceWarnings.Any(warning =>
                    warning.Kind
                    == ToolRecipeThresholdEvidenceWarningKind
                        .InsufficientGoodSamples)
                && missingBadReport.EvidenceWarnings.All(warning =>
                    warning.DevelopmentSampleIdentities.All(identity =>
                        !string.Equals(
                            identity,
                            thresholdHeldOutIdentity,
                            StringComparison.Ordinal))),
                missingBadReport.Message);
            var imbalancedResult = ToolRecipeValidationSetExecution.Execute(
                document,
                [
                    new ToolRecipeValidationSampleInput(
                        thresholdGoodLowPath,
                        ToolRecipeValidationSampleRole.Good),
                    new ToolRecipeValidationSampleInput(
                        thresholdGoodHighPath,
                        ToolRecipeValidationSampleRole.Good),
                    new ToolRecipeValidationSampleInput(
                        thresholdBadLowPath,
                        ToolRecipeValidationSampleRole.Bad)
                ]);
            var imbalancedReport =
                ToolRecipeThresholdCandidateAnalyzer.Analyze(
                    document,
                    imbalancedResult);
            Check(
                "imbalanced class counts are visible without suppressing deterministic candidates",
                imbalancedReport.Status == ResultStatus.Warning
                && imbalancedReport.Candidates.Count > 0
                && imbalancedReport.EvidenceWarnings.Any(warning =>
                    warning.Kind
                    == ToolRecipeThresholdEvidenceWarningKind
                        .ImbalancedSamples
                    && warning.GoodSampleCount == 2
                    && warning.BadSampleCount == 1)
                && imbalancedReport.EvidenceWarnings.Any(warning =>
                    warning.Kind
                    == ToolRecipeThresholdEvidenceWarningKind
                        .InsufficientBadSamples),
                imbalancedReport.Message);
            var overlapBadLowPath =
                Path.Combine(artifactRoot, "threshold-overlap-bad-low.C3D");
            var overlapBadHighPath =
                Path.Combine(artifactRoot, "threshold-overlap-bad-high.C3D");
            CreateThicknessFixture(overlapBadLowPath, 3);
            CreateThicknessFixture(overlapBadHighPath, 5);
            var overlapResult = ToolRecipeValidationSetExecution.Execute(
                document,
                [
                    new ToolRecipeValidationSampleInput(
                        thresholdGoodLowPath,
                        ToolRecipeValidationSampleRole.Good),
                    new ToolRecipeValidationSampleInput(
                        thresholdGoodHighPath,
                        ToolRecipeValidationSampleRole.Good),
                    new ToolRecipeValidationSampleInput(
                        overlapBadLowPath,
                        ToolRecipeValidationSampleRole.Bad),
                    new ToolRecipeValidationSampleInput(
                        overlapBadHighPath,
                        ToolRecipeValidationSampleRole.Bad)
                ]);
            var overlapReport =
                ToolRecipeThresholdCandidateAnalyzer.Analyze(
                    document,
                    overlapResult);
            Check(
                "inseparable Good Bad distributions emit a typed overlap warning",
                overlapReport.Status == ResultStatus.Warning
                && overlapReport.EvidenceWarnings.Any(warning =>
                    warning.Scope == ToolRecipeEvidenceScope.StepMetric
                    && warning.OwnerId == step.Id
                    && warning.MetricName == "Mean"
                    && warning.Kind
                    == ToolRecipeThresholdEvidenceWarningKind
                        .OverlappingDistributions),
                overlapReport.Message);
            var meanCandidates = thresholdReport.Candidates.Where(candidate =>
                    candidate.Scope == ToolRecipeEvidenceScope.StepMetric
                    && candidate.OwnerId == step.Id
                    && candidate.MetricName == "Mean")
                .ToArray();
            var rangeCandidate = meanCandidates.Single(candidate =>
                candidate.LimitKind
                == ToolRecipeThresholdLimitKind.Range);
            Check(
                "two-limit range candidate separates both Bad tails",
                Math.Abs(rangeCandidate.Minimum!.Value - 2) < 1e-9
                && Math.Abs(rangeCandidate.Maximum!.Value - 4) < 1e-9
                && rangeCandidate.GoodAcceptedCount == 2
                && rangeCandidate.BadRejectedCount == 2
                && rangeCandidate.ErrorCount == 0,
                $"{rangeCandidate.LimitsForVerification()} | correct={rangeCandidate.CorrectCount};errors={rangeCandidate.ErrorCount}");
            Check(
                "one-limit candidates expose the unavoidable opposite-tail error",
                meanCandidates.Where(candidate =>
                        candidate.LimitKind
                        is ToolRecipeThresholdLimitKind.Minimum
                        or ToolRecipeThresholdLimitKind.Maximum)
                    .All(candidate =>
                        candidate.ErrorCount == 1
                        && candidate.BadAcceptedCount == 1),
                string.Join(
                    ";",
                    meanCandidates.Where(candidate =>
                            candidate.LimitKind
                            is ToolRecipeThresholdLimitKind.Minimum
                            or ToolRecipeThresholdLimitKind.Maximum)
                        .Select(candidate =>
                        $"{candidate.LimitKind}:{candidate.ErrorCount}/{candidate.BadAcceptedCount}")));
            Check(
                "Held-out identity is recorded as excluded and never enters a candidate decision",
                thresholdReport.HeldOutSampleIdentities.SequenceEqual(
                    [$"5:{thresholdHeldOutIdentity}"])
                && thresholdReport.Candidates.All(candidate =>
                    candidate.Decisions.All(decision =>
                        decision.ExpectedRole
                        != ToolRecipeValidationSampleRole.HeldOut)),
                $"{string.Join(",", thresholdReport.HeldOutSampleIdentities)};heldOutDecision={thresholdReport.Candidates.Any(candidate => candidate.Decisions.Any(decision => decision.ExpectedRole == ToolRecipeValidationSampleRole.HeldOut))}");
            var developmentIdentities = thresholdResult.Samples
                .Where(sample =>
                    sample.Role != ToolRecipeValidationSampleRole.HeldOut)
                .Select(sample => sample.SourceContentSha256)
                .OrderBy(identity => identity, StringComparer.Ordinal)
                .ToArray();
            Check(
                "candidate error table carries every exact development sample ID",
                rangeCandidate.Decisions.Select(decision =>
                        decision.SampleIdentity)
                    .OrderBy(identity => identity, StringComparer.Ordinal)
                    .SequenceEqual(developmentIdentities),
                string.Join(
                    ",",
                    rangeCandidate.Decisions.Select(decision =>
                        $"{decision.SampleOrder}:{decision.Decision}:{decision.SampleIdentity[..12]}")));
            Check(
                "confusion counts reproduce from raw decisions",
                rangeCandidate.GoodAcceptedCount
                    == rangeCandidate.Decisions.Count(decision =>
                        decision.Decision
                        == ToolRecipeThresholdDecisionKind.CorrectGood)
                && rangeCandidate.GoodRejectedCount
                    == rangeCandidate.Decisions.Count(decision =>
                        decision.Decision
                        == ToolRecipeThresholdDecisionKind.FalseReject)
                && rangeCandidate.BadRejectedCount
                    == rangeCandidate.Decisions.Count(decision =>
                        decision.Decision
                        == ToolRecipeThresholdDecisionKind.CorrectBad)
                && rangeCandidate.BadAcceptedCount
                    == rangeCandidate.Decisions.Count(decision =>
                        decision.Decision
                        == ToolRecipeThresholdDecisionKind.FalseAccept),
                $"GA={rangeCandidate.GoodAcceptedCount};GR={rangeCandidate.GoodRejectedCount};BR={rangeCandidate.BadRejectedCount};BA={rangeCandidate.BadAcceptedCount}");
            var repeatedThresholdReport =
                ToolRecipeThresholdCandidateAnalyzer.Analyze(
                    document,
                    thresholdResult);
            Check(
                "candidate IDs and ordering are deterministic",
                thresholdReport.Candidates.Select(candidate =>
                        candidate.CandidateId)
                    .SequenceEqual(
                        repeatedThresholdReport.Candidates.Select(candidate =>
                            candidate.CandidateId)),
                $"{thresholdReport.Candidates.Count} candidate(s)");

            Check(
                "range candidate maps to exact typed Thickness parameters",
                ToolRecipeThresholdCandidateParameterMapper.TryCreateProposal(
                    document,
                    rangeCandidate,
                    out var thresholdProposal,
                    out var thresholdMappingMessage)
                && thresholdProposal is
                {
                    Changes.Count: 2
                }
                && thresholdProposal.Changes.Select(change =>
                        change.ParameterName)
                    .SequenceEqual(
                    [
                        "MinimumThickness",
                        "MaximumThickness"
                    ])
                && thresholdProposal.Changes.Select(change =>
                        change.ProposedValue)
                    .SequenceEqual(["2", "4"]),
                thresholdMappingMessage);
            if (thresholdProposal is null)
            {
                throw new InvalidDataException(
                    "Controlled range candidate did not produce a proposal.");
            }
            var projectedThresholdDocument =
                ToolRecipeThresholdCandidateParameterMapper.ApplyProposal(
                    document,
                    thresholdProposal);
            Check(
                "proposal projection changes a copy and preserves the authored document",
                document.Steps.Single().Parameters.Single(parameter =>
                    parameter.Name == "MinimumThickness").Value == "0"
                && document.Steps.Single().Parameters.Single(parameter =>
                    parameter.Name == "MaximumThickness").Value == "10"
                && projectedThresholdDocument.Steps.Single().Parameters.Single(
                    parameter =>
                        parameter.Name == "MinimumThickness").Value == "2"
                && projectedThresholdDocument.Steps.Single().Parameters.Single(
                    parameter =>
                        parameter.Name == "MaximumThickness").Value == "4",
                $"original={document.Steps.Single().Parameters[0].Value}..{document.Steps.Single().Parameters[1].Value};projected={projectedThresholdDocument.Steps.Single().Parameters[0].Value}..{projectedThresholdDocument.Steps.Single().Parameters[1].Value}");
            var regionCandidate = thresholdReport.Candidates.First(candidate =>
                candidate.Scope == ToolRecipeEvidenceScope.RegionMetric);
            Check(
                "unmapped region candidate fails closed",
                !ToolRecipeThresholdCandidateParameterMapper.TryCreateProposal(
                    document,
                    regionCandidate,
                    out _,
                    out var unmappedRegionMessage),
                unmappedRegionMessage);
            Check(
                "threshold assistant publishes the exact Thickness Warpage and Completeness coverage matrix",
                ToolRecipeThresholdCandidateParameterMapper.SupportedMappings
                    .Select(mapping =>
                        $"{mapping.ToolId}|{mapping.MetricName}|{mapping.LimitKind}|{string.Join(",", mapping.ParameterNames)}")
                    .SequenceEqual(
                    [
                        "thickness|Mean|Minimum|MinimumThickness",
                        "thickness|Mean|Maximum|MaximumThickness",
                        "thickness|Mean|Range|MinimumThickness,MaximumThickness",
                        "warpage|PeakToValley|Maximum|MaximumPeakToValley",
                        "warpage|Rms|Maximum|MaximumRms",
                        "completeness-grid|Minimum finite coverage|Minimum|MinimumFiniteCoverageRatio",
                        "completeness-grid|Minimum reference-relative mean|Minimum|MinimumReferenceRelativeMeanRawHeight",
                        "completeness-grid|Maximum reference-relative mean|Maximum|MaximumReferenceRelativeMeanRawHeight"
                    ]),
                string.Join(
                    ";",
                    ToolRecipeThresholdCandidateParameterMapper
                        .SupportedMappings.Select(mapping =>
                            $"{mapping.ToolId}/{mapping.MetricName}/{mapping.LimitKind}")));
            var warpageStep = new ToolRecipeStep(
                "step.validation.warpage",
                "warpage",
                "Warpage",
                2,
                [source.Id, referenceSelection.Id],
                "result.validation.warpage",
                [
                    new ToolRecipeParameter("MaximumPeakToValley", "100"),
                    new ToolRecipeParameter("MaximumRms", "100"),
                    new ToolRecipeParameter("MinimumValidSampleCount", "3")
                ]);
            var warpageDocument = document with { Steps = [warpageStep] };
            var warpagePeakCandidate = CreateMappingCandidate(
                warpageStep,
                "PeakToValley",
                ToolRecipeThresholdLimitKind.Maximum,
                null,
                5);
            var warpageRmsCandidate = CreateMappingCandidate(
                warpageStep,
                "Rms",
                ToolRecipeThresholdLimitKind.Maximum,
                null,
                2);
            var warpagePeakMapped =
                ToolRecipeThresholdCandidateParameterMapper.TryCreateProposal(
                    warpageDocument,
                    warpagePeakCandidate,
                    out var warpagePeakProposal,
                    out var warpagePeakMessage);
            var warpageRmsMapped =
                ToolRecipeThresholdCandidateParameterMapper.TryCreateProposal(
                    warpageDocument,
                    warpageRmsCandidate,
                    out var warpageRmsProposal,
                    out var warpageRmsMessage);
            Check(
                "Warpage PeakToValley and Rms maximum candidates map to typed parameters",
                warpagePeakMapped
                && warpagePeakProposal?.Changes.Single() is
                {
                    ParameterName: "MaximumPeakToValley",
                    ProposedValue: "5"
                }
                && warpageRmsMapped
                && warpageRmsProposal?.Changes.Single() is
                {
                    ParameterName: "MaximumRms",
                    ProposedValue: "2"
                },
                $"{warpagePeakMessage} | {warpageRmsMessage}");
            var unsupportedWarpageMinimum = CreateMappingCandidate(
                warpageStep,
                "PeakToValley",
                ToolRecipeThresholdLimitKind.Minimum,
                1,
                null);
            Check(
                "unsupported Warpage minimum mapping fails closed",
                !ToolRecipeThresholdCandidateParameterMapper.TryCreateProposal(
                    warpageDocument,
                    unsupportedWarpageMinimum,
                    out _,
                    out var unsupportedWarpageMessage),
                unsupportedWarpageMessage);

            var completenessFixture =
                CompletenessValidationVerificationFixtureFactory.Create(
                    artifactRoot);
            var completenessDocument = completenessFixture.Document;
            var completenessStep = completenessDocument.Steps.Single();
            var completenessResult = ToolRecipeValidationSetExecution.Execute(
                completenessDocument,
                completenessFixture.Samples);
            Check(
                "Completeness fixture replays two Good two Bad and one Held-out with real Pass Fail evidence",
                completenessResult.Samples.Count == 5
                && completenessResult.Samples.Count(sample =>
                    sample.Role == ToolRecipeValidationSampleRole.Good
                    && sample.Status == ResultStatus.Pass) == 2
                && completenessResult.Samples.Count(sample =>
                    sample.Role == ToolRecipeValidationSampleRole.Bad
                    && sample.Status == ResultStatus.Fail) == 2
                && completenessResult.Samples.Count(sample =>
                    sample.Role == ToolRecipeValidationSampleRole.HeldOut
                    && sample.Status == ResultStatus.Pass) == 1,
                string.Join(
                    ",",
                    completenessResult.Samples.Select(sample =>
                        $"{sample.Role}:{sample.Status}")));
            var completenessObservations =
                ToolRecipeLabeledEvidenceAnalyzer.CollectObservations(
                    completenessDocument,
                    completenessResult);
            var completenessPolicyObservations = completenessObservations
                .Where(observation =>
                    observation.OwnerId == completenessStep.Id
                    && observation.MetricName is
                        C3DCompletenessMetricNames.MinimumFiniteCoverage
                        or C3DCompletenessMetricNames
                            .MinimumReferenceRelativeMean
                        or C3DCompletenessMetricNames
                            .MaximumReferenceRelativeMean)
                .ToArray();
            Check(
                "Completeness policy observations preserve one worst-cell locator per sample and bound",
                completenessPolicyObservations.Length == 15
                && completenessPolicyObservations.All(observation =>
                    C3DCompletenessMetricNames.TryGetCellId(
                        $"{observation.EvidenceLocator} "
                        + C3DCompletenessMetricNames.FiniteCoverageSuffix,
                        C3DCompletenessMetricNames.FiniteCoverageSuffix,
                        out _)),
                string.Join(
                    ";",
                    completenessPolicyObservations.Select(observation =>
                        $"{observation.SampleOrder}:{observation.MetricName}:{observation.Value:R}@{observation.EvidenceLocator}")));
            var completenessThresholdReport =
                ToolRecipeThresholdCandidateAnalyzer.Analyze(
                    completenessDocument,
                    completenessResult);
            var completenessSupportedCandidates =
                completenessThresholdReport.Candidates
                    .Where(candidate =>
                        ToolRecipeThresholdCandidateParameterMapper
                            .SupportedMappings.Any(mapping =>
                                mapping.ToolId == completenessStep.ToolId
                                && mapping.MetricName == candidate.MetricName
                                && mapping.LimitKind == candidate.LimitKind))
                    .ToArray();
            Check(
                "Completeness assistant produces three exact zero-error supported candidates without Held-out leakage",
                completenessThresholdReport.ContractVersion == "2.1"
                && completenessThresholdReport.DevelopmentSampleCount == 4
                && completenessThresholdReport.HeldOutSampleCount == 1
                && completenessSupportedCandidates.Length == 3
                && completenessSupportedCandidates.All(candidate =>
                    candidate.ErrorCount == 0
                    && candidate.Decisions.Count == 4
                    && candidate.Decisions.All(decision =>
                        !string.IsNullOrWhiteSpace(
                            decision.EvidenceLocator)
                        && decision.ExpectedRole
                            != ToolRecipeValidationSampleRole.HeldOut)),
                string.Join(
                    ";",
                    completenessSupportedCandidates.Select(candidate =>
                        $"{candidate.MetricName}/{candidate.LimitKind}/{candidate.LimitsForVerification()}/errors={candidate.ErrorCount}")));
            var completenessProposals =
                completenessSupportedCandidates.Select(candidate =>
                {
                    var mapped =
                        ToolRecipeThresholdCandidateParameterMapper
                            .TryCreateProposal(
                                completenessDocument,
                                candidate,
                                out var proposal,
                                out var mapMessage);
                    return (Candidate: candidate, Mapped: mapped,
                        Proposal: proposal, Message: mapMessage);
                }).ToArray();
            Check(
                "Completeness candidates map only to the three authored typed policy parameters",
                completenessProposals.All(item =>
                    item.Mapped
                    && item.Proposal?.Changes.Count == 1)
                && completenessProposals.Select(item =>
                        item.Proposal!.Changes.Single().ParameterName)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .SequenceEqual(
                    [
                        "MaximumReferenceRelativeMeanRawHeight",
                        "MinimumFiniteCoverageRatio",
                        "MinimumReferenceRelativeMeanRawHeight"
                    ]),
                string.Join(
                    ";",
                    completenessProposals.Select(item =>
                        item.Message)));
            var completenessRecipePath = completenessFixture.RecipePath;
            var completenessWorkbench = new ToolWorkbenchViewModel(
                Path.Combine(
                    artifactRoot,
                    "recent-completeness-threshold.json"));
            var completenessCoverageCandidate =
                completenessSupportedCandidates.Single(candidate =>
                    candidate.MetricName
                    == C3DCompletenessMetricNames.MinimumFiniteCoverage);
            Check(
                "Completeness Workbench reopens the same labeled set and candidate",
                completenessWorkbench.TryOpenTeachingRecipe(
                    completenessRecipePath,
                    out var completenessOpenMessage)
                && RunThresholdWorkbench(
                    completenessWorkbench,
                    completenessCoverageCandidate.CandidateId),
                completenessOpenMessage);
            completenessWorkbench.SelectedValidationThresholdCandidate =
                completenessWorkbench.ValidationThresholdCandidates.Single(
                    candidate =>
                        candidate.CandidateId
                        == completenessCoverageCandidate.CandidateId);
            var completenessBeforeReview =
                completenessWorkbench.ValidationSetSummary;
            completenessWorkbench.ReviewValidationThresholdCandidateCommand
                .Execute(null);
            completenessWorkbench.CancelValidationThresholdReviewCommand
                .Execute(null);
            Check(
                "Completeness Review and Cancel leave recipe draft and execution unchanged",
                !completenessWorkbench.IsValidationThresholdReviewActive
                && !completenessWorkbench.HasPendingStepParameterChanges
                && !completenessWorkbench.IsDirty
                && completenessWorkbench.ValidationSetSummary
                    == completenessBeforeReview,
                completenessWorkbench.ValidationThresholdCorrectionSummary);
            completenessWorkbench.ReviewValidationThresholdCandidateCommand
                .Execute(null);
            completenessWorkbench.ApplyValidationThresholdCandidateCommand
                .Execute(null);
            Check(
                "Completeness Apply changes only the mapped PropertyGrid draft",
                completenessWorkbench.IsValidationThresholdCandidateApplied
                && completenessWorkbench.HasPendingStepParameterChanges
                && completenessWorkbench.SelectedStepPropertyDraft
                    is CompletenessGridStepProperties
                    {
                        MinimumFiniteCoverageRatio: 1,
                        MinimumReferenceRelativeMeanRawHeight: 0,
                        MaximumReferenceRelativeMeanRawHeight: 6
                    }
                && completenessWorkbench.PipelineSteps.Single()
                    .Parameters.Single(parameter =>
                        parameter.Name == "MinimumFiniteCoverageRatio").Value
                    != "1"
                && completenessWorkbench.ValidationSetSummary
                    == completenessBeforeReview,
                completenessWorkbench.StepParameterEditStatus);
            Check(
                "Completeness candidate locks Held-out until explicit development replay",
                completenessWorkbench
                    .RevalidateValidationThresholdCorrectionCommand.CanExecute(
                        null)
                && !completenessWorkbench
                    .ReplayValidationThresholdHeldOutCommand.CanExecute(null),
                completenessWorkbench.ValidationThresholdCorrectionSummary);
            completenessWorkbench
                .RevalidateValidationThresholdCorrectionAsync()
                .GetAwaiter().GetResult();
            Check(
                "Completeness development replay validates the projected candidate before Held-out",
                completenessWorkbench
                    .IsValidationThresholdDevelopmentValidated
                && completenessWorkbench
                    .ValidationThresholdDevelopmentSamples.Count == 8
                && completenessWorkbench
                    .ValidationThresholdDevelopmentSamples.All(sample =>
                        sample.Stage != "After"
                        || sample.ExpectedMatch == "Match")
                && completenessWorkbench
                    .ReplayValidationThresholdHeldOutCommand.CanExecute(null)
                && completenessWorkbench.ValidationThresholdHeldOutSamples
                    .Count == 0,
                completenessWorkbench.ValidationThresholdCorrectionSummary);
            completenessWorkbench.ReplayValidationThresholdHeldOutAsync()
                .GetAwaiter().GetResult();
            Check(
                "Completeness Held-out replay stays separate and carries exact sample identity",
                completenessWorkbench.ValidationThresholdHeldOutSamples.Count
                    == 1
                && completenessWorkbench.ValidationThresholdHeldOutSamples
                    .Single().SampleIdentity
                    == completenessResult.Samples.Single(sample =>
                        sample.Role
                        == ToolRecipeValidationSampleRole.HeldOut)
                        .SourceContentSha256
                && completenessWorkbench
                    .IsValidationThresholdDevelopmentValidated
                && completenessWorkbench
                    .ValidationThresholdDevelopmentSamples.Count == 8
                && completenessWorkbench
                    .SelectedValidationThresholdDecisions.All(
                    decision =>
                        !string.IsNullOrWhiteSpace(
                            decision.EvidenceLocator)),
                completenessWorkbench.ValidationThresholdCorrectionSummary);

            var projectedHeldOutResult =
                ToolRecipeValidationSetExecution.Execute(
                    projectedThresholdDocument,
                    [
                        new ToolRecipeValidationSampleInput(
                            thresholdHeldOutPath,
                            ToolRecipeValidationSampleRole.HeldOut)
                    ]);
            var thresholdCorrectionEvidence =
                ToolRecipeThresholdCorrectionEvidenceBuilder.Build(
                    projectedThresholdDocument,
                    thresholdProposal,
                    projectedHeldOutResult);
            Check(
                "projected replay executes Held-out only and records no development decision",
                projectedHeldOutResult.Samples.Count == 1
                && projectedHeldOutResult.Samples.Single().Role
                    == ToolRecipeValidationSampleRole.HeldOut
                && thresholdCorrectionEvidence.HeldOutSamples.Count == 1
                && thresholdCorrectionEvidence.Proposal.Candidate.Decisions
                    .All(decision =>
                        decision.ExpectedRole
                        != ToolRecipeValidationSampleRole.HeldOut),
                $"heldOut={thresholdCorrectionEvidence.HeldOutSamples.Count};developmentDecisions={thresholdCorrectionEvidence.Proposal.Candidate.Decisions.Count}");
            var thresholdEvidenceStoreRecipe = Path.Combine(
                artifactRoot,
                "threshold-evidence-store.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(
                thresholdEvidenceStoreRecipe,
                projectedThresholdDocument);
            ToolRecipeThresholdCorrectionEvidenceStore.SaveForRecipe(
                thresholdEvidenceStoreRecipe,
                thresholdCorrectionEvidence);
            var reopenedThresholdEvidence =
                ToolRecipeThresholdCorrectionEvidenceStore.LoadForRecipe(
                    thresholdEvidenceStoreRecipe);
            Check(
                "threshold correction evidence sidecar round-trips portable exact identities",
                reopenedThresholdEvidence is not null
                && reopenedThresholdEvidence.Proposal.CandidateId
                    == thresholdProposal.CandidateId
                && reopenedThresholdEvidence.HeldOutSamples.Single()
                    .SampleIdentity == thresholdHeldOutIdentity
                && Path.IsPathFullyQualified(
                    reopenedThresholdEvidence.HeldOutSamples.Single()
                        .SourcePath),
                ToolRecipeThresholdCorrectionEvidenceStore.GetPathForRecipe(
                    thresholdEvidenceStoreRecipe));

            var thresholdWorkbench = new ToolWorkbenchViewModel(
                Path.Combine(
                    artifactRoot,
                    "recent-threshold-review.json"));
            Check(
                "threshold Workbench opens labeled fixture and regenerates the mapped candidate",
                thresholdWorkbench.TryOpenTeachingRecipe(
                    thresholdRecipePath,
                    out var thresholdWorkbenchOpenMessage)
                && RunThresholdWorkbench(
                    thresholdWorkbench,
                    rangeCandidate.CandidateId),
                thresholdWorkbenchOpenMessage);
            var thresholdWorkbenchCandidate =
                thresholdWorkbench.ValidationThresholdCandidates.Single(
                    candidate =>
                        candidate.CandidateId == rangeCandidate.CandidateId);
            thresholdWorkbench.SelectedValidationThresholdCandidate =
                thresholdWorkbenchCandidate;
            var beforeReviewDirty = thresholdWorkbench.IsDirty;
            var beforeReviewSummary =
                thresholdWorkbench.ValidationSetSummary;
            var beforeReviewMinimum = thresholdWorkbench.PipelineSteps.Single()
                .Parameters.Single(parameter =>
                    parameter.Name == "MinimumThickness").Value;
            thresholdWorkbench.ReviewValidationThresholdCandidateCommand
                .Execute(null);
            Check(
                "Review exposes typed before/proposed values without mutating recipe or execution",
                thresholdWorkbench.IsValidationThresholdReviewActive
                && thresholdWorkbench.ValidationThresholdParameterChanges.Count
                    == 2
                && thresholdWorkbench.IsDirty == beforeReviewDirty
                && !thresholdWorkbench.HasPendingStepParameterChanges
                && thresholdWorkbench.PipelineSteps.Single()
                    .Parameters.Single(parameter =>
                        parameter.Name == "MinimumThickness").Value
                    == beforeReviewMinimum
                && thresholdWorkbench.ValidationSetSummary
                    == beforeReviewSummary,
                thresholdWorkbench.ValidationThresholdCorrectionSummary);
            thresholdWorkbench.CancelValidationThresholdReviewCommand
                .Execute(null);
            Check(
                "Cancel closes Review and preserves recipe PropertyGrid and execution state",
                !thresholdWorkbench.IsValidationThresholdReviewActive
                && !thresholdWorkbench
                    .IsValidationThresholdCandidateApplied
                && !thresholdWorkbench.HasPendingStepParameterChanges
                && thresholdWorkbench.IsDirty == beforeReviewDirty
                && thresholdWorkbench.ValidationSetSummary
                    == beforeReviewSummary,
                thresholdWorkbench.ValidationThresholdCorrectionSummary);

            thresholdWorkbench.ReviewValidationThresholdCandidateCommand
                .Execute(null);
            thresholdWorkbench.ApplyValidationThresholdCandidateCommand
                .Execute(null);
            Check(
                "candidate Apply changes the typed PropertyGrid draft only",
                thresholdWorkbench.IsValidationThresholdCandidateApplied
                && thresholdWorkbench.HasPendingStepParameterChanges
                && thresholdWorkbench.SelectedStepPropertyDraft
                    is ThicknessStepProperties
                    {
                        MinimumThickness: 2,
                        MaximumThickness: 4
                    }
                && thresholdWorkbench.PipelineSteps.Single()
                    .Parameters.Single(parameter =>
                        parameter.Name == "MinimumThickness").Value == "0"
                && thresholdWorkbench.PipelineSteps.Single()
                    .Parameters.Single(parameter =>
                        parameter.Name == "MaximumThickness").Value == "10"
                && thresholdWorkbench.IsDirty == beforeReviewDirty,
                thresholdWorkbench.StepParameterEditStatus);
            thresholdWorkbench.ReplayValidationThresholdHeldOutAsync()
                .GetAwaiter().GetResult();
            Check(
                "explicit Held-out replay uses the projected draft and persists separate evidence",
                thresholdWorkbench.ValidationThresholdHeldOutSamples.Count == 1
                && thresholdWorkbench.ValidationThresholdHeldOutSamples.Single()
                    .SampleIdentity == thresholdHeldOutIdentity
                && thresholdWorkbench.ValidationSetSummary
                    == beforeReviewSummary
                && File.Exists(
                    ToolRecipeThresholdCorrectionEvidenceStore.GetPathForRecipe(
                        thresholdRecipePath)),
                thresholdWorkbench.ValidationThresholdCorrectionSummary);
            Check(
                "normal PropertyGrid Apply remains separate and does not execute",
                thresholdWorkbench.TryApplySelectedStepParameterDraft(
                    out var thresholdDraftApplyMessage)
                && !thresholdWorkbench.HasPendingStepParameterChanges
                && thresholdWorkbench.IsDirty
                && thresholdWorkbench.PipelineSteps.Single()
                    .Parameters.Single(parameter =>
                        parameter.Name == "MinimumThickness").Value == "2"
                && thresholdWorkbench.PipelineSteps.Single()
                    .Parameters.Single(parameter =>
                        parameter.Name == "MaximumThickness").Value == "4"
                && thresholdWorkbench.ValidationSetSummary
                    == beforeReviewSummary,
                thresholdDraftApplyMessage);
            var thresholdAppliedRecipePath = Path.Combine(
                artifactRoot,
                "threshold-applied.ov3d-recipe.json");
            Check(
                "saved threshold recipe owns both role and correction sidecars",
                thresholdWorkbench.TrySaveTeachingRecipe(
                    thresholdAppliedRecipePath,
                    out var thresholdSaveMessage)
                && File.Exists(
                    ToolRecipeValidationSetDefinitionStore.GetPathForRecipe(
                        thresholdAppliedRecipePath))
                && File.Exists(
                    ToolRecipeThresholdCorrectionEvidenceStore.GetPathForRecipe(
                        thresholdAppliedRecipePath)),
                thresholdSaveMessage);
            var reopenedThresholdWorkbench = new ToolWorkbenchViewModel(
                Path.Combine(
                    artifactRoot,
                    "recent-threshold-reopened.json"));
            Check(
                "reopen restores committed limits and durable Held-out evidence without execution",
                reopenedThresholdWorkbench.TryOpenTeachingRecipe(
                    thresholdAppliedRecipePath,
                    out var thresholdReopenMessage)
                && reopenedThresholdWorkbench.PipelineSteps.Single()
                    .Parameters.Single(parameter =>
                        parameter.Name == "MinimumThickness").Value == "2"
                && reopenedThresholdWorkbench.PipelineSteps.Single()
                    .Parameters.Single(parameter =>
                        parameter.Name == "MaximumThickness").Value == "4"
                && reopenedThresholdWorkbench
                    .ValidationThresholdHeldOutSamples.Count == 1
                && reopenedThresholdWorkbench.ValidationSetSamples.All(
                    sample => sample.Status == "Pending"),
                thresholdReopenMessage);

            var failedDraftDocument = document with
            {
                Name = "Threshold manual correction fixture",
                Steps =
                [
                    document.Steps.Single() with
                    {
                        Parameters = document.Steps.Single().Parameters.Select(
                            parameter => parameter.Name == "MaximumThickness"
                                ? parameter with { Value = "20" }
                                : parameter).ToArray()
                    }
                ]
            };
            var failedDraftRecipePath = Path.Combine(
                artifactRoot,
                "threshold-manual-correction-fixture.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(
                failedDraftRecipePath,
                failedDraftDocument);
            ToolRecipeValidationSetDefinitionStore.SaveForRecipe(
                failedDraftRecipePath,
                new ToolRecipeValidationSetDefinition(
                    ToolRecipeValidationSetDefinition.CurrentSchemaVersion,
                    failedDraftDocument.Name,
                    failedDraftDocument.Source.ContentSha256!,
                    thresholdResult.Samples.Select(sample =>
                        new ToolRecipeValidationSampleDefinition(
                            sample.Order,
                            sample.SourcePath,
                            sample.Role)).ToArray()));
            var manualWorkbench = new ToolWorkbenchViewModel(
                Path.Combine(
                    artifactRoot,
                    "recent-threshold-manual-correction.json"));
            Check(
                "failed threshold draft fixture preserves one genuine false accept before correction",
                manualWorkbench.TryOpenTeachingRecipe(
                    failedDraftRecipePath,
                    out var manualOpenMessage)
                && RunThresholdWorkbench(
                    manualWorkbench,
                    rangeCandidate.CandidateId)
                && manualWorkbench.ValidationSetSamples.Count(sample =>
                    sample.Role == ToolRecipeValidationSampleRole.Bad
                    && sample.Status == "Pass") == 1,
                manualOpenMessage);
            manualWorkbench.SelectedValidationThresholdCandidate =
                manualWorkbench.ValidationThresholdCandidates.Single(
                    candidate =>
                        candidate.CandidateId == rangeCandidate.CandidateId);
            var manualBeforeSummary = manualWorkbench.ValidationSetSummary;
            manualWorkbench.ReviewValidationThresholdCandidateCommand.Execute(
                null);
            manualWorkbench.ApplyValidationThresholdCandidateCommand.Execute(
                null);
            var manualDraft =
                (ThicknessStepProperties)manualWorkbench
                    .SelectedStepPropertyDraft!;
            manualDraft.MinimumThickness = 1.5;
            manualDraft.MaximumThickness = 4.5;
            manualWorkbench.MarkSelectedStepParameterDraftDirty();
            Check(
                "operator changes the suggested draft and commits through ordinary PropertyGrid Apply",
                manualWorkbench.TryApplySelectedStepParameterDraft(
                    out var manualApplyMessage)
                && manualWorkbench
                    .IsValidationThresholdManualCorrectionCommitted
                && !manualWorkbench
                    .IsValidationThresholdDevelopmentValidated
                && manualWorkbench.PipelineSteps.Single().Parameters.Single(
                    parameter =>
                        parameter.Name == "MinimumThickness").Value == "1.5"
                && manualWorkbench.PipelineSteps.Single().Parameters.Single(
                    parameter =>
                        parameter.Name == "MaximumThickness").Value == "4.5"
                && manualWorkbench.ValidationSetSummary
                    == manualBeforeSummary
                && manualWorkbench
                    .ValidationThresholdDevelopmentSamples.Count == 0
                && manualWorkbench.ValidationThresholdHeldOutSamples.Count
                    == 0,
                manualApplyMessage);
            Check(
                "manual correction locks Held-out until explicit development revalidation",
                manualWorkbench
                    .RevalidateValidationThresholdCorrectionCommand.CanExecute(
                        null)
                && !manualWorkbench
                    .ReplayValidationThresholdHeldOutCommand.CanExecute(null),
                manualWorkbench.ValidationThresholdCorrectionSummary);
            manualWorkbench.RevalidateValidationThresholdCorrectionAsync()
                .GetAwaiter().GetResult();
            Check(
                "explicit development revalidation records one before mismatch and zero after",
                manualWorkbench.IsValidationThresholdDevelopmentValidated
                && manualWorkbench.ValidationThresholdDevelopmentSamples.Count
                    == 8
                && manualWorkbench.ValidationThresholdDevelopmentSamples.Count(
                    sample =>
                        sample.Stage == "Before"
                        && sample.ExpectedMatch == "Mismatch") == 1
                && manualWorkbench.ValidationThresholdDevelopmentSamples.All(
                    sample =>
                        sample.Stage != "After"
                        || sample.ExpectedMatch == "Match")
                && manualWorkbench.ValidationThresholdHeldOutSamples.Count
                    == 0
                && manualWorkbench.ValidationSetSummary
                    == manualBeforeSummary,
                manualWorkbench.ValidationThresholdCorrectionSummary);
            Check(
                "Held-out replay unlocks only after corrected development evidence passes",
                manualWorkbench.ReplayValidationThresholdHeldOutCommand
                    .CanExecute(null),
                manualWorkbench.ValidationThresholdCorrectionSummary);
            manualWorkbench.ReplayValidationThresholdHeldOutAsync()
                .GetAwaiter().GetResult();
            var manualEvidence =
                ToolRecipeThresholdCorrectionEvidenceStore.LoadForRecipe(
                    failedDraftRecipePath);
            Check(
                "separate Held-out replay persists exact before suggested manual after and Held-out evidence",
                manualEvidence?.ManualCorrection is
                {
                    BeforeMismatchCount: 1,
                    AfterMismatchCount: 0,
                    BeforeDevelopmentSamples.Count: 4,
                    AfterDevelopmentSamples.Count: 4,
                    ParameterChanges.Count: 2
                }
                && manualEvidence.ManualCorrection.ParameterChanges.Select(
                        change => change.ManualValue)
                    .SequenceEqual(["1.5", "4.5"])
                && manualEvidence.HeldOutSamples.Count == 1
                && manualEvidence.HeldOutSamples.Single().Status
                    == ResultStatus.Pass,
                manualWorkbench.ValidationThresholdCorrectionSummary);
            var savedManualRecipePath = Path.Combine(
                artifactRoot,
                "threshold-manual-correction-applied.ov3d-recipe.json");
            Check(
                "manual correction recipe save carries the durable evidence sidecar",
                manualWorkbench.TrySaveTeachingRecipe(
                    savedManualRecipePath,
                    out var manualSaveMessage)
                && File.Exists(
                    ToolRecipeThresholdCorrectionEvidenceStore.GetPathForRecipe(
                        savedManualRecipePath)),
                manualSaveMessage);
            var reopenedManualWorkbench = new ToolWorkbenchViewModel(
                Path.Combine(
                    artifactRoot,
                    "recent-threshold-manual-reopen.json"));
            Check(
                "manual correction reopens without execution and distinguishes suggested from committed values",
                reopenedManualWorkbench.TryOpenTeachingRecipe(
                    savedManualRecipePath,
                    out var manualReopenMessage)
                && reopenedManualWorkbench
                    .IsValidationThresholdManualCorrectionCommitted
                && reopenedManualWorkbench
                    .ValidationThresholdParameterChanges.Select(change =>
                        change.ManualValue).SequenceEqual(["1.5", "4.5"])
                && reopenedManualWorkbench.ValidationSetSamples.All(sample =>
                    sample.Status == "Pending"),
                manualReopenMessage);

            var unsupported = document with
            {
                Steps =
                [
                    step with
                    {
                        ToolId = "roi-crop",
                        ToolName = "ROI / Crop",
                        MinimumInputCount = 1,
                        InputEntityIds = [source.Id],
                        Parameters = []
                    }
                ]
            };
            Check(
                "tool without an executable adapter is reported, not fabricated",
                !ToolRecipeValidationSetExecution.CanExecute(unsupported, out var unsupportedMessage)
                && !string.IsNullOrWhiteSpace(unsupportedMessage),
                unsupportedMessage);
            Check(
                "fixture recipe remains reopenable",
                ToolRecipeDocumentStore.Load(recipePath).Steps.Count == 1,
                recipePath);

            var graphPackage = Path.GetFullPath(Path.Combine(
                Environment.CurrentDirectory,
                "3D",
                "SyntheticValidation",
                "AffineInspectionPlateV1"));
            var graphRecipePath = Path.Combine(graphPackage, "inspection-recipe.ov3d-recipe.json");
            var graphSourcePath = Path.Combine(graphPackage, "source-affine-inspection-plate-v1.C3D");
            var graphDocument = ToolRecipeDocumentStore.Load(graphRecipePath);
            var graphIdentity = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(graphSourcePath);
            var graphSourceInfo = new FileInfo(graphSourcePath);
            var graphSource = C3DHeightFieldSnapshot.LoadVerified(
                graphSourcePath,
                graphDocument.Source.Id,
                graphDocument.Source.Unit,
                graphDocument.Source.FrameId,
                graphSourceInfo.Length,
                graphIdentity.ContentSha256,
                graphIdentity.GridWidth,
                graphIdentity.GridHeight);
            var graphPassPath = Path.Combine(artifactRoot, "graph-pass.C3D");
            File.Copy(graphSourcePath, graphPassPath, overwrite: true);

            var graphSelections = graphDocument.Selections
                ?? throw new InvalidDataException("Synthetic graph recipe requires authored selections.");
            var graphFailValues = graphSource.Values.ToArray();
            var thicknessStep = graphDocument.Steps.Single(candidate => candidate.ToolId == "thickness");
            var thicknessSelection = graphSelections.Single(candidate =>
                string.Equals(candidate.Id, thicknessStep.InputEntityIds[2], StringComparison.Ordinal));
            AddFinite(graphFailValues, graphSource.Width, thicknessSelection.GridRectangle!, 100);
            var graphFailPath = Path.Combine(artifactRoot, "graph-measurement-fail.C3D");
            C3DHeightFieldSnapshot.CreateForVerification(
                graphDocument.Source.Id,
                graphSource.Width,
                graphSource.Height,
                graphFailValues).SaveC3D(graphFailPath);

            var graphErrorValues = graphSource.Values.ToArray();
            var firstEdgeStep = graphDocument.Steps.First(candidate => candidate.ToolId == "height-difference-edge");
            var firstEdgeSelection = graphSelections.Single(candidate =>
                string.Equals(candidate.Id, firstEdgeStep.InputEntityIds[1], StringComparison.Ordinal));
            Fill(graphErrorValues, graphSource.Width, firstEdgeSelection.GridRectangle!, double.NaN);
            var graphErrorPath = Path.Combine(artifactRoot, "graph-upstream-error.C3D");
            C3DHeightFieldSnapshot.CreateForVerification(
                graphDocument.Source.Id,
                graphSource.Width,
                graphSource.Height,
                graphErrorValues).SaveC3D(graphErrorPath);

            Check(
                "full synthetic affine graph is executable",
                ToolRecipeValidationSetExecution.CanExecute(graphDocument, out var graphCapability),
                graphCapability);
            var graphOriginalPath = graphDocument.Source.Path;
            var graphOriginalHash = graphDocument.Source.ContentSha256;
            var graphResult = ToolRecipeValidationSetExecution.Execute(
                graphDocument,
                [graphPassPath, graphFailPath, graphErrorPath]);
            Check(
                "full graph validation completes Pass Fail Error samples",
                graphResult.Samples.Count == 3
                && graphResult.Samples[0].Status == ResultStatus.Pass
                && graphResult.Samples[1].Status == ResultStatus.Fail
                && graphResult.Samples[2].Status == ResultStatus.Error,
                string.Join(",", graphResult.Samples.Select(sample => $"{sample.Order}:{sample.Status}:{sample.Steps.Count}")));
            Check(
                "passing sample reaches every authored tool in order",
                graphResult.Samples[0].Steps.Count == graphDocument.Steps.Count
                && graphResult.Samples[0].Steps.Select(item => item.StepId)
                    .SequenceEqual(graphDocument.Steps.Select(item => item.Id)),
                $"{graphResult.Samples[0].Steps.Count}/{graphDocument.Steps.Count}");
            Check(
                "measurement failure preserves later ordered evidence",
                graphResult.Samples[1].Steps.Count == graphDocument.Steps.Count
                && graphResult.Samples[1].Steps[^1].StepId == graphDocument.Steps[^1].Id,
                $"{graphResult.Samples[1].Steps.Count}/{graphDocument.Steps.Count};last={graphResult.Samples[1].Steps.LastOrDefault()?.StepId}");
            Check(
                "upstream error stops dependent graph",
                graphResult.Samples[2].Steps.Count < graphDocument.Steps.Count
                && graphResult.Samples[2].Message.Contains("stopped ordered replay", StringComparison.Ordinal),
                $"{graphResult.Samples[2].Steps.Count}/{graphDocument.Steps.Count};{graphResult.Samples[2].Message}");
            Check(
                "full graph replay leaves authored recipe identity unchanged",
                string.Equals(graphDocument.Source.Path, graphOriginalPath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(graphDocument.Source.ContentSha256, graphOriginalHash, StringComparison.OrdinalIgnoreCase),
                $"{graphDocument.Source.Path} | {graphDocument.Source.ContentSha256}");

            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                var canceled = false;
                try
                {
                    ToolRecipeValidationSetExecution.Execute(
                        graphDocument,
                        [graphPassPath],
                        cancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    canceled = true;
                }

                Check(
                    "pre-canceled replay exits without a partial result",
                    canceled,
                    canceled ? "OperationCanceledException" : "execution was not canceled");
            }

            var workbench = new ToolWorkbenchViewModel(Path.Combine(artifactRoot, "recent-validation-set.json"));
            Check(
                "workbench reopens the full graph recipe",
                workbench.TryOpenTeachingRecipe(graphRecipePath, out var openMessage),
                openMessage);
            var workbenchSourcePath = workbench.Source.Path;
            Check(
                "current recipe input stages as one pending sample without execution",
                workbench.AddCurrentSourceToValidationSetCommand.CanExecute(null)
                && StageCurrentSource(workbench, graphSourcePath),
                $"count={workbench.ValidationSetAllCount};status={workbench.ValidationSetSamples.FirstOrDefault()?.Status};source={workbench.ValidationSetSamples.FirstOrDefault()?.SourcePath}");
            workbench.SetValidationSetSources([graphPassPath, graphFailPath, graphErrorPath]);
            SetRole(
                workbench,
                graphPassPath,
                ToolRecipeValidationSampleRole.Good);
            SetRole(
                workbench,
                graphFailPath,
                ToolRecipeValidationSampleRole.Bad);
            SetRole(
                workbench,
                graphErrorPath,
                ToolRecipeValidationSampleRole.HeldOut);
            Check(
                "role assignment is sidecar-only and keeps evidence collapsed",
                !workbench.IsDirty
                && workbench.IsValidationSetDefinitionDirty
                && workbench.HasUncommittedRecipeChanges
                && !workbench.IsValidationEvidenceExpanded
                && workbench.ValidationSetSamples.All(sample =>
                    sample.Status == "Pending")
                && !workbench.HasValidationSetIssues
                && workbench.IsSelectedValidationRoleHeldOut
                && !workbench.HasValidationEvidence
                && !workbench.HasValidationThresholdCandidates,
                $"recipeDirty={workbench.IsDirty};manifestDirty={workbench.IsValidationSetDefinitionDirty};unsaved={workbench.HasUncommittedRecipeChanges};expanded={workbench.IsValidationEvidenceExpanded};pending={workbench.ValidationSetSamples.Count(sample => sample.Status == "Pending")};issues={workbench.HasValidationSetIssues};heldOutSelected={workbench.IsSelectedValidationRoleHeldOut};evidence={workbench.HasValidationEvidence};candidates={workbench.HasValidationThresholdCandidates}");
            workbench.RunValidationSetAsync().GetAwaiter().GetResult();
            Check(
                "workbench exposes Pass Fail Error counts and selects the first issue",
                workbench.ValidationSetAllCount == 3
                && workbench.ValidationSetPassCount == 1
                && workbench.ValidationSetFailCount == 1
                && workbench.ValidationSetErrorCount == 1
                && workbench.HasValidationSetIssues
                && workbench.SelectedValidationSetSample?.Status == "Fail",
                $"{workbench.ValidationSetAllCount}/{workbench.ValidationSetPassCount}/{workbench.ValidationSetFailCount}/{workbench.ValidationSetErrorCount};issues={workbench.HasValidationSetIssues};selected={workbench.SelectedValidationSetSample?.Status}");
            Check(
                "selected failed step retains metric and overlay evidence",
                workbench.SelectedValidationSetStep is { Metrics.Count: > 0, Overlays.Count: > 0 },
                $"step={workbench.SelectedValidationSetStep?.ToolName};metrics={workbench.SelectedValidationSetStep?.Metrics.Count};overlays={workbench.SelectedValidationSetStep?.Overlays.Count}");
            var navigationShell = new ShellMainWindowViewModel(
                recentRecipesPath: Path.Combine(
                    artifactRoot,
                    "recent-validation-teach-navigation.json"));
            Check(
                "failure navigation fixture opens the same authored graph",
                navigationShell.Workbench.TryOpenTeachingRecipe(
                    graphRecipePath,
                    out var navigationOpenMessage),
                navigationOpenMessage);
            navigationShell.Workbench.SelectedValidationSetSample =
                workbench.SelectedValidationSetSample;
            navigationShell.Workbench.SelectedValidationSetStep =
                workbench.SelectedValidationSetStep;
            navigationShell.SelectWorkspaceCommand.Execute(
                ShellWorkspaceMode.Inspect);
            var navigationDirtyBefore = navigationShell.Workbench.IsDirty;
            var navigationStepCountBefore =
                navigationShell.Workbench.PipelineSteps.Count;
            var navigationSourceBefore =
                navigationShell.Workbench.Source.Path;
            var navigationSummaryBefore =
                navigationShell.Workbench.ValidationSetSummary;
            var selectedFailureStepId =
                navigationShell.Workbench.SelectedValidationSetStep?.StepId;
            var canOpenFailureInTeach =
                navigationShell.OpenSelectedValidationIssueInTeachCommand
                    .CanExecute(null);
            navigationShell.OpenSelectedValidationIssueInTeachCommand
                .Execute(null);
            Check(
                "selected failure opens its owning step in Teach without edit or execution",
                canOpenFailureInTeach
                && navigationShell.IsTeachWorkspaceSelected
                && string.Equals(
                    navigationShell.Workbench.SelectedPipelineStep?.Id,
                    selectedFailureStepId,
                    StringComparison.OrdinalIgnoreCase)
                && navigationShell.Workbench.IsDirty == navigationDirtyBefore
                && navigationShell.Workbench.PipelineSteps.Count
                    == navigationStepCountBefore
                && string.Equals(
                    navigationShell.Workbench.Source.Path,
                    navigationSourceBefore,
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    navigationShell.Workbench.ValidationSetSummary,
                    navigationSummaryBefore,
                    StringComparison.Ordinal)
                && !navigationShell.Workbench.IsValidationSetRunning
                && !navigationShell.Workbench.IsSelectedStepPreviewRunning,
                $"canOpen={canOpenFailureInTeach};stage={navigationShell.SelectedWorkspaceMode};selected={navigationShell.Workbench.SelectedPipelineStep?.Id};dirty={navigationDirtyBefore}->{navigationShell.Workbench.IsDirty};steps={navigationStepCountBefore}->{navigationShell.Workbench.PipelineSteps.Count};validationRunning={navigationShell.Workbench.IsValidationSetRunning};previewRunning={navigationShell.Workbench.IsSelectedStepPreviewRunning}");
            Check(
                "workbench exposes role counts and labeled distributions",
                workbench.ValidationSetGoodCount == 1
                && workbench.ValidationSetBadCount == 1
                && workbench.ValidationSetHeldOutCount == 1
                && workbench.ValidationEvidenceDistributions.Count > 0,
                $"{workbench.ValidationSetGoodCount}/{workbench.ValidationSetBadCount}/{workbench.ValidationSetHeldOutCount};distributions={workbench.ValidationEvidenceDistributions.Count}");
            Check(
                "workbench exposes review-only candidates and exact sample decisions",
                workbench.ValidationThresholdCandidates.Count > 0
                && workbench.SelectedValidationThresholdCandidate is not null
                && workbench.SelectedValidationThresholdDecisions.Count == 2
                && !workbench.IsValidationThresholdExpanded,
                $"candidates={workbench.ValidationThresholdCandidates.Count};decisions={workbench.SelectedValidationThresholdDecisions.Count};expanded={workbench.IsValidationThresholdExpanded}");
            var beforeThresholdSelectionSource = workbench.Source.Path;
            var beforeThresholdSelectionSummary =
                workbench.ValidationSetSummary;
            var beforeThresholdSelectionDirty = workbench.IsDirty;
            workbench.SelectedValidationThresholdCandidate =
                workbench.ValidationThresholdCandidates.Skip(1).First();
            Check(
                "candidate selection is view-only and does not execute or edit",
                string.Equals(
                    workbench.Source.Path,
                    beforeThresholdSelectionSource,
                    StringComparison.OrdinalIgnoreCase)
                && workbench.IsDirty == beforeThresholdSelectionDirty
                && string.Equals(
                    workbench.ValidationSetSummary,
                    beforeThresholdSelectionSummary,
                    StringComparison.Ordinal)
                && workbench.SelectedValidationThresholdDecisions.Count == 2,
                $"sourceSame={string.Equals(workbench.Source.Path, beforeThresholdSelectionSource, StringComparison.OrdinalIgnoreCase)};dirty={beforeThresholdSelectionDirty}->{workbench.IsDirty};summarySame={string.Equals(workbench.ValidationSetSummary, beforeThresholdSelectionSummary, StringComparison.Ordinal)}");

            workbench.SetValidationSetFilterCommand.Execute("Fail");
            Check(
                "status filter reduces the visible sample list",
                workbench.ValidationSetSamples.Count == 1
                && workbench.ValidationSetSamples[0].Status == "Fail",
                $"{workbench.ValidationSetFilter};visible={workbench.ValidationSetSamples.Count}");
            workbench.SetValidationSetFilterCommand.Execute("All");
            workbench.NextValidationSetIssueCommand.Execute(null);
            var movedToError = workbench.SelectedValidationSetSample?.Status == "Error";
            workbench.PreviousValidationSetIssueCommand.Execute(null);
            Check(
                "issue navigation moves between Fail and Error",
                movedToError && workbench.SelectedValidationSetSample?.Status == "Fail",
                $"nextError={movedToError};previous={workbench.SelectedValidationSetSample?.Status}");

            var comparisonRequested = false;
            workbench.ValidationSetComparisonRequested += (_, _) => comparisonRequested = true;
            workbench.OpenValidationSetComparisonCommand.Execute(null);
            Check(
                "selected failure opens a read-only source comparison",
                comparisonRequested
                && string.Equals(workbench.CompareSlotAArtifactId, graphDocument.Source.Id, StringComparison.Ordinal)
                && workbench.GetCompareCandidate(workbench.CompareSlotBArtifactId)?.C3DPath == graphFailPath,
                $"requested={comparisonRequested};A={workbench.CompareSlotAArtifactId};B={workbench.CompareSlotBArtifactId}");
            Check(
                "analysis workflow leaves the authored source unchanged",
                string.Equals(workbench.Source.Path, workbenchSourcePath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(workbench.Source.Path, graphSourcePath, StringComparison.OrdinalIgnoreCase),
                workbench.Source.Path);

            var savedGraphRecipe = Path.Combine(
                artifactRoot,
                "saved-labeled-validation.ov3d-recipe.json");
            Check(
                "workbench save writes a durable role sidecar",
                workbench.TrySaveTeachingRecipe(
                    savedGraphRecipe,
                    out var saveMessage)
                && File.Exists(
                    ToolRecipeValidationSetDefinitionStore.GetPathForRecipe(
                        savedGraphRecipe)),
                saveMessage);
            var reopenedWorkbench = new ToolWorkbenchViewModel(
                Path.Combine(artifactRoot, "recent-reopened-validation-set.json"));
            Check(
                "workbench reopen restores roles without execution",
                reopenedWorkbench.TryOpenTeachingRecipe(
                    savedGraphRecipe,
                    out var reopenMessage)
                && reopenedWorkbench.ValidationSetSamples.Select(sample =>
                        sample.Role)
                    .SequenceEqual(
                    [
                        ToolRecipeValidationSampleRole.Good,
                        ToolRecipeValidationSampleRole.Bad,
                        ToolRecipeValidationSampleRole.HeldOut
                    ])
                && reopenedWorkbench.ValidationSetSamples.All(sample =>
                    sample.Status == "Pending")
                && !reopenedWorkbench.HasValidationEvidence
                && !reopenedWorkbench.HasValidationThresholdCandidates,
                reopenMessage);
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unhandled verification exception | {exception}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        summary = $"Validation Set verification: {passed}/{total} passed | {Path.GetFullPath(reportPath)}";
        return passed == total && total == 84;
    }

    private static ToolRecipeThresholdCandidate CreateMappingCandidate(
        ToolRecipeStep step,
        string metricName,
        ToolRecipeThresholdLimitKind limitKind,
        double? minimum,
        double? maximum) =>
        new(
            $"mapping.{step.ToolId}.{metricName}.{limitKind}",
            ToolRecipeEvidenceScope.StepMetric,
            step.Id,
            step.ToolName,
            metricName,
            "raw-height",
            limitKind,
            minimum,
            maximum,
            2,
            0,
            2,
            0,
            []);

    private static bool RunThresholdWorkbench(
        ToolWorkbenchViewModel workbench,
        string candidateId)
    {
        workbench.RunValidationSetAsync().GetAwaiter().GetResult();
        return workbench.ValidationThresholdCandidates.Any(candidate =>
            candidate.CandidateId == candidateId);
    }

    private static bool StageCurrentSource(ToolWorkbenchViewModel workbench, string expectedSourcePath)
    {
        workbench.AddCurrentSourceToValidationSetCommand.Execute(null);
        return workbench.ValidationSetAllCount == 1
               && workbench.ValidationSetSamples.Count == 1
               && workbench.ValidationSetSamples[0].Status == "Pending"
               && string.Equals(
                   workbench.ValidationSetSamples[0].SourcePath,
                   expectedSourcePath,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static void SetRole(
        ToolWorkbenchViewModel workbench,
        string path,
        ToolRecipeValidationSampleRole role)
    {
        workbench.SelectedValidationSetSample =
            workbench.ValidationSetSamples.First(sample => string.Equals(
                sample.SourcePath,
                path,
                StringComparison.OrdinalIgnoreCase));
        workbench.SetValidationSampleRoleCommand.Execute(role.ToString());
    }

    private static void AddFinite(
        double[] values,
        int width,
        ToolRecipeGridRectangle rectangle,
        double offset)
    {
        for (var row = rectangle.Row; row < rectangle.Row + rectangle.RowCount; row++)
        for (var column = rectangle.Column; column < rectangle.Column + rectangle.ColumnCount; column++)
        {
            var index = row * width + column;
            if (double.IsFinite(values[index])) values[index] += offset;
        }
    }

    private static void Fill(
        double[] values,
        int width,
        ToolRecipeGridRectangle rectangle,
        double value)
    {
        for (var row = rectangle.Row; row < rectangle.Row + rectangle.RowCount; row++)
        for (var column = rectangle.Column; column < rectangle.Column + rectangle.ColumnCount; column++)
            values[row * width + column] = value;
    }

    private static void CreateThicknessFixture(
        string path,
        double thickness)
    {
        var values = new double[16];
        for (var row = 0; row < 4; row++)
        for (var column = 0; column < 4; column++)
        {
            var plane = 10 + 0.5 * row + 0.25 * column;
            values[row * 4 + column] =
                row < 2
                    ? plane
                    : plane + thickness;
        }

        C3DHeightFieldSnapshot.CreateForVerification(
            "source.validation",
            4,
            4,
            values).SaveC3D(path);
    }

}

internal static class ThresholdCandidateVerificationFormatting
{
    public static string LimitsForVerification(
        this ToolRecipeThresholdCandidate candidate) =>
        $"{candidate.Minimum?.ToString("R") ?? "-"}.."
        + $"{candidate.Maximum?.ToString("R") ?? "-"}";
}
