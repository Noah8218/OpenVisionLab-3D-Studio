namespace OpenVisionLab.ThreeD.Core;

public sealed record SurfaceModelValidityReport(
    string SchemaVersion,
    SurfaceModelValidityState State,
    int PointCount,
    int FinitePointCount,
    int TriangleCount,
    int IndexValidTriangleCount,
    int NonDegenerateTriangleCount,
    int NormalCount,
    int FiniteNormalCount,
    int NonZeroNormalCount,
    int UnitNormalCount,
    int ComparableNormalCornerCount,
    int ConsistentNormalCornerCount,
    int SampleCount,
    int ValidSampleCount,
    bool SymmetryDeclarationValid,
    string SymmetryEvidence,
    bool SurfaceSelectionValid,
    int RetainedSurfaceCount,
    int RemovedSurfaceCount,
    string SurfaceSelectionEvidence,
    bool ContentIdentityValid,
    IReadOnlyList<string> Errors,
    string Evidence)
{
    public const string CurrentSchemaVersion = "1.2";

    public bool IsValid => State == SurfaceModelValidityState.Valid;
}

public enum SurfaceModelValidityState
{
    Valid,
    Invalid
}

/// <summary>
/// Fail-closed semantic and content-identity validation for SurfaceModel.
/// No point, triangle, normal, or sample is repaired by this validator.
/// </summary>
public static class SurfaceModelArtifactValidator
{
    private const double ZeroLengthSquared = 1e-24;

