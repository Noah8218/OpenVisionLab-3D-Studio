using System.Diagnostics;
using NoahAffineCorrespondence = Lib.ThreeD.FeatureExtraction.FullXyzAffineCorrespondence;
using NoahAffineOptions = Lib.ThreeD.FeatureExtraction.FullXyzAffineSolveOptions;
using NoahAffineResidual = Lib.ThreeD.FeatureExtraction.FullXyzAffineResidual;
using NoahAffineSolver = Lib.ThreeD.FeatureExtraction.FullXyzAffineSolveTool;
using NoahPoint = Lib.ThreeD.FeatureExtraction.ThreeDPoint;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Studio typed adapter for Library-Noah's deterministic exact-four
/// source-to-reference affine solve. The result is matrix evidence only; it
/// never applies the matrix to C3D data.
/// </summary>
public sealed record C3DAffineSolveInput(
    string StepId,
    string OutputEntityId,
    C3DLandmarkCorrespondenceSet PublishedCorrespondenceSet,
    double MaximumConditionEstimate,
    double ArithmeticResidualWarning);

public sealed record C3DAffineSolveEvaluation(
    ToolResult Result,
    C3DAffineTransform3D? Output);

public static class C3DAffineSolveRule
{
    private const int RequiredPairCount = 4;

