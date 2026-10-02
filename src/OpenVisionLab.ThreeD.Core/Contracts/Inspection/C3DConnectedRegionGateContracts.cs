using System.Globalization;
using System.Security.Cryptography;

namespace OpenVisionLab.ThreeD.Core;

public enum C3DConnectedRegionGateMode
{
    Presence,
    FillHeight
}

public enum C3DConnectedRegionGateDecision
{
    NotEvaluated,
    Accepted,
    Rejected,
    Missing
}

/// <summary>
/// Explicit G-13/G-14 policy for one already detected connected region.
/// The reference surface is caller-authored metadata; this contract never
/// fits or calibrates it.
/// </summary>
public sealed record C3DConnectedRegionGatePolicy(
    C3DConnectedRegionGateMode Mode,
    int SelectedRegionIndex,
    double MinimumFiniteCoverageRatio,
    double MinimumMeanHeight,
    double MaximumMeanHeight,
    double ReferenceSlopeX,
    double ReferenceSlopeZ,
    double ReferenceIntercept,
    string HeightUnit)
{
    public const string GateModeParameterName = "GateMode";
    public const string SelectedRegionIndexParameterName = "SelectedRegionIndex";
    public const string MinimumFiniteCoverageRatioParameterName = "MinimumFiniteCoverageRatio";
    public const string MinimumMeanHeightParameterName = "MinimumMeanHeight";
    public const string MaximumMeanHeightParameterName = "MaximumMeanHeight";
    public const string ReferenceSlopeXParameterName = "ReferenceSlopeX";
    public const string ReferenceSlopeZParameterName = "ReferenceSlopeZ";
    public const string ReferenceInterceptParameterName = "ReferenceIntercept";

    public static readonly IReadOnlyList<string> ParameterNames =
    [
        GateModeParameterName,
        SelectedRegionIndexParameterName,
        MinimumFiniteCoverageRatioParameterName,
        MinimumMeanHeightParameterName,
        MaximumMeanHeightParameterName,
        ReferenceSlopeXParameterName,
        ReferenceSlopeZParameterName,
        ReferenceInterceptParameterName
    ];

    public IReadOnlyList<ToolRecipeParameter> ToRecipeParameters() =>
    [
        new(GateModeParameterName, Mode.ToString()),
        new(SelectedRegionIndexParameterName, SelectedRegionIndex.ToString(CultureInfo.InvariantCulture)),
        new(MinimumFiniteCoverageRatioParameterName, MinimumFiniteCoverageRatio.ToString("G17", CultureInfo.InvariantCulture)),
        new(MinimumMeanHeightParameterName, MinimumMeanHeight.ToString("G17", CultureInfo.InvariantCulture)),
        new(MaximumMeanHeightParameterName, MaximumMeanHeight.ToString("G17", CultureInfo.InvariantCulture)),
        new(ReferenceSlopeXParameterName, ReferenceSlopeX.ToString("G17", CultureInfo.InvariantCulture)),
        new(ReferenceSlopeZParameterName, ReferenceSlopeZ.ToString("G17", CultureInfo.InvariantCulture)),
        new(ReferenceInterceptParameterName, ReferenceIntercept.ToString("G17", CultureInfo.InvariantCulture))
    ];

    public static C3DConnectedRegionGatePolicy FromRecipeParameters(
        IReadOnlyList<ToolRecipeParameter>? parameters,
        string heightUnit = "raw-height")
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (parameters.Count != ParameterNames.Count
            || ParameterNames.Any(name => parameters.Count(parameter => parameter?.Name == name) != 1))
        {
            throw new InvalidDataException(
                "Connected Region Coverage/Height Gate requires exactly one value for every recognized parameter.");
        }

        string Value(string name) => parameters.Single(parameter => parameter.Name == name).Value;
        var mode = Value(GateModeParameterName) switch
        {
            nameof(C3DConnectedRegionGateMode.Presence) => C3DConnectedRegionGateMode.Presence,
            nameof(C3DConnectedRegionGateMode.FillHeight) => C3DConnectedRegionGateMode.FillHeight,
            _ => throw new InvalidDataException("GateMode must be Presence or FillHeight.")
        };
        if (!int.TryParse(Value(SelectedRegionIndexParameterName), NumberStyles.Integer, CultureInfo.InvariantCulture, out var selectedRegionIndex)
            || selectedRegionIndex < 0)
        {
            throw new InvalidDataException("SelectedRegionIndex must be an invariant non-negative integer.");
        }

        var minimumCoverage = ParseFinite(Value(MinimumFiniteCoverageRatioParameterName), MinimumFiniteCoverageRatioParameterName);
        var minimumMean = ParseFinite(Value(MinimumMeanHeightParameterName), MinimumMeanHeightParameterName);
        var maximumMean = ParseFinite(Value(MaximumMeanHeightParameterName), MaximumMeanHeightParameterName);
        var slopeX = ParseFinite(Value(ReferenceSlopeXParameterName), ReferenceSlopeXParameterName);
        var slopeZ = ParseFinite(Value(ReferenceSlopeZParameterName), ReferenceSlopeZParameterName);
        var intercept = ParseFinite(Value(ReferenceInterceptParameterName), ReferenceInterceptParameterName);
        if (minimumCoverage is < 0d or > 1d)
        {
            throw new InvalidDataException("MinimumFiniteCoverageRatio must be between 0 and 1.");
        }
        if (minimumMean > maximumMean)
        {
            throw new InvalidDataException("MinimumMeanHeight must not exceed MaximumMeanHeight.");
        }
        if (string.IsNullOrWhiteSpace(heightUnit))
        {
            throw new InvalidDataException("Connected Region Coverage/Height Gate height unit is required.");
        }

        return new(
            mode,
            selectedRegionIndex,
            minimumCoverage,
            minimumMean,
            maximumMean,
            slopeX,
            slopeZ,
            intercept,
            heightUnit.Trim());
    }

    private static double ParseFinite(string value, string name)
    {
        if (value != value.Trim()
            || value.Contains(',', StringComparison.Ordinal)
            || !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || !double.IsFinite(parsed))
        {
            throw new InvalidDataException($"{name} must be an invariant finite number.");
        }

        return parsed;
    }
}

