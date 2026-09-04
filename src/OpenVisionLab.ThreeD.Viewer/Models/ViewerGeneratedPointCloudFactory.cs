using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Viewer.Models;

/// <summary>
/// Creates the deterministic synthetic point cloud used by the Viewer demo
/// source. It is sample data, not a measurement or calibration fixture.
/// </summary>
internal static class ViewerGeneratedPointCloudFactory
{
    public static HeightGridPoint[] Create()
    {
        const int columns = 55;
        const int rows = 41;
        var points = new HeightGridPoint[columns * rows];
        var index = 0;

        for (var row = 0; row < rows; row++)
        {
            var z = -2.0f + row * (4.0f / (rows - 1));
            for (var column = 0; column < columns; column++)
            {
                var localX = -2.2f + column * (4.4f / (columns - 1));
                var wave = 0.16 * Math.Sin(localX * 1.35) + 0.10 * Math.Cos(z * 1.8);
                var bump = 0.42 * Math.Exp(-((localX - 0.58) * (localX - 0.58) + (z + 0.32) * (z + 0.32)) / 0.32);
                var dent = -0.24 * Math.Exp(-((localX + 1.05) * (localX + 1.05) + (z - 0.88) * (z - 0.88)) / 0.24);
                var y = -0.70f + (float)(wave + bump + dent);
                var position = new System.Numerics.Vector3(localX + 3.2f, y, z);
                var heightScalar = Clamp01((y + 1.05) / 0.86);
                var deviationScalar = Clamp01(Math.Abs(bump + dent) / 0.42);
                points[index++] = new HeightGridPoint(position, heightScalar, deviationScalar, y);
            }
        }

        return points;
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
}
