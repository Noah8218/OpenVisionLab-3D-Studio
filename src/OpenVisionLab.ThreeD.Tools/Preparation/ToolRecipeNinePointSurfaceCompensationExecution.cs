using System.Globalization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.Vision3D.FeatureExtraction;
using OpenVisionLab.Vision3D.Geometry;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DNinePointSurfaceCompensationEvaluation(
    ToolResult Result,
    C3DHeightFieldSnapshot? Output,
    NinePointSurfaceCompensationResult? Numerical);

/// <summary>Recipe identity, parameter and C3D persistence boundary. All surface arithmetic belongs to the SDK.</summary>
public static class ToolRecipeNinePointSurfaceCompensationExecution
{
    public const string ToolId = "nine-point-surface-compensation";
    public const string Semantics = NinePointSurfaceCompensationTool.Semantics;
    public const string ToolName = "Nine-point surface compensation";
    private static readonly string[] ParameterNames = ["MiddleRow", "MiddleColumn", "TargetHeight", "Semantics"];

    public static C3DNinePointSurfaceCompensationEvaluation Execute(ToolRecipeDocument document, string stepId,
        C3DHeightFieldSnapshot? source = null, string? recipeDirectory = null, CancellationToken cancellationToken = default)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            ArgumentNullException.ThrowIfNull(document);
            var validation = ToolRecipeValidator.ValidateForStepExecution(document, stepId);
            if (!validation.IsValid) throw new InvalidDataException(string.Join(" ", validation.Errors));
            var step = document.Steps.Single(candidate => string.Equals(candidate.Id, stepId, StringComparison.OrdinalIgnoreCase));
            if (step.ToolId != ToolId || step.InputEntityIds.Count != 1
                || string.IsNullOrWhiteSpace(step.OutputEntityId) || step.InputEntityIds.Contains(step.OutputEntityId, StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException("Nine-point compensation requires one height field and a distinct output ID.");

            if (source is null)
            {
                if (!string.Equals(step.InputEntityIds[0], document.Source.Id, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The selected height-field output is not current. Run its producer first.");
                var descriptor = document.Source;
                var path = Path.IsPathFullyQualified(descriptor.Path) ? descriptor.Path
                    : Path.GetFullPath(Path.Combine(recipeDirectory ?? Environment.CurrentDirectory, descriptor.Path));
                source = C3DHeightFieldSnapshot.LoadVerified(path, descriptor.Id, descriptor.Unit, descriptor.FrameId,
                    descriptor.ByteLength ?? 0, descriptor.ContentSha256 ?? string.Empty, descriptor.GridWidth ?? 0, descriptor.GridHeight ?? 0,
                    cancellationToken);
            }
            if (!string.Equals(step.InputEntityIds[0], source.EntityId, StringComparison.OrdinalIgnoreCase)
                || source.Unit != document.Source.Unit || source.FrameId != document.Source.FrameId
                || !(string.Equals(source.RootSourceSha256, document.Source.ContentSha256, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(source.EntityId, document.Source.Id, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(source.ContentSha256, document.Source.ContentSha256, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Nine-point input must retain the recipe source, declared height unit and XY grid frame.");

            var options = ParseOptions(step, source);
            var map = new HeightMap3D(source.Height, source.Width, source.GridOriginColumn, source.GridOriginRow,
                1.0, 1.0, source.Values.ToArray(), "grid-index", source.Unit, source.FrameId, source.EntityId);
            var numerical = new NinePointSurfaceCompensationTool().Execute(map, options, cancellationToken);
            if (!numerical.Success)
                return Error(numerical.Message, started.Elapsed, numerical);

            // The existing codec rejects finite zero/underflow instead of losing substrate cells on save/reopen.
            var output = source.CreateDerived(step.OutputEntityId, numerical.Output.CopyValues(),
                $"{step.Id}:{Semantics}:source={source.ContentSha256}:target={options.TargetHeight.ToString("G17", CultureInfo.InvariantCulture)}");
            cancellationToken.ThrowIfCancellationRequested();
            var metrics = new List<Metric>
            {
                new("Reference peak-to-valley", MetricKind.Deviation, numerical.ReferencePeakToValley, source.Unit, ResultStatus.Pass),
                new("Reference RMS", MetricKind.Deviation, numerical.ReferenceRms, source.Unit, ResultStatus.Pass),
                new("Target height", MetricKind.Number, options.TargetHeight, source.Unit, ResultStatus.Pass),
                new("Reference points", MetricKind.Count, numerical.ControlPoints.Count, "point", ResultStatus.Pass)
            };
            for (var index = 0; index < numerical.ControlPoints.Count; index++)
            {
                var point = numerical.ControlPoints[index];
                var prefix = $"Reference {index + 1}";
                metrics.Add(new($"{prefix} row", MetricKind.Count, point.Row, "cell", ResultStatus.Pass));
                metrics.Add(new($"{prefix} column", MetricKind.Count, point.Column, "cell", ResultStatus.Pass));
                metrics.Add(new($"{prefix} X", MetricKind.Number, point.X, "grid-index", ResultStatus.Pass));
                metrics.Add(new($"{prefix} Y", MetricKind.Number, point.Y, "grid-index", ResultStatus.Pass));
                metrics.Add(new($"{prefix} height", MetricKind.Number, point.Height, source.Unit, ResultStatus.Pass));
                metrics.Add(new($"{prefix} plane", MetricKind.Number, point.ReferencePlaneHeight, source.Unit, ResultStatus.Pass));
                metrics.Add(new($"{prefix} residual", MetricKind.Deviation, point.PlaneResidual, source.Unit, ResultStatus.Pass));
                metrics.Add(new($"{prefix} compensated", MetricKind.Number, point.CompensatedHeight, source.Unit, ResultStatus.Pass));
            }
            started.Stop();
            return new(new ToolResult(ToolName, ResultStatus.Pass, string.Empty, started.Elapsed, metrics, []), output, numerical);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or InvalidDataException or IOException or UnauthorizedAccessException or OverflowException)
        {
            return Error(exception.Message, started.Elapsed);
        }
    }

    public static NinePointSurfaceCompensationOptions ParseOptions(ToolRecipeStep step, C3DHeightFieldSnapshot source)
    {
        var parameters = step.Parameters ?? [];
        if (parameters.Count != ParameterNames.Length || ParameterNames.Any(name => parameters.Count(p => p.Name == name) != 1)
            || parameters.Single(p => p.Name == "Semantics").Value != Semantics)
            throw new InvalidDataException("Nine-point parameters or model semantics do not match this tool version.");
        string Get(string name) => parameters.Single(p => p.Name == name).Value;
        if (!int.TryParse(Get("MiddleRow"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var row)
            || !int.TryParse(Get("MiddleColumn"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var column)
            || !double.TryParse(Get("TargetHeight"), NumberStyles.Float, CultureInfo.InvariantCulture, out var height) || !double.IsFinite(height))
            throw new InvalidDataException("Middle row/column must be integers and target height must be finite.");
        return new NinePointSurfaceCompensationOptions
        {
            ReferenceRows = [0, row, source.Height - 1],
            ReferenceColumns = [0, column, source.Width - 1],
            TargetHeight = height,
            OutputFrameId = source.FrameId,
            OutputSourceId = step.OutputEntityId
        };
    }

    private static C3DNinePointSurfaceCompensationEvaluation Error(string message, TimeSpan elapsed,
        NinePointSurfaceCompensationResult? numerical = null) =>
        new(new ToolResult(ToolName, ResultStatus.Error, message, elapsed, [], []), null, numerical);
}
