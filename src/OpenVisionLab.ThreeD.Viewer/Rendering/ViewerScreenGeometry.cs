using System.Windows;

namespace OpenVisionLab.ThreeD.Viewer.Rendering;

/// <summary>
/// Small screen-space geometry policies shared by Viewer interaction adapters.
/// </summary>
internal static class ViewerScreenGeometry
{
    public static double DistanceToLineSegment(Point point, Point start, Point end)
    {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared;
        if (lengthSquared <= 0.000001)
        {
            return (point - start).Length;
        }

        var fromStart = point - start;
        var projection = Math.Clamp(
            (fromStart.X * segment.X + fromStart.Y * segment.Y) / lengthSquared,
            0.0,
            1.0);
        var nearest = start + segment * projection;
        return (point - nearest).Length;
    }

    public static bool IsPointInsideConvexQuadrilateral(
        Point point,
        Point first,
        Point second,
        Point third,
        Point fourth)
    {
        var signs = new[]
        {
            Cross(first, second, point),
            Cross(second, third, point),
            Cross(third, fourth, point),
            Cross(fourth, first, point)
        };
        return signs.All(value => value >= -0.001)
            || signs.All(value => value <= 0.001);
    }

    private static double Cross(Point start, Point end, Point point) =>
        (end.X - start.X) * (point.Y - start.Y)
        - (end.Y - start.Y) * (point.X - start.X);
}
