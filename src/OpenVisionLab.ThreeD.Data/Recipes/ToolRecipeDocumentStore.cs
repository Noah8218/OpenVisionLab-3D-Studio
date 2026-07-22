using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Data;

public static class ToolRecipeDocumentStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static void Save(string path, ToolRecipeDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var validation = ToolRecipeValidator.Validate(document);
        if (!validation.IsValid)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, validation.Errors));
        }

        var fullPath = Path.GetFullPath(path);
        VerifyCurrentSelectionBindings(fullPath, document);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporaryPath = $"{fullPath}.tmp.{Guid.NewGuid():N}";
        try
        {
            var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
                .GetBytes(JsonSerializer.Serialize(document, JsonOptions));
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

    public static ToolRecipeDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        using var stream = File.OpenRead(fullPath);
        var document = JsonSerializer.Deserialize<ToolRecipeDocument>(stream, JsonOptions)
            ?? throw new InvalidDataException("Teaching recipe JSON is empty.");
        var validation = ToolRecipeValidator.Validate(document);
        if (!validation.IsValid)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, validation.Errors));
        }

        return document;
    }

    private static void VerifyCurrentSelectionBindings(string documentPath, ToolRecipeDocument document)
    {
        var rawSelections = (document.Selections ?? []).Where(selection =>
            string.Equals(selection.SourceBinding.Format, "C3D", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (rawSelections.Length == 0)
        {
            return;
        }

        var sourcePath = ResolveSourcePath(documentPath, document.Source.Path);
        ToolRecipeSelectionSourceBinding current;
        try
        {
            current = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(sourcePath);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or ArgumentException
            or NotSupportedException
            or OverflowException)
        {
            throw new InvalidDataException(
                $"Structured selections cannot be saved because the C3D source identity is unavailable: {exception.Message}",
                exception);
        }

        foreach (var selection in rawSelections)
        {
            var result = ToolRecipeSelectionSourceBindingVerifier.Verify(current, selection.SourceBinding);
            if (!result.IsCurrent)
            {
                throw new InvalidDataException(
                    $"Structured selection '{selection.Id}' cannot be saved. {result.Message}");
            }
        }
    }

    private static string ResolveSourcePath(string documentPath, string sourcePath)
    {
        if (Path.IsPathFullyQualified(sourcePath))
        {
            return Path.GetFullPath(sourcePath);
        }

        var documentDirectory = Path.GetDirectoryName(documentPath) ?? Environment.CurrentDirectory;
        return Path.GetFullPath(Path.Combine(documentDirectory, sourcePath));
    }
}
