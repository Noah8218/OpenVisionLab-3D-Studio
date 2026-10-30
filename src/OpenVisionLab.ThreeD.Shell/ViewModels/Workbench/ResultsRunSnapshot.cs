using System.Globalization;
using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Viewer.Localization;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>Immutable display data from a saved run; never reloads or rejudges its measurements.</summary>
public sealed record ResultsRunSnapshot(
    string RunId,
    DateTimeOffset RecordedAt,
    string RecipeName,
    string RecipeVersion,
    string RecipeSha256,
    string SourcePath,
    string SourceEntityId,
    string SourceSha256,
    string Unit,
    string FrameId,
    ResultStatus Status,
    string Message,
    double ElapsedMilliseconds,
    int StepCount,
    int PassCount,
    int FailCount,
    int ErrorCount,
    IReadOnlyList<ResultsStoredMetric> Metrics,
    IReadOnlyList<ResultsStoredFailure> Failures)
{
    public string SourceName => Path.GetFileName(SourcePath);
    public string RecordedAtText => RecordedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
    public string RecipeIdentity => $"{RecipeName} · {RecipeVersion} · {ShortHash(RecipeSha256)}";
    public string DecisionSummary => $"OK {PassCount} · NG {FailCount} · Error {ErrorCount} · {Status}";

    public static ResultsRunSnapshot? FromRecord(InspectionRunRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!InspectionRunRecord.SupportsSchemaVersion(record.SchemaVersion)
            || record.Recipe is null || record.Source is null || record.Metrics is null
            || string.IsNullOrWhiteSpace(record.Source.Path)
            || record.Steps?.Any(step => step is null || step.Metrics is null) == true) return null;
        var metrics = record.Steps is { Count: > 0 } steps
            ? steps.SelectMany(step => step.Metrics.Select(metric => CreateMetric(
                step.Id, step.ToolName, step.SemanticFingerprint ?? record.Recipe.Sha256, step.Status, metric))).ToArray()
            : record.Metrics.Select(metric => CreateMetric(
                record.Step?.Id ?? record.ToolName, record.ToolName, record.Recipe.Sha256, record.Status, metric)).ToArray();
        var failures = record.Steps?.Where(step => step.Status is ResultStatus.Fail or ResultStatus.Error)
            .Select(step => new ResultsStoredFailure(step.ToolName, step.Status, step.Message,
                string.Join(" · ", step.Metrics.Select(metric => $"{metric.Name} {metric.Value:0.##} {metric.Unit}".TrimEnd()))))
            .ToArray() ?? [];
        if (failures.Length == 0 && record.Status is ResultStatus.Fail or ResultStatus.Error)
            failures = [new ResultsStoredFailure(record.ToolName, record.Status, record.Message, string.Empty)];
        var statuses = record.Steps is { Count: > 0 } savedSteps
            ? savedSteps.Select(step => step.Status).ToArray() : [record.Status];
        return new ResultsRunSnapshot(record.RunId, record.RecordedAtUtc, Path.GetFileName(record.Recipe.Path),
            record.Recipe.Version, record.Recipe.Sha256, record.Source.Path, record.Source.EntityId,
            record.Source.Sha256, record.Source.Unit, record.Source.FrameId ?? record.Source.EntityId, record.Status,
            record.Message, record.ElapsedMilliseconds, record.Steps?.Count ?? (record.Step is null ? 0 : 1),
            statuses.Count(status => status == ResultStatus.Pass), statuses.Count(status => status == ResultStatus.Fail),
            statuses.Count(status => status == ResultStatus.Error), metrics, failures);
    }

    private static ResultsStoredMetric CreateMetric(string stepId, string toolName, string identity,
        ResultStatus executionStatus, InspectionRunMetric metric) =>
        new($"{stepId}\u001F{metric.Name}\u001F{metric.Kind}\u001F{metric.Unit}",
            stepId, toolName, metric.Name, metric.Kind, metric.Value, metric.Unit, metric.Status, executionStatus, identity);

    private static string ShortHash(string hash) => hash.Length > 10 ? hash[..10] : hash;
}

public sealed record ResultsStoredMetric(
    string Key, string StepId, string ToolName, string Name, MetricKind Kind, double Value,
    string Unit, ResultStatus? Status, ResultStatus ExecutionStatus, string SettingsIdentity)
{
    public bool IsFinite => double.IsFinite(Value);
    public string DisplayName => ViewerLocalization.Shared.LocalizeRuntimeText(Name, "MeasurementMetric");
    public string ValueText => IsFinite ? Value.ToString("0.##", CultureInfo.CurrentCulture) : "—";
    public string StatusText => Status?.ToString() ?? "—";
}

public sealed record ResultsStoredFailure(string ToolName, ResultStatus Status, string Message, string ValueText);
public sealed record ResultsMeasurementOption(string Key, string DisplayName, string Unit);
public sealed record ResultsTrendPoint(RunRecordRecentItem Record, ResultsRunSnapshot Snapshot, ResultsStoredMetric Metric)
{
    public string RecordedAt => Snapshot.RecordedAtText;
    public string RecipeIdentity => Snapshot.RecipeIdentity;
    public string Value => Metric.ValueText;
    public string Unit => Metric.Unit;
    public string Status => Metric.StatusText;
    public int ValueCount => 1;
    public int FailCount => Metric.Status == ResultStatus.Fail ? 1 : 0;
}

public sealed record ResultsMeasurementStatistics(string RecipeIdentity, string SettingsIdentity, string FrameId, string Unit,
    int ValueCount, double? Mean, double? Minimum, double? Maximum, double? SampleStandardDeviation, int FailCount, int ErrorCount)
{
    public string MeanText => Format(Mean);
    public string MinimumText => Format(Minimum);
    public string MaximumText => Format(Maximum);
    public string SigmaText => Format(SampleStandardDeviation);
    private static string Format(double? value) => value is { } number && double.IsFinite(number) ? number.ToString("0.##", CultureInfo.CurrentCulture) : "—";
}

public enum ResultsHistorySection
{
    Trend,
    Failures,
    Report
}
