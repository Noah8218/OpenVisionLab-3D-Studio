using System.Buffers.Binary;

namespace OpenVisionLab.ThreeD.Data;

public enum C3DSourceTopologyReason
{
    HeaderIncomplete,
    DimensionsNonPositive,
    CellCountOverflow,
    PayloadLengthMismatch
}

public sealed class C3DSourceTopologyException : IOException
{
    public C3DSourceTopologyException(C3DSourceTopologyReason reason)
        : base(MessageFor(reason))
    {
        Reason = reason;
    }

    public C3DSourceTopologyReason Reason { get; }

    public static string MessageFor(C3DSourceTopologyReason reason) => reason switch
    {
        C3DSourceTopologyReason.HeaderIncomplete =>
            "C3D topology error [HeaderIncomplete]: the 8-byte grid header is incomplete.",
        C3DSourceTopologyReason.DimensionsNonPositive =>
            "C3D topology error [DimensionsNonPositive]: grid width and height must be positive.",
        C3DSourceTopologyReason.CellCountOverflow =>
            "C3D topology error [CellCountOverflow]: declared grid dimensions exceed the supported cell-count or byte-length range.",
        C3DSourceTopologyReason.PayloadLengthMismatch =>
            "C3D topology error [PayloadLengthMismatch]: actual byte length does not match the declared grid dimensions.",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
    };
}

internal readonly record struct C3DSourceLayout(
    int Width,
    int Height,
    int SampleCount,
    long ExpectedByteLength);

internal static class C3DSourceTopology
{
    public static C3DSourceLayout ReadAndValidate(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead || !stream.CanSeek)
        {
            throw new ArgumentException(
                "C3D topology validation requires a readable seekable stream.",
                nameof(stream));
        }

        var actualByteLength = stream.Length;
        if (actualByteLength < 8)
        {
            throw new C3DSourceTopologyException(
                C3DSourceTopologyReason.HeaderIncomplete);
        }

        Span<byte> header = stackalloc byte[8];
        stream.Position = 0;
        stream.ReadExactly(header);
        var width = BinaryPrimitives.ReadInt32LittleEndian(header);
        var height = BinaryPrimitives.ReadInt32LittleEndian(header[4..]);
        if (width <= 0 || height <= 0)
        {
            throw new C3DSourceTopologyException(
                C3DSourceTopologyReason.DimensionsNonPositive);
        }

        var sampleCount = (long)width * height;
        long expectedByteLength;
        try
        {
            expectedByteLength = checked(8L + sampleCount * sizeof(float));
        }
        catch (OverflowException)
        {
            throw new C3DSourceTopologyException(
                C3DSourceTopologyReason.CellCountOverflow);
        }

        if (sampleCount > int.MaxValue)
        {
            throw new C3DSourceTopologyException(
                C3DSourceTopologyReason.CellCountOverflow);
        }

        if (actualByteLength != expectedByteLength)
        {
            throw new C3DSourceTopologyException(
                C3DSourceTopologyReason.PayloadLengthMismatch);
        }

        return new C3DSourceLayout(
            width,
            height,
            (int)sampleCount,
            expectedByteLength);
    }
}
