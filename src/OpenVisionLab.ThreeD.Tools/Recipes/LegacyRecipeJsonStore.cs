using System.Text.Json;

namespace OpenVisionLab.ThreeD.Tools;

internal static class LegacyRecipeJsonStore
{
    internal static void Save<TRecipe>(string path, TRecipe recipe, JsonSerializerOptions jsonOptions)
    {
        var fullPath = Path.GetFullPath(path);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(recipe, jsonOptions);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporaryPath = $"{fullPath}.tmp.{Guid.NewGuid():N}";
        var ownsTemporaryFile = false;
        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4096,
                       FileOptions.WriteThrough))
            {
                ownsTemporaryFile = true;
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        catch
        {
            // A failed CreateNew never grants ownership of an existing staging file.
            if (ownsTemporaryFile)
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (IOException)
                {
                    // Preserve the save failure even if owned staging cleanup fails.
                }
                catch (UnauthorizedAccessException)
                {
                    // Preserve the save failure even if owned staging cleanup fails.
                }
            }

            throw;
        }
    }
}
