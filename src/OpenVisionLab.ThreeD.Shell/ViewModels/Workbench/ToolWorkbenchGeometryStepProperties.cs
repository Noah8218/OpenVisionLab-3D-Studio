using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Controls.WpfPropertyGrid;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;


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
        OutputRole = ToolWorkbenchStepParameterAccess.GetParameter(step, "OutputRole") ?? string.Empty,
        ConstructionPolicy = TwoPointLineConstructionPolicy.OrderedPointsDefineSegment,
        UnmappedParameters = ToolWorkbenchStepParameterAccess.GetUnmappedParameters(step, MappedNames)
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
        OutputRole = ToolWorkbenchStepParameterAccess.GetParameter(step, "OutputRole") ?? string.Empty,
        ConstructionPolicy = ThreePointPlaneConstructionPolicy.OrderedPointsDefineOrientedPlane,
        UnmappedParameters = ToolWorkbenchStepParameterAccess.GetUnmappedParameters(step, MappedNames)
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
        MaximumPeakToValleyRawHeight = double.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, "MaximumPeakToValleyRawHeight"), NumberStyles.Float, CultureInfo.InvariantCulture, out var p2v) ? p2v : 0d,
        OutputRole = ToolWorkbenchStepParameterAccess.GetParameter(step, "OutputRole") ?? string.Empty,
        ResidualPolicy = DatumPlaneDeviationResidualPolicy.RawHeightMinusDatumPlanePredictedRawHeight,
        MinimumValidSampleCount = int.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, "MinimumValidSampleCount"), NumberStyles.None, CultureInfo.InvariantCulture, out var minimum) ? minimum : 0,
        MinimumAbsoluteNormalY = double.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, "MinimumAbsoluteNormalY"), NumberStyles.Float, CultureInfo.InvariantCulture, out var normalY) ? normalY : 0d,
        UnmappedParameters = ToolWorkbenchStepParameterAccess.GetUnmappedParameters(step, MappedNames)
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
        MaximumOrthogonalResidual = double.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, "MaximumOrthogonalResidual"), NumberStyles.Float, CultureInfo.InvariantCulture, out var residual) ? residual : 0,
        MinimumInlierCount = int.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, "MinimumInlierCount"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : 0,
        MinimumInlierRatio = double.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, "MinimumInlierRatio"), NumberStyles.Float, CultureInfo.InvariantCulture, out var ratio) ? ratio : 0,
        MinimumInlierScanlineSpan = int.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, "MinimumInlierScanlineSpan"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var span) ? span : 0,
        HypothesisPolicy = LineFitHypothesisPolicy.Sha256PairSchedule,
        MaximumHypotheses = 256,
        RefinementPolicy = LineFitRefinementPolicy.OrthogonalTlsUntilStable10,
        DirectionPolicy = LineFitDirectionPolicy.PositiveScanlineAxis,
        EndpointPolicy = LineFitEndpointPolicy.InlierProjectionExtents,
        UnmappedParameters = ToolWorkbenchStepParameterAccess.GetUnmappedParameters(step, MappedNames)
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
        MaximumClosestApproachDistance = double.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, "MaximumClosestApproachDistance"), NumberStyles.Float, CultureInfo.InvariantCulture, out var gap) ? gap : 0,
        MinimumAcuteAngleDegrees = double.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, "MinimumAcuteAngleDegrees"), NumberStyles.Float, CultureInfo.InvariantCulture, out var angle) ? angle : 0,
        MaximumSupportExtension = double.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, "MaximumSupportExtension"), NumberStyles.Float, CultureInfo.InvariantCulture, out var extension) ? extension : 0,
        OutputRole = ToolWorkbenchStepParameterAccess.GetParameter(step, "OutputRole") ?? string.Empty,
        ClosestApproachPolicy = LineIntersectionClosestApproachPolicy.MidpointOfClosestPoints,
        ParallelPolicy = LineIntersectionParallelPolicy.RejectBelowMinimumAcuteAngle,
        SupportPolicy = LineIntersectionSupportPolicy.WithinInlierProjectionExtentsWithMaximumExtension,
        UnmappedParameters = ToolWorkbenchStepParameterAccess.GetUnmappedParameters(step, MappedNames)
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
        PairCountPolicy = ToolWorkbenchStepParameterAccess.GetParameter(step, "PairCountPolicy") ?? "ExactlyFour",
        SourceArtifactPolicy = ToolWorkbenchStepParameterAccess.GetParameter(step, "SourceArtifactPolicy") ?? "CurrentPublishedCornerAnchor",
        AffineIndependencePolicy = ToolWorkbenchStepParameterAccess.GetParameter(step, "AffineIndependencePolicy") ?? "RequireNonDegenerateTetrahedra",
        UnmappedParameters = ToolWorkbenchStepParameterAccess.GetUnmappedParameters(step, MappedNames)
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
