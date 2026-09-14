using System.Numerics;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer.Rendering;

/// <summary>
/// Builds the Viewer display-frame position for a C3D raw-height sample.
/// </summary>
internal static class ViewerC3DGridDisplayGeometry
{
    public static Vector3 CreatePosition(
        int width,
        int height,
        float horizontalScale,
        float heightScale,
        double mean,
        double row,
        double column,
        double rawHeight,
        ModelTransform modelTransform)
    {
        var centerColumn = (width - 1) / 2.0f;
        var centerRow = (height - 1) / 2.0f;
        var position = new Vector3(
            (float)((column - centerColumn) * horizontalScale),
            (float)((rawHeight - mean) * heightScale),
            (float)((row - centerRow) * horizontalScale));
        return modelTransform.Apply(position);
    }
}
