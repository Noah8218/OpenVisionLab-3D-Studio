using OpenVisionLab.ThreeD.Core;
using SharpGL;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    /// <summary>
    /// Displays the existing source C3D and immediately applies a verified
    /// connected-region overlay. No synthetic surface is created.
    /// </summary>
    public bool ShowWorkbenchConnectedRegion(
        C3DConnectedRegionOutput output,
        string? selectedRegionId)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!IsConnectedRegionCompatibleWithCurrentC3D(output))
        {
            viewModel.SetConnectedRegionOverlay(null, null);
            viewModel.ViewerStatus = "Connected-region overlay does not match the current C3D grid.";
            RenderNow();
            return false;
        }

        viewModel.SetConnectedRegionOverlay(output, selectedRegionId);
        RenderNow();
        return true;
    }

    public void SetConnectedRegionOverlay(
        C3DConnectedRegionOutput? output,
        string? selectedRegionId)
    {
        if (output is not null && !IsConnectedRegionCompatibleWithCurrentC3D(output))
        {
            output = null;
            selectedRegionId = null;
        }

        var normalizedSelectedRegionId = output?.Regions.FirstOrDefault(region =>
            string.Equals(region.RegionId, selectedRegionId, StringComparison.OrdinalIgnoreCase))?.RegionId;
        if (ReferenceEquals(viewModel.ConnectedRegionOutput, output)
            && string.Equals(
                viewModel.SelectedConnectedRegionId,
                normalizedSelectedRegionId,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        viewModel.SetConnectedRegionOverlay(output, selectedRegionId);
        RenderNow();
    }

    public void ClearWorkbenchConnectedRegion() => SetConnectedRegionOverlay(null, null);

    private bool IsConnectedRegionCompatibleWithCurrentC3D(
        C3DConnectedRegionOutput output) =>
        c3dSample is not null
        && viewModel.C3DSampleVisible
        && output.GridWidth == c3dSample.Width
        && output.GridHeight == c3dSample.Height
        && string.Equals(
            output.InputContentSha256,
            c3dSample.ContentSha256,
            StringComparison.OrdinalIgnoreCase)
        && output.Regions.Count > 0;

    private void DrawWorkbenchConnectedRegion(OpenGL gl)
    {
        if (viewModel.ConnectedRegionOutput is not { } output
            || !IsConnectedRegionCompatibleWithCurrentC3D(output))
        {
            return;
        }

        foreach (var region in output.Regions)
        {
            var isSelected = string.Equals(
                region.RegionId,
                viewModel.SelectedConnectedRegionId,
                StringComparison.OrdinalIgnoreCase);
            foreach (var cell in region.Cells)
            {
                var rectangle = new ToolRecipeGridRectangle(cell.Row, cell.Column, 1, 1);
                if (isSelected)
                {
                    DrawTeachingGridRectangle(
                        gl,
                        rectangle,
                        1.0,
                        1.0,
                        1.0,
                        showHandles: false,
                        teachingSelectionId: null,
                        lineWidth: 6.0f);
                }

                DrawTeachingGridRectangle(
                    gl,
                    rectangle,
                    1.0,
                    0.72,
                    0.12,
                    showHandles: false,
                    teachingSelectionId: null,
                    lineWidth: isSelected ? 3.5f : 2.25f);
            }
        }
    }
}
