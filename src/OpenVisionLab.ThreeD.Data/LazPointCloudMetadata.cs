using System.Globalization;
using System.Text;

namespace OpenVisionLab.ThreeD.Data;

public sealed record LazPointCloudMetadata(
    string SourcePath,
    string Version,
    string SystemIdentifier,
    string GeneratingSoftware,
    ushort CreationDayOfYear,
    ushort CreationYear,
    ushort HeaderSize,
    uint PointDataOffset,
    uint VariableLengthRecordCount,
    byte RawPointDataFormat,
    byte PointDataFormat,
    bool IsCompressed,
    ushort PointDataRecordLength,
    ulong PointCount,
    double XScale,
    double YScale,
    double ZScale,
    double XOffset,
    double YOffset,
    double ZOffset,
    double MinX,
    double MaxX,
    double MinY,
    double MaxY,
    double MinZ,
    double MaxZ,
    bool HasLaszipVlr)
{
    public static LazPointCloudMetadata Load(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream, Encoding.ASCII);

        if (ReadAscii(reader, 4) != "LASF")
        {
            throw new InvalidDataException($"Unsupported LAZ/LAS header: {path}");
        }

        reader.BaseStream.Position = 24;
        var versionMajor = reader.ReadByte();
        var versionMinor = reader.ReadByte();
        var systemIdentifier = ReadAscii(reader, 32);
        var generatingSoftware = ReadAscii(reader, 32);
        var creationDay = reader.ReadUInt16();
        var creationYear = reader.ReadUInt16();
        var headerSize = reader.ReadUInt16();
        var pointDataOffset = reader.ReadUInt32();
        var vlrCount = reader.ReadUInt32();
        var rawPointDataFormat = reader.ReadByte();
        var pointDataRecordLength = reader.ReadUInt16();
        var legacyPointCount = reader.ReadUInt32();

        reader.BaseStream.Position = 131;
        var xScale = reader.ReadDouble();
        var yScale = reader.ReadDouble();
        var zScale = reader.ReadDouble();
        var xOffset = reader.ReadDouble();
        var yOffset = reader.ReadDouble();
        var zOffset = reader.ReadDouble();
        var maxX = reader.ReadDouble();
        var minX = reader.ReadDouble();
        var maxY = reader.ReadDouble();
        var minY = reader.ReadDouble();
        var maxZ = reader.ReadDouble();
        var minZ = reader.ReadDouble();

        var hasLaszipVlr = ContainsLaszipVlr(reader, headerSize, pointDataOffset, vlrCount);
        var isCompressed = hasLaszipVlr || (rawPointDataFormat & 0x80) != 0;
        var pointDataFormat = (byte)(rawPointDataFormat & 0x3F);
        var pointCount = legacyPointCount != 0 ? legacyPointCount : ReadLas14PointCount(reader, headerSize);

        return new LazPointCloudMetadata(
            path,
            $"{versionMajor}.{versionMinor}",
            systemIdentifier,
            generatingSoftware,
            creationDay,
            creationYear,
            headerSize,
            pointDataOffset,
            vlrCount,
            rawPointDataFormat,
            pointDataFormat,
            isCompressed,
            pointDataRecordLength,
            pointCount,
            xScale,
            yScale,
            zScale,
            xOffset,
            yOffset,
            zOffset,
            minX,
            maxX,
            minY,
            maxY,
            minZ,
            maxZ,
            hasLaszipVlr);
    }

    public string FormatSummary() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Path.GetFileName(SourcePath)} | LAS {Version} | points {PointCount:N0} | format {PointDataFormat} | compressed {IsCompressed} | bounds X {MinX:F3}..{MaxX:F3}, Y {MinY:F3}..{MaxY:F3}, Z {MinZ:F3}..{MaxZ:F3}");

    private static bool ContainsLaszipVlr(BinaryReader reader, ushort headerSize, uint pointDataOffset, uint vlrCount)
    {
        reader.BaseStream.Position = headerSize;
        for (var i = 0; i < vlrCount && reader.BaseStream.Position + 54 <= pointDataOffset; i++)
        {
            reader.ReadUInt16();
            var userId = ReadAscii(reader, 16);
            reader.ReadUInt16();
            var recordLength = reader.ReadUInt16();
            var description = ReadAscii(reader, 32);
            if (userId.Contains("laszip", StringComparison.OrdinalIgnoreCase)
                || description.Contains("laszip", StringComparison.OrdinalIgnoreCase)
                || description.Contains("encoded", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            reader.BaseStream.Position += recordLength;
        }

        return false;
    }

    private static ulong ReadLas14PointCount(BinaryReader reader, ushort headerSize)
    {
        if (headerSize < 375)
        {
            return 0;
        }

        reader.BaseStream.Position = 247;
        return reader.ReadUInt64();
    }

    private static string ReadAscii(BinaryReader reader, int count) =>
        Encoding.ASCII.GetString(reader.ReadBytes(count)).TrimEnd('\0', ' ');
}
