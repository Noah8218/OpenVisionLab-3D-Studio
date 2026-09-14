using System.IO;
using System.Numerics;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class ViewerRayGeometryVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer ray geometry verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;
        var total = 0;

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        var origin = Vector3.Zero;
        var direction = Vector3.UnitZ;
        var projected = ViewerRayGeometry.TryProjectPoint(
            origin,
            direction,
            new Vector3(1.0f, 0.0f, 5.0f),
            out var alongRay,
            out var perpendicularDistance);
        Check(
            "front point projects",
            projected && Math.Abs(alongRay - 5.0f) < 0.0001f
                && Math.Abs(perpendicularDistance - 1.0f) < 0.0001f,
            $"accepted={projected}|along={alongRay:G6}|distance={perpendicularDistance:G6}");

        var onRay = ViewerRayGeometry.TryProjectPoint(
            origin,
            direction,
            new Vector3(0.0f, 0.0f, 2.0f),
            out alongRay,
            out perpendicularDistance);
        Check(
            "point on ray has zero perpendicular distance",
            onRay && Math.Abs(alongRay - 2.0f) < 0.0001f
                && perpendicularDistance == 0.0f,
            $"accepted={onRay}|along={alongRay:G6}|distance={perpendicularDistance:G6}");

        var behind = ViewerRayGeometry.TryProjectPoint(
            origin,
            direction,
            new Vector3(0.0f, 0.0f, -1.0f),
            out alongRay,
            out _);
        Check(
            "point behind ray is rejected",
            !behind && alongRay < 0.0f,
            $"accepted={behind}|along={alongRay:G6}");

        var first = new Vector3(-1.0f, -1.0f, 5.0f);
        var second = new Vector3(1.0f, -1.0f, 5.0f);
        var third = new Vector3(0.0f, 1.0f, 5.0f);
        var triangleHit = ViewerRayGeometry.TryIntersectTriangle(
            origin,
            direction,
            first,
            second,
            third,
            out var triangleDistance,
            out var trianglePoint);
        Check(
            "ray intersects triangle",
            triangleHit && Math.Abs(triangleDistance - 5.0f) < 0.0001f
                && Math.Abs(trianglePoint.Z - 5.0f) < 0.0001f,
            $"hit={triangleHit}|distance={triangleDistance:G6}|pointZ={trianglePoint.Z:G6}");

        var outsideHit = ViewerRayGeometry.TryIntersectTriangle(
            new Vector3(2.0f, 2.0f, 0.0f),
            direction,
            first,
            second,
            third,
            out _,
            out _);
        Check("outside ray misses triangle", !outsideHit, $"hit={outsideHit}");

        var parallelHit = ViewerRayGeometry.TryIntersectTriangle(
            origin,
            Vector3.UnitX,
            first,
            second,
            third,
            out _,
            out _);
        Check("parallel ray misses triangle", !parallelHit, $"hit={parallelHit}");

        var backwardHit = ViewerRayGeometry.TryIntersectTriangle(
            new Vector3(0.0f, 0.0f, 10.0f),
            direction,
            first,
            second,
            third,
            out _,
            out _);
        Check("triangle behind ray is rejected", !backwardHit, $"hit={backwardHit}");

        var normal = ViewerRayGeometry.CalculateTriangleNormal(first, second, third);
        Check(
            "triangle normal is normalized",
            Math.Abs(normal.Length() - 1.0f) < 0.0001f && normal.Z > 0.99f,
            $"length={normal.Length():G6}|z={normal.Z:G6}");

        var degenerateNormal = ViewerRayGeometry.CalculateTriangleNormal(first, first, first);
        Check(
            "degenerate triangle normal is zero",
            degenerateNormal == Vector3.Zero,
            $"normal={degenerateNormal}");

        summary = $"Viewer ray geometry verification: {(passed == total ? "Pass" : "Fail")} ({passed}/{total} checks)";
        lines.Add(summary);
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        return passed == total;
    }
}
