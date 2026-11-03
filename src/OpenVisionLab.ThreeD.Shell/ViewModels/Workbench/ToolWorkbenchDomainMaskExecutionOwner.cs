using System.IO;
using System.Text.Json;
using System.Threading;
using static OpenVisionLab.ThreeD.Shell.ViewModels.Workbench.ToolWorkbenchCancellationSourceLifetime;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the Workbench lifecycle for the D-07 Domain / Mask preparation step.
/// It keeps Preview, Publish, persistence, and stale-state policy here while
/// the typed mask operation remains in the Tools adapter and SDK.
/// </summary>
internal sealed class ToolWorkbenchDomainMaskExecutionOwner : IDisposable
{
    private const int ArtifactSchemaVersion = 2;
    private readonly Func<bool> isSelectedStepDomainMask;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep;
    private readonly Func<bool> isSourceReadyForRecipe;
    private readonly Func<bool> hasPendingStepParameterChanges;
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Func<string?> getRecipePath;
    private readonly Func<string, C3DHeightFieldSnapshot?> getPublishedHeightField;
    private readonly Func<string, object?> getPublishedDomain;
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
    private string? previewDetectionEntityId;
    private string? previewDomainArtifactId;
    private int? previewSelectedRegionIndex;
    private string? previewProducerStepId;
    private string? previewProducerContextSha256;
    private string executionSummary =
        "Select Domain / Mask after publishing a Connected Region or Grid Polygon Domain, then Preview.";
    private int disposalState;

    public ToolWorkbenchDomainMaskExecutionOwner(
        Func<bool> isSelectedStepDomainMask,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep,
        Func<bool> isSourceReadyForRecipe,
        Func<bool> hasPendingStepParameterChanges,
        Func<ToolRecipeDocument> createDocument,
        Func<string?> getRecipePath,
        Func<string, C3DHeightFieldSnapshot?> getPublishedHeightField,
        Func<string, object?> getPublishedDomain,
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

    public bool IsSelectedStepDomainMask => !IsDisposed && isSelectedStepDomainMask();
    public bool IsPreviewRunning => !IsDisposed && isPreviewRunning;
    public bool HasCurrentPreview => !IsDisposed && preview is not null && !isPreviewStale;
    public bool IsPreviewStale => !IsDisposed && isPreviewStale;
    public bool IsPreviewPublished => !IsDisposed && isPreviewPublished;
    public C3DHeightFieldSnapshot? CurrentOutput => IsDisposed ? null : preview;
    public string? CurrentOutputPath => IsDisposed ? null : previewPath;
    public string ExecutionSummary => IsDisposed
        ? "Domain / Mask execution owner has been disposed."
        : executionSummary;
    public string OutputSummary => IsDisposed || preview is not { } output
        ? "No Domain / Mask output."
        : $"{output.Width} × {output.Height} | valid {output.ValidCount:N0} | missing {output.MissingCount:N0} | {(isPreviewPublished ? "Published" : "Preview only")}";

    public bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        var cancellation = Interlocked.Exchange(
            ref previewCancellation,
            null);
        CancelAndDispose(cancellation);
        preview = null;
        previewPath = null;
        previewInputEntityId = null;
        previewDetectionEntityId = null;
        previewDomainArtifactId = null;
        previewSelectedRegionIndex = null;
        previewProducerStepId = null;
        previewProducerContextSha256 = null;
        isPreviewRunning = false;
        isPreviewStale = false;
        isPreviewPublished = false;
    }

    internal bool TryGetPublishedOutput(string outputEntityId, out C3DHeightFieldSnapshot? output)
    {
        output = !IsDisposed
            && isPreviewPublished
            && !isPreviewStale
            && preview is not null
            && string.Equals(preview.EntityId, outputEntityId, StringComparison.OrdinalIgnoreCase)
            ? preview
            : null;
        return output is not null;
    }

