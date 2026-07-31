using System.Text.Json.Serialization;

namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// WPF-neutral evidence describing whether one source owns a dense normal
/// channel and whether those normals are usable against its triangle winding.
/// Geometric face normals calculated for comparison never become source data.
/// </summary>
public sealed record SourceNormalQualityReport(
    string SchemaVersion,
    string SourceId,
    string Format,
    SourceNormalQualityState State,
    int PositionCount,
    int TriangleCount,
    int NormalCount,
    int FiniteNormalCount,
    int NonZeroNormalCount,
    int UnitLengthNormalCount,
    int InvalidIndexCount,
    int DegenerateTriangleCount,
    int ComparableCornerCount,
    int ConsistentCornerCount,
    int ReversedCornerCount,
    double UnitLengthTolerance,
    double MinimumAlignmentCosine,
    double? MinimumNormalLength,
    double? MaximumNormalLength,
    double? MeanNormalLength,
    double? MinimumAlignment,
    double? MeanAlignment,
    string Evidence)
{
    public const string CurrentSchemaVersion = "1.0";

    public bool IsDense =>
        PositionCount > 0
        && NormalCount == PositionCount;

    public bool IsUsable =>
        State == SourceNormalQualityState.Valid;
}

[JsonConverter(typeof(JsonStringEnumConverter<SourceNormalQualityState>))]
public enum SourceNormalQualityState
{
    Unavailable,
    Valid,
    Invalid
}
