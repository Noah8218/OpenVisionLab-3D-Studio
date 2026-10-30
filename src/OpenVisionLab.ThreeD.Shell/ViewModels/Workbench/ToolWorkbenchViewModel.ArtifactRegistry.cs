using System.Collections.ObjectModel;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Compatibility facade for the independently owned artifact projection and
/// read-first navigator state.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    private readonly ToolWorkbenchArtifactProjection artifactProjection = new();
    private readonly ToolWorkbenchRenderableC3DCatalogOwner renderableC3DCatalogOwner = new();
    private ToolWorkbenchArtifactNavigatorOwner artifactNavigatorOwner = null!;

    public ToolWorkbenchNavigatorItem? SelectedNavigatorItem =>
        artifactNavigatorOwner.SelectedNavigatorItem;

    public string SelectedRouteInputIds => artifactNavigatorOwner.SelectedRouteInputIds;

    public string SelectedRouteOutputId => artifactNavigatorOwner.SelectedRouteOutputId;

    public bool IsSelectedToolLabAvailable => artifactNavigatorOwner.IsSelectedToolLabAvailable;

    public string ArtifactRegistrySummary => artifactNavigatorOwner.ArtifactRegistrySummary;

    internal IReadOnlyList<ToolWorkbenchRenderableC3DTarget> RenderableC3DCatalog =>
        renderableC3DCatalogOwner.Targets;

    internal ToolWorkbenchRenderableC3DTarget? GetRenderableC3DTarget(string? artifactId) =>
        renderableC3DCatalogOwner.GetTarget(artifactId);

    private void InitializeArtifactRegistryAndNavigator()
    {
        artifactNavigatorOwner = new ToolWorkbenchArtifactNavigatorOwner(
            artifactProjection,
            CreateArtifactProjectionSnapshot,
            () => SelectedPipelineStep,
            step => SelectedPipelineStep = step,
            toolId => ToolLabRequested?.Invoke(this, new ToolWorkbenchToolLabRequestEventArgs(toolId)));
    }

    private ToolWorkbenchArtifactNavigatorEventCoordinator CreateArtifactNavigatorEventCoordinator() =>
        new(
            artifactNavigatorOwner,
            args => OnPropertyChanged(args.PropertyName),
            () =>
            {
                RebuildRenderableC3DConsumers();
                RebuildFlowPortDiagnostics();
                RebuildCompatibleToolCatalog();
            });

    private void RebuildRenderableC3DConsumers()
    {
        RebuildRenderableC3DCatalog();
        RebuildOutputCompareCandidates();
        RebuildDisplayedOutputs();
        ReconcileViewerWorkspaceContents();
    }

    private void RebuildRenderableC3DCatalog()
    {
        renderableC3DCatalogOwner.Rebuild(CreateRenderableC3DCatalogSnapshot());
        OnPropertyChanged(nameof(RenderableC3DCatalog));
    }

    private ToolWorkbenchRenderableC3DCatalogSnapshot CreateRenderableC3DCatalogSnapshot() => new(
        new ToolWorkbenchRenderableC3DSourceSnapshot(
            Source.Id,
            Source.Name,
            Source.Format,
            Source.Unit,
            Source.FrameId,
            Source.Path,
            SourceSession.SourceBinding?.ContentSha256 ?? string.Empty,
            SourceReadinessSummary,
            IsSourceReadyForRecipe,
            SourceSession.SourceBinding),
        ArtifactRegistry.ToArray(),
        WithOrderedPreparationOutputs([
            .. PipelineSteps.Where(step => step.ToolId == "filter").Select(step =>
            {
                var published = filterExecutionOwner.GetPublishedOutput(step.OutputEntityId);
                var current = string.Equals(filterPreviewOutput?.EntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase);
                return new ToolWorkbenchRenderableC3DPreparationSnapshot("filter",
                    current ? filterPreviewOutput : published?.Output,
                    current ? CurrentFilterPreviewPath : published?.Path,
                    current ? HasCurrentFilterPreview : published is not null,
                    current && IsFilterPreviewStale,
                    current ? IsFilterPreviewPublished : published is not null);
            }),
            new ToolWorkbenchRenderableC3DPreparationSnapshot(
                "height-threshold-background-removal",
                CurrentHeightThresholdPreviewOutput,
                CurrentHeightThresholdPreviewPath,
                HasCurrentHeightThresholdPreview,
                IsHeightThresholdPreviewStale,
                IsHeightThresholdPreviewPublished),
            new ToolWorkbenchRenderableC3DPreparationSnapshot(
                "remove-outlier-pixels",
                CurrentRemoveOutlierPreviewOutput,
                CurrentRemoveOutlierPreviewPath,
                HasCurrentRemoveOutlierPreview,
                IsRemoveOutlierPreviewStale,
                IsRemoveOutlierPreviewPublished),
            new ToolWorkbenchRenderableC3DPreparationSnapshot(
                "domain-mask",
                CurrentDomainMaskPreviewOutput,
                CurrentDomainMaskPreviewPath,
                HasCurrentDomainMaskPreview,
                IsDomainMaskPreviewStale,
                IsDomainMaskPreviewPublished),
            new ToolWorkbenchRenderableC3DPreparationSnapshot(
                "level-surface",
                CurrentLevelSurfacePreviewOutput,
                CurrentLevelSurfacePreviewPath,
                HasCurrentLevelSurfacePreview,
                IsLevelSurfacePreviewStale,
                IsLevelSurfacePreviewPublished),
            .. PipelineSteps.Where(step => step.ToolId == ToolRecipeNinePointSurfaceCompensationExecution.ToolId).Select(step =>
                new ToolWorkbenchRenderableC3DPreparationSnapshot(step.ToolId,
                    ninePointExecutionOwner.GetOutput(step.OutputEntityId), ninePointExecutionOwner.GetPath(step.OutputEntityId),
                    ninePointExecutionOwner.GetOutput(step.OutputEntityId) is not null, false, ninePointExecutionOwner.IsPublished(step.OutputEntityId))),
            new ToolWorkbenchRenderableC3DPreparationSnapshot(
                "roi-crop",
                CurrentRoiCropPreviewOutput,
                CurrentRoiCropPreviewPath,
                HasCurrentRoiCropPreview,
                IsRoiCropPreviewStale,
                IsRoiCropPreviewPublished)
        ]),
        validationSetWorkspace.AllSamples
            .Select(sample => new ToolWorkbenchRenderableC3DValidationSampleSnapshot(
                sample.Order,
                sample.SourcePath,
                $"{Localization.ValidationSet} #{sample.Order} · {sample.FileName}",
                "ValidationSample / C3D",
                sample.StatusText,
                sample.Message))
            .ToArray());

    private IReadOnlyList<ToolWorkbenchRenderableC3DPreparationSnapshot> WithOrderedPreparationOutputs(
        IReadOnlyList<ToolWorkbenchRenderableC3DPreparationSnapshot> manual)
    {
        var current = orderedRunExecutionOwner.GetPublishedSteps().Where(step => step.RuntimeOutput is C3DHeightFieldSnapshot)
            .Select(step => new ToolWorkbenchRenderableC3DPreparationSnapshot(step.ToolId,
                (C3DHeightFieldSnapshot)step.RuntimeOutput!, orderedRunExecutionOwner.GetPublishedPath(step.OutputEntityId), true, false, true));
        return manual.Where(item => item.Output is null || !orderedRunExecutionOwner.OwnsOutput(item.Output.EntityId)).Concat(current).ToArray();
    }

    private ToolWorkbenchArtifactItem? ProjectOrderedArtifact(ToolWorkbenchPipelineStepItem step)
    {
        if (!orderedRunExecutionOwner.OwnsOutput(step.OutputEntityId)) return null;
        var executed = orderedRunExecutionOwner.GetPublishedStep(step.OutputEntityId);
        var output = executed?.RuntimeOutput;
        var (unit, frame) = output switch
        {
            C3DHeightFieldSnapshot map => (map.Unit, map.FrameId),
            C3DSignedHeightDeltaArtifact delta => (delta.Unit, delta.FrameId),
            C3DTransformedHeightField field => (field.ReferenceUnit, field.ReferenceFrameId),
            C3DTransformedPointCloud cloud => (cloud.ReferenceUnit, cloud.ReferenceFrameId),
            C3DAffineTransform3D transform => (transform.ReferenceUnit, transform.ReferenceFrameId),
            ToolRecipeHeightMeasurementOutput measurement => (measurement.Unit, measurement.FrameId),
            _ => (Source.Unit, Source.FrameId)
        };
        var artifact = new ToolWorkbenchArtifactItem(step.OutputEntityId, step.ToolName, step.OutputContract,
            output is null ? "Declared" : "Published", Source.Id, string.Join("; ", step.InputEntityIds), unit, frame,
            executed?.OutputContentSha256 ?? string.Empty, executed?.Evidence ?? string.Format(Localization.FlowPortDeclaredDetailFormat, step.OutputEntityId),
            step, output is null ? "DeclaredOutput" : step.OutputContract.Split('+')[0].Trim());
        return output is C3DHeightFieldSnapshot height ? artifact with
        {
            Minimum = height.ValidCount > 0 ? height.Minimum : null,
            Maximum = height.ValidCount > 0 ? height.Maximum : null,
            ValidSampleCount = height.ValidCount, MissingSampleCount = height.MissingCount,
            GridWidth = height.Width, GridHeight = height.Height
        } : artifact;
    }

    private ToolWorkbenchArtifactProjectionSnapshot CreateArtifactProjectionSnapshot() => new(
        Source,
        IsSourceReadyForRecipe,
        SourceSession.SourceBinding,
        SourceReadinessSummary,
        SourceContextSummary,
        Localization.SourceArtifactReadyDetailFormat,
        References,
        Selections,
        IsSelectionCurrent,
        PipelineSteps,
        CreateSourceQualityDelta,
        FormatSourceQualityDeltaSummary,
        Localization.OutputCompareQualityDeltaUnavailable,
        Localization.RoiCropQualityDeltaEvidence,
        Localization.LevelSurfaceQualityDeltaEvidence,
        Localization.DomainMaskQualityDeltaEvidence,
        Localization.FilterQualityDeltaEvidence,
        Localization.RemoveOutlierQualityDeltaEvidence,
        Localization.FilterQualityDeltaEvidence,
        Localization.FlowPortDeclaredDetailFormat,
        Localization.SelectedToolOutputDisabledDetailFormat,
        Localization.RecipeOwnedSelection,
        Localization.StaleSelectionRecaptureDetail,
        Localization.OutputCompareSourceIdentityRetained,
        Localization.DomainMaskReducedDetail,
        Localization.DomainMaskArtifactDetailFormat,
        Localization.ConnectedRegionArtifactDetailFormat,
        Localization.EditableRegionArtifactDetailFormat,
        Localization.RemoveOutlierArtifactDetailFormat,
        Localization.RoiCropArtifactDetailFormat,
        Localization.LevelSurfaceArtifactDetailFormat,
        Localization.FilterArtifactDetailFormat,
        Localization.HeightThresholdArtifactDetailFormat,
        Localization.HeightDifferenceEdgeArtifactDetailFormat,
        Localization.LineFitArtifactDetailFormat,
        Localization.TwoPointLineArtifactDetailFormat,
        Localization.ThreePointPlaneArtifactDetailFormat,
        Localization.DatumPlaneDeviationArtifactDetailFormat,
        Localization.LineIntersectionArtifactDetailFormat,
        Localization.LandmarkCorrespondenceArtifactDetailFormat,
        Localization.XyzAffineSolveArtifactDetailFormat,
        Localization.XyzAffineApplyArtifactDetailFormat,
        Localization.RegridHeightFieldArtifactDetailFormat,
        Localization.MeasurementArtifactDetailFormat,
        CurrentMeasurementOutput is { } statusOutput ? MeasurementResultStatusLabel(statusOutput.Result) : string.Empty,
        CurrentMeasurementOutput is { } messageOutput ? MeasurementResultMessage(messageOutput.Result) : string.Empty,
        Localization.ResultStatusLabel,
        new ToolWorkbenchArtifactPreview<OpenVisionLab.ThreeD.Data.C3DHeightFieldSnapshot>(
            CurrentRoiCropPreviewOutput,
            IsRoiCropPreviewStale,
            IsRoiCropPreviewPublished),
        new ToolWorkbenchLevelSurfaceArtifactPreview(
            CurrentLevelSurfacePreviewOutput,
            CurrentLevelSurfaceTransform,
            CurrentLevelSurfaceLevelFrame,
            CurrentLevelSurfaceFrameChain,
            CurrentLevelSurfaceQualityEvidence,
            IsLevelSurfacePreviewStale,
            IsLevelSurfacePreviewPublished),
        new ToolWorkbenchArtifactPreview<C3DConnectedRegionArtifact>(
            CurrentConnectedRegionArtifact,
            IsConnectedRegionPreviewStale,
            IsConnectedRegionPreviewPublished),
        new ToolWorkbenchArtifactPreview<C3DGridDomainArtifact>(
            CurrentGridPolygonDomainArtifact,
            IsGridPolygonDomainPreviewStale,
            IsGridPolygonDomainPreviewPublished),
        new ToolWorkbenchArtifactPreview<OpenVisionLab.ThreeD.Data.C3DHeightFieldSnapshot>(
            CurrentDomainMaskPreviewOutput,
            IsDomainMaskPreviewStale,
            IsDomainMaskPreviewPublished),
        new ToolWorkbenchArtifactPreview<C3DEditableRegionArtifact>(
            CurrentEditableRegionArtifact,
            IsEditableRegionPreviewStale,
            IsEditableRegionPreviewPublished),
        new ToolWorkbenchHeightThresholdArtifactPreview(
            CurrentHeightThresholdPreviewOutput,
            CurrentHeightThresholdPreviewEvidence,
            IsHeightThresholdPreviewStale,
            IsHeightThresholdPreviewPublished),
        new ToolWorkbenchRemoveOutlierArtifactPreview(
            CurrentRemoveOutlierPreviewOutput,
            CurrentRemoveOutlierMask,
            IsRemoveOutlierPreviewStale,
            IsRemoveOutlierPreviewPublished),
        new ToolWorkbenchArtifactPreview<OpenVisionLab.ThreeD.Data.C3DHeightFieldSnapshot>(
            filterPreviewOutput,
            isFilterPreviewStale,
            isFilterPreviewPublished),
        new ToolWorkbenchArtifactPreview<C3DHeightDifferenceEdgePointSet>(
            CurrentHeightDifferenceEdgeOutput,
            IsEdgePreviewStale,
            IsEdgePreviewPublished),
        outputEntityId => TryGetPublishedLineFitOutput(outputEntityId, out var output) ? output : null,
        outputEntityId => TryGetPublishedTwoPointLineOutput(outputEntityId, out var output) ? output : null,
        outputEntityId => TryGetPublishedThreePointPlaneOutput(outputEntityId, out var output) ? output : null,
        outputEntityId => TryGetPublishedDatumPlaneDeviationOutput(outputEntityId, out var output) ? output : null,
        outputEntityId => TryGetPublishedLineIntersectionOutput(outputEntityId, out var output) ? output : null,
        new ToolWorkbenchArtifactPreview<C3DLandmarkCorrespondenceSet>(
            CurrentLandmarkCorrespondenceOutput,
            IsLandmarkCorrespondencePreviewStale,
            IsLandmarkCorrespondencePreviewPublished),
        outputEntityId => TryGetPublishedAffineSolveOutput(outputEntityId, out var output) ? output : null,
        new ToolWorkbenchArtifactPreview<C3DTransformedPointCloud>(
            CurrentAffineApplyOutput,
            IsAffineApplyPreviewStale,
            IsAffineApplyPreviewPublished),
        new ToolWorkbenchArtifactPreview<C3DTransformedHeightField>(
            CurrentRegridHeightFieldOutput,
            IsRegridHeightFieldPreviewStale,
            IsRegridHeightFieldPreviewPublished),
        new ToolWorkbenchArtifactPreview<C3DPhysicalEdgeGapFlushResult>(
            CurrentPhysicalEdgeGapFlushOutput,
            IsPhysicalEdgeGapFlushPreviewStale,
            IsPhysicalEdgeGapFlushPreviewPublished),
        new ToolWorkbenchArtifactPreview<OpenVisionLab.ThreeD.Tools.ToolRecipeHeightMeasurementOutput>(
            CurrentMeasurementOutput,
            IsMeasurementPreviewStale,
            IsMeasurementPreviewPublished),
        new ToolWorkbenchArtifactPreview<C3DSignedHeightDeltaArtifact>(
            CurrentSignedDeltaPreviewOutput,
            IsSignedDeltaPreviewStale,
            IsSignedDeltaPreviewPublished),
        outputEntityId => filterExecutionOwner.GetPublishedOutput(outputEntityId)?.Output,
        outputEntityId => TryGetPublishedHeightDifferenceEdgeOutput(outputEntityId, out var output) ? output : null,
        SourceQuality.Report,
        ninePointExecutionOwner.GetOutput,
        ninePointExecutionOwner.IsPublished,
        ProjectOrderedArtifact);

    private void RebuildArtifactRegistryAndNavigator() => artifactNavigatorOwner.Rebuild();

    private void RefreshNavigatorSelection() => artifactNavigatorOwner.RefreshNavigatorSelection();
}

