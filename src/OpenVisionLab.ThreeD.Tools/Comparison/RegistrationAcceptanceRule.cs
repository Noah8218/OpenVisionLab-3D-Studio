using System.Diagnostics;
using OpenVisionLab.ThreeD.Core;
using Sdk = OpenVisionLab.Vision3D.FeatureExtraction;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record RegistrationAcceptancePolicy(
    string Unit,
    long MinimumCorrespondenceCount,
    double MinimumFitness,
    double MaximumInlierRmse,
    double MaximumTranslation,
    double MaximumRotationDegrees,
    double RigidTransformTolerance);

public sealed record RegistrationResultEvidence(
    string Unit,
    long SourcePointCount,
    long TargetPointCount,
    long CorrespondenceCount,
    double Fitness,
    double InlierRmse,
    IReadOnlyList<double> TransformRowMajor);

public enum RegistrationAcceptanceDecision
{
    Accepted,
    InvalidPolicy,
    InvalidEvidence,
    NoCorrespondences,
    InsufficientCorrespondences,
    InsufficientFitness,
    ExcessiveInlierRmse,
    NonRigidTransform,
    TranslationLimitExceeded,
    RotationLimitExceeded
}

public sealed record RegistrationAcceptanceEvaluation(
    ToolResult Result,
    RegistrationAcceptanceDecision Decision);

public static class RegistrationAcceptanceRule
{
    public const string ToolName = "Rigid Registration Acceptance";
    public const string EvaluationOrder =
        "CorrespondenceCount -> Fitness -> InlierRmse -> RigidTransform -> Translation -> Rotation";

    private const string CorrespondenceMetric = "Correspondence count";
    private const string FitnessMetric = "Fitness";
    private const string RmseMetric = "Inlier RMSE";
    private const string HomogeneousMetric = "Homogeneous row max error";
    private const string OrthogonalityMetric = "Rotation orthogonality max error";
    private const string DeterminantMetric = "Rotation determinant";
    private const string TranslationMetric = "Translation magnitude";
    private const string RotationMetric = "Rotation angle";
    private static readonly Sdk.RigidTransformDiagnosticsTool TransformDiagnostics = new();

