using System.Diagnostics;
using OpenVisionLab.ThreeD.Core;

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

        SetValue(metrics, HomogeneousMetric, transform.HomogeneousRowMaxError);
        SetValue(metrics, OrthogonalityMetric, transform.RotationOrthogonalityMaxError);
        SetValue(metrics, DeterminantMetric, transform.RotationDeterminant);
        SetValue(metrics, TranslationMetric, transform.TranslationMagnitude);
        SetValue(metrics, RotationMetric, transform.RotationAngleDegrees);

        var homogeneousStatus = Status(transform.HomogeneousRowMaxError, validatedPolicy.RigidTransformTolerance);
        var orthogonalityStatus = Status(transform.RotationOrthogonalityMaxError, validatedPolicy.RigidTransformTolerance);
        var determinantStatus = Status(Math.Abs(transform.RotationDeterminant - 1.0), validatedPolicy.RigidTransformTolerance);
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
        out TransformMeasures measures,
        out string message)
    {
        measures = default;
        if (values is null || values.Count != 16 || values.Any(value => !double.IsFinite(value)))
        {
            message = "Registration transform must contain 16 finite row-major float64 values.";
            return false;
        }

        var homogeneousRowMaxError = new[]
        {
            Math.Abs(values[12]),
            Math.Abs(values[13]),
            Math.Abs(values[14]),
            Math.Abs(values[15] - 1.0)
        }.Max();
        var rotationRows = new[]
        {
            new Vector3d(values[0], values[1], values[2]),
            new Vector3d(values[4], values[5], values[6]),
            new Vector3d(values[8], values[9], values[10])
        };
        var orthogonalityMaxError = 0.0;
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                var expected = row == column ? 1.0 : 0.0;
                orthogonalityMaxError = Math.Max(
                    orthogonalityMaxError,
                    Math.Abs(rotationRows[row].Dot(rotationRows[column]) - expected));
            }
        }

        var determinant =
            values[0] * (values[5] * values[10] - values[6] * values[9])
            - values[1] * (values[4] * values[10] - values[6] * values[8])
            + values[2] * (values[4] * values[9] - values[5] * values[8]);
        var translationMagnitude = Math.Sqrt(
            values[3] * values[3]
            + values[7] * values[7]
            + values[11] * values[11]);
        var cosine = Math.Clamp((values[0] + values[5] + values[10] - 1.0) / 2.0, -1.0, 1.0);
        var rotationAngleDegrees = Math.Acos(cosine) * 180.0 / Math.PI;
        if (!double.IsFinite(homogeneousRowMaxError)
            || !double.IsFinite(orthogonalityMaxError)
            || !double.IsFinite(determinant)
            || !double.IsFinite(translationMagnitude)
            || !double.IsFinite(rotationAngleDegrees))
        {
            message = "Registration transform produced non-finite plausibility metrics.";
            return false;
        }

        measures = new TransformMeasures(
            homogeneousRowMaxError,
            orthogonalityMaxError,
            determinant,
            translationMagnitude,
            rotationAngleDegrees);
        message = string.Empty;
        return true;
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

    private readonly record struct Vector3d(double X, double Y, double Z)
    {
        public double Dot(Vector3d other) => X * other.X + Y * other.Y + Z * other.Z;
    }

    private readonly record struct TransformMeasures(
        double HomogeneousRowMaxError,
        double RotationOrthogonalityMaxError,
        double RotationDeterminant,
        double TranslationMagnitude,
        double RotationAngleDegrees);
}
