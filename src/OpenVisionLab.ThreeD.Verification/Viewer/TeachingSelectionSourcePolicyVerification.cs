using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.Teaching;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class TeachingSelectionSourcePolicyVerification
{
    public static bool Verify(out string summary)
    {
        const string currentSha = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        var source = new TeachingCaptureSourceSnapshot(64, 48, 0.25f, 42.0, currentSha);
        var currentBinding = new ToolRecipeSelectionSourceBinding("C3D", currentSha, 64, 48);
        var currentSelection = CreateSelection(currentBinding);
        var checks = new (string Name, bool Passed, string Details)[]
        {
            (
                "current C3D source matches",
                TeachingSelectionSourcePolicy.IsCurrentC3DGrid(currentSelection, source),
                "same format, SHA-256, and grid dimensions"),
            (
                "stale SHA-256 is rejected",
                !TeachingSelectionSourcePolicy.IsCurrentC3DGrid(
                    currentSelection with
                    {
                        SourceBinding = currentBinding with
                        {
                            ContentSha256 = new string('B', 64)
                        }
                    },
                    source),
                "source bytes identify a different C3D"),
            (
                "stale grid width is rejected",
                !TeachingSelectionSourcePolicy.IsCurrentC3DGrid(
                    currentSelection with
                    {
                        SourceBinding = currentBinding with { GridWidth = 63 }
                    },
                    source),
                "selection grid no longer matches"),
            (
                "stale grid height is rejected",
                !TeachingSelectionSourcePolicy.IsCurrentC3DGrid(
                    currentSelection with
                    {
                        SourceBinding = currentBinding with { GridHeight = 47 }
                    },
                    source),
                "selection grid no longer matches"),
            (
                "non-C3D source is rejected",
                !TeachingSelectionSourcePolicy.IsCurrentC3DGrid(
                    currentSelection with
                    {
                        SourceBinding = currentBinding with { Format = "TransformedHeightField" }
                    },
                    source),
                "Viewer C3D overlays must not consume transformed-field bindings"),
            (
                "optional metadata remains compatibility-neutral",
                TeachingSelectionSourcePolicy.IsCurrentC3DGrid(
                    currentSelection with
                    {
                        SourceBinding = currentBinding with
                        {
                            OwnerEntityId = "legacy-owner",
                            Unit = "legacy-unit",
                            FrameId = "legacy-frame"
                        }
                    },
                    source),
                "current behavior compares C3D format, bytes, and dimensions only")
        };

        var passed = checks.Count(check => check.Passed);
        var lines = new List<string>
        {
            $"TeachingSelectionSourcePolicy: {(passed == checks.Length ? "Pass" : "Fail")} ({passed}/{checks.Length} checks)"
        };
        lines.AddRange(checks.Select(check =>
            $"{(check.Passed ? "PASS" : "FAIL")} | {check.Name} | {check.Details}"));
        summary = string.Join(Environment.NewLine, lines);
        return passed == checks.Length;
    }

    private static ToolRecipeSelection CreateSelection(
        ToolRecipeSelectionSourceBinding binding) =>
        new(
            "selection.c3d",
            "C3D selection",
            ToolRecipeSelectionKinds.GridRectangle,
            "source.c3d",
            "frame.c3d-grid-index",
            binding,
            new ToolRecipeGridRectangle(1, 1, 2, 2),
            null,
            null);
}
