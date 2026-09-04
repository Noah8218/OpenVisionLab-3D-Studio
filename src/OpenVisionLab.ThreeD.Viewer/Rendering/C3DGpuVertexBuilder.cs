using System.Numerics;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Viewer.Rendering;

/// <summary>
/// Builds the managed XYZRGB payload consumed by the OpenGL buffer owner.
/// Color policy stays with the caller so this class does not depend on the
/// ViewModel or a rendering API.
/// </summary>
internal static class C3DGpuVertexBuilder
{
    public static float[] Build(
        IReadOnlyList<HeightGridPoint> points,
        IReadOnlyList<Vector3> positions,
        Func<HeightGridPoint, Vector3, (double R, double G, double B)> colorResolver)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(colorResolver);
        if (positions.Count != points.Count)
        {
            throw new ArgumentException(
                "C3D GPU positions must contain one position for every render point.",
                nameof(positions));
        }

        var vertices = new float[checked(points.Count * 6)];
        for (var index = 0; index < points.Count; index++)
        {
            var position = positions[index];
            var color = colorResolver(points[index], position);
            var offset = index * 6;
            vertices[offset] = position.X;
            vertices[offset + 1] = position.Y;
            vertices[offset + 2] = position.Z;
            vertices[offset + 3] = (float)color.R;
            vertices[offset + 4] = (float)color.G;
            vertices[offset + 5] = (float)color.B;
        }

        return vertices;
    }
}
