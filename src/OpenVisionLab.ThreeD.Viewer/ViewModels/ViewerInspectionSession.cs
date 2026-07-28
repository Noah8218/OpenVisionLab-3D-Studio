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
    internal const string PointCloudEntityId = "source.generated-point-cloud";
    internal const string C3DEntityId = "source.c3d-thickness";
    internal const string C3DWarpageEntityId = "source.c3d-warpage";
    internal const string LazEntityId = "source.public-laz-manuscript";
    internal const string SyntheticResultEntityId = "result.synthetic-height-deviation";
    internal const string C3DHeightDeviationResultEntityId = "result.c3d-height-deviation";
    internal const string C3DThicknessResultEntityId = "result.c3d-thickness";
    internal const string C3DWarpageResultEntityId = "result.c3d-warpage";
    internal const string C3DPlaneFlatnessResultEntityId = "result.c3d-plane-flatness";
    internal const string C3DPointPairDimensionsResultEntityId = "result.c3d-point-pair-dimensions";
    internal const string C3DGapFlushResultEntityId = "result.c3d-gap-flush";
    internal const string C3DVolumeResultEntityId = "result.c3d-volume";
    internal const string C3DCrossSectionResultEntityId = "result.c3d-cross-section-dimensions";
    internal const string LazTwoPointResultEntityId = "result.laz-two-point-measurement";

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
                C3DEntityId,
                C3DHeightDeviationResultEntityId,
                "Published C3D Height Deviation"),
            ViewerInspectionKind.C3DThickness => (
                "layer.preview.c3d-thickness",
                "Preview: C3D Thickness",
                C3DEntityId,
                C3DThicknessResultEntityId,
                "Published C3D Thickness"),
            ViewerInspectionKind.C3DWarpage => (
                "layer.preview.c3d-warpage",
                "Preview: C3D Warpage",
                C3DWarpageEntityId,
                C3DWarpageResultEntityId,
                "Published C3D Warpage"),
            ViewerInspectionKind.C3DPlaneFlatness => (
                "layer.preview.c3d-plane-flatness",
                "Preview: C3D Plane Flatness",
                C3DEntityId,
                C3DPlaneFlatnessResultEntityId,
                "Published C3D Plane Flatness"),
            ViewerInspectionKind.C3DPointPairDimensions => (
                "layer.preview.c3d-point-pair-dimensions",
                "Preview: C3D Point Pair Dimensions",
                C3DEntityId,
                C3DPointPairDimensionsResultEntityId,
                "Published C3D Point Pair Dimensions"),
            ViewerInspectionKind.C3DGapFlush => (
                "layer.preview.c3d-gap-flush",
                "Preview: C3D Gap / Flush",
                C3DEntityId,
                C3DGapFlushResultEntityId,
                "Published C3D Gap / Flush"),
            ViewerInspectionKind.C3DVolume => (
                "layer.preview.c3d-volume",
                "Preview: C3D Volume",
                C3DEntityId,
                C3DVolumeResultEntityId,
                "Published C3D Volume"),
            ViewerInspectionKind.C3DCrossSectionDimensions => (
                "layer.preview.c3d-cross-section-dimensions",
                "Preview: C3D Cross-section Dimensions",
                C3DEntityId,
                C3DCrossSectionResultEntityId,
                "Published C3D Cross-section Dimensions"),
            ViewerInspectionKind.LazTwoPointMeasurement => (
                "layer.preview.laz-two-point-measurement",
                "Preview: LAZ/LAS Two Point Measurement",
                LazEntityId,
                LazTwoPointResultEntityId,
                "Published LAZ/LAS Two Point Measurement"),
            _ => (
                "layer.preview.synthetic-height-deviation",
                "Preview: Synthetic Height Deviation",
                PointCloudEntityId,
                SyntheticResultEntityId,
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
