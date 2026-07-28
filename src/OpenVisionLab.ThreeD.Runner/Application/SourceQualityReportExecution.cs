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
            File.WriteAllText(
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
}
