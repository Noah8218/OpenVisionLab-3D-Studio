using System.Diagnostics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record ToolRecipeValidationStepResult(
    int Order,
    string StepId,
    string ToolName,
    ResultStatus Status,
    string Evidence);

public sealed record ToolRecipeValidationSampleResult(
    int Order,
    string SourcePath,
    string SourceContentSha256,
    ResultStatus Status,
    string Message,
    TimeSpan Duration,
    IReadOnlyList<ToolRecipeValidationStepResult> Steps);

public sealed record ToolRecipeValidationSetResult(
    ResultStatus Status,
    string Message,
    TimeSpan Duration,
    IReadOnlyList<ToolRecipeValidationSampleResult> Samples);

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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(sourcePaths);

        var stopwatch = Stopwatch.StartNew();
        if (!CanExecute(document, out var capabilityMessage))
        {
            return new ToolRecipeValidationSetResult(
                ResultStatus.Error,
                capabilityMessage,
                stopwatch.Elapsed,
                []);
        }

        var orderedPaths = sourcePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (orderedPaths.Length == 0)
        {
            return new ToolRecipeValidationSetResult(
                ResultStatus.Error,
                "Validation Set has no C3D samples.",
                stopwatch.Elapsed,
                []);
        }

        var samples = new List<ToolRecipeValidationSampleResult>(orderedPaths.Length);
        for (var sampleIndex = 0; sampleIndex < orderedPaths.Length; sampleIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            samples.Add(ExecuteSample(document, orderedPaths[sampleIndex], sampleIndex + 1, cancellationToken));
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
                    step.Evidence))
                .ToArray();

            stopwatch.Stop();
            return new ToolRecipeValidationSampleResult(
                order,
                sourcePath,
                execution.SourceContentSha256,
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
