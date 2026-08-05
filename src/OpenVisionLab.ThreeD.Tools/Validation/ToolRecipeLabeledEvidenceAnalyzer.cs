using OpenVisionLab.Vision3D.FeatureExtraction;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

public static class ToolRecipeLabeledEvidenceAnalyzer
{
    public static ToolRecipeLabeledEvidenceReport Analyze(
        ToolRecipeDocument document,
        ToolRecipeValidationSetResult result)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(result);
        var observations = CollectObservations(document, result);

        var distributions = observations
            .GroupBy(item => new
            {
                item.Scope,
                item.OwnerId,
                item.OwnerName,
                item.MetricName,
                item.Unit
            })
            .OrderBy(group => group.Key.Scope)
            .ThenBy(group => group.Key.OwnerId, StringComparer.Ordinal)
            .ThenBy(group => group.Key.MetricName, StringComparer.Ordinal)
            .Select(group => new ToolRecipeLabeledMetricDistribution(
                group.Key.Scope,
                group.Key.OwnerId,
                group.Key.OwnerName,
                group.Key.MetricName,
                group.Key.Unit,
                Calculate(group)))
            .ToArray();

        var good = result.Samples.Count(sample =>
            sample.Role == ToolRecipeValidationSampleRole.Good);
        var bad = result.Samples.Count(sample =>
            sample.Role == ToolRecipeValidationSampleRole.Bad);
        var heldOut = result.Samples.Count(sample =>
            sample.Role == ToolRecipeValidationSampleRole.HeldOut);
        var warnings = new List<string>();
        if (good == 0) warnings.Add("No Good sample is assigned.");
        if (bad == 0) warnings.Add("No Bad sample is assigned.");
        if (heldOut == 0)
        {
            warnings.Add(
                "No Held-out sample is assigned; future threshold teaching cannot prove a no-leakage replay.");
        }
        if (distributions.Length == 0)
        {
            warnings.Add(
                "No finite step or source-region metric observations were produced.");
        }

