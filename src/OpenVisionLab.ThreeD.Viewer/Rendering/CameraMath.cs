using System.Globalization;
using System.Numerics;
using WindowsPoint = System.Windows.Point;
using WindowsVector = System.Windows.Vector;

namespace OpenVisionLab.ThreeD.Viewer.Rendering;

public static class CameraMath
{
    public const float NearPlane = 0.01f;

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

        var view = Matrix4x4.CreateLookAt(eye, target, CameraUp(eye, target));
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(
            (float)DegreesToRadians(fieldOfViewDegrees),
            width / height,
            NearPlane,
            Math.Max(100.0f, Vector3.Distance(eye, target) + 300.0f));

        Matrix4x4.Invert(view * projection, out var inverseViewProjection);

        var near = Unproject(new Vector4(x, y, 0.0f, 1.0f), inverseViewProjection);
        var far = Unproject(new Vector4(x, y, 1.0f, 1.0f), inverseViewProjection);
        return (near, Vector3.Normalize(far - near));
    }

    public static (Vector3 origin, Vector3 direction) CreateOrthographicPickRay(
        WindowsPoint screenPoint,
        double viewportWidth,
        double viewportHeight,
        double orthographicHeight,
        Vector3 eye,
        Vector3 target)
    {
        var width = (float)Math.Max(1.0, viewportWidth);
        var height = (float)Math.Max(1.0, viewportHeight);
        var x = (float)(2.0 * screenPoint.X / width - 1.0);
        var y = (float)(1.0 - 2.0 * screenPoint.Y / height);
        var worldHeight = (float)Math.Max(0.01, orthographicHeight);
        var view = Matrix4x4.CreateLookAt(eye, target, CameraUp(eye, target));
        var projection = Matrix4x4.CreateOrthographic(
            worldHeight * width / height,
            worldHeight,
            NearPlane,
            Math.Max(100.0f, Vector3.Distance(eye, target) + 300.0f));

        Matrix4x4.Invert(view * projection, out var inverseViewProjection);
        var near = Unproject(new Vector4(x, y, 0.0f, 1.0f), inverseViewProjection);
        var far = Unproject(new Vector4(x, y, 1.0f, 1.0f), inverseViewProjection);
        return (near, Vector3.Normalize(far - near));
    }

    public static bool TryProjectWorldPositionToScreen(
        Vector3 worldPosition,
        double viewportWidth,
        double viewportHeight,
        double fieldOfViewDegrees,
        Vector3 eye,
        Vector3 target,
        out WindowsPoint screenPoint)
    {
        screenPoint = default;
        if (!IsFinite(worldPosition)
            || viewportWidth <= 0.0
            || viewportHeight <= 0.0)
        {
            return false;
        }

        var width = (float)Math.Max(1.0, viewportWidth);
        var height = (float)Math.Max(1.0, viewportHeight);
        var view = Matrix4x4.CreateLookAt(eye, target, CameraUp(eye, target));
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(
            (float)DegreesToRadians(fieldOfViewDegrees),
            width / height,
            NearPlane,
            Math.Max(100.0f, Vector3.Distance(eye, target) + 300.0f));
        var clip = Vector4.Transform(new Vector4(worldPosition, 1.0f), view * projection);
        if (!float.IsFinite(clip.W) || clip.W <= 0.000001f)
        {
            return false;
        }

        var inverseW = 1.0f / clip.W;
        var normalizedX = clip.X * inverseW;
        var normalizedY = clip.Y * inverseW;
        if (!float.IsFinite(normalizedX) || !float.IsFinite(normalizedY))
        {
            return false;
        }

        screenPoint = new WindowsPoint(
            (normalizedX + 1.0f) * 0.5f * width,
            (1.0f - normalizedY) * 0.5f * height);
        return true;
    }

    public static bool TryProjectWorldPositionToOrthographicScreen(
        Vector3 worldPosition,
        double viewportWidth,
        double viewportHeight,
        double orthographicHeight,
        Vector3 eye,
        Vector3 target,
        out WindowsPoint screenPoint)
    {
        screenPoint = default;
        if (!IsFinite(worldPosition)
            || viewportWidth <= 0.0
            || viewportHeight <= 0.0
            || !double.IsFinite(orthographicHeight)
            || orthographicHeight <= 0.0)
        {
            return false;
        }

        var width = (float)Math.Max(1.0, viewportWidth);
        var height = (float)Math.Max(1.0, viewportHeight);
        var worldHeight = (float)orthographicHeight;
        var view = Matrix4x4.CreateLookAt(eye, target, CameraUp(eye, target));
        var projection = Matrix4x4.CreateOrthographic(
            worldHeight * width / height,
            worldHeight,
            NearPlane,
            Math.Max(100.0f, Vector3.Distance(eye, target) + 300.0f));
        var clip = Vector4.Transform(new Vector4(worldPosition, 1.0f), view * projection);
        if (!float.IsFinite(clip.W) || Math.Abs(clip.W) <= 0.000001f)
        {
            return false;
        }

        var inverseW = 1.0f / clip.W;
        var normalizedX = clip.X * inverseW;
        var normalizedY = clip.Y * inverseW;
        if (!float.IsFinite(normalizedX) || !float.IsFinite(normalizedY))
        {
            return false;
        }

        screenPoint = new WindowsPoint(
            (normalizedX + 1.0f) * 0.5f * width,
            (1.0f - normalizedY) * 0.5f * height);
        return true;
    }

    public static (Vector3 Target, double Distance) FitPositions(
        IReadOnlyList<Vector3> positions,
        double yawDegrees,
        double pitchDegrees,
        double fieldOfViewDegrees,
        double viewportAspect,
        double padding = 1.08)
    {
        ArgumentNullException.ThrowIfNull(positions);
        if (positions.Count == 0)
        {
            throw new ArgumentException("At least one position is required.", nameof(positions));
        }

        if (!double.IsFinite(yawDegrees)
            || !double.IsFinite(pitchDegrees)
            || !double.IsFinite(fieldOfViewDegrees)
            || fieldOfViewDegrees is <= 1.0 or >= 179.0
            || !double.IsFinite(viewportAspect)
            || viewportAspect <= 0.0
            || !double.IsFinite(padding)
            || padding < 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(fieldOfViewDegrees), "Camera fit arguments are invalid.");
        }

        var minimum = positions[0];
        var maximum = positions[0];
        foreach (var position in positions)
        {
            if (!IsFinite(position))
            {
                throw new ArgumentException("Camera fit positions must be finite.", nameof(positions));
            }

            minimum = Vector3.Min(minimum, position);
            maximum = Vector3.Max(maximum, position);
        }

        var target = (minimum + maximum) * 0.5f;
        var eyeDirection = Vector3.Normalize(OrbitCameraPosition(
            Vector3.Zero,
            yawDegrees,
            pitchDegrees,
            1.0));
        var forward = -eyeDirection;
        var (right, up) = CameraBasis(forward);
        var verticalHalfAngle = DegreesToRadians(fieldOfViewDegrees) * 0.5;
        var horizontalHalfAngle = Math.Atan(Math.Tan(verticalHalfAngle) * viewportAspect);
        var horizontalScale = Math.Tan(horizontalHalfAngle);
        var verticalScale = Math.Tan(verticalHalfAngle);
        var requiredDistance = 0.0;
        var nearestDepthGuard = 0.0;

        foreach (var position in positions)
        {
            var relative = position - target;
            var towardEye = Vector3.Dot(relative, eyeDirection);
            nearestDepthGuard = Math.Max(nearestDepthGuard, towardEye + NearPlane * 4.0);
            requiredDistance = Math.Max(
                requiredDistance,
                towardEye + Math.Abs(Vector3.Dot(relative, right)) / horizontalScale);
            requiredDistance = Math.Max(
                requiredDistance,
                towardEye + Math.Abs(Vector3.Dot(relative, up)) / verticalScale);
        }

        return (target, Math.Max(0.05, Math.Max(requiredDistance, nearestDepthGuard) * padding));
    }

    public static (Vector3 Target, double Height, double Distance) FitOrthographicPositions(
        IReadOnlyList<Vector3> positions,
        double yawDegrees,
        double pitchDegrees,
        double viewportAspect,
        double padding = 1.12)
    {
        ArgumentNullException.ThrowIfNull(positions);
        if (positions.Count == 0)
        {
            throw new ArgumentException("At least one position is required.", nameof(positions));
        }

        if (!double.IsFinite(yawDegrees)
            || !double.IsFinite(pitchDegrees)
            || !double.IsFinite(viewportAspect)
            || viewportAspect <= 0.0
            || !double.IsFinite(padding)
            || padding < 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(viewportAspect), "Orthographic fit arguments are invalid.");
        }

        var minimum = positions[0];
        var maximum = positions[0];
        foreach (var position in positions)
        {
            if (!IsFinite(position))
            {
                throw new ArgumentException("Camera fit positions must be finite.", nameof(positions));
            }

            minimum = Vector3.Min(minimum, position);
            maximum = Vector3.Max(maximum, position);
        }

        var target = (minimum + maximum) * 0.5f;
        var eyeDirection = Vector3.Normalize(OrbitCameraPosition(
            Vector3.Zero,
            yawDegrees,
            pitchDegrees,
            1.0));
        var forward = -eyeDirection;
        var (right, up) = CameraBasis(forward);
        var horizontalExtent = 0.0;
        var verticalExtent = 0.0;
        var depthExtent = 0.0;
        foreach (var position in positions)
        {
            var relative = position - target;
            horizontalExtent = Math.Max(horizontalExtent, Math.Abs(Vector3.Dot(relative, right)));
            verticalExtent = Math.Max(verticalExtent, Math.Abs(Vector3.Dot(relative, up)));
            depthExtent = Math.Max(depthExtent, Math.Abs(Vector3.Dot(relative, eyeDirection)));
        }

        var height = Math.Max(
            0.05,
            2.0 * Math.Max(verticalExtent, horizontalExtent / viewportAspect) * padding);
        var distance = Math.Max(0.05, depthExtent + height);
        return (target, height, distance);
    }

    public static Vector3 PanDelta(
        WindowsVector delta,
        double viewportHeight,
        double fieldOfViewDegrees,
        double cameraDistance,
        Vector3 target,
        Vector3 eye)
    {
        return PanDelta(
            delta,
            viewportHeight,
            fieldOfViewDegrees,
            cameraDistance,
            target,
            eye,
            orthographicHeight: null);
    }

    public static Vector3 PanDelta(
        WindowsVector delta,
        double viewportHeight,
        double fieldOfViewDegrees,
        double cameraDistance,
        Vector3 target,
        Vector3 eye,
        double? orthographicHeight)
    {
        var forward = Vector3.Normalize(target - eye);
        var (right, up) = CameraBasis(forward);
        var worldPerPixel = orthographicHeight is { } height
            ? Math.Max(0.01, height) / Math.Max(1.0, viewportHeight)
            : 2.0 * cameraDistance * Math.Tan(DegreesToRadians(fieldOfViewDegrees) / 2.0) / Math.Max(1.0, viewportHeight);
        return right * (float)(-delta.X * worldPerPixel) + up * (float)(delta.Y * worldPerPixel);
    }

    public static Vector2 ProjectWorldDirectionToScreen(
        Vector3 worldDirection,
        Vector3 eye,
        Vector3 target)
    {
        var forward = Vector3.Normalize(target - eye);
        var (right, up) = CameraBasis(forward);
        return new Vector2(
            Vector3.Dot(worldDirection, right),
            -Vector3.Dot(worldDirection, up));
    }

    public static Vector3 CameraUp(Vector3 eye, Vector3 target)
    {
        var forward = Vector3.Normalize(target - eye);
        return CameraBasis(forward).Up;
    }

    private static (Vector3 Right, Vector3 Up) CameraBasis(Vector3 forward)
    {
        var referenceUp = Math.Abs(Vector3.Dot(forward, Vector3.UnitY)) > 0.999f
            ? -Vector3.UnitZ
            : Vector3.UnitY;
        var right = Vector3.Normalize(Vector3.Cross(forward, referenceUp));
        var up = Vector3.Normalize(Vector3.Cross(right, forward));
        return (right, up);
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

    private static bool IsFinite(Vector3 vector) =>
        float.IsFinite(vector.X)
        && float.IsFinite(vector.Y)
        && float.IsFinite(vector.Z);
}
