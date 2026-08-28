using System.IO;
using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the Workbench lifecycle for the D-07 Domain / Mask preparation step.
/// It keeps Preview, Publish, persistence, and stale-state policy here while
/// the typed mask operation remains in the Tools adapter and SDK.
/// </summary>
internal sealed class ToolWorkbenchDomainMaskExecutionOwner
{
    private readonly Func<bool> isSelectedStepDomainMask;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep;
    private readonly Func<bool> isSourceReadyForRecipe;
    private readonly Func<bool> hasPendingStepParameterChanges;
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Func<string?> getRecipePath;
    private readonly Func<string, C3DHeightFieldSnapshot?> getPublishedHeightField;
    private readonly Func<string, C3DConnectedRegionArtifact?> getPublishedDomain;
    private readonly Func<object?, bool> isSourceChangeEvent;
    private readonly Action<string, string> appendLog;
    private readonly Action<ToolWorkbenchFilterDisplayRequestEventArgs> requestDisplay;
    private readonly Action onExecutionStateChanged;

    private CancellationTokenSource? previewCancellation;
    private C3DHeightFieldSnapshot? preview;
    private string? previewPath;
    private bool isPreviewRunning;
    private bool isPreviewStale;
    private bool isPreviewPublished;
    private string? previewInputEntityId;
    private string? previewDomainArtifactId;
    private string executionSummary =
        "Select Domain / Mask after publishing a Connected Region, then Preview.";

    public ToolWorkbenchDomainMaskExecutionOwner(
        Func<bool> isSelectedStepDomainMask,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep,
        Func<bool> isSourceReadyForRecipe,
        Func<bool> hasPendingStepParameterChanges,
        Func<ToolRecipeDocument> createDocument,
        Func<string?> getRecipePath,
        Func<string, C3DHeightFieldSnapshot?> getPublishedHeightField,
        Func<string, C3DConnectedRegionArtifact?> getPublishedDomain,
        Func<object?, bool> isSourceChangeEvent,
        Action<string, string> appendLog,
        Action<ToolWorkbenchFilterDisplayRequestEventArgs> requestDisplay,
        Action onExecutionStateChanged)
    {
        this.isSelectedStepDomainMask = isSelectedStepDomainMask;
        this.getSelectedPipelineStep = getSelectedPipelineStep;
        this.isSourceReadyForRecipe = isSourceReadyForRecipe;
        this.hasPendingStepParameterChanges = hasPendingStepParameterChanges;
        this.createDocument = createDocument;
        this.getRecipePath = getRecipePath;
        this.getPublishedHeightField = getPublishedHeightField;
        this.getPublishedDomain = getPublishedDomain;
        this.isSourceChangeEvent = isSourceChangeEvent;
        this.appendLog = appendLog;
        this.requestDisplay = requestDisplay;
        this.onExecutionStateChanged = onExecutionStateChanged;
    }

    public bool IsSelectedStepDomainMask => isSelectedStepDomainMask();
    public bool IsPreviewRunning => isPreviewRunning;
    public bool HasCurrentPreview => preview is not null && !isPreviewStale;
    public bool IsPreviewStale => isPreviewStale;
    public bool IsPreviewPublished => isPreviewPublished;
    public C3DHeightFieldSnapshot? CurrentOutput => preview;
    public string? CurrentOutputPath => previewPath;
    public string ExecutionSummary => executionSummary;
    public string OutputSummary => preview is not { } output
        ? "No Domain / Mask output."
        : $"{output.Width} × {output.Height} | valid {output.ValidCount:N0} | missing {output.MissingCount:N0} | {(isPreviewPublished ? "Published" : "Preview only")}";

    internal bool TryGetPublishedOutput(string outputEntityId, out C3DHeightFieldSnapshot? output)
    {
        output = isPreviewPublished
            && !isPreviewStale
            && preview is not null
            && string.Equals(preview.EntityId, outputEntityId, StringComparison.OrdinalIgnoreCase)
            ? preview
            : null;
        return output is not null;
    }

