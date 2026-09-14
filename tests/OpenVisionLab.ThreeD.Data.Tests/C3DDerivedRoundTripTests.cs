using System.Buffers.Binary;
using System.Security.Cryptography;
using OpenVisionLab.ThreeD.Data;
using Xunit;

namespace OpenVisionLab.ThreeD.Data.Tests;

public sealed class C3DDerivedRoundTripTests
{
    [Fact]
    public void LegacyFloatEncodingCollisionIsReproducible()
    {
        var first = 1.00000001d;
        var second = 1.00000002d;

        Assert.NotEqual(
            BitConverter.DoubleToInt64Bits(first),
            BitConverter.DoubleToInt64Bits(second));
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(EncodeLegacy(first))),
            Convert.ToHexString(SHA256.HashData(EncodeLegacy(second))));
    }

    [Fact]
    public void DerivedRoundTripPreservesCanonicalValuesIdentityAndMissingMask()
    {
        var source = C3DHeightFieldSnapshot.CreateForVerification(
            "source.round-trip",
            2,
            2,
            [1d, 2d, 3d, 4d]);
        var output = source.CreateDerived(
            "derived.round-trip",
            [1.00000001d, double.NaN, -2.5d, 12345.125d],
            "test:derived-round-trip");
        var path = GetPath("derived-round-trip.C3D");

        try
        {
            output.SaveC3D(path);
            var reloaded = C3DHeightFieldSnapshot.LoadVerified(
                path,
                output.EntityId,
                output.Unit,
                output.FrameId,
                output.ByteLength,
                output.ContentSha256,
                output.Width,
                output.Height,
                TestContext.Current.CancellationToken);

            Assert.Equal(output.ContentSha256, reloaded.ContentSha256);
            Assert.Equal(output.ByteLength, reloaded.ByteLength);
            Assert.Equal(output.ValidCount, reloaded.ValidCount);
            Assert.Equal(output.MissingCount, reloaded.MissingCount);
            AssertDoubleBits(output.Values.Span, reloaded.Values.Span);
            Assert.True(double.IsNaN(reloaded.Values.Span[1]));
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public void DerivedCollisionConvergesToOneCanonicalLiveValue()
    {
        var source = C3DHeightFieldSnapshot.CreateForVerification(
            "source.collision",
            1,
            1,
            [1d]);
        var first = source.CreateDerived("derived.first", [1.00000001d], "test:first");
        var second = source.CreateDerived("derived.second", [1.00000002d], "test:second");

        Assert.Equal(first.ContentSha256, second.ContentSha256);
        Assert.Equal(
            BitConverter.DoubleToInt64Bits(first.Values.Span[0]),
            BitConverter.DoubleToInt64Bits(second.Values.Span[0]));
    }

    [Fact]
    public void DerivedRejectsValuesThatCannotBeRepresentedByC3D()
    {
        var source = C3DHeightFieldSnapshot.CreateForVerification(
            "source.invalid",
            1,
            1,
            [1d]);

        foreach (var invalid in new[]
        {
            0d,
            double.Epsilon,
            double.MaxValue,
            double.PositiveInfinity,
            double.NegativeInfinity
        })
        {
            Assert.Throws<InvalidDataException>(() =>
                source.CreateDerived("derived.invalid", [invalid], "test:invalid"));
        }
    }

    private static byte[] EncodeLegacy(double value)
    {
        var bytes = new byte[sizeof(float)];
        BinaryPrimitives.WriteInt32LittleEndian(
            bytes,
            BitConverter.SingleToInt32Bits((float)value));
        return bytes;
    }

    private static void AssertDoubleBits(ReadOnlySpan<double> expected, ReadOnlySpan<double> actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var index = 0; index < expected.Length; index++)
        {
            if (double.IsNaN(expected[index]) || double.IsNaN(actual[index]))
            {
                Assert.True(double.IsNaN(expected[index]) && double.IsNaN(actual[index]), $"index={index}");
                continue;
            }

            Assert.Equal(
                BitConverter.DoubleToInt64Bits(expected[index]),
                BitConverter.DoubleToInt64Bits(actual[index]));
        }
    }

    private static string GetPath(string fileName)
    {
        var directory = Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD.Tests", "C3D");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{Guid.NewGuid():N}-{fileName}");
    }

    private static void TryDelete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
