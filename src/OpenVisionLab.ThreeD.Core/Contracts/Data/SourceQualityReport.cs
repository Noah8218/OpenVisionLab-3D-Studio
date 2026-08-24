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
    public const string LegacySchemaVersion = "1.0";
    public const string CurrentSchemaVersion = "1.1";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SourceQualityGridDiagnostics? GridDiagnostics { get; init; }

    public static bool IsSupportedSchemaVersion(string? schemaVersion) =>
        schemaVersion is LegacySchemaVersion or CurrentSchemaVersion;

    public bool TryValidateGridDiagnostics(out string validationMessage)
    {
        if (SchemaVersion == LegacySchemaVersion)
        {
            var valid = GridDiagnostics is null;
            validationMessage = valid
                ? "Legacy Source Quality does not contain grid diagnostics."
                : "Legacy Source Quality schema 1.0 must not contain grid diagnostics.";
            return valid;
        }

        if (SchemaVersion != CurrentSchemaVersion)
        {
            validationMessage = $"Unsupported Source Quality schema '{SchemaVersion}'.";
            return false;
        }

        if (GridDiagnostics is null)
        {
            validationMessage = "Source Quality schema 1.1 requires grid diagnostics.";
            return false;
        }

        if (!GridDiagnostics.TryValidate(out validationMessage))
        {
            return false;
        }

        if (Grid is null
            || Coverage is null
            || GridDiagnostics.DeclaredCellCount != Grid.CellCount
            || GridDiagnostics.ObservedSampleCount != Coverage.SampleCount)
        {
            validationMessage = "Grid diagnostics do not match Source Quality grid and coverage counts.";
            return false;
        }

        var topologyState = GridDiagnostics.Checks[0].State;
        foreach (var check in GridDiagnostics.Checks)
        {
            if (!check.FirstRow.HasValue)
            {
                continue;
            }

            var locatorIsInGrid = check.FirstRow.Value >= 0
                && check.FirstRow.Value < Grid.Height
                && check.FirstColumn!.Value >= 0
                && check.FirstColumn.Value < Grid.Width;
            if (!locatorIsInGrid
                && topologyState != SourceQualityGridDiagnosticState.Error)
            {
                validationMessage = "Out-of-grid diagnostic locators require a topology error.";
                return false;
            }
        }

        return true;
    }
}

