using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// Immutable full-XYZ closest-approach evidence for two published C3D line
/// features. The midpoint is a source-coordinate corner anchor only; it is
/// not a calibrated or physical intersection claim.
/// </summary>
public sealed class C3DLineIntersectionFeature
{
    public const string ContractVersion = "1.0";

    private C3DLineIntersectionFeature(
        string outputEntityId,
        IC3DLineGeometry firstLine,
        IC3DLineGeometry secondLine,
        double maximumClosestApproachDistance,
        double minimumAcuteAngleDegrees,
        double maximumSupportExtension,
        string outputRole,
        double cornerAnchorX,
        double cornerAnchorY,
        double cornerAnchorZ,
        double firstClosestX,
        double firstClosestY,
        double firstClosestZ,
        double secondClosestX,
        double secondClosestY,
        double secondClosestZ,
        double firstLineParameter,
        double secondLineParameter,
        double acuteAngleDegrees,
        double closestApproachDistance,
        double firstSupportMinimum,
        double firstSupportMaximum,
        double firstSupportExtension,
        double secondSupportMinimum,
        double secondSupportMaximum,
        double secondSupportExtension,
        string provenance,
        string contentSha256)
    {
        OutputEntityId = outputEntityId;
        FirstLineEntityId = firstLine.OutputEntityId;
        FirstLineContentSha256 = firstLine.ContentSha256;
        SecondLineEntityId = secondLine.OutputEntityId;
        SecondLineContentSha256 = secondLine.ContentSha256;
        RootSourceEntityId = firstLine.RootSourceEntityId;
        RootSourceSha256 = firstLine.RootSourceSha256;
        Unit = firstLine.Unit;
        FrameId = firstLine.FrameId;
        MaximumClosestApproachDistance = maximumClosestApproachDistance;
        MinimumAcuteAngleDegrees = minimumAcuteAngleDegrees;
        MaximumSupportExtension = maximumSupportExtension;
        OutputRole = outputRole;
        CornerAnchorX = cornerAnchorX;
        CornerAnchorY = cornerAnchorY;
        CornerAnchorZ = cornerAnchorZ;
        FirstClosestX = firstClosestX;
        FirstClosestY = firstClosestY;
        FirstClosestZ = firstClosestZ;
        SecondClosestX = secondClosestX;
        SecondClosestY = secondClosestY;
        SecondClosestZ = secondClosestZ;
        FirstLineParameter = firstLineParameter;
        SecondLineParameter = secondLineParameter;
        AcuteAngleDegrees = acuteAngleDegrees;
        ClosestApproachDistance = closestApproachDistance;
        FirstSupportMinimum = firstSupportMinimum;
        FirstSupportMaximum = firstSupportMaximum;
        FirstSupportExtension = firstSupportExtension;
        SecondSupportMinimum = secondSupportMinimum;
        SecondSupportMaximum = secondSupportMaximum;
        SecondSupportExtension = secondSupportExtension;
        Provenance = provenance;
        ContentSha256 = contentSha256;
    }

    public string OutputEntityId { get; }
    public string FirstLineEntityId { get; }
    public string FirstLineContentSha256 { get; }
    public string SecondLineEntityId { get; }
    public string SecondLineContentSha256 { get; }
    public string RootSourceEntityId { get; }
    public string RootSourceSha256 { get; }
    public string Unit { get; }
    public string FrameId { get; }
    public string CoordinateConvention => "column-rawHeight-row";
    public string DistanceUnit => "source-coordinate";
    public string ClosestApproachPolicy => "MidpointOfClosestPoints";
    public string ParallelPolicy => "RejectBelowMinimumAcuteAngle";
    public string SupportPolicy => "WithinInlierProjectionExtentsWithMaximumExtension";
    public double MaximumClosestApproachDistance { get; }
    public double MinimumAcuteAngleDegrees { get; }
    public double MaximumSupportExtension { get; }
    public string OutputRole { get; }
    public double CornerAnchorX { get; }
    public double CornerAnchorY { get; }
    public double CornerAnchorZ { get; }
    public double FirstClosestX { get; }
    public double FirstClosestY { get; }
    public double FirstClosestZ { get; }
    public double SecondClosestX { get; }
    public double SecondClosestY { get; }
    public double SecondClosestZ { get; }
    public double FirstLineParameter { get; }
    public double SecondLineParameter { get; }
    public double AcuteAngleDegrees { get; }
    public double ClosestApproachDistance { get; }
    public double FirstSupportMinimum { get; }
    public double FirstSupportMaximum { get; }
    public double FirstSupportExtension { get; }
    public double SecondSupportMinimum { get; }
    public double SecondSupportMaximum { get; }
    public double SecondSupportExtension { get; }
    public string Provenance { get; }
    public string ContentSha256 { get; }

