using System.ComponentModel;
using System.Globalization;
using System.Windows.Controls.WpfPropertyGrid;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed class NinePointSurfaceCompensationStepProperties
{
    internal static readonly HashSet<string> MappedNames = ["MiddleRow", "MiddleColumn", "TargetHeight", "Semantics"];

    [Category("Reference points"), DisplayName("Middle row"), PropertyOrder(0), NumberRange(1, int.MaxValue, 1)]
    public int MiddleRow { get; set; }

    [Category("Reference points"), DisplayName("Middle column"), PropertyOrder(1), NumberRange(1, int.MaxValue, 1)]
    public int MiddleColumn { get; set; }

    [Category("Reference points"), DisplayName("Target height"), PropertyOrder(2)]
    public double TargetHeight { get; set; }

    internal static NinePointSurfaceCompensationStepProperties From(ToolWorkbenchPipelineStepItem step) => new()
    {
        MiddleRow = int.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, "MiddleRow"), out var row) ? row : 0,
        MiddleColumn = int.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, "MiddleColumn"), out var column) ? column : 0,
        TargetHeight = double.TryParse(ToolWorkbenchStepParameterAccess.GetParameter(step, "TargetHeight"), NumberStyles.Float,
            CultureInfo.InvariantCulture, out var height) ? height : double.NaN
    };

    internal bool TryValidate(out string message)
    {
        message = MiddleRow < 1 || MiddleColumn < 1 ? "Middle row and column must be positive."
            : !double.IsFinite(TargetHeight) || TargetHeight == 0.0 ? "C3D reserves zero for missing data. Choose a finite nonzero target height."
            : string.Empty;
        return message.Length == 0;
    }

    internal IReadOnlyDictionary<string, string> Parameters() => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["MiddleRow"] = MiddleRow.ToString(CultureInfo.InvariantCulture),
        ["MiddleColumn"] = MiddleColumn.ToString(CultureInfo.InvariantCulture),
        ["TargetHeight"] = TargetHeight.ToString("G17", CultureInfo.InvariantCulture),
        ["Semantics"] = ToolRecipeNinePointSurfaceCompensationExecution.Semantics
    };
}
