using System.Reflection;

namespace OpenVisionLab.ThreeD.Shell;

internal static class IntegrationBuildIdentity
{
    public static string Version { get; } =
        typeof(IntegrationBuildIdentity).Assembly.GetName().Version?.ToString(3)
        ?? "0.0.0";
    public static string SourceCommit { get; } = ResolveMetadata("OpenVisionSourceCommit");
    public static string SourceState { get; } = ResolveMetadata("OpenVisionSourceState");

    private static string ResolveMetadata(string key) =>
        typeof(IntegrationBuildIdentity).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))?
            .Value
        ?? "unknown";
}
