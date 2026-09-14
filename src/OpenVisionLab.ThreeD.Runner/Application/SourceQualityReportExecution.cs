using System.Text;
using System.Text.Json;
using OpenVisionLab.ThreeD.Data;

internal static class SourceQualityReportExecution
{
    public static int Run(
        string sourcePath,
        string entityId,
        string unit,
        string frameId,
        string reportPath)
    {
        try
        {
            var snapshot = C3DHeightFieldSnapshot.LoadIdentified(
                sourcePath,
                entityId,
                unit,
                frameId);
            var report = C3DSourceQualityAnalyzer.Create(snapshot);
            var fullReportPath = Path.GetFullPath(reportPath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
            WriteTextAtomically(
                fullReportPath,
                JsonSerializer.Serialize(
                    report,
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = true
                    }));
            Console.WriteLine(
                $"SourceQualityReport: Pass ({report.Grid.Width}x{report.Grid.Height}, "
                + $"{report.Coverage.ValidSampleCount:N0} valid, "
                + $"{report.Coverage.MissingSampleCount:N0} missing)");
            return 0;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or ArgumentException
                or OverflowException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static void WriteTextAtomically(string path, string text)
    {
        var fullPath = Path.GetFullPath(path);
        var temporaryPath = $"{fullPath}.tmp.{Guid.NewGuid():N}";
        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4096,
                       FileOptions.WriteThrough))
            using (var writer = new StreamWriter(
                       stream,
                       new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                       bufferSize: 4096,
                       leaveOpen: true))
            {
                writer.Write(text);
                writer.Flush();
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
}
