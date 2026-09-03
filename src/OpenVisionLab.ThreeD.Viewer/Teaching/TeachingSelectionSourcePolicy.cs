using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer.Teaching;

/// <summary>
/// Applies the Viewer rule for deciding whether a persisted teaching
/// selection still belongs to the currently loaded C3D grid. The rule is
/// WPF/OpenGL-neutral so the View only supplies the current source snapshot.
/// Optional binding metadata is intentionally ignored here to preserve the
/// existing C3D selection compatibility contract.
/// </summary>
internal static class TeachingSelectionSourcePolicy
{
    public static bool IsCurrentC3DGrid(
        ToolRecipeSelection selection,
        TeachingCaptureSourceSnapshot source)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return string.Equals(
                selection.SourceBinding.Format,
                "C3D",
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                selection.SourceBinding.ContentSha256,
                source.ContentSha256,
                StringComparison.OrdinalIgnoreCase)
            && selection.SourceBinding.GridWidth == source.Width
            && selection.SourceBinding.GridHeight == source.Height;
    }
}
