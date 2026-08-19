using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell;

internal static partial class PrivacySafeSupportBundleWriter
{
    public const int MaximumSessionLogEntries = 200;

    private const string Omitted = "<omitted>";
    private const string OmittedPath = "<omitted-path>";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Write(
        string runRecordPath,
        string targetRoot,
        IReadOnlyList<ToolWorkbenchLogItem> sessionLog)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runRecordPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRoot);
        ArgumentNullException.ThrowIfNull(sessionLog);

        var record = ReadAndValidateRunRecord(runRecordPath);
        var payloads = new List<SupportPayload>
        {
            CreateRecipePayload(record),
            CreateSessionLogPayload(sessionLog),
            CreateSourceIdentityPayload(record),
            CreateSourceQualityPayload(record),
            CreateCurrentResultPayload(record)
        };
        var fullTargetRoot = Path.GetFullPath(targetRoot);
        Directory.CreateDirectory(fullTargetRoot);

        var safeRunId = $"run-{HashIdentity(record.RunId)[..12]}";
        for (var suffix = 1; suffix <= 9999; suffix++)
        {
            var suffixText = suffix == 1 ? string.Empty : $"-{suffix}";
            var outputPath = Path.Combine(
                fullTargetRoot,
                $"OpenVisionLab-Support-{safeRunId}{suffixText}.zip");
            FileStream? stream = null;
            try
            {
                stream = new FileStream(
                    outputPath,
                    FileMode.CreateNew,
                    FileAccess.ReadWrite,
                    FileShare.None);
                using (stream)
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    foreach (var payload in payloads)
                    {
                        WriteEntry(archive, payload.Name, payload.Bytes);
                    }

                    var manifest = CreateManifest(record, payloads);
                    WriteEntry(archive, "manifest.json", Serialize(manifest));
                }

                return outputPath;
            }
            catch (IOException) when (stream is null && File.Exists(outputPath))
            {
                continue;
            }
            catch
            {
                stream?.Dispose();
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }

                throw;
            }
        }

        throw new IOException("No collision-safe support bundle name was available.");
    }

    private static InspectionRunRecord ReadAndValidateRunRecord(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var record = JsonSerializer.Deserialize<InspectionRunRecord>(
            File.ReadAllText(fullPath),
            JsonOptions)
            ?? throw new InvalidDataException("The Run Record JSON is empty.");
        if (string.IsNullOrWhiteSpace(record.SchemaVersion)
            || string.IsNullOrWhiteSpace(record.RunId)
            || record.Recipe is null
            || record.Source is null
            || string.IsNullOrWhiteSpace(record.Source.EntityId)
            || string.IsNullOrWhiteSpace(record.Source.Sha256))
        {
            throw new InvalidDataException(
                "The Run Record is missing its schema, run, recipe, or source identity.");
        }

        if (record.SourceQualityEvidence is { } quality
            && !quality.TryValidate(record.Source, out var validationMessage))
        {
            throw new InvalidDataException(
                $"Run Record Source Quality is invalid: {validationMessage}");
        }

        return record;
    }

    private static SupportPayload CreateRecipePayload(InspectionRunRecord record)
    {
        var state = "Unavailable";
        var message = "The exact recorded recipe file is unavailable or its SHA-256 does not match.";
        JsonNode? recipe = null;
        if (!string.IsNullOrWhiteSpace(record.Recipe.Path)
            && !string.IsNullOrWhiteSpace(record.Recipe.Sha256)
            && File.Exists(record.Recipe.Path))
        {
            var bytes = File.ReadAllBytes(record.Recipe.Path);
            if (string.Equals(
                    Convert.ToHexString(SHA256.HashData(bytes)),
                    record.Recipe.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                var document = ToolRecipeDocumentStore.Load(record.Recipe.Path);
                recipe = JsonSerializer.SerializeToNode(document, JsonOptions);
                SanitizeRecipe(recipe);
                state = "Available";
                message = "Exact recorded recipe configuration with free-form names, notes, and paths omitted.";
            }
        }

        return CreatePayload(
            "recipe.json",
            new
            {
                SchemaVersion = "1.0",
                State = state,
                Message = message,
                RecordedSha256 = record.Recipe.Sha256,
                Recipe = recipe
            });
    }

    private static SupportPayload CreateSessionLogPayload(
        IReadOnlyList<ToolWorkbenchLogItem> sessionLog)
    {
        var entries = sessionLog
            .Take(MaximumSessionLogEntries)
            .Select(item => new
            {
                item.Time,
                Category = RedactSensitiveText(item.Category),
                Message = RedactSensitiveText(item.Message)
            })
            .ToArray();
        return CreatePayload(
            "log-excerpt.json",
            new
            {
                SchemaVersion = "1.0",
                NewestFirst = true,
                MaximumEntries = MaximumSessionLogEntries,
                EntryCount = entries.Length,
                Entries = entries
            });
    }

    private static SupportPayload CreateSourceIdentityPayload(
        InspectionRunRecord record) =>
        CreatePayload(
            "source-identity.json",
            new
            {
                SchemaVersion = "1.0",
                record.Source.EntityId,
                record.Source.Sha256,
                record.Source.ByteLength,
                record.Source.Unit,
                Path = OmittedPath,
                SourceBytesIncluded = false
            });

    private static SupportPayload CreateSourceQualityPayload(
        InspectionRunRecord record)
    {
        if (record.SourceQualityEvidence is null)
        {
            return CreatePayload(
                "source-quality.json",
                new
                {
                    SchemaVersion = "1.0",
                    State = "Unavailable",
                    Message = "This legacy Run Record did not record Source Quality evidence.",
                    Evidence = (JsonNode?)null
                });
        }

        var evidence = JsonSerializer.SerializeToNode(
            record.SourceQualityEvidence,
            JsonOptions);
        SanitizeNode(evidence);
        return CreatePayload(
            "source-quality.json",
            new
            {
                SchemaVersion = "1.0",
                State = record.SourceQualityEvidence.State.ToString(),
                Message = RedactSensitiveText(record.SourceQualityEvidence.Message),
                Evidence = evidence
            });
    }

    private static SupportPayload CreateCurrentResultPayload(
        InspectionRunRecord record)
    {
        var result = JsonSerializer.SerializeToNode(record, JsonOptions)?.AsObject()
            ?? throw new InvalidDataException("The current result could not be projected.");
        result.Remove(nameof(InspectionRunRecord.ExecutionEnvironment));
        result.Remove(nameof(InspectionRunRecord.Artifacts));
        result.Remove(nameof(InspectionRunRecord.SourceQualityEvidence));
        result.Remove(nameof(InspectionRunRecord.ThresholdCorrectionEvidence));
        result[nameof(InspectionRunRecord.RunId)] =
            $"sha256:{HashIdentity(record.RunId)}";
        SanitizeNode(result);
        return CreatePayload("current-result.json", result);
    }

    private static object CreateManifest(
        InspectionRunRecord record,
        IReadOnlyList<SupportPayload> payloads) =>
        new
        {
            SchemaVersion = "1.0",
            BundleKind = "OpenVisionLab privacy-safe support bundle",
            PrivacyMode = "DefaultSafe",
            RunIdSha256 = HashIdentity(record.RunId),
            record.RecordedAtUtc,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Evidence = new
            {
                Recipe = GetPayloadState(payloads, "recipe.json"),
                SessionLog = $"Newest {GetPayloadInteger(payloads, "log-excerpt.json", "EntryCount")} entries",
                SourceIdentity = "Available",
                SourceQuality = record.SourceQualityEvidence?.State.ToString() ?? "Unavailable",
                CurrentResult = "Available"
            },
            DefaultOmissions = new[]
            {
                "Raw source, point-cloud, mesh, image, and other acquisition bytes",
                "Absolute paths and source file names",
                "Full application log and entries older than the bounded excerpt",
                "User, profile, account, and machine identity",
                "Execution-environment and artifact-path fields",
                "Free-form recipe names, selection names, provenance notes, and revision text"
            },
            SharingNotice = "Review this bundle before sharing. It contains recipe parameters and inspection evidence but no raw 3D source bytes by default.",
            ManifestIntegrity = "The manifest describes payload entries; its own hash is intentionally not self-referential.",
            Payloads = payloads.Select(payload => new
            {
                Entry = payload.Name,
                payload.Bytes.Length,
                Sha256 = Convert.ToHexString(SHA256.HashData(payload.Bytes))
            }).ToArray()
        };

    private static string GetPayloadState(
        IReadOnlyList<SupportPayload> payloads,
        string name)
    {
        var payload = payloads.Single(item => item.Name == name);
        using var document = JsonDocument.Parse(payload.Bytes);
        return document.RootElement.TryGetProperty("State", out var state)
            ? state.GetString() ?? "Unavailable"
            : "Available";
    }

    private static int GetPayloadInteger(
        IReadOnlyList<SupportPayload> payloads,
        string name,
        string propertyName)
    {
        var payload = payloads.Single(item => item.Name == name);
        using var document = JsonDocument.Parse(payload.Bytes);
        return document.RootElement.TryGetProperty(propertyName, out var value)
            ? value.GetInt32()
            : 0;
    }

    private static void SanitizeRecipe(JsonNode? recipe)
    {
        if (recipe is not JsonObject root)
        {
            return;
        }

        root["Name"] = Omitted;
        if (root["Source"] is JsonObject source)
        {
            source["Name"] = Omitted;
            source["Path"] = OmittedPath;
            if (source["AcquisitionProvenance"] is JsonObject provenance)
            {
                provenance["Evidence"] = Omitted;
                provenance["LimitationNotes"] = Omitted;
            }
        }

        RedactArrayNames(root["References"]);
        RedactArrayNames(root["Selections"]);
        if (root["Selections"] is JsonArray selections)
        {
            foreach (var selection in selections.OfType<JsonObject>())
            {
                if (selection["CorrespondenceDescriptor"] is JsonObject descriptor)
                {
                    descriptor["ReferenceProvenance"] = Omitted;
                    descriptor["ReferenceRevision"] = Omitted;
                }
            }
        }

        SanitizeNode(root);
    }

    private static void RedactArrayNames(JsonNode? node)
    {
        if (node is not JsonArray array)
        {
            return;
        }

        foreach (var item in array.OfType<JsonObject>())
        {
            item["Name"] = Omitted;
        }
    }

    private static void SanitizeNode(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (var property in jsonObject.ToArray())
                {
                    if (property.Key.EndsWith("Path", StringComparison.OrdinalIgnoreCase))
                    {
                        jsonObject[property.Key] = OmittedPath;
                        continue;
                    }

                    if (property.Value is JsonValue value
                        && value.TryGetValue<string>(out var text))
                    {
                        jsonObject[property.Key] = RedactSensitiveText(text);
                        continue;
                    }

                    SanitizeNode(property.Value);
                }
                break;
            case JsonArray jsonArray:
                foreach (var item in jsonArray)
                {
                    SanitizeNode(item);
                }
                break;
        }
    }

    internal static string RedactSensitiveText(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        var redacted = WindowsAbsolutePathRegex().Replace(value, OmittedPath);
        redacted = UnixPrivatePathRegex().Replace(redacted, OmittedPath);
        foreach (var token in GetPrivateTokens())
        {
            redacted = redacted.Replace(token, Omitted, StringComparison.OrdinalIgnoreCase);
        }

        return redacted;
    }

    private static IEnumerable<string> GetPrivateTokens()
    {
        var userProfile = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            yield return userProfile;
        }

        if (!string.IsNullOrWhiteSpace(Environment.UserName))
        {
            yield return Environment.UserName;
        }

        if (!string.IsNullOrWhiteSpace(Environment.MachineName))
        {
            yield return Environment.MachineName;
        }
    }

    private static SupportPayload CreatePayload(string name, object value) =>
        new(name, Serialize(value));

    private static byte[] Serialize(object value) =>
        JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);

    private static void WriteEntry(
        ZipArchive archive,
        string name,
        byte[] bytes)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(bytes);
    }

    private static string HashIdentity(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    [GeneratedRegex(@"(?i)(?:[a-z]:\\|\\\\)[^|;\r\n\""']+")]
    private static partial Regex WindowsAbsolutePathRegex();

    [GeneratedRegex(@"(?i)(?<![a-z0-9])/(?:home|users|mnt|tmp)/[^|;\r\n\""']+")]
    private static partial Regex UnixPrivatePathRegex();

    private sealed record SupportPayload(string Name, byte[] Bytes);
}
