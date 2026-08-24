using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// One deterministic prepared-scene sample with an exact locator in the
/// validated finite-point collection.
/// </summary>
public sealed record PreparedSceneSample(
    int Order,
    int SourcePointIndex,
    SurfaceModelPoint3 Position);

/// <summary>
/// Versioned scene preparation parameters. Version 1 preserves every finite
/// input point and selects a deterministic even-index sample subset.
/// </summary>
public sealed record PreparedScenePreparationParameters(
    string SamplingPolicy,
    int MaximumSampleCount)
{
    public const string DeterministicEvenPointSampling =
        "deterministic-even-point-index-v1";
}

/// <summary>
/// Identified, content-addressed measured scene tied to the complete
/// SourceQualityReport used to admit its finite points.
/// </summary>
public sealed record PreparedSceneArtifact(
    string SchemaVersion,
    string ArtifactId,
    string Name,
    string Unit,
    string FrameId,
    string CoordinateConvention,
    SourceQualityReport SourceQuality,
    string SourceQualitySha256,
    PreparedScenePreparationParameters Preparation,
    SurfaceModelPoint3[] Points,
    PreparedSceneSample[] Samples,
    string ContentSha256)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string CurrentCoordinateConvention = "source-cartesian-xyz";

    public static PreparedSceneArtifact Create(
        string artifactId,
        string name,
        string coordinateConvention,
        SourceQualityReport sourceQuality,
        PreparedScenePreparationParameters preparation,
        IReadOnlyList<SurfaceModelPoint3> points,
        IReadOnlyList<PreparedSceneSample> samples)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(coordinateConvention);
        ArgumentNullException.ThrowIfNull(sourceQuality);
        ArgumentNullException.ThrowIfNull(sourceQuality.Coordinates);
        ArgumentNullException.ThrowIfNull(preparation);
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(samples);

        var artifact = new PreparedSceneArtifact(
            CurrentSchemaVersion,
            artifactId.Trim(),
            name.Trim(),
            sourceQuality.Coordinates.Unit?.Trim() ?? string.Empty,
            sourceQuality.Coordinates.FrameId?.Trim() ?? string.Empty,
            coordinateConvention.Trim(),
            sourceQuality,
            SourceQualityReportContentIdentity.CalculateSha256(sourceQuality),
            preparation,
            points.ToArray(),
            samples.ToArray(),
            string.Empty);
        artifact = artifact with
        {
            ContentSha256 = CalculateContentSha256(artifact)
        };

        var validity = PreparedSceneArtifactValidator.Inspect(artifact);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                $"Prepared Scene is invalid: {string.Join(" ", validity.Errors)}");
        }

        return artifact;
    }

    public static string CalculateContentSha256(
        PreparedSceneArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(artifact.SourceQuality);
        ArgumentNullException.ThrowIfNull(artifact.Preparation);
        ArgumentNullException.ThrowIfNull(artifact.Points);
        ArgumentNullException.ThrowIfNull(artifact.Samples);

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(
                   stream,
                   Encoding.UTF8,
                   leaveOpen: true))
        {
            writer.Write("OpenVisionLab.PreparedScene");
            writer.Write(artifact.SchemaVersion ?? string.Empty);
            writer.Write(artifact.ArtifactId ?? string.Empty);
            writer.Write(artifact.Name ?? string.Empty);
            writer.Write(artifact.Unit ?? string.Empty);
            writer.Write(artifact.FrameId ?? string.Empty);
            writer.Write(artifact.CoordinateConvention ?? string.Empty);
            writer.Write(
                (artifact.SourceQualitySha256 ?? string.Empty)
                .ToUpperInvariant());
            writer.Write(
                SourceQualityReportContentIdentity.CalculateSha256(
                    artifact.SourceQuality));
            writer.Write(artifact.Preparation.SamplingPolicy ?? string.Empty);
            writer.Write(artifact.Preparation.MaximumSampleCount);

            writer.Write(artifact.Points.Length);
            foreach (var point in artifact.Points)
            {
                WritePoint(writer, point);
            }

            writer.Write(artifact.Samples.Length);
            foreach (var sample in artifact.Samples)
            {
                ArgumentNullException.ThrowIfNull(sample);
                writer.Write(sample.Order);
                writer.Write(sample.SourcePointIndex);
                WritePoint(writer, sample.Position);
            }
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WritePoint(
        BinaryWriter writer,
        SurfaceModelPoint3 point)
    {
        ArgumentNullException.ThrowIfNull(point);
        writer.Write(point.X);
        writer.Write(point.Y);
        writer.Write(point.Z);
    }
}

