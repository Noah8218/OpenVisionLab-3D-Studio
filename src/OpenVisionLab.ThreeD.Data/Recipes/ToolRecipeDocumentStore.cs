using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Data;

public static class ToolRecipeDocumentStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };

    public static void Save(string path, ToolRecipeDocument document) => SaveCore(path, document, overwrite: true);

    public static void Save(string path, ToolRecipeDocument document, out string sha256) => sha256 = SaveCore(path, document, overwrite: true);

    public static void SaveNew(string path, ToolRecipeDocument document) => SaveCore(path, document, overwrite: false);

    public static bool TrySaveLandmarkReferenceRevisions(string path, ToolRecipeDocument document, string savedSnapshotPath, string savedSha256, out string sha256)
    {
        sha256 = string.Empty;
        var savedBytes = File.ReadAllBytes(savedSnapshotPath);
        if (!Convert.ToHexString(SHA256.HashData(savedBytes)).Equals(savedSha256, StringComparison.OrdinalIgnoreCase)) return false;
        var previous = Load(savedSnapshotPath);
        if (previous.Selections is not { } previousSelections || document.Selections is not { } selections || previousSelections.Count != selections.Count) return false;

        var previousJson = JsonSerializer.SerializeToNode(previous, JsonOptions)!;
        var currentJson = JsonSerializer.SerializeToNode(document, JsonOptions)!;
        previousJson["source"]!["path"] = ResolveSourcePath(path, previous.Source.Path);
        currentJson["source"]!["path"] = ResolveSourcePath(path, document.Source.Path);
        var savedJson = JsonNode.Parse(File.ReadAllText(savedSnapshotPath))!;
        var changed = false;
        for (var index = 0; index < selections.Count; index++)
        {
            if (previousSelections[index].Kind != ToolRecipeSelectionKinds.LandmarkCorrespondenceSet
                || previousSelections[index].CorrespondenceDescriptor is not { } previousDescriptor
                || selections[index].CorrespondenceDescriptor is not { } descriptor
                || descriptor.ReferenceRevision == previousDescriptor.ReferenceRevision) continue;
            currentJson["selections"]![index]!["correspondenceDescriptor"]!["referenceRevision"] = previousDescriptor.ReferenceRevision;
            savedJson["selections"]![index]!["correspondenceDescriptor"]!["referenceRevision"] = descriptor.ReferenceRevision;
            changed = true;
        }

        if (!changed || !JsonNode.DeepEquals(previousJson, currentJson)) return false;
        // Keep field presence from the saved recipe only for a revision-only edit.
        sha256 = SaveCore(path, document, overwrite: true, savedJson.ToJsonString(JsonOptions));
        return true;
    }

    private static string SaveCore(string path, ToolRecipeDocument document, bool overwrite, string? serializedJson = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var validation = ToolRecipeValidator.ValidateForStorage(document);
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
                .GetBytes(serializedJson ?? JsonSerializer.Serialize(document, JsonOptions));
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

            File.Move(temporaryPath, fullPath, overwrite);
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static ToolRecipeDocument Load(string path) => Load(path, out _);

    public static ToolRecipeDocument Load(string path, out string sha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        using var stream = File.OpenRead(fullPath);
        var document = JsonSerializer.Deserialize<ToolRecipeDocument>(stream, JsonOptions)
            ?? throw new InvalidDataException("Teaching recipe JSON is empty.");
        var validation = ToolRecipeValidator.ValidateForStorage(document);
        if (!validation.IsValid)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, validation.Errors));
        }

        stream.Position = 0;
        sha256 = Convert.ToHexString(SHA256.HashData(stream));
        // Storage validation permits these collections to be empty drafts.
        // Parameters remain null when catalog defaults must be restored.
        return document with
        {
            References = document.References ?? [],
            Steps = (document.Steps ?? []).Select(step => step.InputEntityIds is null
                ? step with { InputEntityIds = [] }
                : step).ToArray()
        };
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
