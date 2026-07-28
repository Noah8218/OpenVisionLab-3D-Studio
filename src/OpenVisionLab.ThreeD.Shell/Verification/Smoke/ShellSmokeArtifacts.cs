using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Shell.Views.Tooling;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

internal static class ShellSmokeArtifacts
{
    public static Window RefreshToolLabForCapture(Window window)
    {
        switch (window)
        {
            case FilterToolLabWindow filter:
                filter.RefreshViews();
                break;
            case HeightDifferenceEdgeToolLabWindow edge:
                edge.RefreshViews();
                break;
            case TwoPointLineToolLabWindow twoPointLine:
                twoPointLine.RefreshViews();
                break;
            case ThreePointPlaneToolLabWindow threePointPlane:
                threePointPlane.RefreshViews();
                break;
            case DatumPlaneDeviationToolLabWindow datumPlaneDeviation:
                datumPlaneDeviation.RefreshViews();
                break;
            case LineIntersectionToolLabWindow intersection:
                intersection.RefreshViews();
                break;
            case LandmarkCorrespondenceToolLabWindow correspondence:
                correspondence.RefreshViews();
                break;
            case XYZAffineSolveToolLabWindow affine:
                affine.RefreshViews();
                break;
            case XYZAffineApplyToolLabWindow apply:
                apply.RefreshViews();
                break;
            case RegridHeightMapToolLabWindow regrid:
                regrid.RefreshViews();
                break;
        }

        return window;
    }

    public static void WriteTextReport(string? path, IReadOnlyList<string> lines, bool withoutBom = false)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        if (withoutBom)
        {
            File.WriteAllLines(fullPath, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return;
        }

        File.WriteAllLines(fullPath, lines);
    }

    public static async Task<bool> CaptureWindowWithRetryAsync(
        Window window,
        string path,
        string? qualityReportPath,
        string scope)
    {
        const int maximumAttempts = 3;
        var fullPath = Path.GetFullPath(path);
        var qualityLines = new List<string>();
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            var previousRejectedPath = GetRejectedScreenshotPath(fullPath, attempt);
            if (File.Exists(previousRejectedPath))
            {
                File.Delete(previousRejectedPath);
            }
        }

        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            window.UpdateLayout();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

            var result = WpfScreenshotCapture.Capture(window);
            var qualityLine = $"{scope}Screenshot|attempt={attempt}|{result.Quality.Summary}";
            qualityLines.Add(qualityLine);
            Console.WriteLine(qualityLine);
            if (result.Quality.IsAcceptable)
            {
                WpfScreenshotCapture.Save(result.Bitmap, fullPath);
                qualityLines.Add($"{scope}ScreenshotResult|accepted=True|attempts={attempt}|screenshot={fullPath}");
                WriteTextReport(qualityReportPath, qualityLines);
                return true;
            }

            var rejectedPath = GetRejectedScreenshotPath(fullPath, attempt);
            WpfScreenshotCapture.Save(result.Bitmap, rejectedPath);
            await Task.Delay(250);
        }

        qualityLines.Add($"{scope}ScreenshotResult|accepted=False|attempts={maximumAttempts}|screenshot={fullPath}");
        WriteTextReport(qualityReportPath, qualityLines);
        return false;
    }

    private static string GetRejectedScreenshotPath(string fullPath, int attempt) =>
        Path.Combine(
            Path.GetDirectoryName(fullPath)!,
            $"{Path.GetFileNameWithoutExtension(fullPath)}.rejected-attempt-{attempt}{Path.GetExtension(fullPath)}");
}
