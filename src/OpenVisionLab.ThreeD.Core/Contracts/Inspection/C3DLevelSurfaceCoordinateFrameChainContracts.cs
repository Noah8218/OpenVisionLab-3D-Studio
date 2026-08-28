using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

public enum C3DCoordinateFrameRole
{
    Source,
    Reference,
    Result,
    Level
}

/// <summary>
/// Named frame identity used by the Level Surface evidence graph. Reference
/// is a semantic ROI role; it may intentionally share the source frame.
/// </summary>
public sealed class C3DCoordinateFrameNode
{
    private readonly string[] selectionIds;

    public C3DCoordinateFrameNode(
        C3DCoordinateFrameRole role,
        string frameId,
        string unit,
        string convention,
        string? entityId,
        string? contentSha256,
        IEnumerable<string>? selectionIds = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(convention);
        this.selectionIds = (selectionIds ?? []).ToArray();
        if (this.selectionIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Coordinate-frame selection IDs must be non-empty.", nameof(selectionIds));
        }

        Role = role;
        FrameId = frameId;
        Unit = unit;
        Convention = convention;
        EntityId = entityId;
        ContentSha256 = contentSha256;
    }

    public C3DCoordinateFrameRole Role { get; }
    public string FrameId { get; }
    public string Unit { get; }
    public string Convention { get; }
    public string? EntityId { get; }
    public string? ContentSha256 { get; }
    public IReadOnlyList<string> SelectionIds => selectionIds;
}

public sealed record C3DCoordinateFrameLink(
    C3DCoordinateFrameRole FromRole,
    C3DCoordinateFrameRole ToRole,
    string Relation,
    string? TransformEntityId,
    string? TransformContentSha256);

/// <summary>
/// Deterministic, source-preserving coordinate-frame hierarchy for one Level
/// Surface evaluation. This names the authored reference role, the derived
/// result role, and the reusable Level Frame without performing geometry.
/// </summary>
public sealed class C3DLevelSurfaceCoordinateFrameChain
{
    public const string ContractVersion = "1.0";
    public const string ChainSemantics = "SourceReferenceResultAndLevelFrame";
    public const string ReferenceFramePolicy = "ReferenceRegionsUseSourceFrame";
    public const string ResultFramePolicy = "LevelSurfaceOutputPreservesSourceFrame";
    public const string SourceToReferenceRelation = "AuthoredReferenceRegionsInSourceFrame";
    public const string SourceToLevelRelation = "LevelFrameSourceToFrame";
    public const string SourceToResultRelation = "LevelSurfaceHeightDetrendPreservesSourceFrame";

    private readonly C3DCoordinateFrameLink[] links;

    private C3DLevelSurfaceCoordinateFrameChain(
        string chainId,
        C3DCoordinateFrameNode source,
        C3DCoordinateFrameNode reference,
        C3DCoordinateFrameNode? result,
        C3DCoordinateFrameNode level,
        C3DCoordinateFrameLink[] links,
        string rootSourceEntityId,
        string rootSourceSha256,
        string sourceUnit,
        string sourceFrameId,
        string provenance,
        string contentSha256)
    {
        ChainId = chainId;
        Source = source;
        Reference = reference;
        Result = result;
        Level = level;
        this.links = links;
        RootSourceEntityId = rootSourceEntityId;
        RootSourceSha256 = rootSourceSha256;
        SourceUnit = sourceUnit;
        SourceFrameId = sourceFrameId;
        Provenance = provenance;
        ContentSha256 = contentSha256;
    }

    public string ChainId { get; }
    public C3DCoordinateFrameNode Source { get; }
    public C3DCoordinateFrameNode Reference { get; }
    public C3DCoordinateFrameNode? Result { get; }
    public C3DCoordinateFrameNode Level { get; }
    public IReadOnlyList<C3DCoordinateFrameLink> Links => links;
    public string RootSourceEntityId { get; }
    public string RootSourceSha256 { get; }
    public string SourceUnit { get; }
    public string SourceFrameId { get; }
    public string Provenance { get; }
    public string ContentSha256 { get; }

