using System.Globalization;

namespace OpenVisionLab.ThreeD.Core;

public enum C3DCompletenessCellShape
{
    GridRectangle
}

public static class C3DCompletenessMetricNames
{
    public const string FiniteCoverageSuffix = "finite coverage";
    public const string ReferenceRelativeMeanSuffix =
        "reference-relative mean";
    public const string MinimumFiniteCoverage =
        "Minimum finite coverage";
    public const string MinimumReferenceRelativeMean =
        "Minimum reference-relative mean";
    public const string MaximumReferenceRelativeMean =
        "Maximum reference-relative mean";

    public static string FiniteCoverage(string cellId) =>
        $"{cellId} {FiniteCoverageSuffix}";

    public static string ReferenceRelativeMean(string cellId) =>
        $"{cellId} {ReferenceRelativeMeanSuffix}";

    public static bool TryGetCellId(
        string metricName,
        string suffix,
        out string cellId)
    {
        ArgumentNullException.ThrowIfNull(metricName);
        ArgumentNullException.ThrowIfNull(suffix);
        cellId = string.Empty;
        var delimiter = $" {suffix}";
        if (!metricName.EndsWith(delimiter, StringComparison.Ordinal))
        {
            return false;
        }

        var candidate = metricName[..^delimiter.Length];
        var columnSeparator = candidate.IndexOf(".c", StringComparison.Ordinal);
        if (!candidate.StartsWith('r')
            || columnSeparator < 2
            || columnSeparator == candidate.Length - 2
            || !int.TryParse(
                candidate.AsSpan(1, columnSeparator - 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var row)
            || !int.TryParse(
                candidate.AsSpan(columnSeparator + 2),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var column)
            || row < 1
            || column < 1
            || !string.Equals(
                candidate,
                $"r{row:D3}.c{column:D3}",
                StringComparison.Ordinal))
        {
            return false;
        }

        cellId = candidate;
        return true;
    }
}

/// <summary>
/// Deterministic native-grid cell layout. X advances by source columns and Z
/// advances by source rows; no physical calibration is implied.
/// </summary>
public sealed record C3DCompletenessGridProfile(
    int Rows,
    int Columns,
    int XPitchColumns,
    int ZPitchRows,
    int CellWidthColumns,
    int CellHeightRows,
    C3DCompletenessCellShape CellShape)
{
    public static readonly string[] ParameterNames =
    [
        "Rows",
        "Columns",
        "XPitchColumns",
        "ZPitchRows",
        "CellWidthColumns",
        "CellHeightRows",
        "CellShape"
    ];

    public IReadOnlyList<ToolRecipeParameter> ToRecipeParameters() =>
    [
        new("Rows", Rows.ToString(CultureInfo.InvariantCulture)),
        new("Columns", Columns.ToString(CultureInfo.InvariantCulture)),
        new("XPitchColumns", XPitchColumns.ToString(CultureInfo.InvariantCulture)),
        new("ZPitchRows", ZPitchRows.ToString(CultureInfo.InvariantCulture)),
        new("CellWidthColumns", CellWidthColumns.ToString(CultureInfo.InvariantCulture)),
        new("CellHeightRows", CellHeightRows.ToString(CultureInfo.InvariantCulture)),
        new("CellShape", CellShape.ToString())
    ];

    public static C3DCompletenessGridProfile FromRecipeParameters(
        IReadOnlyList<ToolRecipeParameter> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var allowedNames = ParameterNames
            .Concat(C3DCompletenessPresencePolicy.ParameterNames)
            .ToHashSet(StringComparer.Ordinal);
        if (parameters.Any(parameter => !allowedNames.Contains(parameter.Name))
            || ParameterNames.Any(name =>
                parameters.Count(parameter =>
                    string.Equals(parameter.Name, name, StringComparison.Ordinal)) != 1))
        {
            throw new InvalidDataException(
                "Completeness Grid v1 requires one Rows, Columns, XPitchColumns, "
                + "ZPitchRows, CellWidthColumns, CellHeightRows, and CellShape value, "
                + "with no unknown parameters.");
        }

        int Positive(string name)
        {
            var text = parameters.Single(parameter => parameter.Name == name).Value;
            if (!int.TryParse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var value)
                || value < 1)
            {
                throw new InvalidDataException($"{name} must be an integer greater than zero.");
            }

            return value;
        }

        var shapeText = parameters.Single(
            parameter => parameter.Name == "CellShape").Value;
        if (!Enum.TryParse<C3DCompletenessCellShape>(
                shapeText,
                ignoreCase: false,
                out var shape)
            || shape != C3DCompletenessCellShape.GridRectangle)
        {
            throw new InvalidDataException(
                "CellShape must be the typed GridRectangle v1 shape.");
        }

        var profile = new C3DCompletenessGridProfile(
            Positive("Rows"),
            Positive("Columns"),
            Positive("XPitchColumns"),
            Positive("ZPitchRows"),
            Positive("CellWidthColumns"),
            Positive("CellHeightRows"),
            shape);
        if (profile.XPitchColumns < profile.CellWidthColumns
            || profile.ZPitchRows < profile.CellHeightRows)
        {
            throw new InvalidDataException(
                "Completeness Grid v1 cells must not overlap: X/Z pitch must be "
                + "at least the cell width/height.");
        }

        return profile;
    }
}

