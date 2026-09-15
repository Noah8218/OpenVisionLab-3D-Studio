using System.Security.Cryptography;
using OpenVisionLab.Vision3D.FeatureExtraction;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Immutable full-resolution C3D raw-height data plus the identity of the
/// exact bytes that were parsed. Zero and non-finite samples are missing.
/// </summary>
public sealed class C3DHeightFieldSnapshot
{
    private readonly double[] values;

    private C3DHeightFieldSnapshot(
        string entityId,
        string sourcePath,
        string unit,
        string frameId,
        long byteLength,
        string contentSha256,
        string rootSourceSha256,
        int width,
        int height,
        int gridOriginColumn,
        int gridOriginRow,
        double[] values,
        string provenance,
        bool isDerived,
        CancellationToken cancellationToken)
    {
        EntityId = entityId;
        SourcePath = sourcePath;
        Unit = unit;
        FrameId = frameId;
        ByteLength = byteLength;
        ContentSha256 = contentSha256;
        RootSourceSha256 = rootSourceSha256;
        Width = width;
        Height = height;
        GridOriginColumn = gridOriginColumn;
        GridOriginRow = gridOriginRow;
        this.values = values;
        Provenance = provenance;
        IsDerived = isDerived;

        cancellationToken.ThrowIfCancellationRequested();
        var summary = new HeightDistributionStatisticsTool().Execute(
            values,
            new HeightDistributionStatisticsOptions
            {
                BinCount = 1,
                ZeroIsMissing = false
            });
        cancellationToken.ThrowIfCancellationRequested();
        if (!summary.Success)
        {
            throw new InvalidDataException(summary.Message);
        }
        ValidCount = summary.ValidSampleCount;
        MissingCount = summary.MissingSampleCount;
        Minimum = summary.Minimum;
        Maximum = summary.Maximum;
        Mean = summary.Mean;
    }

    public string EntityId { get; }
    public string SourcePath { get; }
    public string Unit { get; }
    public string FrameId { get; }
    public long ByteLength { get; }
    public string ContentSha256 { get; }
    public string RootSourceSha256 { get; }
    public int Width { get; }
    public int Height { get; }
    public int GridOriginColumn { get; }
    public int GridOriginRow { get; }
    public ReadOnlyMemory<double> Values => values;
    internal IReadOnlyList<double> ValueList => values;
    public int ValidCount { get; }
    public int MissingCount { get; }
    public double Minimum { get; }
    public double Maximum { get; }
    public double Mean { get; }
    public string ScalarMeaning => "raw-height";
    public string Provenance { get; }
    public bool IsDerived { get; }

    public static C3DHeightFieldSnapshot LoadIdentified(
        string path,
        string entityId,
        string unit,
        string frameId)
        => LoadIdentified(
            path,
            entityId,
            unit,
            frameId,
            CancellationToken.None);

    public static C3DHeightFieldSnapshot LoadIdentified(
        string path,
        string entityId,
        string unit,
        string frameId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = Path.GetFullPath(path);
        var (byteLength, hash, width, height, values) = C3DHeightFieldBinaryCodec.ParseAndHash(fullPath, cancellationToken);
        return new C3DHeightFieldSnapshot(
            entityId,
            fullPath,
            unit,
            frameId,
            byteLength,
            hash,
            hash,
            width,
            height,
            0,
            0,
            values,
            $"source:{hash}",
            false,
            cancellationToken);
    }

    public static C3DHeightFieldSnapshot LoadVerified(
        string path,
        string entityId,
        string unit,
        string frameId,
        long expectedByteLength,
        string expectedContentSha256,
        int expectedWidth,
        int expectedHeight)
        => LoadVerified(
            path,
            entityId,
            unit,
            frameId,
            expectedByteLength,
            expectedContentSha256,
            expectedWidth,
            expectedHeight,
            CancellationToken.None);

