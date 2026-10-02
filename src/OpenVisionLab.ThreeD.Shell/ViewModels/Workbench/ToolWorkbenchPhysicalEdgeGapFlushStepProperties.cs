using System.ComponentModel;
using System.Globalization;
using System.Windows.Controls.WpfPropertyGrid;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

[CategoryOrder("Identity", 0)]
[CategoryOrder("Edge policy", 1)]
[CategoryOrder("Support", 2)]
[CategoryOrder("Acceptance", 3)]
[CategoryOrder("Compatibility", 4)]
public sealed class PhysicalEdgeGapFlushStepProperties
{
    internal static readonly HashSet<string> MappedNames =
        new(C3DPhysicalEdgeGapFlushProfile.ParameterNames, StringComparer.Ordinal);

    [Category("Identity")]
    [DisplayName("Section / profile ID")]
    [Description("Explicit authored section identity shared by both physical-edge inputs.")]
    [PropertyOrder(0)]
    public string SectionId { get; set; } = string.Empty;

    [Category("Edge policy")]
    [DisplayName("Gap axis")]
    [Description("Source-grid direction used for the signed gap. Only AcrossColumns or AcrossRows is supported in this slice.")]
    [PropertyOrder(0)]
    public string ComparisonAxis { get; set; } = string.Empty;

    [Category("Edge policy")]
    [DisplayName("First surface side")]
    [Description("Surface support side paired with FirstEdge.")]
    [PropertyOrder(1)]
    public string FirstSurfaceSide { get; set; } = string.Empty;

    [Category("Edge policy")]
    [DisplayName("Second surface side")]
    [Description("Surface support side paired with SecondEdge.")]
    [PropertyOrder(2)]
    public string SecondSurfaceSide { get; set; } = string.Empty;

    [Category("Edge policy")]
    [DisplayName("Surface aggregation")]
    [Description("Support aggregation used for signed flush; the first contract supports MeanInlierPairs.")]
    [PropertyOrder(3)]
    public string SurfaceAggregation { get; set; } = string.Empty;

    [Category("Support")]
    [DisplayName("Minimum valid pairs")]
    [Description("Minimum finite paired scanlines required before the measurement can be Valid.")]
    [PropertyOrder(0)]
    public int MinimumValidPairCount { get; set; }

    [Category("Support")]
    [DisplayName("Minimum coverage ratio")]
    [Description("Inclusive minimum paired-scanline coverage ratio from zero to one.")]
    [PropertyOrder(1)]
    public double MinimumCoverageRatio { get; set; }

    [Category("Support")]
    [DisplayName("Maximum residual")]
    [Description("Maximum allowed fitted-edge residual in the declared source unit.")]
    [PropertyOrder(2)]
    public double MaximumResidual { get; set; }

    [Category("Support")]
    [DisplayName("Minimum inlier ratio")]
    [Description("Inclusive minimum inlier ratio for both fitted edges.")]
    [PropertyOrder(3)]
    public double MinimumInlierRatio { get; set; }

    [Category("Support")]
    [DisplayName("Minimum inlier span")]
    [Description("Minimum scanline span for each fitted edge.")]
    [PropertyOrder(4)]
    public int MinimumInlierScanlineSpan { get; set; }

    [Category("Support")]
    [DisplayName("Ambiguity policy")]
    [Description("Candidate ambiguity policy; the first contract rejects a near tie.")]
    [PropertyOrder(5)]
    public string AmbiguityPolicy { get; set; } = string.Empty;

    [Category("Support")]
    [DisplayName("Ambiguity epsilon")]
    [Description("Non-negative comparison epsilon used to classify a tied or near-tied candidate.")]
    [PropertyOrder(6)]
    public double AmbiguityEpsilon { get; set; }

    [Category("Support")]
    [DisplayName("Contact allowed")]
    [Description("Explicitly permits a zero-gap contact result. False rejects contact by default.")]
    [PropertyOrder(7)]
    public string ContactAllowed { get; set; } = string.Empty;

    [Category("Acceptance")]
    [DisplayName("Tolerance mode")]
    [Description("Enabled evaluates inclusive expected-value tolerances; Disabled reports measured/no tolerance.")]
    [PropertyOrder(0)]
    public string ToleranceMode { get; set; } = string.Empty;

    [Category("Acceptance")]
    [DisplayName("Expected signed gap")]
    [Description("Expected signed gap along the authored positive comparison axis.")]
    [PropertyOrder(1)]
    public double ExpectedGap { get; set; }

    [Category("Acceptance")]
    [DisplayName("Gap tolerance")]
    [Description("Inclusive absolute tolerance around ExpectedGap when ToleranceMode is Enabled.")]
    [PropertyOrder(2)]
    public double GapTolerance { get; set; }

    [Category("Acceptance")]
    [DisplayName("Expected signed flush")]
    [Description("Expected second-surface support minus first-surface support.")]
    [PropertyOrder(3)]
    public double ExpectedFlush { get; set; }

    [Category("Acceptance")]
    [DisplayName("Flush tolerance")]
    [Description("Inclusive absolute tolerance around ExpectedFlush when ToleranceMode is Enabled.")]
    [PropertyOrder(4)]
    public double FlushTolerance { get; set; }

    [Category("Compatibility")]
    [DisplayName("Unmapped parameters")]
    [ReadOnly(true)]
    public string UnmappedParameters { get; init; } = "(none)";