    public static C3DLineIntersectionFeature Create(
        string outputEntityId,
        IC3DLineGeometry firstLine,
        IC3DLineGeometry secondLine,
        double maximumClosestApproachDistance,
        double minimumAcuteAngleDegrees,
        double maximumSupportExtension,
        string outputRole,
        double cornerAnchorX,
        double cornerAnchorY,
        double cornerAnchorZ,
        double firstClosestX,
        double firstClosestY,
        double firstClosestZ,
        double secondClosestX,
        double secondClosestY,
        double secondClosestZ,
        double firstLineParameter,
        double secondLineParameter,
        double acuteAngleDegrees,
        double closestApproachDistance,
        double firstSupportMinimum,
        double firstSupportMaximum,
        double firstSupportExtension,
        double secondSupportMinimum,
        double secondSupportMaximum,
        double secondSupportExtension,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputEntityId);
        ArgumentNullException.ThrowIfNull(firstLine);
        ArgumentNullException.ThrowIfNull(secondLine);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRole);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var hash = CalculateContentSha256(
            outputEntityId, firstLine, secondLine, maximumClosestApproachDistance,
            minimumAcuteAngleDegrees, maximumSupportExtension, outputRole,
            cornerAnchorX, cornerAnchorY, cornerAnchorZ,
            firstClosestX, firstClosestY, firstClosestZ,
            secondClosestX, secondClosestY, secondClosestZ,
            firstLineParameter, secondLineParameter, acuteAngleDegrees,
            closestApproachDistance, firstSupportMinimum, firstSupportMaximum,
            firstSupportExtension, secondSupportMinimum, secondSupportMaximum,
            secondSupportExtension);
        return new C3DLineIntersectionFeature(
            outputEntityId, firstLine, secondLine, maximumClosestApproachDistance,
            minimumAcuteAngleDegrees, maximumSupportExtension, outputRole,
            cornerAnchorX, cornerAnchorY, cornerAnchorZ,
            firstClosestX, firstClosestY, firstClosestZ,
            secondClosestX, secondClosestY, secondClosestZ,
            firstLineParameter, secondLineParameter, acuteAngleDegrees,
            closestApproachDistance, firstSupportMinimum, firstSupportMaximum,
            firstSupportExtension, secondSupportMinimum, secondSupportMaximum,
            secondSupportExtension, provenance, hash);
    }

    private static string CalculateContentSha256(
        string outputEntityId, IC3DLineGeometry firstLine, IC3DLineGeometry secondLine,
        double maximumClosestApproachDistance, double minimumAcuteAngleDegrees,
        double maximumSupportExtension, string outputRole,
        double cornerAnchorX, double cornerAnchorY, double cornerAnchorZ,
        double firstClosestX, double firstClosestY, double firstClosestZ,
        double secondClosestX, double secondClosestY, double secondClosestZ,
        double firstLineParameter, double secondLineParameter, double acuteAngleDegrees,
        double closestApproachDistance, double firstSupportMinimum,
        double firstSupportMaximum, double firstSupportExtension,
        double secondSupportMinimum, double secondSupportMaximum,
        double secondSupportExtension)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("OpenVisionLab.C3DLineIntersectionFeature");
        writer.Write(ContractVersion);
        writer.Write(outputEntityId);
        writer.Write(firstLine.OutputEntityId); writer.Write(firstLine.ContentSha256.ToUpperInvariant());
        writer.Write(secondLine.OutputEntityId); writer.Write(secondLine.ContentSha256.ToUpperInvariant());
        writer.Write(firstLine.RootSourceEntityId); writer.Write(firstLine.RootSourceSha256.ToUpperInvariant());
        writer.Write(firstLine.Unit); writer.Write(firstLine.FrameId); writer.Write("column-rawHeight-row");
        writer.Write(maximumClosestApproachDistance); writer.Write(minimumAcuteAngleDegrees);
        writer.Write(maximumSupportExtension); writer.Write(outputRole);
        writer.Write("MidpointOfClosestPoints"); writer.Write("RejectBelowMinimumAcuteAngle");
        writer.Write("WithinInlierProjectionExtentsWithMaximumExtension");
        writer.Write(cornerAnchorX); writer.Write(cornerAnchorY); writer.Write(cornerAnchorZ);
        writer.Write(firstClosestX); writer.Write(firstClosestY); writer.Write(firstClosestZ);
        writer.Write(secondClosestX); writer.Write(secondClosestY); writer.Write(secondClosestZ);
        writer.Write(firstLineParameter); writer.Write(secondLineParameter);
        writer.Write(acuteAngleDegrees); writer.Write(closestApproachDistance);
        writer.Write(firstSupportMinimum); writer.Write(firstSupportMaximum); writer.Write(firstSupportExtension);
        writer.Write(secondSupportMinimum); writer.Write(secondSupportMaximum); writer.Write(secondSupportExtension);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}