public sealed record ToolWorkbenchArtifactItem(
    string Id,
    string DisplayName,
    string Contract,
    string State,
    string RootSourceId,
    string InputEntityIds,
    string Unit,
    string FrameId,
    string ContentSha256,
    string Detail,
    ToolWorkbenchPipelineStepItem? PipelineStep,
    string NodeKind)
{
    public SourceQualityDelta? PreparationQualityDelta { get; init; }
    public double? Minimum { get; init; }
    public double? Maximum { get; init; }
    public long? ValidSampleCount { get; init; }
    public long? MissingSampleCount { get; init; }
    public int? GridWidth { get; init; }
    public int? GridHeight { get; init; }
    public bool HasContentHash => ContentSha256.Length == 64;
    public string HashShortSuffix => HasContentHash ? $" | SHA {ContentSha256[..12]}" : string.Empty;
}

public sealed class ToolWorkbenchNavigatorItem : System.ComponentModel.INotifyPropertyChanged
{
    private bool isCurrent;
    private bool isExpanded;

    public ToolWorkbenchNavigatorItem(
        string nodeKind,
        string title,
        string detail,
        ToolWorkbenchPipelineStepItem? pipelineStep)
    {
        NodeKind = nodeKind;
        Title = title;
        Detail = detail;
        PipelineStep = pipelineStep;
        isExpanded = nodeKind == "Pipeline"
            || nodeKind == "Step" && pipelineStep?.Order == "01";
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public string NodeKind { get; }
    public string Title { get; }
    public string Detail { get; }
    public ToolWorkbenchPipelineStepItem? PipelineStep { get; }
    public ObservableCollection<ToolWorkbenchNavigatorItem> Children { get; } = [];
    public string AccessibleName => $"{Title}. {Detail}";
    public bool IsCurrent
    {
        get => isCurrent;
        internal set
        {
            if (isCurrent == value)
            {
                return;
            }

            isCurrent = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsCurrent)));
        }
    }

    public bool IsExpanded
    {
        get => isExpanded;
        set
        {
            if (isExpanded == value)
            {
                return;
            }

            isExpanded = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsExpanded)));
        }
    }
}

public sealed class ToolWorkbenchToolLabRequestEventArgs(string toolId) : EventArgs
{
    public string ToolId { get; } = toolId;
}
