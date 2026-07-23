using System.Diagnostics;
using System.Globalization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record ToolRecipeOrderedGraphStepResult(
    int Order,
    string StepId,
    string ToolId,
    string ToolName,
    string OutputEntityId,
    string? OutputContentSha256,
    ToolResult Result,
    string Evidence);

public sealed record ToolRecipeOrderedGraphExecutionResult(
    ResultStatus Status,
    string Message,
    TimeSpan Duration,
    string SourceContentSha256,
    ToolRecipeDocument ReboundDocument,
    IReadOnlyList<ToolRecipeOrderedGraphStepResult> Steps);

/// <summary>
/// Replays the currently executable typed recipe graph against one explicit
/// same-grid C3D sample. The authored document is immutable: raw-source and
/// generated A3 selection identities are rebound only in the returned
/// per-sample document.
/// </summary>
public static class ToolRecipeOrderedGraphExecution
{
    private static readonly HashSet<string> SupportedToolIds = new(StringComparer.Ordinal)
    {
        "filter",
        "height-difference-edge",
        "two-point-line",
        "three-point-plane",
        "datum-plane-raw-height-deviation",
        "three-d-line-fit",
        "line-intersection",
        "landmark-correspondence",
        "xyz-affine-solve",
        "xyz-affine-apply",
        "re-grid-height-map",
        "thickness",
        "warpage",
        "plane-flatness",
        "point-pair-dimensions",
        "gap-flush",
        "volume",
        "cross-section-dimensions"
    };

    private static readonly HashSet<string> MeasurementToolIds = new(StringComparer.Ordinal)
    {
        "thickness",
        "warpage",
        "plane-flatness",
        "point-pair-dimensions",
        "gap-flush",
        "volume",
        "cross-section-dimensions"
    };

    public static bool CanExecute(ToolRecipeDocument document, out string message)
    {
        ArgumentNullException.ThrowIfNull(document);
        var validation = ToolRecipeValidator.Validate(document);
        if (!validation.IsValid)
        {
            message = string.Join(" ", validation.Errors);
            return false;
        }
        if (!string.Equals(document.Source.Format, "C3D", StringComparison.OrdinalIgnoreCase))
        {
            message = "Ordered graph replay currently requires one recipe-bound C3D source.";
            return false;
        }
        if (document.Steps.Count == 0)
        {
            message = "Ordered graph replay requires at least one taught inspection step.";
            return false;
        }

        var unsupported = document.Steps.FirstOrDefault(step => !SupportedToolIds.Contains(step.ToolId));
        if (unsupported is not null)
        {
            message =
                $"Ordered graph replay does not support step '{unsupported.Id}' ({unsupported.ToolName}). "
                + "No executable typed adapter is registered for that tool.";
            return false;
        }

        message =
            $"Ready to replay {document.Steps.Count} authored typed step(s) in recipe order "
            + "against same-grid C3D samples.";
        return true;
    }

