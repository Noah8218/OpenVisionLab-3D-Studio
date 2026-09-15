using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Reporting.RunRecords;
using OpenVisionLab.ThreeD.Tools;

internal sealed record RunArtifactOptions(
    string? JsonPath,
    string? HtmlPath,
    string? CsvPath,
    string? ViewerScreenshotPath)
{
    public bool Requested => JsonPath is not null || HtmlPath is not null || CsvPath is not null;
}

internal static class RunRecordWriter
{
    public static void Write(
        RunArtifactOptions options,
        string recipePath,
        string recipeType,
        string recipeVersion,
        string sourcePath,
        string sourceEntityId,
        string sourceUnit,
        InspectionRunStep? step,
        ToolResult result,
        string runnerReportPath,
        string? viewerContractPath)
    {
        if (!options.Requested) return;

        var recordedAt = DateTimeOffset.UtcNow;
        var recipeHash = HashFile(recipePath);
        var sourceHash = HashFile(sourcePath);
        var record = new InspectionRunRecord(
            "1.2",
            $"run-{recordedAt:yyyyMMddTHHmmssfffZ}-{recipeHash[..12].ToLowerInvariant()}",
            recordedAt,
            new InspectionRunRecipe(recipeType, recipeVersion, Path.GetFullPath(recipePath), recipeHash),
            new InspectionRunSource(sourceEntityId, Path.GetFullPath(sourcePath), sourceHash, new FileInfo(sourcePath).Length, sourceUnit),
            result.ToolName,
            result.Status,
            result.Message,
            result.Elapsed.TotalMilliseconds,
            ToMetrics(result.Metrics),
            result.Overlays.Select(overlay => new InspectionRunOverlay(overlay.Id, overlay.Kind, overlay.Label, overlay.Status, overlay.SourceEntityId)).ToArray(),
            viewerContractPath is null ? "NotCompared" : "Matched",
            CreateArtifacts(options, runnerReportPath, viewerContractPath))
        {
            ExecutionEnvironment = CreateExecutionEnvironment(recipePath),
            Step = step,
            Timing = CreateToolTiming(result.Elapsed.TotalMilliseconds)
        };

        WriteOutputs(options, record);
    }

    public static void WriteOrdered(
        RunArtifactOptions options,
        string recipePath,
        ToolRecipeDocument document,
        string sourcePath,
        string regridStepId,
        ToolResult result,
        ToolRecipeOrderedTransformedHeightFieldExecutionOutput output,
        string runnerReportPath,
        string? viewerContractPath)
    {
        if (!options.Requested) return;

        var recordedAt = DateTimeOffset.UtcNow;
        var recipeHash = HashFile(recipePath);
        var sourceHash = HashFile(sourcePath);
        var regridIndex = document.Steps.ToList().FindIndex(step =>
            string.Equals(step.Id, regridStepId, StringComparison.OrdinalIgnoreCase));
        if (regridIndex < 0) throw new InvalidDataException($"Ordered Run Record cannot find Re-grid step '{regridStepId}'.");

        var regridStep = document.Steps[regridIndex];
        var steps = new List<InspectionRunStepResult>
        {
            ToStepResult(regridIndex, regridStep, output.RegridResult)
        };
        steps.AddRange(output.Measurements.Select(item =>
            ToStepResult(
                item.RecipeIndex,
                document.Steps[item.RecipeIndex],
                item.Output.Result,
                semanticFingerprint: item.Output.SemanticFingerprint,
                algorithmEvidence: item.Output.SemanticFingerprint is null
                    ? null
                    : new InspectionRunAlgorithmEvidence(
                        ToolRecipeHeightMeasurementExecution.SemanticFingerprintAlgorithmVersion,
                        VisionSdkHeightMapInspection.PackageId,
                        VisionSdkHeightMapInspection.PackageVersion))));

        var runSource = new InspectionRunSource(
            document.Source.Id,
            Path.GetFullPath(sourcePath),
            sourceHash,
            new FileInfo(sourcePath).Length,
            document.Source.Unit)
        {
            FrameId = document.Source.FrameId
        };
        var record = new InspectionRunRecord(
            InspectionRunRecord.CurrentSchemaVersion,
            $"run-{recordedAt:yyyyMMddTHHmmssfffZ}-{recipeHash[..12].ToLowerInvariant()}",
            recordedAt,
            new InspectionRunRecipe("tool-recipe", document.SchemaVersion, Path.GetFullPath(recipePath), recipeHash),
            runSource,
            result.ToolName,
            result.Status,
            result.Message,
            result.Elapsed.TotalMilliseconds,
            ToMetrics(result.Metrics),
            ToOverlays(result.Overlays),
            viewerContractPath is null ? "NotCompared" : "Matched",
            CreateArtifacts(options, runnerReportPath, viewerContractPath))
        {
            ExecutionEnvironment = CreateExecutionEnvironment(recipePath),
            Steps = steps,
            SourceQualityEvidence =
                InspectionRunSourceQualityEvidence.Unavailable(
                    "This ordered execution path did not supply Source Quality evidence.")
        };

        WriteOutputs(options, record);
    }

