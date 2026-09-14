using System.IO;
using System.Numerics;
using System.Windows;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class ViewerInputGeometryVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer input geometry verification",
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

        var segmentDistance = ViewerScreenGeometry.DistanceToLineSegment(
            new Point(5.0, 2.0),
            new Point(0.0, 0.0),
            new Point(10.0, 0.0));
        Check(
            "screen segment projection uses perpendicular distance",
            Math.Abs(segmentDistance - 2.0) < 0.0001,
            $"distance={segmentDistance:G6}");

        var zeroLengthDistance = ViewerScreenGeometry.DistanceToLineSegment(
            new Point(4.0, 5.0),
            new Point(1.0, 1.0),
            new Point(1.0, 1.0));
        Check(
            "zero-length screen segment remains a point distance",
            Math.Abs(zeroLengthDistance - 5.0) < 0.0001,
            $"distance={zeroLengthDistance:G6}");

        var beforeDistance = ViewerScreenGeometry.DistanceToLineSegment(
            new Point(-3.0, 0.0),
            new Point(0.0, 0.0),
            new Point(10.0, 0.0));
        var afterDistance = ViewerScreenGeometry.DistanceToLineSegment(
            new Point(13.0, 0.0),
            new Point(0.0, 0.0),
            new Point(10.0, 0.0));
        Check(
            "screen segment projection clamps to both endpoints",
            Math.Abs(beforeDistance - 3.0) < 0.0001
                && Math.Abs(afterDistance - 3.0) < 0.0001,
            $"before={beforeDistance:G6}|after={afterDistance:G6}");

        var first = new Point(0.0, 0.0);
        var second = new Point(10.0, 0.0);
        var third = new Point(10.0, 10.0);
        var fourth = new Point(0.0, 10.0);
        Check(
            "convex screen quadrilateral accepts interior",
            ViewerScreenGeometry.IsPointInsideConvexQuadrilateral(
                new Point(5.0, 5.0),
                first,
                second,
                third,
                fourth),
            "inside=True");
        Check(
            "convex screen quadrilateral accepts boundary",
            ViewerScreenGeometry.IsPointInsideConvexQuadrilateral(
                new Point(0.0, 5.0),
                first,
                second,
                third,
                fourth),
            "boundary=True");
        Check(
            "convex screen quadrilateral rejects exterior",
            !ViewerScreenGeometry.IsPointInsideConvexQuadrilateral(
                new Point(12.0, 5.0),
                first,
                second,
                third,
                fourth),
            "outside=False");

        var gridOrigin = Vector3.Zero;
        var rowSpan = new Vector3(0.0f, 0.0f, 10.0f);
        var columnSpan = new Vector3(10.0f, 0.0f, 0.0f);
        var mapped = ViewerGridPickingGeometry.TryMapRayToGrid(
            new Vector3(5.0f, 10.0f, 7.0f),
            new Vector3(0.0f, -1.0f, 0.0f),
            gridOrigin,
            rowSpan,
            columnSpan,
            11,
            11,
            out var row,
            out var column);
        Check(
            "grid ray maps interior row and column",
            mapped && row == 7 && column == 5,
            $"mapped={mapped}|row={row}|column={column}");

        var cornerMapped = ViewerGridPickingGeometry.TryMapRayToGrid(
            new Vector3(10.0f, 10.0f, 0.0f),
            new Vector3(0.0f, -1.0f, 0.0f),
            gridOrigin,
            rowSpan,
            columnSpan,
            11,
            11,
            out row,
            out column);
        Check(
            "grid ray maps a corner",
            cornerMapped && row == 0 && column == 10,
            $"mapped={cornerMapped}|row={row}|column={column}");

        var clamped = ViewerGridPickingGeometry.TryMapRayToGrid(
            new Vector3(20.0f, 10.0f, -5.0f),
            new Vector3(0.0f, -1.0f, 0.0f),
            gridOrigin,
            rowSpan,
            columnSpan,
            11,
            11,
            out row,
            out column);
        Check(
            "grid ray clamps outside footprint",
            clamped && row == 0 && column == 10,
            $"mapped={clamped}|row={row}|column={column}");

        var parallel = ViewerGridPickingGeometry.TryMapRayToGrid(
            new Vector3(5.0f, 10.0f, 5.0f),
            Vector3.UnitX,
            gridOrigin,
            rowSpan,
            columnSpan,
            11,
            11,
            out _,
            out _);
        Check("parallel grid ray is rejected", !parallel, $"mapped={parallel}");

        var behind = ViewerGridPickingGeometry.TryMapRayToGrid(
            new Vector3(5.0f, -1.0f, 5.0f),
            new Vector3(0.0f, -1.0f, 0.0f),
            gridOrigin,
            rowSpan,
            columnSpan,
            11,
            11,
            out _,
            out _);
        Check("grid plane behind ray is rejected", !behind, $"mapped={behind}");

        var degenerate = ViewerGridPickingGeometry.TryMapRayToGrid(
            new Vector3(5.0f, 10.0f, 5.0f),
            new Vector3(0.0f, -1.0f, 0.0f),
            gridOrigin,
            Vector3.Zero,
            columnSpan,
            11,
            11,
            out _,
            out _);
        Check("degenerate grid span is rejected", !degenerate, $"mapped={degenerate}");

        var invalidDimensions = ViewerGridPickingGeometry.TryMapRayToGrid(
            new Vector3(5.0f, 10.0f, 5.0f),
            new Vector3(0.0f, -1.0f, 0.0f),
            gridOrigin,
            rowSpan,
            columnSpan,
            1,
            11,
            out _,
            out _);
        Check(
            "grid dimensions below two are rejected",
            !invalidDimensions,
            $"mapped={invalidDimensions}");

        summary = $"Viewer input geometry verification: {(passed == total ? "Pass" : "Fail")} ({passed}/{total} checks)";
        lines.Add(summary);
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        return passed == total;
    }
}
