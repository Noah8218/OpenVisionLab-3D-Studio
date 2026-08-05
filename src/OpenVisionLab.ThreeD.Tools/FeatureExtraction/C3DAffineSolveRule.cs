using System.Diagnostics;
using SdkAffineCorrespondence = OpenVisionLab.Vision3D.FeatureExtraction.FullXyzAffineCorrespondence;
using SdkAffineOptions = OpenVisionLab.Vision3D.FeatureExtraction.FullXyzAffineSolveOptions;
using SdkAffineResidual = OpenVisionLab.Vision3D.FeatureExtraction.FullXyzAffineResidual;
using SdkAffineSolver = OpenVisionLab.Vision3D.FeatureExtraction.FullXyzAffineSolveTool;
using SdkPoint = OpenVisionLab.Vision3D.FeatureExtraction.ThreeDPoint;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Studio typed adapter for OpenVisionLab Vision SDK's deterministic exact-four
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
            var sdkResult = new SdkAffineSolver().Execute(
                pairs.Select(pair => new SdkAffineCorrespondence(
                    new SdkPoint(pair.SourceX, pair.SourceY, pair.SourceZ),
                    new SdkPoint(pair.ReferenceX, pair.ReferenceY, pair.ReferenceZ))).ToArray(),
                new SdkAffineOptions
                {
                    MaximumConditionEstimate = input.MaximumConditionEstimate,
                    ArithmeticResidualWarning = input.ArithmeticResidualWarning
                },
                cancellationToken);
            if (!sdkResult.Success)
            {
                throw new InvalidDataException(sdkResult.Message);
            }
            if (sdkResult.Matrix is null || sdkResult.Residuals.Count != RequiredPairCount)
            {
                throw new InvalidDataException("OpenVisionLab Vision SDK Full XYZ affine solve returned incomplete matrix evidence.");
            }

            var matrix = new C3DAffineMatrix3x4(
                sdkResult.Matrix.M11, sdkResult.Matrix.M12, sdkResult.Matrix.M13, sdkResult.Matrix.M14,
                sdkResult.Matrix.M21, sdkResult.Matrix.M22, sdkResult.Matrix.M23, sdkResult.Matrix.M24,
                sdkResult.Matrix.M31, sdkResult.Matrix.M32, sdkResult.Matrix.M33, sdkResult.Matrix.M34);
            var residuals = pairs.Select((pair, index) => CreateResidual(pair, sdkResult.Residuals[index])).ToArray();
            EnsureFinite(
                matrix.Values
                    .Append(sdkResult.SourceAugmentedDeterminant)
                    .Append(sdkResult.LinearDeterminantAbsolute)
                    .Append(sdkResult.ConditionEstimate)
                    .Append(sdkResult.ArithmeticMaximumResidual)
                    .Append(sdkResult.ArithmeticRmsResidual),
                "OpenVisionLab Vision SDK affine evidence");
            var warning = sdkResult.ArithmeticResidualWarningExceeded;
            var output = C3DAffineTransform3D.Create(
                input.OutputEntityId,
                input.PublishedCorrespondenceSet,
                matrix,
                sdkResult.SourceAugmentedDeterminant,
                sdkResult.LinearDeterminantAbsolute,
                sdkResult.ConditionEstimate,
                input.MaximumConditionEstimate,
                sdkResult.ArithmeticRmsResidual,
                sdkResult.ArithmeticMaximumResidual,
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
                        new Metric("Source condition estimate", MetricKind.Number, sdkResult.ConditionEstimate, "ratio"),
                        new Metric("Absolute linear determinant", MetricKind.Number, sdkResult.LinearDeterminantAbsolute, "ratio"),
                        new Metric("Arithmetic RMS residual", MetricKind.Deviation, sdkResult.ArithmeticRmsResidual, input.PublishedCorrespondenceSet.ReferenceUnit),
                        new Metric("Arithmetic maximum residual", MetricKind.Deviation, sdkResult.ArithmeticMaximumResidual, input.PublishedCorrespondenceSet.ReferenceUnit, warning ? ResultStatus.Warning : ResultStatus.Pass)
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
        SdkAffineResidual residual)
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