    public static void WriteOrderedGraph(
        RunArtifactOptions options,
        string recipePath,
        ToolRecipeDocument document,
        string sourcePath,
        ToolRecipeOrderedGraphExecutionResult execution,
        string runnerReportPath,
        string? viewerContractPath)
    {
        if (!options.Requested) return;

        var record = OrderedRunRecordFactory.Create(
            OrderedRunRecordIdentity.Create(recipePath, sourcePath),
            document,
            execution,
            viewerContractPath is null ? "NotCompared" : "Matched",
            CreateArtifacts(options, runnerReportPath, viewerContractPath),
            CreateExecutionEnvironment(recipePath));

        WriteOutputs(options, record);
    }

    public static void WriteSurfaceMatch(
        RunArtifactOptions options,
        string recipePath,
        ToolRecipeDocument document,
        SurfaceModelArtifact model,
        PreparedSceneArtifact scene,
        SurfaceMatchExecutionArtifact execution,
        SurfaceAndEdgeMatchScoreArtifact? score,
        SurfaceAndEdgeMatchAssessmentArtifact? assessment,
        SurfaceMatchRuntimeReport? runtime,
        string runnerReportPath)
    {
        if (!options.Requested) return;

        var evidence = SurfaceMatchRunRecordProjection.Create(
            model,
            scene,
            execution,
            score,
            assessment);
        var recordedAt = DateTimeOffset.UtcNow;
        var recipeHash = HashFile(recipePath);
        var source = scene.SourceQuality.Source;
        var runSource = new InspectionRunSource(
            source.EntityId,
            source.Path,
            source.RootSourceSha256,
            source.ByteLength,
            scene.Unit)
        {
            FrameId = scene.SourceQuality.Coordinates.FrameId
        };
        var status = assessment?.Decision == SurfaceMatchDecision.Pass
            ? ResultStatus.Pass
            : ResultStatus.Fail;
        var message = assessment is null
            ? execution.PoseResult.RejectionReason
            : $"Surface/edge assessment: {assessment.Decision} ({assessment.Reason}).";
        var timing = CreateSurfaceMatchTiming(execution, assessment, runtime);
        var record = new InspectionRunRecord(
            InspectionRunRecord.CurrentSchemaVersion,
            $"run-{recordedAt:yyyyMMddTHHmmssfffZ}-{recipeHash[..12].ToLowerInvariant()}",
            recordedAt,
            new InspectionRunRecipe(
                "tool-recipe",
                document.SchemaVersion,
                Path.GetFullPath(recipePath),
                recipeHash),
            runSource,
            "Surface Match",
            status,
            string.IsNullOrWhiteSpace(message)
                ? "Identified Surface Match evidence exported without recomputation."
                : message,
            timing.TotalElapsedMilliseconds ?? 0.0,
            [],
            [],
            "NotCompared",
            CreateArtifacts(options, runnerReportPath, viewerContractPath: null))
        {
            ExecutionEnvironment = CreateExecutionEnvironment(recipePath),
            SourceQualityEvidence =
                InspectionRunSourceQualityEvidence.Available(
                    runSource,
                    scene.SourceQuality),
            SurfaceMatchEvidence = evidence,
            Timing = timing
        };

        WriteOutputs(options, record);
    }

    private static InspectionRunStepResult ToStepResult(
        int recipeIndex,
        ToolRecipeStep step,
        ToolResult result,
        string? outputContentSha256 = null,
        string? semanticFingerprint = null,
        InspectionRunAlgorithmEvidence? algorithmEvidence = null) =>
        new InspectionRunStepResult(
            recipeIndex,
            step.Id,
            step.ToolId,
            result.ToolName,
            step.InputEntityIds,
            step.OutputEntityId,
            result.Status,
            result.Message,
            result.Elapsed.TotalMilliseconds,
            ToMetrics(result.Metrics),
            ToOverlays(result.Overlays))
        {
            OutputContentSha256 = outputContentSha256,
            SemanticFingerprint = semanticFingerprint,
            AlgorithmEvidence = algorithmEvidence,
            Timing = CreateToolTiming(result.Elapsed.TotalMilliseconds)
        };

    private static InspectionRunArtifacts CreateArtifacts(
        RunArtifactOptions options,
        string runnerReportPath,
        string? viewerContractPath) =>
        new(
            Path.GetFullPath(runnerReportPath),
            FullOptionalPath(viewerContractPath),
            FullOptionalPath(options.ViewerScreenshotPath),
            FullOptionalPath(options.JsonPath),
            FullOptionalPath(options.HtmlPath),
            FullOptionalPath(options.CsvPath));