    public static SurfaceModelValidityReport Inspect(
        SurfaceModelArtifact model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var errors = new List<string>();
        var points = model.Points ?? [];
        var triangles = model.Triangles ?? [];
        var normals = model.Normals ?? [];
        var samples = model.Samples ?? [];
        var preparation = model.Preparation;

        if (model.SchemaVersion is not (
                SurfaceModelArtifact.LegacySchemaVersion
                or SurfaceModelArtifact.SymmetrySchemaVersion
                or SurfaceModelArtifact.CurrentSchemaVersion))
        {
            errors.Add(
                $"Unsupported SurfaceModel schema '{model.SchemaVersion}'.");
        }

        var symmetryDeclarationValid = ValidateSymmetryDeclaration(
            model.SchemaVersion,
            model.Symmetry,
            errors,
            out var symmetryEvidence);
        var surfaceSelectionValid = ValidateSurfaceSelection(
            model.SchemaVersion,
            model.SurfaceSelection,
            triangles.Length,
            errors,
            out var retainedSourceTriangleIndices,
            out var surfaceSelectionEvidence);

        RequireText(model.ArtifactId, "artifact ID", errors);
        RequireText(model.Name, "name", errors);
        RequireText(model.SourceEntityId, "source entity ID", errors);
        RequireText(model.SourceFormat, "source format", errors);
        RequireText(model.Unit, "unit", errors);
        RequireText(model.FrameId, "frame ID", errors);
        if (model.CoordinateConvention
            != SurfaceModelArtifact.CurrentCoordinateConvention)
        {
            errors.Add("SurfaceModel coordinate convention is unsupported.");
        }

        if (!IsCanonicalSha256(model.SourceContentSha256))
        {
            errors.Add(
                "SurfaceModel source content identity must be an uppercase SHA-256.");
        }

        if (preparation is null)
        {
            errors.Add("SurfaceModel preparation parameters are missing.");
        }
        else
        {
            if (preparation.SamplingPolicy
                != SurfaceModelPreparationParameters
                    .DeterministicTriangleCentroidSampling)
            {
                errors.Add("SurfaceModel sampling policy is unsupported.");
            }

            if (preparation.MaximumSampleCount <= 0)
            {
                errors.Add(
                    "SurfaceModel maximum sample count must be positive.");
            }

            if (!double.IsFinite(preparation.MinimumTriangleArea)
                || preparation.MinimumTriangleArea <= 0.0)
            {
                errors.Add(
                    "SurfaceModel minimum triangle area must be finite and positive.");
            }

            if (!double.IsFinite(preparation.UnitNormalTolerance)
                || preparation.UnitNormalTolerance < 0.0
                || preparation.UnitNormalTolerance >= 1.0)
            {
                errors.Add(
                    "SurfaceModel unit-normal tolerance must be finite and in [0,1).");
            }

            if (!double.IsFinite(preparation.MinimumNormalAlignmentCosine)
                || preparation.MinimumNormalAlignmentCosine < -1.0
                || preparation.MinimumNormalAlignmentCosine > 1.0)
            {
                errors.Add(
                    "SurfaceModel minimum normal alignment cosine must be in [-1,1].");
            }
        }

        if (points.Length == 0)
        {
            errors.Add("SurfaceModel requires at least one point.");
        }

        var finitePointCount = points.Count(IsFinite);
        if (finitePointCount != points.Length)
        {
            errors.Add("SurfaceModel points must all be finite.");
        }

        if (triangles.Length == 0)
        {
            errors.Add("SurfaceModel requires at least one triangle.");
        }

        var indexValidTriangleCount = 0;
        var nonDegenerateTriangleCount = 0;
        var comparableNormalCornerCount = 0;
        var consistentNormalCornerCount = 0;
        var triangleNormals =
            new SurfaceModelPoint3?[triangles.Length];

        for (var triangleIndex = 0;
             triangleIndex < triangles.Length;
             triangleIndex++)
        {
            var triangle = triangles[triangleIndex];
            if (triangle is null)
            {
                errors.Add(
                    $"SurfaceModel triangle {triangleIndex} is missing.");
                continue;
            }

            var indexValid =
                IsIndex(triangle.FirstPointIndex, points.Length)
                && IsIndex(triangle.SecondPointIndex, points.Length)
                && IsIndex(triangle.ThirdPointIndex, points.Length)
                && triangle.FirstPointIndex != triangle.SecondPointIndex
                && triangle.FirstPointIndex != triangle.ThirdPointIndex
                && triangle.SecondPointIndex != triangle.ThirdPointIndex;
            if (!indexValid)
            {
                errors.Add(
                    $"SurfaceModel triangle {triangleIndex} has invalid or repeated point indices.");
                continue;
            }

            indexValidTriangleCount++;
            var first = points[triangle.FirstPointIndex];
            var second = points[triangle.SecondPointIndex];
            var third = points[triangle.ThirdPointIndex];
            if (!IsFinite(first) || !IsFinite(second) || !IsFinite(third))
            {
                continue;
            }

            var cross = Cross(Subtract(second, first), Subtract(third, first));
            var crossLength = Length(cross);
            var area = crossLength * 0.5;
            if (!double.IsFinite(area)
                || preparation is null
                || area < preparation.MinimumTriangleArea)
            {
                errors.Add(
                    $"SurfaceModel triangle {triangleIndex} is degenerate or below the minimum area.");
                continue;
            }

            nonDegenerateTriangleCount++;
            var faceNormal = Scale(cross, 1.0 / crossLength);
            triangleNormals[triangleIndex] = faceNormal;

            if (normals.Length != points.Length
                || preparation is null)
            {
                continue;
            }

            CompareCorner(triangle.FirstPointIndex, faceNormal);
            CompareCorner(triangle.SecondPointIndex, faceNormal);
            CompareCorner(triangle.ThirdPointIndex, faceNormal);
        }

        if (normals.Length != points.Length)
        {
            errors.Add(
                "SurfaceModel requires one declared normal per point.");
        }

        var finiteNormalCount = normals.Count(IsFinite);
        var nonZeroNormalCount = normals.Count(normal =>
            IsFinite(normal)
            && LengthSquared(normal) > ZeroLengthSquared);
        var unitNormalCount = preparation is null
            ? 0
            : normals.Count(normal =>
                IsFinite(normal)
                && LengthSquared(normal) > ZeroLengthSquared
                && Math.Abs(Length(normal) - 1.0)
                    <= preparation.UnitNormalTolerance);

        if (finiteNormalCount != normals.Length)
        {
            errors.Add("SurfaceModel normals must all be finite.");
        }

        if (nonZeroNormalCount != normals.Length)
        {
            errors.Add("SurfaceModel normals must all be non-zero.");
        }

        if (preparation is not null
            && unitNormalCount != normals.Length)
        {
            errors.Add(
                "SurfaceModel normals must be unit length within the declared tolerance.");
        }

        var expectedComparableCorners =
            nonDegenerateTriangleCount * 3;
        if (normals.Length == points.Length
            && comparableNormalCornerCount != expectedComparableCorners)
        {
            errors.Add(
                "SurfaceModel normals are not comparable at every valid triangle corner.");
        }

        if (comparableNormalCornerCount > 0
            && consistentNormalCornerCount != comparableNormalCornerCount)
        {
            errors.Add(
                "SurfaceModel normals do not align with every triangle winding.");
        }

        var validSampleCount = 0;
        var expectedSampleCount =
            preparation is null
            || retainedSourceTriangleIndices.Length == 0
                ? 0
                : Math.Min(
                    preparation.MaximumSampleCount,
                    retainedSourceTriangleIndices.Length);
        if (samples.Length != expectedSampleCount)
        {
            errors.Add(
                $"SurfaceModel sample count {samples.Length} does not match deterministic expectation {expectedSampleCount}.");
        }

        var sampledTriangles = new HashSet<int>();
        for (var sampleIndex = 0;
             sampleIndex < samples.Length;
             sampleIndex++)
        {
            var sample = samples[sampleIndex];
            var valid = sample is not null
                && sample.Order == sampleIndex
                && IsFinite(sample.Position)
                && IsFinite(sample.Normal)
                && LengthSquared(sample.Normal) > ZeroLengthSquared
                && preparation is not null
                && Math.Abs(Length(sample.Normal) - 1.0)
                    <= preparation.UnitNormalTolerance
                && IsIndex(sample.SourceTriangleIndex, triangles.Length)
                && sampledTriangles.Add(sample.SourceTriangleIndex);
            if (!valid || sample is null || preparation is null)
            {
                errors.Add(
                    $"SurfaceModel sample {sampleIndex} is invalid.");
                continue;
            }

            var expectedTriangleIndex = SurfaceModelSampling
                .GetEvenTriangleIndex(
                    sampleIndex,
                    samples.Length,
                    retainedSourceTriangleIndices.Length);
            expectedTriangleIndex =
                retainedSourceTriangleIndices[expectedTriangleIndex];
            var triangle = triangles[sample.SourceTriangleIndex];
            var faceNormal = triangleNormals[sample.SourceTriangleIndex];
            if (sample.SourceTriangleIndex != expectedTriangleIndex
                || triangle is null
                || faceNormal is null)
            {
                errors.Add(
                    $"SurfaceModel sample {sampleIndex} does not reference the expected usable triangle.");
                continue;
            }

            var centroid = Scale(
                Add(
                    Add(points[triangle.FirstPointIndex],
                        points[triangle.SecondPointIndex]),
                    points[triangle.ThirdPointIndex]),
                1.0 / 3.0);
            var coordinateTolerance =
                1e-12 * Math.Max(1.0, MaxAbs(centroid));
            var positionError = Length(Subtract(sample.Position, centroid));
            var normalAlignment = Dot(sample.Normal, faceNormal);
            if (positionError > coordinateTolerance
                || normalAlignment
                    < preparation.MinimumNormalAlignmentCosine)
            {
                errors.Add(
                    $"SurfaceModel sample {sampleIndex} does not match its source triangle centroid/normal.");
                continue;
            }

            validSampleCount++;
        }

        var contentIdentityValid = false;
        if (IsCanonicalSha256(model.ContentSha256))
        {
            try
            {
                contentIdentityValid = string.Equals(
                    model.ContentSha256,
                    SurfaceModelArtifact.CalculateContentSha256(model),
                    StringComparison.Ordinal);
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    or InvalidOperationException
                    or NullReferenceException)
            {
                contentIdentityValid = false;
            }
        }

        if (!contentIdentityValid)
        {
            errors.Add(
                "SurfaceModel content identity is missing or does not match its canonical content.");
        }

        var state = errors.Count == 0
            ? SurfaceModelValidityState.Valid
            : SurfaceModelValidityState.Invalid;
        var evidence =
            $"points={finitePointCount}/{points.Length}; "
            + $"triangles=index-valid {indexValidTriangleCount}/{triangles.Length}, "
            + $"non-degenerate {nonDegenerateTriangleCount}/{triangles.Length}; "
            + $"normals=finite {finiteNormalCount}/{normals.Length}, "
            + $"non-zero {nonZeroNormalCount}/{normals.Length}, "
            + $"unit {unitNormalCount}/{normals.Length}, "
            + $"aligned corners {consistentNormalCornerCount}/{comparableNormalCornerCount}; "
            + $"samples={validSampleCount}/{samples.Length}; "
            + $"symmetry={symmetryEvidence}; "
            + $"surfaceSelection={surfaceSelectionEvidence}; "
            + $"contentIdentity={contentIdentityValid}.";

        return new SurfaceModelValidityReport(
            SurfaceModelValidityReport.CurrentSchemaVersion,
            state,
            points.Length,
            finitePointCount,
            triangles.Length,
            indexValidTriangleCount,
            nonDegenerateTriangleCount,
            normals.Length,
            finiteNormalCount,
            nonZeroNormalCount,
            unitNormalCount,
            comparableNormalCornerCount,
            consistentNormalCornerCount,
            samples.Length,
            validSampleCount,
            symmetryDeclarationValid,
            symmetryEvidence,
            surfaceSelectionValid,
            retainedSourceTriangleIndices.Length,
            model.SurfaceSelection?.RemovedSurfaces?.Length ?? 0,
            surfaceSelectionEvidence,
            contentIdentityValid,
            errors.ToArray(),
            evidence);

        void CompareCorner(
            int pointIndex,
            SurfaceModelPoint3 faceNormal)
        {
            var normal = normals[pointIndex];
            if (!IsFinite(normal)
                || LengthSquared(normal) <= ZeroLengthSquared)
            {
                return;
            }

            comparableNormalCornerCount++;
            if (Dot(Scale(normal, 1.0 / Length(normal)), faceNormal)
                >= preparation!.MinimumNormalAlignmentCosine)
            {
                consistentNormalCornerCount++;
            }
        }
    }

