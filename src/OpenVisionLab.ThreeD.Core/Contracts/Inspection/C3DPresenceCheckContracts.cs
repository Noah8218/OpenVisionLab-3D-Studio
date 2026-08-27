using System.Globalization;

namespace OpenVisionLab.ThreeD.Core;

public static class C3DPresenceCheckMetricNames
{
    public const string Presence = "Presence";
    public const string FiniteCoverage = "Finite coverage";
    public const string MeanRawHeight = "Mean raw height";
    public const string FiniteSamples = "Finite samples";
    public const string MissingSamples = "Missing samples";
}

/// <summary>
/// Inclusive acceptance limits for one explicitly authored height/coverage
/// feature. The raw height limits use the source field's declared unit; they
/// are not calibrated physical limits.
/// </summary>
public sealed record C3DPresenceCheckPolicy(
    double MinimumFiniteCoverageRatio,
    double MinimumMeanRawHeight,
    double MaximumMeanRawHeight)
{
    public static readonly string[] ParameterNames =
    [
        "MinimumFiniteCoverageRatio",
        "MinimumMeanRawHeight",
        "MaximumMeanRawHeight"
    ];

    public IReadOnlyList<ToolRecipeParameter> ToRecipeParameters() =>
    [
        new(
            "MinimumFiniteCoverageRatio",
            MinimumFiniteCoverageRatio.ToString("G17", CultureInfo.InvariantCulture)),
        new(
            "MinimumMeanRawHeight",
            MinimumMeanRawHeight.ToString("G17", CultureInfo.InvariantCulture)),
        new(
            "MaximumMeanRawHeight",
            MaximumMeanRawHeight.ToString("G17", CultureInfo.InvariantCulture))
    ];

    public void Validate()
    {
        if (!double.IsFinite(MinimumFiniteCoverageRatio)
            || MinimumFiniteCoverageRatio is < 0d or > 1d)
        {
            throw new InvalidDataException(
                "MinimumFiniteCoverageRatio must be a finite value between zero and one inclusive.");
        }

        if (!double.IsFinite(MinimumMeanRawHeight)
            || !double.IsFinite(MaximumMeanRawHeight))
        {
            throw new InvalidDataException(
                "Presence Check mean raw-height limits must be finite numbers.");
        }

        if (MinimumMeanRawHeight > MaximumMeanRawHeight)
        {
            throw new InvalidDataException(
                "MinimumMeanRawHeight must not exceed MaximumMeanRawHeight.");
        }
    }

    public static C3DPresenceCheckPolicy FromRecipeParameters(
        IReadOnlyList<ToolRecipeParameter> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (parameters.Count != ParameterNames.Length
            || parameters.Any(parameter =>
                !ParameterNames.Contains(parameter.Name, StringComparer.Ordinal))
            || ParameterNames.Any(name =>
                parameters.Count(parameter =>
                    string.Equals(parameter.Name, name, StringComparison.Ordinal)) != 1))
        {
            throw new InvalidDataException(
                "Presence Check v1 requires exactly one MinimumFiniteCoverageRatio, "
                + "MinimumMeanRawHeight, and MaximumMeanRawHeight value with no unknown parameters.");
        }

        var policy = new C3DPresenceCheckPolicy(
            ParseFinite(parameters, "MinimumFiniteCoverageRatio"),
            ParseFinite(parameters, "MinimumMeanRawHeight"),
            ParseFinite(parameters, "MaximumMeanRawHeight"));
        policy.Validate();
        return policy;
    }

    private static double ParseFinite(
        IReadOnlyList<ToolRecipeParameter> parameters,
        string name)
    {
        var text = parameters.Single(parameter => parameter.Name == name).Value;
        if (text != text.Trim()
            || text.Contains(',', StringComparison.Ordinal)
            || !double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var value)
            || !double.IsFinite(value))
        {
            throw new InvalidDataException(
                $"{name} must be an invariant finite number.");
        }

        return value;
    }
}

public sealed record C3DPresenceCheckFeatureMetric(
    string FeatureId,
    ToolRecipeGridRectangle Region,
    int TotalCellCount,
    int FiniteCellCount,
    int MissingCellCount,
    double FiniteCoverageRatio,
    double? MeanRawHeight,
    ResultStatus Decision,
    string DecisionReason)
{
    public bool IsPresent => Decision == ResultStatus.Pass;
}

public sealed record C3DPresenceCheckOverlay(
    string OverlayId,
    string FeatureId,
    ToolRecipeGridRectangle Region,
    ResultStatus Status);

/// <summary>
/// Immutable evidence for one source-bound explicit feature. The feature is
/// represented by the exact recipe-owned selection and source-grid rectangle;
/// no inferred mask or calibrated physical measurement is implied.
/// </summary>
public sealed record C3DPresenceCheckOutput(
    string OutputEntityId,
    string RootSourceEntityId,
    string InputEntityId,
    string InputContentSha256,
    string Unit,
    string FrameId,
    string FeatureSelectionId,
    ToolRecipeGridRectangle FeatureRegion,
    C3DPresenceCheckPolicy Policy,
    C3DPresenceCheckFeatureMetric Feature,
    string ContentSha256,
    C3DPresenceCheckOverlay? Overlay = null)
{
    public const string ContractVersion = "1.0";
}