    public static ToolRecipeOrderedGraphExecutionResult Execute(
        ToolRecipeDocument document,
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var stopwatch = Stopwatch.StartNew();
        if (!CanExecute(document, out var capabilityMessage))
        {
            return Error(document, string.Empty, capabilityMessage, stopwatch.Elapsed, []);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = Path.GetFullPath(sourcePath);
            if (!File.Exists(path)) throw new FileNotFoundException("Validation sample does not exist.", path);

            var identity = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(path);
            if (document.Source.GridWidth is not { } expectedWidth
                || document.Source.GridHeight is not { } expectedHeight)
            {
                throw new InvalidDataException("Recipe source grid identity is incomplete.");
            }
            if (identity.GridWidth != expectedWidth || identity.GridHeight != expectedHeight)
            {
                throw new InvalidDataException(
                    $"Grid mismatch. Recipe expects {expectedWidth} x {expectedHeight}; "
                    + $"sample is {identity.GridWidth} x {identity.GridHeight}.");
            }

            var file = new FileInfo(path);
            var snapshot = C3DHeightFieldSnapshot.LoadVerified(
                path,
                document.Source.Id,
                document.Source.Unit,
                document.Source.FrameId,
                file.Length,
                identity.ContentSha256,
                identity.GridWidth,
                identity.GridHeight);
            var rebound = RebindRawSource(document, path, snapshot);
            var artifacts = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                [rebound.Source.Id] = snapshot
            };
            var steps = new List<ToolRecipeOrderedGraphStepResult>(rebound.Steps.Count);

            for (var index = 0; index < rebound.Steps.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var step = rebound.Steps[index];
                var execution = ExecuteStep(rebound, step, artifacts, cancellationToken);
                steps.Add(new ToolRecipeOrderedGraphStepResult(
                    index + 1,
                    step.Id,
                    step.ToolId,
                    step.ToolName,
                    step.OutputEntityId,
                    OutputContentSha256(execution.Output),
                    execution.Result,
                    Evidence(execution.Result, execution.Output)));

                if (execution.Output is not null)
                {
                    artifacts[step.OutputEntityId] = execution.Output;
                    if (execution.Output is C3DTransformedHeightField field)
                    {
                        rebound = RebindTransformedSelections(rebound, field);
                    }
                }

                if (execution.Result.Status is ResultStatus.Error or ResultStatus.NotRun
                    || execution.Output is null)
                {
                    stopwatch.Stop();
                    return Error(
                        rebound,
                        identity.ContentSha256,
                        $"Step {index + 1} '{step.ToolName}' stopped ordered replay: {execution.Result.Message}",
                        stopwatch.Elapsed,
                        steps);
                }
                if (execution.Output is C3DTransformedHeightField transformed
                    && !transformed.MeetsMinimumCoverage)
                {
                    stopwatch.Stop();
                    return Error(
                        rebound,
                        identity.ContentSha256,
                        $"Step {index + 1} '{step.ToolName}' did not meet its authored Publish coverage gate.",
                        stopwatch.Elapsed,
                        steps);
                }
            }

            stopwatch.Stop();
            var status = Aggregate(steps.Select(step => step.Result.Status));
            return new ToolRecipeOrderedGraphExecutionResult(
                status,
                $"Executed {steps.Count} authored typed step(s) in recipe order.",
                stopwatch.Elapsed,
                identity.ContentSha256,
                rebound,
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
            return Error(document, string.Empty, exception.Message, stopwatch.Elapsed, []);
        }
    }

