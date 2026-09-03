using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Projects the current recipe and established execution-owner state into typed
/// artifact items. The projection is read-only and does not retain session state.
/// </summary>
internal sealed class ToolWorkbenchArtifactProjection
{
    public IReadOnlyList<ToolWorkbenchArtifactItem> Project(
        ToolWorkbenchArtifactProjectionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var artifacts = new List<ToolWorkbenchArtifactItem>
        {
            CreateSourceArtifact(snapshot)
        };

        foreach (var selection in snapshot.Selections)
        {
            var isCurrent = snapshot.IsSelectionCurrent(selection);
            artifacts.Add(new ToolWorkbenchArtifactItem(
                selection.Id,
                selection.Name,
                selection.Kind,
                isCurrent ? "Current selection" : "Stale",
                selection.RootSourceId,
                selection.RootSourceId,
                snapshot.Source.Unit,
                selection.FrameId,
                selection.SourceBinding.ContentSha256,
                isCurrent
                    ? "Recipe-owned teaching selection."
                    : "Recapture is required because the source binding changed.",
                null,
                "Selection"));
        }

        foreach (var step in snapshot.PipelineSteps)
        {
            artifacts.Add(CreateStepArtifact(snapshot, step));
        }

        return artifacts;
    }

    private static ToolWorkbenchArtifactItem CreateSourceArtifact(
        ToolWorkbenchArtifactProjectionSnapshot snapshot)
    {
        var source = snapshot.Source;
        var sourceReady = snapshot.IsSourceReadyForRecipe;
        return new ToolWorkbenchArtifactItem(
            source.Id,
            source.Name,
            "SourceC3D / RawHeightField",
            sourceReady ? "Ready" : string.IsNullOrWhiteSpace(source.Path) ? "Source required" : "Needs repair",
            source.Id,
            string.Empty,
            source.Unit,
            source.FrameId,
            snapshot.SourceBinding?.ContentSha256 ?? string.Empty,
            sourceReady
                ? $"{snapshot.SourceBinding!.GridWidth} × {snapshot.SourceBinding.GridHeight} verified C3D source."
                : snapshot.SourceReadinessSummary,
            null,
            "Source");
    }