/// <summary>
/// Canonical semantic identity for SourceQualityReport. Channel order is not
/// semantic; every channel entry is written in enum order.
/// </summary>
public static class SourceQualityReportContentIdentity
{
    public static string CalculateSha256(SourceQualityReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(report.Source);
        ArgumentNullException.ThrowIfNull(report.Grid);
        ArgumentNullException.ThrowIfNull(report.Coverage);
        ArgumentNullException.ThrowIfNull(report.Coverage.InvalidCellMask);
        ArgumentNullException.ThrowIfNull(report.Height);
        ArgumentNullException.ThrowIfNull(report.Coordinates);
        ArgumentNullException.ThrowIfNull(report.Channels);
        if (!report.TryValidateGridDiagnostics(out var validationMessage))
        {
            throw new InvalidDataException(validationMessage);
        }

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(
                   stream,
                   Encoding.UTF8,
                   leaveOpen: true))
        {
            writer.Write("OpenVisionLab.SourceQualityReport");
            writer.Write(report.SchemaVersion ?? string.Empty);
            writer.Write(report.Source.EntityId ?? string.Empty);
            writer.Write(report.Source.Format ?? string.Empty);
            writer.Write(report.Source.Path ?? string.Empty);
            writer.Write(report.Source.ByteLength);
            writer.Write(
                (report.Source.ContentSha256 ?? string.Empty)
                .ToUpperInvariant());
            writer.Write(
                (report.Source.RootSourceSha256 ?? string.Empty)
                .ToUpperInvariant());
            writer.Write(report.Grid.Width);
            writer.Write(report.Grid.Height);
            writer.Write(report.Grid.CellCount);
            writer.Write(report.Coverage.SampleCount);
            writer.Write(report.Coverage.ValidSampleCount);
            writer.Write(report.Coverage.MissingSampleCount);
            writer.Write(report.Coverage.ValidRatio);
            writer.Write(report.Coverage.MissingRatio);
            writer.Write(report.Coverage.MissingSamplePolicy ?? string.Empty);
            writer.Write(
                report.Coverage.InvalidCellMask.ContractVersion
                ?? string.Empty);
            writer.Write(
                report.Coverage.InvalidCellMask.Encoding
                ?? string.Empty);
            writer.Write(report.Coverage.InvalidCellMask.ByteLength);
            writer.Write(
                (report.Coverage.InvalidCellMask.Sha256 ?? string.Empty)
                .ToUpperInvariant());
            writer.Write(report.Height.ScalarMeaning ?? string.Empty);
            WriteNullable(writer, report.Height.Minimum);
            WriteNullable(writer, report.Height.Maximum);
            WriteNullable(writer, report.Height.Mean);
            writer.Write(report.Height.Distribution is not null);
            if (report.Height.Distribution is not null)
            {
                writer.Write(report.Height.Distribution.BinCount);
                writer.Write(report.Height.Distribution.PeakBinIndex);
                writer.Write(report.Height.Distribution.Bins.Count);
                foreach (var count in report.Height.Distribution.Bins)
                {
                    writer.Write(count);
                }
            }

            writer.Write(report.Coordinates.Unit ?? string.Empty);
            writer.Write(report.Coordinates.FrameId ?? string.Empty);
            writer.Write(
                report.Coordinates.CoordinateConvention
                ?? string.Empty);
            writer.Write(report.Provenance ?? string.Empty);
            writer.Write(report.IsDerived);

            var channels = report.Channels
                .OrderBy(channel => channel.Channel)
                .ToArray();
            writer.Write(channels.Length);
            foreach (var channel in channels)
            {
                ArgumentNullException.ThrowIfNull(channel);
                writer.Write((int)channel.Channel);
                writer.Write((int)channel.State);
                writer.Write(channel.Evidence ?? string.Empty);
            }

            if (report.SchemaVersion == SourceQualityReport.CurrentSchemaVersion)
            {
                var diagnostics = report.GridDiagnostics!;
                writer.Write(diagnostics.SchemaVersion ?? string.Empty);
                writer.Write((int)diagnostics.State);
                writer.Write(diagnostics.DeclaredCellCount);
                writer.Write(diagnostics.ObservedSampleCount);
                writer.Write(diagnostics.UniqueLocatorCount);
                writer.Write(diagnostics.Checks.Count);
                foreach (var check in diagnostics.Checks)
                {
                    writer.Write((int)check.Code);
                    writer.Write((int)check.State);
                    writer.Write(check.AffectedCount);
                    WriteNullable(writer, check.FirstSampleOrdinal);
                    WriteNullable(writer, check.FirstRow);
                    WriteNullable(writer, check.FirstColumn);
                    writer.Write(check.FirstComponent ?? string.Empty);
                    writer.Write(check.Message ?? string.Empty);
                }
            }
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteNullable(
        BinaryWriter writer,
        double? value)
    {
        writer.Write(value.HasValue);
        if (value.HasValue)
        {
            writer.Write(value.Value);
        }
    }

    private static void WriteNullable(
        BinaryWriter writer,
        long? value)
    {
        writer.Write(value.HasValue);
        if (value.HasValue)
        {
            writer.Write(value.Value);
        }
    }

    private static void WriteNullable(
        BinaryWriter writer,
        int? value)
    {
        writer.Write(value.HasValue);
        if (value.HasValue)
        {
            writer.Write(value.Value);
        }
    }
}

public static class PreparedSceneSampling
{
    public static int GetEvenPointIndex(
        int sampleOrder,
        int sampleCount,
        int pointCount)
    {
        if (sampleCount <= 0
            || sampleCount > pointCount
            || sampleOrder < 0
            || sampleOrder >= sampleCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sampleOrder),
                "Prepared Scene sample order/count must select a unique source point.");
        }

        return checked((int)(
            ((long)sampleOrder * 2L + 1L)
            * pointCount
            / (sampleCount * 2L)));
    }
}
