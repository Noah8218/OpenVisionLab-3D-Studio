using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// Immutable source-to-reference landmark evidence. It proves only that four
/// explicit published source anchors pass the authored correspondence gate; it
/// does not calculate or apply an affine transform.
/// </summary>
public sealed class C3DLandmarkCorrespondenceSet
{
    public const string ContractVersion = "1.0";

    private C3DLandmarkCorrespondenceSet(
        string outputEntityId,
        IReadOnlyList<C3DLandmarkCorrespondencePair> pairs,
        string rootSourceEntityId,
        string rootSourceSha256,
        string sourceUnit,
        string sourceFrameId,
        string referenceFrameId,
        string referenceUnit,
        string referenceProvenance,
        string referenceRevision,
        double minimumNormalizedTetrahedronVolume,
        int sourceRank,
        int referenceRank,
        double sourceNormalizedTetrahedronVolume,
        double referenceNormalizedTetrahedronVolume,
        string provenance,
        string contentSha256)
    {
        OutputEntityId = outputEntityId;
        Pairs = pairs;
        RootSourceEntityId = rootSourceEntityId;
        RootSourceSha256 = rootSourceSha256;
        SourceUnit = sourceUnit;
        SourceFrameId = sourceFrameId;
        ReferenceFrameId = referenceFrameId;
        ReferenceUnit = referenceUnit;
        ReferenceProvenance = referenceProvenance;
        ReferenceRevision = referenceRevision;
        MinimumNormalizedTetrahedronVolume = minimumNormalizedTetrahedronVolume;
        SourceRank = sourceRank;
        ReferenceRank = referenceRank;
        SourceNormalizedTetrahedronVolume = sourceNormalizedTetrahedronVolume;
        ReferenceNormalizedTetrahedronVolume = referenceNormalizedTetrahedronVolume;
        Provenance = provenance;
        ContentSha256 = contentSha256;
    }

    public string OutputEntityId { get; }
    public IReadOnlyList<C3DLandmarkCorrespondencePair> Pairs { get; }
    public string RootSourceEntityId { get; }
    public string RootSourceSha256 { get; }
    public string SourceUnit { get; }
    public string SourceFrameId { get; }
    public string SourceCoordinateConvention => "column-rawHeight-row";
    public string ReferenceFrameId { get; }
    public string ReferenceUnit { get; }
    public string ReferenceProvenance { get; }
    public string ReferenceRevision { get; }
    public string PairCountPolicy => "ExactlyFour";
    public string SourceArtifactPolicy => "CurrentPublishedCornerAnchor";
    public string AffineIndependencePolicy => "RequireNonDegenerateTetrahedra";
    public double MinimumNormalizedTetrahedronVolume { get; }
    public int SourceRank { get; }
    public int ReferenceRank { get; }
    public double SourceNormalizedTetrahedronVolume { get; }
    public double ReferenceNormalizedTetrahedronVolume { get; }
    public string Provenance { get; }
    public string ContentSha256 { get; }

    public static C3DLandmarkCorrespondenceSet Create(
        string outputEntityId,
        IReadOnlyList<C3DLandmarkCorrespondencePair> pairs,
        string rootSourceEntityId,
        string rootSourceSha256,
        string sourceUnit,
        string sourceFrameId,
        string referenceFrameId,
        string referenceUnit,
        string referenceProvenance,
        string referenceRevision,
        double minimumNormalizedTetrahedronVolume,
        int sourceRank,
        int referenceRank,
        double sourceNormalizedTetrahedronVolume,
        double referenceNormalizedTetrahedronVolume,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputEntityId);
        ArgumentNullException.ThrowIfNull(pairs);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootSourceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootSourceSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUnit);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFrameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceFrameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceUnit);
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceProvenance);
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceRevision);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var copiedPairs = pairs.ToArray();
        var contentSha256 = CalculateContentSha256(
            outputEntityId, copiedPairs, rootSourceEntityId, rootSourceSha256,
            sourceUnit, sourceFrameId, referenceFrameId, referenceUnit,
            referenceProvenance, referenceRevision, minimumNormalizedTetrahedronVolume,
            sourceRank, referenceRank, sourceNormalizedTetrahedronVolume,
            referenceNormalizedTetrahedronVolume);
        return new C3DLandmarkCorrespondenceSet(
            outputEntityId, copiedPairs, rootSourceEntityId, rootSourceSha256,
            sourceUnit, sourceFrameId, referenceFrameId, referenceUnit,
            referenceProvenance, referenceRevision, minimumNormalizedTetrahedronVolume,
            sourceRank, referenceRank, sourceNormalizedTetrahedronVolume,
            referenceNormalizedTetrahedronVolume, provenance, contentSha256);
    }

    private static string CalculateContentSha256(
        string outputEntityId,
        IReadOnlyList<C3DLandmarkCorrespondencePair> pairs,
        string rootSourceEntityId,
        string rootSourceSha256,
        string sourceUnit,
        string sourceFrameId,
        string referenceFrameId,
        string referenceUnit,
        string referenceProvenance,
        string referenceRevision,
        double minimumNormalizedTetrahedronVolume,
        int sourceRank,
        int referenceRank,
        double sourceNormalizedTetrahedronVolume,
        double referenceNormalizedTetrahedronVolume)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("OpenVisionLab.C3DLandmarkCorrespondenceSet");
        writer.Write(ContractVersion);
        writer.Write(outputEntityId);
        writer.Write(rootSourceEntityId); writer.Write(rootSourceSha256.ToUpperInvariant());
        writer.Write(sourceUnit); writer.Write(sourceFrameId); writer.Write("column-rawHeight-row");
        writer.Write(referenceFrameId); writer.Write(referenceUnit);
        writer.Write(referenceProvenance); writer.Write(referenceRevision);
        writer.Write("ExactlyFour"); writer.Write("CurrentPublishedCornerAnchor"); writer.Write("RequireNonDegenerateTetrahedra");
        writer.Write(minimumNormalizedTetrahedronVolume);
        writer.Write(sourceRank); writer.Write(referenceRank);
        writer.Write(sourceNormalizedTetrahedronVolume); writer.Write(referenceNormalizedTetrahedronVolume);
        writer.Write(pairs.Count);
        foreach (var pair in pairs)
        {
            writer.Write(pair.SourceEntityId); writer.Write(pair.SourceOutputRole); writer.Write(pair.SourceContentSha256.ToUpperInvariant());
            writer.Write(pair.SourceX); writer.Write(pair.SourceY); writer.Write(pair.SourceZ);
            writer.Write(pair.ReferenceLandmarkId); writer.Write(pair.ReferenceX); writer.Write(pair.ReferenceY); writer.Write(pair.ReferenceZ);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}

public sealed record C3DLandmarkCorrespondencePair(
    string SourceEntityId,
    string SourceOutputRole,
    string SourceContentSha256,
    double SourceX,
    double SourceY,
    double SourceZ,
    string ReferenceLandmarkId,
    double ReferenceX,
    double ReferenceY,
    double ReferenceZ);

