using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class ToolRecipeFilterRunnerExecution
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public static int Run(string recipePath, string stepId, string outputC3DPath, string reportPath)
    {
        string? outputTemporaryPath = null;
        string? reportTemporaryPath = null;
        string? outputRollbackPath = null;
        var outputHadPreviousFile = false;
        var outputPublished = false;
        var reportPublished = false;
        var outputSha256 = string.Empty;
        var reportSha256 = string.Empty;
        var fullOutputPath = string.Empty;
        try
        {
            var fullRecipePath = Path.GetFullPath(recipePath);
            var document = ToolRecipeDocumentStore.Load(fullRecipePath);
            var sourcePath = ResolveSourcePath(fullRecipePath, document.Source.Path);
            fullOutputPath = Path.GetFullPath(outputC3DPath);
            var fullReportPath = Path.GetFullPath(reportPath);
            ValidateDistinctPaths(fullRecipePath, sourcePath, fullOutputPath, fullReportPath);
            var evaluation = ToolRecipeFilterExecution.Execute(
                document,
                stepId,
                Path.GetDirectoryName(fullRecipePath));
            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                Console.Error.WriteLine(evaluation.Result.Message);
                return 5;
            }

            outputTemporaryPath = CreateTemporaryPath(fullOutputPath);
            reportTemporaryPath = CreateTemporaryPath(fullReportPath);
            outputRollbackPath = CreateTemporaryPath(fullOutputPath);
            outputHadPreviousFile = File.Exists(fullOutputPath);
            StageC3D(evaluation.Output, outputTemporaryPath);
            outputSha256 = ComputeFileSha256(outputTemporaryPath);
            if (!string.Equals(
                    outputSha256,
                    evaluation.Output.ContentSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Filter Runner staged output hash does not match the evaluated output.");
            }

            var report = new
            {
                schemaVersion = "1.0",
                recipe = new { path = fullRecipePath, schemaVersion = document.SchemaVersion, name = document.Name },
                step = new { id = stepId, toolId = "filter", status = evaluation.Result.Status.ToString() },
                source = new
                {
                    id = document.Source.Id,
                    path = document.Source.Path,
                    byteLength = document.Source.ByteLength,
                    contentSha256 = document.Source.ContentSha256,
                    width = document.Source.GridWidth,
                    height = document.Source.GridHeight,
                    unit = document.Source.Unit,
                    frameId = document.Source.FrameId
                },
                output = new
                {
                    id = evaluation.Output.EntityId,
                    path = fullOutputPath,
                    byteLength = evaluation.Output.ByteLength,
                    contentSha256 = evaluation.Output.ContentSha256,
                    rootSourceSha256 = evaluation.Output.RootSourceSha256,
                    width = evaluation.Output.Width,
                    height = evaluation.Output.Height,
                    validCount = evaluation.Output.ValidCount,
                    missingCount = evaluation.Output.MissingCount,
                    minimum = evaluation.Output.Minimum,
                    maximum = evaluation.Output.Maximum,
                    mean = evaluation.Output.Mean,
                    provenance = evaluation.Output.Provenance
                },
                result = new
                {
                    status = evaluation.Result.Status.ToString(),
                    message = evaluation.Result.Message,
                    elapsedMilliseconds = evaluation.Result.Elapsed.TotalMilliseconds,
                    metrics = evaluation.Result.Metrics.Select(metric => new { name = metric.Name, value = metric.Value, unit = metric.Unit })
                },
                publication = new
                {
                    contractVersion = "1.0",
                    state = "Committed",
                    output = new
                    {
                        path = fullOutputPath,
                        byteLength = new FileInfo(outputTemporaryPath).Length,
                        contentSha256 = outputSha256
                    },
                    report = new { path = fullReportPath }
                },
                claimBoundary = "Preprocessing output in the uncalibrated raw-height/display frame; no measurement OK/NG or physical metrology claim."
            };
            StageText(
                reportTemporaryPath,
                JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            reportSha256 = ComputeFileSha256(reportTemporaryPath);

            if (outputHadPreviousFile)
            {
                File.Copy(fullOutputPath, outputRollbackPath, overwrite: false);
            }

            try
            {
                File.Move(outputTemporaryPath, fullOutputPath, overwrite: true);
                outputPublished = true;
            }
            catch
            {
                outputPublished = FileMatchesSha256(fullOutputPath, outputSha256);
                throw;
            }

            try
            {
                File.Move(reportTemporaryPath, fullReportPath, overwrite: true);
                reportPublished = true;
            }
            catch
            {
                reportPublished = FileMatchesSha256(fullReportPath, reportSha256);
                throw;
            }

            TryDeleteFile(outputRollbackPath);
            Console.WriteLine($"Filter output: {fullOutputPath}");
            Console.WriteLine($"Filter SHA-256: {outputSha256}");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or ArgumentException or OverflowException)
        {
            if (outputPublished && !reportPublished && !string.IsNullOrWhiteSpace(fullOutputPath))
            {
                TryRestoreOutput(
                    fullOutputPath,
                    rollbackPath: outputRollbackPath,
                    hadPreviousFile: outputHadPreviousFile,
                    publishedSha256: outputSha256);
            }

            Console.Error.WriteLine(exception.Message);
            return 5;
        }
        finally
        {
            TryDeleteFile(outputTemporaryPath);
            TryDeleteFile(reportTemporaryPath);
            TryDeleteFile(outputRollbackPath);
        }
    }

    private static void ValidateDistinctPaths(
        string recipePath,
        string sourcePath,
        string outputPath,
        string reportPath)
    {
        var paths = new[]
        {
            (Name: "recipe", Path: ResolvePathIdentity(recipePath)),
            (Name: "source", Path: ResolvePathIdentity(sourcePath)),
            (Name: "output", Path: ResolvePathIdentity(outputPath)),
            (Name: "report", Path: ResolvePathIdentity(reportPath))
        };

        for (var firstIndex = 0; firstIndex < paths.Length; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < paths.Length; secondIndex++)
            {
                if (PathComparer.Equals(paths[firstIndex].Path, paths[secondIndex].Path))
                {
                    throw new InvalidDataException(
                        $"Filter Runner {paths[firstIndex].Name} and {paths[secondIndex].Name} paths must differ: '{paths[firstIndex].Path}'.");
                }
            }
        }
    }

    private static string ResolveSourcePath(string recipePath, string sourcePath) =>
        Path.IsPathFullyQualified(sourcePath)
            ? Path.GetFullPath(sourcePath)
            : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(recipePath) ?? Environment.CurrentDirectory, sourcePath));

    private static string ResolvePathIdentity(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath)
            ?? throw new ArgumentException($"Path has no root: '{path}'.", nameof(path));
        var current = root;
        var relativePath = Path.GetRelativePath(root, fullPath);
        if (relativePath == ".")
        {
            return fullPath;
        }

        foreach (var segment in relativePath.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (TryResolveReparsePoint(current, out var resolvedPath))
            {
                current = resolvedPath;
            }
        }

        return current;
    }

    private static bool TryResolveReparsePoint(string path, out string resolvedPath)
    {
        resolvedPath = path;
        FileSystemInfo entry;
        if (Directory.Exists(path))
        {
            entry = new DirectoryInfo(path);
        }
        else if (File.Exists(path))
        {
            entry = new FileInfo(path);
        }
        else
        {
            return false;
        }

        if (!entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            return false;
        }

        var target = entry.ResolveLinkTarget(returnFinalTarget: true);
        if (target is null)
        {
            return false;
        }

        resolvedPath = Path.GetFullPath(target.FullName);
        return true;
    }

    private static void StageC3D(C3DHeightFieldSnapshot output, string path)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory);
        output.SaveC3D(fullPath);
        using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.WriteThrough);
        stream.Flush(flushToDisk: true);
    }

    private static void StageText(string path, string content)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory);
        using var stream = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            4096,
            FileOptions.WriteThrough);
        using var writer = new StreamWriter(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            4096,
            leaveOpen: true);
        writer.Write(content);
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }

    private static string CreateTemporaryPath(string path) =>
        $"{Path.GetFullPath(path)}.tmp.{Guid.NewGuid():N}";

    private static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static bool FileMatchesSha256(string path, string expectedSha256)
    {
        try
        {
            return File.Exists(path)
                && string.Equals(
                    ComputeFileSha256(path),
                    expectedSha256,
                    StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void TryRestoreOutput(
        string fullOutputPath,
        string? rollbackPath,
        bool hadPreviousFile,
        string publishedSha256)
    {
        try
        {
            if (hadPreviousFile && !string.IsNullOrWhiteSpace(rollbackPath) && File.Exists(rollbackPath))
            {
                File.Move(rollbackPath, fullOutputPath, overwrite: true);
                return;
            }

            if (!hadPreviousFile && FileMatchesSha256(fullOutputPath, publishedSha256))
            {
                File.Delete(fullOutputPath);
            }
        }
        catch (Exception rollbackException) when (
            rollbackException is IOException
                or UnauthorizedAccessException
                or ArgumentException)
        {
            Console.Error.WriteLine($"Filter Runner output rollback could not complete: {rollbackException.Message}");
        }
    }

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

}
