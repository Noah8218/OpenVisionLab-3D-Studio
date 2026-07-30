using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Data;

public static class ToolRecipeThresholdCorrectionEvidenceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true
    };

    public static string GetPathForRecipe(string recipePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipePath);
        return $"{Path.GetFullPath(recipePath)}.threshold-correction.json";
    }

    public static void SaveForRecipe(
        string recipePath,
        ToolRecipeThresholdCorrectionEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        Validate(evidence);
        var fullRecipePath = Path.GetFullPath(recipePath);
        var directory =
            Path.GetDirectoryName(fullRecipePath) ?? Environment.CurrentDirectory;
        var portable = RewritePaths(
            evidence,
            path => Path.GetRelativePath(directory, Path.GetFullPath(path)));
        WriteAtomic(GetPathForRecipe(fullRecipePath), portable);
    }

    public static ToolRecipeThresholdCorrectionEvidence? LoadForRecipe(
        string recipePath)
    {
        var fullRecipePath = Path.GetFullPath(recipePath);
        var path = GetPathForRecipe(fullRecipePath);
        if (!File.Exists(path))
        {
            return null;
        }

        using var stream = File.OpenRead(path);
        var evidence =
            JsonSerializer.Deserialize<ToolRecipeThresholdCorrectionEvidence>(
                stream,
                JsonOptions)
            ?? throw new InvalidDataException(
                "Threshold correction evidence JSON is empty.");
        Validate(evidence);
        var directory =
            Path.GetDirectoryName(fullRecipePath) ?? Environment.CurrentDirectory;
        return RewritePaths(
            evidence,
            value => Path.IsPathFullyQualified(value)
                ? Path.GetFullPath(value)
                : Path.GetFullPath(Path.Combine(directory, value)));
    }

    public static void Validate(ToolRecipeThresholdCorrectionEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.ContractVersion
                != ToolRecipeThresholdCorrectionEvidence.CurrentContractVersion
            || string.IsNullOrWhiteSpace(evidence.RecipeName)
            || string.IsNullOrWhiteSpace(evidence.RecipeSourceSha256)
            || evidence.Proposal.ContractVersion
                != ToolRecipeThresholdParameterProposal.CurrentContractVersion
            || string.IsNullOrWhiteSpace(evidence.Proposal.CandidateId)
            || string.IsNullOrWhiteSpace(evidence.Proposal.StepId)
            || evidence.Proposal.Changes.Count == 0
            || evidence.HeldOutSamples.Count == 0
            || evidence.HeldOutSamples.Any(sample =>
                string.IsNullOrWhiteSpace(sample.SourcePath)
                || string.IsNullOrWhiteSpace(sample.SampleIdentity)))
        {
            throw new InvalidDataException(
                "Threshold correction evidence identity, proposal, or Held-out replay is incomplete.");
        }

        if (evidence.ManualCorrection is { } manual)
        {
            var beforeKeys = manual.BeforeDevelopmentSamples.Select(
                SampleKey).ToArray();
            var afterKeys = manual.AfterDevelopmentSamples.Select(
                SampleKey).ToArray();
            if (manual.ContractVersion
                    != ToolRecipeThresholdManualCorrectionEvidence
                        .CurrentContractVersion
                || manual.ParameterChanges.Count == 0
                || manual.ParameterChanges.All(change => string.Equals(
                    change.SuggestedValue,
                    change.ManualValue,
                    StringComparison.Ordinal))
                || manual.BeforeMismatchCount <= 0
                || manual.AfterMismatchCount != 0
                || manual.BeforeDevelopmentSamples.Count == 0
                || manual.BeforeDevelopmentSamples.Any(sample =>
                    sample.Role == ToolRecipeValidationSampleRole.HeldOut
                    || string.IsNullOrWhiteSpace(sample.SourcePath)
                    || string.IsNullOrWhiteSpace(sample.SampleIdentity))
                || manual.AfterDevelopmentSamples.Any(sample =>
                    sample.Role == ToolRecipeValidationSampleRole.HeldOut
                    || !sample.ExpectedMatch
                    || string.IsNullOrWhiteSpace(sample.SourcePath)
                    || string.IsNullOrWhiteSpace(sample.SampleIdentity))
                || manual.BeforeMismatchCount
                    != manual.BeforeDevelopmentSamples.Count(sample =>
                        !sample.ExpectedMatch)
                || manual.AfterMismatchCount
                    != manual.AfterDevelopmentSamples.Count(sample =>
                        !sample.ExpectedMatch)
                || !beforeKeys.SequenceEqual(
                    afterKeys,
                    StringComparer.Ordinal))
            {
                throw new InvalidDataException(
                    "Manual threshold correction evidence is incomplete or internally inconsistent.");
            }
        }
    }

    private static ToolRecipeThresholdCorrectionEvidence RewritePaths(
        ToolRecipeThresholdCorrectionEvidence evidence,
        Func<string, string> rewrite)
    {
        var candidate = evidence.Proposal.Candidate with
        {
            Decisions = evidence.Proposal.Candidate.Decisions.Select(decision =>
                decision with { SourcePath = rewrite(decision.SourcePath) })
                .ToArray()
        };
        return evidence with
        {
            Proposal = evidence.Proposal with { Candidate = candidate },
            HeldOutSamples = evidence.HeldOutSamples.Select(sample =>
                sample with { SourcePath = rewrite(sample.SourcePath) })
                .ToArray(),
            ManualCorrection = evidence.ManualCorrection is not { } manual
                ? null
                : manual with
                {
                    BeforeDevelopmentSamples =
                        manual.BeforeDevelopmentSamples.Select(sample =>
                            sample with
                            {
                                SourcePath = rewrite(sample.SourcePath)
                            }).ToArray(),
                    AfterDevelopmentSamples =
                        manual.AfterDevelopmentSamples.Select(sample =>
                            sample with
                            {
                                SourcePath = rewrite(sample.SourcePath)
                            }).ToArray()
                }
        };
    }

    private static string SampleKey(
        ToolRecipeThresholdDevelopmentSampleEvidence sample) =>
        $"{sample.SampleOrder}|{sample.SampleIdentity}|{sample.Role}";

    private static void WriteAtomic(
        string path,
        ToolRecipeThresholdCorrectionEvidence evidence)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = $"{path}.tmp.{Guid.NewGuid():N}";
        try
        {
            var bytes = new UTF8Encoding(false).GetBytes(
                JsonSerializer.Serialize(evidence, JsonOptions));
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