/// <summary>
/// Authored deterministic cell acceptance policy. Coverage and height bounds
/// are inclusive. Recipe parameters may omit this entire group for backward
/// compatible evidence-only execution, but partial groups are invalid.
/// </summary>
public sealed record C3DCompletenessPresencePolicy(
    double MinimumFiniteCoverageRatio,
    double MinimumReferenceRelativeMeanRawHeight,
    double MaximumReferenceRelativeMeanRawHeight)
{
    public static readonly string[] ParameterNames =
    [
        "MinimumFiniteCoverageRatio",
        "MinimumReferenceRelativeMeanRawHeight",
        "MaximumReferenceRelativeMeanRawHeight"
    ];

    public IReadOnlyList<ToolRecipeParameter> ToRecipeParameters() =>
    [
        new(
            "MinimumFiniteCoverageRatio",
            MinimumFiniteCoverageRatio.ToString("G17", CultureInfo.InvariantCulture)),
        new(
            "MinimumReferenceRelativeMeanRawHeight",
            MinimumReferenceRelativeMeanRawHeight.ToString("G17", CultureInfo.InvariantCulture)),
        new(
            "MaximumReferenceRelativeMeanRawHeight",
            MaximumReferenceRelativeMeanRawHeight.ToString("G17", CultureInfo.InvariantCulture))
    ];

    public static C3DCompletenessPresencePolicy? FromOptionalRecipeParameters(
        IReadOnlyList<ToolRecipeParameter> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var policyCount = parameters.Count(parameter =>
            ParameterNames.Contains(parameter.Name, StringComparer.Ordinal));
        if (policyCount == 0)
        {
            return null;
        }

        if (policyCount != ParameterNames.Length
            || ParameterNames.Any(name =>
                parameters.Count(parameter =>
                    string.Equals(parameter.Name, name, StringComparison.Ordinal)) != 1))
        {
            throw new InvalidDataException(
                "Completeness Grid presence policy must provide all three values: "
                + "MinimumFiniteCoverageRatio, MinimumReferenceRelativeMeanRawHeight, "
                + "and MaximumReferenceRelativeMeanRawHeight.");
        }

        double Finite(string name)
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

        var policy = new C3DCompletenessPresencePolicy(
            Finite("MinimumFiniteCoverageRatio"),
            Finite("MinimumReferenceRelativeMeanRawHeight"),
            Finite("MaximumReferenceRelativeMeanRawHeight"));
        if (policy.MinimumFiniteCoverageRatio is < 0d or > 1d)
        {
            throw new InvalidDataException(
                "MinimumFiniteCoverageRatio must be between zero and one inclusive.");
        }

        if (policy.MinimumReferenceRelativeMeanRawHeight
            > policy.MaximumReferenceRelativeMeanRawHeight)
        {
            throw new InvalidDataException(
                "MinimumReferenceRelativeMeanRawHeight must not exceed "
                + "MaximumReferenceRelativeMeanRawHeight.");
        }

        return policy;
    }
}

public sealed record C3DCompletenessCellMetric(
    string CellId,
    int GridRow,
    int GridColumn,
    ToolRecipeGridRectangle Region,
    int TotalCellCount,
    int FiniteCellCount,
    int MissingCellCount,
    double FiniteCoverageRatio,
    double? MeanRawHeight,
    double ReferenceMeanRawHeight,
    double? ReferenceRelativeMeanRawHeight,
    ResultStatus? Decision = null,
    string DecisionReason = "");

public sealed record C3DCompletenessCellOverlay(
    string OverlayId,
    string CellId,
    ToolRecipeGridRectangle Region,
    ResultStatus Status);

/// <summary>
/// Typed cell evidence with an optional authored acceptance policy. A null
/// policy is the backward-compatible H-02 evidence-only contract.
/// </summary>
public sealed record C3DCompletenessGridMetricOutput(
    string OutputEntityId,
    string RootSourceEntityId,
    string InputEntityId,
    string InputContentSha256,
    string Unit,
    string FrameId,
    string ReferenceSelectionId,
    ToolRecipeGridRectangle ReferenceRegion,
    int ReferenceFiniteCellCount,
    double ReferenceMeanRawHeight,
    string InspectionGridSelectionId,
    ToolRecipeGridRectangle InspectionGridRegion,
    C3DCompletenessGridProfile Profile,
    IReadOnlyList<C3DCompletenessCellMetric> Cells,
    string ContentSha256,
    C3DCompletenessPresencePolicy? PresencePolicy = null,
    int PassedCellCount = 0,
    int FailedCellCount = 0,
    ResultStatus AggregateStatus = ResultStatus.Warning,
    IReadOnlyList<C3DCompletenessCellOverlay>? CellOverlays = null);
