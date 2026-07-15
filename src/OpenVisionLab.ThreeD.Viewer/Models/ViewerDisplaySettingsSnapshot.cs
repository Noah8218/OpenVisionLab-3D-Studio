namespace OpenVisionLab.ThreeD.Viewer.Models;

internal enum ViewerDisplaySourceKind
{
    GeneratedGeometry,
    C3DHeightGrid,
    ImportedTriangleMesh,
    PointCloud,
    NominalActualComparison
}

internal enum ViewerGeometryStyle
{
    Points,
    Wireframe,
    Surface,
    SurfaceWithEdges
}

internal enum ViewerColorMap
{
    Source,
    Solid,
    Grayscale,
    Height,
    Thermal,
    Deviation,
    Rgb
}

internal readonly record struct ViewerDisplaySettingsSnapshot(
    ViewerDisplaySourceKind Source,
    ViewerGeometryStyle GeometryStyle,
    ViewerColorMap ColorMap,
    bool IsDisplayOnly);
