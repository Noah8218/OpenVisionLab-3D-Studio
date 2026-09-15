using Xunit;

namespace OpenVisionLab.ThreeD.Reporting.Tests;

public sealed class ReportingTestRootTests
{
    [Fact]
    public async Task CreateUsesUniqueDirectoriesForConcurrentFixtures()
    {
        var baseRoot = CreateBaseRoot("concurrent");
        try
        {
            var roots = await Task.WhenAll(
                Enumerable.Range(0, 32)
                    .Select(_ => Task.Run(() => ReportingTestRoot.Create("fixture", baseRoot))));

            Assert.Equal(roots.Length, roots.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.All(roots, root => Assert.True(Directory.Exists(root)));
        }
        finally
        {
            ReportingTestRoot.DeleteBestEffort(baseRoot);
        }
    }

    [Fact]
    public void CreateReportsAnOverridePreparationFailureClearly()
    {
        var baseRoot = CreateBaseRoot("invalid-override");
        Directory.CreateDirectory(baseRoot);
        var filePath = Path.Combine(baseRoot, "not-a-directory");
        File.WriteAllText(filePath, "fixture");
        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                ReportingTestRoot.Create("fixture", filePath));

            Assert.Contains("artifact root could not be prepared", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            ReportingTestRoot.DeleteBestEffort(baseRoot);
        }
    }

    private static string CreateBaseRoot(string caseName)
    {
        var configuredRoot = Environment.GetEnvironmentVariable(ReportingTestRoot.EnvironmentVariable);
        var parent = string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD.Reporting.Tests")
            : Path.GetFullPath(configuredRoot);
        return Path.Combine(parent, "root-contract", caseName, Guid.NewGuid().ToString("N"));
    }
}
