namespace OpenVisionLab.ThreeD.Reporting.Tests;

internal static class ReportingTestRoot
{
    internal const string EnvironmentVariable = "OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT";

    public static string Create(string suiteName) =>
        Create(suiteName, Environment.GetEnvironmentVariable(EnvironmentVariable));

    internal static string Create(string suiteName, string? configuredRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suiteName);
        var baseRoot = string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD.Reporting.Tests")
            : Path.GetFullPath(configuredRoot);
        var root = Path.Combine(baseRoot, suiteName, Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            return root;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"Reporting test artifact root could not be prepared: {root}",
                exception);
        }
    }

    public static void DeleteBestEffort(string root)
    {
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Reporting test artifact cleanup skipped for '{root}': {exception.Message}");
        }
    }
}