    public static C3DHeightFieldSnapshot LoadVerified(
        string path,
        string entityId,
        string unit,
        string frameId,
        long expectedByteLength,
        string expectedContentSha256,
        int expectedWidth,
        int expectedHeight,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = Path.GetFullPath(path);
        var (byteLength, hash, width, height, values) = C3DHeightFieldBinaryCodec.ParseAndHash(fullPath, cancellationToken);
        if (byteLength != expectedByteLength
            || !string.Equals(hash, expectedContentSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("C3D source byte identity does not match the teaching recipe.");
        }

        if (width != expectedWidth || height != expectedHeight)
        {
            throw new InvalidDataException("C3D source grid identity does not match the teaching recipe.");
        }

        return new C3DHeightFieldSnapshot(
            entityId,
            fullPath,
            unit,
            frameId,
            byteLength,
            hash,
            hash,
            width,
            height,
            0,
            0,
            values,
            $"source:{hash}",
            false,
            cancellationToken);
    }

    /// <summary>
    /// Reopens canonical C3D bytes as a derived HeightField only when an
    /// external recipe/sidecar contract supplies and verifies the derived
    /// identity that the binary format intentionally does not contain.
    /// </summary>
    public static C3DHeightFieldSnapshot LoadDerivedVerified(
        string path,
        string entityId,
        string unit,
        string frameId,
        long expectedByteLength,
        string expectedContentSha256,
        string expectedRootSourceSha256,
        int expectedWidth,
        int expectedHeight,
        int expectedGridOriginColumn,
        int expectedGridOriginRow,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedContentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedRootSourceSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (expectedByteLength <= 0 || expectedWidth <= 0 || expectedHeight <= 0)
        {
            throw new InvalidDataException("Derived C3D sidecar dimensions and byte length must be positive.");
        }

        var fullPath = Path.GetFullPath(path);
        var (byteLength, hash, width, height, values) = C3DHeightFieldBinaryCodec.ParseAndHash(fullPath, CancellationToken.None);
        if (byteLength != expectedByteLength
            || !string.Equals(hash, expectedContentSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Derived C3D bytes do not match the recipe sidecar identity.");
        }

        if (width != expectedWidth || height != expectedHeight)
        {
            throw new InvalidDataException("Derived C3D grid identity does not match the recipe sidecar.");
        }

        return new C3DHeightFieldSnapshot(
            entityId,
            fullPath,
            unit,
            frameId,
            byteLength,
            hash,
            expectedRootSourceSha256,
            width,
            height,
            expectedGridOriginColumn,
            expectedGridOriginRow,
            values,
            provenance,
            true,
            CancellationToken.None);
    }

    public static C3DHeightFieldSnapshot CreateForVerification(
        string entityId,
        int width,
        int height,
        IReadOnlyList<double> sourceValues,
        string unit = "raw-height",
        string frameId = "frame.c3d-grid-index")
    {
        if (width <= 0 || height <= 0 || sourceValues.Count != checked(width * height))
        {
            throw new ArgumentException("Verification height field dimensions do not match its values.");
        }

        var values = sourceValues
            .Select(value => double.IsFinite(value) && value != 0.0 ? value : double.NaN)
            .ToArray();
        var bytes = C3DHeightFieldBinaryCodec.Encode(width, height, values);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        return new C3DHeightFieldSnapshot(
            entityId,
            string.Empty,
            unit,
            frameId,
            bytes.LongLength,
            hash,
            hash,
            width,
            height,
            0,
            0,
            values,
            $"verification:{hash}",
            false,
            CancellationToken.None);
    }

    public C3DHeightFieldSnapshot CreateDerived(string outputEntityId, IReadOnlyList<double> outputValues, string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputEntityId);
        if (outputValues.Count != values.Length)
        {
            throw new ArgumentException("Derived height field dimensions must match the source.", nameof(outputValues));
        }

        var copy = outputValues.ToArray();
        if (copy.Any(value => double.IsFinite(value) && value == 0.0))
        {
            throw new InvalidDataException(
                "Derived C3D contains a finite zero that the C3D format reserves for missing data; preserving the missing mask requires a controlled error.");
        }
        var normalized = C3DHeightFieldBinaryCodec.NormalizeDerivedValues(copy);
        var bytes = C3DHeightFieldBinaryCodec.Encode(Width, Height, normalized);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        return new C3DHeightFieldSnapshot(
            outputEntityId,
            string.Empty,
            Unit,
            FrameId,
            bytes.LongLength,
            hash,
            RootSourceSha256,
            Width,
            Height,
            GridOriginColumn,
            GridOriginRow,
            normalized,
            provenance,
            true,
            CancellationToken.None);
    }

    public C3DHeightFieldSnapshot CreateCrop(
        string outputEntityId,
        HeightMapCropResult crop,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputEntityId);
        ArgumentNullException.ThrowIfNull(crop);
        if (!crop.Success || crop.Output is null)
        {
            throw new InvalidDataException(crop.Message);
        }

        var output = crop.Output;
        var expectedOriginColumn = checked(GridOriginColumn + crop.SourceRoi.Column);
        var expectedOriginRow = checked(GridOriginRow + crop.SourceRoi.Row);
        if (output.Columns != crop.SourceRoi.ColumnCount
            || output.Rows != crop.SourceRoi.RowCount
            || output.OriginX != expectedOriginColumn
            || output.OriginY != expectedOriginRow
            || output.ColumnPitch != 1d
            || output.RowPitch != 1d
            || !string.Equals(output.PlanarUnit, "grid-index", StringComparison.Ordinal)
            || !string.Equals(output.HeightUnit, Unit, StringComparison.Ordinal)
            || !string.Equals(output.FrameId, FrameId, StringComparison.Ordinal)
            || !string.Equals(output.SourceId, EntityId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The SDK crop output does not preserve the expected source-grid frame, unit, pitch, or identity.");
        }

        var copy = output.CopyValues();
        if (copy.Any(value => double.IsFinite(value) && value == 0.0))
        {
            throw new InvalidDataException(
                "Cropped C3D contains a finite zero that the C3D format reserves for missing data.");
        }

        var normalized = C3DHeightFieldBinaryCodec.NormalizeDerivedValues(copy);
        var bytes = C3DHeightFieldBinaryCodec.Encode(output.Columns, output.Rows, normalized);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        return new C3DHeightFieldSnapshot(
            outputEntityId,
            string.Empty,
            Unit,
            FrameId,
            bytes.LongLength,
            hash,
            RootSourceSha256,
            output.Columns,
            output.Rows,
            expectedOriginColumn,
            expectedOriginRow,
            normalized,
            provenance,
            true,
            CancellationToken.None);
    }

    public void SaveC3D(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory);
        File.WriteAllBytes(fullPath, C3DHeightFieldBinaryCodec.Encode(Width, Height, values));
    }
}
