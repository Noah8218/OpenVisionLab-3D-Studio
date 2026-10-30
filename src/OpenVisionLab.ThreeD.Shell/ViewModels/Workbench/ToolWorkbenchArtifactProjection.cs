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
            WithHeightMetadata(snapshot, CreateSourceArtifact(snapshot))
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
                    ? snapshot.CurrentSelectionDetail
                    : snapshot.StaleSelectionDetail,
                null,
                "Selection"));
        }

        foreach (var step in snapshot.PipelineSteps)
        {
            artifacts.Add(WithHeightMetadata(snapshot, CreateStepArtifact(snapshot, step)));
        }

        return artifacts;
    }

    // Read cached SDK statistics only when their full identity matches the current artifact.
    // Declared, disabled and stale outputs must never retain an earlier map's values.
    private static ToolWorkbenchArtifactItem WithHeightMetadata(
        ToolWorkbenchArtifactProjectionSnapshot snapshot, ToolWorkbenchArtifactItem artifact)
    {
        if (artifact.State is not ("Ready" or "Preview" or "Published")) return artifact;
        if (artifact.NodeKind == "Source" && snapshot.SourceQuality is { } quality
            && quality.Source.EntityId == artifact.Id
            && string.Equals(quality.Source.ContentSha256, artifact.ContentSha256, StringComparison.OrdinalIgnoreCase)
            && quality.Coordinates.Unit == artifact.Unit && quality.Coordinates.FrameId == artifact.FrameId)
        {
            return artifact with
            {
                Minimum = quality.Height.Minimum,
                Maximum = quality.Height.Maximum,
                ValidSampleCount = quality.Coverage.ValidSampleCount,
                MissingSampleCount = quality.Coverage.MissingSampleCount,
                GridWidth = quality.Grid.Width,
                GridHeight = quality.Grid.Height
            };
        }

        var output = artifact.PipelineStep?.ToolId switch
        {
            "nine-point-surface-compensation" => snapshot.GetNinePointOutput?.Invoke(artifact.Id),
            "roi-crop" => snapshot.RoiCrop.Output,
            "level-surface" => snapshot.LevelSurface.Output,
            "domain-mask" => snapshot.DomainMask.Output,
            "height-threshold-background-removal" => snapshot.HeightThreshold.Output,
            "remove-outlier-pixels" => snapshot.RemoveOutlier.Output,
            "filter" => snapshot.GetPublishedFilterOutput?.Invoke(artifact.Id) ?? snapshot.Filter.Output,
            _ => null
        };
        if (output is not null && output.EntityId == artifact.Id
            && string.Equals(output.ContentSha256, artifact.ContentSha256, StringComparison.OrdinalIgnoreCase)
            && output.Unit == artifact.Unit && output.FrameId == artifact.FrameId)
        {
            return artifact with
            {
                Minimum = output.ValidCount > 0 ? output.Minimum : null,
                Maximum = output.ValidCount > 0 ? output.Maximum : null,
                ValidSampleCount = output.ValidCount,
                MissingSampleCount = output.MissingCount,
                GridWidth = output.Width,
                GridHeight = output.Height
            };
        }

        if (snapshot.SignedDeltaBackgroundSubtraction?.Output is { } delta
            && delta.OutputEntityId == artifact.Id
            && string.Equals(delta.OutputContentSha256, artifact.ContentSha256, StringComparison.OrdinalIgnoreCase)
            && delta.Unit == artifact.Unit && delta.FrameId == artifact.FrameId)
        {
            return artifact with
            {
                ValidSampleCount = delta.OutputValidSampleCount,
                MissingSampleCount = delta.OutputMissingSampleCount,
                GridWidth = delta.Width,
                GridHeight = delta.Height
            };
        }
        return artifact;
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
                ? string.Format(
                    snapshot.SourceArtifactReadyDetailFormat,
                    snapshot.SourceBinding!.GridWidth,
                    snapshot.SourceBinding.GridHeight)
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
                string.Format(snapshot.OutputDisabledDetailFormat, step.Id),
                step,
                "DisabledOutput");
        }

        if (snapshot.ProjectOrderedArtifact?.Invoke(step) is { } ordered) return ordered;

        if (string.Equals(step.ToolId, "roi-crop", StringComparison.Ordinal)
            && snapshot.RoiCrop.Output is { } cropOutput)
        {
            var qualityDelta = snapshot.CreateSourceQualityDelta(
                cropOutput,
                null,
                snapshot.RoiCropQualityDeltaEvidence);
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
                string.Format(
                    snapshot.RoiCropArtifactDetailFormat,
                    cropOutput.Width,
                    cropOutput.Height,
                    cropOutput.GridOriginColumn,
                    cropOutput.GridOriginRow,
                    cropOutput.ValidCount,
                    cropOutput.MissingCount,
                    snapshot.SourceIdentityRetainedDetail,
                    qualityDelta is { } delta
                        ? snapshot.FormatSourceQualityDeltaSummary(delta)
                        : snapshot.QualityDeltaUnavailable),
                step,
                "HeightField")
            {
                PreparationQualityDelta = qualityDelta
            };
        }

        if (step.ToolId == "nine-point-surface-compensation" && snapshot.GetNinePointOutput?.Invoke(step.OutputEntityId) is { } corrected)
            return new ToolWorkbenchArtifactItem(corrected.EntityId, step.ToolName, "HeightField",
                snapshot.IsNinePointPublished?.Invoke(step.OutputEntityId) == true ? "Published" : "Preview",
                source.Id, source.Id, corrected.Unit, corrected.FrameId, corrected.ContentSha256,
                $"{corrected.Width} × {corrected.Height} · {corrected.Unit}", step, "HeightField");

        if (string.Equals(step.ToolId, "level-surface", StringComparison.Ordinal)
            && snapshot.LevelSurface.Output is { } leveledOutput
            && snapshot.LevelSurface.Transform is { } levelingTransform)
        {
            var qualityDelta = snapshot.CreateSourceQualityDelta(
                leveledOutput,
                null,
                snapshot.LevelSurfaceQualityDeltaEvidence);
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
                string.Format(
                    snapshot.LevelSurfaceArtifactDetailFormat,
                    leveledOutput.Width,
                    leveledOutput.Height,
                    levelingTransform.ReferenceResidualRms,
                    levelingTransform.ContentSha256,
                    snapshot.LevelSurface.LevelFrame?.ContentSha256 ?? "(none)",
                    snapshot.LevelSurface.FrameChain?.ContentSha256 ?? "(none)",
                    snapshot.LevelSurface.QualityEvidence?.State.ToString() ?? "(none)",
                    snapshot.LevelSurface.QualityEvidence?.ContentSha256 ?? string.Empty,
                    snapshot.SourceIdentityRetainedDetail,
                    qualityDelta is { } delta
                        ? snapshot.FormatSourceQualityDeltaSummary(delta)
                        : snapshot.QualityDeltaUnavailable),
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
                string.Format(
                    snapshot.ConnectedRegionArtifactDetailFormat,
                    connectedRegionArtifact.Regions.Count,
                    connectedRegionArtifact.MaskContentSha256,
                    connectedRegionArtifact.SourceContentSha256,
                    connectedRegionArtifact.RootSourceSha256),
                step,
                "ConnectedRegionArtifact");
        }

        if (string.Equals(step.ToolId, C3DSignedHeightDeltaRecipePolicy.ToolId, StringComparison.Ordinal)
            && snapshot.SignedDeltaBackgroundSubtraction is { Output: { } signedDelta } signedDeltaPreview
            && string.Equals(signedDelta.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            return new ToolWorkbenchArtifactItem(
                signedDelta.OutputEntityId,
                step.ToolName,
                "SignedDeltaHeightField",
                signedDeltaPreview.IsStale
                    ? "Stale"
                    : signedDeltaPreview.IsPublished
                        ? "Published"
                        : "Preview",
                source.Id,
                string.Join("; ", step.InputEntityIds) + $"; {signedDelta.BackgroundEntityId}",
                signedDelta.Unit,
                signedDelta.FrameId,
                signedDelta.ContentSha256,
                $"Signed current-minus-background delta | paired {signedDelta.PairedValidSampleCount:N0} | missing {signedDelta.MissingEitherSampleCount:N0} | zero {signedDelta.ZeroDeltaSampleCount:N0} | +{signedDelta.PositiveDeltaSampleCount:N0} / -{signedDelta.NegativeDeltaSampleCount:N0} | current {signedDelta.CurrentSourceContentSha256} | background {signedDelta.BackgroundContentSha256} | finite zero is preserved outside C3D wire encoding",
                step,
                "SignedDeltaHeightField");
        }

        if (string.Equals(
                step.ToolId,
                "grid-polygon-domain",
                StringComparison.Ordinal)
            && snapshot.GridPolygonDomain.Output is { } gridDomain
            && string.Equals(
                gridDomain.ArtifactId,
                step.OutputEntityId,
                StringComparison.OrdinalIgnoreCase))
        {
            return new ToolWorkbenchArtifactItem(
                gridDomain.ArtifactId,
                step.ToolName,
                "GridDomainArtifact",
                snapshot.GridPolygonDomain.IsStale
                    ? "Stale"
                    : snapshot.GridPolygonDomain.IsPublished
                        ? "Published"
                        : "Preview",
                source.Id,
                string.Join("; ", step.InputEntityIds),
                gridDomain.Unit,
                gridDomain.FrameId,
                gridDomain.ContentSha256,
                $"GridPolygon domain | cells {gridDomain.Cells.Count:N0} | selection {gridDomain.SelectionId} | source {gridDomain.SourceContentSha256}",
                step,
                "GridDomainArtifact");
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
                snapshot.DomainMaskQualityDeltaEvidence);
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
                string.Format(
                    snapshot.DomainMaskArtifactDetailFormat,
                    domainMaskOutput.Width,
                    domainMaskOutput.Height,
                    domainMaskOutput.ValidCount,
                    domainMaskOutput.MissingCount,
                    snapshot.DomainMaskReducedDetail,
                    snapshot.SourceIdentityRetainedDetail,
                    qualityDelta is { } delta
                        ? snapshot.FormatSourceQualityDeltaSummary(delta)
                        : snapshot.QualityDeltaUnavailable),
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
                string.Format(
                    snapshot.EditableRegionArtifactDetailFormat,
                    editableRegionArtifact.RegionIndex,
                    editableRegionArtifact.Cells.Count,
                    editableRegionArtifact.Bounding.Width,
                    editableRegionArtifact.Bounding.Height,
                    editableRegionArtifact.SourceConnectedRegionContentSha256),
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
                snapshot.RemoveOutlierQualityDeltaEvidence);
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
                string.Format(
                    snapshot.RemoveOutlierArtifactDetailFormat,
                    outlierOutput.Width,
                    outlierOutput.Height,
                    outlierMask.OutlierCellCount,
                    outlierMask.Sha256,
                    snapshot.SourceIdentityRetainedDetail,
                    qualityDelta is { } delta
                        ? snapshot.FormatSourceQualityDeltaSummary(delta)
                        : snapshot.QualityDeltaUnavailable),
                step,
                "FilteredHeightField")
            {
                PreparationQualityDelta = qualityDelta
            };
        }

        if (string.Equals(
                step.ToolId,
                "height-threshold-background-removal",
                StringComparison.Ordinal)
            && snapshot.HeightThreshold.Output is { } thresholdOutput
            && snapshot.HeightThreshold.Evidence is { } thresholdEvidence)
        {
            var qualityDelta = snapshot.CreateSourceQualityDelta(
                thresholdOutput,
                thresholdEvidence.RemovedBackgroundSampleCount,
                snapshot.HeightThresholdQualityDeltaEvidence);
            return new ToolWorkbenchArtifactItem(
                thresholdOutput.EntityId,
                step.ToolName,
                "FilteredHeightField",
                snapshot.HeightThreshold.IsStale
                    ? "Stale"
                    : snapshot.HeightThreshold.IsPublished
                        ? "Published"
                        : "Preview",
                source.Id,
                source.Id,
                thresholdOutput.Unit,
                thresholdOutput.FrameId,
                thresholdOutput.ContentSha256,
                string.Format(
                    snapshot.HeightThresholdArtifactDetailFormat,
                    thresholdOutput.Width,
                    thresholdOutput.Height,
                    thresholdEvidence.Threshold,
                    thresholdEvidence.RetainedValidSampleCount,
                    thresholdEvidence.RemovedBackgroundSampleCount,
                    snapshot.SourceIdentityRetainedDetail),
                step,
                "FilteredHeightField")
            {
                PreparationQualityDelta = qualityDelta
            };
        }

        var filter = snapshot.Filter;
        if (step.ToolId == "filter" && !string.Equals(filter.Output?.EntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase))
            filter = new(snapshot.GetPublishedFilterOutput?.Invoke(step.OutputEntityId), false, true);
        if (string.Equals(step.ToolId, "filter", StringComparison.Ordinal)
            && filter.Output is { } filterPreviewOutput
            && string.Equals(filterPreviewOutput.EntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            var qualityDelta = snapshot.CreateSourceQualityDelta(
                filterPreviewOutput,
                null,
                snapshot.FilterQualityDeltaEvidence);
            return new ToolWorkbenchArtifactItem(
                filterPreviewOutput.EntityId,
                step.ToolName,
                "FilteredHeightField",
                filter.IsStale ? "Stale" : filter.IsPublished ? "Published" : "Preview",
                source.Id,
                source.Id,
                filterPreviewOutput.Unit,
                filterPreviewOutput.FrameId,
                filterPreviewOutput.ContentSha256,
                string.Format(
                    snapshot.FilterArtifactDetailFormat,
                    filterPreviewOutput.Width,
                    filterPreviewOutput.Height,
                    filterPreviewOutput.Provenance,
                    qualityDelta is { } delta
                        ? snapshot.FormatSourceQualityDeltaSummary(delta)
                        : snapshot.QualityDeltaUnavailable),
                step,
                "FilteredHeightField")
            {
                PreparationQualityDelta = qualityDelta
            };
        }

        var edge = snapshot.HeightDifferenceEdge;
        if (step.ToolId == "height-difference-edge" && !string.Equals(edge.Output?.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase))
            edge = new(snapshot.GetPublishedHeightDifferenceEdgeOutput?.Invoke(step.OutputEntityId), false, true);
        if (string.Equals(step.ToolId, "height-difference-edge", StringComparison.Ordinal)
            && edge.Output is { } edgePreviewOutput
            && string.Equals(edgePreviewOutput.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            return new ToolWorkbenchArtifactItem(
                edgePreviewOutput.OutputEntityId,
                step.ToolName,
                "EdgePointSet",
                edge.IsStale ? "Stale" : edge.IsPublished ? "Published" : "Preview",
                edgePreviewOutput.RootSourceEntityId,
                edgePreviewOutput.InputEntityId,
                edgePreviewOutput.Unit,
                edgePreviewOutput.FrameId,
                edgePreviewOutput.ContentSha256,
                string.Format(
                    snapshot.HeightDifferenceEdgeArtifactDetailFormat,
                    edgePreviewOutput.Points.Count,
                    edgePreviewOutput.Provenance),
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
                string.Format(
                    snapshot.LineFitArtifactDetailFormat,
                    publishedLine.Diagnostics.InlierCount,
                    publishedLine.Diagnostics.InputPointCount,
                    publishedLine.Provenance),
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
                string.Format(
                    snapshot.TwoPointLineArtifactDetailFormat,
                    publishedTwoPointLine.FirstRow,
                    publishedTwoPointLine.FirstColumn,
                    publishedTwoPointLine.SecondRow,
                    publishedTwoPointLine.SecondColumn,
                    publishedTwoPointLine.Provenance),
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
                string.Format(
                    snapshot.ThreePointPlaneArtifactDetailFormat,
                    publishedThreePointPlane.FirstRow,
                    publishedThreePointPlane.FirstColumn,
                    publishedThreePointPlane.SecondRow,
                    publishedThreePointPlane.SecondColumn,
                    publishedThreePointPlane.ThirdRow,
                    publishedThreePointPlane.ThirdColumn,
                    publishedThreePointPlane.Provenance),
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
                string.Format(
                    snapshot.DatumPlaneDeviationArtifactDetailFormat,
                    publishedDatumDeviation.OutputRole,
                    publishedDatumDeviation.PeakToValleyRawHeight,
                    publishedDatumDeviation.ValidSampleCount,
                    publishedDatumDeviation.Provenance),
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
                string.Format(
                    snapshot.LineIntersectionArtifactDetailFormat,
                    publishedIntersection.OutputRole,
                    publishedIntersection.ClosestApproachDistance,
                    publishedIntersection.AcuteAngleDegrees),
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
                string.Format(
                    snapshot.LandmarkCorrespondenceArtifactDetailFormat,
                    correspondenceOutput.Pairs.Count,
                    correspondenceOutput.SourceRank,
                    correspondenceOutput.ReferenceRank),
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
                string.Format(
                    snapshot.XyzAffineSolveArtifactDetailFormat,
                    publishedAffine.ConditionEstimate,
                    publishedAffine.ArithmeticMaximumResidual),
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
                string.Format(
                    snapshot.XyzAffineApplyArtifactDetailFormat,
                    affineApplyOutput.FinitePointCount,
                    affineApplyOutput.MissingPointCount),
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
                string.Format(
                    snapshot.RegridHeightFieldArtifactDetailFormat,
                    regridHeightFieldOutput.PopulatedCellCount,
                    regridHeightFieldOutput.Cells.Count,
                    regridHeightFieldOutput.CoverageRatio,
                    regridHeightFieldOutput.MissingCellCount,
                    regridHeightFieldOutput.CollisionCount),
                step,
                "TransformedHeightField");
        }

        if (string.Equals(step.ToolId, "physical-edge-gap-flush", StringComparison.Ordinal))
        {
            if (snapshot.PhysicalEdgeGapFlush.Output is { } physicalOutput
                && string.Equals(
                    physicalOutput.OutputEntityId,
                    step.OutputEntityId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return new ToolWorkbenchArtifactItem(
                    physicalOutput.OutputEntityId,
                    step.ToolName,
                    "MeasurementResult",
                    snapshot.PhysicalEdgeGapFlush.IsStale
                        ? "Stale"
                        : snapshot.PhysicalEdgeGapFlush.IsPublished
                            ? "Published"
                            : "Preview",
                    physicalOutput.RootSourceEntityId,
                    string.Join(
                        "; ",
                        physicalOutput.FirstLineEntityId,
                        physicalOutput.SecondLineEntityId,
                        physicalOutput.FirstEdgePointSetEntityId,
                        physicalOutput.SecondEdgePointSetEntityId),
                    physicalOutput.Unit,
                    physicalOutput.FrameId,
                    physicalOutput.ContentSha256,
                    physicalOutput.EvidenceSummary,
                    step,
                    "MeasurementResult");
            }

            return new ToolWorkbenchArtifactItem(
                step.OutputEntityId,
                step.ToolName,
                "MeasurementResult",
                "Declared",
                source.Id,
                string.Join("; ", step.InputEntityIds),
                source.Unit,
                source.FrameId,
                string.Empty,
                "Physical Edge Gap / Flush requires authored LineFeature A/B followed by EdgePointSet A/B. Preview is explicit; no result exists yet.",
                step,
                "DeclaredOutput");
        }

        if (step.ToolId is "thickness" or "warpage" or "plane-flatness" or "point-pair-dimensions" or "gap-flush" or "volume" or "cross-section-dimensions" or "completeness-grid" or "connected-region-gate"
            && snapshot.Measurement.Output is { } measurementOutput
            && string.Equals(measurementOutput.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            var gateOutput = measurementOutput.ConnectedRegionGate;
            var outputContract = gateOutput is not null
                ? "ConnectedRegionGate"
                : measurementOutput.CompletenessGrid is null
                    ? "MeasurementResult"
                    : "CompletenessGridMetrics";
            return new ToolWorkbenchArtifactItem(
                measurementOutput.OutputEntityId,
                step.ToolName,
                outputContract,
                snapshot.Measurement.IsStale ? "Stale" : snapshot.Measurement.IsPublished ? "Published" : "Preview",
                measurementOutput.RootSourceEntityId,
                gateOutput is null
                    ? $"{measurementOutput.InputEntityId}; {measurementOutput.SelectionId}"
                    : $"{gateOutput.RawSourceEntityId}; {gateOutput.DetectionEntityId}; {gateOutput.RegionArtifactId}",
                measurementOutput.Unit,
                measurementOutput.FrameId,
                measurementOutput.ContentSha256,
                string.Format(
                    snapshot.MeasurementArtifactDetailFormat,
                    snapshot.MeasurementResultStatusLabel,
                    measurementOutput.Result.Message == ToolRecipeStep.UnconfiguredAcceptanceMessage
                        ? snapshot.MeasurementResultMessage : measurementOutput.EvidenceSummary),
                step,
                outputContract);
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
            string.Format(snapshot.DeclaredOutputDetailFormat, step.OutputEntityId),
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

internal sealed record ToolWorkbenchHeightThresholdArtifactPreview(
    C3DHeightFieldSnapshot? Output,
    C3DHeightThresholdBackgroundRemovalEvidence? Evidence,
    bool IsStale,
    bool IsPublished);

internal sealed record ToolWorkbenchArtifactProjectionSnapshot(
    ToolWorkbenchSourceItem Source,
    bool IsSourceReadyForRecipe,
    ToolRecipeSelectionSourceBinding? SourceBinding,
    string SourceReadinessSummary,
    string SourceContextSummary,
    string SourceArtifactReadyDetailFormat,
    IReadOnlyList<ToolWorkbenchReferenceItem> References,
    IReadOnlyList<ToolRecipeSelection> Selections,
    Func<ToolRecipeSelection, bool> IsSelectionCurrent,
    IReadOnlyList<ToolWorkbenchPipelineStepItem> PipelineSteps,
    Func<C3DHeightFieldSnapshot, long?, string, SourceQualityDelta?> CreateSourceQualityDelta,
    Func<SourceQualityDelta, string> FormatSourceQualityDeltaSummary,
    string QualityDeltaUnavailable,
    string RoiCropQualityDeltaEvidence,
    string LevelSurfaceQualityDeltaEvidence,
    string DomainMaskQualityDeltaEvidence,
    string HeightThresholdQualityDeltaEvidence,
    string RemoveOutlierQualityDeltaEvidence,
    string FilterQualityDeltaEvidence,
    string DeclaredOutputDetailFormat,
    string OutputDisabledDetailFormat,
    string CurrentSelectionDetail,
    string StaleSelectionDetail,
    string SourceIdentityRetainedDetail,
    string DomainMaskReducedDetail,
    string DomainMaskArtifactDetailFormat,
    string ConnectedRegionArtifactDetailFormat,
    string EditableRegionArtifactDetailFormat,
    string RemoveOutlierArtifactDetailFormat,
    string RoiCropArtifactDetailFormat,
    string LevelSurfaceArtifactDetailFormat,
    string FilterArtifactDetailFormat,
    string HeightThresholdArtifactDetailFormat,
    string HeightDifferenceEdgeArtifactDetailFormat,
    string LineFitArtifactDetailFormat,
    string TwoPointLineArtifactDetailFormat,
    string ThreePointPlaneArtifactDetailFormat,
    string DatumPlaneDeviationArtifactDetailFormat,
    string LineIntersectionArtifactDetailFormat,
    string LandmarkCorrespondenceArtifactDetailFormat,
    string XyzAffineSolveArtifactDetailFormat,
    string XyzAffineApplyArtifactDetailFormat,
    string RegridHeightFieldArtifactDetailFormat,
    string MeasurementArtifactDetailFormat,
    string MeasurementResultStatusLabel,
    string MeasurementResultMessage,
    Func<ResultStatus, string> ResultStatusLabel,
    ToolWorkbenchArtifactPreview<C3DHeightFieldSnapshot> RoiCrop,
    ToolWorkbenchLevelSurfaceArtifactPreview LevelSurface,
    ToolWorkbenchArtifactPreview<C3DConnectedRegionArtifact> ConnectedRegion,
    ToolWorkbenchArtifactPreview<C3DGridDomainArtifact> GridPolygonDomain,
    ToolWorkbenchArtifactPreview<C3DHeightFieldSnapshot> DomainMask,
    ToolWorkbenchArtifactPreview<C3DEditableRegionArtifact> EditableRegion,
    ToolWorkbenchHeightThresholdArtifactPreview HeightThreshold,
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
    ToolWorkbenchArtifactPreview<C3DPhysicalEdgeGapFlushResult> PhysicalEdgeGapFlush,
    ToolWorkbenchArtifactPreview<ToolRecipeHeightMeasurementOutput> Measurement,
    ToolWorkbenchArtifactPreview<C3DSignedHeightDeltaArtifact>? SignedDeltaBackgroundSubtraction = null,
    Func<string, C3DHeightFieldSnapshot?>? GetPublishedFilterOutput = null,
    Func<string, C3DHeightDifferenceEdgePointSet?>? GetPublishedHeightDifferenceEdgeOutput = null,
    SourceQualityReport? SourceQuality = null,
    Func<string, C3DHeightFieldSnapshot?>? GetNinePointOutput = null,
    Func<string, bool>? IsNinePointPublished = null,
    Func<ToolWorkbenchPipelineStepItem, ToolWorkbenchArtifactItem?>? ProjectOrderedArtifact = null);
