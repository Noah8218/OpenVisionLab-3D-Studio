using System.Numerics;
using OpenVisionLab.ThreeD.Core;
using NoahPoint = Lib.ThreeD.FeatureExtraction.ThreeDPoint;
using NoahQualityState = Lib.ThreeD.FeatureExtraction.DeclaredMeshNormalQualityState;
using NoahQualityTool = Lib.ThreeD.FeatureExtraction.DeclaredMeshNormalQualityTool;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Adapts Studio source identity and report policy to the source-neutral Noah
/// declared-normal quality Tool. It never repairs, generates, or mutates a
/// normal channel.
/// </summary>
public static class ImportedMeshNormalQualityAnalyzer
{
    public const double DefaultUnitLengthTolerance = 1e-3;
    public const double DefaultMinimumAlignmentCosine = 0.5;

    public static SourceNormalQualityReport Create(
        ImportedMesh mesh,
        double unitLengthTolerance = DefaultUnitLengthTolerance,
        double minimumAlignmentCosine = DefaultMinimumAlignmentCosine)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        return Create(
            mesh.Name,
            mesh.Format,
            mesh.Positions,
            mesh.Indices,
            mesh.Normals,
            unitLengthTolerance,
            minimumAlignmentCosine,
            mesh.NormalPresence);
    }

    public static SourceNormalQualityReport Create(
        string sourceId,
        string format,
        IReadOnlyList<Vector3> positions,
        IReadOnlyList<int> indices,
        IReadOnlyList<Vector3> normals,
        double unitLengthTolerance = DefaultUnitLengthTolerance,
        double minimumAlignmentCosine = DefaultMinimumAlignmentCosine,
        IReadOnlyList<bool>? normalPresence = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(indices);
        ArgumentNullException.ThrowIfNull(normals);
        if (normalPresence is { Count: > 0 }
            && normalPresence.Count != normals.Count)
        {
            throw new ArgumentException(
                "Normal presence must be empty or match the normal storage count.",
                nameof(normalPresence));
        }
        if (unitLengthTolerance < 0.0 || !double.IsFinite(unitLengthTolerance))
        {
            throw new ArgumentOutOfRangeException(
                nameof(unitLengthTolerance),
                unitLengthTolerance,
                "Unit-length tolerance must be finite and non-negative.");
        }
        if (minimumAlignmentCosine is < -1.0 or > 1.0
            || !double.IsFinite(minimumAlignmentCosine))
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumAlignmentCosine),
                minimumAlignmentCosine,
                "Minimum alignment cosine must be finite and between -1 and 1.");
        }

        var evaluation = new NoahQualityTool().Execute(
            positions.Select(ToNoahPoint).ToArray(),
            indices,
            normals.Select(ToNoahPoint).ToArray(),
            normalPresence,
            unitLengthTolerance,
            minimumAlignmentCosine);
        var state = evaluation.State switch
        {
            NoahQualityState.Unavailable => SourceNormalQualityState.Unavailable,
            NoahQualityState.Valid => SourceNormalQualityState.Valid,
            NoahQualityState.Invalid => SourceNormalQualityState.Invalid,
            _ => throw new InvalidDataException(
                $"Unsupported Noah declared-normal quality state: {evaluation.State}.")
        };
        if (state == SourceNormalQualityState.Unavailable)
        {
            return new SourceNormalQualityReport(
                SourceNormalQualityReport.CurrentSchemaVersion,
                sourceId,
                format,
                state,
                evaluation.PositionCount,
                evaluation.TriangleCount,
                0, 0, 0, 0, 0, 0, 0, 0, 0,
                unitLengthTolerance,
                minimumAlignmentCosine,
                null, null, null, null, null,
                "No declared normal channel is available. Geometric face normals were not promoted to source data.");
        }

        var dense = evaluation.PositionCount > 0
            && normals.Count == evaluation.PositionCount
            && evaluation.NormalCount == evaluation.PositionCount;
        var evidence = state == SourceNormalQualityState.Valid
            ? $"Dense declared normals are finite, non-zero, unit length within {unitLengthTolerance:R}, and align with every referenced triangle corner at cosine >= {minimumAlignmentCosine:R}."
            : $"Declared normals fail closed: dense={dense}; finite={evaluation.FiniteNormalCount}/{evaluation.NormalCount}; nonZero={evaluation.NonZeroNormalCount}/{evaluation.NormalCount}; unit={evaluation.UnitLengthNormalCount}/{evaluation.NormalCount}; invalidIndices={evaluation.InvalidIndexCount}; degenerateTriangles={evaluation.DegenerateTriangleCount}; alignedCorners={evaluation.ConsistentCornerCount}/{evaluation.ComparableCornerCount}.";

        return new SourceNormalQualityReport(
            SourceNormalQualityReport.CurrentSchemaVersion,
            sourceId,
            format,
            state,
            evaluation.PositionCount,
            evaluation.TriangleCount,
            evaluation.NormalCount,
            evaluation.FiniteNormalCount,
            evaluation.NonZeroNormalCount,
            evaluation.UnitLengthNormalCount,
            evaluation.InvalidIndexCount,
            evaluation.DegenerateTriangleCount,
            evaluation.ComparableCornerCount,
            evaluation.ConsistentCornerCount,
            evaluation.ReversedCornerCount,
            unitLengthTolerance,
            minimumAlignmentCosine,
            Nullable(evaluation.MinimumNormalLength),
            Nullable(evaluation.MaximumNormalLength),
            Nullable(evaluation.MeanNormalLength),
            Nullable(evaluation.MinimumAlignment),
            Nullable(evaluation.MeanAlignment),
            evidence);
    }

    private static NoahPoint ToNoahPoint(Vector3 point) =>
        new(point.X, point.Y, point.Z);

    private static double? Nullable(double value) =>
        double.IsNaN(value) ? null : value;
}