    private static ToolWorkbenchArtifactItem CreateStepArtifact(
        ToolWorkbenchArtifactProjectionSnapshot snapshot,
        ToolWorkbenchPipelineStepItem step)
    {
        var source = snapshot.Source;
        if (!step.OutputEnabled)
        {
            return new ToolWorkbenchArtifactItem(
                step.OutputEntityId,
                step.ToolName,
                step.OutputContract,
                "Disabled",
                source.Id,
                string.Join("; ", step.InputEntityIds),
                source.Unit,
                source.FrameId,
                string.Empty,
                $"Declared by {step.Id}; output policy disabled it. No Preview, Run output, or evidence is fabricated.",
                step,
                "DisabledOutput");
        }

        if (string.Equals(step.ToolId, "roi-crop", StringComparison.Ordinal)
            && snapshot.RoiCrop.Output is { } cropOutput)
        {
            var qualityDelta = snapshot.CreateSourceQualityDelta(
                cropOutput,
                null,
                "not evaluated by ROI / Crop");
            return new ToolWorkbenchArtifactItem(
                cropOutput.EntityId,
                step.ToolName,
                "HeightField",
                snapshot.RoiCrop.IsStale
                    ? "Stale"
                    : snapshot.RoiCrop.IsPublished
                        ? "Published"
                        : "Preview",
                source.Id,
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
            && snapshot.LevelSurface.Output is { } leveledOutput
            && snapshot.LevelSurface.Transform is { } levelingTransform)
        {
            var qualityDelta = snapshot.CreateSourceQualityDelta(
                leveledOutput,
                null,
                "not evaluated by Level Surface");
            return new ToolWorkbenchArtifactItem(
                leveledOutput.EntityId,
                step.ToolName,
                "LeveledHeightField + LevelingTransform + LevelFrame",
                snapshot.LevelSurface.IsStale
                    ? "Stale"
                    : snapshot.LevelSurface.IsPublished
                        ? "Published"
                        : "Preview",
                source.Id,
                source.Id,
                leveledOutput.Unit,
                leveledOutput.FrameId,
                leveledOutput.ContentSha256,
                $"{leveledOutput.Width} x {leveledOutput.Height} | reference RMS {levelingTransform.ReferenceResidualRms:G6} | transform {levelingTransform.ContentSha256} | level frame {snapshot.LevelSurface.LevelFrame?.ContentSha256 ?? "(none)"} | frame chain {snapshot.LevelSurface.FrameChain?.ContentSha256 ?? "(none)"} | quality {snapshot.LevelSurface.QualityEvidence?.State.ToString() ?? "(none)"} {snapshot.LevelSurface.QualityEvidence?.ContentSha256 ?? ""} | source unchanged | {qualityDelta?.Summary ?? "quality delta unavailable"}",
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
            && snapshot.ConnectedRegion.Output is { } connectedRegionArtifact
            && string.Equals(
                connectedRegionArtifact.ArtifactId,
                step.OutputEntityId,
                StringComparison.OrdinalIgnoreCase))
        {
            return new ToolWorkbenchArtifactItem(
                connectedRegionArtifact.ArtifactId,
                step.ToolName,
                "ConnectedRegionArtifact",
                snapshot.ConnectedRegion.IsStale
                    ? "Stale"
                    : snapshot.ConnectedRegion.IsPublished
                        ? "Published"
                        : "Preview",
                source.Id,
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
            && snapshot.DomainMask.Output is { } domainMaskOutput
            && string.Equals(
                domainMaskOutput.EntityId,
                step.OutputEntityId,
                StringComparison.OrdinalIgnoreCase))
        {
            var qualityDelta = snapshot.CreateSourceQualityDelta(
                domainMaskOutput,
                null,
                "domain cells are the explicit Connected Region union");
            return new ToolWorkbenchArtifactItem(
                domainMaskOutput.EntityId,
                step.ToolName,
                "HeightField",
                snapshot.DomainMask.IsStale
                    ? "Stale"
                    : snapshot.DomainMask.IsPublished
                        ? "Published"
                        : "Preview",
                source.Id,
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
            && snapshot.EditableRegion.Output is { } editableRegionArtifact
            && string.Equals(
                editableRegionArtifact.ArtifactId,
                step.OutputEntityId,
                StringComparison.OrdinalIgnoreCase))
        {
            return new ToolWorkbenchArtifactItem(
                editableRegionArtifact.ArtifactId,
                step.ToolName,
                "EditableRegionArtifact",
                snapshot.EditableRegion.IsStale
                    ? "Stale"
                    : snapshot.EditableRegion.IsPublished
                        ? "Published"
                        : "Preview",
                source.Id,
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
            && snapshot.RemoveOutlier.Output is { } outlierOutput
            && snapshot.RemoveOutlier.Mask is { } outlierMask)
        {
            var qualityDelta = snapshot.CreateSourceQualityDelta(
                outlierOutput,
                outlierMask.OutlierCellCount,
                "detected by Remove Outlier Pixels mask");
            return new ToolWorkbenchArtifactItem(
                outlierOutput.EntityId,
                step.ToolName,
                "FilteredHeightField",
                snapshot.RemoveOutlier.IsStale
                    ? "Stale"
                    : snapshot.RemoveOutlier.IsPublished
                        ? "Published"
                        : "Preview",
                source.Id,
                source.Id,
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
            && snapshot.Filter.Output is { } filterPreviewOutput)
        {
            var qualityDelta = snapshot.CreateSourceQualityDelta(
                filterPreviewOutput,
                null,
                "not evaluated by Median Filter");
            return new ToolWorkbenchArtifactItem(
                filterPreviewOutput.EntityId,
                step.ToolName,
                "FilteredHeightField",
                snapshot.Filter.IsStale ? "Stale" : snapshot.Filter.IsPublished ? "Published" : "Preview",
                source.Id,
                source.Id,
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
            && snapshot.HeightDifferenceEdge.Output is { } edgePreviewOutput
            && string.Equals(edgePreviewOutput.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            return new ToolWorkbenchArtifactItem(
                edgePreviewOutput.OutputEntityId,
                step.ToolName,
                "EdgePointSet",
                snapshot.HeightDifferenceEdge.IsStale ? "Stale" : snapshot.HeightDifferenceEdge.IsPublished ? "Published" : "Preview",
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
            && snapshot.GetPublishedLineFitOutput(step.OutputEntityId) is { } publishedLine)
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
            && snapshot.GetPublishedTwoPointLineOutput(step.OutputEntityId) is { } publishedTwoPointLine)
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
            && snapshot.GetPublishedThreePointPlaneOutput(step.OutputEntityId) is { } publishedThreePointPlane)
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
            && snapshot.GetPublishedDatumPlaneDeviationOutput(step.OutputEntityId) is { } publishedDatumDeviation)
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
            && snapshot.GetPublishedLineIntersectionOutput(step.OutputEntityId) is { } publishedIntersection)
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
            && snapshot.LandmarkCorrespondence.Output is { } correspondenceOutput
            && string.Equals(
                correspondenceOutput.OutputEntityId,
                step.OutputEntityId,
                StringComparison.OrdinalIgnoreCase))
        {
            return new ToolWorkbenchArtifactItem(
                correspondenceOutput.OutputEntityId,
                step.ToolName,
                "CorrespondenceSet",
                snapshot.LandmarkCorrespondence.IsStale
                    ? "Stale"
                    : snapshot.LandmarkCorrespondence.IsPublished
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
            && snapshot.GetPublishedAffineSolveOutput(step.OutputEntityId) is { } publishedAffine)
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
            && snapshot.AffineApply.Output is { } affineApplyOutput
            && string.Equals(affineApplyOutput.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            return new ToolWorkbenchArtifactItem(
                affineApplyOutput.OutputEntityId,
                step.ToolName,
                "TransformedPointCloud",
                snapshot.AffineApply.IsStale ? "Stale" : snapshot.AffineApply.IsPublished ? "Published" : "Preview",
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
            && snapshot.RegridHeightField.Output is { } regridHeightFieldOutput
            && string.Equals(regridHeightFieldOutput.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            return new ToolWorkbenchArtifactItem(
                regridHeightFieldOutput.OutputEntityId,
                step.ToolName,
                "TransformedHeightField",
                snapshot.RegridHeightField.IsStale ? "Stale" : snapshot.RegridHeightField.IsPublished ? "Published" : "Preview",
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
            && snapshot.Measurement.Output is { } measurementOutput
            && string.Equals(measurementOutput.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            return new ToolWorkbenchArtifactItem(
                measurementOutput.OutputEntityId,
                step.ToolName,
                measurementOutput.CompletenessGrid is null
                    ? "MeasurementResult"
                    : "CompletenessGridMetrics",
                snapshot.Measurement.IsStale ? "Stale" : snapshot.Measurement.IsPublished ? "Published" : "Preview",
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
            source.Id,
            string.Join("; ", step.InputEntityIds),
            source.Unit,
            source.FrameId,
            string.Empty,
            $"Declared by {step.Id}. No Preview or Published output exists yet.",
            step,
            "DeclaredOutput");
    }
}

internal sealed record ToolWorkbenchArtifactPreview<T>(
    T? Output,
    bool IsStale,
    bool IsPublished)
    where T : class;

internal sealed record ToolWorkbenchLevelSurfaceArtifactPreview(
    C3DHeightFieldSnapshot? Output,
    C3DLevelingTransform? Transform,
    C3DLevelFrameArtifact? LevelFrame,
    C3DLevelSurfaceCoordinateFrameChain? FrameChain,
    C3DLevelFrameQualityEvidence? QualityEvidence,
    bool IsStale,
    bool IsPublished);

internal sealed record ToolWorkbenchRemoveOutlierArtifactPreview(
    C3DHeightFieldSnapshot? Output,
    C3DOutlierCellMap? Mask,
    bool IsStale,
    bool IsPublished);

internal sealed record ToolWorkbenchArtifactProjectionSnapshot(
    ToolWorkbenchSourceItem Source,
    bool IsSourceReadyForRecipe,
    ToolRecipeSelectionSourceBinding? SourceBinding,
    string SourceReadinessSummary,
    string SourceContextSummary,
    IReadOnlyList<ToolWorkbenchReferenceItem> References,
    IReadOnlyList<ToolRecipeSelection> Selections,
    Func<ToolRecipeSelection, bool> IsSelectionCurrent,
    IReadOnlyList<ToolWorkbenchPipelineStepItem> PipelineSteps,
    Func<C3DHeightFieldSnapshot, long?, string, SourceQualityDelta?> CreateSourceQualityDelta,
    ToolWorkbenchArtifactPreview<C3DHeightFieldSnapshot> RoiCrop,
    ToolWorkbenchLevelSurfaceArtifactPreview LevelSurface,
    ToolWorkbenchArtifactPreview<C3DConnectedRegionArtifact> ConnectedRegion,
    ToolWorkbenchArtifactPreview<C3DHeightFieldSnapshot> DomainMask,
    ToolWorkbenchArtifactPreview<C3DEditableRegionArtifact> EditableRegion,
    ToolWorkbenchRemoveOutlierArtifactPreview RemoveOutlier,
    ToolWorkbenchArtifactPreview<C3DHeightFieldSnapshot> Filter,
    ToolWorkbenchArtifactPreview<C3DHeightDifferenceEdgePointSet> HeightDifferenceEdge,
    Func<string, C3DLineFeature?> GetPublishedLineFitOutput,
    Func<string, C3DTwoPointLineFeature?> GetPublishedTwoPointLineOutput,
    Func<string, C3DThreePointPlaneFeature?> GetPublishedThreePointPlaneOutput,
    Func<string, C3DDatumPlaneDeviationFeature?> GetPublishedDatumPlaneDeviationOutput,
    Func<string, C3DLineIntersectionFeature?> GetPublishedLineIntersectionOutput,
    ToolWorkbenchArtifactPreview<C3DLandmarkCorrespondenceSet> LandmarkCorrespondence,
    Func<string, C3DAffineTransform3D?> GetPublishedAffineSolveOutput,
    ToolWorkbenchArtifactPreview<C3DTransformedPointCloud> AffineApply,
    ToolWorkbenchArtifactPreview<C3DTransformedHeightField> RegridHeightField,
    ToolWorkbenchArtifactPreview<ToolRecipeHeightMeasurementOutput> Measurement);