    internal static PhysicalEdgeGapFlushStepProperties From(
        ToolWorkbenchPipelineStepItem step) => new()
    {
        SectionId = GetParameter(step, C3DPhysicalEdgeGapFlushProfile.SectionIdParameter),
        ComparisonAxis = GetParameter(step, C3DPhysicalEdgeGapFlushProfile.ComparisonAxisParameter),
        FirstSurfaceSide = GetParameter(step, C3DPhysicalEdgeGapFlushProfile.FirstSurfaceSideParameter),
        SecondSurfaceSide = GetParameter(step, C3DPhysicalEdgeGapFlushProfile.SecondSurfaceSideParameter),
        SurfaceAggregation = GetParameter(step, C3DPhysicalEdgeGapFlushProfile.SurfaceAggregationParameter),
        MinimumValidPairCount = ParseInteger(step, C3DPhysicalEdgeGapFlushProfile.MinimumValidPairCountParameter),
        MinimumCoverageRatio = ParseDouble(step, C3DPhysicalEdgeGapFlushProfile.MinimumCoverageRatioParameter),
        MaximumResidual = ParseDouble(step, C3DPhysicalEdgeGapFlushProfile.MaximumResidualParameter),
        MinimumInlierRatio = ParseDouble(step, C3DPhysicalEdgeGapFlushProfile.MinimumInlierRatioParameter),
        MinimumInlierScanlineSpan = ParseInteger(step, C3DPhysicalEdgeGapFlushProfile.MinimumInlierScanlineSpanParameter),
        AmbiguityPolicy = GetParameter(step, C3DPhysicalEdgeGapFlushProfile.AmbiguityPolicyParameter),
        AmbiguityEpsilon = ParseDouble(step, C3DPhysicalEdgeGapFlushProfile.AmbiguityEpsilonParameter),
        ContactAllowed = GetParameter(step, C3DPhysicalEdgeGapFlushProfile.ContactAllowedParameter),
        ToleranceMode = GetParameter(step, C3DPhysicalEdgeGapFlushProfile.ToleranceModeParameter),
        ExpectedGap = ParseDouble(step, C3DPhysicalEdgeGapFlushProfile.ExpectedGapParameter),
        GapTolerance = ParseDouble(step, C3DPhysicalEdgeGapFlushProfile.GapToleranceParameter),
        ExpectedFlush = ParseDouble(step, C3DPhysicalEdgeGapFlushProfile.ExpectedFlushParameter),
        FlushTolerance = ParseDouble(step, C3DPhysicalEdgeGapFlushProfile.FlushToleranceParameter),
        UnmappedParameters = ToolWorkbenchStepParameterAccess.GetUnmappedParameters(step, MappedNames)
    };

    internal bool TryValidate(out string message)
    {
        var parameters = ToRecipeParameters();
        return C3DPhysicalEdgeGapFlushProfile.TryParse(parameters, out _, out message);
    }

    internal IReadOnlyList<ToolRecipeParameter> ToRecipeParameters() =>
    [
        new(C3DPhysicalEdgeGapFlushProfile.SectionIdParameter, SectionId),
        new(C3DPhysicalEdgeGapFlushProfile.ComparisonAxisParameter, ComparisonAxis),
        new(C3DPhysicalEdgeGapFlushProfile.FirstSurfaceSideParameter, FirstSurfaceSide),
        new(C3DPhysicalEdgeGapFlushProfile.SecondSurfaceSideParameter, SecondSurfaceSide),
        new(C3DPhysicalEdgeGapFlushProfile.SurfaceAggregationParameter, SurfaceAggregation),
        new(C3DPhysicalEdgeGapFlushProfile.MinimumValidPairCountParameter, MinimumValidPairCount.ToString(CultureInfo.InvariantCulture)),
        new(C3DPhysicalEdgeGapFlushProfile.MinimumCoverageRatioParameter, Format(MinimumCoverageRatio)),
        new(C3DPhysicalEdgeGapFlushProfile.MaximumResidualParameter, Format(MaximumResidual)),
        new(C3DPhysicalEdgeGapFlushProfile.MinimumInlierRatioParameter, Format(MinimumInlierRatio)),
        new(C3DPhysicalEdgeGapFlushProfile.MinimumInlierScanlineSpanParameter, MinimumInlierScanlineSpan.ToString(CultureInfo.InvariantCulture)),
        new(C3DPhysicalEdgeGapFlushProfile.AmbiguityPolicyParameter, AmbiguityPolicy),
        new(C3DPhysicalEdgeGapFlushProfile.AmbiguityEpsilonParameter, Format(AmbiguityEpsilon)),
        new(C3DPhysicalEdgeGapFlushProfile.ContactAllowedParameter, ContactAllowed),
        new(C3DPhysicalEdgeGapFlushProfile.ToleranceModeParameter, ToleranceMode),
        new(C3DPhysicalEdgeGapFlushProfile.ExpectedGapParameter, Format(ExpectedGap)),
        new(C3DPhysicalEdgeGapFlushProfile.GapToleranceParameter, Format(GapTolerance)),
        new(C3DPhysicalEdgeGapFlushProfile.ExpectedFlushParameter, Format(ExpectedFlush)),
        new(C3DPhysicalEdgeGapFlushProfile.FlushToleranceParameter, Format(FlushTolerance))
    ];

    private static string GetParameter(ToolWorkbenchPipelineStepItem step, string name) =>
        ToolWorkbenchStepParameterAccess.GetParameter(step, name);

    private static int ParseInteger(ToolWorkbenchPipelineStepItem step, string name) =>
        int.TryParse(GetParameter(step, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;

    private static double ParseDouble(ToolWorkbenchPipelineStepItem step, string name) =>
        double.TryParse(GetParameter(step, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : double.NaN;

    private static string Format(double value) =>
        value.ToString("G17", CultureInfo.InvariantCulture);
}
