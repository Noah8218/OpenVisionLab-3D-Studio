using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.Models;

namespace OpenVisionLab.ThreeD.Viewer.Rendering;

/// <summary>
/// Immutable identity for a compiled C3D display-list snapshot.
/// </summary>
internal readonly record struct C3DDisplayListKey(
    C3DHeightGrid Source,
    ModelTransform Transform,
    ViewerGeometryStyle GeometryStyle,
    ViewerColorMap ColorMap,
    double HeightColorMinimumRaw,
    double HeightColorMaximumRaw,
    double PointSize,
    C3DWireframeLodLevel WireframeLodLevel);

/// <summary>
/// Immutable identity for an uploaded C3D GPU buffer snapshot.
/// </summary>
internal readonly record struct C3DGpuBufferKey(
    C3DHeightGrid Source,
    ModelTransform Transform,
    ViewerColorMap ColorMap,
    double HeightColorMinimumRaw,
    double HeightColorMaximumRaw,
    PlaneFlatnessEvaluation? DynamicColorEvaluation);