    private static StepExecution ExecuteStep(
        ToolRecipeDocument document,
        ToolRecipeStep step,
        IReadOnlyDictionary<string, object> artifacts,
        CancellationToken cancellationToken)
    {
        switch (step.ToolId)
        {
            case "filter":
            {
                var evaluation = ToolRecipeFilterExecution.Execute(document, step.Id, cancellationToken: cancellationToken);
                return new(evaluation.Result, evaluation.Output);
            }
            case "height-difference-edge":
            {
                var input = Required<C3DHeightFieldSnapshot>(artifacts, step.InputEntityIds[0], step);
                var evaluation = ToolRecipeHeightDifferenceEdgeExecution.Execute(document, step.Id, input, cancellationToken);
                return new(evaluation.Result, evaluation.Output);
            }
            case "two-point-line":
            {
                var evaluation = ToolRecipeTwoPointLineExecution.Execute(document, step.Id, cancellationToken: cancellationToken);
                return new(evaluation.Result, evaluation.Output);
            }
            case "three-point-plane":
            {
                var evaluation = ToolRecipeThreePointPlaneExecution.Execute(document, step.Id, cancellationToken: cancellationToken);
                return new(evaluation.Result, evaluation.Output);
            }
            case "datum-plane-raw-height-deviation":
            {
                var plane = Required<C3DThreePointPlaneFeature>(artifacts, step.InputEntityIds[1], step);
                var evaluation = ToolRecipeDatumPlaneDeviationExecution.Execute(document, step.Id, plane, cancellationToken: cancellationToken);
                return new(evaluation.Result, evaluation.Output);
            }
            case "three-d-line-fit":
            {
                var input = Required<C3DHeightDifferenceEdgePointSet>(artifacts, step.InputEntityIds[0], step);
                var evaluation = ToolRecipeLineFitExecution.Execute(document, step.Id, input, cancellationToken);
                return new(evaluation.Result, evaluation.Output);
            }
            case "line-intersection":
            {
                var first = Required<IC3DLineGeometry>(artifacts, step.InputEntityIds[0], step);
                var second = Required<IC3DLineGeometry>(artifacts, step.InputEntityIds[1], step);
                var evaluation = ToolRecipeLineIntersectionExecution.Execute(document, step.Id, first, second, cancellationToken);
                return new(evaluation.Result, evaluation.Output);
            }
            case "landmark-correspondence":
            {
                var anchors = artifacts.Values.OfType<C3DLineIntersectionFeature>().ToArray();
                var evaluation = ToolRecipeLandmarkCorrespondenceExecution.Execute(document, step.Id, anchors, cancellationToken);
                return new(evaluation.Result, evaluation.Output);
            }
            case "xyz-affine-solve":
            {
                var input = Required<C3DLandmarkCorrespondenceSet>(artifacts, step.InputEntityIds[0], step);
                var evaluation = ToolRecipeXYZAffineSolveExecution.Execute(document, step.Id, input, cancellationToken);
                return new(evaluation.Result, evaluation.Output);
            }
            case "xyz-affine-apply":
            {
                var input = Required<C3DAffineTransform3D>(artifacts, step.InputEntityIds[1], step);
                var evaluation = ToolRecipeXYZAffineApplyExecution.Execute(document, step.Id, input, cancellationToken: cancellationToken);
                return new(evaluation.Result, evaluation.Output);
            }
            case "re-grid-height-map":
            {
                var input = Required<C3DTransformedPointCloud>(artifacts, step.InputEntityIds[0], step);
                var evaluation = ToolRecipeRegridHeightFieldExecution.Execute(document, step.Id, input, cancellationToken);
                return new(evaluation.Result, evaluation.Output);
            }
            case var measurementToolId when MeasurementToolIds.Contains(measurementToolId):
            {
                C3DTransformedHeightField? field = null;
                if (step.InputEntityIds.Count > 0
                    && artifacts.TryGetValue(step.InputEntityIds[0], out var inputArtifact))
                {
                    field = inputArtifact as C3DTransformedHeightField;
                }
                var evaluation = ToolRecipeHeightMeasurementExecution.Execute(
                    document, step.Id, field, recipeDirectory: null, cancellationToken);
                return new(evaluation.Result, evaluation.Output);
            }
            default:
                return new(
                    new ToolResult(
                        step.ToolName,
                        ResultStatus.Error,
                        $"No executable typed adapter is registered for tool '{step.ToolId}'.",
                        TimeSpan.Zero,
                        [],
                        []),
                    null);
        }
    }

    private static T Required<T>(
        IReadOnlyDictionary<string, object> artifacts,
        string entityId,
        ToolRecipeStep step)
        where T : class
    {
        if (!artifacts.TryGetValue(entityId, out var artifact))
        {
            throw new InvalidDataException(
                $"Step '{step.Id}' is waiting for input entity '{entityId}', which was not published by an earlier step.");
        }
        if (artifact is not T typed)
        {
            throw new InvalidDataException(
                $"Step '{step.Id}' requires input '{entityId}' as {typeof(T).Name}, "
                + $"but the published artifact is {artifact.GetType().Name}.");
        }
        return typed;
    }