    private static bool ValidateSurfaceSelection(
        string? schemaVersion,
        SurfaceModelSurfaceSelection? selection,
        int triangleCount,
        ICollection<string> errors,
        out int[] retainedSourceTriangleIndices,
        out string evidence)
    {
        if (schemaVersion != SurfaceModelArtifact.CurrentSchemaVersion)
        {
            retainedSourceTriangleIndices =
                Enumerable.Range(0, triangleCount).ToArray();
            evidence = "all-source-triangles";
            if (selection is null)
            {
                return true;
            }

            errors.Add(
                "SurfaceModel schema 1.0/1.1 cannot contain surface-selection evidence.");
            return false;
        }

        if (selection is null)
        {
            retainedSourceTriangleIndices = [];
            evidence = "missing";
            errors.Add(
                "SurfaceModel schema 1.2 requires surface-selection evidence.");
            return false;
        }

        var valid = true;
        void Error(string message)
        {
            valid = false;
            errors.Add(message);
        }

        if (selection.Policy
            != SurfaceModelSurfaceSelection
                .ExactDuplicateAndExplicitExclusionPolicy)
        {
            Error("SurfaceModel surface-selection policy is unsupported.");
        }

        if (selection.SourceTriangleCount != triangleCount)
        {
            Error(
                "SurfaceModel surface-selection source count must match the preserved source triangles.");
        }

        var internalIndices = selection
            .ExplicitInternalSourceTriangleIndices;
        var unobservableIndices = selection
            .ExplicitUnobservableSourceTriangleIndices;
        var retained = selection.RetainedSourceTriangleIndices;
        var removed = selection.RemovedSurfaces;
        if (internalIndices is null
            || unobservableIndices is null
            || retained is null
            || removed is null)
        {
            retainedSourceTriangleIndices = [];
            evidence = "malformed";
            Error(
                "SurfaceModel surface-selection collections are required.");
            return false;
        }

        if (!IsStrictAscendingRange(internalIndices, triangleCount)
            || !IsStrictAscendingRange(
                unobservableIndices,
                triangleCount)
            || !IsStrictAscendingRange(retained, triangleCount)
            || retained.Length == 0)
        {
            Error(
                "SurfaceModel surface-selection indices must be unique, ascending, in range, and retain at least one triangle.");
        }

        var internalSet = internalIndices.ToHashSet();
        var unobservableSet = unobservableIndices.ToHashSet();
        var retainedSet = retained.ToHashSet();
        if (internalSet.Overlaps(unobservableSet)
            || internalSet.Overlaps(retainedSet)
            || unobservableSet.Overlaps(retainedSet))
        {
            Error(
                "SurfaceModel surface-selection roles must be disjoint.");
        }

        var removedIndices = new HashSet<int>();
        var previousRemovedIndex = -1;
        foreach (var item in removed)
        {
            if (item is null
                || !IsIndex(item.SourceTriangleIndex, triangleCount)
                || item.SourceTriangleIndex <= previousRemovedIndex
                || !removedIndices.Add(item.SourceTriangleIndex)
                || retainedSet.Contains(item.SourceTriangleIndex))
            {
                Error(
                    "SurfaceModel removed-surface evidence must be unique, ascending, in range, and outside the retained domain.");
                continue;
            }

            previousRemovedIndex = item.SourceTriangleIndex;
            if (item.Reason
                == SurfaceModelSurfaceSelection.ExplicitInternalReason)
            {
                if (!internalSet.Contains(item.SourceTriangleIndex)
                    || item.DuplicateOfSourceTriangleIndex.HasValue)
                {
                    Error(
                        "SurfaceModel explicit-internal removal evidence does not match the authored indices.");
                }
            }
            else if (item.Reason
                     == SurfaceModelSurfaceSelection
                         .ExplicitUnobservableReason)
            {
                if (!unobservableSet.Contains(item.SourceTriangleIndex)
                    || item.DuplicateOfSourceTriangleIndex.HasValue)
                {
                    Error(
                        "SurfaceModel explicit-unobservable removal evidence does not match the authored indices.");
                }
            }
            else if (item.Reason
                     == SurfaceModelSurfaceSelection.ExactDuplicateReason)
            {
                if (!selection.RemoveExactDuplicateTriangles
                    || !item.DuplicateOfSourceTriangleIndex.HasValue
                    || !retainedSet.Contains(
                        item.DuplicateOfSourceTriangleIndex.Value)
                    || item.DuplicateOfSourceTriangleIndex.Value
                        >= item.SourceTriangleIndex)
                {
                    Error(
                        "SurfaceModel exact-duplicate removal must reference an earlier retained source triangle.");
                }
            }
            else
            {
                Error(
                    "SurfaceModel removed-surface reason is unsupported.");
            }
        }

        if (!internalSet.SetEquals(removed
                .Where(item => item?.Reason
                    == SurfaceModelSurfaceSelection
                        .ExplicitInternalReason)
                .Select(item => item!.SourceTriangleIndex))
            || !unobservableSet.SetEquals(removed
                .Where(item => item?.Reason
                    == SurfaceModelSurfaceSelection
                        .ExplicitUnobservableReason)
                .Select(item => item!.SourceTriangleIndex))
            || retainedSet.Count + removedIndices.Count != triangleCount
            || Enumerable.Range(0, triangleCount).Any(index =>
                !retainedSet.Contains(index)
                && !removedIndices.Contains(index)))
        {
            Error(
                "SurfaceModel surface-selection evidence must partition every source triangle exactly once.");
        }

        retainedSourceTriangleIndices = retained.ToArray();
        evidence =
            $"{selection.Policy}; retained={retained.Length}/{triangleCount}; "
            + $"removed={removed.Length}; internal={internalIndices.Length}; "
            + $"unobservable={unobservableIndices.Length}; "
            + $"exactDuplicates={removed.Count(item => item?.Reason == SurfaceModelSurfaceSelection.ExactDuplicateReason)}";
        return valid;
    }

