using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer.Teaching;

/// <summary>
/// Applies the Viewer rule for deciding whether a persisted teaching
/// selection still belongs to the currently loaded C3D grid. The rule is
/// WPF/OpenGL-neutral so the View only supplies the current source snapshot.
/// A derived HeightField can be displayed through its byte-identical C3D
/// export. Capture/editing contracts remain separate. Optional binding
/// metadata stays neutral for existing C3D selection compatibility.
/// </summary>
internal static class TeachingSelectionSourcePolicy
{
    public static bool IsCurrentC3DGrid(
        ToolRecipeSelection selection,
        TeachingCaptureSourceSnapshot source)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var isC3DGrid = string.Equals(
                selection.SourceBinding.Format,
                "C3D",
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(selection.SourceBinding.Format, "HeightField", StringComparison.OrdinalIgnoreCase);
        return isC3DGrid && string.Equals(
                selection.SourceBinding.ContentSha256,
                source.ContentSha256,
                StringComparison.OrdinalIgnoreCase)
            && selection.SourceBinding.GridWidth == source.Width
            && selection.SourceBinding.GridHeight == source.Height;
    }
}
