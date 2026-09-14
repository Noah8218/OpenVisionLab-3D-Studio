using OpenVisionLab.ThreeD.Viewer.Models;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

internal enum ViewerInspectionKind
{
    SyntheticHeightDeviation,
    C3DHeightDeviation,
    C3DThickness,
    C3DWarpage,
    C3DPlaneFlatness,
    C3DPointPairDimensions,
    C3DGapFlush,
    C3DVolume,
    C3DCrossSectionDimensions,
    LazTwoPointMeasurement
}

internal sealed class ViewerInspectionSession
{
    public ViewerInspectionSession() => Reset();

    public ViewerInspectionKind ActiveKind { get; private set; }

    public string PreviewLayerId { get; private set; } = string.Empty;

    public string PreviewLayerName { get; private set; } = string.Empty;

    public string SourceEntityId { get; private set; } = string.Empty;

    public string ResultEntityId { get; private set; } = string.Empty;

    public string ResultEntityName { get; private set; } = string.Empty;

    public void Reset() => Activate(ViewerInspectionKind.SyntheticHeightDeviation);

    public void Activate(ViewerInspectionKind kind)
    {
        var (previewLayerId, previewLayerName, sourceEntityId, resultEntityId, resultEntityName) = kind switch
        {
            ViewerInspectionKind.C3DHeightDeviation => (
                "layer.preview.c3d-height-deviation",
                "Preview: C3D Height Deviation Rule",
                ViewerEntityIds.C3DEntityId,
                ViewerEntityIds.C3DHeightDeviationResultEntityId,
                "Published C3D Height Deviation"),
            ViewerInspectionKind.C3DThickness => (
                "layer.preview.c3d-thickness",
                "Preview: C3D Thickness",
                ViewerEntityIds.C3DEntityId,
                ViewerEntityIds.C3DThicknessResultEntityId,
                "Published C3D Thickness"),
            ViewerInspectionKind.C3DWarpage => (
                "layer.preview.c3d-warpage",
                "Preview: C3D Warpage",
                ViewerEntityIds.C3DWarpageEntityId,
                ViewerEntityIds.C3DWarpageResultEntityId,
                "Published C3D Warpage"),
            ViewerInspectionKind.C3DPlaneFlatness => (
                "layer.preview.c3d-plane-flatness",
                "Preview: C3D Plane Flatness",
                ViewerEntityIds.C3DEntityId,
                ViewerEntityIds.C3DPlaneFlatnessResultEntityId,
                "Published C3D Plane Flatness"),
            ViewerInspectionKind.C3DPointPairDimensions => (
                "layer.preview.c3d-point-pair-dimensions",
                "Preview: C3D Point Pair Dimensions",
                ViewerEntityIds.C3DEntityId,
                ViewerEntityIds.C3DPointPairDimensionsResultEntityId,
                "Published C3D Point Pair Dimensions"),
            ViewerInspectionKind.C3DGapFlush => (
                "layer.preview.c3d-gap-flush",
                "Preview: C3D Gap / Flush",
                ViewerEntityIds.C3DEntityId,
                ViewerEntityIds.C3DGapFlushResultEntityId,
                "Published C3D Gap / Flush"),
            ViewerInspectionKind.C3DVolume => (
                "layer.preview.c3d-volume",
                "Preview: C3D Volume",
                ViewerEntityIds.C3DEntityId,
                ViewerEntityIds.C3DVolumeResultEntityId,
                "Published C3D Volume"),
            ViewerInspectionKind.C3DCrossSectionDimensions => (
                "layer.preview.c3d-cross-section-dimensions",
                "Preview: C3D Cross-section Dimensions",
                ViewerEntityIds.C3DEntityId,
                ViewerEntityIds.C3DCrossSectionResultEntityId,
                "Published C3D Cross-section Dimensions"),
            ViewerInspectionKind.LazTwoPointMeasurement => (
                "layer.preview.laz-two-point-measurement",
                "Preview: LAZ/LAS Two Point Measurement",
                ViewerEntityIds.LazEntityId,
                ViewerEntityIds.LazTwoPointResultEntityId,
                "Published LAZ/LAS Two Point Measurement"),
            _ => (
                "layer.preview.synthetic-height-deviation",
                "Preview: Synthetic Height Deviation",
                ViewerEntityIds.PointCloudEntityId,
                ViewerEntityIds.SyntheticResultEntityId,
                "Published Synthetic Height Deviation")
        };

        ActiveKind = kind;
        PreviewLayerId = previewLayerId;
        PreviewLayerName = previewLayerName;
        SourceEntityId = sourceEntityId;
        ResultEntityId = resultEntityId;
        ResultEntityName = resultEntityName;
    }
}