    public static RegistrationAcceptanceEvaluation Evaluate(
        RegistrationAcceptancePolicy? policy,
        RegistrationResultEvidence? evidence)
    {
        var stopwatch = Stopwatch.StartNew();
        var metrics = CreateMetrics(policy, evidence);
        if (!TryValidatePolicy(policy, out var validationMessage))
        {
            MarkGateMetrics(metrics, ResultStatus.Error);
            return Create(
                ResultStatus.Error,
                RegistrationAcceptanceDecision.InvalidPolicy,
                validationMessage,
                stopwatch,
                metrics);
        }

        var validatedPolicy = policy!;
        if (!TryValidateEvidence(evidence, validatedPolicy, out validationMessage))
        {
            MarkGateMetrics(metrics, ResultStatus.Error);
            return Create(
                ResultStatus.Error,
                RegistrationAcceptanceDecision.InvalidEvidence,
                validationMessage,
                stopwatch,
                metrics);
        }

        var validatedEvidence = evidence!;
        if (validatedEvidence.CorrespondenceCount == 0)
        {
            SetStatus(metrics, CorrespondenceMetric, ResultStatus.Fail);
            return Create(
                ResultStatus.Fail,
                RegistrationAcceptanceDecision.NoCorrespondences,
                "Registration rejected: no correspondences; fitness and RMSE were not evaluated.",
                stopwatch,
                metrics);
        }

        if (validatedEvidence.CorrespondenceCount < validatedPolicy.MinimumCorrespondenceCount)
        {
            SetStatus(metrics, CorrespondenceMetric, ResultStatus.Fail);
            return Create(
                ResultStatus.Fail,
                RegistrationAcceptanceDecision.InsufficientCorrespondences,
                "Registration rejected: correspondence count is below the configured minimum; fitness and RMSE were not evaluated.",
                stopwatch,
                metrics);
        }

        SetStatus(metrics, CorrespondenceMetric, ResultStatus.Pass);
        if (validatedEvidence.Fitness < validatedPolicy.MinimumFitness)
        {
            SetStatus(metrics, FitnessMetric, ResultStatus.Fail);
            return Create(
                ResultStatus.Fail,
                RegistrationAcceptanceDecision.InsufficientFitness,
                "Registration rejected: fitness is below the configured minimum; RMSE was not evaluated.",
                stopwatch,
                metrics);
        }

        SetStatus(metrics, FitnessMetric, ResultStatus.Pass);
        if (validatedEvidence.InlierRmse > validatedPolicy.MaximumInlierRmse)
        {
            SetStatus(metrics, RmseMetric, ResultStatus.Fail);
            return Create(
                ResultStatus.Fail,
                RegistrationAcceptanceDecision.ExcessiveInlierRmse,
                "Registration rejected: inlier RMSE exceeds the configured maximum.",
                stopwatch,
                metrics);
        }

        SetStatus(metrics, RmseMetric, ResultStatus.Pass);
        if (!TryMeasureTransform(validatedEvidence.TransformRowMajor, out var transform, out validationMessage))
        {
            SetTransformStatuses(metrics, ResultStatus.Error);
            return Create(
                ResultStatus.Error,
                RegistrationAcceptanceDecision.InvalidEvidence,
                validationMessage,
                stopwatch,
                metrics);
        }

        SetValue(metrics, HomogeneousMetric, transform.HomogeneousRowMaximumError);
        SetValue(metrics, OrthogonalityMetric, transform.RotationOrthogonalityMaximumError);
        SetValue(metrics, DeterminantMetric, transform.RotationDeterminant);
        SetValue(metrics, TranslationMetric, transform.TranslationMagnitude);
        SetValue(metrics, RotationMetric, transform.RotationAngleDegrees);

        var homogeneousStatus = Status(transform.HomogeneousRowMaximumError, validatedPolicy.RigidTransformTolerance);
        var orthogonalityStatus = Status(transform.RotationOrthogonalityMaximumError, validatedPolicy.RigidTransformTolerance);
        var determinantStatus = Status(transform.RotationDeterminantUnitError, validatedPolicy.RigidTransformTolerance);
        SetStatus(metrics, HomogeneousMetric, homogeneousStatus);
        SetStatus(metrics, OrthogonalityMetric, orthogonalityStatus);
        SetStatus(metrics, DeterminantMetric, determinantStatus);
        if (homogeneousStatus == ResultStatus.Fail
            || orthogonalityStatus == ResultStatus.Fail
            || determinantStatus == ResultStatus.Fail)
        {
            return Create(
                ResultStatus.Fail,
                RegistrationAcceptanceDecision.NonRigidTransform,
                "Registration rejected: the estimated transform is not a plausible rigid homogeneous transform.",
                stopwatch,
                metrics);
        }

        var translationStatus = Status(transform.TranslationMagnitude, validatedPolicy.MaximumTranslation);
        SetStatus(metrics, TranslationMetric, translationStatus);
        if (translationStatus == ResultStatus.Fail)
        {
            return Create(
                ResultStatus.Fail,
                RegistrationAcceptanceDecision.TranslationLimitExceeded,
                "Registration rejected: translation magnitude exceeds the configured scenario limit.",
                stopwatch,
                metrics);
        }

        var rotationStatus = Status(transform.RotationAngleDegrees, validatedPolicy.MaximumRotationDegrees);
        SetStatus(metrics, RotationMetric, rotationStatus);
        if (rotationStatus == ResultStatus.Fail)
        {
            return Create(
                ResultStatus.Fail,
                RegistrationAcceptanceDecision.RotationLimitExceeded,
                "Registration rejected: rotation angle exceeds the configured scenario limit.",
                stopwatch,
                metrics);
        }

        return Create(
            ResultStatus.Pass,
            RegistrationAcceptanceDecision.Accepted,
            "Registration accepted: correspondence, fitness, RMSE, and transform plausibility limits passed.",
            stopwatch,
            metrics);
    }

    private static List<Metric> CreateMetrics(
        RegistrationAcceptancePolicy? policy,
        RegistrationResultEvidence? evidence)
    {
        var evidenceUnit = evidence?.Unit ?? policy?.Unit ?? string.Empty;
        var policyUnit = policy?.Unit ?? evidence?.Unit ?? string.Empty;
        return
        [
            new Metric("Source point count", MetricKind.Count, evidence?.SourcePointCount ?? double.NaN, "count"),
            new Metric("Target point count", MetricKind.Count, evidence?.TargetPointCount ?? double.NaN, "count"),
            new Metric(CorrespondenceMetric, MetricKind.Count, evidence?.CorrespondenceCount ?? double.NaN, "count", ResultStatus.NotRun),
            new Metric("Minimum correspondence count", MetricKind.Count, policy?.MinimumCorrespondenceCount ?? double.NaN, "count"),
            new Metric(FitnessMetric, MetricKind.Number, evidence?.Fitness ?? double.NaN, "ratio", ResultStatus.NotRun),
            new Metric("Minimum fitness", MetricKind.Number, policy?.MinimumFitness ?? double.NaN, "ratio"),
            new Metric(RmseMetric, MetricKind.Deviation, evidence?.InlierRmse ?? double.NaN, evidenceUnit, ResultStatus.NotRun),
            new Metric("Maximum inlier RMSE", MetricKind.Deviation, policy?.MaximumInlierRmse ?? double.NaN, policyUnit),
            new Metric(HomogeneousMetric, MetricKind.Number, double.NaN, "ratio", ResultStatus.NotRun),
            new Metric(OrthogonalityMetric, MetricKind.Number, double.NaN, "ratio", ResultStatus.NotRun),
            new Metric(DeterminantMetric, MetricKind.Number, double.NaN, "ratio", ResultStatus.NotRun),
            new Metric("Rigid transform tolerance", MetricKind.Number, policy?.RigidTransformTolerance ?? double.NaN, "ratio"),
            new Metric(TranslationMetric, MetricKind.Length, double.NaN, evidenceUnit, ResultStatus.NotRun),
            new Metric("Maximum translation", MetricKind.Length, policy?.MaximumTranslation ?? double.NaN, policyUnit),
            new Metric(RotationMetric, MetricKind.Angle, double.NaN, "degree", ResultStatus.NotRun),
            new Metric("Maximum rotation", MetricKind.Angle, policy?.MaximumRotationDegrees ?? double.NaN, "degree")
        ];
    }

