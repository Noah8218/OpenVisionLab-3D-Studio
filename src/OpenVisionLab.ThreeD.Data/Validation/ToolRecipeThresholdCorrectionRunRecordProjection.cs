using System.Security.Cryptography;
using System.Text.Json;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Projects already persisted threshold-correction evidence into a Run Record.
/// This type performs identity and freshness checks only; it never executes a
/// recipe, creates a candidate, applies parameters, or replays samples.
/// </summary>
public static class ToolRecipeThresholdCorrectionRunRecordProjection
{
    public static InspectionRunThresholdCorrectionEvidence Create(
        string recipePath,
        ToolRecipeDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipePath);
        ArgumentNullException.ThrowIfNull(document);

        var sidecarPath =
            ToolRecipeThresholdCorrectionEvidenceStore.GetPathForRecipe(
                recipePath);
        if (!File.Exists(sidecarPath))
        {
            return Result(
                InspectionRunThresholdCorrectionEvidenceState.Unavailable,
                "No threshold-correction sidecar was present when this Run Record was created.",
                sidecarPath,
                null,
                null);
        }

        string? sidecarSha256 = null;
        try
        {
            sidecarSha256 = HashFile(sidecarPath);
            var evidence =
                ToolRecipeThresholdCorrectionEvidenceStore.LoadForRecipe(
                    recipePath);
            if (evidence is null)
            {
                return Result(
                    InspectionRunThresholdCorrectionEvidenceState.Unavailable,
                    "No threshold-correction sidecar was present when this Run Record was created.",
                    sidecarPath,
                    sidecarSha256,
                    null);
            }

            ValidateReportingIdentity(evidence);
            var mismatch = FindIdentityMismatch(document, evidence);
            if (mismatch is not null)
            {
                return Result(
                    InspectionRunThresholdCorrectionEvidenceState.Mismatch,
                    mismatch,
                    sidecarPath,
                    sidecarSha256,
                    evidence);
            }

            var stale = FindStaleParameter(document, evidence);
            if (stale is not null)
            {
                return Result(
                    InspectionRunThresholdCorrectionEvidenceState.Stale,
                    stale,
                    sidecarPath,
                    sidecarSha256,
                    evidence);
            }

            return Result(
                InspectionRunThresholdCorrectionEvidenceState.Available,
                "Threshold-correction evidence matched the recorded recipe and was embedded without executing inspection.",
                sidecarPath,
                sidecarSha256,
                evidence);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or JsonException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException)
        {
            return Result(
                InspectionRunThresholdCorrectionEvidenceState.Invalid,
                $"Threshold-correction sidecar could not be trusted: {exception.Message}",
                sidecarPath,
                sidecarSha256,
                null);
        }
    }

    private static void ValidateReportingIdentity(
        ToolRecipeThresholdCorrectionEvidence evidence)
    {
        var proposalNames = evidence.Proposal.Changes.Select(change =>
            change.ParameterName).ToArray();
        if (proposalNames.Distinct(StringComparer.Ordinal).Count()
            != proposalNames.Length)
        {
            throw new InvalidDataException(
                "Threshold-correction proposal contains duplicate parameter identities.");
        }
        if (evidence.ManualCorrection is not { } manual)
        {
            return;
        }

        var manualByName = manual.ParameterChanges.ToDictionary(
            change => change.ParameterName,
            StringComparer.Ordinal);
        if (manualByName.Count != proposalNames.Length)
        {
            throw new InvalidDataException(
                "Manual threshold-correction parameters do not cover the exact proposal.");
        }
        foreach (var change in evidence.Proposal.Changes)
        {
            if (!manualByName.TryGetValue(
                    change.ParameterName,
                    out var manualChange)
                || !string.Equals(
                    manualChange.SuggestedValue,
                    change.ProposedValue,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Manual threshold-correction parameter '{change.ParameterName}' does not preserve the exact suggestion.");
            }
        }
    }

    private static string? FindIdentityMismatch(
        ToolRecipeDocument document,
        ToolRecipeThresholdCorrectionEvidence evidence)
    {
        if (!string.Equals(
                evidence.RecipeName,
                document.Name,
                StringComparison.Ordinal))
        {
            return
                $"Threshold-correction recipe name '{evidence.RecipeName}' does not match '{document.Name}'.";
        }
        if (string.IsNullOrWhiteSpace(document.Source.ContentSha256)
            || !string.Equals(
                evidence.RecipeSourceSha256,
                document.Source.ContentSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            return
                "Threshold-correction source SHA-256 does not match the recorded recipe source identity.";
        }

        var proposal = evidence.Proposal;
        var candidate = proposal.Candidate;
        if (!string.Equals(
                candidate.CandidateId,
                proposal.CandidateId,
                StringComparison.Ordinal)
            || !string.Equals(
                candidate.OwnerId,
                proposal.StepId,
                StringComparison.Ordinal)
            || !string.Equals(
                candidate.MetricName,
                proposal.MetricName,
                StringComparison.Ordinal)
            || candidate.LimitKind != proposal.LimitKind)
        {
            return
                "Threshold-correction candidate identity does not match its persisted proposal.";
        }

        var step = document.Steps.FirstOrDefault(item =>
            string.Equals(
                item.Id,
                proposal.StepId,
                StringComparison.Ordinal));
        if (step is null)
        {
            return
                $"Threshold-correction step '{proposal.StepId}' is not present in the recorded recipe.";
        }
        if (!string.Equals(
                step.ToolId,
                proposal.ToolId,
                StringComparison.Ordinal))
        {
            return
                $"Threshold-correction tool '{proposal.ToolId}' does not match recorded step tool '{step.ToolId}'.";
        }

        return null;
    }

    private static string? FindStaleParameter(
        ToolRecipeDocument document,
        ToolRecipeThresholdCorrectionEvidence evidence)
    {
        var proposal = evidence.Proposal;
        var step = document.Steps.Single(item =>
            string.Equals(
                item.Id,
                proposal.StepId,
                StringComparison.Ordinal));
        var committedValues = evidence.ManualCorrection is { } manual
            ? manual.ParameterChanges.ToDictionary(
                change => change.ParameterName,
                change => change.ManualValue,
                StringComparer.Ordinal)
            : proposal.Changes.ToDictionary(
                change => change.ParameterName,
                change => change.ProposedValue,
                StringComparer.Ordinal);

        foreach (var pair in committedValues)
        {
            var parameter = step.Parameters.FirstOrDefault(item =>
                string.Equals(
                    item.Name,
                    pair.Key,
                    StringComparison.Ordinal));
            if (parameter is null)
            {
                return
                    $"Threshold-correction parameter '{pair.Key}' is missing from recorded step '{step.Id}'.";
            }
            if (!string.Equals(
                    parameter.Value,
                    pair.Value,
                    StringComparison.Ordinal))
            {
                return
                    $"Threshold-correction parameter '{pair.Key}' is stale: sidecar '{pair.Value}', recipe '{parameter.Value}'.";
            }
        }

        return null;
    }

    private static InspectionRunThresholdCorrectionEvidence Result(
        InspectionRunThresholdCorrectionEvidenceState state,
        string message,
        string sidecarPath,
        string? sidecarSha256,
        ToolRecipeThresholdCorrectionEvidence? evidence) =>
        new(
            state,
            message,
            Path.GetFullPath(sidecarPath),
            sidecarSha256,
            evidence);

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
