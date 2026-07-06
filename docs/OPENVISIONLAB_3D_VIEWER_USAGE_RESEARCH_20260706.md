# OpenVisionLab 3D Viewer Usage Research

Checked: 2026-07-06

This document records how commercial and open 3D vision tools use a 3D viewer, and what that implies for OpenVisionLab 3D Studio.

## 1. Product Thesis

The viewer is not a passive model window. In practical 3D vision software, the viewer is the operator workbench for acquisition tuning, region teaching, alignment, measurement, deviation explanation, tolerance review, and reporting.

For OpenVisionLab 3D Studio, the SharpGL viewer should become the place where rule-based inspection is made visible and auditable:

```text
source scan/reference
  -> visible entity/layer
  -> selectable point/region/section
  -> measurement or comparison tool
  -> color/overlay result
  -> metric table
  -> OK/NG reason
  -> report snapshot
```

## 2. Observed Usage Patterns

### 2.1 Acquisition And Data Quality Viewer

Zivid Studio uses its GUI to capture and visualize point clouds, color images, depth maps, SNR maps, and normal maps, and to evaluate data quality and capture settings. Cognex VisionPro also treats acquired point clouds as first-class data that can be viewed in QuickBuild tools or a Visual Studio display control.

Implication:

- OpenVisionLab 3D needs multiple color modes, not only shaded triangles.
- First modes should be `Solid`, `Height`, and `Deviation`.
- Later modes should include `Intensity`, `Depth`, `Normal`, and `Confidence/SNR` when sensor data exists.

### 2.2 Region Teaching And Tool Setup

Cognex VisionPro's 3D PatMax edit control uses a floating 3D display to define train and search regions. LMI Gocator training materials show the 3D sensor UI being used for scan alignment, width/height measurement tools, and measurement anchoring.

Implication:

- Selection is a core viewer feature, not a convenience feature.
- The first selection types should be point pick, box ROI, and section plane.
- Rule parameters should be teachable from the viewer, then stored in the recipe.

### 2.3 Alignment Workspace

CloudCompare's comparison workflow first roughly registers entities, then refines alignment with point-pair picking and ICP before distance comparison. LMI also presents 3D scan alignment as a basic sensor UI workflow.

Implication:

- The viewer must show coordinate frames, transforms, and reference/current entity relationships.
- Manual transform and point-pair anchors should come before advanced registration.
- ICP should be added only after point-cloud entities and transform contracts are stable.

### 2.4 Measurement Workbench

Commercial 3D inspection tools repeatedly expose practical measurements: width, height, distance, angle, dimensioning, presence, position, height, volume, and tolerance checks.

Implication:

- The first rules should be simple geometric rules with obvious visual proof.
- Start with distance, height, bounding size, plane residual, and point count.
- Every rule needs a visible overlay and a metric row.

### 2.5 Deviation And Scan-To-Reference Review

Geomagic Control X highlights scan-to-CAD or scan-to-scan comparison, deviation location, annotations, dimensioning, and reporting. CloudCompare represents distance results as scalar fields that can be displayed as colors and filtered or inspected.

Implication:

- OpenVisionLab 3D needs a `DeviationScalarField` concept early.
- The viewer must support heatmap coloring, tolerance thresholds, and selected worst points.
- A deviation result is not only a number; it is a color layer plus summary metrics.

### 2.6 Presence, Fill, Bin, And Robot Workflows

SICK Nova and related 3D presence inspection examples focus on completeness, fill level, bin/tote inspection, object presence, position, height, volume, and robot palletising. MVTec presents 3D vision as capture, evaluation, and processing of sensor 3D data for inspection and automation, including bin-picking, 3D matching, reconstruction, calibration, and gripping point detection.

Implication:

- The viewer must handle point clouds and range-derived surfaces, not only CAD meshes.
- Coordinate frames and calibration are future core concepts, even if hardware integration is deferred.
- For robot-facing workflows, the viewer must expose pose, normal, and gripping/placement frame overlays.

### 2.7 Reporting And Replay Viewer

Geomagic Control X emphasizes inspection reports. Cognex supports saving/loading 3D image database data for reuse in vision applications.

Implication:

- Screenshot capture is already the correct MVP habit.
- Reports should include source identity, transform, rule parameters, metric table, pass/fail reason, and viewer snapshot.
- Saved scene/result data should replay without the original UI session.

## 3. OpenVisionLab 3D Viewer Roles

The viewer should be developed as these roles, in this order:

