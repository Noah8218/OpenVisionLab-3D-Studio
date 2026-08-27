using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

public enum C3DConnectedRegionConnectivity
{
    Four,
    Eight
}

public sealed record C3DConnectedRegionCell(int Row, int Column);

/// <summary>
/// Immutable, source-bound row-major foreground mask. The mask is an explicit
/// input contract; it does not infer geometry or rasterize a shape.
/// </summary>
public sealed class C3DConnectedRegionMask
{
    public const string ContractVersion = "1.0";

    private readonly bool[] foreground;
    private readonly IReadOnlyList<bool> readOnlyForeground;

    public C3DConnectedRegionMask(
        string maskId,
        string sourceEntityId,
        string sourceContentSha256,
        int width,
        int height,
        IReadOnlyList<bool> foreground)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(maskId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceContentSha256);
        ArgumentNullException.ThrowIfNull(foreground);
        if (width < 1 || height < 1
            || foreground.Count != checked(width * height))
        {
            throw new ArgumentException(
                "Connected-region mask dimensions and row-major values must agree.",
                nameof(foreground));
        }

        MaskId = maskId;
        SourceEntityId = sourceEntityId;
        SourceContentSha256 = sourceContentSha256;
        Width = width;
        Height = height;
        this.foreground = foreground.ToArray();
        readOnlyForeground = Array.AsReadOnly(this.foreground);
        ContentSha256 = CalculateContentSha256();
    }

    public string MaskId { get; }
    public string SourceEntityId { get; }
    public string SourceContentSha256 { get; }
    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<bool> Foreground => readOnlyForeground;
    public string ContentSha256 { get; }

    private string CalculateContentSha256()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("OpenVisionLab.C3DConnectedRegionMask");
            writer.Write(ContractVersion);
            writer.Write(MaskId);
            writer.Write(SourceEntityId);
            writer.Write(SourceContentSha256.ToUpperInvariant());
            writer.Write(Width);
            writer.Write(Height);
            writer.Write(foreground.Length);
            foreach (var value in foreground)
            {
                writer.Write(value);
            }
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}

public sealed record C3DConnectedRegionMetricOutput(
    string RegionId,
    int Index,
    int CellCount,
    int SeedRow,
    int SeedColumn,
    int MinimumRow,
    int MinimumColumn,
    int MaximumRow,
    int MaximumColumn,
    double Area,
    double CenterX,
    double CenterY,
    bool HasOrientation,
    double OrientationDegrees,
    double MinimumX,
    double MinimumY,
    double MaximumX,
    double MaximumY,
    double Width,
    double Height,
    string CoordinateConvention,
    IReadOnlyList<C3DConnectedRegionCell> Cells);

/// <summary>
/// Immutable connected-region detection evidence. Region arithmetic remains
/// owned by the Vision SDK; this contract carries Studio lineage and mapping.
/// </summary>
public sealed class C3DConnectedRegionOutput
{
    public const string ContractVersion = "1.0";

    private readonly IReadOnlyList<C3DConnectedRegionMetricOutput> readOnlyRegions;

    private C3DConnectedRegionOutput(
        string outputEntityId,
        string rootSourceEntityId,
        string rootSourceSha256,
        string inputEntityId,
        string inputContentSha256,
        string maskId,
        string maskSourceEntityId,
        string maskSourceContentSha256,
        string maskContentSha256,
        string unit,
        string frameId,
        int gridWidth,
        int gridHeight,
        C3DConnectedRegionConnectivity connectivity,
        int foregroundCellCount,
        int visitedCellCount,
        IReadOnlyList<C3DConnectedRegionMetricOutput> regions,
        string provenance,
        string contentSha256)
    {
        OutputEntityId = outputEntityId;
        RootSourceEntityId = rootSourceEntityId;
        RootSourceSha256 = rootSourceSha256;
        InputEntityId = inputEntityId;
        InputContentSha256 = inputContentSha256;
        MaskId = maskId;
        MaskSourceEntityId = maskSourceEntityId;
        MaskSourceContentSha256 = maskSourceContentSha256;
        MaskContentSha256 = maskContentSha256;
        Unit = unit;
        FrameId = frameId;
        GridWidth = gridWidth;
        GridHeight = gridHeight;
        Connectivity = connectivity;
        ForegroundCellCount = foregroundCellCount;
        VisitedCellCount = visitedCellCount;
        readOnlyRegions = regions;
        Provenance = provenance;
        ContentSha256 = contentSha256;
    }

    public string OutputEntityId { get; }
    public string RootSourceEntityId { get; }
    public string RootSourceSha256 { get; }
    public string InputEntityId { get; }
    public string InputContentSha256 { get; }
    public string MaskId { get; }
    public string MaskSourceEntityId { get; }
    public string MaskSourceContentSha256 { get; }
    public string MaskContentSha256 { get; }
    public string Unit { get; }
    public string FrameId { get; }
    public int GridWidth { get; }
    public int GridHeight { get; }
    public C3DConnectedRegionConnectivity Connectivity { get; }
    public int ForegroundCellCount { get; }
    public int VisitedCellCount { get; }
    public int RegionCount => readOnlyRegions.Count;
    public IReadOnlyList<C3DConnectedRegionMetricOutput> Regions => readOnlyRegions;
    public string Provenance { get; }
    public string ContentSha256 { get; }

