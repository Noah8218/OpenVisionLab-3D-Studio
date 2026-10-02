using System.Text;
using System.Text.Json;

namespace OpenVisionLab.ThreeD.Data;

public static class RecipeRecentFileStore
{
    public const int MaximumEntries = 10;
    public const int MaximumRecipeEntries = 100;

    public static IReadOnlyList<string> Load(string path)
        => Load(path, MaximumEntries);

    public static IReadOnlyList<string> Load(string path, int maximumEntries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumEntries);
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            var entries = JsonSerializer.Deserialize<string[]>(File.ReadAllBytes(path)) ?? [];
            return Normalize(entries, maximumEntries);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static void Save(string path, IEnumerable<string> paths)
        => Save(path, paths, MaximumEntries);

    public static void Save(string path, IEnumerable<string> paths, int maximumEntries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumEntries);

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporaryPath = $"{fullPath}.tmp.{Guid.NewGuid():N}";
        try
        {
            var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
                .GetBytes(JsonSerializer.Serialize(Normalize(paths, maximumEntries), new JsonSerializerOptions { WriteIndented = true }));
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string[] Normalize(IEnumerable<string> paths, int maximumEntries)
    {
        return paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maximumEntries)
            .ToArray();
    }
}