    public static C3DLevelSurfaceCoordinateFrameChain Create(
        string chainId,
        C3DCoordinateFrameNode source,
        C3DCoordinateFrameNode reference,
        C3DCoordinateFrameNode? result,
        C3DCoordinateFrameNode level,
        IReadOnlyList<C3DCoordinateFrameLink> links,
        string rootSourceEntityId,
        string rootSourceSha256,
        string sourceUnit,
        string sourceFrameId,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chainId);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootSourceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootSourceSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUnit);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFrameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);

        ValidateNode(source, C3DCoordinateFrameRole.Source, requireEntity: true, requireContent: true);
        ValidateNode(reference, C3DCoordinateFrameRole.Reference, requireEntity: true, requireContent: true);
        ValidateNode(level, C3DCoordinateFrameRole.Level, requireEntity: true, requireContent: true);
        if (result is not null)
        {
            ValidateNode(result, C3DCoordinateFrameRole.Result, requireEntity: true, requireContent: true);
        }

        if (!string.Equals(source.EntityId, rootSourceEntityId, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(source.ContentSha256)
            || !string.Equals(source.Unit, sourceUnit, StringComparison.Ordinal)
            || !string.Equals(source.FrameId, sourceFrameId, StringComparison.Ordinal)
            || !string.Equals(reference.FrameId, source.FrameId, StringComparison.Ordinal)
            || !string.Equals(reference.Unit, source.Unit, StringComparison.Ordinal)
            || !string.Equals(reference.Convention, C3DLevelFrameArtifact.SourceCoordinateConvention, StringComparison.Ordinal)
            || !string.Equals(source.Convention, C3DLevelFrameArtifact.SourceCoordinateConvention, StringComparison.Ordinal)
            || reference.SelectionIds.Count == 0
            || reference.SelectionIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != reference.SelectionIds.Count)
        {
            throw new InvalidDataException("Coordinate-frame source and authored reference identities are inconsistent.");
        }

        if (!string.Equals(level.Unit, source.Unit, StringComparison.Ordinal)
            || string.Equals(level.FrameId, source.FrameId, StringComparison.Ordinal)
            || !string.Equals(level.Convention, C3DLevelFrameArtifact.FrameCoordinateConvention, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Coordinate-frame Level Frame must be a distinct derived frame with the source unit.");
        }

        if (result is not null
            && (!string.Equals(result.FrameId, source.FrameId, StringComparison.Ordinal)
                || !string.Equals(result.Unit, source.Unit, StringComparison.Ordinal)
                || !string.Equals(result.Convention, C3DLevelFrameArtifact.SourceCoordinateConvention, StringComparison.Ordinal)
                || result.SelectionIds.Count != 0))
        {
            throw new InvalidDataException("Level Surface result must preserve the source frame and contain no authored ROI selections.");
        }

        var expectedLinkCount = result is null ? 2 : 3;
        if (links.Count != expectedLinkCount)
        {
            throw new InvalidDataException($"Level Surface frame chain requires {expectedLinkCount} named links.");
        }

        var linkCopy = links.ToArray();
        RequireLink(linkCopy, C3DCoordinateFrameRole.Source, C3DCoordinateFrameRole.Reference, SourceToReferenceRelation, requireTransform: false);
        RequireLink(
            linkCopy,
            C3DCoordinateFrameRole.Source,
            C3DCoordinateFrameRole.Level,
            SourceToLevelRelation,
            requireTransform: true,
            expectedTarget: level);
        if (result is not null)
        {
            RequireLink(linkCopy, C3DCoordinateFrameRole.Source, C3DCoordinateFrameRole.Result, SourceToResultRelation, requireTransform: true);
        }

        var normalizedRootHash = rootSourceSha256.ToUpperInvariant();
        var contentSha256 = CalculateContentSha256(
            chainId,
            source,
            reference,
            result,
            level,
            linkCopy,
            rootSourceEntityId,
            normalizedRootHash,
            sourceUnit,
            sourceFrameId,
            provenance);
        return new C3DLevelSurfaceCoordinateFrameChain(
            chainId,
            source,
            reference,
            result,
            level,
            linkCopy,
            rootSourceEntityId,
            normalizedRootHash,
            sourceUnit,
            sourceFrameId,
            provenance,
            contentSha256);
    }

    private static void ValidateNode(
        C3DCoordinateFrameNode node,
        C3DCoordinateFrameRole expectedRole,
        bool requireEntity,
        bool requireContent)
    {
        if (node.Role != expectedRole
            || (requireEntity && string.IsNullOrWhiteSpace(node.EntityId))
            || (requireContent && string.IsNullOrWhiteSpace(node.ContentSha256)))
        {
            throw new InvalidDataException($"Coordinate-frame node '{expectedRole}' is incomplete or has the wrong role.");
        }
    }

    private static void RequireLink(
        IReadOnlyList<C3DCoordinateFrameLink> links,
        C3DCoordinateFrameRole fromRole,
        C3DCoordinateFrameRole toRole,
        string relation,
        bool requireTransform,
        C3DCoordinateFrameNode? expectedTarget = null)
    {
        var matches = links.Where(link =>
            link.FromRole == fromRole
            && link.ToRole == toRole
            && string.Equals(link.Relation, relation, StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidDataException($"Coordinate-frame link '{relation}' is missing or duplicated.");
        }

        var link = matches[0];
        if (requireTransform
            && (string.IsNullOrWhiteSpace(link.TransformEntityId)
                || string.IsNullOrWhiteSpace(link.TransformContentSha256)))
        {
            throw new InvalidDataException($"Coordinate-frame link '{relation}' requires a typed transform identity.");
        }

        if (expectedTarget is not null
            && (!string.Equals(link.TransformEntityId, expectedTarget.EntityId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(link.TransformContentSha256, expectedTarget.ContentSha256, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException($"Coordinate-frame link '{relation}' does not match its target frame identity.");
        }
    }

    private static string CalculateContentSha256(
        string chainId,
        C3DCoordinateFrameNode source,
        C3DCoordinateFrameNode reference,
        C3DCoordinateFrameNode? result,
        C3DCoordinateFrameNode level,
        IReadOnlyList<C3DCoordinateFrameLink> links,
        string rootSourceEntityId,
        string rootSourceSha256,
        string sourceUnit,
        string sourceFrameId,
        string provenance)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("OpenVisionLab.C3DLevelSurfaceCoordinateFrameChain");
        writer.Write(ContractVersion);
        writer.Write(ChainSemantics);
        writer.Write(ReferenceFramePolicy);
        writer.Write(ResultFramePolicy);
        writer.Write(chainId);
        writer.Write(rootSourceEntityId);
        writer.Write(rootSourceSha256);
        writer.Write(sourceUnit);
        writer.Write(sourceFrameId);
        writer.Write(provenance);
        WriteNode(writer, source);
        WriteNode(writer, reference);
        writer.Write(result is not null);
        if (result is not null) WriteNode(writer, result);
        WriteNode(writer, level);
        writer.Write(links.Count);
        foreach (var link in links)
        {
            writer.Write((int)link.FromRole);
            writer.Write((int)link.ToRole);
            writer.Write(link.Relation);
            WriteNullable(writer, link.TransformEntityId);
            WriteNullable(writer, link.TransformContentSha256);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteNode(BinaryWriter writer, C3DCoordinateFrameNode node)
    {
        writer.Write((int)node.Role);
        writer.Write(node.FrameId);
        writer.Write(node.Unit);
        writer.Write(node.Convention);
        WriteNullable(writer, node.EntityId);
        WriteNullable(writer, node.ContentSha256?.ToUpperInvariant());
        writer.Write(node.SelectionIds.Count);
        foreach (var selectionId in node.SelectionIds) writer.Write(selectionId);
    }

    private static void WriteNullable(BinaryWriter writer, string? value)
    {
        writer.Write(value is not null);
        if (value is not null) writer.Write(value);
    }
}