1. `Inspection View`: render source/reference/result layers with visibility control.
2. `Teaching View`: pick points, boxes, regions, and section planes for rule setup.
3. `Measurement View`: draw anchors, dimensions, and metric overlays.
4. `Alignment View`: show current/reference transforms and manual alignment handles.
5. `Deviation View`: render scalar-field heatmaps and tolerance failures.
6. `Evidence View`: capture snapshots and report-ready annotations.

## 4. Viewer-First Development Track

Do not jump directly to CAD import, camera SDK integration, or 3D algorithms. Complete the viewer first so later rule results have a reliable visual surface.

Recommended viewer slice:

1. Add a generated point-cloud entity.
2. Add per-point color rendering in SharpGL.
3. Add color modes: `Solid`, `Height`, `Deviation`.
4. Add point, box ROI, and section-plane selection state.
5. Render reusable measurement/result overlay primitives.
6. Capture screenshot smoke artifacts from mesh and point-cloud viewer states.

After the viewer gate passes, the first algorithm slice should use synthetic data whose expected result is known:

1. Generate a reference plane or box and a scanned point cloud with a known defect.
2. Implement one simple distance-to-plane or height tolerance rule.
3. Render failed points as a heatmap or highlighted overlay.
4. Show OK/NG, min/max/mean deviation, failed count, and tolerance.
5. Capture a screenshot smoke artifact from that result.

This proves the most important product question after the viewer is stable: can the product explain a rule-based 3D inspection result clearly enough for an operator to trust it?

## 5. Core Concepts To Add

```text
Scene
EntityLayer
  MeshEntity
  PointCloudEntity
  ReferenceEntity
Transform3D
UnitSystem
SelectionRegion
  PickedPoint
  BoxRoi
  SectionPlane
Metric
ToolResult
Overlay
  MeasurementOverlay
  DeviationOverlay
  CoordinateFrameOverlay
ScalarField
  HeightScalarField
  DeviationScalarField
ReportSnapshot
```

## 6. Non-Goals For The Next Slice

- No live camera SDK.
- No PLC or robot connection.
- No full STEP/IGES/CAD topology.
- No broad Open3D/PCL/VTK integration.
- No automatic ICP until manual transform and point-pair concepts exist.
- No AI judgment before deterministic rule evidence exists.

## 7. Source Notes

| Source | Viewer usage lesson |
| --- | --- |
| [Cognex VisionPro point clouds](https://docs.cognex.com/vpro_0x091400/web/EN/help/html/EF40E637-216A-4235-A828-EA470AAED570.htm) | Point clouds are viewed in tool setup UIs and in application display controls; train/search regions are taught in 3D. |
| [LMI Gocator 3D profile measurement training](https://lmi3d.com/video/gocator-training-series-part-1-introduction-to-3d-profile-measurement/) | Sensor UI is used for setup, scan alignment, width/height tools, and measurement anchoring. |
| [MVTec 3D Vision](https://www.mvtec.com/knowledge-base/technologies/3d-vision) | 3D vision supports inspection and automation workflows such as quality control, bin-picking, matching, reconstruction, calibration, and gripping point detection. |
| [Zivid Studio User Guide](https://www.zivid.com/hubfs/User%20guides%20and%20datasheets/Zivid%20Studio%20User%20Guide%20SDK%202.17%20-%20English.pdf) | A 3D camera GUI visualizes point clouds plus diagnostic maps and helps tune capture/data quality. |
| [Hexagon Geomagic Control X](https://hexagon.com/products/geomagic-control-x) | Scan-based inspection uses device integration, scan comparison, deviation location, annotations, dimensioning, automation, and reports. |
| [CloudCompare comparison workflow](https://www.cloudcompare.org/doc/wiki/index.php/How_to_compare_two_3D_entities) | Comparison requires rough alignment, fine registration, and cloud/cloud or cloud/mesh distance computation. |
| [Open3D distance queries](https://www.open3d.org/docs/latest/tutorial/geometry/distance_queries.html) | Mesh distance, signed distance, and occupancy queries are standard building blocks for later geometry validation. |
| [Point Cloud Library](https://pointclouds.org/) | Point-cloud processing is a broad domain with filtering, registration, segmentation, surface, recognition, I/O, and visualization modules. |
| [SICK Nova / Visionary-T Mini presence inspection coverage](https://www.imveurope.com/article/sick-3d-camera-simplifies-machine-vision-tasks) | Practical 3D inspection often means presence, completeness, fill level, position, height, volume, and tolerance checks. |
