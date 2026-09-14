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
        Converters = { new JsonStringEnumConverter() }
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

        return JsonSerializer.Deserialize<InspectionRunRecord>(
                   File.ReadAllText(fullPath, Encoding.UTF8),
                   Options)
               ?? throw new InvalidDataException("Run Record JSON contains null.");
    }
}
