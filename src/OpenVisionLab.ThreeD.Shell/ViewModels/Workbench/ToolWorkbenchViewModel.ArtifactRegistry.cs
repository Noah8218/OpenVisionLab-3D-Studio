using System.Collections.ObjectModel;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Builds the read-first artifact and recipe navigation presentation from the existing
/// recipe session. It never executes a tool or mutates a route.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    private readonly object artifactRegistryGate = new();

    private ToolWorkbenchNavigatorItem? selectedNavigatorItem;

    public ToolWorkbenchNavigatorItem? SelectedNavigatorItem
    {
        get => selectedNavigatorItem;
        private set
        {
            if (ReferenceEquals(selectedNavigatorItem, value))
            {
                return;
            }

            selectedNavigatorItem = value;
            OnPropertyChanged();
        }
    }

    public string SelectedRouteInputIds => SelectedPipelineStep is null
        ? string.Empty
        : string.Join("; ", SelectedPipelineStep.InputEntityIds);

    public string SelectedRouteOutputId => SelectedPipelineStep?.OutputEntityId ?? string.Empty;

    public bool IsSelectedToolLabAvailable => HasToolLab(SelectedPipelineStep?.ToolId);

    public string ArtifactRegistrySummary => ArtifactRegistry.Count == 0
        ? "No typed artifacts are registered."
        : $"{ArtifactRegistry.Count} typed entities | {ArtifactRegistry.Count(item => item.HasContentHash)} with current output identity";

    private void RebuildArtifactRegistryAndNavigator()
    {
        lock (artifactRegistryGate)
        {
            RebuildArtifactRegistryAndNavigatorCore();
        }
    }

    private void RebuildArtifactRegistryAndNavigatorCore()
    {
        var artifacts = new List<ToolWorkbenchArtifactItem>
        {
            CreateSourceArtifact()
        };

        foreach (var selection in Selections)
        {
            artifacts.Add(new ToolWorkbenchArtifactItem(
                selection.Id,
                selection.Name,
                selection.Kind,
                IsSelectionCurrent(selection) ? "Current selection" : "Stale",
                selection.RootSourceId,
                selection.RootSourceId,
                Source.Unit,
                selection.FrameId,
                selection.SourceBinding.ContentSha256,
                IsSelectionCurrent(selection)
                    ? "Recipe-owned teaching selection."
                    : "Recapture is required because the source binding changed.",
                null,
                "Selection"));
        }

        foreach (var step in PipelineSteps)
        {
            artifacts.Add(CreateStepArtifact(step));
        }

        ArtifactRegistry.ReplaceAll(artifacts);
        var navigatorRoots = new List<ToolWorkbenchNavigatorItem>();

        var sourceRoot = new ToolWorkbenchNavigatorItem(
            "SourceRoot",
            "Source & references",
            SourceContextSummary,
            null);
        sourceRoot.Children.Add(CreateArtifactNode(ArtifactRegistry[0], null, "Source"));
        foreach (var reference in References)
        {
            sourceRoot.Children.Add(new ToolWorkbenchNavigatorItem(
                "Reference",
                reference.Name,
                $"{reference.Id} | {reference.Kind}",
                null));
        }
        navigatorRoots.Add(sourceRoot);

        var pipelineRoot = new ToolWorkbenchNavigatorItem(
            "Pipeline",
            $"Recipe pipeline ({PipelineSteps.Count} steps)",
            "Ordered, read-first INPUT → OUTPUT teaching structure.",
            null);
        foreach (var step in PipelineSteps)
        {
            var stepNode = new ToolWorkbenchNavigatorItem(
                "Step",
                $"{step.Order}  {step.ToolName}",
                $"{step.State} | {step.Id}",
                step);

            foreach (var inputId in step.InputEntityIds)
            {
                var input = ArtifactRegistry.FirstOrDefault(item =>
                    string.Equals(item.Id, inputId, StringComparison.OrdinalIgnoreCase));
                stepNode.Children.Add(input is null
                    ? new ToolWorkbenchNavigatorItem(
                        "Input",
                        $"Input: {inputId}",
                        "Unresolved input entity ID.",
                        step)
                    : CreateArtifactNode(input, step, "Input"));
            }

            var output = ArtifactRegistry.First(item =>
                string.Equals(item.Id, step.OutputEntityId, StringComparison.OrdinalIgnoreCase));
            stepNode.Children.Add(CreateArtifactNode(output, step, "Output"));
            pipelineRoot.Children.Add(stepNode);
        }
        navigatorRoots.Add(pipelineRoot);

        if (Selections.Count > 0)
        {
            var selectionRoot = new ToolWorkbenchNavigatorItem(
                "Selections",
                $"Teaching selections ({Selections.Count})",
                "Recipe-owned source-bound captures.",
                null);
            foreach (var selection in ArtifactRegistry.Where(item => item.NodeKind == "Selection"))
            {
                selectionRoot.Children.Add(CreateArtifactNode(selection, null, "Selection"));
            }
            navigatorRoots.Add(selectionRoot);
        }

        NavigatorRoots.ReplaceAll(navigatorRoots);
        RefreshNavigatorSelection();
        RebuildOutputCompareCandidates();
        RebuildDisplayedOutputs();
        RebuildFlowPortDiagnostics();
        RebuildCompatibleToolCatalog();
        OnPropertyChanged(nameof(ArtifactRegistrySummary));
    }

    private ToolWorkbenchArtifactItem CreateSourceArtifact()
    {
        var sourceReady = IsSourceReadyForRecipe;
        return new ToolWorkbenchArtifactItem(
            Source.Id,
            Source.Name,
            "SourceC3D / RawHeightField",
            sourceReady ? "Ready" : string.IsNullOrWhiteSpace(Source.Path) ? "Source required" : "Needs repair",
            Source.Id,
            string.Empty,
            Source.Unit,
            Source.FrameId,
            SourceSession.SourceBinding?.ContentSha256 ?? string.Empty,
            sourceReady
                ? $"{SourceSession.SourceBinding!.GridWidth} × {SourceSession.SourceBinding.GridHeight} verified C3D source."
                : SourceReadinessSummary,
            null,
            "Source");
    }

    private ToolWorkbenchArtifactItem CreateStepArtifact(ToolWorkbenchPipelineStepItem step)
    {
        if (!step.OutputEnabled)
        {
            return new ToolWorkbenchArtifactItem(
                step.OutputEntityId,
                step.ToolName,
                step.OutputContract,
                "Disabled",
                Source.Id,
                string.Join("; ", step.InputEntityIds),
                Source.Unit,
                Source.FrameId,
                string.Empty,
                $"Declared by {step.Id}; output policy disabled it. No Preview, Run output, or evidence is fabricated.",
                step,
                "DisabledOutput");
        }

        if (string.Equals(step.ToolId, "roi-crop", StringComparison.Ordinal)
            && CurrentRoiCropPreviewOutput is { } cropOutput)
        {
            var qualityDelta = CreateSourceQualityDelta(
                cropOutput,
                null,
                "not evaluated by ROI / Crop");
            return new ToolWorkbenchArtifactItem(
                cropOutput.EntityId,
                step.ToolName,
                "HeightField",
                IsRoiCropPreviewStale
                    ? "Stale"
                    : IsRoiCropPreviewPublished
                        ? "Published"
                        : "Preview",
                Source.Id,
                string.Join("; ", step.InputEntityIds),
                cropOutput.Unit,
                cropOutput.FrameId,
                cropOutput.ContentSha256,
                $"{cropOutput.Width} x {cropOutput.Height} | source origin ({cropOutput.GridOriginColumn}, {cropOutput.GridOriginRow}) | valid {cropOutput.ValidCount:N0} | missing {cropOutput.MissingCount:N0} | source unchanged | {qualityDelta?.Summary ?? "quality delta unavailable"}",
                step,
                "HeightField")
            {
                PreparationQualityDelta = qualityDelta
            };
        }

        if (string.Equals(step.ToolId, "level-surface", StringComparison.Ordinal)
            && CurrentLevelSurfacePreviewOutput is { } leveledOutput
            && CurrentLevelSurfaceTransform is { } levelingTransform)
        {
            var qualityDelta = CreateSourceQualityDelta(
                leveledOutput,
                null,
                "not evaluated by Level Surface");
            return new ToolWorkbenchArtifactItem(
                leveledOutput.EntityId,
                step.ToolName,
                "LeveledHeightField + LevelingTransform + LevelFrame",
                IsLevelSurfacePreviewStale
                    ? "Stale"
                    : IsLevelSurfacePreviewPublished
                        ? "Published"
                        : "Preview",
                Source.Id,
                Source.Id,
                leveledOutput.Unit,
                leveledOutput.FrameId,
                leveledOutput.ContentSha256,
                $"{leveledOutput.Width} x {leveledOutput.Height} | reference RMS {levelingTransform.ReferenceResidualRms:G6} | transform {levelingTransform.ContentSha256} | level frame {CurrentLevelSurfaceLevelFrame?.ContentSha256 ?? "(none)"} | frame chain {CurrentLevelSurfaceFrameChain?.ContentSha256 ?? "(none)"} | quality {CurrentLevelSurfaceQualityEvidence?.State.ToString() ?? "(none)"} {CurrentLevelSurfaceQualityEvidence?.ContentSha256 ?? ""} | source unchanged | {qualityDelta?.Summary ?? "quality delta unavailable"}",
                step,
                "LeveledHeightField")
            {
                PreparationQualityDelta = qualityDelta
            };
        }

        if (string.Equals(
                step.ToolId,
                "connected-region",
                StringComparison.Ordinal)
            && CurrentConnectedRegionArtifact is { } connectedRegionArtifact
            && string.Equals(
                connectedRegionArtifact.ArtifactId,
                step.OutputEntityId,
                StringComparison.OrdinalIgnoreCase))
        {
            return new ToolWorkbenchArtifactItem(
                connectedRegionArtifact.ArtifactId,
                step.ToolName,
                "ConnectedRegionArtifact",
                IsConnectedRegionPreviewStale
                    ? "Stale"
                    : IsConnectedRegionPreviewPublished
                        ? "Published"
                        : "Preview",
                Source.Id,
                connectedRegionArtifact.SourceEntityId,
                connectedRegionArtifact.Unit,
                connectedRegionArtifact.FrameId,
                connectedRegionArtifact.ContentSha256,
                $"{connectedRegionArtifact.Regions.Count:N0} region(s) | mask {connectedRegionArtifact.MaskContentSha256} | filtered {connectedRegionArtifact.SourceContentSha256} | root {connectedRegionArtifact.RootSourceSha256}",
                step,
                "ConnectedRegionArtifact");
        }

        if (string.Equals(
                step.ToolId,
                "domain-mask",
                StringComparison.Ordinal)
            && CurrentDomainMaskPreviewOutput is { } domainMaskOutput
            && string.Equals(
                domainMaskOutput.EntityId,
                step.OutputEntityId,
                StringComparison.OrdinalIgnoreCase))
        {
            var qualityDelta = CreateSourceQualityDelta(
                domainMaskOutput,
                null,
                "domain cells are the explicit Connected Region union");
            return new ToolWorkbenchArtifactItem(
                domainMaskOutput.EntityId,
                step.ToolName,
                "HeightField",
                IsDomainMaskPreviewStale
                    ? "Stale"
                    : IsDomainMaskPreviewPublished
                        ? "Published"
                        : "Preview",
                Source.Id,
                string.Join("; ", step.InputEntityIds),
                domainMaskOutput.Unit,
                domainMaskOutput.FrameId,
                domainMaskOutput.ContentSha256,
                $"{domainMaskOutput.Width} × {domainMaskOutput.Height} | valid {domainMaskOutput.ValidCount:N0} | missing {domainMaskOutput.MissingCount:N0} | domain-reduced | source unchanged | {qualityDelta?.Summary ?? "quality delta unavailable"}",
                step,
                "HeightField")
            {
                PreparationQualityDelta = qualityDelta
            };
        }

        if (string.Equals(
                step.ToolId,
                "editable-region",
                StringComparison.Ordinal)
            && CurrentEditableRegionArtifact is { } editableRegionArtifact
            && string.Equals(
                editableRegionArtifact.ArtifactId,
                step.OutputEntityId,
                StringComparison.OrdinalIgnoreCase))
        {
            return new ToolWorkbenchArtifactItem(
                editableRegionArtifact.ArtifactId,
                step.ToolName,
                "EditableRegionArtifact",
                IsEditableRegionPreviewStale
                    ? "Stale"
                    : IsEditableRegionPreviewPublished
                        ? "Published"
                        : "Preview",
                Source.Id,
                editableRegionArtifact.SourceConnectedRegionArtifactId,
                editableRegionArtifact.Unit,
                editableRegionArtifact.FrameId,
                editableRegionArtifact.ContentSha256,
                $"region {editableRegionArtifact.RegionIndex} | {editableRegionArtifact.Cells.Count:N0} exact cell(s) | bounds {editableRegionArtifact.Bounding.Width} × {editableRegionArtifact.Bounding.Height} | connected {editableRegionArtifact.SourceConnectedRegionContentSha256}",
                step,
                "EditableRegionArtifact");
        }

        if (string.Equals(
                step.ToolId,
                "remove-outlier-pixels",
                StringComparison.Ordinal)
            && CurrentRemoveOutlierPreviewOutput is { } outlierOutput
            && CurrentRemoveOutlierMask is { } outlierMask)
        {
            var qualityDelta = CreateSourceQualityDelta(
                outlierOutput,
                outlierMask.OutlierCellCount,
                "detected by Remove Outlier Pixels mask");
            return new ToolWorkbenchArtifactItem(
                outlierOutput.EntityId,
                step.ToolName,
                "FilteredHeightField",
                IsRemoveOutlierPreviewStale
                    ? "Stale"
                    : IsRemoveOutlierPreviewPublished
                        ? "Published"
                        : "Preview",
                Source.Id,
                Source.Id,
                outlierOutput.Unit,
                outlierOutput.FrameId,
                outlierOutput.ContentSha256,
                $"{outlierOutput.Width} × {outlierOutput.Height} | removed {outlierMask.OutlierCellCount:N0} | outlier mask {outlierMask.Sha256} | source unchanged | {qualityDelta?.Summary ?? "quality delta unavailable"}",
                step,
                "FilteredHeightField")
            {
                PreparationQualityDelta = qualityDelta
            };
        }

        if (string.Equals(step.ToolId, "filter", StringComparison.Ordinal)
            && filterPreviewOutput is not null)
        {
            var qualityDelta = CreateSourceQualityDelta(
                filterPreviewOutput,
                null,
                "not evaluated by Median Filter");
            return new ToolWorkbenchArtifactItem(
                filterPreviewOutput.EntityId,
                step.ToolName,
                "FilteredHeightField",
                isFilterPreviewStale ? "Stale" : isFilterPreviewPublished ? "Published" : "Preview",
                Source.Id,
                Source.Id,
                filterPreviewOutput.Unit,
                filterPreviewOutput.FrameId,
                filterPreviewOutput.ContentSha256,
                $"{filterPreviewOutput.Width} × {filterPreviewOutput.Height} | {filterPreviewOutput.Provenance} | {qualityDelta?.Summary ?? "quality delta unavailable"}",
                step,
                "FilteredHeightField")
            {
                PreparationQualityDelta = qualityDelta
            };
        }

        if (string.Equals(step.ToolId, "height-difference-edge", StringComparison.Ordinal)
            && CurrentHeightDifferenceEdgeOutput is { } edgePreviewOutput
            && string.Equals(edgePreviewOutput.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            return new ToolWorkbenchArtifactItem(
                edgePreviewOutput.OutputEntityId,
                step.ToolName,
                "EdgePointSet",
                IsEdgePreviewStale ? "Stale" : IsEdgePreviewPublished ? "Published" : "Preview",
                edgePreviewOutput.RootSourceEntityId,
                edgePreviewOutput.InputEntityId,
                edgePreviewOutput.Unit,
                edgePreviewOutput.FrameId,
                edgePreviewOutput.ContentSha256,
                $"{edgePreviewOutput.Points.Count:N0} points | {edgePreviewOutput.Provenance}",
                step,
                "EdgePointSet");
        }

        if (string.Equals(step.ToolId, "three-d-line-fit", StringComparison.Ordinal)
            && TryGetPublishedLineFitOutput(step.OutputEntityId, out var publishedLine)
            && publishedLine is not null)
        {
            return new ToolWorkbenchArtifactItem(
                publishedLine.OutputEntityId,
                step.ToolName,
                "LineFeature",
                "Published",
                publishedLine.RootSourceEntityId,
                publishedLine.InputEdgePointSetEntityId,
                publishedLine.Unit,
                publishedLine.FrameId,
                publishedLine.ContentSha256,
                $"{publishedLine.Diagnostics.InlierCount:N0}/{publishedLine.Diagnostics.InputPointCount:N0} inliers | {publishedLine.Provenance}",
                step,
                "LineFeature");
        }

        if (string.Equals(step.ToolId, "two-point-line", StringComparison.Ordinal)
            && TryGetPublishedTwoPointLineOutput(step.OutputEntityId, out var publishedTwoPointLine)
            && publishedTwoPointLine is not null)
        {
            return new ToolWorkbenchArtifactItem(
                publishedTwoPointLine.OutputEntityId,
                step.ToolName,
                "LineFeature",
                "Published",
                publishedTwoPointLine.RootSourceEntityId,
                publishedTwoPointLine.InputSelectionId,
                publishedTwoPointLine.Unit,
                publishedTwoPointLine.FrameId,
                publishedTwoPointLine.ContentSha256,
                $"ordered picks ({publishedTwoPointLine.FirstRow}, {publishedTwoPointLine.FirstColumn}) -> ({publishedTwoPointLine.SecondRow}, {publishedTwoPointLine.SecondColumn}) | {publishedTwoPointLine.Provenance}",
                step,
                "LineFeature");
        }

        if (string.Equals(step.ToolId, "three-point-plane", StringComparison.Ordinal)
            && TryGetPublishedThreePointPlaneOutput(step.OutputEntityId, out var publishedThreePointPlane)
            && publishedThreePointPlane is not null)
        {
            return new ToolWorkbenchArtifactItem(
                publishedThreePointPlane.OutputEntityId,
                step.ToolName,
                "PlaneFeature",
                "Published",
                publishedThreePointPlane.RootSourceEntityId,
                publishedThreePointPlane.InputSelectionId,
                publishedThreePointPlane.Unit,
                publishedThreePointPlane.FrameId,
                publishedThreePointPlane.ContentSha256,
                $"ordered picks ({publishedThreePointPlane.FirstRow}, {publishedThreePointPlane.FirstColumn}) -> ({publishedThreePointPlane.SecondRow}, {publishedThreePointPlane.SecondColumn}) -> ({publishedThreePointPlane.ThirdRow}, {publishedThreePointPlane.ThirdColumn}) | {publishedThreePointPlane.Provenance}",
                step,
                "PlaneFeature");
        }

        if (string.Equals(step.ToolId, "datum-plane-raw-height-deviation", StringComparison.Ordinal)
            && TryGetPublishedDatumPlaneDeviationOutput(step.OutputEntityId, out var publishedDatumDeviation)
            && publishedDatumDeviation is not null)
        {
            return new ToolWorkbenchArtifactItem(
                publishedDatumDeviation.OutputEntityId,
                step.ToolName,
                "DatumPlaneDeviationResult",
                "Published",
                publishedDatumDeviation.RootSourceEntityId,
                $"{publishedDatumDeviation.PlaneFeatureEntityId}; {publishedDatumDeviation.MeasurementSelectionId}",
                publishedDatumDeviation.Unit,
                publishedDatumDeviation.FrameId,
                publishedDatumDeviation.ContentSha256,
                $"{publishedDatumDeviation.OutputRole} | P2V {publishedDatumDeviation.PeakToValleyRawHeight:G6} raw-height | {publishedDatumDeviation.ValidSampleCount:N0} samples | {publishedDatumDeviation.Provenance}",
                step,
                "DatumPlaneDeviationResult");
        }

        if (string.Equals(step.ToolId, "line-intersection", StringComparison.Ordinal)
            && TryGetPublishedLineIntersectionOutput(step.OutputEntityId, out var publishedIntersection)
            && publishedIntersection is not null)
        {
            return new ToolWorkbenchArtifactItem(
                publishedIntersection.OutputEntityId,
                step.ToolName,
                "CornerAnchor",
                "Published",
                publishedIntersection.RootSourceEntityId,
                $"{publishedIntersection.FirstLineEntityId}; {publishedIntersection.SecondLineEntityId}",
                publishedIntersection.Unit,
                publishedIntersection.FrameId,
                publishedIntersection.ContentSha256,
                $"{publishedIntersection.OutputRole} | gap {publishedIntersection.ClosestApproachDistance:G6} | acute {publishedIntersection.AcuteAngleDegrees:G6} degrees",
                step,
                "CornerAnchor");
        }

        if (string.Equals(step.ToolId, "landmark-correspondence", StringComparison.Ordinal)
            && CurrentLandmarkCorrespondenceOutput is { } correspondenceOutput
            && string.Equals(
                correspondenceOutput.OutputEntityId,
                step.OutputEntityId,
                StringComparison.OrdinalIgnoreCase))
        {
            return new ToolWorkbenchArtifactItem(
                correspondenceOutput.OutputEntityId,
                step.ToolName,
                "CorrespondenceSet",
                IsLandmarkCorrespondencePreviewStale
                    ? "Stale"
                    : IsLandmarkCorrespondencePreviewPublished
                        ? "Published"
                        : "Preview",
                correspondenceOutput.RootSourceEntityId,
                string.Join("; ", correspondenceOutput.Pairs.Select(pair => pair.SourceEntityId)),
                correspondenceOutput.SourceUnit,
                correspondenceOutput.SourceFrameId,
                correspondenceOutput.ContentSha256,
                $"{correspondenceOutput.Pairs.Count}/4 pairs | source rank {correspondenceOutput.SourceRank}/4 | reference rank {correspondenceOutput.ReferenceRank}/4 | correspondence evidence only",
                step,
                "CorrespondenceSet");
        }

        if (string.Equals(step.ToolId, "xyz-affine-solve", StringComparison.Ordinal)
            && TryGetPublishedAffineSolveOutput(step.OutputEntityId, out var publishedAffine)
            && publishedAffine is not null)
        {
            return new ToolWorkbenchArtifactItem(
                publishedAffine.OutputEntityId,
                step.ToolName,
                "AffineTransform3D",
                "Published",
                publishedAffine.RootSourceEntityId,
                publishedAffine.CorrespondenceEntityId,
                publishedAffine.ReferenceUnit,
                publishedAffine.ReferenceFrameId,
                publishedAffine.ContentSha256,
                $"condition {publishedAffine.ConditionEstimate:G6} | max residual {publishedAffine.ArithmeticMaximumResidual:G6} | matrix evidence only",
                step,
                "AffineTransform3D");
        }

        if (string.Equals(step.ToolId, "xyz-affine-apply", StringComparison.Ordinal)
            && CurrentAffineApplyOutput is { } affineApplyOutput
            && string.Equals(affineApplyOutput.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            return new ToolWorkbenchArtifactItem(
                affineApplyOutput.OutputEntityId,
                step.ToolName,
                "TransformedPointCloud",
                IsAffineApplyPreviewStale ? "Stale" : IsAffineApplyPreviewPublished ? "Published" : "Preview",
                affineApplyOutput.RootSourceEntityId,
                affineApplyOutput.AffineTransformEntityId,
                affineApplyOutput.ReferenceUnit,
                affineApplyOutput.ReferenceFrameId,
                affineApplyOutput.ContentSha256,
                $"{affineApplyOutput.FinitePointCount:N0} finite transformed points | {affineApplyOutput.MissingPointCount:N0} missing source cells | A3 re-grid excluded",
                step,
                "TransformedPointCloud");
        }

        if (string.Equals(step.ToolId, "re-grid-height-map", StringComparison.Ordinal)
            && CurrentRegridHeightFieldOutput is { } regridHeightFieldOutput
            && string.Equals(regridHeightFieldOutput.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            return new ToolWorkbenchArtifactItem(
                regridHeightFieldOutput.OutputEntityId,
                step.ToolName,
                "TransformedHeightField",
                IsRegridHeightFieldPreviewStale ? "Stale" : IsRegridHeightFieldPreviewPublished ? "Published" : "Preview",
                regridHeightFieldOutput.RootSourceEntityId,
                regridHeightFieldOutput.AffineTransformEntityId,
                regridHeightFieldOutput.ReferenceUnit,
                regridHeightFieldOutput.ReferenceFrameId,
                regridHeightFieldOutput.ContentSha256,
                $"{regridHeightFieldOutput.PopulatedCellCount:N0}/{regridHeightFieldOutput.Cells.Count:N0} populated | coverage {regridHeightFieldOutput.CoverageRatio:P2} | missing {regridHeightFieldOutput.MissingCellCount:N0} | collisions {regridHeightFieldOutput.CollisionCount:N0}",
                step,
                "TransformedHeightField");
        }

        if (step.ToolId is "thickness" or "warpage" or "plane-flatness" or "point-pair-dimensions" or "gap-flush" or "volume" or "cross-section-dimensions" or "completeness-grid"
            && CurrentMeasurementOutput is { } measurementOutput
            && string.Equals(measurementOutput.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            return new ToolWorkbenchArtifactItem(
                measurementOutput.OutputEntityId,
                step.ToolName,
                measurementOutput.CompletenessGrid is null
                    ? "MeasurementResult"
                    : "CompletenessGridMetrics",
                IsMeasurementPreviewStale ? "Stale" : IsMeasurementPreviewPublished ? "Published" : "Preview",
                measurementOutput.RootSourceEntityId,
                $"{measurementOutput.InputEntityId}; {measurementOutput.SelectionId}",
                measurementOutput.Unit,
                measurementOutput.FrameId,
                measurementOutput.ContentSha256,
                $"{measurementOutput.Result.Status} | {measurementOutput.EvidenceSummary}",
                step,
                measurementOutput.CompletenessGrid is null
                    ? "MeasurementResult"
                    : "CompletenessGridMetrics");
        }

        return new ToolWorkbenchArtifactItem(
            step.OutputEntityId,
            step.ToolName,
            step.OutputContract,
            "Declared",
            Source.Id,
            string.Join("; ", step.InputEntityIds),
            Source.Unit,
            Source.FrameId,
            string.Empty,
            $"Declared by {step.Id}. No Preview or Published output exists yet.",
            step,
            "DeclaredOutput");
    }

    private ToolWorkbenchNavigatorItem CreateArtifactNode(
        ToolWorkbenchArtifactItem artifact,
        ToolWorkbenchPipelineStepItem? pipelineStep,
        string role) => new(
            artifact.NodeKind,
            $"{role}: {artifact.DisplayName}",
            $"{artifact.Id} | {artifact.Contract} | {artifact.State}{artifact.HashShortSuffix}",
            pipelineStep ?? artifact.PipelineStep);

    private void SelectNavigatorItem(ToolWorkbenchNavigatorItem? item)
    {
        if (item is null)
        {
            return;
        }

        if (item.PipelineStep is not null
            && !ReferenceEquals(SelectedPipelineStep, item.PipelineStep))
        {
            SelectedPipelineStep = item.PipelineStep;
            if (!ReferenceEquals(SelectedPipelineStep, item.PipelineStep))
            {
                return;
            }
        }

        SelectedNavigatorItem = item;
        RefreshNavigatorSelection();
    }

    private void RequestSelectedToolLab()
    {
        if (SelectedPipelineStep is { } step && HasToolLab(step.ToolId))
        {
            ToolLabRequested?.Invoke(this, new ToolWorkbenchToolLabRequestEventArgs(step.ToolId));
        }
    }

    private static bool HasToolLab(string? toolId) => toolId is "filter"
        or "height-difference-edge"
        or "two-point-line"
        or "three-point-plane"
        or "datum-plane-raw-height-deviation"
        or "line-intersection"
        or "landmark-correspondence"
        or "xyz-affine-solve"
        or "xyz-affine-apply";

    private void RefreshNavigatorSelection()
    {
        if (SelectedPipelineStep is not null
            && (SelectedNavigatorItem is null
                || !ReferenceEquals(SelectedNavigatorItem.PipelineStep, SelectedPipelineStep)))
        {
            SelectedNavigatorItem = EnumerateNavigatorItems(NavigatorRoots)
                .FirstOrDefault(item => item.NodeKind == "Step" && ReferenceEquals(item.PipelineStep, SelectedPipelineStep));
        }

        foreach (var item in EnumerateNavigatorItems(NavigatorRoots))
        {
            item.IsCurrent = ReferenceEquals(item, SelectedNavigatorItem);
        }

        if (SelectedNavigatorItem is not null)
        {
            SelectedNavigatorItem.IsExpanded = true;
        }
    }

    private static IEnumerable<ToolWorkbenchNavigatorItem> EnumerateNavigatorItems(
        IEnumerable<ToolWorkbenchNavigatorItem> roots)
    {
        foreach (var root in roots)
        {
            yield return root;
            foreach (var child in EnumerateNavigatorItems(root.Children))
            {
                yield return child;
            }
        }
    }
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
