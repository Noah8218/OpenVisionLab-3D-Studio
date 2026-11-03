using System.Text;
using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class ToolRecipeRemoveOutlierPixelsRunnerExecution
{
    public static int Run(
        string recipePath,
        string stepId,
        string outputC3DPath,
        string reportPath)
    {
        try
        {
            var fullReportPath = Path.GetFullPath(reportPath);
            var fullRecipePath = Path.GetFullPath(recipePath);
            var document = ToolRecipeDocumentStore.Load(fullRecipePath);
            var fullOutputPath = Path.GetFullPath(outputC3DPath);
            var declaredSourcePath = document.Source?.Path
                ?? throw new InvalidDataException("Remove-outlier recipe source path is required.");
            var sourcePath = Path.IsPathFullyQualified(declaredSourcePath)
                ? Path.GetFullPath(declaredSourcePath)
                : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(fullRecipePath)!, declaredSourcePath));
            ValidatePublicationPaths(
                fullOutputPath,
                fullReportPath,
                out _,
                ("recipe", fullRecipePath),
                ("source", sourcePath));
            var evaluation = ToolRecipeRemoveOutlierPixelsExecution.Execute(
                document,
                stepId,
                Path.GetDirectoryName(fullRecipePath));
            if (evaluation.Result.Status != ResultStatus.Pass
                || evaluation.Output is null
                || evaluation.OutlierMask is null)
            {
                Console.Error.WriteLine(evaluation.Result.Message);
                return 5;
            }

            SaveC3DAtomically(evaluation.Output, fullOutputPath);
            var report = new
            {
                schemaVersion = "1.0",
                recipe = new
                {
                    path = fullRecipePath,
                    schemaVersion = document.SchemaVersion,
                    name = document.Name
                },
                step = new
                {
                    id = stepId,
                    toolId = "remove-outlier-pixels",
                    status = evaluation.Result.Status.ToString()
                },
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
                outlierMask = new
                {
                    contractVersion = C3DOutlierCellMap.ContractVersion,
                    encoding = C3DOutlierCellMap.Encoding,
                    width = evaluation.OutlierMask.Width,
                    height = evaluation.OutlierMask.Height,
                    outlierCellCount = evaluation.OutlierMask.OutlierCellCount,
                    packedByteLength = evaluation.OutlierMask.PackedByteLength,
                    sha256 = evaluation.OutlierMask.Sha256
                },
                result = new
                {
                    status = evaluation.Result.Status.ToString(),
                    message = evaluation.Result.Message,
                    elapsedMilliseconds =
                        evaluation.Result.Elapsed.TotalMilliseconds,
                    metrics = evaluation.Result.Metrics.Select(
                        metric => new
                        {
                            name = metric.Name,
                            value = metric.Value,
                            unit = metric.Unit
                        })
                },
                sourceMutation = false,
                claimBoundary =
                    "Deterministic raw-height preparation evidence; no physical calibration, measurement OK/NG, or metrology claim."
            };
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
            WriteTextAtomically(
                fullReportPath,
                JsonSerializer.Serialize(
                    report,
                    new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Outlier-removed output: {fullOutputPath}");
            Console.WriteLine(
                $"Output SHA-256: {evaluation.Output.ContentSha256}");
            Console.WriteLine(
                $"Outlier mask SHA-256: {evaluation.OutlierMask.Sha256}");
            return 0;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or JsonException
                or ArgumentException
                or OverflowException)
        {
            Console.Error.WriteLine(exception.Message);
            return 5;
        }
    }

    private static void ValidatePublicationPaths(
        string outputPath,
        string reportPath,
        out bool reportPathIsSafe,
        params (string Name, string Path)[] inputPaths)
    {
        reportPathIsSafe = false;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var outputIdentity = ResolvePathIdentity(outputPath);
        var reportIdentity = ResolvePathIdentity(reportPath);
        var inputs = inputPaths.Select(input =>
            (Name: input.Name, Path: ResolvePathIdentity(input.Path))).ToArray();
        if (string.Equals(outputIdentity, reportIdentity, comparison))
        {
            throw new InvalidDataException("Runner output and report paths must differ.");
        }

        foreach (var input in inputs)
        {
            if (string.Equals(reportIdentity, input.Path, comparison))
            {
                throw new InvalidDataException($"Runner report path must differ from the {input.Name} path.");
            }
        }

        reportPathIsSafe = true;
        foreach (var input in inputs)
        {
            if (string.Equals(outputIdentity, input.Path, comparison))
            {
                throw new InvalidDataException($"Runner output path must differ from the {input.Name} path.");
            }
        }
    }

    private static string ResolvePathIdentity(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath)
            ?? throw new ArgumentException("Runner path must have a root.", nameof(path));
        var relativePath = Path.GetRelativePath(root, fullPath);
        if (relativePath == ".") return Path.TrimEndingDirectorySeparator(fullPath);
        var current = root;
        foreach (var segment in relativePath.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            FileSystemInfo entry = Directory.Exists(current)
                ? new DirectoryInfo(current)
                : new FileInfo(current);
            if (entry.LinkTarget is not null)
            {
                var target = entry.ResolveLinkTarget(returnFinalTarget: true)
                    ?? throw new InvalidDataException("Runner path link could not be resolved.");
                current = Path.GetFullPath(target.FullName);
            }
            else if (entry.Exists && entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new InvalidDataException("Runner path has an unresolved reparse point.");
            }
        }

        return Path.TrimEndingDirectorySeparator(current);
    }

    private static void SaveC3DAtomically(C3DHeightFieldSnapshot output, string path)
    {
        var fullPath = Path.GetFullPath(path);
        var temporaryPath = $"{fullPath}.tmp.{Guid.NewGuid():N}";
        try
        {
            output.SaveC3D(temporaryPath);
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

    private static void WriteTextAtomically(string path, string text)
    {
        var fullPath = Path.GetFullPath(path);
        var temporaryPath = $"{fullPath}.tmp.{Guid.NewGuid():N}";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 4096, leaveOpen: true))
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
