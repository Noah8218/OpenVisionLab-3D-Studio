using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Reporting.RunRecords;

public static class InspectionRunRecordJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsonStringEnumConverter() },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static void Write(string path, InspectionRunRecord record)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(record);

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporaryPath = $"{fullPath}.tmp.{Guid.NewGuid():N}";
        try
        {
            var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
                .GetBytes(JsonSerializer.Serialize(record, Options));
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

    public static InspectionRunRecord Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Run Record was not found.", fullPath);
        }

        var record = JsonSerializer.Deserialize<InspectionRunRecord>(
                         File.ReadAllText(fullPath, Encoding.UTF8),
                         Options)
                     ?? throw new InvalidDataException("Run Record JSON contains null.");
        Validate(record);
        return record;
    }

    public static void Validate(InspectionRunRecord? record)
    {
        if (record is null)
        {
            throw new InvalidDataException("Run Record is required.");
        }

        if (!InspectionRunRecord.SupportsSchemaVersion(record.SchemaVersion))
        {
            throw new InvalidDataException(
                $"Unsupported Run Record schema '{record.SchemaVersion}'.");
        }

        if (string.IsNullOrWhiteSpace(record.RunId)
            || !Enum.IsDefined(record.WorkflowKind)
            || record.Recipe is null
            || record.Source is null
            || record.Artifacts is null
            || string.IsNullOrWhiteSpace(record.ToolName)
            || !Enum.IsDefined(record.Status)
            || !double.IsFinite(record.ElapsedMilliseconds)
            || record.ElapsedMilliseconds < 0.0
            || string.IsNullOrWhiteSpace(record.Recipe.Path)
            || string.IsNullOrWhiteSpace(record.Source.EntityId)
            || string.IsNullOrWhiteSpace(record.Source.Path))
        {
            throw new InvalidDataException(
                "Run Record is missing a required identity, source, tool, status, or elapsed-time field.");
        }

        if (record.Metrics is null
            || record.Metrics.Any(metric => metric is null)
            || (record.Steps is { } resultSteps && resultSteps.Any(step => step is not null
                && (step.Metrics is null
                    || step.Metrics.Any(metric => metric is null)
                    || step.Overlays is null
                    || step.Overlays.Any(overlay => overlay is null)))))
        {
            throw new InvalidDataException("Run Record contains incomplete metric or step overlay data.");
        }

        if (record.Steps is { } inputSteps
            && inputSteps.Any(step => step is not null && step.InputEntityIds is null))
        {
            throw new InvalidDataException("Run Record contains incomplete step input data.");
        }

        if (record.Steps is { } steps
            && steps.Any(step => step is null || !step.TryValidateEvidence(out _)))
        {
            throw new InvalidDataException(
                "Run Record contains invalid semantic algorithm or SDK evidence.");
        }

        if (record.NominalActualAlignmentEvidence is { } alignment
            && !alignment.TryValidate(out var alignmentValidationMessage))
        {
            throw new InvalidDataException(alignmentValidationMessage);
        }
    }
}
