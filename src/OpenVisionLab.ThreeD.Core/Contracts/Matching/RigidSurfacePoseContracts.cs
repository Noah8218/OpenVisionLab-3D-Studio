using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// Explicit row-major rigid model-to-scene pose. Points are transformed as
/// scene = rotation * model + translation.
/// </summary>
public sealed record RigidPose3D(
    string Unit,
    string SourceFrameId,
    string TargetFrameId,
    double M11,
    double M12,
    double M13,
    double M21,
    double M22,
    double M23,
    double M31,
    double M32,
    double M33,
    double TranslationX,
    double TranslationY,
    double TranslationZ)
{
    public SurfaceModelPoint3 TransformPoint(
        SurfaceModelPoint3 point)
    {
        ArgumentNullException.ThrowIfNull(point);
        return new SurfaceModelPoint3(
            M11 * point.X
                + M12 * point.Y
                + M13 * point.Z
                + TranslationX,
            M21 * point.X
                + M22 * point.Y
                + M23 * point.Z
                + TranslationY,
            M31 * point.X
                + M32 * point.Y
                + M33 * point.Z
                + TranslationZ);
    }

    public SurfaceModelPoint3 TransformDirection(
        SurfaceModelPoint3 direction)
    {
        ArgumentNullException.ThrowIfNull(direction);
        return new SurfaceModelPoint3(
            M11 * direction.X
                + M12 * direction.Y
                + M13 * direction.Z,
            M21 * direction.X
                + M22 * direction.Y
                + M23 * direction.Z,
            M31 * direction.X
                + M32 * direction.Y
                + M33 * direction.Z);
    }

    public bool IsRigid(double tolerance)
    {
        if (!double.IsFinite(tolerance)
            || tolerance <= 0.0
            || !Values.All(double.IsFinite))
        {
            return false;
        }

        var first = new SurfaceModelPoint3(M11, M12, M13);
        var second = new SurfaceModelPoint3(M21, M22, M23);
        var third = new SurfaceModelPoint3(M31, M32, M33);
        var determinant =
            M11 * (M22 * M33 - M23 * M32)
            - M12 * (M21 * M33 - M23 * M31)
            + M13 * (M21 * M32 - M22 * M31);
        return Math.Abs(Dot(first, first) - 1.0) <= tolerance
            && Math.Abs(Dot(second, second) - 1.0) <= tolerance
            && Math.Abs(Dot(third, third) - 1.0) <= tolerance
            && Math.Abs(Dot(first, second)) <= tolerance
            && Math.Abs(Dot(first, third)) <= tolerance
            && Math.Abs(Dot(second, third)) <= tolerance
            && Math.Abs(determinant - 1.0) <= tolerance;
    }

    public double TranslationMagnitude =>
        Math.Sqrt(
            TranslationX * TranslationX
            + TranslationY * TranslationY
            + TranslationZ * TranslationZ);

    public double RotationAngleDegrees
    {
        get
        {
            var cosine = Math.Clamp(
                (M11 + M22 + M33 - 1.0) / 2.0,
                -1.0,
                1.0);
            return Math.Acos(cosine) * 180.0 / Math.PI;
        }
    }

    public IReadOnlyList<double> ToRowMajor4X4() =>
    [
        M11, M12, M13, TranslationX,
        M21, M22, M23, TranslationY,
        M31, M32, M33, TranslationZ,
        0.0, 0.0, 0.0, 1.0
    ];

    private double[] Values =>
    [
        M11, M12, M13,
        M21, M22, M23,
        M31, M32, M33,
        TranslationX, TranslationY, TranslationZ
    ];

    private static double Dot(
        SurfaceModelPoint3 first,
        SurfaceModelPoint3 second) =>
        first.X * second.X
        + first.Y * second.Y
        + first.Z * second.Z;
}

/// <summary>
/// Explicit finite search domain for version-1 rigid surface matching.
/// Rotation candidates are enumerated X, then Y, then Z in degrees.
/// Translation is estimated by centroid alignment and rejected outside the
/// declared per-axis bounds.
/// </summary>
public sealed record RigidSurfacePoseSearchParameters(
    double MinimumRotationXDegrees,
    double MaximumRotationXDegrees,
    double RotationStepXDegrees,
    double MinimumRotationYDegrees,
    double MaximumRotationYDegrees,
    double RotationStepYDegrees,
    double MinimumRotationZDegrees,
    double MaximumRotationZDegrees,
    double RotationStepZDegrees,
    double MinimumTranslationX,
    double MaximumTranslationX,
    double MinimumTranslationY,
    double MaximumTranslationY,
    double MinimumTranslationZ,
    double MaximumTranslationZ,
    double MaximumCorrespondenceDistance,
    int MinimumMatchedSampleCount,
    int MaximumCandidateCount);