/// <summary>
/// Content-addressed result for one explicitly selected detected region.
/// Detection failure is never converted into an expected-object absence.
/// </summary>
public sealed record C3DConnectedRegionGateOutput(
    string SchemaVersion,
    string OutputEntityId,
    string RootSourceEntityId,
    string RawSourceEntityId,
    string RawSourceContentSha256,
    string DetectionEntityId,
    string DetectionContentSha256,
    string DetectionRootSourceSha256,
    string RegionArtifactId,
    string RegionArtifactContentSha256,
    int SelectedRegionIndex,
    string Unit,
    string FrameId,
    int GridWidth,
    int GridHeight,
    C3DConnectedRegionGatePolicy Policy,
    int TotalCellCount,
    int FiniteCellCount,
    double FiniteCoverageRatio,
    double? MeanHeight,
    double? MinimumHeight,
    double? MaximumHeight,
    C3DConnectedRegionGateDecision Decision,
    string ReasonCode,
    string Message,
    string ContentSha256)
{
    public const string CurrentSchemaVersion = "1.0";

    public int MissingCellCount => TotalCellCount - FiniteCellCount;

    public static C3DConnectedRegionGateOutput Create(
        string outputEntityId,
        string rootSourceEntityId,
        string rawSourceEntityId,
        string rawSourceContentSha256,
        string detectionEntityId,
        string detectionContentSha256,
        string detectionRootSourceSha256,
        string regionArtifactId,
        string regionArtifactContentSha256,
        string unit,
        string frameId,
        int gridWidth,
        int gridHeight,
        C3DConnectedRegionGatePolicy policy,
        int totalCellCount,
        int finiteCellCount,
        double finiteCoverageRatio,
        double? meanHeight,
        double? minimumHeight,
        double? maximumHeight,
        C3DConnectedRegionGateDecision decision,
        string reasonCode,
        string message) =>
        new C3DConnectedRegionGateOutput(
            CurrentSchemaVersion,
            outputEntityId.Trim(),
            rootSourceEntityId.Trim(),
            rawSourceEntityId.Trim(),
            rawSourceContentSha256.Trim().ToUpperInvariant(),
            detectionEntityId.Trim(),
            detectionContentSha256.Trim().ToUpperInvariant(),
            detectionRootSourceSha256.Trim().ToUpperInvariant(),
            regionArtifactId.Trim(),
            regionArtifactContentSha256.Trim().ToUpperInvariant(),
            policy.SelectedRegionIndex,
            unit.Trim(),
            frameId.Trim(),
            gridWidth,
            gridHeight,
            policy,
            totalCellCount,
            finiteCellCount,
            finiteCoverageRatio,
            meanHeight,
            minimumHeight,
            maximumHeight,
            decision,
            reasonCode.Trim(),
            message.Trim(),
            string.Empty) with
        {
            ContentSha256 = CalculateContentSha256(new C3DConnectedRegionGateOutput(
                CurrentSchemaVersion,
                outputEntityId.Trim(),
                rootSourceEntityId.Trim(),
                rawSourceEntityId.Trim(),
                rawSourceContentSha256.Trim().ToUpperInvariant(),
                detectionEntityId.Trim(),
                detectionContentSha256.Trim().ToUpperInvariant(),
                detectionRootSourceSha256.Trim().ToUpperInvariant(),
                regionArtifactId.Trim(),
                regionArtifactContentSha256.Trim().ToUpperInvariant(),
                policy.SelectedRegionIndex,
                unit.Trim(),
                frameId.Trim(),
                gridWidth,
                gridHeight,
                policy,
                totalCellCount,
                finiteCellCount,
                finiteCoverageRatio,
                meanHeight,
                minimumHeight,
                maximumHeight,
                decision,
                reasonCode.Trim(),
                message.Trim(),
                string.Empty))
        };

    public static string CalculateContentSha256(C3DConnectedRegionGateOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("OpenVisionLab.C3DConnectedRegionGateOutput");
            writer.Write(output.SchemaVersion ?? string.Empty);
            writer.Write(output.OutputEntityId ?? string.Empty);
            writer.Write(output.RootSourceEntityId ?? string.Empty);
            writer.Write(output.RawSourceEntityId ?? string.Empty);
            writer.Write(output.RawSourceContentSha256 ?? string.Empty);
            writer.Write(output.DetectionEntityId ?? string.Empty);
            writer.Write(output.DetectionContentSha256 ?? string.Empty);
            writer.Write(output.DetectionRootSourceSha256 ?? string.Empty);
            writer.Write(output.RegionArtifactId ?? string.Empty);
            writer.Write(output.RegionArtifactContentSha256 ?? string.Empty);
            writer.Write(output.SelectedRegionIndex);
            writer.Write(output.Unit ?? string.Empty);
            writer.Write(output.FrameId ?? string.Empty);
            writer.Write(output.GridWidth);
            writer.Write(output.GridHeight);
            writer.Write(output.Policy.Mode.ToString());
            writer.Write(output.Policy.SelectedRegionIndex);
            writer.Write(output.Policy.MinimumFiniteCoverageRatio);
            writer.Write(output.Policy.MinimumMeanHeight);
            writer.Write(output.Policy.MaximumMeanHeight);
            writer.Write(output.Policy.ReferenceSlopeX);
            writer.Write(output.Policy.ReferenceSlopeZ);
            writer.Write(output.Policy.ReferenceIntercept);
            writer.Write(output.Policy.HeightUnit ?? string.Empty);
            writer.Write(output.TotalCellCount);
            writer.Write(output.FiniteCellCount);
            writer.Write(output.FiniteCoverageRatio);
            WriteNullable(writer, output.MeanHeight);
            WriteNullable(writer, output.MinimumHeight);
            WriteNullable(writer, output.MaximumHeight);
            writer.Write(output.Decision.ToString());
            writer.Write(output.ReasonCode ?? string.Empty);
            writer.Write(output.Message ?? string.Empty);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    public bool TryValidate(out string message)
    {
        var valid = SchemaVersion == CurrentSchemaVersion
            && !string.IsNullOrWhiteSpace(OutputEntityId)
            && !string.IsNullOrWhiteSpace(RootSourceEntityId)
            && !string.IsNullOrWhiteSpace(RawSourceEntityId)
            && IsSha256(RawSourceContentSha256)
            && !string.IsNullOrWhiteSpace(DetectionEntityId)
            && IsSha256(DetectionContentSha256)
            && IsSha256(DetectionRootSourceSha256)
            && !string.IsNullOrWhiteSpace(RegionArtifactId)
            && IsSha256(RegionArtifactContentSha256)
            && SelectedRegionIndex >= 0
            && !string.IsNullOrWhiteSpace(Unit)
            && !string.IsNullOrWhiteSpace(FrameId)
            && GridWidth > 0
            && GridHeight > 0
            && TotalCellCount >= 0
            && FiniteCellCount >= 0
            && FiniteCellCount <= TotalCellCount
            && double.IsFinite(FiniteCoverageRatio)
            && FiniteCoverageRatio is >= 0d and <= 1d
            && (MeanHeight is not { } mean || double.IsFinite(mean))
            && (MinimumHeight is not { } minimum || double.IsFinite(minimum))
            && (MaximumHeight is not { } maximum || double.IsFinite(maximum))
            && Enum.IsDefined(Decision)
            && !string.IsNullOrWhiteSpace(ReasonCode)
            && !string.IsNullOrWhiteSpace(Message)
            && string.Equals(ContentSha256, CalculateContentSha256(this), StringComparison.OrdinalIgnoreCase);
        message = valid
            ? "Connected-region gate output identity and evidence are valid."
            : "Connected-region gate output identity or evidence is invalid.";
        return valid;
    }

    private static void WriteNullable(BinaryWriter writer, double? value)
    {
        writer.Write(value.HasValue);
        if (value is { } finite)
        {
            writer.Write(finite);
        }
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9'
            or >= 'A' and <= 'F');
}
