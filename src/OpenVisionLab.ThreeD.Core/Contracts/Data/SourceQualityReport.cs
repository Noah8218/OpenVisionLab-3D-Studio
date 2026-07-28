using System.Text.Json.Serialization;

namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// WPF-neutral, serializable evidence describing the quality and identity of
/// one loaded source. It reports only channels that the source actually owns.
/// </summary>
public sealed record SourceQualityReport(
    string SchemaVersion,
    SourceQualitySourceIdentity Source,
    SourceQualityGrid Grid,
    SourceQualityCoverage Coverage,
    SourceQualityHeightStatistics Height,
    SourceQualityCoordinateContext Coordinates,
    string Provenance,
    bool IsDerived,
    IReadOnlyList<SourceQualityChannelAvailability> Channels)
{
    public const string CurrentSchemaVersion = "1.0";
}

public sealed record SourceQualitySourceIdentity(
    string EntityId,
    string Format,
    string Path,
    long ByteLength,
    string ContentSha256,
    string RootSourceSha256);

public sealed record SourceQualityGrid(
    int Width,
    int Height,
    long CellCount);

public sealed record SourceQualityCoverage(
    long SampleCount,
    long ValidSampleCount,
    long MissingSampleCount,
    double ValidRatio,
    double MissingRatio,
    string MissingSamplePolicy,
    SourceQualityInvalidCellMaskIdentity InvalidCellMask);

public sealed record SourceQualityInvalidCellMaskIdentity(
    string ContractVersion,
    string Encoding,
    int ByteLength,
    string Sha256);

public sealed record SourceQualityHeightStatistics(
    string ScalarMeaning,
    double? Minimum,
    double? Maximum,
    double? Mean,
    SourceQualityDistribution? Distribution);

public sealed record SourceQualityDistribution(
    int BinCount,
    int PeakBinIndex,
    IReadOnlyList<long> Bins);

public sealed record SourceQualityCoordinateContext(
    string Unit,
    string FrameId,
    string CoordinateConvention);

[JsonConverter(typeof(JsonStringEnumConverter<SourceQualityChannel>))]
public enum SourceQualityChannel
{
    Height,
    Intensity,
    Color,
    Depth,
    Normal,
    Confidence,
    SignalToNoiseRatio
}

[JsonConverter(typeof(JsonStringEnumConverter<SourceQualityChannelState>))]
public enum SourceQualityChannelState
{
    Available,
    Unavailable
}

public sealed record SourceQualityChannelAvailability(
    SourceQualityChannel Channel,
    SourceQualityChannelState State,
    string Evidence);
