namespace OpenVisionLab.ThreeD.Viewer.Models;

/// <summary>
/// WPF-neutral identity contract for the Viewer source and result entities.
/// </summary>
public static class ViewerEntityIds
{
    public const string PointCloudEntityId = "source.generated-point-cloud";
    public const string C3DEntityId = "source.c3d-thickness";
    public const string C3DWarpageEntityId = "source.c3d-warpage";
    public const string LazEntityId = "source.public-laz-manuscript";
    public const string SyntheticResultEntityId = "result.synthetic-height-deviation";
    public const string C3DHeightDeviationResultEntityId = "result.c3d-height-deviation";
    public const string C3DThicknessResultEntityId = "result.c3d-thickness";
    public const string C3DWarpageResultEntityId = "result.c3d-warpage";
    public const string C3DPlaneFlatnessResultEntityId = "result.c3d-plane-flatness";
    public const string C3DPointPairDimensionsResultEntityId = "result.c3d-point-pair-dimensions";
    public const string C3DGapFlushResultEntityId = "result.c3d-gap-flush";
    public const string C3DVolumeResultEntityId = "result.c3d-volume";
    public const string C3DCrossSectionResultEntityId = "result.c3d-cross-section-dimensions";
    public const string LazTwoPointResultEntityId = "result.laz-two-point-measurement";
}
