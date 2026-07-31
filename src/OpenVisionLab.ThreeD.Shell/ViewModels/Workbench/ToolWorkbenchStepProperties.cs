using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Controls.WpfPropertyGrid;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

[CategoryOrder("Acceptance", 0)]
[CategoryOrder("Sampling", 1)]
[CategoryOrder("Compatibility", 2)]
public sealed class ThicknessStepProperties
{
    internal static readonly HashSet<string> MappedNames =
        ["MinimumThickness", "MaximumThickness", "MinimumValidSampleCount"];

    [Category("Acceptance")]
    [DisplayName("Minimum thickness")]
    [Description("Inclusive lower acceptance limit in the source field's declared scalar unit.")]
    [PropertyOrder(0)]
    public double MinimumThickness { get; set; }

    [Category("Acceptance")]
    [DisplayName("Maximum thickness")]
    [Description("Inclusive upper acceptance limit in the source field's declared scalar unit.")]
    [PropertyOrder(1)]
    public double MaximumThickness { get; set; }

    [Category("Sampling")]
    [DisplayName("Minimum valid samples")]
    [Description("Minimum finite samples required inside the recipe-owned GridRectangle.")]
    [PropertyOrder(0)]
    public int MinimumValidSampleCount { get; set; }

    [Category("Compatibility")]
    [DisplayName("Unmapped parameters")]
    [ReadOnly(true)]
    public string UnmappedParameters { get; init; } = "(none)";

    internal static ThicknessStepProperties From(ToolWorkbenchPipelineStepItem step) => new()
    {
        MinimumThickness = ParseDouble(step, "MinimumThickness"),
        MaximumThickness = ParseDouble(step, "MaximumThickness"),
        MinimumValidSampleCount = ParseInt(step, "MinimumValidSampleCount"),
        UnmappedParameters = ToolWorkbenchStepPropertySession.GetUnmappedParameters(step, MappedNames)
    };

    internal bool TryValidate(out string message)
    {
        if (!double.IsFinite(MinimumThickness) || !double.IsFinite(MaximumThickness) || MinimumThickness > MaximumThickness)
        {
            message = "Thickness limits must be finite and ordered.";
            return false;
        }
        if (MinimumValidSampleCount < 1)
        {
            message = "Minimum valid samples must be at least one.";
            return false;
        }
        message = string.Empty;
        return true;
    }

