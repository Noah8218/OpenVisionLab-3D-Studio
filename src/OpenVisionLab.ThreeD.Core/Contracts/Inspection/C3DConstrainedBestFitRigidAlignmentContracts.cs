using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// Immutable source-to-reference proper rigid pose produced by the bounded
/// all-correspondence best-fit route.
/// </summary>
public readonly record struct C3DConstrainedBestFitRigidAlignmentPose(
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
    public (double X, double Y, double Z) Transform(
        double sourceX,
        double sourceY,
        double sourceZ) =>
        (
            (M11 * sourceX) + (M12 * sourceY) + (M13 * sourceZ) + TranslationX,
            (M21 * sourceX) + (M22 * sourceY) + (M23 * sourceZ) + TranslationY,
            (M31 * sourceX) + (M32 * sourceY) + (M33 * sourceZ) + TranslationZ);

    public IReadOnlyList<double> Values =>
    [
        M11, M12, M13, TranslationX,
        M21, M22, M23, TranslationY,
        M31, M32, M33, TranslationZ,
        0d, 0d, 0d, 1d
    ];

    public bool IsRigid(double tolerance)
    {
        if (!double.IsFinite(tolerance)
            || tolerance <= 0d
            || Values.Take(12).Any(value => !double.IsFinite(value)))
        {
            return false;
        }

        var first = (X: M11, Y: M12, Z: M13);
        var second = (X: M21, Y: M22, Z: M23);
        var third = (X: M31, Y: M32, Z: M33);
        var determinant =
            M11 * (M22 * M33 - M23 * M32)
            - M12 * (M21 * M33 - M23 * M31)
            + M13 * (M21 * M32 - M22 * M31);
        return Math.Abs(Dot(first, first) - 1d) <= tolerance
            && Math.Abs(Dot(second, second) - 1d) <= tolerance
            && Math.Abs(Dot(third, third) - 1d) <= tolerance
            && Math.Abs(Dot(first, second)) <= tolerance
            && Math.Abs(Dot(first, third)) <= tolerance
            && Math.Abs(Dot(second, third)) <= tolerance
            && Math.Abs(determinant - 1d) <= tolerance;
    }

    private static double Dot(
        (double X, double Y, double Z) first,
        (double X, double Y, double Z) second) =>
        (first.X * second.X) + (first.Y * second.Y) + (first.Z * second.Z);
}

public readonly record struct C3DConstrainedBestFitRigidAlignmentCentroid(
    double X,
    double Y,
    double Z);

/// <summary>
/// One stable ordered source/reference point correspondence.
/// </summary>
public sealed record C3DConstrainedBestFitRigidAlignmentPair(
    string SourcePointId,
    string ReferencePointId,
    double SourceX,
    double SourceY,
    double SourceZ,
    double ReferenceX,
    double ReferenceY,
    double ReferenceZ);

public sealed record C3DConstrainedBestFitRigidAlignmentResidual(
    int PairIndex,
    string SourcePointId,
    string ReferencePointId,
    double SourceX,
    double SourceY,
    double SourceZ,
    double ReferenceX,
    double ReferenceY,
    double ReferenceZ,
    double TransformedX,
    double TransformedY,
    double TransformedZ,
    double ResidualX,
    double ResidualY,
    double ResidualZ,
    double ResidualNorm);

/// <summary>
/// Immutable direct/Runner evidence for a bounded all-correspondence proper
/// rigid least-squares alignment. It never applies the pose to a cloud or
/// decides product acceptance.
/// </summary>
public sealed class C3DConstrainedBestFitRigidAlignmentArtifact
{
    public const string ContractVersion = "1.0";
    public const string CorrespondenceCountPolicyName = "MinimumFourAllPairsMaximumSixtyFour";
    public const string PoseConstraintPolicyName = "ProperRigidNoScaleNoShearNoReflection";
    public const string CoordinateConventionName = "full-xyz-source-to-reference";

    private const int MinimumCorrespondenceCount = 4;
    private const int MaximumSupportedCorrespondenceCount = 64;