    private static bool IsStrictAscendingRange(
        IReadOnlyList<int> indices,
        int count)
    {
        for (var index = 0; index < indices.Count; index++)
        {
            if (!IsIndex(indices[index], count)
                || index > 0 && indices[index] <= indices[index - 1])
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidateSymmetryDeclaration(
        string? schemaVersion,
        SurfaceModelSymmetryDeclaration? symmetry,
        ICollection<string> errors,
        out string evidence)
    {
        if (schemaVersion == SurfaceModelArtifact.LegacySchemaVersion)
        {
            evidence = "schema-1.0-undeclared";
            if (symmetry is null)
            {
                return true;
            }

            errors.Add(
                "SurfaceModel schema 1.0 cannot contain a symmetry declaration.");
            return false;
        }

        if (schemaVersion is not (
                SurfaceModelArtifact.SymmetrySchemaVersion
                or SurfaceModelArtifact.CurrentSchemaVersion))
        {
            evidence = "unsupported-schema";
            return false;
        }

        if (symmetry is null)
        {
            evidence = "missing";
            errors.Add(
                "SurfaceModel schema 1.1/1.2 requires a symmetry declaration.");
            return false;
        }

        if (symmetry.Kind == SurfaceModelSymmetryDeclaration.NoneKind)
        {
            evidence = "none";
            if (symmetry.Axis == SurfaceModelSymmetryDeclaration.NoAxis
                && symmetry.Order == 1)
            {
                return true;
            }

            errors.Add(
                "SurfaceModel symmetry kind 'none' requires axis 'none' and order 1.");
            return false;
        }

        if (symmetry.Kind
            == SurfaceModelSymmetryDeclaration.DiscreteRotationKind)
        {
            evidence =
                $"discrete-rotation:{symmetry.Axis}:{symmetry.Order}";
            var axisValid = symmetry.Axis is
                SurfaceModelSymmetryDeclaration.XAxis
                or SurfaceModelSymmetryDeclaration.YAxis
                or SurfaceModelSymmetryDeclaration.ZAxis;
            if (axisValid && symmetry.Order >= 2)
            {
                return true;
            }

            errors.Add(
                "SurfaceModel discrete-rotation symmetry requires model axis x, y, or z and order at least 2.");
            return false;
        }

        evidence = $"unsupported:{symmetry.Kind}";
        errors.Add(
            $"SurfaceModel symmetry kind '{symmetry.Kind}' is unsupported.");
        return false;
    }

    private static void RequireText(
        string? value,
        string label,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"SurfaceModel {label} is required.");
        }
    }

