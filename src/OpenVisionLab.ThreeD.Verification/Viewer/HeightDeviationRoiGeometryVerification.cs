using System.IO;
using System.Numerics;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.Recipes;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class HeightDeviationRoiGeometryVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Height-Deviation ROI geometry verification",
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

        var fromBounds = HeightDeviationRoiGeometry.FromBounds(
            new HeightDeviationRoiBounds(-2.0f, 6.0f, 1.0f, 9.0f));
        Check(
            "bounds conversion keeps center and half-size",
            fromBounds == new HeightDeviationRecipeRoiRegion(2.0, 5.0, 4.0, 4.0),
            $"region={fromBounds}");

        var degenerate = HeightDeviationRoiGeometry.FromBounds(
            new HeightDeviationRoiBounds(3.0f, 3.0f, -4.0f, -4.0f));
        Check(
            "degenerate bounds keep the existing minimum ROI size",
            degenerate.HalfWidth == 0.0001
            && degenerate.HalfDepth == 0.0001,
            $"halfWidth={degenerate.HalfWidth};halfDepth={degenerate.HalfDepth}");

        var original = new HeightDeviationRecipeRoiRegion(1.0, 2.0, 3.0, 4.0);
        var offset = HeightDeviationRoiGeometry.Offset(original, -0.5, 1.5);
        Check(
            "offset changes only the ROI center",
            offset == new HeightDeviationRecipeRoiRegion(0.5, 3.5, 3.0, 4.0),
            $"original={original};offset={offset}");

        Check(
            "finite positive ROI is valid",
            HeightDeviationRoiGeometry.IsValid(original),
            $"valid={HeightDeviationRoiGeometry.IsValid(original)}");
        Check(
            "zero or non-finite ROI size is rejected",
            !HeightDeviationRoiGeometry.IsValid(original with { HalfWidth = 0.0 })
            && !HeightDeviationRoiGeometry.IsValid(original with { HalfDepth = double.NaN }),
            "zero width and NaN depth are rejected");

        var separated = new HeightDeviationRecipeRoiRegion(8.0, 2.0, 3.0, 4.0);
        var touching = new HeightDeviationRecipeRoiRegion(7.0, 2.0, 3.0, 4.0);
        Check(
            "overlap uses strict interior intersection",
            !HeightDeviationRoiGeometry.Overlaps(original, separated)
            && !HeightDeviationRoiGeometry.Overlaps(original, touching)
            && HeightDeviationRoiGeometry.Overlaps(
                original,
                new HeightDeviationRecipeRoiRegion(3.0, 2.0, 3.0, 4.0)),
            "separated and edge-touching regions do not overlap");

        var sceneBounds = new HeightDeviationRoiBounds(0.0f, 10.0f, 0.0f, 10.0f);
        Check(
            "bounds intersection includes the boundary",
            HeightDeviationRoiGeometry.Intersects(
                new HeightDeviationRecipeRoiRegion(-1.0, 5.0, 1.0, 1.0),
                sceneBounds),
            "left edge touches scene bounds");
        Check(
            "bounds intersection rejects an outside region",
            !HeightDeviationRoiGeometry.Intersects(
                new HeightDeviationRecipeRoiRegion(-3.0, 5.0, 1.0, 1.0),
                sceneBounds),
            "region is fully left of scene bounds");

        var containmentRegion = new HeightDeviationRecipeRoiRegion(1.0, 2.0, 3.0, 4.0);
        Check(
            "point containment includes interior and every ROI boundary",
            HeightDeviationRoiGeometry.Contains(containmentRegion, new Vector3(1.0f, 0.0f, 2.0f))
            && HeightDeviationRoiGeometry.Contains(containmentRegion, new Vector3(4.0f, 0.0f, 2.0f))
            && HeightDeviationRoiGeometry.Contains(containmentRegion, new Vector3(-2.0f, 0.0f, 2.0f))
            && HeightDeviationRoiGeometry.Contains(containmentRegion, new Vector3(1.0f, 0.0f, 6.0f))
            && HeightDeviationRoiGeometry.Contains(containmentRegion, new Vector3(1.0f, 0.0f, -2.0f)),
            "interior, left, right, near, and far edges are included");
        Check(
            "point containment rejects points outside the ROI",
            !HeightDeviationRoiGeometry.Contains(containmentRegion, new Vector3(4.001f, 0.0f, 2.0f))
            && !HeightDeviationRoiGeometry.Contains(containmentRegion, new Vector3(1.0f, 0.0f, 6.001f)),
            "points beyond the right and far edges are rejected");

        var succeeded = passed == total;
        var result = $"HeightDeviationRoiGeometry: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)";
        lines.Add(result);
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"HeightDeviationRoiGeometry|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }
}