    private C3DConstrainedBestFitRigidAlignmentArtifact(
        string outputEntityId,
        string stepId,
        string sourceEntityId,
        string sourceContentSha256,
        string referenceEntityId,
        string referenceContentSha256,
        string sourceUnit,
        string sourceFrameId,
        string referenceUnit,
        string referenceFrameId,
        IReadOnlyList<C3DConstrainedBestFitRigidAlignmentPair> pairs,
        int maximumCorrespondenceCount,
        double minimumNormalizedLineSpread,
        double arithmeticResidualWarning,
        C3DConstrainedBestFitRigidAlignmentPose pose,
        double sourceNormalizedLineSpread,
        double referenceNormalizedLineSpread,
        C3DConstrainedBestFitRigidAlignmentCentroid sourceCentroid,
        C3DConstrainedBestFitRigidAlignmentCentroid referenceCentroid,
        double rmsResidual,
        double maximumResidual,
        bool arithmeticResidualWarningExceeded,
        IReadOnlyList<C3DConstrainedBestFitRigidAlignmentResidual> residuals,
        string provenance,
        string contentSha256)
    {
        OutputEntityId = outputEntityId;
        StepId = stepId;
        SourceEntityId = sourceEntityId;
        SourceContentSha256 = sourceContentSha256;
        ReferenceEntityId = referenceEntityId;
        ReferenceContentSha256 = referenceContentSha256;
        SourceUnit = sourceUnit;
        SourceFrameId = sourceFrameId;
        ReferenceUnit = referenceUnit;
        ReferenceFrameId = referenceFrameId;
        Pairs = pairs;
        MaximumCorrespondenceCount = maximumCorrespondenceCount;
        MinimumNormalizedLineSpread = minimumNormalizedLineSpread;
        ArithmeticResidualWarning = arithmeticResidualWarning;
        Pose = pose;
        SourceNormalizedLineSpread = sourceNormalizedLineSpread;
        ReferenceNormalizedLineSpread = referenceNormalizedLineSpread;
        SourceCentroid = sourceCentroid;
        ReferenceCentroid = referenceCentroid;
        RmsResidual = rmsResidual;
        MaximumResidual = maximumResidual;
        ArithmeticResidualWarningExceeded = arithmeticResidualWarningExceeded;
        Residuals = residuals;
        Provenance = provenance;
        ContentSha256 = contentSha256;
    }

    public string OutputEntityId { get; }
    public string StepId { get; }
    public string SourceEntityId { get; }
    public string SourceContentSha256 { get; }
    public string ReferenceEntityId { get; }
    public string ReferenceContentSha256 { get; }
    public string SourceUnit { get; }
    public string SourceFrameId { get; }
    public string ReferenceUnit { get; }
    public string ReferenceFrameId { get; }
    public string CoordinateConvention => CoordinateConventionName;
    public string CorrespondenceCountPolicy => CorrespondenceCountPolicyName;
    public string PoseConstraintPolicy => PoseConstraintPolicyName;
    public IReadOnlyList<C3DConstrainedBestFitRigidAlignmentPair> Pairs { get; }
    public int MaximumCorrespondenceCount { get; }
    public bool UsedAllCorrespondences => Pairs.Count == Residuals.Count;
    public double MinimumNormalizedLineSpread { get; }
    public double ArithmeticResidualWarning { get; }
    public C3DConstrainedBestFitRigidAlignmentPose Pose { get; }
    public double SourceNormalizedLineSpread { get; }
    public double ReferenceNormalizedLineSpread { get; }
    public C3DConstrainedBestFitRigidAlignmentCentroid SourceCentroid { get; }
    public C3DConstrainedBestFitRigidAlignmentCentroid ReferenceCentroid { get; }
    public double RmsResidual { get; }
    public double MaximumResidual { get; }
    public bool ArithmeticResidualWarningExceeded { get; }
    public IReadOnlyList<C3DConstrainedBestFitRigidAlignmentResidual> Residuals { get; }
    public string Provenance { get; }
    public string ContentSha256 { get; }

