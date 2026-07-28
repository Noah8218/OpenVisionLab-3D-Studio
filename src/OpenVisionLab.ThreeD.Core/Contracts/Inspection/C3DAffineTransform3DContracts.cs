using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// Immutable, source-to-reference full-XYZ affine evidence. This contract is
/// solve-only: it does not contain, create, or imply a transformed point cloud.
/// </summary>
public sealed class C3DAffineTransform3D
{
    public const string ContractVersion = "1.0";

    private C3DAffineTransform3D(
        string outputEntityId,
        C3DLandmarkCorrespondenceSet correspondence,
        C3DAffineMatrix3x4 matrix,
        double sourceAugmentedDeterminant,
        double linearDeterminantAbsolute,
        double conditionEstimate,
        double maximumConditionEstimate,
        double arithmeticRmsResidual,
        double arithmeticMaximumResidual,
        double arithmeticResidualWarning,
        IReadOnlyList<C3DAffineLandmarkResidual> residuals,
        string provenance,
        string contentSha256)
    {
        OutputEntityId = outputEntityId;
        CorrespondenceEntityId = correspondence.OutputEntityId;
        CorrespondenceContentSha256 = correspondence.ContentSha256;
        RootSourceEntityId = correspondence.RootSourceEntityId;
        RootSourceSha256 = correspondence.RootSourceSha256;
        SourceUnit = correspondence.SourceUnit;
        SourceFrameId = correspondence.SourceFrameId;
        SourceCoordinateConvention = correspondence.SourceCoordinateConvention;
        ReferenceFrameId = correspondence.ReferenceFrameId;
        ReferenceUnit = correspondence.ReferenceUnit;
        ReferenceProvenance = correspondence.ReferenceProvenance;
        ReferenceRevision = correspondence.ReferenceRevision;
        Matrix = matrix;
        SourceAugmentedDeterminant = sourceAugmentedDeterminant;
        LinearDeterminantAbsolute = linearDeterminantAbsolute;
        ConditionEstimate = conditionEstimate;
        MaximumConditionEstimate = maximumConditionEstimate;
        ArithmeticRmsResidual = arithmeticRmsResidual;
        ArithmeticMaximumResidual = arithmeticMaximumResidual;
        ArithmeticResidualWarning = arithmeticResidualWarning;
        Residuals = residuals;
        Provenance = provenance;
        ContentSha256 = contentSha256;
    }

    public string OutputEntityId { get; }
    public string CorrespondenceEntityId { get; }
    public string CorrespondenceContentSha256 { get; }
    public string RootSourceEntityId { get; }
    public string RootSourceSha256 { get; }
    public string SourceUnit { get; }
    public string SourceFrameId { get; }
    public string SourceCoordinateConvention { get; }
    public string ReferenceFrameId { get; }
    public string ReferenceUnit { get; }
    public string ReferenceProvenance { get; }
    public string ReferenceRevision { get; }
    public string SolvePolicy => "ExactFourPartialPivot";
    public C3DAffineMatrix3x4 Matrix { get; }
    public double SourceAugmentedDeterminant { get; }
    public double LinearDeterminantAbsolute { get; }
    public double ConditionEstimate { get; }
    public double MaximumConditionEstimate { get; }
    public double ArithmeticRmsResidual { get; }
    public double ArithmeticMaximumResidual { get; }
    public double ArithmeticResidualWarning { get; }
    public IReadOnlyList<C3DAffineLandmarkResidual> Residuals { get; }
    public string Provenance { get; }
    public string ContentSha256 { get; }

    public (double X, double Y, double Z) Transform(double sourceX, double sourceY, double sourceZ) =>
        Matrix.Transform(sourceX, sourceY, sourceZ);

