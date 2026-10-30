using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using Xunit;

namespace OpenVisionLab.ThreeD.Data.Tests;

public sealed class ThresholdCorrectionEvidenceStoreTests
{
    [Theory]
    [InlineData("proposal")]
    [InlineData("changes")]
    [InlineData("candidate")]
    [InlineData("decisions")]
    [InlineData("decision-entry")]
    [InlineData("held-out")]
    [InlineData("held-out-entry")]
    [InlineData("manual-changes")]
    [InlineData("manual-change-entry")]
    [InlineData("manual-before")]
    [InlineData("manual-before-entry")]
    [InlineData("manual-after")]
    public void MissingRequiredEvidenceFailsControlledAndPreservesBytes(string missing)
    {
        var directory = Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD.Tests", $"correction-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var recipePath = Path.Combine(directory, "recipe.ov3d-recipe.json");
        var manifestPath = ToolRecipeThresholdCorrectionEvidenceStore.GetPathForRecipe(recipePath);
        var valid = CreateEvidence(directory);
        var malformed = missing switch
        {
            "proposal" => valid with { Proposal = null! },
            "changes" => valid with { Proposal = valid.Proposal with { Changes = null! } },
            "candidate" => valid with { Proposal = valid.Proposal with { Candidate = null! } },
            "decisions" => valid with { Proposal = valid.Proposal with { Candidate = valid.Proposal.Candidate with { Decisions = null! } } },
            "decision-entry" => valid with { Proposal = valid.Proposal with { Candidate = valid.Proposal.Candidate with { Decisions = [null!] } } },
            "held-out" => valid with { HeldOutSamples = null! },
            "held-out-entry" => valid with { HeldOutSamples = [null!] },
            "manual-changes" => valid with { ManualCorrection = valid.ManualCorrection! with { ParameterChanges = null! } },
            "manual-change-entry" => valid with { ManualCorrection = valid.ManualCorrection! with { ParameterChanges = [null!] } },
            "manual-before" => valid with { ManualCorrection = valid.ManualCorrection! with { BeforeDevelopmentSamples = null! } },
            "manual-before-entry" => valid with { ManualCorrection = valid.ManualCorrection! with { BeforeDevelopmentSamples = [null!] } },
            "manual-after" => valid with { ManualCorrection = valid.ManualCorrection! with { AfterDevelopmentSamples = null! } },
            _ => throw new ArgumentOutOfRangeException(nameof(missing))
        };
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter() } };
        var original = JsonSerializer.SerializeToUtf8Bytes(malformed, options);
        try
        {
            File.WriteAllBytes(manifestPath, original);
            Assert.Throws<InvalidDataException>(() => ToolRecipeThresholdCorrectionEvidenceStore.LoadForRecipe(recipePath));
            Assert.Equal(original, File.ReadAllBytes(manifestPath));
            Assert.Throws<InvalidDataException>(() => ToolRecipeThresholdCorrectionEvidenceStore.SaveForRecipe(recipePath, malformed));
            Assert.Equal(original, File.ReadAllBytes(manifestPath));

            ToolRecipeThresholdCorrectionEvidenceStore.SaveForRecipe(recipePath, valid);
            var restored = Assert.IsType<ToolRecipeThresholdCorrectionEvidence>(ToolRecipeThresholdCorrectionEvidenceStore.LoadForRecipe(recipePath));
            Assert.Equal(valid.Proposal.CandidateId, restored.Proposal.CandidateId);
            Assert.Equal(valid.HeldOutSamples[0].SourcePath, Assert.Single(restored.HeldOutSamples).SourcePath);
            Assert.Equal(valid.ManualCorrection!.BeforeDevelopmentSamples[0].SourcePath, Assert.Single(restored.ManualCorrection!.BeforeDevelopmentSamples).SourcePath);
        }
        finally
        {
            File.Delete(manifestPath);
            Directory.Delete(directory);
        }
    }

    [Theory]
    [InlineData("change-entry")]
    [InlineData("held-metrics")]
    [InlineData("held-metric-entry")]
    [InlineData("before-metrics")]
    [InlineData("before-metric-entry")]
    [InlineData("after-metrics")]
    [InlineData("after-metric-entry")]
    public void MalformedNestedEvidenceIsRejectedBeforeProjectionAndAllowsRecovery(string missing)
    {
        var directory = Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD.Tests", $"nested-correction-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var recipePath = Path.Combine(directory, "recipe.ov3d-recipe.json");
        var manifestPath = ToolRecipeThresholdCorrectionEvidenceStore.GetPathForRecipe(recipePath);
        var seed = CreateEvidence(directory);
        var seedManual = seed.ManualCorrection!;
        var metric = new ToolRecipeThresholdHeldOutMetricEvidence("step.height", "Height", "height", "raw-height", 0.5, ResultStatus.Pass);
        var valid = seed with
        {
            HeldOutSamples = [seed.HeldOutSamples[0] with { Metrics = [metric] }],
            ManualCorrection = seedManual with
            {
                BeforeDevelopmentSamples = [seedManual.BeforeDevelopmentSamples[0] with { Metrics = [metric with { Value = 1.5 }] }],
                AfterDevelopmentSamples = [seedManual.AfterDevelopmentSamples[0] with { Metrics = [metric with { Value = 1.5, Status = ResultStatus.Fail }] }]
            }
        };
        var manual = valid.ManualCorrection!;
        var malformed = missing switch
        {
            "change-entry" => valid with { Proposal = valid.Proposal with { Changes = [null!] } },
            "held-metrics" => valid with { HeldOutSamples = [valid.HeldOutSamples[0] with { Metrics = null! }] },
            "held-metric-entry" => valid with { HeldOutSamples = [valid.HeldOutSamples[0] with { Metrics = [null!] }] },
            "before-metrics" => valid with { ManualCorrection = manual with { BeforeDevelopmentSamples = [manual.BeforeDevelopmentSamples[0] with { Metrics = null! }] } },
            "before-metric-entry" => valid with { ManualCorrection = manual with { BeforeDevelopmentSamples = [manual.BeforeDevelopmentSamples[0] with { Metrics = [null!] }] } },
            "after-metrics" => valid with { ManualCorrection = manual with { AfterDevelopmentSamples = [manual.AfterDevelopmentSamples[0] with { Metrics = null! }] } },
            "after-metric-entry" => valid with { ManualCorrection = manual with { AfterDevelopmentSamples = [manual.AfterDevelopmentSamples[0] with { Metrics = [null!] }] } },
            _ => throw new ArgumentOutOfRangeException(nameof(missing))
        };
        var document = new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion, valid.RecipeName,
            new("source", "Source", "C3D", "raw-height", "frame.c3d-grid-index", Path.Combine(directory, "good.C3D"), ContentSha256: valid.RecipeSourceSha256),
            [], [new("step.height", "height", "Height", 1, ["source"], "output", [new("UpperTolerance", "1")])]);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter() } };
        var original = JsonSerializer.SerializeToUtf8Bytes(malformed, options);
        try
        {
            File.WriteAllBytes(manifestPath, original);
            var rejected = ToolRecipeThresholdCorrectionRunRecordProjection.Create(recipePath, document);
            Assert.Equal(InspectionRunThresholdCorrectionEvidenceState.Invalid, rejected.State);
            Assert.Null(rejected.Evidence);
            Assert.Throws<InvalidDataException>(() => ToolRecipeThresholdCorrectionEvidenceStore.LoadForRecipe(recipePath));
            Assert.Equal(original, File.ReadAllBytes(manifestPath));
            Assert.Throws<InvalidDataException>(() => ToolRecipeThresholdCorrectionEvidenceStore.SaveForRecipe(recipePath, malformed));
            Assert.Equal(original, File.ReadAllBytes(manifestPath));

            ToolRecipeThresholdCorrectionEvidenceStore.SaveForRecipe(recipePath, valid);
            var restored = Assert.IsType<ToolRecipeThresholdCorrectionEvidence>(ToolRecipeThresholdCorrectionEvidenceStore.LoadForRecipe(recipePath));
            Assert.Equal(valid.Proposal.Changes[0], Assert.Single(restored.Proposal.Changes));
            Assert.Equal(metric, Assert.Single(Assert.Single(restored.HeldOutSamples).Metrics));
            Assert.Equal(manual.BeforeDevelopmentSamples[0].Metrics[0], Assert.Single(Assert.Single(restored.ManualCorrection!.BeforeDevelopmentSamples).Metrics));
            Assert.Equal(manual.AfterDevelopmentSamples[0].Metrics[0], Assert.Single(Assert.Single(restored.ManualCorrection.AfterDevelopmentSamples).Metrics));
            Assert.Equal(InspectionRunThresholdCorrectionEvidenceState.Available, ToolRecipeThresholdCorrectionRunRecordProjection.Create(recipePath, document).State);
        }
        finally
        {
            File.Delete(manifestPath);
            Directory.Delete(directory);
        }
    }

    private static ToolRecipeThresholdCorrectionEvidence CreateEvidence(string directory)
    {
        var goodPath = Path.Combine(directory, "good.C3D");
        var badPath = Path.Combine(directory, "bad.C3D");
        var heldPath = Path.Combine(directory, "held.C3D");
        var candidate = new ToolRecipeThresholdCandidate(
            "candidate.maximum", ToolRecipeEvidenceScope.StepMetric, "step.height", "Height", "height", "raw-height",
            ToolRecipeThresholdLimitKind.Maximum, null, 2, 1, 0, 0, 1,
            [new(1, "good", goodPath, ToolRecipeValidationSampleRole.Good, ToolRecipeValidationSampleRole.Good, ToolRecipeThresholdDecisionKind.CorrectGood, 0.5),
             new(2, "bad", badPath, ToolRecipeValidationSampleRole.Bad, ToolRecipeValidationSampleRole.Good, ToolRecipeThresholdDecisionKind.FalseAccept, 1.5)]);
        var proposal = new ToolRecipeThresholdParameterProposal(
            "1.0", candidate.CandidateId, "step.height", "height", "Height", "height", ToolRecipeThresholdLimitKind.Maximum,
            [new("UpperTolerance", "1", "2")], candidate);
        var before = new ToolRecipeThresholdDevelopmentSampleEvidence(2, "bad", badPath, ToolRecipeValidationSampleRole.Bad, ResultStatus.Pass, false, "before", []);
        var after = before with { Status = ResultStatus.Fail, ExpectedMatch = true, Message = "after" };
        var manual = new ToolRecipeThresholdManualCorrectionEvidence("1.0", [new("UpperTolerance", "2", "1")], 1, [before], 0, [after]);
        var held = new ToolRecipeThresholdHeldOutSampleEvidence(3, "held", heldPath, ResultStatus.Pass, "held-out replay", []);
        return new ToolRecipeThresholdCorrectionEvidence("1.0", "Correction", new string('A', 64), proposal, ResultStatus.Pass, "applied", [held], manual);
    }
}
