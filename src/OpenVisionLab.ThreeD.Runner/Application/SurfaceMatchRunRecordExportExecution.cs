using System.Text;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

internal static class SurfaceMatchRunRecordExportExecution
{
    public static int Run(
        string recipePath,
        string modelPath,
        string scenePath,
        string executionPath,
        string? scorePath,
        string? assessmentPath,
        string? runtimePath,
        string reportPath,
        RunArtifactOptions outputs)
    {
        try
        {
            if (!outputs.Requested)
            {
                throw new InvalidDataException(
                    "Surface Match export requires at least one JSON, HTML, or CSV output.");
            }

            var fullRecipePath = Path.GetFullPath(recipePath);
            var document = ToolRecipeDocumentStore.Load(fullRecipePath);
            var model = SurfaceModelArtifactStore.Load(modelPath);
            var scene = PreparedSceneArtifactStore.Load(scenePath);
            var execution = SurfaceMatchExecutionArtifactStore.Load(
                executionPath);
            var score = string.IsNullOrWhiteSpace(scorePath)
                ? null
                : SurfaceEdgeArtifactStore.LoadScore(scorePath);
            var assessment = string.IsNullOrWhiteSpace(assessmentPath)
                ? null
                : SurfaceEdgeDiagnosticReviewArtifactStore.LoadAssessment(
                    assessmentPath);
            var runtime = string.IsNullOrWhiteSpace(runtimePath)
                ? null
                : SurfaceMatchAssessmentArtifactStore.LoadRuntime(runtimePath);

            var fullReportPath = Path.GetFullPath(reportPath);
            RunRecordWriter.WriteSurfaceMatch(
                outputs,
                fullRecipePath,
                document,
                model,
                scene,
                execution,
                score,
                assessment,
                runtime,
                fullReportPath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(fullReportPath)
                ?? Environment.CurrentDirectory);
            WriteLinesAtomically(
                fullReportPath,
                [
                    "SurfaceMatchRunRecordExport|Complete|matchingRecomputed=false",
                    $"Recipe|path={fullRecipePath}|schema={document.SchemaVersion}",
                    $"Model|path={Path.GetFullPath(modelPath)}|sha256={model.ContentSha256}",
                    $"Scene|path={Path.GetFullPath(scenePath)}|sha256={scene.ContentSha256}",
                    $"Execution|path={Path.GetFullPath(executionPath)}|sha256={execution.ContentSha256}|pose={execution.PoseResult.ContentSha256}|state={execution.PoseResult.State}",
                    $"Score|path={(scorePath is null ? "(none)" : Path.GetFullPath(scorePath))}|sha256={score?.ContentSha256 ?? "(none)"}",
                    $"Assessment|path={(assessmentPath is null ? "(none)" : Path.GetFullPath(assessmentPath))}|sha256={assessment?.ContentSha256 ?? "(none)"}",
                    $"Runtime|path={(runtimePath is null ? "(none)" : Path.GetFullPath(runtimePath))}|state={(runtime is null ? "Unavailable" : "Available")}|matchingRecomputed=false",
                    $"Outputs|json={outputs.JsonPath ?? "(none)"}|html={outputs.HtmlPath ?? "(none)"}|csv={outputs.CsvPath ?? "(none)"}"
                ]);
            Console.WriteLine(
                $"Surface Match evidence exported without recomputation: {execution.ContentSha256}");
            return 0;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException
            or OverflowException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static void WriteLinesAtomically(
        string path,
        IEnumerable<string> lines)
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
                foreach (var line in lines)
                {
                    writer.WriteLine(line);
                }

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