    public async Task<bool> PreviewAsync()
    {
        if (IsDisposed
            || !CanPreview()
            || getSelectedPipelineStep() is not { } step)
        {
            return false;
        }

        var cancellation = new CancellationTokenSource();
        var cancellationToken = cancellation.Token;
        var previousCancellation = Interlocked.Exchange(
            ref previewCancellation,
            cancellation);
        CancelAndDispose(previousCancellation);
        if (IsDisposed)
        {
            if (ReferenceEquals(
                Interlocked.CompareExchange(
                    ref previewCancellation,
                    null,
                    cancellation),
                cancellation))
            {
                cancellation.Dispose();
            }

            return false;
        }

        SetRunning(true);
        isPreviewStale = false;
        isPreviewPublished = false;
        step.State = "Preview running";
        SetSummary("Domain / Mask Preview is projecting the complete validated domain without changing inputs.");
        if (!IsCurrentPreview(cancellation))
        {
            return false;
        }

        appendLog("Preview", $"Domain / Mask Preview started: {step.Id}.");

        try
        {
            var document = createDocument();
            if (!TryFindProducer(document, step.Id, step.OutputEntityId, out var producer))
            {
                step.State = "Taught / needs correction";
                SetSummary("Domain / Mask producer is missing or ambiguous in the current recipe.");
                return false;
            }
            if (!TryResolveInputs(
                    document,
                    producer,
                    out var source,
                    out var detectionSource,
                    out var domain,
                    out var selectedRegionIndex,
                    out var reason))
            {
                if (!IsCurrentPreview(cancellation))
                {
                    return false;
                }

                step.State = "Taught / needs correction";
                SetSummary(reason);
                if (IsCurrentPreview(cancellation))
                {
                    appendLog("Warning", $"Domain / Mask Preview blocked: {reason}");
                }

                return false;
            }

            if (!IsCurrentPreview(cancellation)) return false;
            var producerContextSha256 = CreateProducerContextSha256(
                document, producer, source, detectionSource, domain, selectedRegionIndex);
            var evaluation = await Task.Run(
                () => detectionSource is not null
                    && domain is C3DConnectedRegionArtifact detectedRegion
                    && selectedRegionIndex is { } detectedIndex
                    ? ToolRecipeDomainMaskExecution.Execute(
                        document,
                        producer.Id,
                        source,
                        detectionSource,
                        detectedRegion,
                        detectedIndex,
                        cancellationToken)
                    : domain switch
                    {
                        C3DConnectedRegionArtifact connectedRegion => ToolRecipeDomainMaskExecution.Execute(
                            document,
                            producer.Id,
                            source,
                            connectedRegion,
                            cancellationToken),
                        C3DGridDomainArtifact gridDomain => ToolRecipeDomainMaskExecution.Execute(
                            document,
                            producer.Id,
                            source,
                            gridDomain,
                            cancellationToken),
                        _ => new C3DDomainMaskEvaluation(
                            new ToolResult(
                                C3DDomainMaskRule.ToolName,
                                ResultStatus.Error,
                                "Domain / Mask requires a supported ConnectedRegionArtifact or GridDomainArtifact.",
                                TimeSpan.Zero,
                                [],
                                []),
                            null)
                    },
                cancellationToken);

            if (!IsCurrentPreview(cancellation))
            {
                return false;
            }
            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                preview = null;
                previewPath = null;
                previewInputEntityId = null;
                previewDetectionEntityId = null;
                previewDomainArtifactId = null;
                previewSelectedRegionIndex = null;
                previewProducerStepId = null;
                previewProducerContextSha256 = null;
                step.State = "Error";
                SetSummary(evaluation.Result.Message);
                if (IsCurrentPreview(cancellation))
                {
                    appendLog("Error", $"Domain / Mask Preview failed: {evaluation.Result.Message}");
                }

                return false;
            }

            var contextReady = TryGetProducerContext(createDocument(), producer.Id, producer.OutputEntityId, out var currentContext, out _);
            if (!IsCurrentPreview(cancellation)) return false;
            if (!contextReady
                || !string.Equals(producerContextSha256, currentContext, StringComparison.OrdinalIgnoreCase))
            {
                preview = null;
                previewPath = null;
                previewProducerStepId = null;
                previewProducerContextSha256 = null;
                isPreviewStale = true;
                step.State = "Preview stale";
                SetSummary("Domain / Mask producer context changed during Preview. Preview again before Publish.");
                return false;
            }
            preview = evaluation.Output;
            previewInputEntityId = source.EntityId;
            previewDetectionEntityId = detectionSource?.EntityId;
            previewDomainArtifactId = DescribeDomain(domain).ArtifactId;
            previewSelectedRegionIndex = selectedRegionIndex;
            previewProducerStepId = producer.Id;
            previewProducerContextSha256 = producerContextSha256;
            previewPath = CreatePreviewPath(evaluation.Output.ContentSha256);
            SaveC3DAtomically(evaluation.Output, previewPath);
            if (!IsCurrentPreview(cancellation))
            {
                return false;
            }

            step.State = "Preview ready";
            var domainDescription = DescribeDomain(domain);
            var routeDescription = detectionSource is null
                ? $"source {source.EntityId}"
                : $"raw {source.EntityId} | detection {detectionSource.EntityId} | region {selectedRegionIndex}";
            SetSummary(
                $"Preview ready | domain {domainDescription.Kind} {domainDescription.Count:N0} | {routeDescription} | valid {evaluation.Output.ValidCount:N0} | missing {evaluation.Output.MissingCount:N0} | inputs unchanged");
            if (IsCurrentPreview(cancellation))
            {
                appendLog(
                    "Preview",
                    $"Domain / Mask Preview ready: output={evaluation.Output.ContentSha256}; {routeDescription}; domain={domainDescription.Hash}.");
            }

            if (!IsCurrentPreview(cancellation))
            {
                return false;
            }

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
            if (!IsCurrentPreview(cancellation))
            {
                return false;
            }

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
            var ownsCancellation = ReferenceEquals(
                Interlocked.CompareExchange(
                    ref previewCancellation,
                    null,
                    cancellation),
                cancellation);
            if (ownsCancellation)
            {
                cancellation.Dispose();
            }

            if (ownsCancellation && !IsDisposed)
            {
                SetRunning(false);
            }
        }
    }

    public bool CanPreview() =>
        !IsDisposed
        && isSelectedStepDomainMask()
        && isSourceReadyForRecipe()
        && !hasPendingStepParameterChanges()
        && !isPreviewRunning
        && getSelectedPipelineStep() is { } step
        && (step.InputEntityIds.Count is 2 or 3)
        && getPublishedDomain(step.InputEntityIds[^1]) is not null
        && (step.InputEntityIds.Count == 2
            || getPublishedHeightField(step.InputEntityIds[1]) is not null)
        && ToolRecipeValidator.ValidateForStepExecution(createDocument(), step.Id).IsValid;

    public void Publish()
    {
        MarkStaleIfUpstreamChanged();
        if (IsDisposed
            || isPreviewRunning
            || getSelectedPipelineStep() is not { } step
            || !HasCurrentPreview
            || !string.Equals(step.Id, previewProducerStepId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(step.OutputEntityId, preview!.EntityId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        isPreviewPublished = true;
        step.State = "Published";
        SetSummary(
            $"Published {step.OutputEntityId} | SHA-256 {preview!.ContentSha256} | source and typed domain remain unchanged");
        appendLog("Publish", $"Domain / Mask output published without re-running: {step.OutputEntityId}.");
        PersistPublishedArtifactIfPossible();
    }

    public void Cancel()
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            Volatile.Read(ref previewCancellation)?.Cancel();
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
            || isPreviewRunning)
        {
            return;
        }

        var selected = getSelectedPipelineStep();
        var isSelectedParameter = sender is ToolWorkbenchParameterItem parameter
            && string.Equals(selected?.Id, previewProducerStepId, StringComparison.OrdinalIgnoreCase)
            && (selected?.Parameters.Contains(parameter) ?? false);
        if (isSourceChangeEvent(sender)
            || sender is ToolWorkbenchPipelineStepItem changedStep
                && string.Equals(changedStep.Id, previewProducerStepId, StringComparison.OrdinalIgnoreCase)
            || isSelectedParameter)
        {
            MarkStale("Source, Domain / Mask routing, or upstream identity changed. Preview again before Publish.");
        }
    }

    public void MarkStaleIfUpstreamChanged()
    {
        if (IsDisposed
            || preview is null
            || isPreviewStale
            || isPreviewRunning)
        {
            return;
        }

        var document = createDocument();
        if (!TryGetProducerContext(document, previewProducerStepId, preview.EntityId, out var currentContext, out _)
            || string.IsNullOrWhiteSpace(previewProducerContextSha256)
            || !string.Equals(previewProducerContextSha256, currentContext, StringComparison.OrdinalIgnoreCase))
        {
            MarkStale("Raw source, detection source, selected region, or Domain / Mask artifact changed. Preview again before Publish.");
        }
    }

    public void Clear(string summary)
    {
        if (IsDisposed)
        {
            return;
        }

        var cancellation = Interlocked.Exchange(
            ref previewCancellation,
            null);
        CancelAndDispose(cancellation);
        preview = null;
        previewPath = null;
        previewInputEntityId = null;
        previewDetectionEntityId = null;
        previewDomainArtifactId = null;
        previewSelectedRegionIndex = null;
        previewProducerStepId = null;
        previewProducerContextSha256 = null;
        isPreviewStale = false;
        isPreviewPublished = false;
        SetSummary(summary);
    }

    public void RefreshState()
    {
        if (IsDisposed)
        {
            return;
        }

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
        if (IsDisposed)
        {
            return;
        }

        isPreviewRunning = value;
        onExecutionStateChanged();
    }

    public void PersistPublishedArtifactIfPossible()
    {
        if (IsDisposed
            || !isPreviewPublished
            || isPreviewStale
            || preview is null
            || string.IsNullOrWhiteSpace(getRecipePath()))
        {
            return;
        }

        MarkStaleIfUpstreamChanged();
        if (isPreviewStale
            || !isPreviewPublished
            || string.IsNullOrWhiteSpace(previewProducerContextSha256)
            || !TryFindProducer(createDocument(), previewProducerStepId, preview.EntityId, out var step)
            || string.IsNullOrWhiteSpace(previewInputEntityId)
            || string.IsNullOrWhiteSpace(previewDomainArtifactId))
        {
            return;
        }

        if (step.InputEntityIds.Count == 3
            && (string.IsNullOrWhiteSpace(previewDetectionEntityId)
                || previewSelectedRegionIndex is null))
        {
            return;
        }

        try
        {
            var recipePath = getRecipePath()!;
            var recipeDirectory = GetRecipeDirectory(recipePath);
            var c3dPath = GetArtifactC3DPath(recipeDirectory, recipePath, preview.EntityId);
            SaveC3DAtomically(preview, c3dPath);
            var sidecarPath = GetArtifactSidecarPath(recipeDirectory, recipePath, preview.EntityId);
            var sidecar = new DomainMaskArtifactRecord(
                step.Id,
                step.OutputEntityId,
                previewInputEntityId,
                previewDetectionEntityId,
                previewDomainArtifactId,
                previewSelectedRegionIndex,
                preview.ContentSha256,
                preview.Width,
                preview.Height,
                preview.Unit,
                preview.FrameId,
                preview.Provenance,
                new FileInfo(c3dPath).Length,
                ArtifactSchemaVersion,
                previewProducerContextSha256);
            WriteTextAtomically(
                sidecarPath,
                JsonSerializer.Serialize(sidecar, new JsonSerializerOptions { WriteIndented = true }));
            previewPath = c3dPath;
            if (IsDisposed)
            {
                return;
            }

            appendLog("Save", $"Domain / Mask output sidecar saved: {sidecarPath}.");
            onExecutionStateChanged();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            if (IsDisposed)
            {
                return;
            }

            SetSummary($"Published in this session, but the Domain / Mask sidecar could not be saved: {exception.Message}");
            appendLog("Error", $"Domain / Mask sidecar save failed: {exception.Message}");
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
            string.Equals(candidate.ToolId, "domain-mask", StringComparison.Ordinal));
        if (step is null || string.IsNullOrWhiteSpace(recipePath) || step.InputEntityIds.Count is not (2 or 3))
        {
            return;
        }

        var recipeDirectory = GetRecipeDirectory(recipePath);
        var sidecarPath = GetArtifactSidecarPath(recipeDirectory, recipePath, step.OutputEntityId);
        var c3dPath = GetArtifactC3DPath(recipeDirectory, recipePath, step.OutputEntityId);
        // A partial modern pair is a failed publication; never mix naming versions.
        if (!File.Exists(sidecarPath) && !File.Exists(c3dPath))
        {
            var legacyId = SanitizeLegacy(step.OutputEntityId);
            if (document.Steps.Count(candidate =>
                    string.Equals(candidate.ToolId, "domain-mask", StringComparison.Ordinal)
                    && string.Equals(SanitizeLegacy(candidate.OutputEntityId), legacyId, StringComparison.OrdinalIgnoreCase)) != 1)
            {
                appendLog("Warning", "Domain / Mask legacy artifact restore skipped: ambiguous output filename.");
                return;
            }
            sidecarPath = GetArtifactSidecarPath(recipeDirectory, recipePath, step.OutputEntityId, legacy: true);
            c3dPath = GetArtifactC3DPath(recipeDirectory, recipePath, step.OutputEntityId, legacy: true);
        }
        if (!File.Exists(sidecarPath) || !File.Exists(c3dPath))
        {
            return;
        }

        try
        {
            var record = JsonSerializer.Deserialize<DomainMaskArtifactRecord>(File.ReadAllText(sidecarPath))
                ?? throw new InvalidDataException("Domain / Mask sidecar is empty.");
            if (IsDisposed)
            {
                return;
            }

            if (record.SchemaVersion != ArtifactSchemaVersion
                || record.ProducerContextSha256 is not { Length: 64 } savedContext
                || !savedContext.All(Uri.IsHexDigit))
            {
                throw new InvalidDataException("Domain / Mask sidecar has no verifiable current producer context. Preview and Publish again.");
            }
            if (!string.Equals(record.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(record.StepId, step.Id, StringComparison.OrdinalIgnoreCase))
            {
                SetSummary("Saved Domain / Mask output was not restored: sidecar and recipe identities do not match.");
                appendLog("Warning", "Domain / Mask output restore skipped: sidecar and recipe identities do not match.");
                return;
            }

            if (!TryResolveInputs(
                    document,
                    step,
                    out var source,
                    out var detectionSource,
                    out var domain,
                    out var selectedRegionIndex,
                    out var reason))
            {
                var detail = reasonOrDefault(reason);
                SetSummary($"Saved Domain / Mask output was not restored: {detail}");
                appendLog("Warning", $"Domain / Mask output restore skipped: {detail}");
                return;
            }

            var currentContext = CreateProducerContextSha256(document, step, source, detectionSource, domain, selectedRegionIndex);
            if (!string.Equals(savedContext, currentContext, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Domain / Mask sidecar producer context does not match the current raw, detection, domain, or recipe inputs.");
            }
            if (!string.Equals(record.SourceEntityId, source.EntityId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(record.DomainArtifactId, DescribeDomain(domain).ArtifactId, StringComparison.OrdinalIgnoreCase))
            {
                const string detail = "sidecar source or domain identity does not match the current Published inputs.";
                SetSummary($"Saved Domain / Mask output was not restored: {detail}");
                appendLog("Warning", $"Domain / Mask output restore skipped: {detail}");
                return;
            }

            if (step.InputEntityIds.Count == 3
                && (!string.Equals(record.DetectionEntityId, detectionSource?.EntityId, StringComparison.OrdinalIgnoreCase)
                    || record.SelectedRegionIndex != selectedRegionIndex))
            {
                const string detail = "sidecar detection source or selected region does not match the current Published inputs.";
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

            if (IsDisposed)
            {
                return;
            }

            preview = restored;
            previewPath = c3dPath;
            previewInputEntityId = source.EntityId;
            previewDetectionEntityId = detectionSource?.EntityId;
            previewDomainArtifactId = DescribeDomain(domain).ArtifactId;
            previewSelectedRegionIndex = selectedRegionIndex;
            previewProducerStepId = step.Id;
            previewProducerContextSha256 = currentContext;
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
            if (IsDisposed)
            {
                return;
            }

            SetSummary($"Saved Domain / Mask output was not restored: {exception.Message}");
            appendLog("Warning", $"Domain / Mask output restore skipped: {exception.Message}");
        }
    }

    private static bool TryFindProducer(
        ToolRecipeDocument document,
        string? stepId,
        string outputEntityId,
        out ToolRecipeStep producer)
    {
        var matches = document.Steps.Where(step =>
            string.Equals(step.ToolId, "domain-mask", StringComparison.Ordinal)
            && string.Equals(step.Id, stepId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(step.OutputEntityId, outputEntityId, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        producer = matches.Length == 1 ? matches[0] : null!;
        return matches.Length == 1;
    }

    private bool TryGetProducerContext(
        ToolRecipeDocument document,
        string? stepId,
        string outputEntityId,
        out string? contextSha256,
        out string reason)
    {
        contextSha256 = null;
        reason = "Domain / Mask producer or its Published inputs are no longer current.";
        if (!isSourceReadyForRecipe()
            || !TryFindProducer(document, stepId, outputEntityId, out var producer)) return false;
        var validation = ToolRecipeValidator.ValidateForStepExecution(document, producer.Id);
        if (!validation.IsValid)
        {
            reason = string.Join(" ", validation.Errors);
            return false;
        }
        try
        {
            if (!TryResolveInputs(document, producer, out var source, out var detection, out var domain, out var index, out reason)) return false;
            contextSha256 = CreateProducerContextSha256(document, producer, source, detection, domain, index);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or JsonException or NotSupportedException)
        {
            reason = exception.Message;
            return false;
        }
    }

    private static string CreateProducerContextSha256(
        ToolRecipeDocument document,
        ToolRecipeStep producer,
        C3DHeightFieldSnapshot source,
        C3DHeightFieldSnapshot? detection,
        object domain,
        int? selectedRegionIndex)
    {
        // Exclude the raw file location and descriptive names. Keep the exact
        // authored dependency/policy payload and resolved data/domain identities.
        var portable = document with { Source = document.Source with { Path = string.Empty } };
        var description = DescribeDomain(domain);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Contract = "OpenVisionLab.DomainMask.ProducerContext.v1",
            Recipe = portable.CreateStepExecutionContext(producer.Id),
            Source = SnapshotContext(source),
            Detection = SnapshotContext(detection),
            Domain = new { description.Kind, ArtifactId = description.ArtifactId.ToUpperInvariant(), ContentSha256 = description.Hash.ToUpperInvariant(), description.Count },
            SelectedRegionIndex = selectedRegionIndex
        });
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
    }

    private static object? SnapshotContext(C3DHeightFieldSnapshot? source) => source is null ? null : new
    {
        EntityId = source.EntityId.ToUpperInvariant(),
        ContentSha256 = source.ContentSha256.ToUpperInvariant(),
        RootSourceSha256 = source.RootSourceSha256.ToUpperInvariant(),
        source.ByteLength, source.Width, source.Height, source.GridOriginColumn, source.GridOriginRow,
        source.Unit, source.FrameId, source.ScalarMeaning
    };

    private bool TryResolveInputs(
        ToolRecipeDocument document,
        ToolWorkbenchPipelineStepItem step,
        out C3DHeightFieldSnapshot source,
        out C3DHeightFieldSnapshot? detectionSource,
        out object domain,
        out int? selectedRegionIndex,
        out string reason) =>
        TryResolveInputs(
            document,
            step.InputEntityIds,
            step.Parameters
                .Select(parameter => new ToolRecipeParameter(parameter.Name, parameter.Value))
                .ToArray(),
            out source,
            out detectionSource,
            out domain,
            out selectedRegionIndex,
            out reason);

    private bool TryResolveInputs(
        ToolRecipeDocument document,
        ToolRecipeStep step,
        out C3DHeightFieldSnapshot source,
        out C3DHeightFieldSnapshot? detectionSource,
        out object domain,
        out int? selectedRegionIndex,
        out string reason) =>
        TryResolveInputs(
            document,
            step.InputEntityIds,
            step.Parameters,
            out source,
            out detectionSource,
            out domain,
            out selectedRegionIndex,
            out reason);

    private bool TryResolveInputs(
        ToolRecipeDocument document,
        IReadOnlyList<string> inputEntityIds,
        IReadOnlyList<ToolRecipeParameter>? parameters,
        out C3DHeightFieldSnapshot source,
        out C3DHeightFieldSnapshot? detectionSource,
        out object domain,
        out int? selectedRegionIndex,
        out string reason)
    {
        source = null!;
        detectionSource = null;
        domain = null!;
        selectedRegionIndex = null;
        reason = string.Empty;
        if (IsDisposed)
        {
            reason = "Domain / Mask owner is disposed.";
            return false;
        }

        if (inputEntityIds.Count is not (2 or 3))
        {
            reason = "Domain / Mask requires either the v1 HeightField + domain route or the v2 raw + filtered detection + Connected Region route.";
            return false;
        }

        domain = getPublishedDomain(inputEntityIds[^1])!;
        if (inputEntityIds.Count == 2
            && domain is not (C3DConnectedRegionArtifact or C3DGridDomainArtifact))
        {
            reason = "The ConnectedRegionArtifact or GridDomainArtifact domain is not Published or is no longer current.";
            return false;
        }

        if (inputEntityIds.Count == 3)
        {
            if (domain is not C3DConnectedRegionArtifact)
            {
                reason = "The v2 raw-region route requires a Published ConnectedRegionArtifact as its third input.";
                return false;
            }

            detectionSource = getPublishedHeightField(inputEntityIds[1]);
            if (detectionSource is null)
            {
                reason = "The v2 raw-region route requires the exact Published FilteredHeightField used for detection.";
                return false;
            }

            var selectedRegionParameter = parameters?.SingleOrDefault(parameter =>
                parameter is not null
                && string.Equals(parameter.Name, "SelectedRegionIndex", StringComparison.Ordinal));
            if (selectedRegionParameter is null
                || !int.TryParse(
                    selectedRegionParameter.Value,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var parsedRegionIndex)
                || parsedRegionIndex < 0)
            {
                reason = "The v2 raw-region route requires one non-negative SelectedRegionIndex parameter.";
                return false;
            }

            selectedRegionIndex = parsedRegionIndex;
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

    private void MarkStale(string summary)
    {
        if (IsDisposed)
        {
            return;
        }

        isPreviewStale = true;
        isPreviewPublished = false;
        if (getSelectedPipelineStep() is { } step
            && string.Equals(step.Id, previewProducerStepId, StringComparison.OrdinalIgnoreCase))
            step.State = "Preview stale";
        SetSummary(summary);
    }

    private void SetSummary(string value)
    {
        if (IsDisposed)
        {
            return;
        }

        executionSummary = value;
        onExecutionStateChanged();
    }

    private bool IsCurrentPreview(CancellationTokenSource cancellation) =>
        !IsDisposed && ReferenceEquals(
            Volatile.Read(ref previewCancellation),
            cancellation);

    private static string reasonOrDefault(string reason) =>
        string.IsNullOrWhiteSpace(reason) ? "artifact identity or current input did not match." : reason;

    private static (string Kind, int Count, string Hash, string ArtifactId) DescribeDomain(object domain) =>
        domain switch
        {
            C3DConnectedRegionArtifact connectedRegion =>
                ("Connected Region", connectedRegion.Regions.Count, connectedRegion.ContentSha256, connectedRegion.ArtifactId),
            C3DGridDomainArtifact gridDomain =>
                ("Grid Polygon", gridDomain.Cells.Count, gridDomain.ContentSha256, gridDomain.ArtifactId),
            _ => ("unsupported", 0, string.Empty, string.Empty)
        };

    private static void SaveC3DAtomically(C3DHeightFieldSnapshot output, string path)
        => WriteAtomically(path, output.SaveC3D);

    private static void WriteTextAtomically(string path, string contents)
        => WriteAtomically(path, temporaryPath => File.WriteAllText(temporaryPath, contents));

    private static string GetRecipeDirectory(string recipePath) =>
        Path.GetDirectoryName(Path.GetFullPath(recipePath)) ?? Environment.CurrentDirectory;

    private static void WriteAtomically(string path, Action<string> write)
    {
        var fullPath = Path.GetFullPath(path);
        var temporaryPath = $"{fullPath}.tmp.{Guid.NewGuid():N}";
        try
        {
            write(temporaryPath);
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

    private static string GetArtifactC3DPath(string recipeDirectory, string recipePath, string outputEntityId, bool legacy = false) =>
        Path.Combine(
            recipeDirectory,
            $"{Path.GetFileNameWithoutExtension(recipePath)}.domain-mask.{(legacy ? SanitizeLegacy(outputEntityId) : Sanitize(outputEntityId))}.c3d");

    private static string GetArtifactSidecarPath(string recipeDirectory, string recipePath, string outputEntityId, bool legacy = false) =>
        Path.Combine(
            recipeDirectory,
            $"{Path.GetFileNameWithoutExtension(recipePath)}.domain-mask.{(legacy ? SanitizeLegacy(outputEntityId) : Sanitize(outputEntityId))}.json");

    // Ordinal-ignore-case logical identity, encoded as exact UTF-16LE code units.
    // '~' cannot occur in a legacy sanitized ID, so new writes cannot alias old paths.
    private static string Sanitize(string value)
    {
        var canonical = value.ToUpperInvariant();
        var bytes = new byte[checked(canonical.Length * 2)];
        for (var index = 0; index < canonical.Length; index++)
        {
            bytes[index * 2] = (byte)(canonical[index] & 0xff);
            bytes[index * 2 + 1] = (byte)(canonical[index] >> 8);
        }
        return "v2~" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
    }

    private static string SanitizeLegacy(string value) =>
        string.Concat(value.Select(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_'));

    private sealed record DomainMaskArtifactRecord(
        string StepId,
        string OutputEntityId,
        string SourceEntityId,
        string? DetectionEntityId,
        string DomainArtifactId,
        int? SelectedRegionIndex,
        string OutputContentSha256,
        int Width,
        int Height,
        string Unit,
        string FrameId,
        string Provenance,
        long ByteLength,
        int SchemaVersion = 1,
        string? ProducerContextSha256 = null);
}
