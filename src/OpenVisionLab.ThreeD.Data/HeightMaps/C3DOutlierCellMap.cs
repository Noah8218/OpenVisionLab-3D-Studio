using System.Security.Cryptography;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Immutable, coordinate-true mask of cells removed by one outlier rule.
/// Bit index is row * width + column. One means removed as an outlier and
/// bits are packed least-significant-bit first without flipping or sampling.
/// Original missing cells are never marked as outliers.
/// </summary>
public sealed class C3DOutlierCellMap
{
    public const string ContractVersion = "1.0";
    public const string Encoding =
        "row-major-bitset;1=removed-outlier;lsb-first;identity=prefix+version+width+height+byteLength+bytes";

    private const string IdentityPrefix =
        "OpenVisionLab.RemoveOutlierPixelsMask";

    private readonly byte[] packedBits;

    private C3DOutlierCellMap(
        int width,
        int height,
        int outlierCellCount,
        byte[] packedBits,
        string sha256)
    {
        Width = width;
        Height = height;
        CellCount = checked(width * height);
        OutlierCellCount = outlierCellCount;
        this.packedBits = packedBits;
        Sha256 = sha256;
    }

    public int Width { get; }
    public int Height { get; }
    public int CellCount { get; }
    public int OutlierCellCount { get; }
    public int PackedByteLength => packedBits.Length;
    public ReadOnlyMemory<byte> PackedBits => packedBits;
    public string Sha256 { get; }

    public static C3DOutlierCellMap Create(
        int width,
        int height,
        IReadOnlyCollection<int> outlierIndices)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        ArgumentNullException.ThrowIfNull(outlierIndices);
        var cellCount = checked(width * height);
        var packedBits = new byte[(cellCount + 7) / 8];
        var uniqueCount = 0;
        foreach (var index in outlierIndices)
        {
            if (index < 0 || index >= cellCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(outlierIndices),
                    $"Outlier index {index} is outside the {width} x {height} grid.");
            }

            var bit = (byte)(1 << (index % 8));
            if ((packedBits[index / 8] & bit) != 0)
            {
                continue;
            }

            packedBits[index / 8] |= bit;
            uniqueCount++;
        }

        return new C3DOutlierCellMap(
            width,
            height,
            uniqueCount,
            packedBits,
            CalculateSha256(width, height, packedBits));
    }

    public bool TryIsOutlier(int column, int row, out bool isOutlier)
    {
        if (column < 0 || column >= Width || row < 0 || row >= Height)
        {
            isOutlier = false;
            return false;
        }

        isOutlier = IsOutlierIndex(checked(row * Width + column));
        return true;
    }

    internal bool IsOutlierIndex(int index) =>
        (packedBits[index / 8] & (1 << (index % 8))) != 0;

    private static string CalculateSha256(
        int width,
        int height,
        ReadOnlySpan<byte> packedBits)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(
            stream,
            System.Text.Encoding.UTF8,
            leaveOpen: true);
        writer.Write(IdentityPrefix);
        writer.Write(ContractVersion);
        writer.Write(width);
        writer.Write(height);
        writer.Write(packedBits.Length);
        writer.Write(packedBits);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}
