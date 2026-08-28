using System.Diagnostics;
using System.Globalization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DConnectedRegionEvaluation(
    ToolResult Result,
    C3DConnectedRegionArtifact? Output);

/// <summary>
/// Product adapter for the typed Connected Region step. The upstream
/// Remove Outlier Pixels output and mask are already verified artifacts; this
/// adapter owns recipe policy, result evidence, and Core projection.
/// Connected-region labeling and metric arithmetic remain in Data/SDK.
/// </summary>
public static class ToolRecipeConnectedRegionExecution
{
    private static readonly string[] ParameterNames =
    [
        "Connectivity",
        "OriginX",
        "OriginY",
        "ColumnPitch",
        "RowPitch",
        "AreaUnit"
    ];

    public static C3DConnectedRegionEvaluation Execute(
        ToolRecipeDocument document,
        string stepId,
        C3DHeightFieldSnapshot source,
        C3DOutlierCellMap mask,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(mask);
        var stopwatch = Stopwatch.StartNew();

        var validation = ToolRecipeValidator.ValidateForStepExecution(document, stepId);
        if (!validation.IsValid)
        {
            return Error(string.Join(" ", validation.Errors));
        }

        var matching = document.Steps
            .Where(step => string.Equals(step.Id, stepId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matching.Length != 1)
        {
            return Error($"Recipe must contain exactly one step with ID '{stepId}'.");
        }

        var step = matching[0];
        if (!string.Equals(step.ToolId, "connected-region", StringComparison.Ordinal))
        {
            return Error($"Step '{step.Id}' is not the Connected Region v1 adapter.");
        }

        try
        {
            if (step.InputEntityIds.Count != 1
                || !string.Equals(
                    step.InputEntityIds[0],
                    source.EntityId,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Connected Region v1 requires the supplied source to match its first input entity.");
            }

            if (source.Width != mask.Width || source.Height != mask.Height)
            {
                throw new InvalidDataException(
                    "Connected Region v1 requires the filtered height field and outlier mask to share one grid.");
            }

            var parameters = ParseParameters(step);
            cancellationToken.ThrowIfCancellationRequested();
            var analysis = C3DConnectedRegionAnalyzer.AnalyzeOutlierMask(
                mask,
                parameters.Connectivity,
                cancellationToken);
            if (!analysis.Success)
            {
                throw new InvalidDataException(analysis.Message);
            }

            var metrics = C3DConnectedRegionAnalyzer.AnalyzeOutlierMaskMetrics(
                mask,
                parameters.Connectivity,
                new C3DConnectedRegionMetricsOptions
                {
                    OriginX = parameters.OriginX,
                    OriginY = parameters.OriginY,
                    ColumnPitch = parameters.ColumnPitch,
                    RowPitch = parameters.RowPitch,
                    AreaUnit = parameters.AreaUnit
                },
                cancellationToken);
            if (!metrics.Success)
            {
                throw new InvalidDataException(metrics.Message);
            }

            var artifact = C3DConnectedRegionArtifactFactory.Create(
                step.OutputEntityId,
                step.ToolName,
                source,
                mask,
                analysis,
                parameters.Connectivity,
                metrics,
                parameters.OriginX,
                parameters.OriginY,
                parameters.ColumnPitch,
                parameters.RowPitch,
                parameters.AreaUnit);
            stopwatch.Stop();
            return new(
                new ToolResult(
                    "Connected Region",
                    ResultStatus.Pass,
                    "Completed - connected regions were published as a typed artifact; source data remains unchanged.",
                    stopwatch.Elapsed,
                    [
                        new Metric("Connected region count", MetricKind.Count, artifact.Regions.Count, "count"),
                        new Metric("Foreground cell count", MetricKind.Count, analysis.ForegroundCellCount, "count"),
                        new Metric("Largest region cell count", MetricKind.Count, artifact.Regions.Count == 0 ? 0 : artifact.Regions.Max(region => region.CellCount), "count")
                    ],
                    [
                        new Overlay(
                            $"connected-region.{artifact.ArtifactId}",
                            OverlayKind.ColorMap,
                            $"Connected regions: {artifact.Regions.Count:N0} | artifact {artifact.ContentSha256[..12]}",
                            SourceEntityId: source.EntityId)
                    ]),
                artifact);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidDataException or OverflowException)
        {
            stopwatch.Stop();
            return Error(exception.Message, stopwatch.Elapsed);
        }
    }

    internal static (
        C3DConnectedRegionConnectivity Connectivity,
        double OriginX,
        double OriginY,
        double ColumnPitch,
        double RowPitch,
        string AreaUnit) ParseParameters(ToolRecipeStep step)
    {
        var parameters = step.Parameters ?? [];
        if (ParameterNames.Any(
                name => parameters.Count(parameter => parameter.Name == name) != 1))
        {
            throw new InvalidDataException(
                "Connected Region v1 requires one value for every recognized parameter.");
        }

        string Value(string name) =>
            parameters.Single(parameter => parameter.Name == name).Value;

        var connectivity = Value("Connectivity") switch
        {
            C3DConnectedRegionArtifact.FourConnectivity => C3DConnectedRegionConnectivity.Four,
            C3DConnectedRegionArtifact.EightConnectivity => C3DConnectedRegionConnectivity.Eight,
            _ => throw new InvalidDataException(
                "Connected Region v1 Connectivity must be Four or Eight.")
        };
        if (!double.TryParse(Value("OriginX"), NumberStyles.Float, CultureInfo.InvariantCulture, out var originX)
            || !double.TryParse(Value("OriginY"), NumberStyles.Float, CultureInfo.InvariantCulture, out var originY)
            || !double.TryParse(Value("ColumnPitch"), NumberStyles.Float, CultureInfo.InvariantCulture, out var columnPitch)
            || !double.TryParse(Value("RowPitch"), NumberStyles.Float, CultureInfo.InvariantCulture, out var rowPitch)
            || !double.IsFinite(originX)
            || !double.IsFinite(originY)
            || !double.IsFinite(columnPitch)
            || !double.IsFinite(rowPitch)
            || columnPitch <= 0d
            || rowPitch <= 0d
            || string.IsNullOrWhiteSpace(Value("AreaUnit")))
        {
            throw new InvalidDataException(
                "Connected Region v1 coordinate and area parameters must be finite, positive where applicable, and non-empty.");
        }

        return (connectivity, originX, originY, columnPitch, rowPitch, Value("AreaUnit").Trim());
    }

    private static C3DConnectedRegionEvaluation Error(
        string message,
        TimeSpan? elapsed = null) => new(
        new ToolResult(
            "Connected Region",
            ResultStatus.Error,
            message,
            elapsed ?? TimeSpan.Zero,
            [],
            []),
        null);
}
