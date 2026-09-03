using System.Collections.ObjectModel;
using OpenVisionLab.ThreeD.Core;

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
        artifactNavigatorOwner.PropertyChanged += (_, args) => OnPropertyChanged(args.PropertyName);
        artifactNavigatorOwner.Rebuilt += (_, _) =>
        {
            RebuildRenderableC3DConsumers();
            RebuildFlowPortDiagnostics();
            RebuildCompatibleToolCatalog();
        };
    }

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
        [
            new ToolWorkbenchRenderableC3DPreparationSnapshot(
                "filter",
                filterPreviewOutput,
                CurrentFilterPreviewPath,
                HasCurrentFilterPreview,
                IsFilterPreviewStale,
                IsFilterPreviewPublished),
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
            new ToolWorkbenchRenderableC3DPreparationSnapshot(
                "roi-crop",
                CurrentRoiCropPreviewOutput,
                CurrentRoiCropPreviewPath,
                HasCurrentRoiCropPreview,
                IsRoiCropPreviewStale,
                IsRoiCropPreviewPublished)
        ],
        validationSetDefinitionOwner.Samples
            .Select(sample => new ToolWorkbenchRenderableC3DValidationSampleSnapshot(
                sample.Order,
                sample.SourcePath,
                $"{Localization.ValidationSet} #{sample.Order} · {sample.FileName}",
                "ValidationSample / C3D",
                sample.StatusText,
                sample.Message))
            .ToArray());

    private ToolWorkbenchArtifactProjectionSnapshot CreateArtifactProjectionSnapshot() => new(
        Source,
        IsSourceReadyForRecipe,
        SourceSession.SourceBinding,
        SourceReadinessSummary,
        SourceContextSummary,
        References,
        Selections,
        IsSelectionCurrent,
        PipelineSteps,
        CreateSourceQualityDelta,
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
        new ToolWorkbenchArtifactPreview<OpenVisionLab.ThreeD.Data.C3DHeightFieldSnapshot>(
            CurrentDomainMaskPreviewOutput,
            IsDomainMaskPreviewStale,
            IsDomainMaskPreviewPublished),
        new ToolWorkbenchArtifactPreview<C3DEditableRegionArtifact>(
            CurrentEditableRegionArtifact,
            IsEditableRegionPreviewStale,
            IsEditableRegionPreviewPublished),
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
        new ToolWorkbenchArtifactPreview<OpenVisionLab.ThreeD.Tools.ToolRecipeHeightMeasurementOutput>(
            CurrentMeasurementOutput,
            IsMeasurementPreviewStale,
            IsMeasurementPreviewPublished));

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