    private static double ParseDouble(ToolWorkbenchPipelineStepItem step, string name) =>
        double.TryParse(ToolWorkbenchStepPropertySession.GetParameter(step, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value : double.NaN;
    private static int ParseInt(ToolWorkbenchPipelineStepItem step, string name) =>
        int.TryParse(ToolWorkbenchStepPropertySession.GetParameter(step, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value : 0;
}

[CategoryOrder("Grid layout", 0)]
[CategoryOrder("Cell geometry", 1)]
[CategoryOrder("Acceptance", 2)]
[CategoryOrder("Compatibility", 3)]
public sealed class CompletenessGridStepProperties
{
    internal static readonly HashSet<string> MappedNames =
        C3DCompletenessGridProfile.ParameterNames
            .Concat(C3DCompletenessPresencePolicy.ParameterNames)
            .ToHashSet(StringComparer.Ordinal);

    [Category("Grid layout")]
    [DisplayName("Rows")]
    [Description("Number of deterministic cell rows generated inside the Inspection Grid ROI.")]
    [PropertyOrder(0)]
    public int Rows { get; set; }

    [Category("Grid layout")]
    [DisplayName("Columns")]
    [Description("Number of deterministic cell columns generated inside the Inspection Grid ROI.")]
    [PropertyOrder(1)]
    public int Columns { get; set; }

    [Category("Grid layout")]
    [DisplayName("X pitch (columns)")]
    [Description("Native-grid column advance between cell origins. This is not a calibrated physical distance.")]
    [PropertyOrder(2)]
    public int XPitchColumns { get; set; }

    [Category("Grid layout")]
    [DisplayName("Z pitch (rows)")]
    [Description("Native-grid row advance between cell origins. This is not a calibrated physical distance.")]
    [PropertyOrder(3)]
    public int ZPitchRows { get; set; }

    [Category("Cell geometry")]
    [DisplayName("Cell width (columns)")]
    [Description("Native-grid column count in each generated cell.")]
    [PropertyOrder(0)]
    public int CellWidthColumns { get; set; }

    [Category("Cell geometry")]
    [DisplayName("Cell height (rows)")]
    [Description("Native-grid row count in each generated cell.")]
    [PropertyOrder(1)]
    public int CellHeightRows { get; set; }

    [Category("Cell geometry")]
    [DisplayName("Cell shape")]
    [Description("Typed v1 cell geometry. GridRectangle is the only supported shape.")]
    [PropertyOrder(2)]
    public C3DCompletenessCellShape CellShape { get; set; }

    [Category("Acceptance")]
    [DisplayName("Minimum finite coverage ratio")]
    [Description("Inclusive minimum finite-cell ratio from 0 through 1. Missing samples never fabricate a height.")]
    [PropertyOrder(0)]
    public double MinimumFiniteCoverageRatio { get; set; }

    [Category("Acceptance")]
    [DisplayName("Minimum relative mean raw height")]
    [Description("Inclusive lower limit for cell mean raw height relative to the Reference ROI mean.")]
    [PropertyOrder(1)]
    public double MinimumReferenceRelativeMeanRawHeight { get; set; }

    [Category("Acceptance")]
    [DisplayName("Maximum relative mean raw height")]
    [Description("Inclusive upper limit for cell mean raw height relative to the Reference ROI mean.")]
    [PropertyOrder(2)]
    public double MaximumReferenceRelativeMeanRawHeight { get; set; }

    [Category("Compatibility")]
    [DisplayName("Unmapped parameters")]
    [ReadOnly(true)]
    public string UnmappedParameters { get; init; } = "(none)";

    internal static CompletenessGridStepProperties From(
        ToolWorkbenchPipelineStepItem step)
    {
        var profile = C3DCompletenessGridProfile.FromRecipeParameters(
            step.Parameters
                .Select(parameter =>
                    new ToolRecipeParameter(parameter.Name, parameter.Value))
                .ToArray());
        var policy =
            C3DCompletenessPresencePolicy.FromOptionalRecipeParameters(
                step.Parameters
                    .Select(parameter =>
                        new ToolRecipeParameter(parameter.Name, parameter.Value))
                    .ToArray())
            ?? new C3DCompletenessPresencePolicy(0.95d, -100000d, 100000d);
        return new CompletenessGridStepProperties
        {
            Rows = profile.Rows,
            Columns = profile.Columns,
            XPitchColumns = profile.XPitchColumns,
            ZPitchRows = profile.ZPitchRows,
            CellWidthColumns = profile.CellWidthColumns,
            CellHeightRows = profile.CellHeightRows,
            CellShape = profile.CellShape,
            MinimumFiniteCoverageRatio =
                policy.MinimumFiniteCoverageRatio,
            MinimumReferenceRelativeMeanRawHeight =
                policy.MinimumReferenceRelativeMeanRawHeight,
            MaximumReferenceRelativeMeanRawHeight =
                policy.MaximumReferenceRelativeMeanRawHeight,
            UnmappedParameters =
                ToolWorkbenchStepPropertySession.GetUnmappedParameters(
                    step,
                    MappedNames)
        };
    }

    internal bool TryCreateContracts(
        out C3DCompletenessGridProfile? profile,
        out C3DCompletenessPresencePolicy? policy,
        out string message)
    {
        profile = null;
        policy = null;
        try
        {
            profile = C3DCompletenessGridProfile.FromRecipeParameters(
                new C3DCompletenessGridProfile(
                    Rows,
                    Columns,
                    XPitchColumns,
                    ZPitchRows,
                    CellWidthColumns,
                    CellHeightRows,
                    CellShape).ToRecipeParameters());
            policy =
                C3DCompletenessPresencePolicy.FromOptionalRecipeParameters(
                    new C3DCompletenessPresencePolicy(
                        MinimumFiniteCoverageRatio,
                        MinimumReferenceRelativeMeanRawHeight,
                        MaximumReferenceRelativeMeanRawHeight)
                    .ToRecipeParameters());
            message = string.Empty;
            return true;
        }
        catch (Exception exception) when (
            exception is InvalidDataException
            or ArgumentException
            or OverflowException)
        {
            message = exception.Message;
            return false;
        }
    }
}

[CategoryOrder("Acceptance", 0)]
[CategoryOrder("Sampling", 1)]
[CategoryOrder("Compatibility", 2)]
public sealed class WarpageStepProperties
{
    internal static readonly HashSet<string> MappedNames =
        ["MaximumPeakToValley", "MaximumRms", "MinimumValidSampleCount"];

    [Category("Acceptance")]
    [DisplayName("Maximum peak-to-valley")]
    [Description("Maximum allowed best-fit-plane residual peak-to-valley value.")]
    [PropertyOrder(0)]
    public double MaximumPeakToValley { get; set; }

    [Category("Acceptance")]
    [DisplayName("Maximum RMS")]
    [Description("Maximum allowed best-fit-plane residual RMS value.")]
    [PropertyOrder(1)]
    public double MaximumRms { get; set; }

    [Category("Sampling")]
    [DisplayName("Minimum valid samples")]
    [Description("Minimum finite samples required inside the recipe-owned GridRectangle.")]
    [PropertyOrder(0)]
    public int MinimumValidSampleCount { get; set; }

    [Category("Compatibility")]
    [DisplayName("Unmapped parameters")]
    [ReadOnly(true)]
    public string UnmappedParameters { get; init; } = "(none)";

    internal static WarpageStepProperties From(ToolWorkbenchPipelineStepItem step) => new()
    {
        MaximumPeakToValley = ParseDouble(step, "MaximumPeakToValley"),
        MaximumRms = ParseDouble(step, "MaximumRms"),
        MinimumValidSampleCount = ParseInt(step, "MinimumValidSampleCount"),
        UnmappedParameters = ToolWorkbenchStepPropertySession.GetUnmappedParameters(step, MappedNames)
    };

    internal bool TryValidate(out string message)
    {
        if (!double.IsFinite(MaximumPeakToValley) || MaximumPeakToValley <= 0d
            || !double.IsFinite(MaximumRms) || MaximumRms <= 0d)
        {
            message = "Warpage peak-to-valley and RMS limits must be finite and greater than zero.";
            return false;
        }
        if (MinimumValidSampleCount < 3)
        {
            message = "Warpage requires at least three valid samples.";
            return false;
        }
        message = string.Empty;
        return true;
    }

    private static double ParseDouble(ToolWorkbenchPipelineStepItem step, string name) =>
        double.TryParse(ToolWorkbenchStepPropertySession.GetParameter(step, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value : double.NaN;
    private static int ParseInt(ToolWorkbenchPipelineStepItem step, string name) =>
        int.TryParse(ToolWorkbenchStepPropertySession.GetParameter(step, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value : 0;
}

[CategoryOrder("Acceptance", 0)]
[CategoryOrder("Sampling", 1)]
[CategoryOrder("Compatibility", 2)]
public sealed class PlaneFlatnessStepProperties
{
    internal static readonly HashSet<string> MappedNames =
        ["MaximumFlatness", "MinimumReferenceSampleCount", "MinimumMeasurementSampleCount"];

    [Category("Acceptance")]
    [DisplayName("Maximum flatness")]
    [Description("Inclusive maximum signed-distance peak-to-valley value in the TransformedHeightField reference unit.")]
    [PropertyOrder(0)]
    public double MaximumFlatness { get; set; }

    [Category("Sampling")]
    [DisplayName("Minimum reference samples")]
    [Description("Minimum finite samples required to fit the reference plane.")]
    [PropertyOrder(0)]
    public int MinimumReferenceSampleCount { get; set; }

    [Category("Sampling")]
    [DisplayName("Minimum measurement samples")]
    [Description("Minimum finite samples required in the measured surface ROI.")]
    [PropertyOrder(1)]
    public int MinimumMeasurementSampleCount { get; set; }

    [Category("Compatibility")]
    [DisplayName("Unmapped parameters")]
    [ReadOnly(true)]
    public string UnmappedParameters { get; init; } = "(none)";

    internal static PlaneFlatnessStepProperties From(ToolWorkbenchPipelineStepItem step) => new()
    {
        MaximumFlatness = ParseDouble(step, "MaximumFlatness"),
        MinimumReferenceSampleCount = ParseInt(step, "MinimumReferenceSampleCount"),
        MinimumMeasurementSampleCount = ParseInt(step, "MinimumMeasurementSampleCount"),
        UnmappedParameters = ToolWorkbenchStepPropertySession.GetUnmappedParameters(step, MappedNames)
    };

    internal bool TryValidate(out string message)
    {
        if (!double.IsFinite(MaximumFlatness) || MaximumFlatness <= 0d)
        {
            message = "Maximum flatness must be finite and greater than zero.";
            return false;
        }
        if (MinimumReferenceSampleCount < 3 || MinimumMeasurementSampleCount < 3)
        {
            message = "Plane Flatness requires at least three finite samples in each ROI.";
            return false;
        }
        message = string.Empty;
        return true;
    }

    private static double ParseDouble(ToolWorkbenchPipelineStepItem step, string name) =>
        double.TryParse(ToolWorkbenchStepPropertySession.GetParameter(step, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value : double.NaN;
    private static int ParseInt(ToolWorkbenchPipelineStepItem step, string name) =>
        int.TryParse(ToolWorkbenchStepPropertySession.GetParameter(step, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value : 0;
}

[CategoryOrder("Acceptance", 0)]
[CategoryOrder("Compatibility", 1)]
public sealed class PointPairDimensionsStepProperties
{
    internal static readonly HashSet<string> MappedNames =
        ["ExpectedDistance", "DistanceTolerance", "ExpectedPlanarWidth", "PlanarWidthTolerance", "ExpectedElevationAngleDegrees", "ElevationAngleToleranceDegrees"];

    [Category("Acceptance")]
    [DisplayName("Expected 3D distance")]
    [Description("Expected full-XYZ distance in the Published TransformedHeightField reference unit.")]
    [PropertyOrder(0)]
    public double ExpectedDistance { get; set; }

    [Category("Acceptance")]
    [DisplayName("Distance tolerance")]
    [PropertyOrder(1)]
    public double DistanceTolerance { get; set; }

    [Category("Acceptance")]
    [DisplayName("Expected planar width")]
    [Description("Expected distance after removing the component along the reference-grid height axis.")]
    [PropertyOrder(2)]
    public double ExpectedPlanarWidth { get; set; }

    [Category("Acceptance")]
    [DisplayName("Planar width tolerance")]
    [PropertyOrder(3)]
    public double PlanarWidthTolerance { get; set; }

    [Category("Acceptance")]
    [DisplayName("Expected elevation angle")]
    [Description("Signed elevation from the reference plane toward the reference-grid height axis, in degrees.")]
    [PropertyOrder(4)]
    public double ExpectedElevationAngleDegrees { get; set; }

    [Category("Acceptance")]
    [DisplayName("Elevation angle tolerance")]
    [PropertyOrder(5)]
    public double ElevationAngleToleranceDegrees { get; set; }

    [Category("Compatibility")]
    [DisplayName("Unmapped parameters")]
    [ReadOnly(true)]
    public string UnmappedParameters { get; init; } = "(none)";

    internal static PointPairDimensionsStepProperties From(ToolWorkbenchPipelineStepItem step) => new()
    {
        ExpectedDistance = Parse(step, "ExpectedDistance"),
        DistanceTolerance = Parse(step, "DistanceTolerance"),
        ExpectedPlanarWidth = Parse(step, "ExpectedPlanarWidth"),
        PlanarWidthTolerance = Parse(step, "PlanarWidthTolerance"),
        ExpectedElevationAngleDegrees = Parse(step, "ExpectedElevationAngleDegrees"),
        ElevationAngleToleranceDegrees = Parse(step, "ElevationAngleToleranceDegrees"),
        UnmappedParameters = ToolWorkbenchStepPropertySession.GetUnmappedParameters(step, MappedNames)
    };

    internal bool TryValidate(out string message)
    {
        if (!NonNegative(ExpectedDistance) || !NonNegative(DistanceTolerance)
            || !NonNegative(ExpectedPlanarWidth) || !NonNegative(PlanarWidthTolerance)
            || !double.IsFinite(ExpectedElevationAngleDegrees) || ExpectedElevationAngleDegrees is < -90d or > 90d
            || !NonNegative(ElevationAngleToleranceDegrees))
        {
            message = "Point Pair expected lengths and tolerances must be finite and non-negative; elevation angle must be between -90 and 90 degrees.";
            return false;
        }
        message = string.Empty;
        return true;
    }

    private static bool NonNegative(double value) => double.IsFinite(value) && value >= 0d;
    private static double Parse(ToolWorkbenchPipelineStepItem step, string name) =>
        double.TryParse(ToolWorkbenchStepPropertySession.GetParameter(step, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value : double.NaN;
}

[CategoryOrder("Acceptance", 0)]
[CategoryOrder("Compatibility", 1)]
public sealed class GapFlushStepProperties
{
    internal static readonly HashSet<string> MappedNames =
        ["ExpectedGap", "GapTolerance", "ExpectedFlush", "FlushTolerance"];

    [Category("Acceptance")]
    [DisplayName("Expected signed gap")]
    [Description("Expected signed U-axis separation between the second ROI left edge and first ROI right edge.")]
    [PropertyOrder(0)]
    public double ExpectedGap { get; set; }

    [Category("Acceptance")]
    [DisplayName("Gap tolerance")]
    [PropertyOrder(1)]
    public double GapTolerance { get; set; }

    [Category("Acceptance")]
    [DisplayName("Expected signed flush")]
    [Description("Expected second-minus-first mean height along the TransformedHeightField H axis.")]
    [PropertyOrder(2)]
    public double ExpectedFlush { get; set; }

    [Category("Acceptance")]
    [DisplayName("Flush tolerance")]
    [PropertyOrder(3)]
    public double FlushTolerance { get; set; }

    [Category("Compatibility")]
    [DisplayName("Unmapped parameters")]
    [ReadOnly(true)]
    public string UnmappedParameters { get; init; } = "(none)";

    internal static GapFlushStepProperties From(ToolWorkbenchPipelineStepItem step) => new()
    {
        ExpectedGap = Parse(step, "ExpectedGap"),
        GapTolerance = Parse(step, "GapTolerance"),
        ExpectedFlush = Parse(step, "ExpectedFlush"),
        FlushTolerance = Parse(step, "FlushTolerance"),
        UnmappedParameters = ToolWorkbenchStepPropertySession.GetUnmappedParameters(step, MappedNames)
    };

    internal bool TryValidate(out string message)
    {
        if (!double.IsFinite(ExpectedGap) || !NonNegative(GapTolerance)
            || !double.IsFinite(ExpectedFlush) || !NonNegative(FlushTolerance))
        {
            message = "Gap / Flush expected values must be finite and tolerances must be non-negative.";
            return false;
        }
        message = string.Empty;
        return true;
    }

    private static bool NonNegative(double value) => double.IsFinite(value) && value >= 0d;
    private static double Parse(ToolWorkbenchPipelineStepItem step, string name) =>
        double.TryParse(ToolWorkbenchStepPropertySession.GetParameter(step, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value : double.NaN;
}

[CategoryOrder("Acceptance", 0)]
[CategoryOrder("Compatibility", 1)]
public sealed class VolumeStepProperties
{
    internal static readonly HashSet<string> MappedNames =
        ["ExpectedNetVolume", "VolumeTolerance"];

    [Category("Acceptance")]
    [DisplayName("Expected signed net volume")]
    [Description("Expected signed integral of H-axis height relative to the fitted reference plane, in the declared reference-grid model unit cubed.")]
    [PropertyOrder(0)]
    public double ExpectedNetVolume { get; set; }

    [Category("Acceptance")]
    [DisplayName("Volume tolerance")]
    [Description("Allowed absolute difference from the expected signed net volume.")]
    [PropertyOrder(1)]
    public double VolumeTolerance { get; set; }

    [Category("Compatibility")]
    [DisplayName("Unmapped parameters")]
    [ReadOnly(true)]
    public string UnmappedParameters { get; init; } = "(none)";

    internal static VolumeStepProperties From(ToolWorkbenchPipelineStepItem step) => new()
    {
        ExpectedNetVolume = Parse(step, "ExpectedNetVolume"),
        VolumeTolerance = Parse(step, "VolumeTolerance"),
        UnmappedParameters = ToolWorkbenchStepPropertySession.GetUnmappedParameters(step, MappedNames)
    };

    internal bool TryValidate(out string message)
    {
        if (!double.IsFinite(ExpectedNetVolume) || !double.IsFinite(VolumeTolerance) || VolumeTolerance < 0d)
        {
            message = "Expected net volume must be finite and volume tolerance must be finite and non-negative.";
            return false;
        }
        message = string.Empty;
        return true;
    }

    private static double Parse(ToolWorkbenchPipelineStepItem step, string name) =>
        double.TryParse(ToolWorkbenchStepPropertySession.GetParameter(step, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value : double.NaN;
}

[CategoryOrder("Acceptance", 0)]
[CategoryOrder("Compatibility", 1)]
public sealed class CrossSectionDimensionsStepProperties
{
    internal static readonly HashSet<string> MappedNames =
        ["ExpectedWidth", "WidthTolerance", "ExpectedHeightRange", "HeightTolerance"];

    [Category("Acceptance")]
    [DisplayName("Expected section width")]
    [Description("Expected U-axis distance between the first and last finite cells on the authored A3 row segment.")]
    [PropertyOrder(0)]
    public double ExpectedWidth { get; set; }

    [Category("Acceptance")]
    [DisplayName("Width tolerance")]
    [PropertyOrder(1)]
    public double WidthTolerance { get; set; }

    [Category("Acceptance")]
    [DisplayName("Expected height range")]
    [Description("Expected maximum-minus-minimum H value along the authored row segment.")]
    [PropertyOrder(2)]
    public double ExpectedHeightRange { get; set; }

    [Category("Acceptance")]
    [DisplayName("Height tolerance")]
    [PropertyOrder(3)]
    public double HeightTolerance { get; set; }

    [Category("Compatibility")]
    [DisplayName("Unmapped parameters")]
    [ReadOnly(true)]
    public string UnmappedParameters { get; init; } = "(none)";

    internal static CrossSectionDimensionsStepProperties From(ToolWorkbenchPipelineStepItem step) => new()
    {
        ExpectedWidth = Parse(step, "ExpectedWidth"),
        WidthTolerance = Parse(step, "WidthTolerance"),
        ExpectedHeightRange = Parse(step, "ExpectedHeightRange"),
        HeightTolerance = Parse(step, "HeightTolerance"),
        UnmappedParameters = ToolWorkbenchStepPropertySession.GetUnmappedParameters(step, MappedNames)
    };

    internal bool TryValidate(out string message)
    {
        if (!NonNegative(ExpectedWidth) || !NonNegative(WidthTolerance)
            || !NonNegative(ExpectedHeightRange) || !NonNegative(HeightTolerance))
        {
            message = "Cross-section expected dimensions and tolerances must be finite and non-negative.";
            return false;
        }
        message = string.Empty;
        return true;
    }

    private static bool NonNegative(double value) => double.IsFinite(value) && value >= 0d;
    private static double Parse(ToolWorkbenchPipelineStepItem step, string name) =>
        double.TryParse(ToolWorkbenchStepPropertySession.GetParameter(step, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value : double.NaN;
}

public enum FilterMethod
{
    Median
}

public enum FilterMissingValuePolicy
{
    PreserveMask
}

public enum FilterBoundaryPolicy
{
    AvailableNeighbors
}

[CategoryOrder("Filter", 0)]
[CategoryOrder("Compatibility", 1)]
public sealed class FilterStepProperties
{
    internal static readonly HashSet<string> MappedNames =
        ["Method", "KernelSize", "MissingValuePolicy", "BoundaryPolicy"];

    [Category("Filter")]
    [DisplayName("Method")]
    [Description("Filtering method. Recipe v1 supports Median only.")]
    [PropertyOrder(0)]
    public FilterMethod Method { get; set; }

    [Category("Filter")]
    [DisplayName("Kernel size")]
    [Description("Odd square neighborhood size. Supported values are 3, 5, and 7.")]
    [PropertyOrder(1)]
    [NumberRange(3, 7, 2)]
    public int KernelSize { get; set; }

    [Category("Filter")]
    [DisplayName("Missing values")]
    [Description("Keeps missing source cells missing.")]
    [PropertyOrder(2)]
    public FilterMissingValuePolicy MissingValuePolicy { get; set; }

    [Category("Filter")]
    [DisplayName("Boundary")]
    [Description("Uses only valid neighbors available inside the source boundary.")]
    [PropertyOrder(3)]
    public FilterBoundaryPolicy BoundaryPolicy { get; set; }

    [Category("Compatibility")]
    [DisplayName("Unmapped parameters")]
    [Description("Unknown parameters are retained unchanged when known parameters are applied.")]
    [PropertyOrder(10)]
    [ReadOnly(true)]
    public string UnmappedParameters { get; init; } = "(none)";

    internal static FilterStepProperties From(ToolWorkbenchPipelineStepItem step) => new()
    {
        Method = Enum.TryParse<FilterMethod>(ToolWorkbenchStepPropertySession.GetParameter(step, "Method"), out var method) ? method : FilterMethod.Median,
        KernelSize = int.TryParse(ToolWorkbenchStepPropertySession.GetParameter(step, "KernelSize"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var kernel) ? kernel : 0,
        MissingValuePolicy = Enum.TryParse<FilterMissingValuePolicy>(ToolWorkbenchStepPropertySession.GetParameter(step, "MissingValuePolicy"), out var missing) ? missing : FilterMissingValuePolicy.PreserveMask,
        BoundaryPolicy = Enum.TryParse<FilterBoundaryPolicy>(ToolWorkbenchStepPropertySession.GetParameter(step, "BoundaryPolicy"), out var boundary) ? boundary : FilterBoundaryPolicy.AvailableNeighbors,
        UnmappedParameters = ToolWorkbenchStepPropertySession.GetUnmappedParameters(step, MappedNames)
    };

    internal bool TryValidate(out string message)
    {
        if (KernelSize is not (3 or 5 or 7))
        {
            message = "Kernel size must be 3, 5, or 7.";
            return false;
        }

        message = string.Empty;
        return true;
    }
}

public enum RemoveOutlierPixelsRule
{
    LocalMedianAbsoluteDeviation
}

public enum RemoveOutlierPixelsMissingValuePolicy
{
    PreserveMask
}

public enum RemoveOutlierPixelsBoundaryPolicy
{
    AvailableNeighbors
}

public enum RemoveOutlierPixelsOutlierPolicy
{
    SetMissing
}

[CategoryOrder("Outlier rule", 0)]
[CategoryOrder("Evidence policy", 1)]
[CategoryOrder("Compatibility", 2)]
public sealed class RemoveOutlierPixelsStepProperties
{
    internal static readonly HashSet<string> MappedNames =
    [
        "Rule",
        "WindowSize",
        "MaximumAbsoluteDeviation",
        "MinimumValidNeighbors",
        "MissingValuePolicy",
        "BoundaryPolicy",
        "OutlierPolicy"
    ];

    [Category("Outlier rule")]
    [DisplayName("Rule")]
    [Description("Compares each finite center cell with the median of its finite neighbors.")]
    [PropertyOrder(0)]
    public RemoveOutlierPixelsRule Rule { get; set; }

    [Category("Outlier rule")]
    [DisplayName("Window size")]
    [Description("Odd square neighborhood. The center cell is excluded from the local median.")]
    [PropertyOrder(1)]
    [NumberRange(3, 7, 2)]
    public int WindowSize { get; set; }

    [Category("Outlier rule")]
    [DisplayName("Maximum absolute deviation")]
    [Description("A cell is removed only when |center - local median| is strictly greater than this raw-height threshold.")]
    [PropertyOrder(2)]
    [NumberRange(0.000001, double.MaxValue, 1)]
    public double MaximumAbsoluteDeviation { get; set; }

    [Category("Outlier rule")]
    [DisplayName("Minimum valid neighbors")]
    [Description("Leaves the center unchanged when fewer finite neighbors are available.")]
    [PropertyOrder(3)]
    [NumberRange(1, 48, 1)]
    public int MinimumValidNeighbors { get; set; }

    [Category("Evidence policy")]
    [DisplayName("Missing values")]
    [Description("Original missing cells remain missing and are not counted in the outlier mask.")]
    [PropertyOrder(4)]
    public RemoveOutlierPixelsMissingValuePolicy MissingValuePolicy { get; set; }

    [Category("Evidence policy")]
    [DisplayName("Boundary")]
    [Description("Uses finite neighbors available inside the source boundary without padding.")]
    [PropertyOrder(5)]
    public RemoveOutlierPixelsBoundaryPolicy BoundaryPolicy { get; set; }

    [Category("Evidence policy")]
    [DisplayName("Outlier action")]
    [Description("Removed outliers become missing cells in the separate derived output.")]
    [PropertyOrder(6)]
    public RemoveOutlierPixelsOutlierPolicy OutlierPolicy { get; set; }

    [Category("Compatibility")]
    [DisplayName("Unmapped parameters")]
    [Description("Unknown parameters are retained unchanged when known parameters are applied.")]
    [PropertyOrder(10)]
    [ReadOnly(true)]
    public string UnmappedParameters { get; init; } = "(none)";

    internal static RemoveOutlierPixelsStepProperties From(
        ToolWorkbenchPipelineStepItem step) => new()
    {
        Rule = Enum.TryParse<RemoveOutlierPixelsRule>(
            ToolWorkbenchStepPropertySession.GetParameter(step, "Rule"),
            out var rule)
            ? rule
            : RemoveOutlierPixelsRule.LocalMedianAbsoluteDeviation,
        WindowSize = int.TryParse(
            ToolWorkbenchStepPropertySession.GetParameter(step, "WindowSize"),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var windowSize)
            ? windowSize
            : 0,
        MaximumAbsoluteDeviation = double.TryParse(
            ToolWorkbenchStepPropertySession.GetParameter(
                step,
                "MaximumAbsoluteDeviation"),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var maximumDeviation)
            ? maximumDeviation
            : double.NaN,
        MinimumValidNeighbors = int.TryParse(
            ToolWorkbenchStepPropertySession.GetParameter(
                step,
                "MinimumValidNeighbors"),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var minimumNeighbors)
            ? minimumNeighbors
            : 0,
        MissingValuePolicy = Enum.TryParse<RemoveOutlierPixelsMissingValuePolicy>(
            ToolWorkbenchStepPropertySession.GetParameter(step, "MissingValuePolicy"),
            out var missing)
            ? missing
            : RemoveOutlierPixelsMissingValuePolicy.PreserveMask,
        BoundaryPolicy = Enum.TryParse<RemoveOutlierPixelsBoundaryPolicy>(
            ToolWorkbenchStepPropertySession.GetParameter(step, "BoundaryPolicy"),
            out var boundary)
            ? boundary
            : RemoveOutlierPixelsBoundaryPolicy.AvailableNeighbors,
        OutlierPolicy = Enum.TryParse<RemoveOutlierPixelsOutlierPolicy>(
            ToolWorkbenchStepPropertySession.GetParameter(step, "OutlierPolicy"),
            out var outlier)
            ? outlier
            : RemoveOutlierPixelsOutlierPolicy.SetMissing,
        UnmappedParameters =
            ToolWorkbenchStepPropertySession.GetUnmappedParameters(step, MappedNames)
    };

    internal bool TryValidate(out string message)
    {
        if (WindowSize is not (3 or 5 or 7))
        {
            message = "Window size must be 3, 5, or 7.";
            return false;
        }

        if (!double.IsFinite(MaximumAbsoluteDeviation)
            || MaximumAbsoluteDeviation <= 0d)
        {
            message = "Maximum absolute deviation must be finite and greater than zero.";
            return false;
        }

        var maximumNeighbors = checked(WindowSize * WindowSize - 1);
        if (MinimumValidNeighbors < 1
            || MinimumValidNeighbors > maximumNeighbors)
        {
            message =
                $"Minimum valid neighbors must be between 1 and {maximumNeighbors} for a {WindowSize} x {WindowSize} window.";
            return false;
        }

        message = string.Empty;
        return true;
    }
}

public enum LevelSurfaceReferenceFitPolicy
{
    LeastSquaresHeightPlane
}

public enum LevelSurfaceLevelingPolicy
{
    HeightDetrendToReferenceMean
}

public enum LevelSurfaceMissingValuePolicy
{
    PreserveMask
}

public enum LevelSurfaceGridPolicy
{
    PreserveSourceGrid
}

[CategoryOrder("Leveling", 0)]
[CategoryOrder("Preservation", 1)]
[CategoryOrder("Compatibility", 2)]
public sealed class LevelSurfaceStepProperties
{
    internal static readonly HashSet<string> MappedNames =
    [
        "ReferenceFitPolicy",
        "LevelingPolicy",
        "MissingValuePolicy",
        "GridPolicy",
        "MinimumValidSampleCount",
        "MaximumReferenceRmsResidual"
    ];

    [Category("Leveling")]
    [DisplayName("Reference fit")]
    [Description("Fits one least-squares raw-height plane across the unique finite cells of every authored reference ROI.")]
    [PropertyOrder(0)]
    public LevelSurfaceReferenceFitPolicy ReferenceFitPolicy { get; set; }

    [Category("Leveling")]
    [DisplayName("Leveling policy")]
    [Description("Removes the fitted X/Z height trend while preserving the mean reference height.")]
    [PropertyOrder(1)]
    public LevelSurfaceLevelingPolicy LevelingPolicy { get; set; }

    [Category("Leveling")]
    [DisplayName("Minimum valid samples")]
    [Description("Minimum unique finite samples required across all reference ROIs.")]
    [PropertyOrder(2)]
    [NumberRange(3, int.MaxValue, 1)]
    public int MinimumValidSampleCount { get; set; }

    [Category("Leveling")]
    [DisplayName("Maximum reference RMS")]
    [Description("Preview fails closed when the reference-plane vertical residual RMS exceeds this raw-height gate.")]
    [PropertyOrder(3)]
    [NumberRange(0.000001, double.MaxValue, 1)]
    public double MaximumReferenceRmsResidual { get; set; }

    [Category("Preservation")]
    [DisplayName("Missing values")]
    [Description("Source missing cells remain missing.")]
    [PropertyOrder(4)]
    public LevelSurfaceMissingValuePolicy MissingValuePolicy { get; set; }

    [Category("Preservation")]
    [DisplayName("Grid")]
    [Description("Preserves source row/column coordinates without interpolation or re-gridding.")]
    [PropertyOrder(5)]
    public LevelSurfaceGridPolicy GridPolicy { get; set; }

    [Category("Compatibility")]
    [DisplayName("Unmapped parameters")]
    [Description("Unknown parameters are retained unchanged when known parameters are applied.")]
    [PropertyOrder(10)]
    [ReadOnly(true)]
    public string UnmappedParameters { get; init; } = "(none)";

    internal static LevelSurfaceStepProperties From(
        ToolWorkbenchPipelineStepItem step) => new()
    {
        ReferenceFitPolicy = Enum.TryParse<LevelSurfaceReferenceFitPolicy>(
            ToolWorkbenchStepPropertySession.GetParameter(step, "ReferenceFitPolicy"),
            out var fit)
            ? fit
            : LevelSurfaceReferenceFitPolicy.LeastSquaresHeightPlane,
        LevelingPolicy = Enum.TryParse<LevelSurfaceLevelingPolicy>(
            ToolWorkbenchStepPropertySession.GetParameter(step, "LevelingPolicy"),
            out var leveling)
            ? leveling
            : LevelSurfaceLevelingPolicy.HeightDetrendToReferenceMean,
        MissingValuePolicy = Enum.TryParse<LevelSurfaceMissingValuePolicy>(
            ToolWorkbenchStepPropertySession.GetParameter(step, "MissingValuePolicy"),
            out var missing)
            ? missing
            : LevelSurfaceMissingValuePolicy.PreserveMask,
        GridPolicy = Enum.TryParse<LevelSurfaceGridPolicy>(
            ToolWorkbenchStepPropertySession.GetParameter(step, "GridPolicy"),
            out var grid)
            ? grid
            : LevelSurfaceGridPolicy.PreserveSourceGrid,
        MinimumValidSampleCount = int.TryParse(
            ToolWorkbenchStepPropertySession.GetParameter(step, "MinimumValidSampleCount"),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var minimum)
            ? minimum
            : 0,
        MaximumReferenceRmsResidual = double.TryParse(
            ToolWorkbenchStepPropertySession.GetParameter(step, "MaximumReferenceRmsResidual"),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var maximum)
            ? maximum
            : double.NaN,
        UnmappedParameters =
            ToolWorkbenchStepPropertySession.GetUnmappedParameters(step, MappedNames)
    };

    internal bool TryValidate(out string message)
    {
        if (MinimumValidSampleCount < 3)
        {
            message = "Minimum valid sample count must be at least three.";
            return false;
        }
        if (!double.IsFinite(MaximumReferenceRmsResidual)
            || MaximumReferenceRmsResidual <= 0)
        {
            message = "Maximum reference RMS must be finite and greater than zero.";
            return false;
        }
        message = string.Empty;
        return true;
    }
}

public enum HeightDifferenceEdgeComparisonAxis
{
    Unspecified,
    AcrossColumns,
    AcrossRows
}

public enum HeightDifferenceEdgePolarity
{
    Unspecified,
    Rising,
    Falling,
    Absolute
}

public enum HeightDifferenceEdgeCandidatePolicy
{
    StrongestPerScanline
}

public enum HeightDifferenceEdgePointPolicy
{
    PairMidpoint
}

public enum HeightDifferenceEdgeMissingValuePolicy
{
    SkipPair
}

public enum HeightDifferenceEdgeBoundaryPolicy
{
    WithinSelection
}

[CategoryOrder("Edge", 0)]
[CategoryOrder("Policies", 1)]
[CategoryOrder("Compatibility", 2)]
public sealed class HeightDifferenceEdgeStepProperties
{
    internal static readonly HashSet<string> MappedNames =
    [
        "ComparisonAxis", "Polarity", "MinimumDelta", "CandidatePolicy",
        "PointPolicy", "MissingValuePolicy", "BoundaryPolicy"
    ];

    [Category("Edge")]
    [DisplayName("Comparison axis")]
    [Description("Adjacent-height comparison direction in the source grid.")]
    [PropertyOrder(0)]
    public HeightDifferenceEdgeComparisonAxis ComparisonAxis { get; set; }

    [Category("Edge")]
    [DisplayName("Polarity")]
    [Description("Accepted sign of the adjacent raw-height difference.")]
    [PropertyOrder(1)]
    public HeightDifferenceEdgePolarity Polarity { get; set; }

    [Category("Edge")]
    [DisplayName("Minimum delta")]
    [Description("Finite raw-height difference threshold; must be greater than zero.")]
    [PropertyOrder(2)]
    [NumberRange(0, 1000000, 1, 3)]
    public double MinimumDelta { get; set; }

    [Category("Policies")]
    [DisplayName("Candidate")]
    [Description("Selects the strongest accepted pair in each scanline.")]
    [PropertyOrder(3)]
    public HeightDifferenceEdgeCandidatePolicy CandidatePolicy { get; set; }

    [Category("Policies")]
    [DisplayName("Point position")]
    [Description("Places the edge point at the adjacent pair midpoint.")]
    [PropertyOrder(4)]
    public HeightDifferenceEdgePointPolicy PointPolicy { get; set; }

    [Category("Policies")]
    [DisplayName("Missing values")]
    [Description("Skips adjacent pairs containing a missing sample.")]
    [PropertyOrder(5)]
    public HeightDifferenceEdgeMissingValuePolicy MissingValuePolicy { get; set; }

    [Category("Policies")]
    [DisplayName("Boundary")]
    [Description("Searches only within the recipe-owned GridRectangle.")]
    [PropertyOrder(6)]
    public HeightDifferenceEdgeBoundaryPolicy BoundaryPolicy { get; set; }

    [Category("Compatibility")]
    [DisplayName("Unmapped parameters")]
    [Description("Unknown parameters are retained unchanged when known parameters are applied.")]
    [PropertyOrder(10)]
    [ReadOnly(true)]
    public string UnmappedParameters { get; init; } = "(none)";

    internal static HeightDifferenceEdgeStepProperties From(ToolWorkbenchPipelineStepItem step) => new()
    {
        ComparisonAxis = Enum.TryParse<HeightDifferenceEdgeComparisonAxis>(ToolWorkbenchStepPropertySession.GetParameter(step, "ComparisonAxis"), out var axis)
            ? axis
            : HeightDifferenceEdgeComparisonAxis.Unspecified,
        Polarity = Enum.TryParse<HeightDifferenceEdgePolarity>(ToolWorkbenchStepPropertySession.GetParameter(step, "Polarity"), out var polarity)
            ? polarity
            : HeightDifferenceEdgePolarity.Unspecified,
        MinimumDelta = double.TryParse(ToolWorkbenchStepPropertySession.GetParameter(step, "MinimumDelta"), NumberStyles.Float, CultureInfo.InvariantCulture, out var delta)
            ? delta
            : 0,
        CandidatePolicy = Enum.TryParse<HeightDifferenceEdgeCandidatePolicy>(ToolWorkbenchStepPropertySession.GetParameter(step, "CandidatePolicy"), out var candidate)
            ? candidate
            : HeightDifferenceEdgeCandidatePolicy.StrongestPerScanline,
        PointPolicy = Enum.TryParse<HeightDifferenceEdgePointPolicy>(ToolWorkbenchStepPropertySession.GetParameter(step, "PointPolicy"), out var point)
            ? point
            : HeightDifferenceEdgePointPolicy.PairMidpoint,
        MissingValuePolicy = Enum.TryParse<HeightDifferenceEdgeMissingValuePolicy>(ToolWorkbenchStepPropertySession.GetParameter(step, "MissingValuePolicy"), out var missing)
            ? missing
            : HeightDifferenceEdgeMissingValuePolicy.SkipPair,
        BoundaryPolicy = Enum.TryParse<HeightDifferenceEdgeBoundaryPolicy>(ToolWorkbenchStepPropertySession.GetParameter(step, "BoundaryPolicy"), out var boundary)
            ? boundary
            : HeightDifferenceEdgeBoundaryPolicy.WithinSelection,
        UnmappedParameters = ToolWorkbenchStepPropertySession.GetUnmappedParameters(step, MappedNames)
    };

    internal bool TryValidate(out string message)
    {
        if (ComparisonAxis == HeightDifferenceEdgeComparisonAxis.Unspecified)
        {
            message = "Select AcrossColumns or AcrossRows.";
            return false;
        }

        if (Polarity == HeightDifferenceEdgePolarity.Unspecified)
        {
            message = "Select Rising, Falling, or Absolute polarity.";
            return false;
        }

        if (!double.IsFinite(MinimumDelta) || MinimumDelta <= 0)
        {
            message = "Minimum delta must be finite and greater than zero.";
            return false;
        }

        message = string.Empty;
        return true;
    }
}

public enum TwoPointLineConstructionPolicy
{
    OrderedPointsDefineSegment
}

[CategoryOrder("Construction", 0)]
[CategoryOrder("Fixed v1 policy", 1)]
[CategoryOrder("Compatibility", 2)]
public sealed class TwoPointLineStepProperties
{
    internal static readonly HashSet<string> MappedNames = ["OutputRole", "ConstructionPolicy"];

    [Category("Construction")]
    [DisplayName("Output role")]
    [Description("A unique operator-facing role for this ordered two-point line output.")]
    [PropertyOrder(0)]
    public string OutputRole { get; set; } = string.Empty;

    [Category("Fixed v1 policy")]
    [DisplayName("Construction policy")]
    [Description("The first authored pick is the segment start and the second is the segment end. No fitting, snapping, or interpolation occurs.")]
    [PropertyOrder(1)]
    [ReadOnly(true)]
    public TwoPointLineConstructionPolicy ConstructionPolicy { get; set; } = TwoPointLineConstructionPolicy.OrderedPointsDefineSegment;

    [Category("Compatibility")]
    [DisplayName("Unmapped parameters")]
    [Description("Unknown parameters are preserved unchanged when known parameters are applied.")]
    [PropertyOrder(2)]
    [ReadOnly(true)]
    public string UnmappedParameters { get; init; } = "(none)";

    internal static TwoPointLineStepProperties From(ToolWorkbenchPipelineStepItem step) => new()
    {
        OutputRole = ToolWorkbenchStepPropertySession.GetParameter(step, "OutputRole") ?? string.Empty,
        ConstructionPolicy = TwoPointLineConstructionPolicy.OrderedPointsDefineSegment,
        UnmappedParameters = ToolWorkbenchStepPropertySession.GetUnmappedParameters(step, MappedNames)
    };

    internal bool TryValidate(out string message)
    {
        if (string.IsNullOrWhiteSpace(OutputRole) || OutputRole != OutputRole.Trim())
        {
            message = "Output role must be an explicit non-empty identifier without surrounding whitespace.";
            return false;
        }
        message = string.Empty;
        return true;
    }
}

public enum ThreePointPlaneConstructionPolicy
{
    OrderedPointsDefineOrientedPlane
}

[CategoryOrder("Construction", 0)]
[CategoryOrder("Fixed v1 policy", 1)]
[CategoryOrder("Compatibility", 2)]
public sealed class ThreePointPlaneStepProperties
{
    internal static readonly HashSet<string> MappedNames = ["OutputRole", "ConstructionPolicy"];

    [Category("Construction")]
    [DisplayName("Output role")]
    [Description("A unique operator-facing role for this ordered three-point datum-plane output.")]
    [PropertyOrder(0)]
    public string OutputRole { get; set; } = string.Empty;

    [Category("Fixed v1 policy")]
    [DisplayName("Construction policy")]
    [Description("P1 -> P2 -> P3 fixes the oriented normal by the right-hand rule. No region fit, snapping, interpolation, or acceptance evaluation occurs.")]
    [PropertyOrder(1)]
    [ReadOnly(true)]
    public ThreePointPlaneConstructionPolicy ConstructionPolicy { get; set; } = ThreePointPlaneConstructionPolicy.OrderedPointsDefineOrientedPlane;

    [Category("Compatibility")]
    [DisplayName("Unmapped parameters")]
    [Description("Unknown parameters are preserved unchanged when known parameters are applied.")]
    [PropertyOrder(2)]
    [ReadOnly(true)]
    public string UnmappedParameters { get; init; } = "(none)";

    internal static ThreePointPlaneStepProperties From(ToolWorkbenchPipelineStepItem step) => new()
    {
        OutputRole = ToolWorkbenchStepPropertySession.GetParameter(step, "OutputRole") ?? string.Empty,
        ConstructionPolicy = ThreePointPlaneConstructionPolicy.OrderedPointsDefineOrientedPlane,
        UnmappedParameters = ToolWorkbenchStepPropertySession.GetUnmappedParameters(step, MappedNames)
    };

    internal bool TryValidate(out string message)
    {
        if (string.IsNullOrWhiteSpace(OutputRole) || OutputRole != OutputRole.Trim())
        {
            message = "Output role must be an explicit non-empty identifier without surrounding whitespace.";
            return false;
        }
        message = string.Empty;
        return true;
    }
}

public enum DatumPlaneDeviationResidualPolicy
{
    RawHeightMinusDatumPlanePredictedRawHeight
}

[CategoryOrder("Deviation rule", 0)]
[CategoryOrder("Fixed v1 policy", 1)]
[CategoryOrder("Compatibility", 2)]
public sealed class DatumPlaneDeviationStepProperties
{
    internal static readonly HashSet<string> MappedNames =
    [
        "MaximumPeakToValleyRawHeight", "OutputRole", "ResidualPolicy",
        "MinimumValidSampleCount", "MinimumAbsoluteNormalY"
    ];

    [Category("Deviation rule")]
    [DisplayName("Maximum P2V raw height")]
    [Description("Inclusive peak-to-valley limit of raw-height residuals from the Published 3-Point Plane. This is not a calibrated physical limit.")]
    [PropertyOrder(0)]
    [NumberRange(0, 1000000, 0.001, 6)]
    public double MaximumPeakToValleyRawHeight { get; set; }

    [Category("Deviation rule")]
    [DisplayName("Output role")]
    [Description("Named semantic role for the read-only datum-plane residual result.")]
    [PropertyOrder(1)]
    public string OutputRole { get; set; } = string.Empty;

    [Category("Fixed v1 policy")]
    [DisplayName("Residual policy")]
    [Description("Residual is current raw height minus datum-plane predicted raw height at the same grid cell.")]
    [PropertyOrder(2)]
    [ReadOnly(true)]
    public DatumPlaneDeviationResidualPolicy ResidualPolicy { get; set; }

    [Category("Fixed v1 policy")]
    [DisplayName("Minimum valid samples")]
    [Description("At least this many finite C3D cells must be present in the recipe-owned measurement rectangle.")]
    [PropertyOrder(3)]
    [NumberRange(3, 1000000, 1)]
    public int MinimumValidSampleCount { get; set; }

    [Category("Fixed v1 policy")]
    [DisplayName("Minimum |normal Y|")]
    [Description("Rejects a near-vertical datum plane because raw height cannot be solved safely from its plane equation.")]
    [PropertyOrder(4)]
    [NumberRange(0, 1, 0.01, 6)]
    public double MinimumAbsoluteNormalY { get; set; }

    [Category("Compatibility")]
    [DisplayName("Unmapped parameters")]
    [PropertyOrder(5)]
    [ReadOnly(true)]
    public string UnmappedParameters { get; init; } = "(none)";

    internal static DatumPlaneDeviationStepProperties From(ToolWorkbenchPipelineStepItem step) => new()
    {
        MaximumPeakToValleyRawHeight = double.TryParse(ToolWorkbenchStepPropertySession.GetParameter(step, "MaximumPeakToValleyRawHeight"), NumberStyles.Float, CultureInfo.InvariantCulture, out var p2v) ? p2v : 0d,
        OutputRole = ToolWorkbenchStepPropertySession.GetParameter(step, "OutputRole") ?? string.Empty,
        ResidualPolicy = DatumPlaneDeviationResidualPolicy.RawHeightMinusDatumPlanePredictedRawHeight,
        MinimumValidSampleCount = int.TryParse(ToolWorkbenchStepPropertySession.GetParameter(step, "MinimumValidSampleCount"), NumberStyles.None, CultureInfo.InvariantCulture, out var minimum) ? minimum : 0,
        MinimumAbsoluteNormalY = double.TryParse(ToolWorkbenchStepPropertySession.GetParameter(step, "MinimumAbsoluteNormalY"), NumberStyles.Float, CultureInfo.InvariantCulture, out var normalY) ? normalY : 0d,
        UnmappedParameters = ToolWorkbenchStepPropertySession.GetUnmappedParameters(step, MappedNames)
    };

    internal bool TryValidate(out string message)
    {
        if (!double.IsFinite(MaximumPeakToValleyRawHeight) || MaximumPeakToValleyRawHeight <= 0d)
        {
            message = "Maximum P2V raw height must be finite and greater than zero.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(OutputRole) || OutputRole != OutputRole.Trim())
        {
            message = "Output role is required without surrounding whitespace.";
            return false;
        }
        if (MinimumValidSampleCount < 3)
        {
            message = "Minimum valid samples must be at least three.";
            return false;
        }
        if (!double.IsFinite(MinimumAbsoluteNormalY) || MinimumAbsoluteNormalY <= 0d || MinimumAbsoluteNormalY > 1d)
        {
            message = "Minimum |normal Y| must be finite, greater than zero, and no greater than one.";
            return false;
        }
        message = string.Empty;
        return true;
    }
}

public enum LineFitMethod
{
    DeterministicConsensusOrthogonalTls
}

public enum LineFitHypothesisPolicy
{
    Sha256PairSchedule
}

public enum LineFitRefinementPolicy
{
    OrthogonalTlsUntilStable10
}

public enum LineFitDirectionPolicy
{
    PositiveScanlineAxis
}

public enum LineFitEndpointPolicy
{
    InlierProjectionExtents
}

[CategoryOrder("Fit rule", 0)]
[CategoryOrder("Fixed v1 policy", 1)]
[CategoryOrder("Compatibility", 2)]
public sealed class LineFitStepProperties
{
    internal static readonly HashSet<string> MappedNames =
    [
        "FitMethod", "MaximumOrthogonalResidual", "MinimumInlierCount", "MinimumInlierRatio", "MinimumInlierScanlineSpan",
        "HypothesisPolicy", "MaximumHypotheses", "RefinementPolicy", "DirectionPolicy", "EndpointPolicy"
    ];

    [Category("Fit rule")]
    [DisplayName("Method")]
    [Description("Deterministic full-XYZ consensus followed by orthogonal TLS.")]
    [PropertyOrder(0)]
    [ReadOnly(true)]
    public LineFitMethod FitMethod { get; set; }

    [Category("Fit rule")]
    [DisplayName("Maximum residual")]
    [Description("Inclusive full-XYZ orthogonal residual in uncalibrated source coordinates.")]
    [PropertyOrder(1)]
    [NumberRange(0, 1000000, 1, 6)]
    public double MaximumOrthogonalResidual { get; set; }

    [Category("Fit rule")]
    [DisplayName("Minimum inliers")]
    [Description("At least three supporting EdgePointSet points are required.")]
    [PropertyOrder(2)]
    [NumberRange(0, 1000000, 1)]
    public int MinimumInlierCount { get; set; }

    [Category("Fit rule")]
    [DisplayName("Minimum ratio")]
    [Description("Required inlier ratio from greater than zero through one.")]
    [PropertyOrder(3)]
    [NumberRange(0, 1, 0.01, 4)]
    public double MinimumInlierRatio { get; set; }

    [Category("Fit rule")]
    [DisplayName("Minimum support span")]
    [Description("Minimum inlier scanline span in source grid-index intervals; at least two.")]
    [PropertyOrder(4)]
    [NumberRange(0, 1000000, 1)]
    public int MinimumInlierScanlineSpan { get; set; }

    [Category("Fixed v1 policy")]
    [DisplayName("Hypotheses")]
    [Description("All pairs through 256 candidates; SHA-256-derived unique pairs above that count.")]
    [PropertyOrder(5)]
    [ReadOnly(true)]
    public LineFitHypothesisPolicy HypothesisPolicy { get; set; }

    [Category("Fixed v1 policy")]
    [DisplayName("Maximum hypotheses")]
    [Description("Fixed deterministic v1 candidate limit.")]
    [PropertyOrder(6)]
    [ReadOnly(true)]
    public int MaximumHypotheses { get; set; } = 256;

    [Category("Fixed v1 policy")]
    [DisplayName("Refinement")]
    [Description("Refit and reclassify until membership is stable, at most ten iterations.")]
    [PropertyOrder(7)]
    [ReadOnly(true)]
    public LineFitRefinementPolicy RefinementPolicy { get; set; }

    [Category("Fixed v1 policy")]
    [DisplayName("Direction")]
    [Description("Canonical positive source scanline axis: +Z AcrossColumns, +X AcrossRows.")]
    [PropertyOrder(8)]
    [ReadOnly(true)]
    public LineFitDirectionPolicy DirectionPolicy { get; set; }

    [Category("Fixed v1 policy")]
    [DisplayName("Segment")]
    [Description("Displays only final inlier projection extents, never an infinite line.")]
    [PropertyOrder(9)]
    [ReadOnly(true)]
    public LineFitEndpointPolicy EndpointPolicy { get; set; }

    [Category("Compatibility")]
    [DisplayName("Unmapped parameters")]
    [Description("Unknown parameters are preserved unchanged when known parameters are applied.")]
    [PropertyOrder(10)]
    [ReadOnly(true)]
    public string UnmappedParameters { get; init; } = "(none)";

    internal static LineFitStepProperties From(ToolWorkbenchPipelineStepItem step) => new()
    {
        FitMethod = LineFitMethod.DeterministicConsensusOrthogonalTls,
        MaximumOrthogonalResidual = double.TryParse(ToolWorkbenchStepPropertySession.GetParameter(step, "MaximumOrthogonalResidual"), NumberStyles.Float, CultureInfo.InvariantCulture, out var residual) ? residual : 0,
        MinimumInlierCount = int.TryParse(ToolWorkbenchStepPropertySession.GetParameter(step, "MinimumInlierCount"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : 0,
        MinimumInlierRatio = double.TryParse(ToolWorkbenchStepPropertySession.GetParameter(step, "MinimumInlierRatio"), NumberStyles.Float, CultureInfo.InvariantCulture, out var ratio) ? ratio : 0,
        MinimumInlierScanlineSpan = int.TryParse(ToolWorkbenchStepPropertySession.GetParameter(step, "MinimumInlierScanlineSpan"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var span) ? span : 0,
        HypothesisPolicy = LineFitHypothesisPolicy.Sha256PairSchedule,
        MaximumHypotheses = 256,
        RefinementPolicy = LineFitRefinementPolicy.OrthogonalTlsUntilStable10,
        DirectionPolicy = LineFitDirectionPolicy.PositiveScanlineAxis,
        EndpointPolicy = LineFitEndpointPolicy.InlierProjectionExtents,
        UnmappedParameters = ToolWorkbenchStepPropertySession.GetUnmappedParameters(step, MappedNames)
    };

    internal bool TryValidate(out string message)
    {
        if (!double.IsFinite(MaximumOrthogonalResidual) || MaximumOrthogonalResidual <= 0)
        {
            message = "Maximum residual must be finite and greater than zero.";
            return false;
        }
        if (MinimumInlierCount < 3)
        {
            message = "Minimum inliers must be at least three.";
            return false;
        }
        if (!double.IsFinite(MinimumInlierRatio) || MinimumInlierRatio <= 0 || MinimumInlierRatio > 1)
        {
            message = "Minimum ratio must be greater than zero and no greater than one.";
            return false;
        }
        if (MinimumInlierScanlineSpan < 2)
        {
            message = "Minimum support span must be at least two grid-index intervals.";
            return false;
        }
        message = string.Empty;
        return true;
    }
}

public enum LineIntersectionClosestApproachPolicy
{
    MidpointOfClosestPoints
}

public enum LineIntersectionParallelPolicy
{
    RejectBelowMinimumAcuteAngle
}

public enum LineIntersectionSupportPolicy
{
    WithinInlierProjectionExtentsWithMaximumExtension
}

[CategoryOrder("Corner rule", 0)]
[CategoryOrder("Fixed v1 policy", 1)]
[CategoryOrder("Compatibility", 2)]
public sealed class LineIntersectionStepProperties
{
    internal static readonly HashSet<string> MappedNames =
    [
        "MaximumClosestApproachDistance", "MinimumAcuteAngleDegrees", "MaximumSupportExtension",
        "OutputRole", "ClosestApproachPolicy", "ParallelPolicy", "SupportPolicy"
    ];

    [Category("Corner rule")]
    [DisplayName("Maximum closest gap")]
    [Description("Inclusive full-XYZ closest-approach gap in uncalibrated source coordinates.")]
    [PropertyOrder(0)]
    [NumberRange(0, 1000000, 1, 6)]
    public double MaximumClosestApproachDistance { get; set; }

    [Category("Corner rule")]
    [DisplayName("Minimum acute angle")]
    [Description("Minimum included acute angle in degrees. Near-parallel lines are rejected.")]
    [PropertyOrder(1)]
    [NumberRange(0, 90, 1, 6)]
    public double MinimumAcuteAngleDegrees { get; set; }

    [Category("Corner rule")]
    [DisplayName("Maximum support extension")]
    [Description("Allowed source-coordinate extrapolation beyond each fitted inlier segment; zero forbids extension.")]
    [PropertyOrder(2)]
    [NumberRange(0, 1000000, 1, 6)]
    public double MaximumSupportExtension { get; set; }

    [Category("Corner rule")]
    [DisplayName("Output role")]
    [Description("Named semantic corner role, for example UpperLeftCorner. It does not change geometry.")]
    [PropertyOrder(3)]
    public string OutputRole { get; set; } = string.Empty;

    [Category("Fixed v1 policy")]
    [DisplayName("Closest approach")]
    [PropertyOrder(4)]
    [ReadOnly(true)]
    public LineIntersectionClosestApproachPolicy ClosestApproachPolicy { get; set; }

    [Category("Fixed v1 policy")]
    [DisplayName("Parallel handling")]
    [PropertyOrder(5)]
    [ReadOnly(true)]
    public LineIntersectionParallelPolicy ParallelPolicy { get; set; }

    [Category("Fixed v1 policy")]
    [DisplayName("Support handling")]
    [PropertyOrder(6)]
    [ReadOnly(true)]
    public LineIntersectionSupportPolicy SupportPolicy { get; set; }

    [Category("Compatibility")]
    [DisplayName("Unmapped parameters")]
    [PropertyOrder(7)]
    [ReadOnly(true)]
    public string UnmappedParameters { get; init; } = "(none)";

    internal static LineIntersectionStepProperties From(ToolWorkbenchPipelineStepItem step) => new()
    {
        MaximumClosestApproachDistance = double.TryParse(ToolWorkbenchStepPropertySession.GetParameter(step, "MaximumClosestApproachDistance"), NumberStyles.Float, CultureInfo.InvariantCulture, out var gap) ? gap : 0,
        MinimumAcuteAngleDegrees = double.TryParse(ToolWorkbenchStepPropertySession.GetParameter(step, "MinimumAcuteAngleDegrees"), NumberStyles.Float, CultureInfo.InvariantCulture, out var angle) ? angle : 0,
        MaximumSupportExtension = double.TryParse(ToolWorkbenchStepPropertySession.GetParameter(step, "MaximumSupportExtension"), NumberStyles.Float, CultureInfo.InvariantCulture, out var extension) ? extension : 0,
        OutputRole = ToolWorkbenchStepPropertySession.GetParameter(step, "OutputRole") ?? string.Empty,
        ClosestApproachPolicy = LineIntersectionClosestApproachPolicy.MidpointOfClosestPoints,
        ParallelPolicy = LineIntersectionParallelPolicy.RejectBelowMinimumAcuteAngle,
        SupportPolicy = LineIntersectionSupportPolicy.WithinInlierProjectionExtentsWithMaximumExtension,
        UnmappedParameters = ToolWorkbenchStepPropertySession.GetUnmappedParameters(step, MappedNames)
    };

    internal bool TryValidate(out string message)
    {
        if (!double.IsFinite(MaximumClosestApproachDistance) || MaximumClosestApproachDistance <= 0)
        {
            message = "Maximum closest gap must be finite and greater than zero.";
            return false;
        }
        if (!double.IsFinite(MinimumAcuteAngleDegrees) || MinimumAcuteAngleDegrees <= 0 || MinimumAcuteAngleDegrees > 90)
        {
            message = "Minimum acute angle must be finite, greater than zero, and no greater than 90 degrees.";
            return false;
        }
        if (!double.IsFinite(MaximumSupportExtension) || MaximumSupportExtension < 0)
        {
            message = "Maximum support extension must be finite and no less than zero.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(OutputRole) || OutputRole != OutputRole.Trim())
        {
            message = "Output role is required without surrounding whitespace.";
            return false;
        }
        message = string.Empty;
        return true;
    }
}

[CategoryOrder("Correspondence policy", 0)]
[CategoryOrder("Compatibility", 1)]
public sealed class LandmarkCorrespondenceStepProperties
{
    internal static readonly HashSet<string> MappedNames =
    [
        "PairCountPolicy", "SourceArtifactPolicy", "AffineIndependencePolicy"
    ];

    [Category("Correspondence policy")]
    [DisplayName("Pair count")]
    [Description("Landmark Correspondence v1 accepts exactly four authored pairs.")]
    [PropertyOrder(0)]
    [ReadOnly(true)]
    public string PairCountPolicy { get; init; } = "ExactlyFour";

    [Category("Correspondence policy")]
    [DisplayName("Source artifact")]
    [Description("Only exact current Published CornerAnchor outputs are valid inputs.")]
    [PropertyOrder(1)]
    [ReadOnly(true)]
    public string SourceArtifactPolicy { get; init; } = "CurrentPublishedCornerAnchor";

    [Category("Correspondence policy")]
    [DisplayName("Affine independence")]
    [Description("Both source and reference landmarks must form non-degenerate tetrahedra. This tool does not calculate an affine matrix.")]
    [PropertyOrder(2)]
    [ReadOnly(true)]
    public string AffineIndependencePolicy { get; init; } = "RequireNonDegenerateTetrahedra";

    [Category("Compatibility")]
    [DisplayName("Unmapped parameters")]
    [PropertyOrder(3)]
    [ReadOnly(true)]
    public string UnmappedParameters { get; init; } = "(none)";

    internal static LandmarkCorrespondenceStepProperties From(ToolWorkbenchPipelineStepItem step) => new()
    {
        PairCountPolicy = ToolWorkbenchStepPropertySession.GetParameter(step, "PairCountPolicy") ?? "ExactlyFour",
        SourceArtifactPolicy = ToolWorkbenchStepPropertySession.GetParameter(step, "SourceArtifactPolicy") ?? "CurrentPublishedCornerAnchor",
        AffineIndependencePolicy = ToolWorkbenchStepPropertySession.GetParameter(step, "AffineIndependencePolicy") ?? "RequireNonDegenerateTetrahedra",
        UnmappedParameters = ToolWorkbenchStepPropertySession.GetUnmappedParameters(step, MappedNames)
    };

    internal bool TryValidate(out string message)
    {
        if (!string.Equals(PairCountPolicy, "ExactlyFour", StringComparison.Ordinal)
            || !string.Equals(SourceArtifactPolicy, "CurrentPublishedCornerAnchor", StringComparison.Ordinal)
            || !string.Equals(AffineIndependencePolicy, "RequireNonDegenerateTetrahedra", StringComparison.Ordinal))
        {
            message = "Landmark Correspondence v1 fixed policies do not match the approved contract.";
            return false;
        }

        message = string.Empty;
        return true;
    }
}

[CategoryOrder("Solve policy", 0)]
[CategoryOrder("Numerical review", 1)]
[CategoryOrder("Compatibility", 2)]
public sealed class XYZAffineSolveStepProperties
{
    internal static readonly HashSet<string> MappedNames =
    [
        "SolvePolicy", "MaximumConditionEstimate", "ArithmeticResidualWarning"
    ];

    [Category("Solve policy")]
    [DisplayName("Solve policy")]
    [Description("A1 uses exactly four published affine-independent pairs with scaled partial pivoting. Least squares and automatic matching are excluded.")]
    [PropertyOrder(0)]
    [ReadOnly(true)]
    public string SolvePolicy { get; init; } = "ExactFourPartialPivot";

    [Category("Numerical review")]
    [DisplayName("Maximum condition estimate")]
    [Description("Reject the source augmented matrix when its infinity-norm condition estimate exceeds this explicit finite limit.")]
    [PropertyOrder(1)]
    [NumberRange(1, 1000000000000, 1, 6)]
    public double MaximumConditionEstimate { get; set; } = 1000000;

    [Category("Numerical review")]
    [DisplayName("Arithmetic residual warning")]
    [Description("Residual review threshold in reference-coordinate units. Exceeding it remains solve evidence, not an inspection OK/NG result.")]
    [PropertyOrder(2)]
    [NumberRange(0, 1000000, 0.000001, 9)]
    public double ArithmeticResidualWarning { get; set; } = 0.001;

    [Category("Compatibility")]
    [DisplayName("Unmapped parameters")]
    [PropertyOrder(3)]
    [ReadOnly(true)]
    public string UnmappedParameters { get; init; } = "(none)";

    internal static XYZAffineSolveStepProperties From(ToolWorkbenchPipelineStepItem step) => new()
    {
        SolvePolicy = ToolWorkbenchStepPropertySession.GetParameter(step, "SolvePolicy") ?? "ExactFourPartialPivot",
        MaximumConditionEstimate = double.TryParse(ToolWorkbenchStepPropertySession.GetParameter(step, "MaximumConditionEstimate"), NumberStyles.Float, CultureInfo.InvariantCulture, out var maximum) ? maximum : 1000000,
        ArithmeticResidualWarning = double.TryParse(ToolWorkbenchStepPropertySession.GetParameter(step, "ArithmeticResidualWarning"), NumberStyles.Float, CultureInfo.InvariantCulture, out var warning) ? warning : 0.001,
        UnmappedParameters = ToolWorkbenchStepPropertySession.GetUnmappedParameters(step, MappedNames)
    };

    internal bool TryValidate(out string message)
    {
        if (!string.Equals(SolvePolicy, "ExactFourPartialPivot", StringComparison.Ordinal))
        {
            message = "XYZ Affine Solve v1 requires SolvePolicy ExactFourPartialPivot.";
            return false;
        }
        if (!double.IsFinite(MaximumConditionEstimate) || MaximumConditionEstimate <= 0)
        {
            message = "Maximum condition estimate must be a finite positive number.";
            return false;
        }
        if (!double.IsFinite(ArithmeticResidualWarning) || ArithmeticResidualWarning < 0)
        {
            message = "Arithmetic residual warning must be a finite non-negative number.";
            return false;
        }
        message = string.Empty;
        return true;
    }
}

[CategoryOrder("A2 contract", 0)]
public sealed class XYZAffineApplyStepProperties
{
    internal static readonly HashSet<string> MappedNames = [];

    [Category("A2 contract")]
    [DisplayName("Execution policy")]
    [Description("Apply XYZ Affine v1 has no authored numerical parameters. It verifies the recipe-bound raw C3D and current Published AffineTransform3D, then transforms each finite point once.")]
    [PropertyOrder(0)]
    [ReadOnly(true)]
    public string ExecutionPolicy { get; init; } = "Raw source + Published A1";

    [Category("A2 contract")]
    [DisplayName("Excluded operations")]
    [PropertyOrder(1)]
    [ReadOnly(true)]
    public string ExcludedOperations { get; init; } = "No re-grid / no measurement";

    internal static XYZAffineApplyStepProperties From(ToolWorkbenchPipelineStepItem step) => new();
}

[CategoryOrder("A3 reference grid", 0)]
[CategoryOrder("A3 publish policy", 1)]
public sealed class RegridHeightMapStepProperties
{
    internal static readonly HashSet<string> MappedNames =
    [
        "ReferenceFrameId", "ReferenceUnit", "ReferenceProvenance", "ReferenceRevision",
        "OriginX", "OriginY", "OriginZ", "UAxisX", "UAxisY", "UAxisZ", "VAxisX", "VAxisY", "VAxisZ", "HAxisX", "HAxisY", "HAxisZ",
        "PitchU", "PitchV", "RowCount", "ColumnCount", "MinimumCoverageRatio",
        "CellAssignment", "CollisionPolicy", "OutOfBoundsPolicy", "HolePolicy"
    ];

    [Category("A3 reference grid")] [DisplayName("Reference frame ID")] [PropertyOrder(0)] public string ReferenceFrameId { get; set; } = "frame.reference";
    [Category("A3 reference grid")] [DisplayName("Reference unit")] [PropertyOrder(1)] public string ReferenceUnit { get; set; } = "unitless";
    [Category("A3 reference grid")] [DisplayName("Reference provenance")] [PropertyOrder(2)] public string ReferenceProvenance { get; set; } = "Authored reference grid";
    [Category("A3 reference grid")] [DisplayName("Reference revision")] [PropertyOrder(3)] public string ReferenceRevision { get; set; } = "R1";
    [Category("A3 reference grid")] [DisplayName("Origin X")] [PropertyOrder(4)] public double OriginX { get; set; }
    [Category("A3 reference grid")] [DisplayName("Origin Y")] [PropertyOrder(5)] public double OriginY { get; set; }
    [Category("A3 reference grid")] [DisplayName("Origin Z")] [PropertyOrder(6)] public double OriginZ { get; set; }
    [Category("A3 reference grid")] [DisplayName("U axis X")] [PropertyOrder(7)] public double UAxisX { get; set; } = 1;
    [Category("A3 reference grid")] [DisplayName("U axis Y")] [PropertyOrder(8)] public double UAxisY { get; set; }
    [Category("A3 reference grid")] [DisplayName("U axis Z")] [PropertyOrder(9)] public double UAxisZ { get; set; }
    [Category("A3 reference grid")] [DisplayName("V axis X")] [PropertyOrder(10)] public double VAxisX { get; set; }
    [Category("A3 reference grid")] [DisplayName("V axis Y")] [PropertyOrder(11)] public double VAxisY { get; set; } = 1;
    [Category("A3 reference grid")] [DisplayName("V axis Z")] [PropertyOrder(12)] public double VAxisZ { get; set; }
    [Category("A3 reference grid")] [DisplayName("Height axis X")] [PropertyOrder(13)] public double HAxisX { get; set; }
    [Category("A3 reference grid")] [DisplayName("Height axis Y")] [PropertyOrder(14)] public double HAxisY { get; set; }
    [Category("A3 reference grid")] [DisplayName("Height axis Z")] [PropertyOrder(15)] public double HAxisZ { get; set; } = 1;
    [Category("A3 reference grid")] [DisplayName("U pitch")] [Description("Positive reference-frame spacing along U.")] [PropertyOrder(16)] public double PitchU { get; set; } = 1;
    [Category("A3 reference grid")] [DisplayName("V pitch")] [Description("Positive reference-frame spacing along V.")] [PropertyOrder(17)] public double PitchV { get; set; } = 1;
    [Category("A3 reference grid")] [DisplayName("Rows")] [PropertyOrder(18)] public int RowCount { get; set; } = 1;
    [Category("A3 reference grid")] [DisplayName("Columns")] [PropertyOrder(19)] public int ColumnCount { get; set; } = 1;
    [Category("A3 publish policy")] [DisplayName("Minimum coverage ratio")] [Description("Preview may complete below this ratio, but Publish remains disabled.")] [PropertyOrder(20)] [NumberRange(0, 1, 0.01)] public double MinimumCoverageRatio { get; set; } = 1;
    [Category("A3 publish policy")] [DisplayName("Cell assignment")] [PropertyOrder(21)] [ReadOnly(true)] public string CellAssignment { get; } = C3DReferenceGridProfile.CellAssignment;
    [Category("A3 publish policy")] [DisplayName("Collision policy")] [PropertyOrder(22)] [ReadOnly(true)] public string CollisionPolicy { get; } = C3DReferenceGridProfile.CollisionPolicy;
    [Category("A3 publish policy")] [DisplayName("Out-of-bounds policy")] [PropertyOrder(23)] [ReadOnly(true)] public string OutOfBoundsPolicy { get; } = C3DReferenceGridProfile.OutOfBoundsPolicy;
    [Category("A3 publish policy")] [DisplayName("Missing-cell policy")] [PropertyOrder(24)] [ReadOnly(true)] public string HolePolicy { get; } = C3DReferenceGridProfile.HolePolicy;

    internal static RegridHeightMapStepProperties From(ToolWorkbenchPipelineStepItem step) => new()
    {
        ReferenceFrameId = ToolWorkbenchStepPropertySession.GetParameter(step, "ReferenceFrameId") ?? "frame.reference",
        ReferenceUnit = ToolWorkbenchStepPropertySession.GetParameter(step, "ReferenceUnit") ?? "unitless",
        ReferenceProvenance = ToolWorkbenchStepPropertySession.GetParameter(step, "ReferenceProvenance") ?? "Authored reference grid",
        ReferenceRevision = ToolWorkbenchStepPropertySession.GetParameter(step, "ReferenceRevision") ?? "R1",
        OriginX = Double(step, "OriginX", 0), OriginY = Double(step, "OriginY", 0), OriginZ = Double(step, "OriginZ", 0),
        UAxisX = Double(step, "UAxisX", 1), UAxisY = Double(step, "UAxisY", 0), UAxisZ = Double(step, "UAxisZ", 0),
        VAxisX = Double(step, "VAxisX", 0), VAxisY = Double(step, "VAxisY", 1), VAxisZ = Double(step, "VAxisZ", 0),
        HAxisX = Double(step, "HAxisX", 0), HAxisY = Double(step, "HAxisY", 0), HAxisZ = Double(step, "HAxisZ", 1),
        PitchU = Double(step, "PitchU", 1), PitchV = Double(step, "PitchV", 1),
        RowCount = Integer(step, "RowCount", 1), ColumnCount = Integer(step, "ColumnCount", 1),
        MinimumCoverageRatio = Double(step, "MinimumCoverageRatio", 1)
    };

    internal bool TryCreateProfile(out C3DReferenceGridProfile? profile, out string message)
    {
        profile = null;
        try
        {
            profile = C3DReferenceGridProfile.Create(
                ReferenceFrameId, ReferenceUnit, ReferenceProvenance, ReferenceRevision,
                new C3DReferenceGridVector(OriginX, OriginY, OriginZ),
                new C3DReferenceGridVector(UAxisX, UAxisY, UAxisZ),
                new C3DReferenceGridVector(VAxisX, VAxisY, VAxisZ),
                new C3DReferenceGridVector(HAxisX, HAxisY, HAxisZ),
                PitchU, PitchV, RowCount, ColumnCount, MinimumCoverageRatio);
            message = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or OverflowException)
        {
            message = exception.Message;
            return false;
        }
    }

    private static double Double(ToolWorkbenchPipelineStepItem step, string name, double fallback) =>
        double.TryParse(ToolWorkbenchStepPropertySession.GetParameter(step, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    private static int Integer(ToolWorkbenchPipelineStepItem step, string name, int fallback) =>
        int.TryParse(ToolWorkbenchStepPropertySession.GetParameter(step, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
}

[CategoryOrder("Surface acceptance", 0)]
[CategoryOrder("Edge acceptance", 1)]
[CategoryOrder("Rotation search (deg)", 2)]
[CategoryOrder("Translation bounds", 3)]
[CategoryOrder("Search guard", 4)]
public sealed class SurfaceMatchStepProperties
{
    internal static readonly HashSet<string> MappedNames =
    [
        "MinimumCoverageRatio", "MaximumInlierRmse",
        "MinimumEdgeCoverageRatio", "MaximumEdgeInlierRmse",
        "MinimumRotationXDegrees", "MaximumRotationXDegrees", "RotationStepXDegrees",
        "MinimumRotationYDegrees", "MaximumRotationYDegrees", "RotationStepYDegrees",
        "MinimumRotationZDegrees", "MaximumRotationZDegrees", "RotationStepZDegrees",
        "MinimumTranslationX", "MaximumTranslationX",
        "MinimumTranslationY", "MaximumTranslationY",
        "MinimumTranslationZ", "MaximumTranslationZ",
        "MaximumCorrespondenceDistance", "MinimumMatchedSampleCount", "MaximumCandidateCount"
    ];

    [Category("Surface acceptance")]
    [DisplayName("Minimum surface coverage")]
    [Description("Separate Pass/Fail limit over the raw one-way model coverage. It does not change pose search or the Viewer overlay.")]
    [PropertyOrder(0)]
    [NumberRange(0, 1, 0.01)]
    public double MinimumCoverageRatio { get; set; } = 0.9;

    [Category("Surface acceptance")]
    [DisplayName("Maximum surface RMSE")]
    [Description("Separate Pass/Fail limit in the model/scene unit. It does not change the correspondence distance used by search.")]
    [PropertyOrder(1)]
    public double MaximumInlierRmse { get; set; } = 0.25;

    [Category("Edge acceptance")]
    [DisplayName("Minimum 3D-edge coverage")]
    [Description("Independent Pass/Fail limit over the raw 3D-edge score. It does not change surface coverage, pose search, or the Viewer overlay.")]
    [PropertyOrder(2)]
    [NumberRange(0, 1, 0.01)]
    public double MinimumEdgeCoverageRatio { get; set; } = 0.9;

    [Category("Edge acceptance")]
    [DisplayName("Maximum 3D-edge RMSE")]
    [Description("Independent 3D-edge RMSE limit in the model/scene unit. No weighted surface-edge score is created.")]
    [PropertyOrder(3)]
    public double MaximumEdgeInlierRmse { get; set; } = 0.25;

    [Category("Rotation search (deg)")] [DisplayName("X minimum")] [PropertyOrder(10)] public double MinimumRotationXDegrees { get; set; }
    [Category("Rotation search (deg)")] [DisplayName("X maximum")] [PropertyOrder(11)] public double MaximumRotationXDegrees { get; set; }
    [Category("Rotation search (deg)")] [DisplayName("X step")] [PropertyOrder(12)] public double RotationStepXDegrees { get; set; } = 1;
    [Category("Rotation search (deg)")] [DisplayName("Y minimum")] [PropertyOrder(13)] public double MinimumRotationYDegrees { get; set; }
    [Category("Rotation search (deg)")] [DisplayName("Y maximum")] [PropertyOrder(14)] public double MaximumRotationYDegrees { get; set; }
    [Category("Rotation search (deg)")] [DisplayName("Y step")] [PropertyOrder(15)] public double RotationStepYDegrees { get; set; } = 1;
    [Category("Rotation search (deg)")] [DisplayName("Z minimum")] [PropertyOrder(16)] public double MinimumRotationZDegrees { get; set; } = -45;
    [Category("Rotation search (deg)")] [DisplayName("Z maximum")] [PropertyOrder(17)] public double MaximumRotationZDegrees { get; set; } = 45;
    [Category("Rotation search (deg)")] [DisplayName("Z step")] [PropertyOrder(18)] public double RotationStepZDegrees { get; set; } = 15;

    [Category("Translation bounds")] [DisplayName("X minimum")] [PropertyOrder(20)] public double MinimumTranslationX { get; set; } = -10;
    [Category("Translation bounds")] [DisplayName("X maximum")] [PropertyOrder(21)] public double MaximumTranslationX { get; set; } = 10;
    [Category("Translation bounds")] [DisplayName("Y minimum")] [PropertyOrder(22)] public double MinimumTranslationY { get; set; } = -10;
    [Category("Translation bounds")] [DisplayName("Y maximum")] [PropertyOrder(23)] public double MaximumTranslationY { get; set; } = 10;
    [Category("Translation bounds")] [DisplayName("Z minimum")] [PropertyOrder(24)] public double MinimumTranslationZ { get; set; } = -10;
    [Category("Translation bounds")] [DisplayName("Z maximum")] [PropertyOrder(25)] public double MaximumTranslationZ { get; set; } = 10;

    [Category("Search guard")]
    [DisplayName("Maximum correspondence distance")]
    [Description("Raw nearest-sample distance used by pose scoring. This is not the Pass/Fail RMSE limit.")]
    [PropertyOrder(30)]
    public double MaximumCorrespondenceDistance { get; set; } = 1;

    [Category("Search guard")]
    [DisplayName("Minimum matched samples")]
    [PropertyOrder(31)]
    public int MinimumMatchedSampleCount { get; set; } = 3;

    [Category("Search guard")]
    [DisplayName("Maximum candidates")]
    [Description("Fail closed before execution when the authored rotation grid exceeds this budget.")]
    [PropertyOrder(32)]
    public int MaximumCandidateCount { get; set; } = 10000;

    internal static SurfaceMatchStepProperties From(
        ToolWorkbenchPipelineStepItem step) => new()
    {
        MinimumCoverageRatio = Double(step, "MinimumCoverageRatio", 0.9),
        MaximumInlierRmse = Double(step, "MaximumInlierRmse", 0.25),
        MinimumEdgeCoverageRatio = Double(step, "MinimumEdgeCoverageRatio", 0.9),
        MaximumEdgeInlierRmse = Double(step, "MaximumEdgeInlierRmse", 0.25),
        MinimumRotationXDegrees = Double(step, "MinimumRotationXDegrees", 0),
        MaximumRotationXDegrees = Double(step, "MaximumRotationXDegrees", 0),
        RotationStepXDegrees = Double(step, "RotationStepXDegrees", 1),
        MinimumRotationYDegrees = Double(step, "MinimumRotationYDegrees", 0),
        MaximumRotationYDegrees = Double(step, "MaximumRotationYDegrees", 0),
        RotationStepYDegrees = Double(step, "RotationStepYDegrees", 1),
        MinimumRotationZDegrees = Double(step, "MinimumRotationZDegrees", -45),
        MaximumRotationZDegrees = Double(step, "MaximumRotationZDegrees", 45),
        RotationStepZDegrees = Double(step, "RotationStepZDegrees", 15),
        MinimumTranslationX = Double(step, "MinimumTranslationX", -10),
        MaximumTranslationX = Double(step, "MaximumTranslationX", 10),
        MinimumTranslationY = Double(step, "MinimumTranslationY", -10),
        MaximumTranslationY = Double(step, "MaximumTranslationY", 10),
        MinimumTranslationZ = Double(step, "MinimumTranslationZ", -10),
        MaximumTranslationZ = Double(step, "MaximumTranslationZ", 10),
        MaximumCorrespondenceDistance = Double(step, "MaximumCorrespondenceDistance", 1),
        MinimumMatchedSampleCount = Integer(step, "MinimumMatchedSampleCount", 3),
        MaximumCandidateCount = Integer(step, "MaximumCandidateCount", 10000)
    };

    internal bool TryCreateContracts(
        out RigidSurfacePoseSearchParameters? search,
        out SurfaceMatchAcceptancePolicy? policy,
        out string message)
    {
        search = new RigidSurfacePoseSearchParameters(
            MinimumRotationXDegrees, MaximumRotationXDegrees, RotationStepXDegrees,
            MinimumRotationYDegrees, MaximumRotationYDegrees, RotationStepYDegrees,
            MinimumRotationZDegrees, MaximumRotationZDegrees, RotationStepZDegrees,
            MinimumTranslationX, MaximumTranslationX,
            MinimumTranslationY, MaximumTranslationY,
            MinimumTranslationZ, MaximumTranslationZ,
            MaximumCorrespondenceDistance,
            MinimumMatchedSampleCount,
            MaximumCandidateCount);
        var validity =
            RigidSurfacePoseSearchParameterValidator.Inspect(search);
        if (!validity.IsValid)
        {
            policy = null;
            message = string.Join(" ", validity.Errors);
            return false;
        }

        try
        {
            policy = SurfaceMatchAcceptancePolicy.Create(
                MinimumCoverageRatio,
                MaximumInlierRmse);
            message =
                $"Finite search domain ready ({validity.CandidateCount} candidates). Parameter Apply will not execute Preview or Run.";
            return true;
        }
        catch (InvalidDataException exception)
        {
            policy = null;
            message = exception.Message;
            return false;
        }
    }

    internal bool TryCreateIndependentContracts(
        out RigidSurfacePoseSearchParameters? search,
        out SurfaceAndEdgeMatchAcceptancePolicy? policy,
        out string message)
    {
        if (!TryCreateContracts(
                out search,
                out var surfacePolicy,
                out message)
            || surfacePolicy is null)
        {
            policy = null;
            return false;
        }

        try
        {
            var edgePolicy = SurfaceEdgeAcceptancePolicy.Create(
                MinimumEdgeCoverageRatio,
                MaximumEdgeInlierRmse);
            policy = SurfaceAndEdgeMatchAcceptancePolicy.Create(
                surfacePolicy,
                edgePolicy);
            message =
                "Independent surface and 3D-edge limits ready. Parameter Apply will not execute Preview or Run.";
            return true;
        }
        catch (InvalidDataException exception)
        {
            policy = null;
            message = exception.Message;
            return false;
        }
    }

    internal IReadOnlyDictionary<string, string> ToRecipeParameters() =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MinimumCoverageRatio"] = Text(MinimumCoverageRatio),
            ["MaximumInlierRmse"] = Text(MaximumInlierRmse),
            ["MinimumEdgeCoverageRatio"] = Text(MinimumEdgeCoverageRatio),
            ["MaximumEdgeInlierRmse"] = Text(MaximumEdgeInlierRmse),
            ["MinimumRotationXDegrees"] = Text(MinimumRotationXDegrees),
            ["MaximumRotationXDegrees"] = Text(MaximumRotationXDegrees),
            ["RotationStepXDegrees"] = Text(RotationStepXDegrees),
            ["MinimumRotationYDegrees"] = Text(MinimumRotationYDegrees),
            ["MaximumRotationYDegrees"] = Text(MaximumRotationYDegrees),
            ["RotationStepYDegrees"] = Text(RotationStepYDegrees),
            ["MinimumRotationZDegrees"] = Text(MinimumRotationZDegrees),
            ["MaximumRotationZDegrees"] = Text(MaximumRotationZDegrees),
            ["RotationStepZDegrees"] = Text(RotationStepZDegrees),
            ["MinimumTranslationX"] = Text(MinimumTranslationX),
            ["MaximumTranslationX"] = Text(MaximumTranslationX),
            ["MinimumTranslationY"] = Text(MinimumTranslationY),
            ["MaximumTranslationY"] = Text(MaximumTranslationY),
            ["MinimumTranslationZ"] = Text(MinimumTranslationZ),
            ["MaximumTranslationZ"] = Text(MaximumTranslationZ),
            ["MaximumCorrespondenceDistance"] = Text(MaximumCorrespondenceDistance),
            ["MinimumMatchedSampleCount"] = MinimumMatchedSampleCount.ToString(CultureInfo.InvariantCulture),
            ["MaximumCandidateCount"] = MaximumCandidateCount.ToString(CultureInfo.InvariantCulture)
        };

    private static double Double(
        ToolWorkbenchPipelineStepItem step,
        string name,
        double fallback) =>
        double.TryParse(
            ToolWorkbenchStepPropertySession.GetParameter(step, name),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var value)
                ? value
                : fallback;

    private static int Integer(
        ToolWorkbenchPipelineStepItem step,
        string name,
        int fallback) =>
        int.TryParse(
            ToolWorkbenchStepPropertySession.GetParameter(step, name),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value)
                ? value
                : fallback;

    private static string Text(double value) =>
        value.ToString("G17", CultureInfo.InvariantCulture);
}