    private static ToolRecipeDocument RebindRawSource(
        ToolRecipeDocument document,
        string sourcePath,
        C3DHeightFieldSnapshot snapshot)
    {
        var selections = (document.Selections ?? [])
            .Select(selection =>
            {
                if (!string.Equals(selection.RootSourceId, document.Source.Id, StringComparison.OrdinalIgnoreCase))
                {
                    return selection;
                }
                if (string.Equals(selection.SourceBinding.Format, "TransformedHeightField", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(selection.SourceBinding.OwnerEntityId))
                {
                    return selection with
                    {
                        SourceBinding = selection.SourceBinding with
                        {
                            RootSourceContentSha256 = snapshot.ContentSha256
                        }
                    };
                }
                if (!string.Equals(selection.SourceBinding.Format, "C3D", StringComparison.OrdinalIgnoreCase)
                    || !string.IsNullOrWhiteSpace(selection.SourceBinding.OwnerEntityId))
                {
                    return selection;
                }

                var points = selection.Points?.Select(point =>
                {
                    var locator = point.Locator;
                    if (locator.Row < 0 || locator.Row >= snapshot.Height
                        || locator.Column < 0 || locator.Column >= snapshot.Width)
                    {
                        throw new InvalidDataException(
                            $"Selection '{selection.Id}' contains an out-of-grid point ({locator.Row}, {locator.Column}).");
                    }
                    var height = snapshot.Values.Span[locator.Row * snapshot.Width + locator.Column];
                    if (!double.IsFinite(height))
                    {
                        throw new InvalidDataException(
                            $"Selection '{selection.Id}' point ({locator.Row}, {locator.Column}) is missing in the validation sample.");
                    }
                    return point with
                    {
                        CapturedPosition = new ToolRecipeXyz(locator.Column, height, locator.Row),
                        RawHeight = height
                    };
                }).ToArray();

                return selection with
                {
                    SourceBinding = selection.SourceBinding with
                    {
                        ContentSha256 = snapshot.ContentSha256,
                        GridWidth = snapshot.Width,
                        GridHeight = snapshot.Height
                    },
                    Points = points
                };
            })
            .ToArray();

        return document with
        {
            Source = document.Source with
            {
                Path = sourcePath,
                ByteLength = snapshot.ByteLength,
                ContentSha256 = snapshot.ContentSha256,
                GridWidth = snapshot.Width,
                GridHeight = snapshot.Height
            },
            Selections = selections
        };
    }

    private static ToolRecipeDocument RebindTransformedSelections(
        ToolRecipeDocument document,
        C3DTransformedHeightField field)
    {
        var binding = ToolRecipeSelectionSourceBindingVerifier.FromTransformedHeightField(field);
        var selections = (document.Selections ?? [])
            .Select(selection =>
            {
                if (!string.Equals(selection.SourceBinding.Format, "TransformedHeightField", StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(selection.SourceBinding.OwnerEntityId, field.OutputEntityId, StringComparison.OrdinalIgnoreCase))
                {
                    return selection;
                }
                return selection with
                {
                    FrameId = field.ReferenceFrameId,
                    SourceBinding = binding
                };
            })
            .ToArray();
        return document with { Selections = selections };
    }

    private static string Evidence(ToolResult result, object? output)
    {
        if (output is ToolRecipeHeightMeasurementOutput measurement)
        {
            return measurement.EvidenceSummary;
        }
        if (result.Metrics.Count == 0) return result.Message;
        var metrics = string.Join(
            " | ",
            result.Metrics.Take(6).Select(metric =>
                $"{metric.Name}={metric.Value.ToString("G6", CultureInfo.InvariantCulture)} {metric.Unit}".TrimEnd()));
        return $"{result.Message} | {metrics}";
    }

    private static string? OutputContentSha256(object? output) => output switch
    {
        C3DHeightFieldSnapshot value => value.ContentSha256,
        C3DHeightDifferenceEdgePointSet value => value.ContentSha256,
        C3DTwoPointLineFeature value => value.ContentSha256,
        C3DThreePointPlaneFeature value => value.ContentSha256,
        C3DDatumPlaneDeviationFeature value => value.ContentSha256,
        C3DLineFeature value => value.ContentSha256,
        C3DLineIntersectionFeature value => value.ContentSha256,
        C3DLandmarkCorrespondenceSet value => value.ContentSha256,
        C3DAffineTransform3D value => value.ContentSha256,
        C3DTransformedPointCloud value => value.ContentSha256,
        C3DTransformedHeightField value => value.ContentSha256,
        ToolRecipeHeightMeasurementOutput value => value.ContentSha256,
        _ => null
    };

    private static ToolRecipeOrderedGraphExecutionResult Error(
        ToolRecipeDocument document,
        string sourceContentSha256,
        string message,
        TimeSpan duration,
        IReadOnlyList<ToolRecipeOrderedGraphStepResult> steps) =>
        new(
            ResultStatus.Error,
            message,
            duration,
            sourceContentSha256,
            document,
            steps);

    private static ResultStatus Aggregate(IEnumerable<ResultStatus> statuses)
    {
        var values = statuses.ToArray();
        if (values.Contains(ResultStatus.Error) || values.Contains(ResultStatus.NotRun)) return ResultStatus.Error;
        if (values.Contains(ResultStatus.Fail)) return ResultStatus.Fail;
        if (values.Contains(ResultStatus.Warning)) return ResultStatus.Warning;
        return ResultStatus.Pass;
    }

    private sealed record StepExecution(ToolResult Result, object? Output);
}
