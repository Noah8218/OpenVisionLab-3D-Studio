using System.IO;
using System.Text.Json;
using System.Threading;
using static OpenVisionLab.ThreeD.Shell.ViewModels.Workbench.ToolWorkbenchCancellationSourceLifetime;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns ROI / Crop Preview state, output persistence, and cancellation. The
/// registered cancellation source identifies the operation allowed to publish
/// the derived HeightField or request a display update.
/// </summary>
internal sealed class ToolWorkbenchRoiCropExecutionOwner : IDisposable
{
    private const string ArtifactSchemaVersion = "1.0";
    private static readonly JsonSerializerOptions SidecarJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly Func<bool> isSelected;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedStep;
    private readonly Func<bool> isSourceReady;
    private readonly Func<bool> hasPendingParameters;
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Func<string?> getRecipePath;
    private readonly Func<object?, bool> isSourceChangeEvent;
    private readonly Action<string, string> appendLog;
    private readonly Action<ToolWorkbenchFilterDisplayRequestEventArgs> requestDisplay;
    private readonly Action onStateChanged;

    private CancellationTokenSource? cancellation;
    private C3DRoiCropEvaluation? preview;
    private string? previewPath;
    private bool isRunning;
    private bool isStale;
    private bool isPublished;
    private string summary = "Select ROI / Crop, teach one GridRectangle, then Preview.";
    private int disposalState;

    public ToolWorkbenchRoiCropExecutionOwner(
        Func<bool> isSelected,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedStep,
        Func<bool> isSourceReady,
        Func<bool> hasPendingParameters,
        Func<ToolRecipeDocument> createDocument,
        Func<string?> getRecipePath,
        Func<object?, bool> isSourceChangeEvent,
        Action<string, string> appendLog,
        Action<ToolWorkbenchFilterDisplayRequestEventArgs> requestDisplay,
        Action onStateChanged)
    {
        this.isSelected = isSelected;
        this.getSelectedStep = getSelectedStep;
        this.isSourceReady = isSourceReady;
        this.hasPendingParameters = hasPendingParameters;
        this.createDocument = createDocument;
        this.getRecipePath = getRecipePath;
        this.isSourceChangeEvent = isSourceChangeEvent;
        this.appendLog = appendLog;
        this.requestDisplay = requestDisplay;
        this.onStateChanged = onStateChanged;
    }

    public bool IsSelected => !IsDisposed && isSelected();
    public bool IsRunning => !IsDisposed && isRunning;
    public bool HasCurrentPreview => !IsDisposed && preview?.Output is not null && !isStale;
    public bool IsStale => !IsDisposed && isStale;
    public bool IsPublished => !IsDisposed && isPublished;
    public C3DHeightFieldSnapshot? CurrentOutput => IsDisposed ? null : preview?.Output;
    public ToolRecipeGridRectangle? CurrentRegion => IsDisposed ? null : preview?.SourceRegion;
    public string? CurrentPreviewPath => IsDisposed ? null : previewPath;
    public string Summary => IsDisposed
        ? "ROI / Crop execution owner has been disposed."
        : summary;
    public string RegionSummary => IsDisposed || preview?.SourceRegion is not { } region
        ? "No crop region evidence until Preview completes."
        : $"Source row {region.Row}, column {region.Column} | {region.RowCount} x {region.ColumnCount}";
    public string OutputSummary => IsDisposed || preview?.Output is not { } output
        ? "No cropped HeightField output."
        : $"{output.Width} x {output.Height} | source origin ({output.GridOriginColumn}, {output.GridOriginRow}) | valid {output.ValidCount:N0} | missing {output.MissingCount:N0} | {(isPublished ? "Published" : "Preview only")}";

