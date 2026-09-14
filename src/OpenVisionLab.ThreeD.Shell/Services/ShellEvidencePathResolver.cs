using System.IO;

namespace OpenVisionLab.ThreeD.Shell.Services;

/// <summary>
/// Owns Shell evidence-root discovery and relative/absolute artifact-path
/// normalization. It has no WPF, Run Record state, or process lifetime.
/// </summary>
internal static class ShellEvidencePathResolver
{
    private const string SolutionFileName = "OpenVisionLab.ThreeDStudio.slnx";

    public static string ResolveWorkspaceRoot(string? startDirectory = null)
    {
        var startPath = Path.GetFullPath(
            string.IsNullOrWhiteSpace(startDirectory)
                ? Directory.GetCurrentDirectory()
                : startDirectory);
        var directory = new DirectoryInfo(startPath);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return startPath;
    }

    public static string ResolvePath(string root, string? requestedPath, string fallbackPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(fallbackPath);

        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            return fallbackPath;
        }

        return Path.IsPathRooted(requestedPath)
            ? requestedPath
            : Path.Combine(root, requestedPath);
    }

    public static string? ResolveOptionalPath(string root, string? requestedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        return string.IsNullOrWhiteSpace(requestedPath)
            ? null
            : Path.IsPathRooted(requestedPath)
                ? requestedPath
                : Path.Combine(root, requestedPath);
    }
}
