using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// One source-grid cell belonging to a connected-region artifact. Row and
/// column are preserved exactly; no display-space resampling is implied.
/// </summary>
public sealed record C3DConnectedRegionArtifactCell(
    int Row,
    int Column);

/// <summary>
/// Row/column and optional physical-coordinate bounds for one connected
/// region. Coordinate values are evidence from the producing analysis.
/// </summary>
public sealed record C3DConnectedRegionArtifactBounding(
    int MinimumRow,
    int MinimumColumn,
    int MaximumRow,
    int MaximumColumn,
    double MinimumX,
    double MinimumY,
    double MaximumX,
    double MaximumY)
{
    public int Width => MaximumColumn - MinimumColumn + 1;
    public int Height => MaximumRow - MinimumRow + 1;
}

/// <summary>
/// Optional G12 metrics attached to one detected region. A missing value is
/// represented by null at the region level rather than by a fabricated metric.
/// </summary>
public sealed record C3DConnectedRegionArtifactMetrics(
    int CellCount,
    double Area,
    double CenterX,
    double CenterY,
    bool HasOrientation,
    double OrientationDegrees,
    C3DConnectedRegionArtifactBounding Bounding);

/// <summary>
/// Immutable region detection output that downstream recipe artifacts can
/// reference without copying the source height field or mask bytes.
/// </summary>
public sealed record C3DConnectedRegionArtifactRegion(
    int Index,
    int SeedRow,
    int SeedColumn,
    IReadOnlyList<C3DConnectedRegionArtifactCell> Cells,
    int MinimumRow,
    int MinimumColumn,
    int MaximumRow,
    int MaximumColumn,
    C3DConnectedRegionArtifactMetrics? Metrics)
{
    public int CellCount => Cells?.Count ?? 0;
}

