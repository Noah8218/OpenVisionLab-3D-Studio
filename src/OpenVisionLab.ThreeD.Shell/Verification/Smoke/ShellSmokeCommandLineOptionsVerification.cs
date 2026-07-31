using System.IO;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

internal static class ShellSmokeCommandLineOptionsVerification
{
    private const string Option = "--verify-shell-smoke-command-line";

    public static bool TryRun(string[] arguments, out bool passed, out string summary)
    {
        var optionIndex = Array.FindIndex(
            arguments,
            argument => argument.Equals(Option, StringComparison.OrdinalIgnoreCase));
        if (optionIndex < 0)
        {
            passed = false;
            summary = string.Empty;
            return false;
        }

        if (optionIndex + 1 >= arguments.Length)
        {
            passed = false;
            summary = $"{Option} requires a report path.";
            return true;
        }

        var reportPath = Path.GetFullPath(arguments[optionIndex + 1]);
        var checks = new List<(string Name, bool Passed)>
        {
            ("ValuePath", VerifyValuePath()),
            ("MissingValue", VerifyMissingValue()),
            ("CaseInsensitiveFlag", VerifyCaseInsensitiveFlag()),
            ("FilterPublishImpliesPreview", VerifyFilterPublishImpliesPreview()),
            ("EdgeLineFitImpliesPreview", VerifyEdgeLineFitImpliesPreview()),
            ("WindowSize", VerifyWindowSize()),
            ("CompactWorkbench", VerifyCompactWorkbench()),
            ("LoadedHandler", VerifyLoadedHandler()),
            ("FilterPreviewLoadedHandler", VerifyFilterPreviewLoadedHandler()),
            ("MeasurementPreviewLoadedHandler", VerifyMeasurementPreviewLoadedHandler()),
            ("ThicknessRepeatGrid", VerifyThicknessRepeatGrid()),
            ("ThicknessRepeatGridLoadedHandler", VerifyThicknessRepeatGridLoadedHandler()),
            ("ViewerLayout", VerifyViewerLayout()),
            ("ViewerPopoutCaptureLoadedHandler", VerifyViewerPopoutCaptureLoadedHandler()),
            ("RemoveOutlierPreviewLoadedHandler", VerifyRemoveOutlierPreviewLoadedHandler()),
            ("LevelSurfacePreviewLoadedHandler", VerifyLevelSurfacePreviewLoadedHandler()),
            ("SourceQuality", VerifySourceQuality()),
            ("SourceQualityLoadedHandler", VerifySourceQualityLoadedHandler()),
            ("HeightImageDisplayRange", VerifyHeightImageDisplayRange()),
            ("HeightImageDisplayRangeLoadedHandler", VerifyHeightImageDisplayRangeLoadedHandler()),
            ("SharedHeightHover", VerifySharedHeightHover()),
            ("SharedHeightHoverLoadedHandler", VerifySharedHeightHoverLoadedHandler()),
            ("HeightImageRoiPointer", VerifyHeightImageRoiPointer()),
            ("OrientedBoxPointer", VerifyOrientedBoxPointer()),
            ("ExpandSelectedToolParameters", VerifyExpandSelectedToolParameters()),
            ("FocusSelectedToolParameterSearch", VerifyFocusSelectedToolParameterSearch())
        };
        passed = checks.All(check => check.Passed);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Shell smoke command-line options verification"
        };
        lines.AddRange(checks.Select(check => $"{check.Name}={(check.Passed ? "PASS" : "FAIL")}"));
        lines.Add($"Result={(passed ? "PASS" : "FAIL")}|{checks.Count(check => check.Passed)}/{checks.Count}");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllLines(reportPath, lines);
        summary = lines[^1];
        return true;
    }

    private static bool VerifyValuePath()
    {
        var options = ShellSmokeCommandLineOptions.Parse(
            ["shell.exe", "--shell-smoke-screenshot", "capture.png"]);
        return options.ShellScreenshotPath == "capture.png";
    }

    private static bool VerifyMissingValue()
    {
        var options = ShellSmokeCommandLineOptions.Parse(
            ["shell.exe", "--shell-smoke-screenshot"]);
        return options.ShellScreenshotPath is null;
    }

    private static bool VerifyCaseInsensitiveFlag()
    {
        var options = ShellSmokeCommandLineOptions.Parse(
            ["shell.exe", "--SMOKE-PUBLISH-RESULT"]);
        return options.SmokePublishResult;
    }

    private static bool VerifyFilterPublishImpliesPreview()
    {
        var options = ShellSmokeCommandLineOptions.Parse(
            ["shell.exe", "--smoke-tool-filter-publish"]);
        return options.FilterPublishSmoke && options.FilterPreviewSmoke;
    }

    private static bool VerifyEdgeLineFitImpliesPreview()
    {
        var options = ShellSmokeCommandLineOptions.Parse(
            ["shell.exe", "--smoke-tool-line-fit-preview"]);
        return options.LineFitPreviewSmoke && options.EdgePreviewSmoke;
    }

    private static bool VerifyWindowSize()
    {
        var options = ShellSmokeCommandLineOptions.Parse(
            ["shell.exe", "--shell-smoke-width", "1280", "--shell-smoke-height", "760"]);
        return options.WindowSize is { Width: 1280, Height: 760 };
    }

    private static bool VerifyCompactWorkbench()
    {
        var options = ShellSmokeCommandLineOptions.Parse(
            ["shell.exe", "--smoke-tool-teaching-selection", "capturing"]);
        return options.NeedsCompactWorkbench;
    }

    private static bool VerifyLoadedHandler()
    {
        var options = ShellSmokeCommandLineOptions.Parse(
            ["shell.exe", "--smoke-async-c3d-load", "sample.c3d"]);
        return options.ShouldAttachLoadedHandler(hasViewerSmokeScreenshot: false);
    }

    private static bool VerifyFilterPreviewLoadedHandler()
    {
        var options = ShellSmokeCommandLineOptions.Parse(
            ["shell.exe", "--smoke-tool-filter-preview"]);
        return options.FilterPreviewSmoke
            && options.ShouldAttachLoadedHandler(hasViewerSmokeScreenshot: false);
    }

    private static bool VerifyMeasurementPreviewLoadedHandler()
    {
        var options = ShellSmokeCommandLineOptions.Parse(
            ["shell.exe", "--smoke-tool-measurement-preview"]);
        return options.MeasurementPreviewSmoke
            && options.ShouldAttachLoadedHandler(hasViewerSmokeScreenshot: false);
    }

    private static bool VerifyThicknessRepeatGrid()
    {
        var options = ShellSmokeCommandLineOptions.Parse(
            ["shell.exe", "--smoke-thickness-repeat-grid", "review"]);
        return options.ThicknessRepeatGridSmoke == "review";
    }

    private static bool VerifyThicknessRepeatGridLoadedHandler()
    {
        var options = ShellSmokeCommandLineOptions.Parse(
            ["shell.exe", "--smoke-thickness-repeat-grid", "apply"]);
        return options.ThicknessRepeatGridSmoke == "apply"
            && options.ShouldAttachLoadedHandler(hasViewerSmokeScreenshot: false);
    }

    private static bool VerifyViewerLayout()
    {
        var options = ShellSmokeCommandLineOptions.Parse(
            ["shell.exe", "--smoke-viewer-layout", "vertical"]);
        return options.ViewerLayoutSmoke == "vertical"
            && options.ShouldAttachLoadedHandler(hasViewerSmokeScreenshot: false);
    }

    private static bool VerifyViewerPopoutCaptureLoadedHandler()
    {
        var options = ShellSmokeCommandLineOptions.Parse(
        [
            "shell.exe",
            "--viewer-popout-screenshot",
            "popout.png",
            "--viewer-popout-screenshot-quality-report",
            "popout-quality.txt"
        ]);
        return options.ViewerPopoutScreenshotPath == "popout.png"
            && options.ViewerPopoutScreenshotQualityReportPath == "popout-quality.txt"
            && options.ShouldAttachLoadedHandler(hasViewerSmokeScreenshot: false);
    }

    private static bool VerifyRemoveOutlierPreviewLoadedHandler()
    {
        var options = ShellSmokeCommandLineOptions.Parse(
            ["shell.exe", "--smoke-tool-remove-outlier-preview"]);
        return options.RemoveOutlierPreviewSmoke
               && options.NeedsCompactWorkbench
               && options.ShouldAttachLoadedHandler(false);
    }

    private static bool VerifyLevelSurfacePreviewLoadedHandler()
    {
        var options = ShellSmokeCommandLineOptions.Parse(
            ["shell.exe", "--smoke-tool-level-surface-preview"]);
        return options.LevelSurfacePreviewSmoke
               && options.NeedsCompactWorkbench
               && options.ShouldAttachLoadedHandler(false);
    }

    private static bool VerifyHeightImageRoiPointer()
    {
        var options = ShellSmokeCommandLineOptions.Parse(
        [
            "shell.exe",
            "--smoke-height-image-roi-pointer",
            "review",
            "--smoke-height-image-roi-pointer-report",
            "roi-pointer.txt",
            "--smoke-height-image-roi-pointer-save",
            "roi-pointer.ov3d-recipe.json"
        ]);
        return options.HeightImageRoiPointerSmoke == "review"
               && options.HeightImageRoiPointerSmokeReportPath == "roi-pointer.txt"
               && options.HeightImageRoiPointerSmokeSavePath == "roi-pointer.ov3d-recipe.json"
               && options.ShouldAttachLoadedHandler(hasViewerSmokeScreenshot: false);
    }

    private static bool VerifyOrientedBoxPointer()
    {
        var options = ShellSmokeCommandLineOptions.Parse(
        [
            "shell.exe",
            "--smoke-oriented-box-pointer-report",
            "oriented-box-pointer.txt"
        ]);
        return options.OrientedBoxPointerSmokeReportPath == "oriented-box-pointer.txt"
               && options.ShouldAttachLoadedHandler(hasViewerSmokeScreenshot: false);
    }

    private static bool VerifyExpandSelectedToolParameters()
    {
        var options = ShellSmokeCommandLineOptions.Parse(
            ["shell.exe", "--smoke-expand-selected-tool-parameters"]);
        return options.ExpandSelectedToolParametersSmoke
               && options.ShouldAttachLoadedHandler(hasViewerSmokeScreenshot: false);
    }

    private static bool VerifyFocusSelectedToolParameterSearch()
    {
        var options = ShellSmokeCommandLineOptions.Parse(
            ["shell.exe", "--smoke-focus-selected-tool-parameter-search"]);
        return options.FocusSelectedToolParameterSearchSmoke
               && options.ShouldAttachLoadedHandler(hasViewerSmokeScreenshot: false);
    }

    private static bool VerifySourceQuality()
    {
        var options = ShellSmokeCommandLineOptions.Parse(
        [
            "shell.exe",
            "--smoke-source-quality",
            "--smoke-source-quality-report",
            "quality.txt"
        ]);
        return options.SourceQualitySmoke
               && options.SourceQualitySmokeReportPath == "quality.txt";
    }

    private static bool VerifySourceQualityLoadedHandler()
    {
        var options = ShellSmokeCommandLineOptions.Parse(
            ["shell.exe", "--smoke-source-quality"]);
        return options.ShouldAttachLoadedHandler(hasViewerSmokeScreenshot: false);
    }

    private static bool VerifyHeightImageDisplayRange()
    {
        var options = ShellSmokeCommandLineOptions.Parse(
        [
            "shell.exe",
            "--smoke-height-image-display-range",
            "--smoke-height-image-palette",
            "Thermal",
            "--smoke-height-image-range-min",
            "-200.5",
            "--smoke-height-image-range-max",
            "1200.25",
            "--smoke-height-image-display-range-report",
            "range.txt"
        ]);
        return options.HeightImageDisplayRangeSmoke
               && options.HeightImagePaletteSmoke == "Thermal"
               && options.HeightImageRangeMinimumSmoke == -200.5
               && options.HeightImageRangeMaximumSmoke == 1200.25
               && options.HeightImageDisplayRangeSmokeReportPath == "range.txt";
    }

    private static bool VerifyHeightImageDisplayRangeLoadedHandler()
    {
        var options = ShellSmokeCommandLineOptions.Parse(
            ["shell.exe", "--smoke-height-image-display-range"]);
        return options.ShouldAttachLoadedHandler(hasViewerSmokeScreenshot: false);
    }

    private static bool VerifySharedHeightHover()
    {
        var options = ShellSmokeCommandLineOptions.Parse(
        [
            "shell.exe",
            "--smoke-shared-height-hover",
            "--smoke-shared-height-hover-row",
            "407",
            "--smoke-shared-height-hover-column",
            "593",
            "--smoke-shared-height-hover-report",
            "hover.txt"
        ]);
        return options.SharedHeightHoverSmoke
               && options.SharedHeightHoverRow == 407
               && options.SharedHeightHoverColumn == 593
               && options.SharedHeightHoverSmokeReportPath == "hover.txt";
    }

    private static bool VerifySharedHeightHoverLoadedHandler()
    {
        var options = ShellSmokeCommandLineOptions.Parse(
            ["shell.exe", "--smoke-shared-height-hover"]);
        return options.ShouldAttachLoadedHandler(hasViewerSmokeScreenshot: false);
    }
}
