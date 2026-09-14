using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Controls.WpfPropertyGrid;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;


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
        ReferenceFrameId = ToolWorkbenchStepParameterAccess.GetParameter(step, "ReferenceFrameId") ?? "frame.reference",
        ReferenceUnit = ToolWorkbenchStepParameterAccess.GetParameter(step, "ReferenceUnit") ?? "unitless",
        ReferenceProvenance = ToolWorkbenchStepParameterAccess.GetParameter(step, "ReferenceProvenance") ?? "Authored reference grid",
        ReferenceRevision = ToolWorkbenchStepParameterAccess.GetParameter(step, "ReferenceRevision") ?? "R1",
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
        double.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    private static int Integer(ToolWorkbenchPipelineStepItem step, string name, int fallback) =>
        int.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
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
            ToolWorkbenchStepParameterAccess.GetParameter(step, name),
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
            ToolWorkbenchStepParameterAccess.GetParameter(step, name),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value)
                ? value
                : fallback;

    private static string Text(double value) =>
        value.ToString("G17", CultureInfo.InvariantCulture);
}
