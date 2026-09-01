using System.Reflection;
using OpenVisionLab.Integration.Contracts;

namespace OpenVisionLab.ThreeD.Shell;

internal static class IntegrationBuildIdentity
{
    private const string ProductVersionMetadataName = "OpenVisionLabProductVersion";
    private static readonly Assembly ApplicationAssembly =
        typeof(IntegrationBuildIdentity).Assembly;

    public static string Version { get; } =
        ResolveMetadata(ProductVersionMetadataName)
        ?? ResolveMetadata(IntegrationRuntimeBuildManifestContract.ApplicationVersionMetadataName)
        ?? ApplicationAssembly.GetName().Version?.ToString(3)
        ?? "0.0.0";
    public static string SourceCommit { get; } =
        ResolveMetadata(IntegrationRuntimeBuildManifestContract.SourceCommitMetadataName)
        ?? "unknown";
    public static string SourceState { get; } =
        ResolveMetadata(IntegrationRuntimeBuildManifestContract.SourceStateMetadataName)
        ?? "unknown";

    public static IntegrationApplicationIdentity LoadQualifiedIdentity(
        string? manifestPath = null) =>
        IntegrationRuntimeBuildVerifier.LoadQualifiedIdentity(
            ApplicationAssembly,
            IntegrationApplicationIds.ThreeDStudio,
            manifestPath);

    public static IntegrationApplicationIdentity LoadQualifiedTargetIdentity(
        IntegrationApplicationIdentity expectedIdentity,
        string? manifestPath = null) =>
        IntegrationRuntimeBuildVerifier.LoadQualifiedTargetIdentity(
            ApplicationAssembly,
            expectedIdentity,
            manifestPath);

    private static string? ResolveMetadata(string key) =>
        ApplicationAssembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))?
            .Value;
}