    private static InspectionRunTiming CreateToolTiming(
        double elapsedMilliseconds) =>
        InspectionRunTiming.Available(
            InspectionRunTiming.StopwatchClock,
            elapsedMilliseconds,
            [
                new InspectionRunStageTiming(
                    InspectionRunTiming.ToolExecutionStage,
                    elapsedMilliseconds)
            ],
            "Existing ToolResult elapsed observation; no additional execution.");

    private static InspectionRunTiming CreateSurfaceMatchTiming(
        SurfaceMatchExecutionArtifact execution,
        SurfaceAndEdgeMatchAssessmentArtifact? assessment,
        SurfaceMatchRuntimeReport? runtime)
    {
        if (runtime is null)
        {
            return InspectionRunTiming.Unavailable(
                "No persisted Surface Match runtime evidence was supplied.");
        }

        if (!SurfaceMatchAssessmentArtifactValidator.InspectRuntime(
                runtime,
                out var runtimeEvidence)
            || !string.Equals(
                runtime.ExecutionContentSha256,
                execution.ContentSha256,
                StringComparison.Ordinal)
            || assessment is null
            || !string.Equals(
                runtime.AssessmentContentSha256,
                assessment.ContentSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Surface Match runtime does not match the identified execution and assessment: "
                + runtimeEvidence);
        }

        return InspectionRunTiming.Available(
            runtime.Clock,
            runtime.TotalMilliseconds,
            runtime.Stages.Select(stage => new InspectionRunStageTiming(
                stage.StageId,
                TimeSpan.FromTicks(stage.ElapsedTicks).TotalMilliseconds)),
            "Persisted Surface Match runtime evidence; matching was not recomputed.");
    }

    private static InspectionRunMetric[] ToMetrics(IEnumerable<Metric> metrics) =>
        metrics
            .Where(metric => double.IsFinite(metric.Value))
            .Select(metric => new InspectionRunMetric(metric.Name, metric.Kind, metric.Value, metric.Unit, metric.Status))
            .ToArray();

    private static InspectionRunOverlay[] ToOverlays(IEnumerable<Overlay> overlays) =>
        overlays.Select(overlay => new InspectionRunOverlay(overlay.Id, overlay.Kind, overlay.Label, overlay.Status, overlay.SourceEntityId)).ToArray();

    private static void WriteOutputs(RunArtifactOptions options, InspectionRunRecord record)
    {
        // Select outputs only. Reporting formats the completed evidence; it must not rerun or observe the input.
        if (options.JsonPath is not null) WriteJson(options.JsonPath, record);
        if (options.HtmlPath is not null) InspectionRunRecordReports.WriteHtml(options.HtmlPath, record);
        if (options.CsvPath is not null) InspectionRunRecordReports.WriteCsv(options.CsvPath, record);
    }

    private static void WriteJson(string path, InspectionRunRecord record)
    {
        InspectionRunRecordJson.Write(path, record);
    }

    private static InspectionRunEnvironment CreateExecutionEnvironment(string recipePath)
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(RunRecordWriter).Assembly;
        var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(attribute => attribute.Key, attribute => attribute.Value, StringComparer.Ordinal);
        var applicationVersion = metadata.GetValueOrDefault("OpenVisionLabProductVersion")
            ?? assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
        var viewerHostApiVersion = metadata.GetValueOrDefault("OpenVisionLabViewerHostApiVersion") ?? "unknown";
        var (gitCommit, gitWorkingTree) = ReadGitIdentity(recipePath);

        return new InspectionRunEnvironment(
            assembly.GetName().Name ?? "OpenVisionLab.ThreeD.Runner",
            applicationVersion,
            viewerHostApiVersion,
            gitCommit,
            gitWorkingTree,
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.OSDescription,
            RuntimeInformation.ProcessArchitecture.ToString());
    }

    private static (string Commit, string WorkingTree) ReadGitIdentity(string recipePath)
    {
        var workingDirectory = FindGitWorkingDirectory(Path.GetDirectoryName(Path.GetFullPath(recipePath)))
            ?? FindGitWorkingDirectory(Environment.CurrentDirectory);
        if (workingDirectory is null)
        {
            return (Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "unknown", "unknown");
        }

        var commit = RunGit(workingDirectory, "rev-parse", "HEAD");
        var status = RunGit(workingDirectory, "status", "--porcelain");
        return (
            string.IsNullOrWhiteSpace(commit) ? Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "unknown" : commit,
            status is null ? "unknown" : string.IsNullOrWhiteSpace(status) ? "clean" : "dirty");
    }

    private static string? FindGitWorkingDirectory(string? startPath)
    {
        var directory = string.IsNullOrWhiteSpace(startPath) ? null : new DirectoryInfo(startPath);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git"))
                || File.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string? RunGit(string workingDirectory, params string[] arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("git")
                {
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
            if (!process.Start()) return null;
            var output = process.StandardOutput.ReadToEnd().Trim();
            if (!process.WaitForExit(3000) || process.ExitCode != 0) return null;
            return output;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string? FullOptionalPath(string? path) => string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
}
