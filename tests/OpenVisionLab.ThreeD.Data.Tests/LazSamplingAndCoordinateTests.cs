using System.Numerics;
using System.Text;
using OpenVisionLab.ThreeD.Data;
using Xunit;

#pragma warning disable CS0618

namespace OpenVisionLab.ThreeD.Data.Tests;

public sealed class LazSamplingAndCoordinateTests
{
    [Fact]
    public void SamplingCoversTheFullIndexRangeForBoundaryBudgets()
    {
        const int limit = 10;
        var pointCounts = new[] { 9, 10, 11, 15, 19, 21 };
        var root = GetDirectory();

        try
        {
            foreach (var pointCount in pointCounts)
            {
                var path = Path.Combine(root, $"points-{pointCount}.las");
                WriteLas(path, pointCount);
                var pointCloud = LazPointCloud.Load(path, limit);
                var samples = pointCloud.SampledPointView;

                Assert.True(samples.Count <= limit, $"N={pointCount};count={samples.Count}");
                Assert.Equal(Math.Min(pointCount, limit), samples.Count);
                if (samples.Count > 1)
                {
                    Assert.Equal(0UL, samples[0].SourceCoordinate.PointIndex);
                    Assert.Equal((ulong)(pointCount - 1), samples[^1].SourceCoordinate.PointIndex);
                }

                for (var index = 1; index < samples.Count; index++)
                {
                    Assert.True(
                        samples[index - 1].SourceCoordinate.PointIndex < samples[index].SourceCoordinate.PointIndex,
                        $"N={pointCount};previous={samples[index - 1].SourceCoordinate.PointIndex};current={samples[index].SourceCoordinate.PointIndex}");
                }
            }
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void LargeSourceCoordinatesRemainPreciseAtTheDataBoundary()
    {
        var root = GetDirectory();
        var path = Path.Combine(root, "large-coordinate.las");

        try
        {
            WriteLas(path, 2, xStep: 0.001, yStep: 0.001, zStep: 0.001);
            var pointCloud = LazPointCloud.Load(path, 2);
            var first = pointCloud.SampledPointView[0];
            var second = pointCloud.SampledPointView[1];

            Assert.True(first.HasPreciseSourceCoordinate);
            Assert.True(second.HasPreciseSourceCoordinate);
            Assert.Equal(0UL, first.SourceCoordinate.PointIndex);
            Assert.Equal(1UL, second.SourceCoordinate.PointIndex);
            Assert.InRange(second.SourceCoordinate.X - first.SourceCoordinate.X, 0.001 - 1e-9, 0.001 + 1e-9);
            Assert.InRange(second.SourceCoordinate.Y - first.SourceCoordinate.Y, 0.001 - 1e-9, 0.001 + 1e-9);
            Assert.InRange(second.SourceCoordinate.Z - first.SourceCoordinate.Z, 0.001 - 1e-9, 0.001 + 1e-9);
            Assert.Equal(first.Position.X, second.Position.X);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void ReadOnlySampleViewKeepsLegacyPositionCompatibility()
    {
        var root = GetDirectory();
        var path = Path.Combine(root, "read-only-view.las");

        try
        {
            WriteLas(path, 3);
            var pointCloud = LazPointCloud.Load(path, 3);

            Assert.IsAssignableFrom<IReadOnlyList<LazPointCloudPoint>>(pointCloud.SampledPointView);
            Assert.Equal(pointCloud.SampledPointView.Count, pointCloud.SampledPoints.Length);
            Assert.Equal(pointCloud.SampledPointView[1], pointCloud.SampledPoints[1]);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static string GetDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD.Tests", "LAZ", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void WriteLas(
        string path,
        int pointCount,
        double xStep = 1.0,
        double yStep = 2.0,
        double zStep = 3.0)
    {
        const ushort headerSize = 227;
        const ushort recordLength = 34;
        const double scale = 0.001;
        const double xOffset = 1_000_000.0;
        const double yOffset = 2_000_000.0;
        const double zOffset = 3_000_000.0;
        var points = Enumerable.Range(0, pointCount)
            .Select(index => (
                X: xOffset + index * xStep,
                Y: yOffset + index * yStep,
                Z: zOffset + index * zStep))
            .ToArray();

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);
        writer.Write(Encoding.ASCII.GetBytes("LASF"));
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(new byte[16]);
        writer.Write((byte)1);
        writer.Write((byte)2);
        WriteFixedAscii(writer, "OpenVisionLab Tests", 32);
        WriteFixedAscii(writer, "OpenVisionLab", 32);
        writer.Write((ushort)1);
        writer.Write((ushort)2026);
        writer.Write(headerSize);
        writer.Write((uint)headerSize);
        writer.Write((uint)0);
        writer.Write((byte)3);
        writer.Write(recordLength);
        writer.Write((uint)pointCount);
        writer.Write((uint)pointCount);
        writer.Write(new byte[16]);
        writer.Write(scale);
        writer.Write(scale);
        writer.Write(scale);
        writer.Write(xOffset);
        writer.Write(yOffset);
        writer.Write(zOffset);
        writer.Write(points.Max(point => point.X));
        writer.Write(points.Min(point => point.X));
        writer.Write(points.Max(point => point.Y));
        writer.Write(points.Min(point => point.Y));
        writer.Write(points.Max(point => point.Z));
        writer.Write(points.Min(point => point.Z));

        Assert.Equal(headerSize, stream.Position);
        foreach (var (point, index) in points.Select((point, index) => (point, index)))
        {
            writer.Write(checked((int)Math.Round((point.X - xOffset) / scale)));
            writer.Write(checked((int)Math.Round((point.Y - yOffset) / scale)));
            writer.Write(checked((int)Math.Round((point.Z - zOffset) / scale)));
            writer.Write((ushort)(100 + index));
            writer.Write((byte)1);
            writer.Write((byte)1);
            writer.Write((sbyte)0);
            writer.Write((byte)0);
            writer.Write((ushort)index);
            writer.Write((double)index);
            writer.Write((ushort)(index + 1));
            writer.Write((ushort)(index + 2));
            writer.Write((ushort)(index + 3));
        }
    }

    private static void WriteFixedAscii(BinaryWriter writer, string value, int length)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        writer.Write(bytes.AsSpan(0, Math.Min(bytes.Length, length)));
        if (bytes.Length < length)
        {
            writer.Write(new byte[length - bytes.Length]);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}

#pragma warning restore CS0618
