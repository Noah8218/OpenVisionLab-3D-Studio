using OpenVisionLab.ThreeD.Data;
using Xunit;

namespace OpenVisionLab.ThreeD.Data.Tests;

public sealed class ExistingVerificationFacadeTests
{
    [Fact]
    public void C3DHeightProfileContract()
    {
        var reportPath = GetReportPath("c3d-height-profile.txt");

        Assert.True(
            C3DHeightProfileVerification.Verify(reportPath, out var summary),
            $"{summary}{Environment.NewLine}Report: {reportPath}");
    }

    [Fact]
    public void ToolRecipeSelectionContract()
    {
        var reportPath = GetReportPath("tool-recipe-selection.txt");

        Assert.True(
            ToolRecipeSelectionContractVerification.Verify(reportPath, out var summary),
            $"{summary}{Environment.NewLine}Report: {reportPath}");
    }

    private static string GetReportPath(string fileName)
    {
        var directory = Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD.Tests");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }
}
