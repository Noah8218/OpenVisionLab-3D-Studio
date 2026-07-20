using System.Diagnostics;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Deterministic exact-four source-to-reference full-XYZ affine solve. The
/// result is matrix evidence only; it never applies the matrix to C3D data.
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
    private const double PivotRelativeTolerance = 1e-12;

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
            var source = pairs.Select(pair => new[] { pair.SourceX, pair.SourceY, pair.SourceZ, 1d }).ToArray();
            var inverse = InvertScaledPartialPivot(source, cancellationToken);
            var determinant = DeterminantScaledPartialPivot(source, cancellationToken);
            var condition = InfinityNorm(source) * InfinityNorm(inverse);
            if (!double.IsFinite(condition) || condition > input.MaximumConditionEstimate)
            {
                throw new InvalidDataException($"XYZ Affine Solve rejected source correspondence condition estimate {condition:G8}; taught maximum is {input.MaximumConditionEstimate:G8}.");
            }

            var coefficients = new double[4, 3];
            for (var row = 0; row < RequiredPairCount; row++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var coordinate = 0; coordinate < 3; coordinate++)
                {
                    for (var index = 0; index < RequiredPairCount; index++)
                    {
                        coefficients[row, coordinate] += inverse[row, index] * Reference(pairs[index], coordinate);
                    }
                }
            }

            var matrix = new C3DAffineMatrix3x4(
                coefficients[0, 0], coefficients[1, 0], coefficients[2, 0], coefficients[3, 0],
                coefficients[0, 1], coefficients[1, 1], coefficients[2, 1], coefficients[3, 1],
                coefficients[0, 2], coefficients[1, 2], coefficients[2, 2], coefficients[3, 2]);
            EnsureFinite(matrix.Values, "Affine matrix");
            var linearDeterminantAbsolute = Math.Abs(Determinant3x3(matrix));
            var residuals = pairs.Select(pair => CreateResidual(pair, matrix)).ToArray();
            var maximumResidual = residuals.Max(item => item.ResidualNorm);
            var rmsResidual = Math.Sqrt(residuals.Average(item => item.ResidualNorm * item.ResidualNorm));
            EnsureFinite([determinant, linearDeterminantAbsolute, condition, maximumResidual, rmsResidual], "Affine evidence");
            var warning = maximumResidual > input.ArithmeticResidualWarning;
            var output = C3DAffineTransform3D.Create(
                input.OutputEntityId,
                input.PublishedCorrespondenceSet,
                matrix,
                determinant,
                linearDeterminantAbsolute,
                condition,
                input.MaximumConditionEstimate,
                rmsResidual,
                maximumResidual,
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
                        new Metric("Source condition estimate", MetricKind.Number, condition, "ratio"),
                        new Metric("Absolute linear determinant", MetricKind.Number, linearDeterminantAbsolute, "ratio"),
                        new Metric("Arithmetic RMS residual", MetricKind.Deviation, rmsResidual, input.PublishedCorrespondenceSet.ReferenceUnit),
                        new Metric("Arithmetic maximum residual", MetricKind.Deviation, maximumResidual, input.PublishedCorrespondenceSet.ReferenceUnit, warning ? ResultStatus.Warning : ResultStatus.Pass)
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

    private static double[,] InvertScaledPartialPivot(double[][] source, CancellationToken cancellationToken)
    {
        var augmented = new double[RequiredPairCount, RequiredPairCount * 2];
        var scales = new double[RequiredPairCount];
        for (var row = 0; row < RequiredPairCount; row++)
        {
            for (var column = 0; column < RequiredPairCount; column++)
            {
                augmented[row, column] = source[row][column];
                augmented[row, RequiredPairCount + column] = row == column ? 1d : 0d;
            }
            scales[row] = source[row].Select(Math.Abs).Max();
            if (scales[row] <= 0d || !double.IsFinite(scales[row])) throw new InvalidDataException("XYZ Affine Solve source matrix row is singular.");
        }
        for (var column = 0; column < RequiredPairCount; column++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pivotRow = Enumerable.Range(column, RequiredPairCount - column)
                .OrderByDescending(row => Math.Abs(augmented[row, column]) / scales[row])
                .First();
            var pivot = augmented[pivotRow, column];
            if (Math.Abs(pivot) <= scales[pivotRow] * PivotRelativeTolerance)
            {
                throw new InvalidDataException("XYZ Affine Solve source matrix failed the scaled partial-pivot independence gate.");
            }
            SwapRows(augmented, column, pivotRow);
            (scales[column], scales[pivotRow]) = (scales[pivotRow], scales[column]);
            pivot = augmented[column, column];
            for (var entry = 0; entry < RequiredPairCount * 2; entry++) augmented[column, entry] /= pivot;
            for (var row = 0; row < RequiredPairCount; row++)
            {
                if (row == column) continue;
                var factor = augmented[row, column];
                for (var entry = 0; entry < RequiredPairCount * 2; entry++) augmented[row, entry] -= factor * augmented[column, entry];
            }
        }
        var inverse = new double[RequiredPairCount, RequiredPairCount];
        for (var row = 0; row < RequiredPairCount; row++)
        {
            for (var column = 0; column < RequiredPairCount; column++) inverse[row, column] = augmented[row, RequiredPairCount + column];
        }
        return inverse;
    }

    private static double DeterminantScaledPartialPivot(double[][] source, CancellationToken cancellationToken)
    {
        var matrix = source.Select(row => row.ToArray()).ToArray();
        var scales = matrix.Select(row => row.Select(Math.Abs).Max()).ToArray();
        var sign = 1d;
        var determinant = 1d;
        for (var column = 0; column < RequiredPairCount; column++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pivotRow = Enumerable.Range(column, RequiredPairCount - column)
                .OrderByDescending(row => Math.Abs(matrix[row][column]) / scales[row])
                .First();
            var pivot = matrix[pivotRow][column];
            if (Math.Abs(pivot) <= scales[pivotRow] * PivotRelativeTolerance)
            {
                throw new InvalidDataException("XYZ Affine Solve source determinant is singular.");
            }
            if (pivotRow != column)
            {
                (matrix[column], matrix[pivotRow]) = (matrix[pivotRow], matrix[column]);
                (scales[column], scales[pivotRow]) = (scales[pivotRow], scales[column]);
                sign = -sign;
            }
            pivot = matrix[column][column];
            determinant *= pivot;
            for (var row = column + 1; row < RequiredPairCount; row++)
            {
                var factor = matrix[row][column] / pivot;
                for (var entry = column + 1; entry < RequiredPairCount; entry++) matrix[row][entry] -= factor * matrix[column][entry];
            }
        }
        return sign * determinant;
    }

    private static C3DAffineLandmarkResidual CreateResidual(C3DLandmarkCorrespondencePair pair, C3DAffineMatrix3x4 matrix)
    {
        var transformed = matrix.Transform(pair.SourceX, pair.SourceY, pair.SourceZ);
        var residualX = pair.ReferenceX - transformed.X;
        var residualY = pair.ReferenceY - transformed.Y;
        var residualZ = pair.ReferenceZ - transformed.Z;
        var norm = Math.Sqrt(residualX * residualX + residualY * residualY + residualZ * residualZ);
        return new C3DAffineLandmarkResidual(
            pair.SourceEntityId, pair.SourceOutputRole, pair.SourceContentSha256, pair.ReferenceLandmarkId,
            pair.SourceX, pair.SourceY, pair.SourceZ,
            pair.ReferenceX, pair.ReferenceY, pair.ReferenceZ,
            transformed.X, transformed.Y, transformed.Z,
            residualX, residualY, residualZ, norm);
    }

    private static double Reference(C3DLandmarkCorrespondencePair pair, int coordinate) => coordinate switch
    {
        0 => pair.ReferenceX,
        1 => pair.ReferenceY,
        _ => pair.ReferenceZ
    };

    private static double InfinityNorm(double[][] matrix) => matrix.Max(row => row.Sum(Math.Abs));
    private static double InfinityNorm(double[,] matrix) => Enumerable.Range(0, RequiredPairCount)
        .Max(row => Enumerable.Range(0, RequiredPairCount).Sum(column => Math.Abs(matrix[row, column])));
    private static double Determinant3x3(C3DAffineMatrix3x4 matrix) =>
        matrix.M11 * (matrix.M22 * matrix.M33 - matrix.M23 * matrix.M32)
        - matrix.M12 * (matrix.M21 * matrix.M33 - matrix.M23 * matrix.M31)
        + matrix.M13 * (matrix.M21 * matrix.M32 - matrix.M22 * matrix.M31);
    private static void SwapRows(double[,] matrix, int first, int second)
    {
        if (first == second) return;
        for (var column = 0; column < matrix.GetLength(1); column++) (matrix[first, column], matrix[second, column]) = (matrix[second, column], matrix[first, column]);
    }
    private static bool Finite(IEnumerable<double> values) => values.All(double.IsFinite);
    private static void EnsureFinite(IEnumerable<double> values, string label)
    {
        if (!Finite(values)) throw new InvalidDataException($"{label} contains a non-finite value.");
    }
}
