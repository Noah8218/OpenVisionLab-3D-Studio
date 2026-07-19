using System.Globalization;
using System.Numerics;
using WindowsPoint = System.Windows.Point;
using WindowsVector = System.Windows.Vector;

namespace OpenVisionLab.ThreeD.Viewer.Rendering;

public static class CameraMath
{
    public static Vector3 CameraTarget(double x, double y, double z) =>
        new((float)x, (float)y, (float)z);

    public static Vector3 OrbitCameraPosition(Vector3 target, double yawDegrees, double pitchDegrees, double distance)
    {
        var yaw = DegreesToRadians(yawDegrees);
        var pitch = DegreesToRadians(pitchDegrees);
        var x = distance * Math.Cos(pitch) * Math.Sin(yaw);
        var y = distance * Math.Sin(pitch);
        var z = distance * Math.Cos(pitch) * Math.Cos(yaw);
        return target + new Vector3((float)x, (float)y, (float)z);
    }

    public static (Vector3 origin, Vector3 direction) CreatePickRay(
        WindowsPoint screenPoint,
        double viewportWidth,
        double viewportHeight,
        double fieldOfViewDegrees,
        Vector3 eye,
        Vector3 target)
    {
        var width = (float)Math.Max(1.0, viewportWidth);
        var height = (float)Math.Max(1.0, viewportHeight);
        var x = (float)(2.0 * screenPoint.X / width - 1.0);
        var y = (float)(1.0 - 2.0 * screenPoint.Y / height);

        var view = Matrix4x4.CreateLookAt(eye, target, Vector3.UnitY);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(
            (float)DegreesToRadians(fieldOfViewDegrees),
            width / height,
            0.1f,
            100.0f);

        Matrix4x4.Invert(view * projection, out var inverseViewProjection);

        var near = Unproject(new Vector4(x, y, 0.0f, 1.0f), inverseViewProjection);
        var far = Unproject(new Vector4(x, y, 1.0f, 1.0f), inverseViewProjection);
        return (near, Vector3.Normalize(far - near));
    }

    public static Vector3 PanDelta(
        WindowsVector delta,
        double viewportHeight,
        double fieldOfViewDegrees,
        double cameraDistance,
        Vector3 target,
        Vector3 eye)
    {
        var forward = Vector3.Normalize(target - eye);
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        var up = Vector3.Normalize(Vector3.Cross(right, forward));
        var worldPerPixel = 2.0 * cameraDistance * Math.Tan(DegreesToRadians(fieldOfViewDegrees) / 2.0) / Math.Max(1.0, viewportHeight);
        return right * (float)(-delta.X * worldPerPixel) + up * (float)(delta.Y * worldPerPixel);
    }

    public static Vector2 ProjectWorldDirectionToScreen(
        Vector3 worldDirection,
        Vector3 eye,
        Vector3 target)
    {
        var forward = Vector3.Normalize(target - eye);
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        var up = Vector3.Normalize(Vector3.Cross(right, forward));
        return new Vector2(
            Vector3.Dot(worldDirection, right),
            -Vector3.Dot(worldDirection, up));
    }

    public static bool IntersectUnitCube(Vector3 origin, Vector3 direction, float halfSize, out float distance)
    {
        distance = 0;
        var min = new Vector3(-halfSize, -halfSize, -halfSize);
        var max = new Vector3(halfSize, halfSize, halfSize);
        var tMin = 0.0f;
        var tMax = float.PositiveInfinity;

        for (var axis = 0; axis < 3; axis++)
        {
            var axisOrigin = GetAxis(origin, axis);
            var axisDirection = GetAxis(direction, axis);
            var axisMin = GetAxis(min, axis);
            var axisMax = GetAxis(max, axis);

            if (Math.Abs(axisDirection) < 0.00001f)
            {
                if (axisOrigin < axisMin || axisOrigin > axisMax)
                {
                    return false;
                }

                continue;
            }

            var t1 = (axisMin - axisOrigin) / axisDirection;
            var t2 = (axisMax - axisOrigin) / axisDirection;
            if (t1 > t2)
            {
                (t1, t2) = (t2, t1);
            }

            tMin = Math.Max(tMin, t1);
            tMax = Math.Min(tMax, t2);
            if (tMin > tMax)
            {
                return false;
            }
        }

        distance = tMin;
        return true;
    }

    public static string FormatPoint(Vector3 point) =>
        string.Create(CultureInfo.InvariantCulture, $"({point.X:F3}, {point.Y:F3}, {point.Z:F3})");

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static Vector3 Unproject(Vector4 point, Matrix4x4 inverseViewProjection)
    {
        var transformed = Vector4.Transform(point, inverseViewProjection);
        if (Math.Abs(transformed.W) < 0.000001f)
        {
            return new Vector3(transformed.X, transformed.Y, transformed.Z);
        }

        return new Vector3(transformed.X, transformed.Y, transformed.Z) / transformed.W;
    }

    private static float GetAxis(Vector3 vector, int axis) => axis switch
    {
        0 => vector.X,
        1 => vector.Y,
        _ => vector.Z
    };
}
