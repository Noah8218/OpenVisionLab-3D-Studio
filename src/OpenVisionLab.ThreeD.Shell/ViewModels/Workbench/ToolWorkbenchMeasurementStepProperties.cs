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
        UnmappedParameters = ToolWorkbenchStepParameterAccess.GetUnmappedParameters(step, MappedNames)
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
        double.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value : double.NaN;
    private static int ParseInt(ToolWorkbenchPipelineStepItem step, string name) =>
        int.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
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
                ToolWorkbenchStepParameterAccess.GetUnmappedParameters(
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
        UnmappedParameters = ToolWorkbenchStepParameterAccess.GetUnmappedParameters(step, MappedNames)
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
        double.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value : double.NaN;
    private static int ParseInt(ToolWorkbenchPipelineStepItem step, string name) =>
        int.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
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
        UnmappedParameters = ToolWorkbenchStepParameterAccess.GetUnmappedParameters(step, MappedNames)
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
        double.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value : double.NaN;
    private static int ParseInt(ToolWorkbenchPipelineStepItem step, string name) =>
        int.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
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
        UnmappedParameters = ToolWorkbenchStepParameterAccess.GetUnmappedParameters(step, MappedNames)
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
        double.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
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
        UnmappedParameters = ToolWorkbenchStepParameterAccess.GetUnmappedParameters(step, MappedNames)
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
        double.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
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
        UnmappedParameters = ToolWorkbenchStepParameterAccess.GetUnmappedParameters(step, MappedNames)
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
        double.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
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
        UnmappedParameters = ToolWorkbenchStepParameterAccess.GetUnmappedParameters(step, MappedNames)
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
        double.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value : double.NaN;
}
