using System.IO;

namespace OpenVisionLab.ThreeD.Viewer.Loading;

/// <summary>
/// Resolves repository or published-sample paths by walking from the supplied
/// roots. It owns only path discovery; file loading and UI messaging stay with
/// the caller.
/// </summary>
internal static class ViewerSamplePathLocator
{
    public static string? Find(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        return Find(relativePath, [Environment.CurrentDirectory, AppContext.BaseDirectory]);
    }

    internal static string? Find(string relativePath, IEnumerable<string> roots)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(roots);
        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            var directory = new DirectoryInfo(root);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, relativePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }
}