    public static C3DConstrainedBestFitRigidAlignmentArtifact Create(
        string outputEntityId,
        string stepId,
        string sourceEntityId,
        string sourceContentSha256,
        string referenceEntityId,
        string referenceContentSha256,
        string sourceUnit,
        string sourceFrameId,
        string referenceUnit,
        string referenceFrameId,
        IReadOnlyList<C3DConstrainedBestFitRigidAlignmentPair> pairs,
        int maximumCorrespondenceCount,
        double minimumNormalizedLineSpread,
        double arithmeticResidualWarning,
        C3DConstrainedBestFitRigidAlignmentPose pose,
        double sourceNormalizedLineSpread,
        double referenceNormalizedLineSpread,
        C3DConstrainedBestFitRigidAlignmentCentroid sourceCentroid,
        C3DConstrainedBestFitRigidAlignmentCentroid referenceCentroid,
        double rmsResidual,
        double maximumResidual,
        bool arithmeticResidualWarningExceeded,
        IReadOnlyList<C3DConstrainedBestFitRigidAlignmentResidual> residuals,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceContentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceContentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUnit);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFrameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceUnit);
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceFrameId);
        ArgumentNullException.ThrowIfNull(pairs);
        ArgumentNullException.ThrowIfNull(residuals);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);

        var copiedPairs = pairs.ToArray();
        var copiedResiduals = residuals.ToArray();
        if (copiedPairs.Length < MinimumCorrespondenceCount
            || copiedPairs.Length > MaximumSupportedCorrespondenceCount
            || copiedResiduals.Length != copiedPairs.Length
            || maximumCorrespondenceCount < MinimumCorrespondenceCount
            || maximumCorrespondenceCount > MaximumSupportedCorrespondenceCount
            || copiedPairs.Length > maximumCorrespondenceCount)
        {
            throw new ArgumentException("Constrained best-fit rigid alignment requires four to sixty-four pairs and one residual per pair.");
        }
        if (!string.Equals(sourceUnit, referenceUnit, StringComparison.Ordinal))
        {
            throw new ArgumentException("Constrained best-fit rigid alignment requires matching source/reference units; frame IDs remain explicit endpoints.");
        }
        if (!double.IsFinite(minimumNormalizedLineSpread)
            || minimumNormalizedLineSpread <= 0d
            || minimumNormalizedLineSpread >= 1d
            || !double.IsFinite(arithmeticResidualWarning)
            || arithmeticResidualWarning < 0d
            || !double.IsFinite(sourceNormalizedLineSpread)
            || sourceNormalizedLineSpread <= minimumNormalizedLineSpread
            || !double.IsFinite(referenceNormalizedLineSpread)
            || referenceNormalizedLineSpread <= minimumNormalizedLineSpread
            || !Finite(sourceCentroid.X, sourceCentroid.Y, sourceCentroid.Z)
            || !Finite(referenceCentroid.X, referenceCentroid.Y, referenceCentroid.Z)
            || !double.IsFinite(rmsResidual)
            || rmsResidual < 0d
            || !double.IsFinite(maximumResidual)
            || maximumResidual < 0d
            || !pose.IsRigid(1e-8))
        {
            throw new ArgumentException("Constrained best-fit rigid alignment contains invalid pose, geometry, threshold, or residual evidence.");
        }

        var sourceCoordinates = new HashSet<(double X, double Y, double Z)>();
        var referenceCoordinates = new HashSet<(double X, double Y, double Z)>();
        foreach (var pair in copiedPairs)
        {
            if (pair is null
                || string.IsNullOrWhiteSpace(pair.SourcePointId)
                || string.IsNullOrWhiteSpace(pair.ReferencePointId)
                || !Finite(pair.SourceX, pair.SourceY, pair.SourceZ, pair.ReferenceX, pair.ReferenceY, pair.ReferenceZ)
                || !sourceCoordinates.Add((pair.SourceX, pair.SourceY, pair.SourceZ))
                || !referenceCoordinates.Add((pair.ReferenceX, pair.ReferenceY, pair.ReferenceZ)))
            {
                throw new ArgumentException("Constrained best-fit rigid alignment requires finite unique source/reference coordinates.");
            }
        }

        var residualIndexes = new HashSet<int>();
        foreach (var residual in copiedResiduals)
        {
            if (residual is null
                || residual.PairIndex < 0
                || residual.PairIndex >= copiedPairs.Length
                || !residualIndexes.Add(residual.PairIndex)
                || !Finite(
                    residual.SourceX, residual.SourceY, residual.SourceZ,
                    residual.ReferenceX, residual.ReferenceY, residual.ReferenceZ,
                    residual.TransformedX, residual.TransformedY, residual.TransformedZ,
                    residual.ResidualX, residual.ResidualY, residual.ResidualZ,
                    residual.ResidualNorm))
            {
                throw new ArgumentException("Constrained best-fit rigid alignment residual evidence is invalid.");
            }
        }

        var hash = CalculateContentSha256(
            outputEntityId,
            stepId,
            sourceEntityId,
            sourceContentSha256,
            referenceEntityId,
            referenceContentSha256,
            sourceUnit,
            sourceFrameId,
            referenceUnit,
            referenceFrameId,
            copiedPairs,
            maximumCorrespondenceCount,
            minimumNormalizedLineSpread,
            arithmeticResidualWarning,
            pose,
            sourceNormalizedLineSpread,
            referenceNormalizedLineSpread,
            sourceCentroid,
            referenceCentroid,
            rmsResidual,
            maximumResidual,
            arithmeticResidualWarningExceeded,
            copiedResiduals);
        return new C3DConstrainedBestFitRigidAlignmentArtifact(
            outputEntityId,
            stepId,
            sourceEntityId,
            sourceContentSha256,
            referenceEntityId,
            referenceContentSha256,
            sourceUnit,
            sourceFrameId,
            referenceUnit,
            referenceFrameId,
            copiedPairs,
            maximumCorrespondenceCount,
            minimumNormalizedLineSpread,
            arithmeticResidualWarning,
            pose,
            sourceNormalizedLineSpread,
            referenceNormalizedLineSpread,
            sourceCentroid,
            referenceCentroid,
            rmsResidual,
            maximumResidual,
            arithmeticResidualWarningExceeded,
            copiedResiduals,
            provenance,
            hash);
    }

    private static string CalculateContentSha256(
        string outputEntityId,
        string stepId,
        string sourceEntityId,
        string sourceContentSha256,
        string referenceEntityId,
        string referenceContentSha256,
        string sourceUnit,
        string sourceFrameId,
        string referenceUnit,
        string referenceFrameId,
        IReadOnlyList<C3DConstrainedBestFitRigidAlignmentPair> pairs,
        int maximumCorrespondenceCount,
        double minimumNormalizedLineSpread,
        double arithmeticResidualWarning,
        C3DConstrainedBestFitRigidAlignmentPose pose,
        double sourceNormalizedLineSpread,
        double referenceNormalizedLineSpread,
        C3DConstrainedBestFitRigidAlignmentCentroid sourceCentroid,
        C3DConstrainedBestFitRigidAlignmentCentroid referenceCentroid,
        double rmsResidual,
        double maximumResidual,
        bool arithmeticResidualWarningExceeded,
        IReadOnlyList<C3DConstrainedBestFitRigidAlignmentResidual> residuals)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("OpenVisionLab.C3DConstrainedBestFitRigidAlignmentArtifact");
        writer.Write(ContractVersion);
        writer.Write(outputEntityId);
        writer.Write(stepId);
        writer.Write(sourceEntityId);
        writer.Write(sourceContentSha256.ToUpperInvariant());
        writer.Write(referenceEntityId);
        writer.Write(referenceContentSha256.ToUpperInvariant());
        writer.Write(sourceUnit);
        writer.Write(sourceFrameId);
        writer.Write(referenceUnit);
        writer.Write(referenceFrameId);
        writer.Write(CoordinateConventionName);
        writer.Write(CorrespondenceCountPolicyName);
        writer.Write(PoseConstraintPolicyName);
        writer.Write(pairs.Count);
        foreach (var pair in pairs)
        {
            writer.Write(pair.SourcePointId);
            writer.Write(pair.ReferencePointId);
            writer.Write(pair.SourceX);
            writer.Write(pair.SourceY);
            writer.Write(pair.SourceZ);
            writer.Write(pair.ReferenceX);
            writer.Write(pair.ReferenceY);
            writer.Write(pair.ReferenceZ);
        }
        writer.Write(maximumCorrespondenceCount);
        writer.Write(minimumNormalizedLineSpread);
        writer.Write(arithmeticResidualWarning);
        foreach (var value in pose.Values) writer.Write(value);
        writer.Write(sourceNormalizedLineSpread);
        writer.Write(referenceNormalizedLineSpread);
        writer.Write(sourceCentroid.X);
        writer.Write(sourceCentroid.Y);
        writer.Write(sourceCentroid.Z);
        writer.Write(referenceCentroid.X);
        writer.Write(referenceCentroid.Y);
        writer.Write(referenceCentroid.Z);
        writer.Write(rmsResidual);
        writer.Write(maximumResidual);
        writer.Write(arithmeticResidualWarningExceeded);
        writer.Write(residuals.Count);
        foreach (var residual in residuals)
        {
            writer.Write(residual.PairIndex);
            writer.Write(residual.SourcePointId);
            writer.Write(residual.ReferencePointId);
            writer.Write(residual.SourceX);
            writer.Write(residual.SourceY);
            writer.Write(residual.SourceZ);
            writer.Write(residual.ReferenceX);
            writer.Write(residual.ReferenceY);
            writer.Write(residual.ReferenceZ);
            writer.Write(residual.TransformedX);
            writer.Write(residual.TransformedY);
            writer.Write(residual.TransformedZ);
            writer.Write(residual.ResidualX);
            writer.Write(residual.ResidualY);
            writer.Write(residual.ResidualZ);
            writer.Write(residual.ResidualNorm);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static bool Finite(params double[] values) => values.All(double.IsFinite);
}
