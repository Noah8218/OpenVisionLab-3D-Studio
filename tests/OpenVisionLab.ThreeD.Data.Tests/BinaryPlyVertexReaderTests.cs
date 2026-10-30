using System.Numerics;
using System.Text;
using OpenVisionLab.ThreeD.Data;
using Xunit;

namespace OpenVisionLab.ThreeD.Data.Tests;

public sealed class BinaryPlyVertexReaderTests
{
    [Fact]
    public void SingleVertexDoesNotAllocateAFullChunkForUnusedRows()
    {
        const int propertyCount = 256;
        var path = CreatePly(1, propertyCount);
        try
        {
            var allocationStart = GC.GetAllocatedBytesForCurrentThread();
            using var reader = new BinaryPlyVertexReader(path);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
            var unusedFullChunkBytes = 65_536L * propertyCount * sizeof(float);
            Assert.True(allocated < unusedFullChunkBytes, $"Reader allocated {allocated} bytes for one vertex; a full unused chunk occupies {unusedFullChunkBytes} bytes.");
            Assert.Equal(1, reader.ReadChunk());
            Assert.Equal(new Vector3(0, 1, 2), reader.GetPosition(0));
            Assert.Equal(255f, reader.GetSingle(0, propertyCount - 1));
            Assert.Equal(0, reader.ReadChunk());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FullChunkAndFinalVertexRetainOriginalCoordinates()
    {
        var path = CreatePly(65_537, 3);
        try
        {
            using var reader = new BinaryPlyVertexReader(path);
            Assert.Equal(65_536, reader.ReadChunk());
            Assert.Equal(new Vector3(0, 1, 2), reader.GetPosition(0));
            Assert.Equal(196605f, reader.GetSingle(65_535, 0));
            Assert.Equal(1, reader.ReadChunk());
            Assert.Equal(new Vector3(196608, 196609, 196610), reader.GetPosition(0));
            Assert.Equal(0, reader.ReadChunk());
            Assert.True(reader.IsComplete);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreatePly(int vertexCount, int propertyCount)
    {
        var directory = Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD.Tests", "Ply");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{Guid.NewGuid():N}.ply");
        using var stream = File.Create(path);
        var header = new StringBuilder($"ply\nformat binary_little_endian 1.0\nelement vertex {vertexCount}\n");
        for (var property = 0; property < propertyCount; property++)
        {
            var name = property < 3 ? new[] { "x", "y", "z" }[property] : $"attribute{property}";
            header.Append($"property float {name}\n");
        }
        header.Append("end_header\n");
        stream.Write(Encoding.ASCII.GetBytes(header.ToString()));
        using var writer = new BinaryWriter(stream);
        for (var vertex = 0; vertex < vertexCount; vertex++)
        {
            for (var property = 0; property < propertyCount; property++)
            {
                writer.Write((float)(vertex * propertyCount + property));
            }
        }
        return path;
    }
}