    public bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        var currentCancellation = Interlocked.Exchange(
            ref cancellation,
            null);
        CancelAndDispose(currentCancellation);
        preview = null;
        previewPath = null;
        isRunning = false;
        isStale = false;
        isPublished = false;
    }

    public bool TryGetPublishedOutput(string outputEntityId, out C3DHeightFieldSnapshot? output)
    {
        if (IsDisposed)
        {
            output = null;
            return false;
        }

        var current = preview?.Output;
        output = isPublished && !isStale
            && string.Equals(current?.EntityId, outputEntityId, StringComparison.OrdinalIgnoreCase)
            ? current
            : null;
        return output is not null;
    }

    public async Task<bool> PreviewAsync()
    {
        if (IsDisposed
            || !CanPreview()
            || getSelectedStep() is not { } step)
        {
            return false;
        }

        var currentCancellation = new CancellationTokenSource();
        var cancellationToken = currentCancellation.Token;
        var previousCancellation = Interlocked.Exchange(
            ref cancellation,
            currentCancellation);
        CancelAndDispose(previousCancellation);
        if (IsDisposed)
        {
            if (ReferenceEquals(
                Interlocked.CompareExchange(
                    ref cancellation,
                    null,
                    currentCancellation),
                currentCancellation))
            {
                currentCancellation.Dispose();
            }

            return false;
        }

        SetRunning(true);
        isStale = false;
        isPublished = false;
        step.State = "Preview running";
        SetSummary("ROI / Crop Preview is copying the selected source cells without changing the source.");
        if (!IsCurrentPreview(currentCancellation))
        {
            return false;
        }

        appendLog("Preview", $"ROI / Crop Preview started: {step.Id}.");
        try
        {
            var document = createDocument();
            var evaluation = await Task.Run(
                () => ToolRecipeRoiCropExecution.Execute(
                    document,
                    step.Id,
                    GetRecipeDirectory(),
                    cancellationToken),
                cancellationToken);

            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }
            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                if (!IsCurrentPreview(currentCancellation))
                {
                    return false;
                }

                preview = evaluation;
                previewPath = null;
                step.State = "Error";
                SetSummary(evaluation.Result.Message);
                if (IsCurrentPreview(currentCancellation))
                {
                    appendLog("Error", $"ROI / Crop Preview did not produce output: {evaluation.Result.Message}");
                }

                return false;
            }

            preview = evaluation;
            previewPath = CreatePreviewPath(evaluation.Output.ContentSha256);
            SaveC3DAtomically(evaluation.Output, previewPath);
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            step.State = "Preview ready";
            SetSummary($"Preview ready | {RegionSummary} | output {evaluation.Output.ContentSha256} | source unchanged");
            if (IsCurrentPreview(currentCancellation))
            {
                appendLog("Preview", $"ROI / Crop Preview ready: output={evaluation.Output.ContentSha256}; {RegionSummary}.");
            }

            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            requestDisplay(new ToolWorkbenchFilterDisplayRequestEventArgs(
                previewPath,
                evaluation.Output.ContentSha256,
                false,
                "ROI / Crop Preview"));
            return true;
        }
        catch (OperationCanceledException)
        {
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            step.State = "Ready";
            SetSummary("Preview canceled. Source and authored recipe were not changed.");
            if (IsCurrentPreview(currentCancellation))
            {
                appendLog("Preview", "ROI / Crop Preview canceled.");
            }

            return false;
        }
        finally
        {
            var ownsCancellation = ReferenceEquals(
                Interlocked.CompareExchange(
                    ref cancellation,
                    null,
                    currentCancellation),
                currentCancellation);
            if (ownsCancellation)
            {
                currentCancellation.Dispose();
            }

            if (ownsCancellation && !IsDisposed)
            {
                SetRunning(false);
            }
        }
    }

    public bool CanPreview() => !IsDisposed
        && IsSelected
        && isSourceReady()
        && !hasPendingParameters()
        && !isRunning
        && getSelectedStep() is { } step
        && ToolRecipeValidator.ValidateForStepExecution(createDocument(), step.Id).IsValid;

    public void Publish()
    {
        if (IsDisposed
            || getSelectedStep() is not { } step
            || !HasCurrentPreview)
        {
            return;
        }
        isPublished = true;
        step.State = "Published";
        SetSummary($"Published {step.OutputEntityId} | output SHA-256 {preview!.Output!.ContentSha256} | {RegionSummary} | source unchanged");
        appendLog("Publish", $"ROI / Crop output published without re-running: {step.OutputEntityId}.");
        PersistPublishedArtifactIfPossible();
    }

    public void PersistPublishedArtifactIfPossible()
    {
        if (IsDisposed
            || !isPublished
            || isStale
            || preview?.Output is not { } output
            || preview.SourceRegion is not { } sourceRegion
            || string.IsNullOrWhiteSpace(getRecipePath()))
        {
            return;
        }

        try
        {
            var document = createDocument();
            var step = document.Steps.FirstOrDefault(candidate =>
                string.Equals(candidate.ToolId, "roi-crop", StringComparison.Ordinal));
            if (step is null)
            {
                throw new InvalidDataException("ROI / Crop recipe step is missing.");
            }

            var source = RequireRecipeSource(document.Source);
            if (!string.Equals(output.RootSourceSha256, source.ContentSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("ROI / Crop output root source does not match the current recipe source.");
            }

            var recipePath = getRecipePath()!;
            var recipeDirectory = GetRecipeDirectory();
            var c3dPath = GetArtifactC3DPath(recipeDirectory, recipePath, step.OutputEntityId);
            SaveC3DAtomically(output, c3dPath);
            var record = new RoiCropArtifactRecord(
                ArtifactSchemaVersion,
                step.Id,
                step.OutputEntityId,
                source.Id,
                source.ContentSha256!,
                source.ByteLength!.Value,
                source.GridWidth!.Value,
                source.GridHeight!.Value,
                source.Unit,
                source.FrameId,
                sourceRegion,
                output.ContentSha256,
                new FileInfo(c3dPath).Length,
                output.Width,
                output.Height,
                output.GridOriginColumn,
                output.GridOriginRow,
                output.RootSourceSha256,
                output.Unit,
                output.FrameId,
                output.Provenance);
            var sidecarPath = GetArtifactSidecarPath(recipeDirectory, recipePath, step.OutputEntityId);
            WriteTextAtomically(sidecarPath, JsonSerializer.Serialize(record, SidecarJsonOptions));
            previewPath = c3dPath;
            if (IsDisposed)
            {
                return;
            }

            appendLog("Save", $"ROI / Crop output and provenance sidecar saved: {sidecarPath}.");
            onStateChanged();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            if (IsDisposed)
            {
                return;
            }

            SetSummary($"Published in this session, but the ROI / Crop sidecar could not be saved: {exception.Message}");
            appendLog("Error", $"ROI / Crop sidecar save failed: {exception.Message}");
        }
    }

    public void RestorePublishedArtifact()
    {
        if (IsDisposed)
        {
            return;
        }

        var recipePath = getRecipePath();
        var document = createDocument();
        var step = document.Steps.FirstOrDefault(candidate =>
            string.Equals(candidate.ToolId, "roi-crop", StringComparison.Ordinal));
        if (step is null || string.IsNullOrWhiteSpace(recipePath))
        {
            return;
        }

        var recipeDirectory = GetRecipeDirectory();
        var sidecarPath = GetArtifactSidecarPath(recipeDirectory, recipePath, step.OutputEntityId);
        var c3dPath = GetArtifactC3DPath(recipeDirectory, recipePath, step.OutputEntityId);
        var hasSidecar = File.Exists(sidecarPath);
        var hasC3D = File.Exists(c3dPath);
        var hasDownstreamArtifactReference = (document.Selections ?? []).Any(selection =>
            string.Equals(selection.SourceBinding.OwnerEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase));
        if (!hasSidecar && !hasC3D)
        {
            if (hasDownstreamArtifactReference)
            {
                ReportRestoreFailure("published crop metadata is missing.", sidecarPath);
            }

            return;
        }
        if (!hasSidecar || !hasC3D)
        {
            ReportRestoreFailure("published crop C3D bytes and metadata must be present together.", sidecarPath);
            return;
        }

        try
        {
            var record = JsonSerializer.Deserialize<RoiCropArtifactRecord>(
                File.ReadAllText(sidecarPath),
                SidecarJsonOptions)
                ?? throw new InvalidDataException("ROI / Crop sidecar is empty.");
            if (!string.Equals(record.SchemaVersion, ArtifactSchemaVersion, StringComparison.Ordinal)
                || !string.Equals(record.StepId, step.Id, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(record.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("ROI / Crop sidecar and recipe step identities do not match.");
            }

            var source = LoadRecipeSource(document.Source);
            var selection = FindCropSelection(document, step);
            if (!string.Equals(record.SourceEntityId, source.EntityId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(record.SourceContentSha256, source.ContentSha256, StringComparison.OrdinalIgnoreCase)
                || record.SourceByteLength != source.ByteLength
                || record.SourceWidth != source.Width
                || record.SourceHeight != source.Height
                || !string.Equals(record.SourceUnit, source.Unit, StringComparison.Ordinal)
                || !string.Equals(record.SourceFrameId, source.FrameId, StringComparison.Ordinal))
            {
                throw new InvalidDataException("ROI / Crop sidecar source identity does not match the current recipe source.");
            }
            if (record.SourceRegion != selection.GridRectangle)
            {
                throw new InvalidDataException("ROI / Crop sidecar ROI does not match the recipe selection.");
            }
            if (!string.Equals(record.OutputRootSourceSha256, source.ContentSha256, StringComparison.OrdinalIgnoreCase)
                || record.OutputGridOriginColumn != checked(source.GridOriginColumn + selection.GridRectangle!.Column)
                || record.OutputGridOriginRow != checked(source.GridOriginRow + selection.GridRectangle.Row)
                || record.OutputWidth != selection.GridRectangle.ColumnCount
                || record.OutputHeight != selection.GridRectangle.RowCount
                || !string.Equals(record.OutputUnit, source.Unit, StringComparison.Ordinal)
                || !string.Equals(record.OutputFrameId, source.FrameId, StringComparison.Ordinal))
            {
                throw new InvalidDataException("ROI / Crop sidecar output origin, grid, unit, frame, or root identity is invalid.");
            }

            var restored = C3DHeightFieldSnapshot.LoadDerivedVerified(
                c3dPath,
                record.OutputEntityId,
                record.OutputUnit,
                record.OutputFrameId,
                record.OutputByteLength,
                record.OutputContentSha256,
                record.OutputRootSourceSha256,
                record.OutputWidth,
                record.OutputHeight,
                record.OutputGridOriginColumn,
                record.OutputGridOriginRow,
                record.OutputProvenance);
            var binding = ToolRecipeSelectionSourceBindingVerifier.FromHeightField(restored);
            foreach (var downstreamSelection in (document.Selections ?? []).Where(selection =>
                         string.Equals(selection.SourceBinding.OwnerEntityId, record.OutputEntityId, StringComparison.OrdinalIgnoreCase)))
            {
                if (!ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(
                        downstreamSelection.SourceBinding,
                        binding))
                {
                    throw new InvalidDataException("ROI / Crop downstream selection metadata does not match the restored output.");
                }
            }

            preview = new C3DRoiCropEvaluation(
                new ToolResult(
                    C3DRoiCropRule.ToolName,
                    ResultStatus.Pass,
                    "The published ROI / Crop output was restored from canonical C3D bytes and recipe metadata without executing the crop.",
                    TimeSpan.Zero,
                    [],
                    []),
                restored,
                selection.GridRectangle);
            previewPath = c3dPath;
            isStale = false;
            isPublished = true;
            if (getSelectedStep() is { } selected
                && string.Equals(selected.Id, step.Id, StringComparison.OrdinalIgnoreCase))
            {
                selected.State = "Published";
            }

            SetSummary($"Restored Published ROI / Crop output {restored.EntityId} without executing the crop.");
            appendLog("Open", $"ROI / Crop output restored with sidecar metadata: {sidecarPath}.");
            onStateChanged();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or JsonException)
        {
            if (IsDisposed)
            {
                return;
            }

            ReportRestoreFailure(exception.Message, sidecarPath);
        }
    }

    public void Cancel()
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            Volatile.Read(ref cancellation)?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A concurrent owner disposal already released the token source.
        }
    }

    public void MarkStaleIfNeeded(object? sender)
    {
        if (IsDisposed
            || preview is null
            || isRunning)
        {
            return;
        }
        var step = getSelectedStep();
        var isSelectedParameter = IsSelected
            && sender is ToolWorkbenchParameterItem parameter
            && (step?.Parameters.Contains(parameter) ?? false);
        var isSelection = sender is ToolRecipeSelection selection
            && step?.InputEntityIds.Skip(1).Contains(selection.Id, StringComparer.OrdinalIgnoreCase) == true;
        if (isSourceChangeEvent(sender) || (IsSelected && ReferenceEquals(sender, step)) || isSelectedParameter || isSelection)
        {
            isStale = true;
            isPublished = false;
            if (step is not null)
            {
                step.State = "Preview stale";
            }
            SetSummary("Source, crop ROI, routing, output, or fixed crop policy changed. Preview again before Publish.");
        }
    }

    public void Clear(string value)
    {
        if (IsDisposed)
        {
            return;
        }

        var currentCancellation = Interlocked.Exchange(
            ref cancellation,
            null);
        CancelAndDispose(currentCancellation);
        preview = null;
        previewPath = null;
        isStale = false;
        isPublished = false;
        SetRunning(false);
        SetSummary(value);
    }

    public void Refresh()
    {
        if (IsDisposed)
        {
            return;
        }

        if (getSelectedStep() is { } step
            && IsSelected
            && preview is null
            && !isRunning
            && step.State is "Taught / pending" or "Taught / needs correction")
        {
            step.State = ToolRecipeValidator.ValidateForStepExecution(createDocument(), step.Id).IsValid
                ? "Ready"
                : "Taught / needs correction";
        }
        onStateChanged();
    }

    private void SetRunning(bool value)
    {
        if (IsDisposed)
        {
            return;
        }

        isRunning = value;
        onStateChanged();
    }

    private void SetSummary(string value)
    {
        if (IsDisposed)
        {
            return;
        }

        summary = value;
        onStateChanged();
    }

    private void ReportRestoreFailure(string reason, string sidecarPath)
    {
        if (IsDisposed)
        {
            return;
        }

        SetSummary($"Saved ROI / Crop output was not restored: {reason}");
        appendLog("Warning", $"ROI / Crop output restore skipped: {reason} | sidecar={sidecarPath}");
    }

    private static ToolRecipeSource RequireRecipeSource(ToolRecipeSource source)
    {
        if (!string.Equals(source.Format, "C3D", StringComparison.OrdinalIgnoreCase)
            || source.ByteLength is not { } byteLength
            || string.IsNullOrWhiteSpace(source.ContentSha256)
            || source.GridWidth is not { } width
            || source.GridHeight is not { } height
            || byteLength <= 0
            || width <= 0
            || height <= 0)
        {
            throw new InvalidDataException("ROI / Crop recipe source identity is incomplete.");
        }

        return source;
    }

    private static C3DHeightFieldSnapshot LoadRecipeSource(ToolRecipeSource source)
    {
        var completeSource = RequireRecipeSource(source);
        return C3DHeightFieldSnapshot.LoadVerified(
            completeSource.Path,
            completeSource.Id,
            completeSource.Unit,
            completeSource.FrameId,
            completeSource.ByteLength!.Value,
            completeSource.ContentSha256!,
            completeSource.GridWidth!.Value,
            completeSource.GridHeight!.Value);
    }

    private static ToolRecipeSelection FindCropSelection(
        ToolRecipeDocument document,
        ToolRecipeStep step)
    {
        if (step.InputEntityIds.Count != 2)
        {
            throw new InvalidDataException("ROI / Crop recipe step must contain one source and one ROI selection.");
        }

        var selection = (document.Selections ?? []).SingleOrDefault(candidate =>
            string.Equals(candidate.Id, step.InputEntityIds[1], StringComparison.OrdinalIgnoreCase));
        if (selection?.GridRectangle is null
            || !string.Equals(selection.Kind, ToolRecipeSelectionKinds.GridRectangle, StringComparison.Ordinal)
            || !string.Equals(selection.RootSourceId, document.Source.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("ROI / Crop recipe selection is missing or does not target the current source.");
        }

        return selection;
    }

    private bool IsCurrentPreview(CancellationTokenSource currentCancellation) =>
        !IsDisposed && ReferenceEquals(
            Volatile.Read(ref cancellation),
            currentCancellation);

    private string GetRecipeDirectory()
    {
        var path = getRecipePath();
        var directory = string.IsNullOrWhiteSpace(path)
            ? null
            : Path.GetDirectoryName(Path.GetFullPath(path));
        return string.IsNullOrWhiteSpace(directory) ? Environment.CurrentDirectory : directory;
    }

    private static string CreatePreviewPath(string hash)
    {
        var testArtifactRoot = Environment.GetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT");
        var directory = string.IsNullOrWhiteSpace(testArtifactRoot)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OpenVisionLab",
                "3DStudio",
                "Preview")
            : Path.Combine(Path.GetFullPath(testArtifactRoot), "Preview");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"roi-crop-{hash}.c3d");
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

    private static void WriteTextAtomically(string path, string contents)
    {
        var fullPath = Path.GetFullPath(path);
        var temporaryPath = $"{fullPath}.tmp.{Guid.NewGuid():N}";
        try
        {
            File.WriteAllText(temporaryPath, contents);
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

    private static string GetArtifactC3DPath(
        string recipeDirectory,
        string recipePath,
        string outputEntityId) =>
        Path.Combine(
            recipeDirectory,
            $"{Path.GetFileNameWithoutExtension(recipePath)}.roi-crop.{Sanitize(outputEntityId)}.c3d");

    private static string GetArtifactSidecarPath(
        string recipeDirectory,
        string recipePath,
        string outputEntityId) =>
        Path.Combine(
            recipeDirectory,
            $"{Path.GetFileNameWithoutExtension(recipePath)}.roi-crop.{Sanitize(outputEntityId)}.json");

    private static string Sanitize(string value) =>
        string.Concat(value.Select(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_'));

    private sealed record RoiCropArtifactRecord(
        string SchemaVersion,
        string StepId,
        string OutputEntityId,
        string SourceEntityId,
        string SourceContentSha256,
        long SourceByteLength,
        int SourceWidth,
        int SourceHeight,
        string SourceUnit,
        string SourceFrameId,
        ToolRecipeGridRectangle SourceRegion,
        string OutputContentSha256,
        long OutputByteLength,
        int OutputWidth,
        int OutputHeight,
        int OutputGridOriginColumn,
        int OutputGridOriginRow,
        string OutputRootSourceSha256,
        string OutputUnit,
        string OutputFrameId,
        string OutputProvenance);
}
