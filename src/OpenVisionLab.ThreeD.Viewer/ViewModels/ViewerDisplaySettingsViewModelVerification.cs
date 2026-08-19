using System.Globalization;
using System.IO;
using System.Numerics;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.Rendering;
using OpenVisionLab.ThreeD.Viewer.Localization;
using OpenVisionLab;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

internal static class ViewerDisplaySettingsViewModelVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D display-settings ViewModel verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;

        try
        {
            var viewModel = new ViewerDisplaySettingsViewModel();
            var propertyChanges = 0;
            var renderChanges = 0;
            viewModel.PropertyChanged += (_, _) => propertyChanges++;
            viewModel.RenderSettingsChanged += (_, _) => renderChanges++;

            Check("initial source", viewModel.ActiveSource == "Generated geometry", viewModel.ActiveSource);
            Check("initial geometry", viewModel.EffectiveGeometryStyle == "Points", viewModel.EffectiveSummary);
            Check("initial color", viewModel.EffectiveColorMap == "Height", viewModel.EffectiveSummary);
            Check("initial display-only", viewModel.IsDisplayOnly && !viewModel.CanSelectGeometryStyle, viewModel.EffectiveSummary);
            Check("initial color selectable", viewModel.CanSelectColorMap, viewModel.CanSelectColorMap.ToString());
            Check("initial choices", Sequence(viewModel.AvailableColorMaps, "Solid", "Height"), string.Join(",", viewModel.AvailableColorMaps));
            var initialSettings = viewModel.EffectiveSettings;
            Check(
                "initial typed snapshot",
                initialSettings == new ViewerDisplaySettingsSnapshot(
                    ViewerDisplaySourceKind.GeneratedGeometry,
                    ViewerGeometryStyle.Points,
                    ViewerColorMap.Height,
                    IsDisplayOnly: true),
                initialSettings.ToString());
            Check(
                "initial render density",
                viewModel.PointSize == 2.0
                && viewModel.SelectedRenderDensity == "Balanced"
                && viewModel.C3DMaxRenderedPoints == 55000
                && viewModel.LazMaxSampledPoints == 50000
                && viewModel.ImportedMeshMaxRenderedTriangles == 60000
                && viewModel.NominalActualMaxDisplaySamples == 60000,
                viewModel.RenderDensitySummary);
            viewModel.PointSize = 9.0;
            Check(
                "point size owner clamps existing range",
                viewModel.PointSize == 6.0,
                viewModel.PointSize.ToString("F1", CultureInfo.InvariantCulture));
            viewModel.SelectedRenderDensity = "Fast";
            Check(
                "fast render density budgets",
                viewModel.C3DMaxRenderedPoints == 25000
                && viewModel.LazMaxSampledPoints == 25000
                && viewModel.ImportedMeshMaxRenderedTriangles == 25000
                && viewModel.NominalActualMaxDisplaySamples == 25000
                && viewModel.RenderDensitySummary.StartsWith("Fast:", StringComparison.Ordinal),
                viewModel.RenderDensitySummary);
            viewModel.SelectedRenderDensity = "Detailed";
            Check(
                "detailed render density budgets",
                viewModel.C3DMaxRenderedPoints == 140000
                && viewModel.LazMaxSampledPoints == 150000
                && viewModel.ImportedMeshMaxRenderedTriangles == 180000
                && viewModel.NominalActualMaxDisplaySamples == 150000
                && viewModel.RenderDensitySummary.StartsWith("Detailed:", StringComparison.Ordinal),
                viewModel.RenderDensitySummary);
            viewModel.SelectedRenderDensity = "Unsupported";
            Check(
                "unsupported render density keeps existing Balanced fallback",
                viewModel.SelectedRenderDensity == "Balanced"
                && viewModel.C3DMaxRenderedPoints == 55000
                && renderChanges == 0
                && viewModel.DisplaySettingsRevision == 0,
                $"density={viewModel.SelectedRenderDensity}|renders={renderChanges}|revision={viewModel.DisplaySettingsRevision}");

            viewModel.SelectedGeometryStyle = "Surface";
            Check("geometry bridge guard", viewModel.SelectedGeometryStyle == "Points" && viewModel.FallbackApplied, viewModel.FallbackSummary);
            Check("geometry guard does not render", renderChanges == 0, renderChanges.ToString(CultureInfo.InvariantCulture));

            viewModel.SelectedColorMap = "Solid";
            Check("valid color selection", viewModel.SelectedColorMap == "Solid" && !viewModel.FallbackApplied, viewModel.EffectiveSummary);
            Check("valid color notifies renderer", renderChanges == 1, renderChanges.ToString(CultureInfo.InvariantCulture));
            Check(
                "snapshot is immutable",
                initialSettings.ColorMap == ViewerColorMap.Height
                && viewModel.EffectiveSettings.ColorMap == ViewerColorMap.Solid,
                $"before={initialSettings.ColorMap}|after={viewModel.EffectiveSettings.ColorMap}");

            var c3dViewModel = new ViewerDisplaySettingsViewModel();
            var c3dRenderChanges = 0;
            c3dViewModel.RenderSettingsChanged += (_, _) => c3dRenderChanges++;
            c3dViewModel.ConfigureC3DHeightGrid(deviationAvailable: false);
            Check("C3D source", c3dViewModel.ActiveSource == "C3D height grid", c3dViewModel.ActiveSource);
            Check("C3D geometry choices", Sequence(c3dViewModel.AvailableGeometryStyles, "Points", "Wireframe", "Surface", "Surface + Edges"), string.Join(",", c3dViewModel.AvailableGeometryStyles));
            Check(
                "C3D color choices",
                Sequence(c3dViewModel.AvailableColorMaps, "Solid", "Grayscale", "Height", "Thermal"),
                string.Join(",", c3dViewModel.AvailableColorMaps));
            Check("C3D current default", c3dViewModel.SelectedGeometryStyle == "Surface" && c3dViewModel.SelectedColorMap == "Height", c3dViewModel.EffectiveSummary);
            Check("C3D geometry selectable", c3dViewModel.CanSelectGeometryStyle, c3dViewModel.CanSelectGeometryStyle.ToString());
            Check(
                "C3D typed snapshot",
                c3dViewModel.EffectiveSettings.Source == ViewerDisplaySourceKind.C3DHeightGrid
                && c3dViewModel.EffectiveSettings.GeometryStyle == ViewerGeometryStyle.Surface
                && c3dViewModel.EffectiveSettings.ColorMap == ViewerColorMap.Height,
                c3dViewModel.EffectiveSettings.ToString());

            c3dViewModel.SelectedGeometryStyle = "Wireframe";
            Check(
                "C3D geometry selection",
                c3dViewModel.EffectiveSettings.GeometryStyle == ViewerGeometryStyle.Wireframe
                && !c3dViewModel.FallbackApplied,
                c3dViewModel.EffectiveSettings.ToString());
            Check("C3D geometry render notification", c3dRenderChanges == 1, c3dRenderChanges.ToString(CultureInfo.InvariantCulture));
            c3dViewModel.SelectedGeometryStyle = "Contours";
            Check(
                "C3D unsupported geometry fallback",
                c3dViewModel.SelectedGeometryStyle == "Wireframe"
                && c3dViewModel.FallbackApplied
                && c3dRenderChanges == 1,
                c3dViewModel.FallbackSummary);
            c3dViewModel.SelectedGeometryStyle = "Points";
            Check("C3D geometry reset", c3dViewModel.SelectedGeometryStyle == "Points" && c3dRenderChanges == 2, c3dViewModel.EffectiveSummary);

            c3dViewModel.SelectedColorMap = "Grayscale";
            Check(
                "C3D grayscale selection",
                c3dViewModel.EffectiveSettings.ColorMap == ViewerColorMap.Grayscale
                && c3dRenderChanges == 3,
                c3dViewModel.EffectiveSummary);
            c3dViewModel.SelectedColorMap = "Thermal";
            Check(
                "C3D thermal selection",
                c3dViewModel.EffectiveSettings.ColorMap == ViewerColorMap.Thermal
                && c3dRenderChanges == 4,
                c3dViewModel.EffectiveSummary);
            c3dViewModel.SelectedColorMap = "Height";
            Check(
                "C3D height reset",
                c3dViewModel.EffectiveSettings.ColorMap == ViewerColorMap.Height
                && c3dRenderChanges == 5,
                c3dViewModel.EffectiveSummary);

            c3dViewModel.SelectedColorMap = "RGB";
            Check("C3D unsupported color fallback", c3dViewModel.SelectedColorMap == "Height" && c3dViewModel.FallbackApplied, c3dViewModel.FallbackSummary);
            Check("unchanged fallback does not render", c3dRenderChanges == 5, c3dRenderChanges.ToString(CultureInfo.InvariantCulture));

            c3dViewModel.ConfigureC3DHeightGrid(deviationAvailable: true);
            Check(
                "C3D result color capability",
                Sequence(c3dViewModel.AvailableColorMaps, "Solid", "Grayscale", "Height", "Thermal", "Deviation"),
                string.Join(",", c3dViewModel.AvailableColorMaps));
            c3dViewModel.SelectedColorMap = "Deviation";
            Check("C3D deviation selection", c3dViewModel.SelectedColorMap == "Deviation", c3dViewModel.EffectiveSummary);
            Check("C3D deviation render notification", c3dRenderChanges == 6, c3dRenderChanges.ToString(CultureInfo.InvariantCulture));
            c3dViewModel.ResetC3DHeightGridGeometryStyle();
            Check(
                "new C3D source resets display geometry to Surface",
                c3dViewModel.SelectedGeometryStyle == "Surface" && c3dRenderChanges == 7,
                $"{c3dViewModel.EffectiveSummary}|renders={c3dRenderChanges}");

            c3dViewModel.ConfigurePointCloud(sourceColorAvailable: true);
            Check("point-cloud geometry capability", Sequence(c3dViewModel.AvailableGeometryStyles, "Points"), string.Join(",", c3dViewModel.AvailableGeometryStyles));
            Check("point-cloud geometry disabled", !c3dViewModel.CanSelectGeometryStyle, c3dViewModel.CanSelectGeometryStyle.ToString());
            Check("point-cloud color capability", Sequence(c3dViewModel.AvailableColorMaps, "Solid", "Height", "RGB"), string.Join(",", c3dViewModel.AvailableColorMaps));
            Check("source-change fallback", c3dViewModel.SelectedColorMap == "RGB" && c3dViewModel.FallbackApplied, c3dViewModel.FallbackSummary);
            Check("deviation fallback is explicit", c3dViewModel.FallbackSummary.Contains("Deviation requires an active result", StringComparison.Ordinal), c3dViewModel.FallbackSummary);

            c3dViewModel.ConfigurePointCloud(sourceColorAvailable: false);
            Check("point-cloud no-RGB capability", Sequence(c3dViewModel.AvailableColorMaps, "Solid", "Height"), string.Join(",", c3dViewModel.AvailableColorMaps));
            Check("point-cloud no-RGB fallback", c3dViewModel.SelectedColorMap == "Height" && c3dViewModel.FallbackApplied, c3dViewModel.FallbackSummary);

            var meshViewModel = new ViewerDisplaySettingsViewModel();
            var meshRenderChanges = 0;
            meshViewModel.RenderSettingsChanged += (_, _) => meshRenderChanges++;
            meshViewModel.ConfigureImportedMesh(sourceColorAvailable: false);
            Check("mesh geometry choices", Sequence(meshViewModel.AvailableGeometryStyles, "Points", "Wireframe", "Surface", "Surface + Edges"), string.Join(",", meshViewModel.AvailableGeometryStyles));
            Check("mesh geometry selectable", meshViewModel.CanSelectGeometryStyle, meshViewModel.CanSelectGeometryStyle.ToString());
            Check("mesh current geometry", meshViewModel.SelectedGeometryStyle == "Surface + Edges", meshViewModel.EffectiveSummary);
            Check("mesh solid capability", Sequence(meshViewModel.AvailableColorMaps, "Solid") && meshViewModel.SelectedColorMap == "Solid" && !meshViewModel.CanSelectColorMap, meshViewModel.EffectiveSummary);
            Check(
                "mesh typed snapshot",
                meshViewModel.EffectiveSettings.Source == ViewerDisplaySourceKind.ImportedTriangleMesh
                && meshViewModel.EffectiveSettings.GeometryStyle == ViewerGeometryStyle.SurfaceWithEdges
                && meshViewModel.EffectiveSettings.ColorMap == ViewerColorMap.Solid,
                meshViewModel.EffectiveSettings.ToString());

            var meshGeometryRenderChanges = meshRenderChanges;
            meshViewModel.SelectedGeometryStyle = "Points";
            meshViewModel.SelectedGeometryStyle = "Wireframe";
            meshViewModel.SelectedGeometryStyle = "Surface";
            meshViewModel.SelectedGeometryStyle = "Surface + Edges";
            Check(
                "mesh geometry selections notify renderer",
                meshViewModel.SelectedGeometryStyle == "Surface + Edges"
                && meshRenderChanges == meshGeometryRenderChanges + 4,
                $"style={meshViewModel.SelectedGeometryStyle}|renderChanges={meshRenderChanges}");
            meshViewModel.SelectedGeometryStyle = "Contours";
            Check(
                "mesh unsupported geometry fallback",
                meshViewModel.SelectedGeometryStyle == "Surface + Edges"
                && meshViewModel.FallbackApplied
                && meshRenderChanges == meshGeometryRenderChanges + 4,
                meshViewModel.FallbackSummary);

            meshViewModel.ConfigureImportedMesh(sourceColorAvailable: true);
            Check("mesh source-color capability", Sequence(meshViewModel.AvailableColorMaps, "Source") && meshViewModel.SelectedColorMap == "Source" && !meshViewModel.CanSelectColorMap, meshViewModel.EffectiveSummary);

            c3dViewModel.ConfigureNominalActualComparison(deviationAvailable: true);
            Check("nominal-actual current geometry", c3dViewModel.SelectedGeometryStyle == "Points", c3dViewModel.EffectiveSummary);
            Check("nominal-actual deviation", Sequence(c3dViewModel.AvailableColorMaps, "Deviation") && c3dViewModel.SelectedColorMap == "Deviation", c3dViewModel.EffectiveSummary);

            var pointOnlyC3D = new ViewerDisplaySettingsViewModel();
            pointOnlyC3D.ConfigureC3DHeightGrid(deviationAvailable: false, surfaceGeometryAvailable: false);
            Check(
                "C3D point-only capability",
                Sequence(pointOnlyC3D.AvailableGeometryStyles, "Points")
                && !pointOnlyC3D.CanSelectGeometryStyle,
                pointOnlyC3D.EffectiveSummary);
            pointOnlyC3D.SelectedGeometryStyle = "Surface";
            Check(
                "C3D point-only guard",
                pointOnlyC3D.SelectedGeometryStyle == "Points"
                && pointOnlyC3D.FallbackApplied
                && pointOnlyC3D.FallbackSummary.Contains(
                    "not selectable for C3D height grid",
                    StringComparison.Ordinal),
                pointOnlyC3D.FallbackSummary);

            Check(
                "grayscale palette endpoints",
                ColorNear(ViewerColorMapPalette.Grayscale(0.0), (0.0, 0.0, 0.0))
                && ColorNear(ViewerColorMapPalette.Grayscale(1.0), (1.0, 1.0, 1.0)),
                "black to white");
            Check(
                "grayscale palette midpoint",
                ColorNear(ViewerColorMapPalette.Grayscale(0.5), (0.5, 0.5, 0.5)),
                ViewerColorMapPalette.Grayscale(0.5).ToString());
            Check(
                "thermal palette stops",
                ColorNear(ViewerColorMapPalette.Thermal(0.0), (0.0, 0.0, 0.0))
                && ColorNear(ViewerColorMapPalette.Thermal(1.0 / 3.0), (1.0, 0.0, 0.0))
                && ColorNear(ViewerColorMapPalette.Thermal(2.0 / 3.0), (1.0, 1.0, 0.0))
                && ColorNear(ViewerColorMapPalette.Thermal(1.0), (1.0, 1.0, 1.0)),
                "black to red to yellow to white");
            Check(
                "palette clamps invalid values",
                ColorNear(ViewerColorMapPalette.Grayscale(double.NaN), (0.0, 0.0, 0.0))
                && ColorNear(ViewerColorMapPalette.Thermal(-1.0), (0.0, 0.0, 0.0))
                && ColorNear(ViewerColorMapPalette.Thermal(2.0), (1.0, 1.0, 1.0)),
                "invalid and out-of-range values clamped");

            var renderProxy = C3DHeightGridRenderProxy.Create(
                [GridPoint(0, 0), GridPoint(0, 1), GridPoint(1, 0), GridPoint(1, 1)],
                pointStride: 1);
            Check(
                "render proxy quad topology",
                renderProxy.TriangleCount == 2
                && renderProxy.EdgeCount == 5
                && renderProxy.GridEdgeCount == 4
                && renderProxy.InteractionGridEdgeCount == 4
                && renderProxy.CoarseInteractionGridEdgeCount == 4
                && renderProxy.SurfaceEdgeCount == 4
                && renderProxy.TriangleIndices.SequenceEqual([0, 2, 1, 1, 2, 3])
                && renderProxy.EdgeIndices.SequenceEqual([0, 2, 2, 1, 1, 0, 2, 3, 3, 1])
                && renderProxy.GridEdgeIndices.SequenceEqual([0, 1, 0, 2, 1, 3, 2, 3])
                && renderProxy.InteractionGridEdgeIndices.SequenceEqual([0, 1, 0, 2, 1, 3, 2, 3])
                && renderProxy.CoarseInteractionGridEdgeIndices.SequenceEqual([0, 1, 0, 2, 1, 3, 2, 3])
                && renderProxy.SurfaceEdgeIndices.SequenceEqual([0, 1, 2, 3, 0, 2, 1, 3]),
                $"triangles={renderProxy.TriangleCount}|edges={renderProxy.EdgeCount}|gridEdges={renderProxy.GridEdgeCount}|mediumGridEdges={renderProxy.InteractionGridEdgeCount}|coarseGridEdges={renderProxy.CoarseInteractionGridEdgeCount}|surfaceEdges={renderProxy.SurfaceEdgeCount}");
            Check(
                "render proxy unique edges",
                CountUniqueEdges(renderProxy.EdgeIndices) == renderProxy.EdgeCount,
                renderProxy.EdgeCount.ToString(CultureInfo.InvariantCulture));
            Check(
                "render proxy unique grid edges",
                CountUniqueEdges(renderProxy.GridEdgeIndices) == renderProxy.GridEdgeCount,
                renderProxy.GridEdgeCount.ToString(CultureInfo.InvariantCulture));
            Check(
                "render proxy unique surface edges",
                CountUniqueEdges(renderProxy.SurfaceEdgeIndices) == renderProxy.SurfaceEdgeCount,
                renderProxy.SurfaceEdgeCount.ToString(CultureInfo.InvariantCulture));
            var holeProxy = C3DHeightGridRenderProxy.Create(
                [GridPoint(0, 0), GridPoint(0, 1), GridPoint(1, 0)],
                pointStride: 1);
            Check(
                "render proxy does not bridge holes",
                !holeProxy.HasSurface
                && holeProxy.EdgeCount == 0
                && holeProxy.GridEdgeCount == 0
                && holeProxy.InteractionGridEdgeCount == 0
                && holeProxy.CoarseInteractionGridEdgeCount == 0
                && holeProxy.SurfaceEdgeCount == 0,
                $"triangles={holeProxy.TriangleCount}|edges={holeProxy.EdgeCount}|gridEdges={holeProxy.GridEdgeCount}|mediumGridEdges={holeProxy.InteractionGridEdgeCount}|coarseGridEdges={holeProxy.CoarseInteractionGridEdgeCount}|surfaceEdges={holeProxy.SurfaceEdgeCount}");
            var strideProxy = C3DHeightGridRenderProxy.Create(
                [GridPoint(0, 0), GridPoint(0, 2), GridPoint(2, 0), GridPoint(2, 2)],
                pointStride: 2);
            Check(
                "render proxy respects stride",
                strideProxy.TriangleCount == 2
                && strideProxy.EdgeCount == 5
                && strideProxy.GridEdgeCount == 4
                && strideProxy.InteractionGridEdgeCount == 4
                && strideProxy.CoarseInteractionGridEdgeCount == 4
                && strideProxy.SurfaceEdgeCount == 4,
                $"triangles={strideProxy.TriangleCount}|edges={strideProxy.EdgeCount}|gridEdges={strideProxy.GridEdgeCount}|mediumGridEdges={strideProxy.InteractionGridEdgeCount}|coarseGridEdges={strideProxy.CoarseInteractionGridEdgeCount}|surfaceEdges={strideProxy.SurfaceEdgeCount}");
            var sampledEdgeProxy = C3DHeightGridRenderProxy.Create(
                Enumerable.Range(0, 5)
                    .SelectMany(row => Enumerable.Range(0, 5).Select(column => GridPoint(row, column)))
                    .ToArray(),
                pointStride: 1);
            Check(
                "render proxy samples surface overlay edges",
                sampledEdgeProxy.GridEdgeCount == 40
                && sampledEdgeProxy.InteractionGridEdgeCount == 24
                && sampledEdgeProxy.CoarseInteractionGridEdgeCount == 16
                && sampledEdgeProxy.CoarseInteractionGridEdgeCount < sampledEdgeProxy.InteractionGridEdgeCount
                && sampledEdgeProxy.InteractionGridEdgeCount < sampledEdgeProxy.GridEdgeCount
                && sampledEdgeProxy.SurfaceEdgeCount == 16
                && sampledEdgeProxy.SurfaceEdgeCount < sampledEdgeProxy.GridEdgeCount,
                $"gridEdges={sampledEdgeProxy.GridEdgeCount}|mediumGridEdges={sampledEdgeProxy.InteractionGridEdgeCount}|coarseGridEdges={sampledEdgeProxy.CoarseInteractionGridEdgeCount}|mediumInterval={C3DHeightGridRenderProxy.MediumWireframeLineInterval}|coarseInterval={C3DHeightGridRenderProxy.CoarseWireframeLineInterval}|surfaceEdges={sampledEdgeProxy.SurfaceEdgeCount}|surfaceInterval={C3DHeightGridRenderProxy.SurfaceEdgeSampleInterval}");
            Check(
                "render proxy interaction edges remain unique",
                CountUniqueEdges(sampledEdgeProxy.InteractionGridEdgeIndices)
                    == sampledEdgeProxy.InteractionGridEdgeCount
                && CountUniqueEdges(sampledEdgeProxy.CoarseInteractionGridEdgeIndices)
                    == sampledEdgeProxy.CoarseInteractionGridEdgeCount,
                $"{sampledEdgeProxy.InteractionGridEdgeCount}/{sampledEdgeProxy.CoarseInteractionGridEdgeCount}");
            Check(
                "render proxy rejects duplicate cells",
                Throws<InvalidDataException>(() => C3DHeightGridRenderProxy.Create([GridPoint(0, 0), GridPoint(0, 0)], 1)),
                "duplicate rejected");
            Check(
                "render proxy rejects invalid stride",
                Throws<ArgumentOutOfRangeException>(() => C3DHeightGridRenderProxy.Create([GridPoint(0, 0)], 0)),
                "zero stride rejected");

            var rootViewModel = new MainWindowViewModel();
            Check(
                "selection session defaults",
                rootViewModel.SelectionSession.SelectedMode == "Point"
                && rootViewModel.SelectionSession.SelectedEntity == "Generated Unit Cube"
                && rootViewModel.SelectionSession.PickCoordinate == "(none)"
                && rootViewModel.SelectionSession.OverlayVisible,
                $"mode={rootViewModel.SelectedSelectionMode}|entity={rootViewModel.SelectedEntity}|pick={rootViewModel.PickCoordinate}|overlay={rootViewModel.SelectionOverlayVisible}");
            var selectionNotifications = new HashSet<string>(StringComparer.Ordinal);
            rootViewModel.PropertyChanged += (_, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.PropertyName))
                {
                    selectionNotifications.Add(args.PropertyName);
                }
            };
            rootViewModel.SelectedSelectionMode = "Box ROI";
            rootViewModel.SelectedEntity = "Verification Entity";
            rootViewModel.PickCoordinate = "X 1, Y 2, Z 3";
            rootViewModel.SelectionOverlayVisible = false;
            Check(
                "selection session root facade",
                rootViewModel.SelectionSession.SelectedMode == "Box ROI"
                && rootViewModel.SelectionSession.SelectedEntity == "Verification Entity"
                && rootViewModel.SelectionSession.PickCoordinate == "X 1, Y 2, Z 3"
                && !rootViewModel.SelectionSession.OverlayVisible
                && rootViewModel.SelectionSummary == "Box ROI: viewer state only"
                && selectionNotifications.Contains(nameof(MainWindowViewModel.SelectedSelectionMode))
                && selectionNotifications.Contains(nameof(MainWindowViewModel.SelectedEntity))
                && selectionNotifications.Contains(nameof(MainWindowViewModel.PickCoordinate))
                && selectionNotifications.Contains(nameof(MainWindowViewModel.SelectionOverlayVisible)),
                string.Join(",", selectionNotifications.OrderBy(name => name, StringComparer.Ordinal)));
            rootViewModel.SelectedSelectionMode = "Point";
            rootViewModel.SelectedEntity = "Generated Unit Cube";
            rootViewModel.PickCoordinate = "(none)";
            rootViewModel.SelectionOverlayVisible = true;
            var previewRequests = 0;
            var publishRequests = 0;
            rootViewModel.NominalActual.PreviewRequested += (_, _) => previewRequests++;
            rootViewModel.NominalActual.PublishRequested += (_, _) => publishRequests++;
            var nominalState = rootViewModel.NominalActual.State;

            rootViewModel.PointSize = 10.0;
            rootViewModel.SelectedRenderDensity = "Fast";
            Check(
                "root display compatibility facade",
                rootViewModel.PointSize == 6.0
                && rootViewModel.Display.PointSize == 6.0
                && rootViewModel.SelectedRenderDensity == "Fast"
                && rootViewModel.Display.SelectedRenderDensity == "Fast"
                && rootViewModel.C3DMaxRenderedPoints == 25000
                && rootViewModel.NominalActual.NextPreviewDisplayDensity == "Fast"
                && rootViewModel.NominalActual.NextPreviewDisplaySampleBudget == 25000,
                $"point={rootViewModel.PointSize:F1}|density={rootViewModel.SelectedRenderDensity}|budget={rootViewModel.NominalActual.NextPreviewDisplaySampleBudget}");

            rootViewModel.Display.SelectedColorMap = "Solid";
            Check(
                "root color compatibility snapshot",
                rootViewModel.Display.EffectiveSettings.ColorMap == ViewerColorMap.Solid
                && rootViewModel.SelectedColorMode == "Solid",
                $"snapshot={rootViewModel.Display.EffectiveSettings.ColorMap}|bridge={rootViewModel.SelectedColorMode}");
            Check("display change does not preview", previewRequests == 0 && rootViewModel.NominalActual.State == nominalState, $"requests={previewRequests}|state={rootViewModel.NominalActual.State}");
            Check("display change does not publish", publishRequests == 0, publishRequests.ToString(CultureInfo.InvariantCulture));

            rootViewModel.SetC3DDisplayCapabilities(surfaceGeometryAvailable: true);
            rootViewModel.UseC3DSmokeScene();
            Check("root C3D context", rootViewModel.Display.ActiveSource == "C3D height grid" && rootViewModel.Display.SelectedColorMap == "Height", rootViewModel.Display.EffectiveSummary);
            var cameraFit = CameraMath.FitPositions(
                [
                    new Vector3(-5.0f, -1.0f, -3.0f),
                    new Vector3(5.0f, 2.0f, 3.0f)
                ],
                yawDegrees: 0.0,
                pitchDegrees: 80.0,
                fieldOfViewDegrees: 45.0,
                viewportAspect: 1.5);
            rootViewModel.ApplyC3DCameraFit(
                cameraFit.Target,
                cameraFit.Distance,
                useTopInspectionView: true,
                "verification fit");
            Check(
                "C3D bounds fit owns camera state",
                rootViewModel.YawDegrees == 0.0
                && rootViewModel.PitchDegrees == 80.0
                && rootViewModel.CameraTargetX == 0.0
                && Math.Abs(rootViewModel.CameraTargetY - 0.5) < 0.000001
                && rootViewModel.CameraTargetZ == 0.0
                && double.IsFinite(rootViewModel.CameraDistance)
                && rootViewModel.CameraDistance > 0.0,
                rootViewModel.BottomStatus);
            for (var index = 0; index < 80; index++)
            {
                rootViewModel.ZoomCamera(0.80);
            }

            Check(
                "C3D close zoom exceeds legacy clamp",
                rootViewModel.CameraDistance < 2.4
                && rootViewModel.CameraDistance >= Math.Max(0.05, cameraFit.Distance * 0.01),
                rootViewModel.CameraDistance.ToString("F6", CultureInfo.InvariantCulture));
            var savedPerspectiveDistance = rootViewModel.CameraDistance;
            var topViewRequestCount = 0;
            rootViewModel.TopViewRequested += (_, _) => topViewRequestCount++;
            rootViewModel.TopViewCommand.Execute(null);
            Check(
                "Top command routes without execution",
                topViewRequestCount == 1
                && rootViewModel.ProjectionMode == ViewerProjectionMode.Perspective,
                $"requests={topViewRequestCount}|projection={rootViewModel.ProjectionMode}");
            var orthographicFit = CameraMath.FitOrthographicPositions(
                [
                    new Vector3(-5.0f, -1.0f, -3.0f),
                    new Vector3(5.0f, 2.0f, 3.0f)
                ],
                yawDegrees: 0.0,
                pitchDegrees: 90.0,
                viewportAspect: 1.5);
            rootViewModel.ApplyTopOrthographicFit(
                orthographicFit.Target,
                orthographicFit.Height,
                orthographicFit.Distance,
                "verification Top fit");
            Check(
                "Top orthographic state is explicit",
                rootViewModel.IsTopOrthographicView
                && !rootViewModel.IsPerspectiveView
                && rootViewModel.CameraSession.HasSavedPerspective
                && rootViewModel.YawDegrees == 0.0
                && rootViewModel.PitchDegrees == 90.0
                && rootViewModel.OrthographicHeight > 0.0
                && rootViewModel.BottomStatus.Contains("Top orthographic", StringComparison.Ordinal),
                rootViewModel.BottomStatus);
            var topHeightBeforeZoom = rootViewModel.OrthographicHeight;
            rootViewModel.ZoomCamera(0.80);
            Check(
                "Top zoom changes orthographic height only",
                rootViewModel.OrthographicHeight < topHeightBeforeZoom
                && Math.Abs(rootViewModel.CameraDistance - orthographicFit.Distance) < 0.000001,
                $"height={rootViewModel.OrthographicHeight:F6}|distance={rootViewModel.CameraDistance:F6}");
            rootViewModel.RestorePerspectiveView("verification perspective restore");
            Check(
                "Perspective restore returns prior camera",
                rootViewModel.IsPerspectiveView
                && !rootViewModel.IsTopOrthographicView
                && rootViewModel.CameraSession.HasSavedPerspective
                && Math.Abs(rootViewModel.CameraDistance - savedPerspectiveDistance) < 0.000001,
                rootViewModel.BottomStatus);
            var geometryRevision = rootViewModel.DisplaySettingsRevision;
            rootViewModel.Display.SelectedGeometryStyle = "Surface + Edges";
            Check(
                "root geometry snapshot bridge",
                rootViewModel.Display.EffectiveSettings.GeometryStyle == ViewerGeometryStyle.SurfaceWithEdges
                && rootViewModel.SelectedGeometryStyle == "Surface + Edges"
                && rootViewModel.DisplaySettingsRevision == geometryRevision + 1,
                $"snapshot={rootViewModel.Display.EffectiveSettings.GeometryStyle}|bridge={rootViewModel.SelectedGeometryStyle}|revision={rootViewModel.DisplaySettingsRevision}");
            rootViewModel.SelectedColorMode = "RGB";
            Check("root unavailable color fallback", rootViewModel.SelectedColorMode == "Height" && rootViewModel.Display.FallbackApplied, rootViewModel.Display.FallbackSummary);

            var flatnessReference = new[]
            {
                new HeightFieldPlaneSample(new Vector3(0.0f, 0.0f, 0.0f), 0.0),
                new HeightFieldPlaneSample(new Vector3(1.0f, 0.0f, 0.0f), 0.0),
                new HeightFieldPlaneSample(new Vector3(0.0f, 0.0f, 1.0f), 0.0),
            };
            var flatnessEvaluation = PlaneFlatnessRule.Evaluate(new PlaneFlatnessRuleInput(
                MainWindowViewModel.C3DEntityId,
                flatnessReference,
                [.. flatnessReference, new HeightFieldPlaneSample(new Vector3(0.5f, 0.1f, 0.5f), 0.1)],
                0.2,
                "model"));
            rootViewModel.SetPlaneFlatnessPreview(flatnessEvaluation);
            Check(
                "root C3D result selects deviation",
                rootViewModel.PlaneFlatnessVisible
                && rootViewModel.SelectedColorMode == "Deviation"
                && rootViewModel.Display.EffectiveSettings.ColorMap == ViewerColorMap.Deviation,
                rootViewModel.Display.EffectiveSummary);

            rootViewModel.SetLazDisplayCapabilities(sourceColorAvailable: true);
            rootViewModel.UseLazPointSmokeScene();
            Check("root point-cloud context", rootViewModel.Display.ActiveSource == "LAZ/LAS point cloud" && rootViewModel.SelectedColorMode == "RGB", rootViewModel.Display.EffectiveSummary);
            Check("root point-cloud typed color", rootViewModel.Display.EffectiveSettings.ColorMap == ViewerColorMap.Rgb, rootViewModel.Display.EffectiveSettings.ToString());
            rootViewModel.SelectedColorMode = "Deviation";
            Check("root deviation guard mode", rootViewModel.SelectedColorMode == "RGB", rootViewModel.Display.EffectiveSummary);
            Check("root deviation guard status", rootViewModel.ViewerStatus.Contains("Deviation requires an active result", StringComparison.Ordinal), rootViewModel.ViewerStatus);

            rootViewModel.SetImportedMeshDisplayCapabilities(sourceColorAvailable: true);
            rootViewModel.UseGlbSmokeScene();
            Check("root mesh context", rootViewModel.Display.ActiveSource == "Imported triangle mesh", rootViewModel.Display.EffectiveSummary);
            Check("root mesh effective settings", rootViewModel.Display.SelectedGeometryStyle == "Surface + Edges" && rootViewModel.SelectedColorMode == "Source", rootViewModel.Display.EffectiveSummary);
            var meshGeometryRevision = rootViewModel.DisplaySettingsRevision;
            rootViewModel.Display.SelectedGeometryStyle = "Wireframe";
            Check(
                "root mesh geometry snapshot bridge",
                rootViewModel.Display.EffectiveSettings.GeometryStyle == ViewerGeometryStyle.Wireframe
                && rootViewModel.SelectedGeometryStyle == "Wireframe"
                && rootViewModel.DisplaySettingsRevision == meshGeometryRevision + 1,
                $"snapshot={rootViewModel.Display.EffectiveSettings.GeometryStyle}|bridge={rootViewModel.SelectedGeometryStyle}|revision={rootViewModel.DisplaySettingsRevision}");
            Check("mesh display change does not preview", previewRequests == 0 && rootViewModel.NominalActual.State == nominalState, $"requests={previewRequests}|state={rootViewModel.NominalActual.State}");
            Check("mesh display change does not publish", publishRequests == 0, publishRequests.ToString(CultureInfo.InvariantCulture));
            Check("property notifications", propertyChanges > 0, propertyChanges.ToString(CultureInfo.InvariantCulture));

            var origin = Vector3.Zero;
            var frontEye = CameraMath.OrbitCameraPosition(origin, 0.0, 0.0, 5.0);
            Check(
                "camera target projects to viewport center",
                CameraMath.TryProjectWorldPositionToScreen(
                    origin,
                    800,
                    600,
                    45,
                    frontEye,
                    origin,
                    out var projectedOrigin)
                && Math.Abs(projectedOrigin.X - 400.0) < 0.001
                && Math.Abs(projectedOrigin.Y - 300.0) < 0.001,
                projectedOrigin.ToString(CultureInfo.InvariantCulture));
            var topEye = CameraMath.OrbitCameraPosition(origin, 0.0, 90.0, 10.0);
            Check(
                "Top orthographic target projects to viewport center",
                CameraMath.TryProjectWorldPositionToOrthographicScreen(
                    origin,
                    800,
                    600,
                    orthographicHeight: 8.0,
                    topEye,
                    origin,
                    out var projectedTopOrigin)
                && Math.Abs(projectedTopOrigin.X - 400.0) < 0.001
                && Math.Abs(projectedTopOrigin.Y - 300.0) < 0.001,
                projectedTopOrigin.ToString(CultureInfo.InvariantCulture));
            var topPickRay = CameraMath.CreateOrthographicPickRay(
                new System.Windows.Point(400.0, 300.0),
                800,
                600,
                orthographicHeight: 8.0,
                topEye,
                origin);
            Check(
                "Top orthographic center pick ray is stable",
                Math.Abs(topPickRay.origin.X) < 0.0001
                && Math.Abs(topPickRay.origin.Z) < 0.0001
                && VectorNear(new Vector2(topPickRay.direction.X, topPickRay.direction.Z), Vector2.Zero)
                && topPickRay.direction.Y < -0.999f,
                $"origin={topPickRay.origin}|direction={topPickRay.direction}");
            Check(
                "Top orthographic fit respects viewport aspect",
                Vector3.Distance(orthographicFit.Target, new Vector3(0.0f, 0.5f, 0.0f)) < 0.0001f
                && orthographicFit.Height > 7.0
                && orthographicFit.Height < 8.0
                && orthographicFit.Distance > orthographicFit.Height,
                orthographicFit.ToString());
            var frontX = CameraMath.ProjectWorldDirectionToScreen(Vector3.UnitX, frontEye, origin);
            var frontY = CameraMath.ProjectWorldDirectionToScreen(Vector3.UnitY, frontEye, origin);
            var frontZ = CameraMath.ProjectWorldDirectionToScreen(Vector3.UnitZ, frontEye, origin);
            Check(
                "orientation triad front camera",
                VectorNear(frontX, new Vector2(1.0f, 0.0f))
                && VectorNear(frontY, new Vector2(0.0f, -1.0f))
                && VectorNear(frontZ, Vector2.Zero),
                $"X={frontX}|Y={frontY}|Z={frontZ}");

            var sideEye = CameraMath.OrbitCameraPosition(origin, 90.0, 0.0, 5.0);
            var sideX = CameraMath.ProjectWorldDirectionToScreen(Vector3.UnitX, sideEye, origin);
            var sideY = CameraMath.ProjectWorldDirectionToScreen(Vector3.UnitY, sideEye, origin);
            var sideZ = CameraMath.ProjectWorldDirectionToScreen(Vector3.UnitZ, sideEye, origin);
            Check(
                "orientation triad side camera",
                VectorNear(sideX, Vector2.Zero)
                && VectorNear(sideY, new Vector2(0.0f, -1.0f))
                && VectorNear(sideZ, new Vector2(-1.0f, 0.0f)),
                $"X={sideX}|Y={sideY}|Z={sideZ}");

            var pitchedEye = CameraMath.OrbitCameraPosition(origin, 0.0, 45.0, 5.0);
            var pitchedX = CameraMath.ProjectWorldDirectionToScreen(Vector3.UnitX, pitchedEye, origin);
            var pitchedY = CameraMath.ProjectWorldDirectionToScreen(Vector3.UnitY, pitchedEye, origin);
            var pitchedZ = CameraMath.ProjectWorldDirectionToScreen(Vector3.UnitZ, pitchedEye, origin);
            Check(
                "orientation triad pitched camera",
                VectorNear(pitchedX, new Vector2(1.0f, 0.0f))
                && VectorNear(pitchedY, new Vector2(0.0f, -0.70710677f))
                && VectorNear(pitchedZ, new Vector2(0.0f, 0.70710677f)),
                $"X={pitchedX}|Y={pitchedY}|Z={pitchedZ}");

            var originalLanguage = OpenVisionLanguageService.CurrentLanguage;
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
            Check(
                "viewer localization English runtime",
                ViewerLocalization.Shared.LocalizeRuntimeText(
                    "Ready: generated cube and point cloud loaded")
                == "Ready: generated cube and point cloud loaded",
                ViewerLocalization.Shared.LocalizeRuntimeText(
                    "Ready: generated cube and point cloud loaded"));
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
            var koreanRuntimeStatus = ViewerLocalization.Shared.LocalizeRuntimeText(
                "Ready: generated cube and point cloud loaded");
            Check(
                "viewer localization Korean runtime",
                koreanRuntimeStatus == "준비: 생성된 큐브와 포인트 클라우드 로드됨",
                koreanRuntimeStatus);
            Check(
                "viewer localization preserves technical values",
                ViewerLocalization.Shared.LocalizeRuntimeText(
                    "Transform: Identity | T(0.000, 0.000, 0.000) | R(0.0, 0.0, 0.0) | S 1.000")
                == "변환: 항등 | T(0.000, 0.000, 0.000) | R(0.0, 0.0, 0.0) | S 1.000",
                ViewerLocalization.Shared.LocalizeRuntimeText(
                    "Transform: Identity | T(0.000, 0.000, 0.000) | R(0.0, 0.0, 0.0) | S 1.000"));
            Check(
                "viewer localization geometry display only",
                ViewerLocalization.Shared.LocalizeRuntimeText("Wireframe") == "와이어프레임"
                && c3dViewModel.SelectedGeometryStyle == "Points",
                $"display={ViewerLocalization.Shared.LocalizeRuntimeText("Wireframe")}|stored={c3dViewModel.SelectedGeometryStyle}");
            Check(
                "viewer localization color-map display only",
                ViewerLocalization.Shared.LocalizeRuntimeText("Height") == "높이"
                && c3dViewModel.SelectedColorMap == "Deviation",
                $"display={ViewerLocalization.Shared.LocalizeRuntimeText("Height")}|stored={c3dViewModel.SelectedColorMap}");
            Check(
                "viewer localization expert status",
                ViewerLocalization.Shared.LocalizeRuntimeText(
                    "Viewer hosted | recipe comparison matched")
                == "뷰어 연결됨 | 레시피 비교 일치",
                ViewerLocalization.Shared.LocalizeRuntimeText(
                    "Viewer hosted | recipe comparison matched"));
            Check(
                "viewer localization evidence summary",
                ViewerLocalization.Shared.LocalizeRuntimeText(
                    "Runner/UI contract matched | Status: Fail | Key metric: Peak 1.0 | Evidence: Matched")
                == "Runner/UI 계약 일치 | 상태: 실패 | 핵심 측정값: Peak 1.0 | 증거: 일치",
                ViewerLocalization.Shared.LocalizeRuntimeText(
                    "Runner/UI contract matched | Status: Fail | Key metric: Peak 1.0 | Evidence: Matched"));
            Check(
                "viewer localization mode formatter",
                ViewerLocalization.Shared.LocalizeRuntimeText("Point", "Mode") == "모드: 포인트",
                ViewerLocalization.Shared.LocalizeRuntimeText("Point", "Mode"));
            var localizedNotifications = new HashSet<string>(StringComparer.Ordinal);
            rootViewModel.PropertyChanged += (_, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.PropertyName))
                {
                    localizedNotifications.Add(args.PropertyName);
                }
            };
            rootViewModel.RefreshLocalizedPresentation();
            Check(
                "viewer localization refresh notifications",
                localizedNotifications.Contains(nameof(MainWindowViewModel.ViewerStatus))
                && localizedNotifications.Contains(nameof(MainWindowViewModel.BottomStatus))
                && localizedNotifications.Contains(nameof(MainWindowViewModel.CoordinateFrameSummary))
                && localizedNotifications.Contains(nameof(MainWindowViewModel.SelectedRenderDensity)),
                string.Join(",", localizedNotifications.OrderBy(name => name, StringComparer.Ordinal)));
            OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);

            summary = $"Display-settings ViewModel verification: Pass ({passed} checks)";
            lines.Add(summary);
            WriteReport(reportPath, lines);
            return true;
        }
        catch (Exception exception)
        {
            summary = $"Display-settings ViewModel verification: Fail after {passed} checks: {exception.Message}";
            lines.Add(summary);
            WriteReport(reportPath, lines);
            return false;
        }

        void Check(string name, bool condition, string detail)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"{name}: {detail}");
            }

            passed++;
            lines.Add($"PASS|{name}|{detail}");
        }
    }

    private static bool Sequence(IReadOnlyList<string> actual, params string[] expected) =>
        actual.SequenceEqual(expected, StringComparer.Ordinal);

    private static bool ColorNear(
        (double R, double G, double B) actual,
        (double R, double G, double B) expected,
        double tolerance = 1e-12) =>
        Math.Abs(actual.R - expected.R) <= tolerance
        && Math.Abs(actual.G - expected.G) <= tolerance
        && Math.Abs(actual.B - expected.B) <= tolerance;

    private static bool VectorNear(Vector2 actual, Vector2 expected, float tolerance = 0.00001f) =>
        Vector2.Distance(actual, expected) <= tolerance;

    private static HeightGridPoint GridPoint(int row, int column) =>
        new(new Vector3(column, 0.0f, row), 0.5, 0.0, 1.0f, row, column);

    private static int CountUniqueEdges(IReadOnlyList<int> edgeIndices)
    {
        var edges = new HashSet<(int Minimum, int Maximum)>();
        for (var index = 0; index < edgeIndices.Count; index += 2)
        {
            edges.Add((
                Math.Min(edgeIndices[index], edgeIndices[index + 1]),
                Math.Max(edgeIndices[index], edgeIndices[index + 1])));
        }

        return edges.Count;
    }

    private static bool Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }

    private static void WriteReport(string reportPath, IEnumerable<string> lines)
    {
        var fullPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllLines(fullPath, lines);
    }
}