/// <summary>
/// Source-bound, content-addressed connected-region output. This is a Core
/// contract only: it does not execute a tool, mutate a source, or make a
/// calibrated physical-measurement claim.
/// </summary>
public sealed record C3DConnectedRegionArtifact(
    string SchemaVersion,
    string ArtifactId,
    string Name,
    string SourceEntityId,
    string SourceContentSha256,
    string RootSourceSha256,
    string MaskContentSha256,
    string Unit,
    string FrameId,
    int GridWidth,
    int GridHeight,
    string Connectivity,
    string CoordinateConvention,
    double OriginX,
    double OriginY,
    double ColumnPitch,
    double RowPitch,
    string AreaUnit,
    IReadOnlyList<C3DConnectedRegionArtifactRegion> Regions,
    string ContentSha256)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string CurrentCoordinateConvention =
        "GridXGridYCellCenterFootprint";
    public const string FourConnectivity = "Four";
    public const string EightConnectivity = "Eight";

    public static C3DConnectedRegionArtifact Create(
        string artifactId,
        string name,
        string sourceEntityId,
        string sourceContentSha256,
        string rootSourceSha256,
        string maskContentSha256,
        string unit,
        string frameId,
        int gridWidth,
        int gridHeight,
        string connectivity,
        double originX,
        double originY,
        double columnPitch,
        double rowPitch,
        string areaUnit,
        IReadOnlyList<C3DConnectedRegionArtifactRegion> regions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceContentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootSourceSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(maskContentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectivity);
        ArgumentException.ThrowIfNullOrWhiteSpace(areaUnit);
        ArgumentNullException.ThrowIfNull(regions);

        var artifact = new C3DConnectedRegionArtifact(
            CurrentSchemaVersion,
            artifactId.Trim(),
            name.Trim(),
            sourceEntityId.Trim(),
            sourceContentSha256.Trim().ToUpperInvariant(),
            rootSourceSha256.Trim().ToUpperInvariant(),
            maskContentSha256.Trim().ToUpperInvariant(),
            unit.Trim(),
            frameId.Trim(),
            gridWidth,
            gridHeight,
            connectivity.Trim(),
            CurrentCoordinateConvention,
            originX,
            originY,
            columnPitch,
            rowPitch,
            areaUnit.Trim(),
            regions
                .Select(region => new C3DConnectedRegionArtifactRegion(
                    region.Index,
                    region.SeedRow,
                    region.SeedColumn,
                    (region.Cells ?? [])
                        .Select(cell => new C3DConnectedRegionArtifactCell(
                            cell.Row,
                            cell.Column))
                        .ToArray(),
                    region.MinimumRow,
                    region.MinimumColumn,
                    region.MaximumRow,
                    region.MaximumColumn,
                    region.Metrics is null
                        ? null
                        : new C3DConnectedRegionArtifactMetrics(
                            region.Metrics.CellCount,
                            region.Metrics.Area,
                            region.Metrics.CenterX,
                            region.Metrics.CenterY,
                            region.Metrics.HasOrientation,
                            region.Metrics.OrientationDegrees,
                            region.Metrics.Bounding is null
                                ? throw new InvalidDataException(
                                    "Connected-region metric bounding is required.")
                                : new C3DConnectedRegionArtifactBounding(
                                    region.Metrics.Bounding.MinimumRow,
                                    region.Metrics.Bounding.MinimumColumn,
                                    region.Metrics.Bounding.MaximumRow,
                                    region.Metrics.Bounding.MaximumColumn,
                                    region.Metrics.Bounding.MinimumX,
                                    region.Metrics.Bounding.MinimumY,
                                    region.Metrics.Bounding.MaximumX,
                                    region.Metrics.Bounding.MaximumY))))
                .ToArray(),
            string.Empty);
        artifact = artifact with
        {
            ContentSha256 = CalculateContentSha256(artifact)
        };

        var validity = C3DConnectedRegionArtifactValidator.Inspect(artifact);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                $"Connected-region artifact is invalid: {string.Join(" ", validity.Errors)}");
        }

        return artifact;
    }

    public static string CalculateContentSha256(
        C3DConnectedRegionArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(
                   stream,
                   Encoding.UTF8,
                   leaveOpen: true))
        {
            writer.Write("OpenVisionLab.C3DConnectedRegionArtifact");
            writer.Write(artifact.SchemaVersion ?? string.Empty);
            writer.Write(artifact.ArtifactId ?? string.Empty);
            writer.Write(artifact.Name ?? string.Empty);
            writer.Write(artifact.SourceEntityId ?? string.Empty);
            writer.Write((artifact.SourceContentSha256 ?? string.Empty).ToUpperInvariant());
            writer.Write((artifact.RootSourceSha256 ?? string.Empty).ToUpperInvariant());
            writer.Write((artifact.MaskContentSha256 ?? string.Empty).ToUpperInvariant());
            writer.Write(artifact.Unit ?? string.Empty);
            writer.Write(artifact.FrameId ?? string.Empty);
            writer.Write(artifact.GridWidth);
            writer.Write(artifact.GridHeight);
            writer.Write(artifact.Connectivity ?? string.Empty);
            writer.Write(artifact.CoordinateConvention ?? string.Empty);
            writer.Write(artifact.OriginX);
            writer.Write(artifact.OriginY);
            writer.Write(artifact.ColumnPitch);
            writer.Write(artifact.RowPitch);
            writer.Write(artifact.AreaUnit ?? string.Empty);

            if (artifact.Regions is null)
            {
                writer.Write(-1);
            }
            else
            {
                writer.Write(artifact.Regions.Count);
                foreach (var region in artifact.Regions)
                {
                    ArgumentNullException.ThrowIfNull(region);
                    writer.Write(region.Index);
                    writer.Write(region.SeedRow);
                    writer.Write(region.SeedColumn);
                    writer.Write(region.MinimumRow);
                    writer.Write(region.MinimumColumn);
                    writer.Write(region.MaximumRow);
                    writer.Write(region.MaximumColumn);
                    var cells = region.Cells ?? [];
                    writer.Write(cells.Count);
                    foreach (var cell in cells)
                    {
                        ArgumentNullException.ThrowIfNull(cell);
                        writer.Write(cell.Row);
                        writer.Write(cell.Column);
                    }

                    writer.Write(region.Metrics is not null);
                    if (region.Metrics is not null)
                    {
                        var metrics = region.Metrics;
                        writer.Write(metrics.CellCount);
                        writer.Write(metrics.Area);
                        writer.Write(metrics.CenterX);
                        writer.Write(metrics.CenterY);
                        writer.Write(metrics.HasOrientation);
                        writer.Write(metrics.OrientationDegrees);
                        WriteBounding(writer, metrics.Bounding);
                    }
                }
            }
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteBounding(
        BinaryWriter writer,
        C3DConnectedRegionArtifactBounding? bounding)
    {
        ArgumentNullException.ThrowIfNull(bounding);
        writer.Write(bounding.MinimumRow);
        writer.Write(bounding.MinimumColumn);
        writer.Write(bounding.MaximumRow);
        writer.Write(bounding.MaximumColumn);
        writer.Write(bounding.MinimumX);
        writer.Write(bounding.MinimumY);
        writer.Write(bounding.MaximumX);
        writer.Write(bounding.MaximumY);
    }
}
