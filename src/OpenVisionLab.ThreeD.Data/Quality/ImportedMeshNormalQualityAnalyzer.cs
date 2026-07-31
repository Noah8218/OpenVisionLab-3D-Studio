using System.Numerics;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Validates a declared per-position normal channel without repairing,
/// generating, or mutating it.
/// </summary>
public static class ImportedMeshNormalQualityAnalyzer
{
    public const double DefaultUnitLengthTolerance = 1e-3;
    public const double DefaultMinimumAlignmentCosine = 0.5;
    private const double ZeroLengthSquared = 1e-20;

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

        var triangleCount = indices.Count / 3;
        var normalCount = normalPresence is { Count: > 0 }
            ? normalPresence.Count(present => present)
            : normals.Count;
        if (normalCount == 0)
        {
            return new SourceNormalQualityReport(
                SourceNormalQualityReport.CurrentSchemaVersion,
                sourceId,
                format,
                SourceNormalQualityState.Unavailable,
                positions.Count,
                triangleCount,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                unitLengthTolerance,
                minimumAlignmentCosine,
                null,
                null,
                null,
                null,
                null,
                "No declared normal channel is available. Geometric face normals were not promoted to source data.");
        }

        var finiteNormalCount = 0;
        var nonZeroNormalCount = 0;
        var unitLengthNormalCount = 0;
        var minimumNormalLength = double.PositiveInfinity;
        var maximumNormalLength = double.NegativeInfinity;
        var normalLengthSum = 0.0;
        for (var index = 0; index < normals.Count; index++)
        {
            if (!IsPresent(index))
            {
                continue;
            }

            var normal = normals[index];
            if (!IsFinite(normal))
            {
                continue;
            }

            finiteNormalCount++;
            var length = normal.Length();
            minimumNormalLength = Math.Min(minimumNormalLength, length);
            maximumNormalLength = Math.Max(maximumNormalLength, length);
            normalLengthSum += length;
            if (normal.LengthSquared() <= ZeroLengthSquared)
            {
                continue;
            }

            nonZeroNormalCount++;
            if (Math.Abs(length - 1.0) <= unitLengthTolerance)
            {
                unitLengthNormalCount++;
            }
        }

        var invalidIndexCount = 0;
        var degenerateTriangleCount = 0;
        var comparableCornerCount = 0;
        var consistentCornerCount = 0;
        var reversedCornerCount = 0;
        var minimumAlignment = double.PositiveInfinity;
        var alignmentSum = 0.0;
        for (var offset = 0; offset + 2 < indices.Count; offset += 3)
        {
            var aIndex = indices[offset];
            var bIndex = indices[offset + 1];
            var cIndex = indices[offset + 2];
            var triangleInvalidIndexCount =
                (IsIndexInRange(aIndex, positions.Count) ? 0 : 1)
                + (IsIndexInRange(bIndex, positions.Count) ? 0 : 1)
                + (IsIndexInRange(cIndex, positions.Count) ? 0 : 1);
            if (triangleInvalidIndexCount > 0)
            {
                invalidIndexCount += triangleInvalidIndexCount;
                continue;
            }

            var geometric = Vector3.Cross(
                positions[bIndex] - positions[aIndex],
                positions[cIndex] - positions[aIndex]);
            if (!IsFinite(geometric)
                || geometric.LengthSquared() <= ZeroLengthSquared)
            {
                degenerateTriangleCount++;
                continue;
            }

            geometric = Vector3.Normalize(geometric);
            CompareCorner(aIndex, geometric);
            CompareCorner(bIndex, geometric);
            CompareCorner(cIndex, geometric);
        }

        var dense =
            positions.Count > 0
            && normals.Count == positions.Count
            && normalCount == positions.Count;
        var allComparable = comparableCornerCount == triangleCount * 3;
        var valid =
            indices.Count > 0
            && indices.Count % 3 == 0
            && dense
            && finiteNormalCount == normalCount
            && nonZeroNormalCount == normalCount
            && unitLengthNormalCount == normalCount
            && invalidIndexCount == 0
            && degenerateTriangleCount == 0
            && allComparable
            && consistentCornerCount == comparableCornerCount;
        var state = valid
            ? SourceNormalQualityState.Valid
            : SourceNormalQualityState.Invalid;
        var evidence = valid
            ? $"Dense declared normals are finite, non-zero, unit length within {unitLengthTolerance:R}, and align with every referenced triangle corner at cosine >= {minimumAlignmentCosine:R}."
            : $"Declared normals fail closed: dense={dense}; finite={finiteNormalCount}/{normalCount}; nonZero={nonZeroNormalCount}/{normalCount}; unit={unitLengthNormalCount}/{normalCount}; invalidIndices={invalidIndexCount}; degenerateTriangles={degenerateTriangleCount}; alignedCorners={consistentCornerCount}/{comparableCornerCount}.";

        return new SourceNormalQualityReport(
            SourceNormalQualityReport.CurrentSchemaVersion,
            sourceId,
            format,
            state,
            positions.Count,
            triangleCount,
            normalCount,
            finiteNormalCount,
            nonZeroNormalCount,
            unitLengthNormalCount,
            invalidIndexCount,
            degenerateTriangleCount,
            comparableCornerCount,
            consistentCornerCount,
            reversedCornerCount,
            unitLengthTolerance,
            minimumAlignmentCosine,
            finiteNormalCount == 0 ? null : minimumNormalLength,
            finiteNormalCount == 0 ? null : maximumNormalLength,
            finiteNormalCount == 0 ? null : normalLengthSum / finiteNormalCount,
            comparableCornerCount == 0 ? null : minimumAlignment,
            comparableCornerCount == 0 ? null : alignmentSum / comparableCornerCount,
            evidence);

        void CompareCorner(int index, Vector3 geometric)
        {
            if (!IsPresent(index))
            {
                return;
            }

            var normal = normals[index];
            if (!IsFinite(normal)
                || normal.LengthSquared() <= ZeroLengthSquared)
            {
                return;
            }

            var alignment = Vector3.Dot(Vector3.Normalize(normal), geometric);
            comparableCornerCount++;
            minimumAlignment = Math.Min(minimumAlignment, alignment);
            alignmentSum += alignment;
            if (alignment >= minimumAlignmentCosine)
            {
                consistentCornerCount++;
            }

            if (alignment < 0.0)
            {
                reversedCornerCount++;
            }
        }

        bool IsPresent(int index) =>
            IsIndexInRange(index, normals.Count)
            && (normalPresence is not { Count: > 0 }
                || normalPresence[index]);
    }

    private static bool IsIndexInRange(int index, int count) =>
        (uint)index < (uint)count;

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X)
        && float.IsFinite(value.Y)
        && float.IsFinite(value.Z);
}