    private static bool TryValidatePolicy(
        RegistrationAcceptancePolicy? policy,
        out string message)
    {
        if (policy is null
            || string.IsNullOrWhiteSpace(policy.Unit)
            || policy.MinimumCorrespondenceCount <= 0
            || !double.IsFinite(policy.MinimumFitness)
            || policy.MinimumFitness <= 0.0
            || policy.MinimumFitness > 1.0
            || !double.IsFinite(policy.MaximumInlierRmse)
            || policy.MaximumInlierRmse < 0.0
            || !double.IsFinite(policy.MaximumTranslation)
            || policy.MaximumTranslation < 0.0
            || !double.IsFinite(policy.MaximumRotationDegrees)
            || policy.MaximumRotationDegrees < 0.0
            || policy.MaximumRotationDegrees > 180.0
            || !double.IsFinite(policy.RigidTransformTolerance)
            || policy.RigidTransformTolerance <= 0.0)
        {
            message = "Registration acceptance policy requires explicit units and finite positive correspondence/fitness guards, non-negative RMSE/translation/rotation limits, and a positive rigid-transform tolerance.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static bool TryValidateEvidence(
        RegistrationResultEvidence? evidence,
        RegistrationAcceptancePolicy policy,
        out string message)
    {
        if (evidence is null
            || string.IsNullOrWhiteSpace(evidence.Unit)
            || !string.Equals(evidence.Unit, policy.Unit, StringComparison.Ordinal)
            || evidence.SourcePointCount <= 0
            || evidence.TargetPointCount <= 0
            || evidence.CorrespondenceCount < 0
            || evidence.CorrespondenceCount > evidence.SourcePointCount
            || !double.IsFinite(evidence.Fitness)
            || evidence.Fitness < 0.0
            || evidence.Fitness > 1.0
            || !double.IsFinite(evidence.InlierRmse)
            || evidence.InlierRmse < 0.0)
        {
            message = "Registration evidence requires matching explicit units, positive source/target counts, a valid correspondence count, fitness in [0,1], and a finite non-negative inlier RMSE.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static bool TryMeasureTransform(
        IReadOnlyList<double>? values,
        out Sdk.RigidTransformDiagnosticsResult measures,
        out string message)
    {
        measures = TransformDiagnostics.Execute(values!);
        message = measures.Message;
        return measures.Success;
    }

    private static RegistrationAcceptanceEvaluation Create(
        ResultStatus status,
        RegistrationAcceptanceDecision decision,
        string message,
        Stopwatch stopwatch,
        IReadOnlyList<Metric> metrics)
    {
        stopwatch.Stop();
        return new RegistrationAcceptanceEvaluation(
            new ToolResult(ToolName, status, message, stopwatch.Elapsed, metrics.ToArray(), []),
            decision);
    }

    private static void MarkGateMetrics(List<Metric> metrics, ResultStatus status)
    {
        foreach (var name in new[]
        {
            CorrespondenceMetric,
            FitnessMetric,
            RmseMetric,
            HomogeneousMetric,
            OrthogonalityMetric,
            DeterminantMetric,
            TranslationMetric,
            RotationMetric
        })
        {
            SetStatus(metrics, name, status);
        }
    }

    private static void SetTransformStatuses(List<Metric> metrics, ResultStatus status)
    {
        SetStatus(metrics, HomogeneousMetric, status);
        SetStatus(metrics, OrthogonalityMetric, status);
        SetStatus(metrics, DeterminantMetric, status);
    }

    private static void SetStatus(List<Metric> metrics, string name, ResultStatus status)
    {
        var index = metrics.FindIndex(metric => metric.Name == name);
        metrics[index] = metrics[index] with { Status = status };
    }

    private static void SetValue(List<Metric> metrics, string name, double value)
    {
        var index = metrics.FindIndex(metric => metric.Name == name);
        metrics[index] = metrics[index] with { Value = value };
    }

    private static ResultStatus Status(double actual, double maximum) =>
        actual <= maximum ? ResultStatus.Pass : ResultStatus.Fail;

}
