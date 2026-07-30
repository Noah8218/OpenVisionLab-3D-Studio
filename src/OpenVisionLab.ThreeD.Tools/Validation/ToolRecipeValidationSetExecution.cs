using System.Diagnostics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record ToolRecipeValidationStepResult(
    int Order,
    string StepId,
    string ToolName,
    ResultStatus Status,
    string Evidence,
    IReadOnlyList<Metric> Metrics,
    IReadOnlyList<Overlay> Overlays);

public sealed record ToolRecipeValidationSampleResult(
    int Order,
    string SourcePath,
    string SourceContentSha256,
    ToolRecipeValidationSampleRole Role,
    ResultStatus Status,
    string Message,
    TimeSpan Duration,
    IReadOnlyList<ToolRecipeValidationStepResult> Steps);

public sealed record ToolRecipeValidationSetResult(
    ResultStatus Status,
    string Message,
    TimeSpan Duration,
    IReadOnlyList<ToolRecipeValidationSampleResult> Samples);

public sealed record ToolRecipeValidationProgress(
    int CompletedCount,
    int TotalCount,
    string CurrentSourcePath,
    ResultStatus? CompletedStatus);

public sealed record ToolRecipeValidationSampleInput(
    string SourcePath,
    ToolRecipeValidationSampleRole Role);

/// <summary>
/// Executes a taught recipe against an explicit, ordered set of same-grid C3D
/// samples without changing the authored recipe. Every sample goes through the
/// general ordered typed graph executor; unsupported tool IDs fail closed.
/// </summary>
public static class ToolRecipeValidationSetExecution
{
    public static bool CanExecute(ToolRecipeDocument document, out string message)
        => ToolRecipeOrderedGraphExecution.CanExecute(document, out message);

    public static ToolRecipeValidationSetResult Execute(
        ToolRecipeDocument document,
        IReadOnlyList<string> sourcePaths,
        CancellationToken cancellationToken = default,
        IProgress<ToolRecipeValidationProgress>? progress = null) =>
        Execute(
            document,
            sourcePaths
                .Select(path => new ToolRecipeValidationSampleInput(
                    path,
                    ToolRecipeValidationSampleRole.Good))
                .ToArray(),
            cancellationToken,
            progress);

    public static ToolRecipeValidationSetResult Execute(
        ToolRecipeDocument document,
        IReadOnlyList<ToolRecipeValidationSampleInput> sourceSamples,
        CancellationToken cancellationToken = default,
        IProgress<ToolRecipeValidationProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(sourceSamples);

        var stopwatch = Stopwatch.StartNew();
        if (!CanExecute(document, out var capabilityMessage))
        {
            return new ToolRecipeValidationSetResult(
                ResultStatus.Error,
                capabilityMessage,
                stopwatch.Elapsed,
                []);
        }

        var normalizedSamples = sourceSamples
            .Where(sample => !string.IsNullOrWhiteSpace(sample.SourcePath))
            .Select(sample => sample with
            {
                SourcePath = Path.GetFullPath(sample.SourcePath)
            })
            .ToArray();
        var conflictingRole = normalizedSamples
            .GroupBy(sample => sample.SourcePath, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Select(item => item.Role).Distinct().Count() > 1);
        if (conflictingRole is not null)
        {
            return new ToolRecipeValidationSetResult(
                ResultStatus.Error,
                $"Validation sample '{conflictingRole.Key}' has conflicting roles.",
                stopwatch.Elapsed,
                []);
        }
        var orderedSamples = normalizedSamples
            .DistinctBy(sample => sample.SourcePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (orderedSamples.Length == 0)
        {
            return new ToolRecipeValidationSetResult(
                ResultStatus.Error,
                "Validation Set has no C3D samples.",
                stopwatch.Elapsed,
                []);
        }

        var samples = new List<ToolRecipeValidationSampleResult>(orderedSamples.Length);
        for (var sampleIndex = 0; sampleIndex < orderedSamples.Length; sampleIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceSample = orderedSamples[sampleIndex];
            var sourcePath = sourceSample.SourcePath;
            progress?.Report(new ToolRecipeValidationProgress(
                sampleIndex,
                orderedSamples.Length,
                sourcePath,
                null));
            var sample = ExecuteSample(
                document,
                sourcePath,
                sourceSample.Role,
                sampleIndex + 1,
                cancellationToken);
            samples.Add(sample);
            progress?.Report(new ToolRecipeValidationProgress(
                sampleIndex + 1,
                orderedSamples.Length,
                sourcePath,
                sample.Status));
        }

        stopwatch.Stop();
        var status = Aggregate(samples.Select(sample => sample.Status));
        var passCount = samples.Count(sample => sample.Status == ResultStatus.Pass);
        var failCount = samples.Count(sample => sample.Status == ResultStatus.Fail);
        var errorCount = samples.Count(sample => sample.Status == ResultStatus.Error);
        return new ToolRecipeValidationSetResult(
            status,
            $"{samples.Count} sample(s) completed | Pass {passCount} | Fail {failCount} | Error {errorCount}",
            stopwatch.Elapsed,
            samples);
    }

    private static ToolRecipeValidationSampleResult ExecuteSample(
        ToolRecipeDocument document,
        string sourcePath,
        ToolRecipeValidationSampleRole role,
        int order,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var execution = ToolRecipeOrderedGraphExecution.Execute(
                document,
                sourcePath,
                cancellationToken);
            var steps = execution.Steps
                .Select(step => new ToolRecipeValidationStepResult(
                    step.Order,
                    step.StepId,
                    step.ToolName,
                    step.Result.Status,
                    step.Evidence,
                    step.Result.Metrics,
                    step.Result.Overlays))
                .ToArray();

            stopwatch.Stop();
            return new ToolRecipeValidationSampleResult(
                order,
                sourcePath,
                execution.SourceContentSha256,
                role,
                execution.Status,
                execution.Message,
                stopwatch.Elapsed,
                steps);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException
            or OverflowException)
        {
            stopwatch.Stop();
            return new ToolRecipeValidationSampleResult(
                order,
                sourcePath,
                string.Empty,
                role,
                ResultStatus.Error,
                exception.Message,
                stopwatch.Elapsed,
                []);
        }
    }

    private static ResultStatus Aggregate(IEnumerable<ResultStatus> statuses)
    {
        var statusArray = statuses.ToArray();
        if (statusArray.Contains(ResultStatus.Error)) return ResultStatus.Error;
        if (statusArray.Contains(ResultStatus.Fail)) return ResultStatus.Fail;
        if (statusArray.Contains(ResultStatus.Warning)) return ResultStatus.Warning;
        return ResultStatus.Pass;
    }
}