public sealed record SourceQualityGridDiagnostics(
    string SchemaVersion,
    SourceQualityGridDiagnosticState State,
    long DeclaredCellCount,
    long ObservedSampleCount,
    long UniqueLocatorCount,
    IReadOnlyList<SourceQualityGridDiagnosticCheck> Checks)
{
    public const string CurrentSchemaVersion = "1.0";

    private static readonly SourceQualityGridDiagnosticCode[] RequiredCheckOrder =
    [
        SourceQualityGridDiagnosticCode.Topology,
        SourceQualityGridDiagnosticCode.LocatorMonotonicity,
        SourceQualityGridDiagnosticCode.DuplicateLocator,
        SourceQualityGridDiagnosticCode.CoordinateFiniteness
    ];

    public bool TryValidate(out string validationMessage)
    {
        if (SchemaVersion != CurrentSchemaVersion
            || !Enum.IsDefined(State)
            || DeclaredCellCount < 0
            || ObservedSampleCount < 0
            || UniqueLocatorCount < 0
            || UniqueLocatorCount > ObservedSampleCount
            || Checks is null
            || Checks.Count != RequiredCheckOrder.Length)
        {
            validationMessage = "Grid diagnostics metadata is invalid.";
            return false;
        }

        for (var index = 0; index < RequiredCheckOrder.Length; index++)
        {
            var check = Checks[index];
            if (check is null
                || check.Code != RequiredCheckOrder[index]
                || !Enum.IsDefined(check.State)
                || check.AffectedCount < 0
                || string.IsNullOrWhiteSpace(check.Message))
            {
                validationMessage = "Grid diagnostic checks are missing, out of order, or invalid.";
                return false;
            }

            var hasLocation = check.FirstSampleOrdinal.HasValue
                || check.FirstRow.HasValue
                || check.FirstColumn.HasValue
                || !string.IsNullOrEmpty(check.FirstComponent);
            if (check.State == SourceQualityGridDiagnosticState.Pass)
            {
                if (check.AffectedCount != 0 || hasLocation)
                {
                    validationMessage = "Passing grid diagnostic checks cannot contain affected samples.";
                    return false;
                }
            }
            else if (check.AffectedCount == 0)
            {
                validationMessage = "Failing grid diagnostic checks must contain an affected count.";
                return false;
            }

            else if (check.Code != SourceQualityGridDiagnosticCode.Topology
                     && (!check.FirstSampleOrdinal.HasValue
                         || !check.FirstRow.HasValue
                         || !check.FirstColumn.HasValue))
            {
                validationMessage = "Failing locator and coordinate checks require the first affected sample location.";
                return false;
            }

            if (check.FirstSampleOrdinal is < 0
                || check.FirstSampleOrdinal >= ObservedSampleCount
                || check.FirstRow.HasValue != check.FirstColumn.HasValue
                || check.FirstRow.HasValue != check.FirstSampleOrdinal.HasValue
                || !string.IsNullOrEmpty(check.FirstComponent)
                    != check.FirstSampleOrdinal.HasValue)
            {
                validationMessage = "Grid diagnostic sample locations are incomplete or outside the observed sample range.";
                return false;
            }

            if (check.State == SourceQualityGridDiagnosticState.Error
                && (check.Code == SourceQualityGridDiagnosticCode.CoordinateFiniteness
                    ? check.FirstComponent is not ("X" or "Y" or "Z")
                    : check.FirstSampleOrdinal.HasValue
                        && check.FirstComponent != "Locator"))
            {
                validationMessage = "Grid diagnostic sample components do not match the check type.";
                return false;
            }
        }

        var topology = Checks[0];
        var duplicate = Checks[2];
        var exactTopologyCounts = DeclaredCellCount == ObservedSampleCount
            && ObservedSampleCount == UniqueLocatorCount;
        if (topology.State == SourceQualityGridDiagnosticState.Pass
            && !exactTopologyCounts)
        {
            validationMessage = "Passing topology diagnostics require exact declared, observed, and unique counts.";
            return false;
        }

        var hasDuplicateLocators = UniqueLocatorCount != ObservedSampleCount;
        if ((duplicate.State == SourceQualityGridDiagnosticState.Error)
                != hasDuplicateLocators
            || duplicate.State == SourceQualityGridDiagnosticState.Error
                && duplicate.AffectedCount
                    != ObservedSampleCount - UniqueLocatorCount)
        {
            validationMessage = "Duplicate-locator diagnostics contradict the observed and unique locator counts.";
            return false;
        }

        var expectedState = Checks.Any(check =>
            check.State == SourceQualityGridDiagnosticState.Error)
            ? SourceQualityGridDiagnosticState.Error
            : SourceQualityGridDiagnosticState.Pass;
        if (State != expectedState)
        {
            validationMessage = "Grid diagnostics aggregate state does not match its checks.";
            return false;
        }

        validationMessage = $"Grid diagnostics contain {Checks.Count} deterministic checks.";
        return true;
    }
}

[JsonConverter(typeof(JsonStringEnumConverter<SourceQualityGridDiagnosticCode>))]
public enum SourceQualityGridDiagnosticCode
{
    Topology,
    LocatorMonotonicity,
    DuplicateLocator,
    CoordinateFiniteness
}

[JsonConverter(typeof(JsonStringEnumConverter<SourceQualityGridDiagnosticState>))]
public enum SourceQualityGridDiagnosticState
{
    Pass,
    Error
}

public sealed record SourceQualityGridDiagnosticCheck(
    SourceQualityGridDiagnosticCode Code,
    SourceQualityGridDiagnosticState State,
    long AffectedCount,
    long? FirstSampleOrdinal,
    int? FirstRow,
    int? FirstColumn,
    string? FirstComponent,
    string Message);

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
