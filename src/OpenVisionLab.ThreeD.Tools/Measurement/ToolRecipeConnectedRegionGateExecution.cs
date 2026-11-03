using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Recipe adapter for the bounded G-13/G-14 raw-region gate. It owns input
/// identity and recipe lifecycle only; the numeric gate remains in the typed
/// C3DConnectedRegionGateRule/Data/SDK path.
/// </summary>
public static class ToolRecipeConnectedRegionGateExecution
{
    public static bool TryPrepare(
        ToolRecipeDocument document,
        string stepId,
        C3DHeightFieldSnapshot? publishedRawSource,
        C3DHeightFieldSnapshot? publishedDetectionSource,
        C3DConnectedRegionArtifact? publishedRegionArtifact,
        string? recipeDirectory,
        out string message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(document);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
            var validation = ToolRecipeValidator.ValidateForStepExecution(document, stepId);
            if (!validation.IsValid)
            {
                message = string.Join(" ", validation.Errors);
                return false;
            }

            var step = document.Steps.Single(candidate =>
                string.Equals(candidate.Id, stepId, StringComparison.OrdinalIgnoreCase));
            if (step.ToolId != "connected-region-gate")
            {
                message = $"Step '{step.Id}' is not a Connected Region Coverage/Height Gate step.";
                return false;
            }

            var raw = ResolveRawSource(
                document,
                step,
                publishedRawSource,
                recipeDirectory,
                cancellationToken);
            var detection = RequireDetection(step, publishedDetectionSource);
            var region = RequireRegion(step, publishedRegionArtifact);
            var policy = C3DConnectedRegionGatePolicy.FromRecipeParameters(
                step.Parameters ?? [],
                raw.Unit);
            ValidateProvenance(raw, detection, region, policy);
            message = $"{step.ToolName} v1 is ready from the exact raw source, Published detection snapshot, and ConnectedRegionArtifact.";
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or InvalidDataException
            or InvalidOperationException
            or OverflowException)
        {
            message = exception.Message;
            return false;
        }
    }

    public static ToolRecipeHeightMeasurementEvaluation Execute(
        ToolRecipeDocument document,
        string stepId,
        C3DHeightFieldSnapshot? publishedRawSource,
        C3DHeightFieldSnapshot? publishedDetectionSource,
        C3DConnectedRegionArtifact? publishedRegionArtifact,
        string? recipeDirectory,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (!TryPrepare(
                    document,
                    stepId,
                    publishedRawSource,
                    publishedDetectionSource,
                    publishedRegionArtifact,
                    recipeDirectory,
                    out var message,
                    cancellationToken))
            {
                return Error(message);
            }

            var step = document.Steps.Single(candidate =>
                string.Equals(candidate.Id, stepId, StringComparison.OrdinalIgnoreCase));
            var raw = ResolveRawSource(
                document,
                step,
                publishedRawSource,
                recipeDirectory,
                cancellationToken);
            var detection = RequireDetection(step, publishedDetectionSource);
            var region = RequireRegion(step, publishedRegionArtifact);
            var policy = C3DConnectedRegionGatePolicy.FromRecipeParameters(
                step.Parameters ?? [],
                raw.Unit);
            var evaluation = C3DConnectedRegionGateRule.Evaluate(
                new C3DConnectedRegionGateInput(
                    step.Id,
                    step.OutputEntityId,
                    document.Source.Id,
                    raw,
                    detection,
                    region,
                    policy),
                cancellationToken);
            stopwatch.Stop();
            var result = step.ApplyAcceptancePolicy(evaluation.Result) with { Elapsed = stopwatch.Elapsed };
            var outputHash = evaluation.Output is { } artifact && step.AcceptanceLimitsConfigured == false
                ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{artifact.ContentSha256}|AcceptanceLimitsConfigured:False")))
                : evaluation.Output?.ContentSha256;
            var output = evaluation.Output is null
                ? null
                : new ToolRecipeHeightMeasurementOutput(
                    step.OutputEntityId,
                    document.Source.Id,
                    raw.EntityId,
                    region.ArtifactId,
                    raw.Unit,
                    raw.FrameId,
                    outputHash!,
                    result,
                    step.AcceptanceLimitsConfigured == false ? result.Message : evaluation.Output.Message,
                    ConnectedRegionGate: evaluation.Output,
                    SemanticFingerprint: outputHash);
            return new ToolRecipeHeightMeasurementEvaluation(
                result,
                output);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or InvalidDataException
            or InvalidOperationException
            or OverflowException)
        {
            stopwatch.Stop();
            return Error(exception.Message);
        }
    }

    private static C3DHeightFieldSnapshot ResolveRawSource(
        ToolRecipeDocument document,
        ToolRecipeStep step,
        C3DHeightFieldSnapshot? publishedRawSource,
        string? recipeDirectory,
        CancellationToken cancellationToken)
    {
        var source = document.Source;
        if (!string.Equals(source.Format, "C3D", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(step.InputEntityIds[0], source.Id, StringComparison.OrdinalIgnoreCase)
            || source.ByteLength is not { } byteLength
            || string.IsNullOrWhiteSpace(source.ContentSha256)
            || source.GridWidth is not { } width
            || source.GridHeight is not { } height)
        {
            throw new InvalidDataException("Connected Region Coverage/Height Gate requires a complete recipe-bound raw C3D source identity.");
        }

        if (publishedRawSource is not null)
        {
            if (publishedRawSource.IsDerived
                || !string.Equals(publishedRawSource.EntityId, source.Id, StringComparison.OrdinalIgnoreCase)
                || publishedRawSource.ByteLength != byteLength
                || !string.Equals(publishedRawSource.ContentSha256, source.ContentSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(publishedRawSource.RootSourceSha256, source.ContentSha256, StringComparison.OrdinalIgnoreCase)
                || publishedRawSource.Width != width
                || publishedRawSource.Height != height
                || !string.Equals(publishedRawSource.Unit, source.Unit, StringComparison.Ordinal)
                || !string.Equals(publishedRawSource.FrameId, source.FrameId, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Connected Region Coverage/Height Gate raw source identity does not match the recipe source.");
            }

            return publishedRawSource;
        }

        var path = Path.IsPathFullyQualified(source.Path)
            ? Path.GetFullPath(source.Path)
            : Path.GetFullPath(Path.Combine(recipeDirectory ?? Environment.CurrentDirectory, source.Path));
        return C3DHeightFieldSnapshot.LoadVerified(
            path,
            source.Id,
            source.Unit,
            source.FrameId,
            byteLength,
            source.ContentSha256,
            width,
            height,
            cancellationToken);
    }

    private static C3DHeightFieldSnapshot RequireDetection(
        ToolRecipeStep step,
        C3DHeightFieldSnapshot? detection) =>
        detection is not null
            && string.Equals(detection.EntityId, step.InputEntityIds[1], StringComparison.OrdinalIgnoreCase)
            ? detection
            : throw new InvalidDataException(
                $"Connected Region Coverage/Height Gate is waiting for Published detection HeightField '{step.InputEntityIds.ElementAtOrDefault(1)}'.");

    private static C3DConnectedRegionArtifact RequireRegion(
        ToolRecipeStep step,
        C3DConnectedRegionArtifact? region) =>
        region is not null
            && string.Equals(region.ArtifactId, step.InputEntityIds[2], StringComparison.OrdinalIgnoreCase)
            ? region
            : throw new InvalidDataException(
                $"Connected Region Coverage/Height Gate is waiting for ConnectedRegionArtifact '{step.InputEntityIds.ElementAtOrDefault(2)}'.");

    private static void ValidateProvenance(
        C3DHeightFieldSnapshot raw,
        C3DHeightFieldSnapshot detection,
        C3DConnectedRegionArtifact region,
        C3DConnectedRegionGatePolicy policy)
    {
        if (raw.Width != detection.Width
            || raw.Height != detection.Height
            || raw.Width != region.GridWidth
            || raw.Height != region.GridHeight
            || !string.Equals(detection.EntityId, region.SourceEntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(detection.ContentSha256, region.SourceContentSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(detection.RootSourceSha256, region.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(raw.RootSourceSha256, region.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(raw.Unit, region.Unit, StringComparison.Ordinal)
            || !string.Equals(raw.FrameId, region.FrameId, StringComparison.Ordinal)
            || !string.Equals(policy.HeightUnit, raw.Unit, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Connected Region Coverage/Height Gate raw, detection, region, unit, or frame provenance is stale or mismatched.");
        }
    }

    private static ToolRecipeHeightMeasurementEvaluation Error(string message) =>
        new(
            new ToolResult(
                "Connected Region Coverage/Height Gate",
                ResultStatus.Error,
                message,
                TimeSpan.Zero,
                [],
                []),
            null);
}