    public async Task<bool> PreviewAsync()
    {
        if (!CanPreview() || getSelectedPipelineStep() is not { } step)
        {
            return false;
        }

        previewCancellation?.Dispose();
        previewCancellation = new CancellationTokenSource();
        isPreviewRunning = true;
        isPreviewStale = false;
        isPreviewPublished = false;
        step.State = "Preview running";
        SetSummary("Domain / Mask Preview is reducing the complete validated Connected Region domain without changing inputs.");
        appendLog("Preview", $"Domain / Mask Preview started: {step.Id}.");

        try
        {
            var document = createDocument();
            if (!TryResolveInputs(document, step.InputEntityIds, out var source, out var domain, out var reason))
            {
                step.State = "Taught / needs correction";
                SetSummary(reason);
                appendLog("Warning", $"Domain / Mask Preview blocked: {reason}");
                return false;
            }

            var evaluation = await Task.Run(
                () => ToolRecipeDomainMaskExecution.Execute(
                    document,
                    step.Id,
                    source,
                    domain,
                    previewCancellation.Token),
                previewCancellation.Token);
            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                preview = null;
                previewPath = null;
                previewInputEntityId = null;
                previewDomainArtifactId = null;
                step.State = "Error";
                SetSummary(evaluation.Result.Message);
                appendLog("Error", $"Domain / Mask Preview failed: {evaluation.Result.Message}");
                return false;
            }

            preview = evaluation.Output;
            previewInputEntityId = source.EntityId;
            previewDomainArtifactId = domain.ArtifactId;
            previewPath = CreatePreviewPath(evaluation.Output.ContentSha256);
            evaluation.Output.SaveC3D(previewPath);
            step.State = "Preview ready";
            SetSummary(
                $"Preview ready | domain {domain.Regions.Count:N0} region(s) | valid {evaluation.Output.ValidCount:N0} | missing {evaluation.Output.MissingCount:N0} | source unchanged");
            appendLog(
                "Preview",
                $"Domain / Mask Preview ready: output={evaluation.Output.ContentSha256}; source={source.EntityId}; domain={domain.ContentSha256}.");
            requestDisplay(
                new ToolWorkbenchFilterDisplayRequestEventArgs(
                    previewPath,
                    evaluation.Output.ContentSha256,
                    false,
                    "Domain / Mask Preview"));
            return true;
        }
        catch (OperationCanceledException)
        {
            if (getSelectedPipelineStep() is { } canceledStep)
            {
                canceledStep.State = "Ready";
            }

            SetSummary("Preview canceled. Source, domain artifact, and recipe were not changed.");
            appendLog("Preview", "Domain / Mask Preview canceled.");
            return false;
        }
        finally
        {
            isPreviewRunning = false;
            onExecutionStateChanged();
        }
    }

    public bool CanPreview() =>
        isSelectedStepDomainMask()
        && isSourceReadyForRecipe()
        && !hasPendingStepParameterChanges()
        && !isPreviewRunning
        && getSelectedPipelineStep() is { } step
        && step.InputEntityIds.Count == 2
        && getPublishedDomain(step.InputEntityIds[1]) is not null
        && ToolRecipeValidator.ValidateForStepExecution(createDocument(), step.Id).IsValid;

    public void Publish()
    {
        if (getSelectedPipelineStep() is not { } step || !HasCurrentPreview)
        {
            return;
        }

        isPreviewPublished = true;
        step.State = "Published";
        SetSummary(
            $"Published {step.OutputEntityId} | SHA-256 {preview!.ContentSha256} | source and Connected Region remain unchanged");
        appendLog("Publish", $"Domain / Mask output published without re-running: {step.OutputEntityId}.");
        PersistPublishedArtifactIfPossible();
    }

    public void Cancel() => previewCancellation?.Cancel();

    public void MarkStaleIfNeeded(object? sender)
    {
        if (preview is null || isPreviewRunning)
        {
            return;
        }

        var selected = getSelectedPipelineStep();
        var isSelectedParameter = sender is ToolWorkbenchParameterItem parameter
            && (selected?.Parameters.Contains(parameter) ?? false);
        if (selected is { } step
            && (isSourceChangeEvent(sender)
                || ReferenceEquals(sender, step)
                || isSelectedParameter))
        {
            MarkStale(step, "Source, Domain / Mask routing, or upstream identity changed. Preview again before Publish.");
        }
    }

    public void MarkStaleIfUpstreamChanged()
    {
        if (preview is null
            || isPreviewRunning
            || getSelectedPipelineStep() is not { } step
            || step.InputEntityIds.Count != 2)
        {
            return;
        }

        var domain = getPublishedDomain(step.InputEntityIds[1]);
        var source = getPublishedHeightField(step.InputEntityIds[0]);
        var sourceIsRecipeSource = string.Equals(
            step.InputEntityIds[0],
            createDocument().Source.Id,
            StringComparison.OrdinalIgnoreCase);
        if (domain is null
            || (!sourceIsRecipeSource
                && (source is null
                    || !string.Equals(previewInputEntityId, source.EntityId, StringComparison.OrdinalIgnoreCase)))
            || !string.Equals(previewDomainArtifactId, domain.ArtifactId, StringComparison.OrdinalIgnoreCase))
        {
            MarkStale(step, "Published HeightField or Connected Region domain changed. Preview Domain / Mask again before Publish.");
        }
    }

    public void Clear(string summary)
    {
        previewCancellation?.Cancel();
        preview = null;
        previewPath = null;
        previewInputEntityId = null;
        previewDomainArtifactId = null;
        isPreviewStale = false;
        isPreviewPublished = false;
        SetSummary(summary);
    }

    public void RefreshState()
    {
        if (getSelectedPipelineStep() is { } step
            && IsSelectedStepDomainMask
            && preview is null
            && !isPreviewRunning
            && step.State is "Taught / pending" or "Taught / needs correction")
        {
            step.State = CanPreview() ? "Ready" : "Taught / needs correction";
        }

        onExecutionStateChanged();
    }

    public void SetRunning(bool value)
    {
        isPreviewRunning = value;
        onExecutionStateChanged();
    }

    public void PersistPublishedArtifactIfPossible()
    {
        if (!isPreviewPublished
            || isPreviewStale
            || preview is null
            || string.IsNullOrWhiteSpace(getRecipePath()))
        {
            return;
        }

        var step = getSelectedPipelineStep();
        if (step is null
            || string.IsNullOrWhiteSpace(previewInputEntityId)
            || string.IsNullOrWhiteSpace(previewDomainArtifactId))
        {
            return;
        }

        try
        {
            var c3dPath = GetArtifactC3DPath(getRecipePath()!, preview.EntityId);
            preview.SaveC3D(c3dPath);
            var sidecarPath = GetArtifactSidecarPath(getRecipePath()!, preview.EntityId);
            var sidecar = new DomainMaskArtifactRecord(
                step.Id,
                step.OutputEntityId,
                previewInputEntityId,
                previewDomainArtifactId,
                preview.ContentSha256,
                preview.Width,
                preview.Height,
                preview.Unit,
                preview.FrameId,
                preview.Provenance,
                new FileInfo(c3dPath).Length);
            File.WriteAllText(
                sidecarPath,
                JsonSerializer.Serialize(sidecar, new JsonSerializerOptions { WriteIndented = true }));
            previewPath = c3dPath;
            appendLog("Save", $"Domain / Mask output sidecar saved: {sidecarPath}.");
            onExecutionStateChanged();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            SetSummary($"Published in this session, but the Domain / Mask sidecar could not be saved: {exception.Message}");
            appendLog("Error", $"Domain / Mask sidecar save failed: {exception.Message}");
        }
    }

    public void RestorePublishedArtifact()
    {
        var recipePath = getRecipePath();
        var document = createDocument();
        var step = document.Steps.FirstOrDefault(candidate =>
            string.Equals(candidate.ToolId, "domain-mask", StringComparison.Ordinal));
        if (step is null || string.IsNullOrWhiteSpace(recipePath) || step.InputEntityIds.Count != 2)
        {
            return;
        }

        var sidecarPath = GetArtifactSidecarPath(recipePath, step.OutputEntityId);
        var c3dPath = GetArtifactC3DPath(recipePath, step.OutputEntityId);
        if (!File.Exists(sidecarPath) || !File.Exists(c3dPath))
        {
            return;
        }

        try
        {
            var record = JsonSerializer.Deserialize<DomainMaskArtifactRecord>(File.ReadAllText(sidecarPath))
                ?? throw new InvalidDataException("Domain / Mask sidecar is empty.");
            if (!string.Equals(record.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(record.StepId, step.Id, StringComparison.OrdinalIgnoreCase))
            {
                SetSummary("Saved Domain / Mask output was not restored: sidecar and recipe identities do not match.");
                appendLog("Warning", "Domain / Mask output restore skipped: sidecar and recipe identities do not match.");
                return;
            }

            if (!TryResolveInputs(document, step.InputEntityIds, out var source, out var domain, out var reason))
            {
                var detail = reasonOrDefault(reason);
                SetSummary($"Saved Domain / Mask output was not restored: {detail}");
                appendLog("Warning", $"Domain / Mask output restore skipped: {detail}");
                return;
            }

            if (!string.Equals(record.SourceEntityId, source.EntityId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(record.DomainArtifactId, domain.ArtifactId, StringComparison.OrdinalIgnoreCase))
            {
                const string detail = "sidecar source or domain identity does not match the current Published inputs.";
                SetSummary($"Saved Domain / Mask output was not restored: {detail}");
                appendLog("Warning", $"Domain / Mask output restore skipped: {detail}");
                return;
            }

            if (record.Width != source.Width
                || record.Height != source.Height
                || !string.Equals(record.Unit, source.Unit, StringComparison.Ordinal)
                || !string.Equals(record.FrameId, source.FrameId, StringComparison.Ordinal))
            {
                const string detail = "sidecar grid, unit, or frame does not match the current Published HeightField.";
                SetSummary($"Saved Domain / Mask output was not restored: {detail}");
                appendLog("Warning", $"Domain / Mask output restore skipped: {detail}");
                return;
            }

            var loaded = C3DHeightFieldSnapshot.LoadVerified(
                c3dPath,
                source.EntityId,
                source.Unit,
                source.FrameId,
                record.ByteLength,
                record.OutputContentSha256,
                record.Width,
                record.Height);
            var restored = source.CreateDerived(
                step.OutputEntityId,
                loaded.Values.ToArray(),
                record.Provenance);
            if (!string.Equals(restored.ContentSha256, record.OutputContentSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Domain / Mask sidecar output hash could not be reproduced.");
            }

            preview = restored;
            previewPath = c3dPath;
            previewInputEntityId = source.EntityId;
            previewDomainArtifactId = domain.ArtifactId;
            isPreviewStale = false;
            isPreviewPublished = true;
            if (getSelectedPipelineStep() is { } selected
                && string.Equals(selected.Id, step.Id, StringComparison.OrdinalIgnoreCase))
            {
                selected.State = "Published";
            }

            SetSummary($"Restored Published Domain / Mask output {restored.EntityId} without executing the tool.");
            appendLog("Open", $"Domain / Mask output restored: {sidecarPath}.");
            onExecutionStateChanged();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or JsonException)
        {
            SetSummary($"Saved Domain / Mask output was not restored: {exception.Message}");
            appendLog("Warning", $"Domain / Mask output restore skipped: {exception.Message}");
        }
    }

    private bool TryResolveInputs(
        ToolRecipeDocument document,
        IReadOnlyList<string> inputEntityIds,
        out C3DHeightFieldSnapshot source,
        out C3DConnectedRegionArtifact domain,
        out string reason)
    {
        source = null!;
        domain = null!;
        reason = string.Empty;
        if (inputEntityIds.Count != 2)
        {
            reason = "Domain / Mask requires one HeightField followed by one Published ConnectedRegionArtifact.";
            return false;
        }

        domain = getPublishedDomain(inputEntityIds[1])!;
        if (domain is null)
        {
            reason = "The ConnectedRegionArtifact domain is not Published or is no longer current.";
            return false;
        }

        source = getPublishedHeightField(inputEntityIds[0])!;
        if (source is null
            && string.Equals(inputEntityIds[0], document.Source.Id, StringComparison.OrdinalIgnoreCase))
        {
            if (document.Source is not
                {
                    Path: { Length: > 0 } sourcePath,
                    ByteLength: { } byteLength,
                    ContentSha256: { Length: > 0 } contentSha256,
                    GridWidth: { } gridWidth,
                    GridHeight: { } gridHeight
                })
            {
                reason = "The recipe source identity is incomplete.";
                return false;
            }

            source = C3DHeightFieldSnapshot.LoadVerified(
                sourcePath,
                document.Source.Id,
                document.Source.Unit,
                document.Source.FrameId,
                byteLength,
                contentSha256,
                gridWidth,
                gridHeight);
        }

        if (source is null)
        {
            reason = "The first Domain / Mask HeightField input is not Published or restorable.";
            return false;
        }

        return true;
    }

    private void MarkStale(ToolWorkbenchPipelineStepItem step, string summary)
    {
        isPreviewStale = true;
        isPreviewPublished = false;
        step.State = "Preview stale";
        SetSummary(summary);
    }

    private void SetSummary(string value)
    {
        executionSummary = value;
        onExecutionStateChanged();
    }

    private static string reasonOrDefault(string reason) =>
        string.IsNullOrWhiteSpace(reason) ? "artifact identity or current input did not match." : reason;

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
        return Path.Combine(directory, $"domain-mask-{hash}.c3d");
    }

    private static string GetArtifactC3DPath(string recipePath, string outputEntityId) =>
        Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(recipePath)) ?? Environment.CurrentDirectory,
            $"{Path.GetFileNameWithoutExtension(recipePath)}.domain-mask.{Sanitize(outputEntityId)}.c3d");

    private static string GetArtifactSidecarPath(string recipePath, string outputEntityId) =>
        Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(recipePath)) ?? Environment.CurrentDirectory,
            $"{Path.GetFileNameWithoutExtension(recipePath)}.domain-mask.{Sanitize(outputEntityId)}.json");

    private static string Sanitize(string value) =>
        string.Concat(value.Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_'));

    private sealed record DomainMaskArtifactRecord(
        string StepId,
        string OutputEntityId,
        string SourceEntityId,
        string DomainArtifactId,
        string OutputContentSha256,
        int Width,
        int Height,
        string Unit,
        string FrameId,
        string Provenance,
        long ByteLength);
}
