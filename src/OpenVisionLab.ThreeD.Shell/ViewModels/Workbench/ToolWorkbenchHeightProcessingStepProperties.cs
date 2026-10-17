using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Controls.WpfPropertyGrid;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;


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
        Method = Enum.TryParse<FilterMethod>(ToolWorkbenchStepParameterAccess.GetParameter(step, "Method"), out var method) ? method : FilterMethod.Median,
        KernelSize = int.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, "KernelSize"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var kernel) ? kernel : 0,
        MissingValuePolicy = Enum.TryParse<FilterMissingValuePolicy>(ToolWorkbenchStepParameterAccess.GetParameter(step, "MissingValuePolicy"), out var missing) ? missing : FilterMissingValuePolicy.PreserveMask,
        BoundaryPolicy = Enum.TryParse<FilterBoundaryPolicy>(ToolWorkbenchStepParameterAccess.GetParameter(step, "BoundaryPolicy"), out var boundary) ? boundary : FilterBoundaryPolicy.AvailableNeighbors,
        UnmappedParameters = ToolWorkbenchStepParameterAccess.GetUnmappedParameters(step, MappedNames)
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

public enum HeightThresholdBackgroundRemovalMode
{
    KeepAtOrAboveThreshold,
    KeepAtOrBelowThreshold
}

[CategoryOrder("Threshold rule", 0)]
[CategoryOrder("Evidence policy", 1)]
[CategoryOrder("Compatibility", 2)]
public sealed class HeightThresholdBackgroundRemovalStepProperties
{
    internal static readonly HashSet<string> MappedNames =
    [
        "Threshold",
        "Mode",
        "ComparisonPolicy",
        "MissingValuePolicy",
        "BackgroundPolicy"
    ];

    [Category("Threshold rule")]
    [DisplayName("Threshold")]
    [Description("Inclusive raw-height threshold used to retain finite samples in the derived map.")]
    [PropertyOrder(0)]
    [NumberRange(double.MinValue, double.MaxValue, 0.001)]
    public double Threshold { get; set; }

    [Category("Threshold rule")]
    [DisplayName("Keep mode")]
    [Description("Retain finite raw-height samples at or above, or at or below, the threshold.")]
    [PropertyOrder(1)]
    public HeightThresholdBackgroundRemovalMode Mode { get; set; }

    [Category("Evidence policy")]
    [DisplayName("Comparison policy")]
    [Description("Fixed v1 inclusive finite-sample predicate; this value is persisted as recipe evidence.")]
    [PropertyOrder(2)]
    [ReadOnly(true)]
    public string ComparisonPolicy { get; init; } = C3DHeightThresholdBackgroundRemovalEvidence.ComparisonPolicyName;

    [Category("Evidence policy")]
    [DisplayName("Missing values")]
    [Description("Existing missing source cells remain missing in the derived output.")]
    [PropertyOrder(3)]
    [ReadOnly(true)]
    public string MissingValuePolicy { get; init; } = C3DHeightThresholdBackgroundRemovalEvidence.MissingValuePolicyName;

    [Category("Evidence policy")]
    [DisplayName("Background action")]
    [Description("Samples rejected by the predicate become missing only in the derived output.")]
    [PropertyOrder(4)]
    [ReadOnly(true)]
    public string BackgroundPolicy { get; init; } = C3DHeightThresholdBackgroundRemovalEvidence.BackgroundPolicyName;

    [Category("Compatibility")]
    [DisplayName("Unmapped parameters")]
    [Description("Unknown parameters are retained unchanged when known parameters are applied.")]
    [PropertyOrder(10)]
    [ReadOnly(true)]
    public string UnmappedParameters { get; init; } = "(none)";