        var status = warnings.Count == 0
            ? ResultStatus.Pass
            : ResultStatus.Warning;
        return new ToolRecipeLabeledEvidenceReport(
            ToolRecipeLabeledEvidenceReport.CurrentContractVersion,
            status,
            $"{result.Samples.Count} labeled sample(s) | Good {good} | Bad {bad} | Held-out {heldOut} | distributions {distributions.Length}",
            good,
            bad,
            heldOut,
            distributions,
            warnings);
    }

    public static IReadOnlyList<ToolRecipeMetricObservation>
        CollectObservations(
            ToolRecipeDocument document,
            ToolRecipeValidationSetResult result)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(result);
        var observations = new List<ToolRecipeMetricObservation>();
        AddStepMetricObservations(result, observations);
        AddCompletenessPolicyObservations(document, result, observations);
        AddRegionMetricObservations(document, result, observations);
        return observations
            .OrderBy(observation => observation.Scope)
            .ThenBy(observation => observation.OwnerId, StringComparer.Ordinal)
            .ThenBy(observation => observation.MetricName, StringComparer.Ordinal)
            .ThenBy(observation => observation.Unit, StringComparer.Ordinal)
            .ThenBy(observation => observation.SampleOrder)
            .ToArray();
    }

    private static void AddCompletenessPolicyObservations(
        ToolRecipeDocument document,
        ToolRecipeValidationSetResult result,
        ICollection<ToolRecipeMetricObservation> observations)
    {
        var steps = document.Steps
            .Where(step => string.Equals(
                step.ToolId,
                "completeness-grid",
                StringComparison.Ordinal))
            .ToArray();
        foreach (var sample in result.Samples)
        foreach (var step in steps)
        {
            var executed = sample.Steps.FirstOrDefault(item =>
                string.Equals(item.StepId, step.Id, StringComparison.Ordinal));
            if (executed is null)
            {
                continue;
            }

            var coverage = executed.Metrics
                .Where(metric => double.IsFinite(metric.Value))
                .Select(metric =>
                {
                    var matched = C3DCompletenessMetricNames.TryGetCellId(
                        metric.Name,
                        C3DCompletenessMetricNames.FiniteCoverageSuffix,
                        out var cellId);
                    return (Metric: metric, Matched: matched, CellId: cellId);
                })
                .Where(item => item.Matched)
                .OrderBy(item => item.Metric.Value)
                .ThenBy(item => item.CellId, StringComparer.Ordinal)
                .FirstOrDefault();
            AddCompletenessObservation(
                sample,
                executed,
                coverage.Matched ? coverage.Metric : null,
                C3DCompletenessMetricNames.MinimumFiniteCoverage,
                coverage.CellId,
                observations);

            var relative = executed.Metrics
                .Where(metric => double.IsFinite(metric.Value))
                .Select(metric =>
                {
                    var matched = C3DCompletenessMetricNames.TryGetCellId(
                        metric.Name,
                        C3DCompletenessMetricNames.ReferenceRelativeMeanSuffix,
                        out var cellId);
                    return (Metric: metric, Matched: matched, CellId: cellId);
                })
                .Where(item => item.Matched)
                .ToArray();
            var minimum = relative
                .OrderBy(item => item.Metric.Value)
                .ThenBy(item => item.CellId, StringComparer.Ordinal)
                .FirstOrDefault();
            AddCompletenessObservation(
                sample,
                executed,
                minimum.Matched ? minimum.Metric : null,
                C3DCompletenessMetricNames.MinimumReferenceRelativeMean,
                minimum.CellId,
                observations);
            var maximum = relative
                .OrderByDescending(item => item.Metric.Value)
                .ThenBy(item => item.CellId, StringComparer.Ordinal)
                .FirstOrDefault();
            AddCompletenessObservation(
                sample,
                executed,
                maximum.Matched ? maximum.Metric : null,
                C3DCompletenessMetricNames.MaximumReferenceRelativeMean,
                maximum.CellId,
                observations);
        }
    }

    private static void AddCompletenessObservation(
        ToolRecipeValidationSampleResult sample,
        ToolRecipeValidationStepResult step,
        Metric? metric,
        string metricName,
        string cellId,
        ICollection<ToolRecipeMetricObservation> observations)
    {
        if (metric is null || string.IsNullOrWhiteSpace(cellId))
        {
            return;
        }

        observations.Add(new ToolRecipeMetricObservation(
            sample.Order,
            SampleIdentity(sample),
            sample.SourcePath,
            sample.Role,
            ToolRecipeEvidenceScope.StepMetric,
            step.StepId,
            step.ToolName,
            metricName,
            metric.Unit,
            metric.Value,
            cellId));
    }

    private static void AddStepMetricObservations(
        ToolRecipeValidationSetResult result,
        ICollection<ToolRecipeMetricObservation> observations)
    {
        foreach (var sample in result.Samples)
        {
            var identity = SampleIdentity(sample);
            foreach (var step in sample.Steps)
            foreach (var metric in step.Metrics.Where(metric =>
                         double.IsFinite(metric.Value)))
            {
                observations.Add(new ToolRecipeMetricObservation(
                    sample.Order,
                    identity,
                    sample.SourcePath,
                    sample.Role,
                    ToolRecipeEvidenceScope.StepMetric,
                    step.StepId,
                    step.ToolName,
                    metric.Name,
                    metric.Unit,
                    metric.Value));
            }
        }
    }

    private static void AddRegionMetricObservations(
        ToolRecipeDocument document,
        ToolRecipeValidationSetResult result,
        ICollection<ToolRecipeMetricObservation> observations)
    {
        var routedSelectionIds = document.Steps
            .SelectMany(step => step.InputEntityIds)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var regions = (document.Selections ?? [])
            .Where(selection =>
                routedSelectionIds.Contains(selection.Id)
                && selection.GridRectangle is not null
                && string.Equals(
                    selection.SourceBinding.Format,
                    "C3D",
                    StringComparison.OrdinalIgnoreCase))
            .OrderBy(selection => selection.Id, StringComparer.Ordinal)
            .ToArray();
        if (regions.Length == 0
            || document.Source.GridWidth is null
            || document.Source.GridHeight is null)
        {
            return;
        }

        foreach (var sample in result.Samples)
        {
            C3DHeightFieldSnapshot source;
            try
            {
                source = C3DHeightFieldSnapshot.LoadIdentified(
                    sample.SourcePath,
                    document.Source.Id,
                    document.Source.Unit,
                    document.Source.FrameId);
            }
            catch (Exception exception) when (
                exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or ArgumentException
                or NotSupportedException
                or OverflowException)
            {
                continue;
            }
            if (source.Width != document.Source.GridWidth
                || source.Height != document.Source.GridHeight)
            {
                continue;
            }

            var sourceValues = source.Values.ToArray();
            foreach (var selection in regions)
            {
                var rectangle = selection.GridRectangle!;
                var regionStatistics = new HeightMapRegionStatisticsTool()
                    .Execute(
                        source.Height,
                        source.Width,
                        sourceValues,
                        new HeightGridRegion(
                            rectangle.Row,
                            rectangle.Column,
                            rectangle.RowCount,
                            rectangle.ColumnCount));
                if (!regionStatistics.Success
                    || regionStatistics.FiniteCellCount == 0)
                {
                    continue;
                }

                var identity = SampleIdentity(sample);
                observations.Add(new ToolRecipeMetricObservation(
                    sample.Order,
                    identity,
                    sample.SourcePath,
                    sample.Role,
                    ToolRecipeEvidenceScope.RegionMetric,
                    selection.Id,
                    selection.Name,
                    "Mean raw height",
                    source.Unit,
                    regionStatistics.Mean));
                observations.Add(new ToolRecipeMetricObservation(
                    sample.Order,
                    identity,
                    sample.SourcePath,
                    sample.Role,
                    ToolRecipeEvidenceScope.RegionMetric,
                    selection.Id,
                    selection.Name,
                    "Valid cell ratio",
                    "ratio",
                    regionStatistics.FiniteCoverageRatio));
            }
        }
    }

    private static IReadOnlyList<ToolRecipeRoleMetricStatistics> Calculate(
        IEnumerable<ToolRecipeMetricObservation> observations)
    {
        var selected = observations
            .ToArray();
        var sdkResult = new LabeledEvidenceStatisticsTool().Execute(
            selected.Select(item =>
                new LabeledEvidenceStatisticsObservation(
                    $"{item.SampleOrder}:{item.SampleIdentity}",
                    ToSdkRole(item.Role),
                    item.Value))
                .ToArray());
        if (!sdkResult.Success)
        {
            throw new InvalidOperationException(sdkResult.Message);
        }

        return sdkResult.RoleStatistics
            .Select(statistics =>
            {
                var role = FromSdkRole(statistics.Role);
                return new ToolRecipeRoleMetricStatistics(
                    role,
                    statistics.SampleCount,
                    statistics.ValueCount,
                    statistics.Minimum,
                    statistics.Maximum,
                    statistics.Mean,
                    statistics.PopulationStandardDeviation,
                    role != ToolRecipeValidationSampleRole.HeldOut);
            })
            .ToArray();
    }

    private static LabeledEvidenceRole ToSdkRole(
        ToolRecipeValidationSampleRole role) => role switch
    {
        ToolRecipeValidationSampleRole.Good => LabeledEvidenceRole.Good,
        ToolRecipeValidationSampleRole.Bad => LabeledEvidenceRole.Bad,
        ToolRecipeValidationSampleRole.HeldOut => LabeledEvidenceRole.HeldOut,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };

    private static ToolRecipeValidationSampleRole FromSdkRole(
        LabeledEvidenceRole role) => role switch
    {
        LabeledEvidenceRole.Good => ToolRecipeValidationSampleRole.Good,
        LabeledEvidenceRole.Bad => ToolRecipeValidationSampleRole.Bad,
        LabeledEvidenceRole.HeldOut => ToolRecipeValidationSampleRole.HeldOut,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };

    private static string SampleIdentity(
        ToolRecipeValidationSampleResult sample) =>
        string.IsNullOrWhiteSpace(sample.SourceContentSha256)
            ? sample.SourcePath
            : sample.SourceContentSha256;
}
