using System.Numerics;

namespace OpenVisionLab.ThreeD.Viewer.Rendering;

/// <summary>
/// WPF-neutral ray geometry used by Viewer picking adapters.
/// </summary>
internal static class ViewerRayGeometry
{
    public static bool TryProjectPoint(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        Vector3 point,
        out float alongRay,
        out float perpendicularDistance)
    {
        alongRay = Vector3.Dot(point - rayOrigin, rayDirection);
        if (alongRay < 0.0f)
        {
            perpendicularDistance = float.PositiveInfinity;
            return false;
        }

        var closestOnRay = rayOrigin + rayDirection * alongRay;
        perpendicularDistance = Vector3.Distance(point, closestOnRay);
        return true;
    }

    public static bool TryIntersectTriangle(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        Vector3 first,
        Vector3 second,
        Vector3 third,
        out float distance,
        out Vector3 hit)
    {
        const float Epsilon = 0.0000001f;

        distance = 0.0f;
        hit = default;

        var edge1 = second - first;
        var edge2 = third - first;
        var p = Vector3.Cross(rayDirection, edge2);
        var determinant = Vector3.Dot(edge1, p);
        if (Math.Abs(determinant) < Epsilon)
        {
            return false;
        }

        var inverseDeterminant = 1.0f / determinant;
        var t = rayOrigin - first;
        var u = Vector3.Dot(t, p) * inverseDeterminant;
        if (u < -Epsilon || u > 1.0f + Epsilon)
        {
            return false;
        }

        var q = Vector3.Cross(t, edge1);
        var v = Vector3.Dot(rayDirection, q) * inverseDeterminant;
        if (v < -Epsilon || u + v > 1.0f + Epsilon)
        {
            return false;
        }

        distance = Vector3.Dot(edge2, q) * inverseDeterminant;
        if (distance < 0.0f)
        {
            return false;
        }

        hit = rayOrigin + rayDirection * distance;
        return true;
    }

    public static Vector3 CalculateTriangleNormal(Vector3 first, Vector3 second, Vector3 third)
    {
        var normal = Vector3.Cross(second - first, third - first);
        return normal.LengthSquared() <= 0.000000000001f
            ? Vector3.Zero
            : Vector3.Normalize(normal);
    }
}
