using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Controls.WpfPropertyGrid;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;


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
        SolvePolicy = ToolWorkbenchStepParameterAccess.GetParameter(step, "SolvePolicy") ?? "ExactFourPartialPivot",
        MaximumConditionEstimate = double.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, "MaximumConditionEstimate"), NumberStyles.Float, CultureInfo.InvariantCulture, out var maximum) ? maximum : 1000000,
        ArithmeticResidualWarning = double.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, "ArithmeticResidualWarning"), NumberStyles.Float, CultureInfo.InvariantCulture, out var warning) ? warning : 0.001,
        UnmappedParameters = ToolWorkbenchStepParameterAccess.GetUnmappedParameters(step, MappedNames)
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

[CategoryOrder("D-07 contract", 0)]
public sealed class DomainMaskStepProperties
{
    internal static readonly HashSet<string> MappedNames = [];

    [Category("D-07 contract")]
    [DisplayName("Domain policy")]
    [Description("The complete Published ConnectedRegionArtifact is unioned into one same-grid foreground mask. No parameters are authored.")]
    [PropertyOrder(0)]
    [ReadOnly(true)]
    public string DomainPolicy { get; init; } = "Complete Published ConnectedRegionArtifact";

    [Category("D-07 contract")]
    [DisplayName("Missing-value policy")]
    [Description("Cells outside the domain become missing; in-domain values, including existing missing cells, are preserved.")]
    [PropertyOrder(1)]
    [ReadOnly(true)]
    public string MissingValuePolicy { get; init; } = "Outside → missing; inside → preserve";

    [Category("D-07 contract")]
    [DisplayName("Execution policy")]
    [Description("Preview, Publish, Run, and save/reopen remain explicit. Selection or editing never executes the tool.")]
    [PropertyOrder(2)]
    [ReadOnly(true)]
    public string ExecutionPolicy { get; init; } = "Explicit Preview → Publish → Run";

    internal static DomainMaskStepProperties From(ToolWorkbenchPipelineStepItem step) => new();
}