    public static C3DAffineSolveEvaluation Evaluate(
        C3DAffineSolveInput input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            Validate(input);
            cancellationToken.ThrowIfCancellationRequested();
            var pairs = input.PublishedCorrespondenceSet.Pairs;
            var noahResult = new NoahAffineSolver().Execute(
                pairs.Select(pair => new NoahAffineCorrespondence(
                    new NoahPoint(pair.SourceX, pair.SourceY, pair.SourceZ),
                    new NoahPoint(pair.ReferenceX, pair.ReferenceY, pair.ReferenceZ))).ToArray(),
                new NoahAffineOptions
                {
                    MaximumConditionEstimate = input.MaximumConditionEstimate,
                    ArithmeticResidualWarning = input.ArithmeticResidualWarning
                },
                cancellationToken);
            if (!noahResult.Success)
            {
                throw new InvalidDataException(noahResult.Message);
            }
            if (noahResult.Matrix is null || noahResult.Residuals.Count != RequiredPairCount)
            {
                throw new InvalidDataException("Library-Noah Full XYZ affine solve returned incomplete matrix evidence.");
            }

            var matrix = new C3DAffineMatrix3x4(
                noahResult.Matrix.M11, noahResult.Matrix.M12, noahResult.Matrix.M13, noahResult.Matrix.M14,
                noahResult.Matrix.M21, noahResult.Matrix.M22, noahResult.Matrix.M23, noahResult.Matrix.M24,
                noahResult.Matrix.M31, noahResult.Matrix.M32, noahResult.Matrix.M33, noahResult.Matrix.M34);
            var residuals = pairs.Select((pair, index) => CreateResidual(pair, noahResult.Residuals[index])).ToArray();
            EnsureFinite(
                matrix.Values
                    .Append(noahResult.SourceAugmentedDeterminant)
                    .Append(noahResult.LinearDeterminantAbsolute)
                    .Append(noahResult.ConditionEstimate)
                    .Append(noahResult.ArithmeticMaximumResidual)
                    .Append(noahResult.ArithmeticRmsResidual),
                "Library-Noah affine evidence");
            var warning = noahResult.ArithmeticResidualWarningExceeded;
            var output = C3DAffineTransform3D.Create(
                input.OutputEntityId,
                input.PublishedCorrespondenceSet,
                matrix,
                noahResult.SourceAugmentedDeterminant,
                noahResult.LinearDeterminantAbsolute,
                noahResult.ConditionEstimate,
                input.MaximumConditionEstimate,
                noahResult.ArithmeticRmsResidual,
                noahResult.ArithmeticMaximumResidual,
                input.ArithmeticResidualWarning,
                residuals,
                $"{input.StepId}:XYZAffineSolve:{C3DAffineTransform3D.ContractVersion}:policy=ExactFourPartialPivot:input={input.PublishedCorrespondenceSet.ContentSha256}");
            stopwatch.Stop();
            var message = warning
                ? "Completed - arithmetic residual exceeds the authored review threshold; this is solve evidence, not an inspection decision."
                : "Completed - exact-four source-to-reference affine matrix evidence only; no C3D point was moved.";
            return new C3DAffineSolveEvaluation(
                new ToolResult(
                    "XYZ Affine Solve",
                    ResultStatus.Pass,
                    message,
                    stopwatch.Elapsed,
                    [
                        new Metric("Correspondence count", MetricKind.Count, RequiredPairCount, "count"),
                        new Metric("Source condition estimate", MetricKind.Number, noahResult.ConditionEstimate, "ratio"),
                        new Metric("Absolute linear determinant", MetricKind.Number, noahResult.LinearDeterminantAbsolute, "ratio"),
                        new Metric("Arithmetic RMS residual", MetricKind.Deviation, noahResult.ArithmeticRmsResidual, input.PublishedCorrespondenceSet.ReferenceUnit),
                        new Metric("Arithmetic maximum residual", MetricKind.Deviation, noahResult.ArithmeticMaximumResidual, input.PublishedCorrespondenceSet.ReferenceUnit, warning ? ResultStatus.Warning : ResultStatus.Pass)
                    ],
                    residuals.Select(residual => new Overlay(
                        $"{input.OutputEntityId}.{residual.ReferenceLandmarkId}",
                        OverlayKind.Polyline,
                        $"{residual.SourceOutputRole} residual",
                        warning ? ResultStatus.Warning : ResultStatus.Pass,
                        input.PublishedCorrespondenceSet.RootSourceEntityId)).ToArray()),
                output);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or OverflowException or ArithmeticException)
        {
            stopwatch.Stop();
            return new C3DAffineSolveEvaluation(
                new ToolResult("XYZ Affine Solve", ResultStatus.Error, exception.Message, stopwatch.Elapsed, [], []), null);
        }
    }

    private static void Validate(C3DAffineSolveInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
        ArgumentNullException.ThrowIfNull(input.PublishedCorrespondenceSet);
        if (!double.IsFinite(input.MaximumConditionEstimate) || input.MaximumConditionEstimate <= 0d)
        {
            throw new InvalidDataException("MaximumConditionEstimate must be an explicit finite positive number.");
        }
        if (!double.IsFinite(input.ArithmeticResidualWarning) || input.ArithmeticResidualWarning < 0d)
        {
            throw new InvalidDataException("ArithmeticResidualWarning must be an explicit finite non-negative number.");
        }
        var correspondence = input.PublishedCorrespondenceSet;
        if (correspondence.Pairs.Count != RequiredPairCount
            || correspondence.SourceRank != RequiredPairCount
            || correspondence.ReferenceRank != RequiredPairCount
            || correspondence.SourceNormalizedTetrahedronVolume <= correspondence.MinimumNormalizedTetrahedronVolume
            || correspondence.ReferenceNormalizedTetrahedronVolume <= correspondence.MinimumNormalizedTetrahedronVolume)
        {
            throw new InvalidDataException("XYZ Affine Solve requires one current exact-four affine-independent CorrespondenceSet.");
        }
        if (string.Equals(input.OutputEntityId, correspondence.OutputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("XYZ Affine Solve output ID must differ from its CorrespondenceSet input ID.");
        }
        if (string.IsNullOrWhiteSpace(correspondence.ContentSha256)
            || string.IsNullOrWhiteSpace(correspondence.RootSourceSha256)
            || !Finite(correspondence.Pairs.SelectMany(pair => new[]
            {
                pair.SourceX, pair.SourceY, pair.SourceZ, pair.ReferenceX, pair.ReferenceY, pair.ReferenceZ
            })))
        {
            throw new InvalidDataException("XYZ Affine Solve requires finite correspondence coordinates and immutable source identity.");
        }
        if (correspondence.Pairs.Select(pair => pair.SourceEntityId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != RequiredPairCount
            || correspondence.Pairs.Select(pair => pair.ReferenceLandmarkId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != RequiredPairCount
            || correspondence.Pairs.Select(pair => (pair.SourceX, pair.SourceY, pair.SourceZ)).Distinct().Count() != RequiredPairCount
            || correspondence.Pairs.Select(pair => (pair.ReferenceX, pair.ReferenceY, pair.ReferenceZ)).Distinct().Count() != RequiredPairCount)
        {
            throw new InvalidDataException("XYZ Affine Solve requires four distinct source and reference landmark identities and coordinates.");
        }
    }

    private static C3DAffineLandmarkResidual CreateResidual(
        C3DLandmarkCorrespondencePair pair,
        NoahAffineResidual residual)
    {
        return new C3DAffineLandmarkResidual(
            pair.SourceEntityId, pair.SourceOutputRole, pair.SourceContentSha256, pair.ReferenceLandmarkId,
            pair.SourceX, pair.SourceY, pair.SourceZ,
            pair.ReferenceX, pair.ReferenceY, pair.ReferenceZ,
            residual.Transformed.X, residual.Transformed.Y, residual.Transformed.Z,
            residual.Residual.X, residual.Residual.Y, residual.Residual.Z,
            residual.ResidualNorm);
    }

    private static bool Finite(IEnumerable<double> values) => values.All(double.IsFinite);

    private static void EnsureFinite(IEnumerable<double> values, string label)
    {
        if (!Finite(values)) throw new InvalidDataException($"{label} contains a non-finite value.");
    }
}
