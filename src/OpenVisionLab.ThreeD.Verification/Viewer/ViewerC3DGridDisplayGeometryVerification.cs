using System.IO;
using System.Numerics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class ViewerC3DGridDisplayGeometryVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer C3D display geometry verification",
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

        var center = ViewerC3DGridDisplayGeometry.CreatePosition(
            3,
            3,
            5.0f,
            C3DHeightGrid.ViewerHeightScale,
            30.0,
            1.0,
            1.0,
            30.0,
            ModelTransform.Identity);
        Check(
            "grid center at mean maps to display origin",
            center == Vector3.Zero,
            $"position={center}");

        var corner = ViewerC3DGridDisplayGeometry.CreatePosition(
            3,
            2,
            5.0f,
            C3DHeightGrid.ViewerHeightScale,
            30.0,
            0.0,
            0.0,
            10.0,
            ModelTransform.Identity);
        Check(
            "raw grid frame maps column to X and row to Z",
            Approximately(corner.X, -5.0f)
                && Approximately(corner.Y, -0.012f)
                && Approximately(corner.Z, -2.5f),
            $"position={corner}");

        var interpolated = ViewerC3DGridDisplayGeometry.CreatePosition(
            5,
            5,
            2.0f,
            0.5f,
            100.0,
            2.5,
            1.5,
            104.0,
            ModelTransform.Identity);
        Check(
            "fractional row, column, and raw-height preserve scale",
            Approximately(interpolated.X, -1.0f)
                && Approximately(interpolated.Y, 2.0f)
                && Approximately(interpolated.Z, 1.0f),
            $"position={interpolated}");

        var translated = ViewerC3DGridDisplayGeometry.CreatePosition(
            3,
            3,
            5.0f,
            C3DHeightGrid.ViewerHeightScale,
            30.0,
            1.0,
            1.0,
            30.0,
            new ModelTransform(1.0, 2.0, 3.0, 0.0, 0.0, 0.0, 1.0));
        Check(
            "model transform is applied after display-frame mapping",
            translated == new Vector3(1.0f, 2.0f, 3.0f),
            $"position={translated}");

        var rotated = ViewerC3DGridDisplayGeometry.CreatePosition(
            3,
            3,
            5.0f,
            C3DHeightGrid.ViewerHeightScale,
            30.0,
            1.0,
            2.0,
            30.0,
            new ModelTransform(0.0, 0.0, 0.0, 0.0, 0.0, 90.0, 1.0));
        Check(
            "rotation follows the shared ModelTransform order",
            Approximately(rotated.X, 0.0f)
                && Approximately(rotated.Y, 5.0f)
                && Approximately(rotated.Z, 0.0f),
            $"position={rotated}");

        summary = $"Viewer C3D display geometry verification: {(passed == total ? "Pass" : "Fail")} ({passed}/{total} checks)";
        lines.Add(summary);
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        return passed == total;
    }

    private static bool Approximately(float actual, float expected) =>
        Math.Abs(actual - expected) < 0.0001f;
}