public sealed record SurfaceCoverageMatch(
    int ModelSampleOrder,
    int SceneSampleOrder,
    double Distance);

/// <summary>
/// Raw, decision-free surface coverage. The denominator is always the number
/// of nominal model samples and each measured scene sample may be used once.
/// </summary>
public sealed record SurfaceCoverageEvaluation(
    string Semantics,
    int ModelSampleCount,
    int SceneSampleCount,
    int MatchedModelSampleCount,
    int UnmatchedModelSampleCount,
    double CoverageRatio,
    double? InlierRmse,
    double MaximumCorrespondenceDistance,
    SurfaceCoverageMatch[] Matches,
    string Evidence)
{
    public const string CurrentSemantics =
        "one-way-model-sample-greedy-unique-nearest-position-v1";
}

public enum RigidSurfacePoseSearchState
{
    Matched,
    NoMatch
}

/// <summary>
/// Identified, deterministic pose-search evidence. It reports raw coverage
/// and pose only; acceptance limits remain a later policy layer.
/// </summary>
public sealed record RigidSurfacePoseSearchResult(
    string SchemaVersion,
    string SolverVersion,
    string ModelContentSha256,
    string SceneContentSha256,
    RigidSurfacePoseSearchParameters Parameters,
    RigidSurfacePoseSearchState State,
    int EvaluatedCandidateCount,
    RigidPose3D? Pose,
    SurfaceCoverageEvaluation Coverage,
    string RejectionReason,
    string ContentSha256)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string CurrentSolverVersion =
        "bounded-euler-centroid-nearest-v1";

    public static RigidSurfacePoseSearchResult Create(
        string modelContentSha256,
        string sceneContentSha256,
        RigidSurfacePoseSearchParameters parameters,
        RigidSurfacePoseSearchState state,
        int evaluatedCandidateCount,
        RigidPose3D? pose,
        SurfaceCoverageEvaluation coverage,
        string rejectionReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelContentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(sceneContentSha256);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(coverage);
        ArgumentNullException.ThrowIfNull(coverage.Matches);

        if (!IsCanonicalSha256(modelContentSha256)
            || !IsCanonicalSha256(sceneContentSha256))
        {
            throw new InvalidDataException(
                "Pose result requires canonical model and scene SHA-256 identities.");
        }

        if (evaluatedCandidateCount <= 0)
        {
            throw new InvalidDataException(
                "Pose result requires at least one evaluated candidate.");
        }

        if (state == RigidSurfacePoseSearchState.Matched
            && (pose is null || !pose.IsRigid(1e-9)))
        {
            throw new InvalidDataException(
                "Matched pose result requires a finite rigid pose.");
        }

        if (state == RigidSurfacePoseSearchState.NoMatch
            && pose is not null)
        {
            throw new InvalidDataException(
                "No-match pose result cannot retain a pose.");
        }

        ValidateCoverage(coverage);
        var result = new RigidSurfacePoseSearchResult(
            CurrentSchemaVersion,
            CurrentSolverVersion,
            modelContentSha256,
            sceneContentSha256,
            parameters,
            state,
            evaluatedCandidateCount,
            pose,
            coverage,
            rejectionReason?.Trim() ?? string.Empty,
            string.Empty);
        return result with
        {
            ContentSha256 = CalculateContentSha256(result)
        };
    }

    public static string CalculateContentSha256(
        RigidSurfacePoseSearchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(result.Parameters);
        ArgumentNullException.ThrowIfNull(result.Coverage);
        ArgumentNullException.ThrowIfNull(result.Coverage.Matches);

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(
                   stream,
                   Encoding.UTF8,
                   leaveOpen: true))
        {
            writer.Write("OpenVisionLab.RigidSurfacePoseSearchResult");
            writer.Write(result.SchemaVersion ?? string.Empty);
            writer.Write(result.SolverVersion ?? string.Empty);
            writer.Write(
                (result.ModelContentSha256 ?? string.Empty)
                .ToUpperInvariant());
            writer.Write(
                (result.SceneContentSha256 ?? string.Empty)
                .ToUpperInvariant());
            WriteParameters(writer, result.Parameters);
            writer.Write((int)result.State);
            writer.Write(result.EvaluatedCandidateCount);
            writer.Write(result.Pose is not null);
            if (result.Pose is not null)
            {
                WritePose(writer, result.Pose);
            }

            WriteCoverage(writer, result.Coverage);
            writer.Write(result.RejectionReason ?? string.Empty);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void ValidateCoverage(
        SurfaceCoverageEvaluation coverage)
    {
        if (coverage.Semantics
                != SurfaceCoverageEvaluation.CurrentSemantics
            || coverage.ModelSampleCount <= 0
            || coverage.SceneSampleCount <= 0
            || coverage.MatchedModelSampleCount < 0
            || coverage.MatchedModelSampleCount
                > coverage.ModelSampleCount
            || coverage.UnmatchedModelSampleCount
                != coverage.ModelSampleCount
                    - coverage.MatchedModelSampleCount
            || coverage.Matches.Length
                != coverage.MatchedModelSampleCount
            || !double.IsFinite(coverage.CoverageRatio)
            || Math.Abs(
                coverage.CoverageRatio
                - coverage.MatchedModelSampleCount
                    / (double)coverage.ModelSampleCount)
                > 1e-12
            || coverage.InlierRmse.HasValue
                != (coverage.MatchedModelSampleCount > 0)
            || coverage.InlierRmse.HasValue
                && (!double.IsFinite(coverage.InlierRmse.Value)
                    || coverage.InlierRmse.Value < 0.0)
            || !double.IsFinite(
                coverage.MaximumCorrespondenceDistance)
            || coverage.MaximumCorrespondenceDistance <= 0.0
            || coverage.Matches.Any(match =>
                match is null
                || match.ModelSampleOrder < 0
                || match.ModelSampleOrder
                    >= coverage.ModelSampleCount
                || match.SceneSampleOrder < 0
                || match.SceneSampleOrder
                    >= coverage.SceneSampleCount
                || !double.IsFinite(match.Distance)
                || match.Distance < 0.0
                || match.Distance
                    > coverage.MaximumCorrespondenceDistance))
        {
            throw new InvalidDataException(
                "Surface coverage evidence is internally inconsistent.");
        }

        if (coverage.Matches
                .Select(match => match.ModelSampleOrder)
                .Distinct()
                .Count()
            != coverage.Matches.Length
            || coverage.Matches
                .Select(match => match.SceneSampleOrder)
                .Distinct()
                .Count()
            != coverage.Matches.Length)
        {
            throw new InvalidDataException(
                "Surface coverage matches must be one-to-one.");
        }
    }

    private static void WriteParameters(
        BinaryWriter writer,
        RigidSurfacePoseSearchParameters parameters)
    {
        writer.Write(parameters.MinimumRotationXDegrees);
        writer.Write(parameters.MaximumRotationXDegrees);
        writer.Write(parameters.RotationStepXDegrees);
        writer.Write(parameters.MinimumRotationYDegrees);
        writer.Write(parameters.MaximumRotationYDegrees);
        writer.Write(parameters.RotationStepYDegrees);
        writer.Write(parameters.MinimumRotationZDegrees);
        writer.Write(parameters.MaximumRotationZDegrees);
        writer.Write(parameters.RotationStepZDegrees);
        writer.Write(parameters.MinimumTranslationX);
        writer.Write(parameters.MaximumTranslationX);
        writer.Write(parameters.MinimumTranslationY);
        writer.Write(parameters.MaximumTranslationY);
        writer.Write(parameters.MinimumTranslationZ);
        writer.Write(parameters.MaximumTranslationZ);
        writer.Write(parameters.MaximumCorrespondenceDistance);
        writer.Write(parameters.MinimumMatchedSampleCount);
        writer.Write(parameters.MaximumCandidateCount);
    }

    private static void WritePose(
        BinaryWriter writer,
        RigidPose3D pose)
    {
        writer.Write(pose.Unit ?? string.Empty);
        writer.Write(pose.SourceFrameId ?? string.Empty);
        writer.Write(pose.TargetFrameId ?? string.Empty);
        foreach (var value in pose.ToRowMajor4X4())
        {
            writer.Write(value);
        }
    }

    private static void WriteCoverage(
        BinaryWriter writer,
        SurfaceCoverageEvaluation coverage)
    {
        writer.Write(coverage.Semantics ?? string.Empty);
        writer.Write(coverage.ModelSampleCount);
        writer.Write(coverage.SceneSampleCount);
        writer.Write(coverage.MatchedModelSampleCount);
        writer.Write(coverage.UnmatchedModelSampleCount);
        writer.Write(coverage.CoverageRatio);
        writer.Write(coverage.InlierRmse.HasValue);
        if (coverage.InlierRmse.HasValue)
        {
            writer.Write(coverage.InlierRmse.Value);
        }

        writer.Write(coverage.MaximumCorrespondenceDistance);
        writer.Write(coverage.Matches.Length);
        foreach (var match in coverage.Matches)
        {
            writer.Write(match.ModelSampleOrder);
            writer.Write(match.SceneSampleOrder);
            writer.Write(match.Distance);
        }

        writer.Write(coverage.Evidence ?? string.Empty);
    }

    private static bool IsCanonicalSha256(string value) =>
        value.Length == 64
        && value.All(character =>
            character is >= '0' and <= '9'
            or >= 'A' and <= 'F');
}