    internal static HeightThresholdBackgroundRemovalStepProperties From(
        ToolWorkbenchPipelineStepItem step) => new()
    {
        Threshold = double.TryParse(
            ToolWorkbenchStepParameterAccess.GetParameter(step, "Threshold"),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var threshold)
            ? threshold
            : double.NaN,
        Mode = Enum.TryParse<HeightThresholdBackgroundRemovalMode>(
            ToolWorkbenchStepParameterAccess.GetParameter(step, "Mode"),
            out var mode)
            ? mode
            : HeightThresholdBackgroundRemovalMode.KeepAtOrAboveThreshold,
        ComparisonPolicy = ToolWorkbenchStepParameterAccess.GetParameter(step, "ComparisonPolicy")
            ?? C3DHeightThresholdBackgroundRemovalEvidence.ComparisonPolicyName,
        MissingValuePolicy = ToolWorkbenchStepParameterAccess.GetParameter(step, "MissingValuePolicy")
            ?? C3DHeightThresholdBackgroundRemovalEvidence.MissingValuePolicyName,
        BackgroundPolicy = ToolWorkbenchStepParameterAccess.GetParameter(step, "BackgroundPolicy")
            ?? C3DHeightThresholdBackgroundRemovalEvidence.BackgroundPolicyName,
        UnmappedParameters = ToolWorkbenchStepParameterAccess.GetUnmappedParameters(step, MappedNames)
    };

    internal bool TryValidate(out string message)
    {
        if (!double.IsFinite(Threshold))
        {
            message = "Threshold must be finite.";
            return false;
        }

        if (!Enum.IsDefined(Mode))
        {
            message = "Keep mode is invalid.";
            return false;
        }

        message = string.Empty;
        return true;
    }
}

[CategoryOrder("Saved background", 0)]
[CategoryOrder("Evidence policy", 1)]
[CategoryOrder("Compatibility", 2)]
public sealed class SignedDeltaBackgroundSubtractionStepProperties
{
    internal static readonly HashSet<string> MappedNames =
    [
        C3DSignedHeightDeltaRecipePolicy.BackgroundPathParameterName,
        C3DSignedHeightDeltaRecipePolicy.BackgroundEntityIdParameterName,
        C3DSignedHeightDeltaRecipePolicy.BackgroundByteLengthParameterName,
        C3DSignedHeightDeltaRecipePolicy.BackgroundContentSha256ParameterName,
        C3DSignedHeightDeltaRecipePolicy.BackgroundWidthParameterName,
        C3DSignedHeightDeltaRecipePolicy.BackgroundHeightParameterName,
        C3DSignedHeightDeltaRecipePolicy.BackgroundUnitParameterName,
        C3DSignedHeightDeltaRecipePolicy.BackgroundFrameIdParameterName,
        C3DSignedHeightDeltaRecipePolicy.SubtractionPolicyParameterName,
        C3DSignedHeightDeltaRecipePolicy.MissingValuePolicyParameterName,
        C3DSignedHeightDeltaRecipePolicy.ZeroDeltaPolicyParameterName
    ];

    [Category("Saved background")]
    [DisplayName("Background path")]
    [Description("Recipe-relative or absolute C3D path for the saved background; bytes are verified before Preview.")]
    [PropertyOrder(0)]
    public string BackgroundPath { get; set; } = string.Empty;

    [Category("Saved background")]
    [DisplayName("Background entity ID")]
    [Description("Stable identity of the saved background source; it must differ from the current source.")]
    [PropertyOrder(1)]
    public string BackgroundEntityId { get; set; } = string.Empty;

    [Category("Saved background")]
    [DisplayName("Background byte length")]
    [Description("Recorded byte length used for fail-closed identity verification.")]
    [PropertyOrder(2)]
    public long BackgroundByteLength { get; set; }

    [Category("Saved background")]
    [DisplayName("Background SHA-256")]
    [Description("Recorded C3D content SHA-256 used for fail-closed identity verification.")]
    [PropertyOrder(3)]
    public string BackgroundContentSha256 { get; set; } = string.Empty;

    [Category("Saved background")]
    [DisplayName("Background width")]
    [PropertyOrder(4)]
    public int BackgroundWidth { get; set; }

    [Category("Saved background")]
    [DisplayName("Background height")]
    [PropertyOrder(5)]
    public int BackgroundHeight { get; set; }

    [Category("Saved background")]
    [DisplayName("Background unit")]
    [PropertyOrder(6)]
    public string BackgroundUnit { get; set; } = "raw-height";