    private static bool IsCanonicalSha256(string? value) =>
        value is { Length: 64 }
        && value.All(character =>
            character is >= '0' and <= '9'
            or >= 'A' and <= 'F');

    private static bool IsIndex(int index, int count) =>
        (uint)index < (uint)count;

    private static bool IsFinite(SurfaceModelPoint3? value) =>
        value is not null
        && double.IsFinite(value.X)
        && double.IsFinite(value.Y)
        && double.IsFinite(value.Z);

    private static SurfaceModelPoint3 Add(
        SurfaceModelPoint3 first,
        SurfaceModelPoint3 second) =>
        new(
            first.X + second.X,
            first.Y + second.Y,
            first.Z + second.Z);

    private static SurfaceModelPoint3 Subtract(
        SurfaceModelPoint3 first,
        SurfaceModelPoint3 second) =>
        new(
            first.X - second.X,
            first.Y - second.Y,
            first.Z - second.Z);

    private static SurfaceModelPoint3 Scale(
        SurfaceModelPoint3 value,
        double scale) =>
        new(value.X * scale, value.Y * scale, value.Z * scale);

    private static SurfaceModelPoint3 Cross(
        SurfaceModelPoint3 first,
        SurfaceModelPoint3 second) =>
        new(
            first.Y * second.Z - first.Z * second.Y,
            first.Z * second.X - first.X * second.Z,
            first.X * second.Y - first.Y * second.X);

    private static double Dot(
        SurfaceModelPoint3 first,
        SurfaceModelPoint3 second) =>
        first.X * second.X
        + first.Y * second.Y
        + first.Z * second.Z;

    private static double Length(SurfaceModelPoint3 value) =>
        Math.Sqrt(LengthSquared(value));

    private static double LengthSquared(SurfaceModelPoint3 value) =>
        Dot(value, value);

    private static double MaxAbs(SurfaceModelPoint3 value) =>
        Math.Max(
            Math.Abs(value.X),
            Math.Max(Math.Abs(value.Y), Math.Abs(value.Z)));
}