    public static C3DAffineTransform3D Create(
        string outputEntityId,
        C3DLandmarkCorrespondenceSet correspondence,
        C3DAffineMatrix3x4 matrix,
        double sourceAugmentedDeterminant,
        double linearDeterminantAbsolute,
        double conditionEstimate,
        double maximumConditionEstimate,
        double arithmeticRmsResidual,
        double arithmeticMaximumResidual,
        double arithmeticResidualWarning,
        IReadOnlyList<C3DAffineLandmarkResidual> residuals,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputEntityId);
        ArgumentNullException.ThrowIfNull(correspondence);
        ArgumentNullException.ThrowIfNull(residuals);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var copiedResiduals = residuals.ToArray();
        var hash = CalculateContentSha256(
            outputEntityId, correspondence, matrix, sourceAugmentedDeterminant,
            linearDeterminantAbsolute, conditionEstimate, maximumConditionEstimate,
            arithmeticRmsResidual, arithmeticMaximumResidual, arithmeticResidualWarning,
            copiedResiduals);
        return new C3DAffineTransform3D(
            outputEntityId, correspondence, matrix, sourceAugmentedDeterminant,
            linearDeterminantAbsolute, conditionEstimate, maximumConditionEstimate,
            arithmeticRmsResidual, arithmeticMaximumResidual, arithmeticResidualWarning,
            copiedResiduals, provenance, hash);
    }

    private static string CalculateContentSha256(
        string outputEntityId,
        C3DLandmarkCorrespondenceSet correspondence,
        C3DAffineMatrix3x4 matrix,
        double sourceAugmentedDeterminant,
        double linearDeterminantAbsolute,
        double conditionEstimate,
        double maximumConditionEstimate,
        double arithmeticRmsResidual,
        double arithmeticMaximumResidual,
        double arithmeticResidualWarning,
        IReadOnlyList<C3DAffineLandmarkResidual> residuals)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("OpenVisionLab.C3DAffineTransform3D");
        writer.Write(ContractVersion);
        writer.Write(outputEntityId);
        writer.Write(correspondence.OutputEntityId);
        writer.Write(correspondence.ContentSha256.ToUpperInvariant());
        writer.Write(correspondence.RootSourceEntityId);
        writer.Write(correspondence.RootSourceSha256.ToUpperInvariant());
        writer.Write(correspondence.SourceUnit); writer.Write(correspondence.SourceFrameId);
        writer.Write(correspondence.SourceCoordinateConvention);
        writer.Write(correspondence.ReferenceFrameId); writer.Write(correspondence.ReferenceUnit);
        writer.Write(correspondence.ReferenceProvenance); writer.Write(correspondence.ReferenceRevision);
        writer.Write("ExactFourPartialPivot");
        foreach (var value in matrix.Values) writer.Write(value);
        writer.Write(sourceAugmentedDeterminant); writer.Write(linearDeterminantAbsolute);
        writer.Write(conditionEstimate); writer.Write(maximumConditionEstimate);
        writer.Write(arithmeticRmsResidual); writer.Write(arithmeticMaximumResidual); writer.Write(arithmeticResidualWarning);
        writer.Write(residuals.Count);
        foreach (var residual in residuals)
        {
            writer.Write(residual.SourceEntityId); writer.Write(residual.SourceOutputRole);
            writer.Write(residual.SourceContentSha256.ToUpperInvariant());
            writer.Write(residual.ReferenceLandmarkId);
            writer.Write(residual.SourceX); writer.Write(residual.SourceY); writer.Write(residual.SourceZ);
            writer.Write(residual.ReferenceX); writer.Write(residual.ReferenceY); writer.Write(residual.ReferenceZ);
            writer.Write(residual.TransformedX); writer.Write(residual.TransformedY); writer.Write(residual.TransformedZ);
            writer.Write(residual.ResidualX); writer.Write(residual.ResidualY); writer.Write(residual.ResidualZ);
            writer.Write(residual.ResidualNorm);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}

public readonly record struct C3DAffineMatrix3x4(
    double M11, double M12, double M13, double M14,
    double M21, double M22, double M23, double M24,
    double M31, double M32, double M33, double M34)
{
    public IReadOnlyList<double> Values =>
        [M11, M12, M13, M14, M21, M22, M23, M24, M31, M32, M33, M34];

    public (double X, double Y, double Z) Transform(double sourceX, double sourceY, double sourceZ) =>
        (M11 * sourceX + M12 * sourceY + M13 * sourceZ + M14,
         M21 * sourceX + M22 * sourceY + M23 * sourceZ + M24,
         M31 * sourceX + M32 * sourceY + M33 * sourceZ + M34);
}

public sealed record C3DAffineLandmarkResidual(
    string SourceEntityId,
    string SourceOutputRole,
    string SourceContentSha256,
    string ReferenceLandmarkId,
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