    public static C3DConnectedRegionOutput Create(
        string outputEntityId,
        string rootSourceEntityId,
        string rootSourceSha256,
        string inputEntityId,
        string inputContentSha256,
        C3DConnectedRegionMask mask,
        string unit,
        string frameId,
        int gridWidth,
        int gridHeight,
        C3DConnectedRegionConnectivity connectivity,
        int foregroundCellCount,
        int visitedCellCount,
        IReadOnlyList<C3DConnectedRegionMetricOutput> regions,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootSourceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootSourceSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputContentSha256);
        ArgumentNullException.ThrowIfNull(mask);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (gridWidth < 1 || gridHeight < 1
            || mask.Width != gridWidth
            || mask.Height != gridHeight
            || foregroundCellCount < 1
            || visitedCellCount != foregroundCellCount)
        {
            throw new ArgumentException(
                "Connected-region output dimensions and counts are invalid.",
                nameof(regions));
        }

        var copiedRegions = regions
            .Select(region =>
            {
                ArgumentNullException.ThrowIfNull(region);
                ArgumentNullException.ThrowIfNull(region.Cells);
                return region with
                {
                    Cells = Array.AsReadOnly(region.Cells.ToArray())
                };
            })
            .ToArray();
        var readOnlyRegions = Array.AsReadOnly(copiedRegions);
        var hash = CalculateContentSha256(
            outputEntityId,
            rootSourceEntityId,
            rootSourceSha256,
            inputEntityId,
            inputContentSha256,
            mask,
            unit,
            frameId,
            gridWidth,
            gridHeight,
            connectivity,
            foregroundCellCount,
            visitedCellCount,
            copiedRegions);
        return new C3DConnectedRegionOutput(
            outputEntityId,
            rootSourceEntityId,
            rootSourceSha256,
            inputEntityId,
            inputContentSha256,
            mask.MaskId,
            mask.SourceEntityId,
            mask.SourceContentSha256,
            mask.ContentSha256,
            unit,
            frameId,
            gridWidth,
            gridHeight,
            connectivity,
            foregroundCellCount,
            visitedCellCount,
            readOnlyRegions,
            provenance,
            hash);
    }

    private static string CalculateContentSha256(
        string outputEntityId,
        string rootSourceEntityId,
        string rootSourceSha256,
        string inputEntityId,
        string inputContentSha256,
        C3DConnectedRegionMask mask,
        string unit,
        string frameId,
        int gridWidth,
        int gridHeight,
        C3DConnectedRegionConnectivity connectivity,
        int foregroundCellCount,
        int visitedCellCount,
        IReadOnlyList<C3DConnectedRegionMetricOutput> regions)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("OpenVisionLab.C3DConnectedRegionOutput");
            writer.Write(ContractVersion);
            writer.Write(outputEntityId);
            writer.Write(rootSourceEntityId);
            writer.Write(rootSourceSha256.ToUpperInvariant());
            writer.Write(inputEntityId);
            writer.Write(inputContentSha256.ToUpperInvariant());
            writer.Write(mask.MaskId);
            writer.Write(mask.SourceEntityId);
            writer.Write(mask.SourceContentSha256.ToUpperInvariant());
            writer.Write(mask.ContentSha256.ToUpperInvariant());
            writer.Write(unit);
            writer.Write(frameId);
            writer.Write(gridWidth);
            writer.Write(gridHeight);
            writer.Write(connectivity.ToString());
            writer.Write(foregroundCellCount);
            writer.Write(visitedCellCount);
            writer.Write(regions.Count);
            foreach (var region in regions)
            {
                writer.Write(region.RegionId);
                writer.Write(region.Index);
                writer.Write(region.CellCount);
                writer.Write(region.SeedRow);
                writer.Write(region.SeedColumn);
                writer.Write(region.MinimumRow);
                writer.Write(region.MinimumColumn);
                writer.Write(region.MaximumRow);
                writer.Write(region.MaximumColumn);
                writer.Write(region.Area);
                writer.Write(region.CenterX);
                writer.Write(region.CenterY);
                writer.Write(region.HasOrientation);
                if (region.HasOrientation)
                {
                    writer.Write(region.OrientationDegrees);
                }

                writer.Write(region.MinimumX);
                writer.Write(region.MinimumY);
                writer.Write(region.MaximumX);
                writer.Write(region.MaximumY);
                writer.Write(region.Width);
                writer.Write(region.Height);
                writer.Write(region.CoordinateConvention);
                writer.Write(region.Cells.Count);
                foreach (var cell in region.Cells)
                {
                    writer.Write(cell.Row);
                    writer.Write(cell.Column);
                }
            }
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}