    [Category("Saved background")]
    [DisplayName("Background frame")]
    [PropertyOrder(7)]
    public string BackgroundFrameId { get; set; } = string.Empty;

    [Category("Evidence policy")]
    [DisplayName("Subtraction policy")]
    [ReadOnly(true)]
    [PropertyOrder(8)]
    public string SubtractionPolicy { get; init; } = C3DSignedHeightDeltaRecipePolicy.SubtractionPolicyName;

    [Category("Evidence policy")]
    [DisplayName("Missing value policy")]
    [ReadOnly(true)]
    [PropertyOrder(9)]
    public string MissingValuePolicy { get; init; } = C3DSignedHeightDeltaRecipePolicy.MissingValuePolicyName;

    [Category("Evidence policy")]
    [DisplayName("Zero delta policy")]
    [ReadOnly(true)]
    [PropertyOrder(10)]
    public string ZeroDeltaPolicy { get; init; } = C3DSignedHeightDeltaRecipePolicy.ZeroDeltaPolicyName;

    [Category("Compatibility")]
    [DisplayName("Unmapped parameters")]
    [ReadOnly(true)]
    [PropertyOrder(20)]
    public string UnmappedParameters { get; init; } = "(none)";

    internal static SignedDeltaBackgroundSubtractionStepProperties From(ToolWorkbenchPipelineStepItem step) => new()
    {
        BackgroundPath = ToolWorkbenchStepParameterAccess.GetParameter(step, C3DSignedHeightDeltaRecipePolicy.BackgroundPathParameterName) ?? string.Empty,
        BackgroundEntityId = ToolWorkbenchStepParameterAccess.GetParameter(step, C3DSignedHeightDeltaRecipePolicy.BackgroundEntityIdParameterName) ?? string.Empty,
        BackgroundByteLength = long.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, C3DSignedHeightDeltaRecipePolicy.BackgroundByteLengthParameterName), NumberStyles.Integer, CultureInfo.InvariantCulture, out var byteLength) ? byteLength : 0,
        BackgroundContentSha256 = ToolWorkbenchStepParameterAccess.GetParameter(step, C3DSignedHeightDeltaRecipePolicy.BackgroundContentSha256ParameterName) ?? string.Empty,
        BackgroundWidth = int.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, C3DSignedHeightDeltaRecipePolicy.BackgroundWidthParameterName), NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) ? width : 0,
        BackgroundHeight = int.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, C3DSignedHeightDeltaRecipePolicy.BackgroundHeightParameterName), NumberStyles.Integer, CultureInfo.InvariantCulture, out var height) ? height : 0,
        BackgroundUnit = ToolWorkbenchStepParameterAccess.GetParameter(step, C3DSignedHeightDeltaRecipePolicy.BackgroundUnitParameterName) ?? string.Empty,
        BackgroundFrameId = ToolWorkbenchStepParameterAccess.GetParameter(step, C3DSignedHeightDeltaRecipePolicy.BackgroundFrameIdParameterName) ?? string.Empty,
        SubtractionPolicy = ToolWorkbenchStepParameterAccess.GetParameter(step, C3DSignedHeightDeltaRecipePolicy.SubtractionPolicyParameterName) ?? C3DSignedHeightDeltaRecipePolicy.SubtractionPolicyName,
        MissingValuePolicy = ToolWorkbenchStepParameterAccess.GetParameter(step, C3DSignedHeightDeltaRecipePolicy.MissingValuePolicyParameterName) ?? C3DSignedHeightDeltaRecipePolicy.MissingValuePolicyName,
        ZeroDeltaPolicy = ToolWorkbenchStepParameterAccess.GetParameter(step, C3DSignedHeightDeltaRecipePolicy.ZeroDeltaPolicyParameterName) ?? C3DSignedHeightDeltaRecipePolicy.ZeroDeltaPolicyName,
        UnmappedParameters = ToolWorkbenchStepParameterAccess.GetUnmappedParameters(step, MappedNames)
    };

    internal bool TryValidate(out string message)
    {
        if (string.IsNullOrWhiteSpace(BackgroundPath)
            || string.IsNullOrWhiteSpace(BackgroundEntityId)
            || BackgroundByteLength <= 0
            || BackgroundWidth <= 0
            || BackgroundHeight <= 0
            || BackgroundContentSha256.Length != 64
            || !BackgroundContentSha256.All(Uri.IsHexDigit)
            || string.IsNullOrWhiteSpace(BackgroundUnit)
            || string.IsNullOrWhiteSpace(BackgroundFrameId))
        {
            message = "Saved background path, identity, dimensions, unit, frame, and SHA-256 are required.";
            return false;
        }

        if (SubtractionPolicy != C3DSignedHeightDeltaRecipePolicy.SubtractionPolicyName
            || MissingValuePolicy != C3DSignedHeightDeltaRecipePolicy.MissingValuePolicyName
            || ZeroDeltaPolicy != C3DSignedHeightDeltaRecipePolicy.ZeroDeltaPolicyName)
        {
            message = "Signed delta evidence policies are fixed and cannot be changed.";
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
    [PropertyOrder(2)]
    [NumberRange(3, 7, 2)]
    public int WindowSize { get; set; }

    [Category("Outlier rule")]
    [DisplayName("Maximum absolute deviation")]
    [Description("A cell is removed only when |center - local median| is strictly greater than this raw-height threshold.")]
    [PropertyOrder(1)]
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
            ToolWorkbenchStepParameterAccess.GetParameter(step, "Rule"),
            out var rule)
            ? rule
            : RemoveOutlierPixelsRule.LocalMedianAbsoluteDeviation,
        WindowSize = int.TryParse(
            ToolWorkbenchStepParameterAccess.GetParameter(step, "WindowSize"),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var windowSize)
            ? windowSize
            : 0,
        MaximumAbsoluteDeviation = double.TryParse(
            ToolWorkbenchStepParameterAccess.GetParameter(
                step,
                "MaximumAbsoluteDeviation"),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var maximumDeviation)
            ? maximumDeviation
            : double.NaN,
        MinimumValidNeighbors = int.TryParse(
            ToolWorkbenchStepParameterAccess.GetParameter(
                step,
                "MinimumValidNeighbors"),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var minimumNeighbors)
            ? minimumNeighbors
            : 0,
        MissingValuePolicy = Enum.TryParse<RemoveOutlierPixelsMissingValuePolicy>(
            ToolWorkbenchStepParameterAccess.GetParameter(step, "MissingValuePolicy"),
            out var missing)
            ? missing
            : RemoveOutlierPixelsMissingValuePolicy.PreserveMask,
        BoundaryPolicy = Enum.TryParse<RemoveOutlierPixelsBoundaryPolicy>(
            ToolWorkbenchStepParameterAccess.GetParameter(step, "BoundaryPolicy"),
            out var boundary)
            ? boundary
            : RemoveOutlierPixelsBoundaryPolicy.AvailableNeighbors,
        OutlierPolicy = Enum.TryParse<RemoveOutlierPixelsOutlierPolicy>(
            ToolWorkbenchStepParameterAccess.GetParameter(step, "OutlierPolicy"),
            out var outlier)
            ? outlier
            : RemoveOutlierPixelsOutlierPolicy.SetMissing,
        UnmappedParameters =
            ToolWorkbenchStepParameterAccess.GetUnmappedParameters(step, MappedNames)
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

public enum ConnectedRegionConnectivity
{
    Four,
    Eight
}

[CategoryOrder("Connectivity", 0)]
[CategoryOrder("Coordinates", 1)]
[CategoryOrder("Compatibility", 2)]
public sealed class ConnectedRegionStepProperties
{
    internal static readonly HashSet<string> MappedNames =
    [
        "Connectivity",
        "OriginX",
        "OriginY",
        "ColumnPitch",
        "RowPitch",
        "AreaUnit"
    ];

    [Category("Connectivity")]
    [DisplayName("Connectivity")]
    [Description("Four-connectivity or eight-connectivity over the exact published source-grid outlier mask.")]
    [PropertyOrder(0)]
    public ConnectedRegionConnectivity Connectivity { get; set; }

    [Category("Coordinates")]
    [DisplayName("Origin X")]
    [Description("X coordinate of the source-grid cell-center origin.")]
    [PropertyOrder(1)]
    public double OriginX { get; set; }

    [Category("Coordinates")]
    [DisplayName("Origin Y")]
    [Description("Y coordinate of the source-grid cell-center origin.")]
    [PropertyOrder(2)]
    public double OriginY { get; set; }

    [Category("Coordinates")]
    [DisplayName("Column pitch")]
    [Description("Positive X spacing between adjacent source-grid columns.")]
    [PropertyOrder(3)]
    [NumberRange(0.000001, double.MaxValue, 1)]
    public double ColumnPitch { get; set; }

    [Category("Coordinates")]
    [DisplayName("Row pitch")]
    [Description("Positive Y spacing between adjacent source-grid rows.")]
    [PropertyOrder(4)]
    [NumberRange(0.000001, double.MaxValue, 1)]
    public double RowPitch { get; set; }

    [Category("Coordinates")]
    [DisplayName("Area unit")]
    [Description("Unit label for region area; it is persisted as evidence and is not a calibration claim.")]
    [PropertyOrder(5)]
    public string AreaUnit { get; set; } = "grid-unit^2";

    [Category("Compatibility")]
    [DisplayName("Unmapped parameters")]
    [Description("Unknown parameters are retained unchanged when known parameters are applied.")]
    [PropertyOrder(10)]
    [ReadOnly(true)]
    public string UnmappedParameters { get; init; } = "(none)";

    internal static ConnectedRegionStepProperties From(
        ToolWorkbenchPipelineStepItem step) => new()
    {
        Connectivity = Enum.TryParse<ConnectedRegionConnectivity>(
            ToolWorkbenchStepParameterAccess.GetParameter(step, "Connectivity"),
            out var connectivity)
            ? connectivity
            : ConnectedRegionConnectivity.Four,
        OriginX = ParseDouble(step, "OriginX"),
        OriginY = ParseDouble(step, "OriginY"),
        ColumnPitch = ParseDouble(step, "ColumnPitch"),
        RowPitch = ParseDouble(step, "RowPitch"),
        AreaUnit = ToolWorkbenchStepParameterAccess.GetParameter(step, "AreaUnit"),
        UnmappedParameters =
            ToolWorkbenchStepParameterAccess.GetUnmappedParameters(step, MappedNames)
    };

    internal bool TryValidate(out string message)
    {
        if (!double.IsFinite(OriginX) || !double.IsFinite(OriginY))
        {
            message = "Origin X and Origin Y must be finite.";
            return false;
        }

        if (!double.IsFinite(ColumnPitch) || ColumnPitch <= 0d
            || !double.IsFinite(RowPitch) || RowPitch <= 0d)
        {
            message = "Column pitch and row pitch must be finite and greater than zero.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(AreaUnit))
        {
            message = "Area unit must not be empty.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static double ParseDouble(
        ToolWorkbenchPipelineStepItem step,
        string name) =>
        double.TryParse(
            ToolWorkbenchStepParameterAccess.GetParameter(step, name),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : double.NaN;
}

[CategoryOrder("Region", 0)]
[CategoryOrder("Compatibility", 1)]
public sealed class EditableRegionStepProperties
{
    internal static readonly HashSet<string> MappedNames = ["SelectedRegionIndex"];

    [Category("Region")]
    [DisplayName("Selected region index")]
    [Description("Stable zero-based index from the current Published Connected Region artifact. Preview selects the exact region without changing that upstream artifact.")]
    [PropertyOrder(0)]
    [NumberRange(0, int.MaxValue, 1)]
    public int SelectedRegionIndex { get; set; }

    [Category("Compatibility")]
    [DisplayName("Unmapped parameters")]
    [Description("Unknown parameters are retained unchanged when the known region index is applied.")]
    [PropertyOrder(10)]
    [ReadOnly(true)]
    public string UnmappedParameters { get; init; } = "(none)";

    internal static EditableRegionStepProperties From(ToolWorkbenchPipelineStepItem step) => new()
    {
        SelectedRegionIndex = int.TryParse(
            ToolWorkbenchStepParameterAccess.GetParameter(step, "SelectedRegionIndex"),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var index)
            ? index
            : -1,
        UnmappedParameters = ToolWorkbenchStepParameterAccess.GetUnmappedParameters(step, MappedNames)
    };

    internal bool TryValidate(out string message)
    {
        if (SelectedRegionIndex < 0)
        {
            message = "Selected region index must be zero or greater.";
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
            ToolWorkbenchStepParameterAccess.GetParameter(step, "ReferenceFitPolicy"),
            out var fit)
            ? fit
            : LevelSurfaceReferenceFitPolicy.LeastSquaresHeightPlane,
        LevelingPolicy = Enum.TryParse<LevelSurfaceLevelingPolicy>(
            ToolWorkbenchStepParameterAccess.GetParameter(step, "LevelingPolicy"),
            out var leveling)
            ? leveling
            : LevelSurfaceLevelingPolicy.HeightDetrendToReferenceMean,
        MissingValuePolicy = Enum.TryParse<LevelSurfaceMissingValuePolicy>(
            ToolWorkbenchStepParameterAccess.GetParameter(step, "MissingValuePolicy"),
            out var missing)
            ? missing
            : LevelSurfaceMissingValuePolicy.PreserveMask,
        GridPolicy = Enum.TryParse<LevelSurfaceGridPolicy>(
            ToolWorkbenchStepParameterAccess.GetParameter(step, "GridPolicy"),
            out var grid)
            ? grid
            : LevelSurfaceGridPolicy.PreserveSourceGrid,
        MinimumValidSampleCount = int.TryParse(
            ToolWorkbenchStepParameterAccess.GetParameter(step, "MinimumValidSampleCount"),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var minimum)
            ? minimum
            : 0,
        MaximumReferenceRmsResidual = double.TryParse(
            ToolWorkbenchStepParameterAccess.GetParameter(step, "MaximumReferenceRmsResidual"),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var maximum)
            ? maximum
            : double.NaN,
        UnmappedParameters =
            ToolWorkbenchStepParameterAccess.GetUnmappedParameters(step, MappedNames)
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
        ComparisonAxis = Enum.TryParse<HeightDifferenceEdgeComparisonAxis>(ToolWorkbenchStepParameterAccess.GetParameter(step, "ComparisonAxis"), out var axis)
            ? axis
            : HeightDifferenceEdgeComparisonAxis.Unspecified,
        Polarity = Enum.TryParse<HeightDifferenceEdgePolarity>(ToolWorkbenchStepParameterAccess.GetParameter(step, "Polarity"), out var polarity)
            ? polarity
            : HeightDifferenceEdgePolarity.Unspecified,
        MinimumDelta = double.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, "MinimumDelta"), NumberStyles.Float, CultureInfo.InvariantCulture, out var delta)
            ? delta
            : 0,
        CandidatePolicy = Enum.TryParse<HeightDifferenceEdgeCandidatePolicy>(ToolWorkbenchStepParameterAccess.GetParameter(step, "CandidatePolicy"), out var candidate)
            ? candidate
            : HeightDifferenceEdgeCandidatePolicy.StrongestPerScanline,
        PointPolicy = Enum.TryParse<HeightDifferenceEdgePointPolicy>(ToolWorkbenchStepParameterAccess.GetParameter(step, "PointPolicy"), out var point)
            ? point
            : HeightDifferenceEdgePointPolicy.PairMidpoint,
        MissingValuePolicy = Enum.TryParse<HeightDifferenceEdgeMissingValuePolicy>(ToolWorkbenchStepParameterAccess.GetParameter(step, "MissingValuePolicy"), out var missing)
            ? missing
            : HeightDifferenceEdgeMissingValuePolicy.SkipPair,
        BoundaryPolicy = Enum.TryParse<HeightDifferenceEdgeBoundaryPolicy>(ToolWorkbenchStepParameterAccess.GetParameter(step, "BoundaryPolicy"), out var boundary)
            ? boundary
            : HeightDifferenceEdgeBoundaryPolicy.WithinSelection,
        UnmappedParameters = ToolWorkbenchStepParameterAccess.GetUnmappedParameters(step, MappedNames)
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
