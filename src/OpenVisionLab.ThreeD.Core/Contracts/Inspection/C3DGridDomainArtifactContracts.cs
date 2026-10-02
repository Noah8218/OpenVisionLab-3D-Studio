using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// One source-grid cell included by an authored inspection domain. The cell
/// is a row/column membership claim; it is not a height value or a physical
/// measurement.
/// </summary>
public sealed record C3DGridDomainArtifactCell(int Row, int Column);

/// <summary>
/// Source-bound authored domain produced from a recipe-owned GridPolygon.
/// This is deliberately distinct from ConnectedRegionArtifact, which remains
/// a detection output with connectivity and region metrics.
/// </summary>
public sealed record C3DGridDomainArtifact(
    string SchemaVersion,
    string ArtifactId,
    string Name,
    string SourceEntityId,
    string SourceContentSha256,
    string RootSourceSha256,
    string SelectionId,
    string SelectionContentSha256,
    string Unit,
    string FrameId,
    int GridWidth,
    int GridHeight,
    string CoordinateConvention,
    IReadOnlyList<C3DGridDomainArtifactCell> Cells,
    string MaskContentSha256,
    string ContentSha256)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string CurrentCoordinateConvention =
        "GridXGridYCellCenterFootprint";

    public static C3DGridDomainArtifact Create(
        string artifactId,
        string name,
        string sourceEntityId,
        string sourceContentSha256,
        string rootSourceSha256,
        string selectionId,
        string selectionContentSha256,
        string unit,
        string frameId,
        int gridWidth,
        int gridHeight,
        IReadOnlyList<C3DGridDomainArtifactCell> cells)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceContentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootSourceSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectionContentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);
        ArgumentNullException.ThrowIfNull(cells);

        var artifact = new C3DGridDomainArtifact(
            CurrentSchemaVersion,
            artifactId.Trim(),
            name.Trim(),
            sourceEntityId.Trim(),
            sourceContentSha256.Trim().ToUpperInvariant(),
            rootSourceSha256.Trim().ToUpperInvariant(),
            selectionId.Trim(),
            selectionContentSha256.Trim().ToUpperInvariant(),
            unit.Trim(),
            frameId.Trim(),
            gridWidth,
            gridHeight,
            CurrentCoordinateConvention,
            cells.Select(cell => new C3DGridDomainArtifactCell(cell.Row, cell.Column)).ToArray(),
            string.Empty,
            string.Empty);
        artifact = artifact with
        {
            MaskContentSha256 = CalculateMaskContentSha256(artifact),
        };
        artifact = artifact with
        {
            ContentSha256 = CalculateContentSha256(artifact),
        };

        var validity = C3DGridDomainArtifactValidator.Inspect(artifact);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                $"Grid-domain artifact is invalid: {string.Join(" ", validity.Errors)}");
        }

        return artifact;
    }

    /// <summary>
    /// Creates the stable identity of the exact recipe selection payload used
    /// to produce this artifact. Geometry order is significant.
    /// </summary>
    public static string CalculateSelectionContentSha256(ToolRecipeSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(selection.SourceBinding);
        ArgumentNullException.ThrowIfNull(selection.GridPolygon);

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("OpenVisionLab.C3DGridDomainSelection");
            writer.Write(selection.Id ?? string.Empty);
            writer.Write(selection.Kind ?? string.Empty);
            writer.Write(selection.RootSourceId ?? string.Empty);
            writer.Write(selection.FrameId ?? string.Empty);
            writer.Write(selection.SourceBinding.Format ?? string.Empty);
            writer.Write((selection.SourceBinding.ContentSha256 ?? string.Empty).ToUpperInvariant());
            writer.Write(selection.SourceBinding.GridWidth);
            writer.Write(selection.SourceBinding.GridHeight);
            writer.Write(selection.SourceBinding.OwnerEntityId ?? string.Empty);
            writer.Write((selection.SourceBinding.RootSourceContentSha256 ?? string.Empty).ToUpperInvariant());
            writer.Write(selection.SourceBinding.Unit ?? string.Empty);
            writer.Write(selection.SourceBinding.FrameId ?? string.Empty);
            writer.Write(selection.GridPolygon.Vertices?.Count ?? -1);
            foreach (var vertex in selection.GridPolygon.Vertices ?? [])
            {
                ArgumentNullException.ThrowIfNull(vertex);
                writer.Write(vertex.Row);
                writer.Write(vertex.Column);
            }
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    public static string CalculateMaskContentSha256(C3DGridDomainArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var cellCount = checked(artifact.GridWidth * artifact.GridHeight);
        var membership = new byte[cellCount];
        foreach (var cell in artifact.Cells ?? [])
        {
            if (cell is null || cell.Row < 0 || cell.Row >= artifact.GridHeight
                || cell.Column < 0 || cell.Column >= artifact.GridWidth)
            {
                continue;
            }

            membership[checked(cell.Row * artifact.GridWidth + cell.Column)] = 1;
        }

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("OpenVisionLab.C3DGridDomainMask");
            writer.Write(artifact.GridWidth);
            writer.Write(artifact.GridHeight);
            writer.Write(membership.Length);
            writer.Write(membership);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    public static string CalculateContentSha256(C3DGridDomainArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("OpenVisionLab.C3DGridDomainArtifact");
            writer.Write(artifact.SchemaVersion ?? string.Empty);
            writer.Write(artifact.ArtifactId ?? string.Empty);
            writer.Write(artifact.Name ?? string.Empty);
            writer.Write(artifact.SourceEntityId ?? string.Empty);
            writer.Write((artifact.SourceContentSha256 ?? string.Empty).ToUpperInvariant());
            writer.Write((artifact.RootSourceSha256 ?? string.Empty).ToUpperInvariant());
            writer.Write(artifact.SelectionId ?? string.Empty);
            writer.Write((artifact.SelectionContentSha256 ?? string.Empty).ToUpperInvariant());
            writer.Write(artifact.Unit ?? string.Empty);
            writer.Write(artifact.FrameId ?? string.Empty);
            writer.Write(artifact.GridWidth);
            writer.Write(artifact.GridHeight);
            writer.Write(artifact.CoordinateConvention ?? string.Empty);
            writer.Write((artifact.MaskContentSha256 ?? string.Empty).ToUpperInvariant());
            var cells = artifact.Cells ?? [];
            writer.Write(cells.Count);
            foreach (var cell in cells)
            {
                ArgumentNullException.ThrowIfNull(cell);
                writer.Write(cell.Row);
                writer.Write(cell.Column);
            }
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}
