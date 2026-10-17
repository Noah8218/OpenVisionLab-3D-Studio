using System.IO;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Verification;

internal static class ShellStartupVerification
{
    public static bool VerifyRenderContextOption(string reportPath, out string summary)
    {
        using var viewer = new OpenVisionThreeDViewerControl(loadDefaultSamples: false);
        var software = Environment.GetCommandLineArgs().Contains("--smoke-software-rendering", StringComparer.OrdinalIgnoreCase);
        var expected = software ? SharpGL.RenderContextType.DIBSection : SharpGL.RenderContextType.FBO;
        var actual = ((SharpGL.WPF.OpenGLControl)viewer.FindName("Viewport")).RenderContextType;
        var passed = actual == expected;
        summary = $"Verification renderer option: {(passed ? "Pass" : "Fail")} (1 checks)";
        File.WriteAllLines(reportPath, [$"{(passed ? "PASS" : "FAIL")} | software={software};expected={expected};actual={actual};GL context not created", summary]);
        return passed;
    }
}
