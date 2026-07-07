using System.Globalization;
using System.Numerics;
using LASzip.Net;

namespace OpenVisionLab.ThreeD.Data;

public sealed class LazPointCloud
{
    private LazPointCloud(
        string sourcePath,
        LazPointCloudMetadata metadata,
        bool isCompressed,
        ulong decodedPointCount,
        int sampleStride,
        LazPointCloudPoint[] sampledPoints,
        bool hasRgb,
        bool boundsMatch,
        double minX,
        double maxX,
        double minY,
        double maxY,
        double minZ,
        double maxZ,
        double averageRed,
        double averageGreen,
        double averageBlue)
    {
        SourcePath = sourcePath;
        Metadata = metadata;
        IsCompressed = isCompressed;
        DecodedPointCount = decodedPointCount;
        SampleStride = sampleStride;
        SampledPoints = sampledPoints;
        HasRgb = hasRgb;
        BoundsMatch = boundsMatch;
        MinX = minX;
        MaxX = maxX;
        MinY = minY;
        MaxY = maxY;
        MinZ = minZ;
        MaxZ = maxZ;
        AverageRed = averageRed;
        AverageGreen = averageGreen;
        AverageBlue = averageBlue;
    }

    public string SourcePath { get; }

    public LazPointCloudMetadata Metadata { get; }

    public bool IsCompressed { get; }

    public ulong DecodedPointCount { get; }

    public int SampleStride { get; }

    public LazPointCloudPoint[] SampledPoints { get; }

    public bool HasRgb { get; }

    public bool BoundsMatch { get; }

    public double MinX { get; }

    public double MaxX { get; }

    public double MinY { get; }

    public double MaxY { get; }

    public double MinZ { get; }

    public double MaxZ { get; }

    public double AverageRed { get; }

    public double AverageGreen { get; }

    public double AverageBlue { get; }

    public static LazPointCloud Load(string path, int maxSampledPoints = 50000)
    {
        if (maxSampledPoints < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSampledPoints), "Sample point limit must be positive.");
        }

        var fullPath = Path.GetFullPath(path);
        var metadata = LazPointCloudMetadata.Load(fullPath);
        var reader = new laszip();
        if (reader.open_reader(fullPath, out var isCompressed) != 0)
        {
            throw new InvalidDataException($"Unable to open LAZ/LAS file: {reader.get_error()}");
        }

        try
        {
            var header = reader.get_header_pointer();
            reader.get_point_count(out long apiPointCount);
            var decodedPointCount = apiPointCount > 0
                ? checked((ulong)apiPointCount)
                : metadata.PointCount;
            var point = reader.get_point_pointer();
            var sampleStride = checked((int)Math.Max(1, decodedPointCount / (ulong)maxSampledPoints));
            var sampledPoints = new List<LazPointCloudPoint>(Math.Min(maxSampledPoints, checked((int)Math.Min(decodedPointCount, int.MaxValue))));
            var minX = double.PositiveInfinity;
            var minY = double.PositiveInfinity;
            var minZ = double.PositiveInfinity;
            var maxX = double.NegativeInfinity;
            var maxY = double.NegativeInfinity;
            var maxZ = double.NegativeInfinity;
            ulong redSum = 0;
            ulong greenSum = 0;
            ulong blueSum = 0;
            var hasRgb = false;

            for (ulong index = 0; index < decodedPointCount; index++)
            {
                if (reader.read_point() != 0)
                {
                    throw new InvalidDataException($"Unable to read LAZ/LAS point {index}: {reader.get_error()}");
                }

                var x = point.X * header.x_scale_factor + header.x_offset;
                var y = point.Y * header.y_scale_factor + header.y_offset;
                var z = point.Z * header.z_scale_factor + header.z_offset;
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
                minZ = Math.Min(minZ, z);
                maxZ = Math.Max(maxZ, z);

                var red = (ushort)0;
                var green = (ushort)0;
                var blue = (ushort)0;
                if (point.rgb is { Length: >= 3 })
                {
                    hasRgb = true;
                    red = point.rgb[0];
                    green = point.rgb[1];
                    blue = point.rgb[2];
                    redSum += red;
                    greenSum += green;
                    blueSum += blue;
                }

                if (index % (ulong)sampleStride == 0 && sampledPoints.Count < maxSampledPoints)
                {
                    sampledPoints.Add(new LazPointCloudPoint(new Vector3((float)x, (float)y, (float)z), red, green, blue));
                }
            }

            var boundsMatch = NearlyEqual(minX, metadata.MinX)
                && NearlyEqual(maxX, metadata.MaxX)
                && NearlyEqual(minY, metadata.MinY)
                && NearlyEqual(maxY, metadata.MaxY)
                && NearlyEqual(minZ, metadata.MinZ)
                && NearlyEqual(maxZ, metadata.MaxZ);
            var pointCount = decodedPointCount == 0 ? 1.0 : decodedPointCount;
            return new LazPointCloud(
                fullPath,
                metadata,
                isCompressed,
                decodedPointCount,
                sampleStride,
                sampledPoints.ToArray(),
                hasRgb,
                boundsMatch,
                minX,
                maxX,
                minY,
                maxY,
                minZ,
                maxZ,
                redSum / pointCount,
                greenSum / pointCount,
                blueSum / pointCount);
        }
        finally
        {
            reader.close_reader();
        }
    }

    public string FormatContractLine() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"LAZ|loaded=True|decoder=points-decoded|source={SourcePath}|compressed={IsCompressed}|pointFormat={Metadata.PointDataFormat}|decodedPoints={DecodedPointCount}|sampledPoints={SampledPoints.Length}|sampleStride={SampleStride}|rgb={HasRgb}|boundsX={MinX:F3}..{MaxX:F3}|boundsY={MinY:F3}..{MaxY:F3}|boundsZ={MinZ:F3}..{MaxZ:F3}|boundsMatch={BoundsMatch}|avgRgb={AverageRed:F3},{AverageGreen:F3},{AverageBlue:F3}");

    private static bool NearlyEqual(double actual, double expected) =>
        Math.Abs(actual - expected) <= 0.001;
}

public readonly record struct LazPointCloudPoint(Vector3 Position, ushort Red, ushort Green, ushort Blue);
