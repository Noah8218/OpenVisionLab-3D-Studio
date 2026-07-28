# OpenVisionLab 3D Commercial Viewer Review

Updated: 2026-07-07

## Purpose

This review resets the 3D Viewer target against commercial 3D vision and metrology software. The goal is not to copy a vendor UI. The goal is to make OpenVisionLab 3D Studio's SharpGL viewer good enough to support rule-based 3D inspection workflows before adding more algorithms.

## Sources Checked

Official or vendor-owned sources were preferred:

- LMI GoPxL inspection software and UI:
  - https://lmi3d.com/gopxl-inspection-software/
  - https://lmi3d.com/gopxl-modern-user-interface/
  - https://lmi3d.com/gopxl-multi-dimensional-measurement-capability/
  - https://lmi3d.com/blog/surface-track-inspection/
- Cognex In-Sight 3D / VisionPro:
  - https://docs.cognex.com/is3d_2410/web/EN/InSight_Sheet3D/Content/Topics/GettingStarted/getstarted_sheet.htm
  - https://docs.cognex.com/is3d_120/web/EN/Help_IS3D/Content/Topics/Spreadsheet/3DVisionTools/3D_Extract_ExtractBlobMax3D.htm
  - https://docs.cognex.com/vpronine_924/support/EN/VisionProWith3DExpress.pdf
  - https://docs.cognex.com/vpro_0x091700/web/EN/help/html/b922e702-61a7-47d5-97f8-f5ca5028b07b.htm
- MVTec HALCON:
  - https://www.mvtec.com/knowledge-base/technologies/3d-vision
  - https://www.mvtec.com/doc/halcon/12/en/disp_object_model_3d.html
  - https://www.mvtec.com/doc/halcon/13/en/select_object_model_3d.html
  - https://www.mvtec.com/knowledge-base/news/article/optimize-your-3d-data-for-surface-based-3d-matching-with-mvtec-halcon
- Keyence LJ / 3D inspection software:
  - https://www.keyence.com/products/measure/laser-2d/lj-x8000/
  - https://www.keyence.com/products/measure/laser-2d/lj_developer/
  - https://www.keyence.com/products/measure/applications/profile-measurement/3d-profile-measurement.jsp
  - https://www.keyence.com/products/vision/applications/3d-inspection-software.jsp
- Zebra Aurora Vision / Design Assistant:
  - https://docs.adaptive-vision.com/5.6/studio/user_interface/WorkingWith3DData.html
  - https://www.zebra.com/us/en/vision-academy/aurora-design-assistant/3d-aurora-design-assistant.html
  - https://www.zebra.com/us/en/software/machine-vision-and-fixed-industrial-scanning-software/aurora-design-assistant.html
  - https://www.zebra.com/us/en/products/oem/software/aurora-vision-studio.html
- Metrology-oriented references:
  - https://www.polyworks.com/en-us/products/polyworks-inspector
  - https://hexagon.com/products/geomagic-control-x
  - https://www.creaform3d.com/en/products/software/creaform-metrology-suite/inspection-software-module

## What Commercial Tools Actually Do With The 3D Viewer

| Pattern | Commercial evidence | Meaning for OpenVisionLab 3D |
| --- | --- | --- |
| Viewer is the inspection work surface, not a passive renderer. | LMI GoPxL describes connecting sensors, acquiring scans, aligning, applying built-in measurements, and outputting decisions from the same web UI. | Keep building the Viewer as the main review surface for source data, tools, overlays, metrics, and recipe replay. |
| 3D and 2D views coexist. | GoPxL applies tools to 3D shape and 2D intensity data. Zebra shows profiles, depth maps, point clouds, and 3D previews. | Add height map/profile views later. The 3D viewport alone is not enough for inspection. |
| Built-in tools are concrete and measurement-first. | Keyence lists position adjustment, dimensional measurement, appearance inspection, filters, image composition, and 3D rendering. Cognex exposes 3D tools through spreadsheet functions such as extracting 3D blobs relative to reference geometry. | Tool authoring should start with plane/height/volume/profile/ROI tools, not generic plugin architecture. |
| Color maps and tolerances are first-class. | Metrology tools such as PolyWorks, Geomagic Control X, and Creaform inspection software center on scan-to-CAD/scan comparison, alignment, and deviation color maps. | Result overlays need adjustable color scale, tolerance bands, fail markers, and persistent snapshots. |
| Alignment and coordinate systems are product features. | LMI emphasizes sensor alignment; PolyWorks and Creaform emphasize alignment before comparison; Cognex notes strict 3D coordinate-space requirements. | Units, transforms, frame identity, and alignment state must be visible and serializable before broader algorithms. |
| Cross sections and profiles are core inspection tools. | Cognex VisionPro 3DExpress mentions planar surface information, height/volume calculations, and cross-section analysis. Keyence profile measurement targets height, volume, distance, and angle. | Add section/profile extraction before complex CAD or AI work. |
| Viewer interaction must be production-stable. | Zebra documents rotation, zoom, pan, point size, color scale, surface visibility, and separate 3D preview behavior. Cognex documents native 3D display acceleration limitations. | We must keep orbit/pan/zoom/fit/pick stable, and smoke-test Shell/full-window captures separately from Viewer-only captures. |
| Development and runtime modes are separated. | Cognex ties spreadsheet tool results to WebHMI. Zebra Design Assistant uses flowcharts and custom web operator interfaces. | Keep recipe edit/preview/run separate from runtime/operator review. Do not collapse authoring and production evidence. |
| Reports are part of inspection, not an afterthought. | PolyWorks highlights inspection reports and 3D control review; metrology workflows use snapshots, deviation maps, dimensions, and annotations. | Our comparison pane is the right direction, but it needs persisted snapshots and a real run-history list after save/edit exists. |

## Viewer Capability Target

### Minimum Commercial-Parity Viewer Gate

Before broad 3D algorithm work, the Viewer should support:

1. Source entities and result entities with explicit visibility.
2. Stable camera: orbit, pan, zoom, fit all, fit selection, reset.
3. Picking with source/entity identity and model coordinates.
4. Color modes: height, deviation, status, intensity when available.
5. Color scale legend with tolerance thresholds.
6. ROI tools: point, box, section/profile line, plane/region.
7. Measurement overlays: distance, height delta, area/volume/profile summary.
8. Result overlays: pass/fail markers, tolerance band, color map, profile line.
9. Units and transforms visible in the UI.
10. Recipe load, preview, explicit publish, replay comparison.
11. Shell-level evidence view: UI contract, runner report, screenshot snapshot.
12. Smoke coverage for Viewer-only and Shell-wide captures.

### Not Required Yet

These are commercial-platform features, but should remain out of early scope:

- Live sensor configuration and triggering.
- PLC/I/O, robot, or factory network communication.
- Multi-sensor calibration wizard.
- CAD kernel and GD&T authoring.
- AI anomaly training.
- Runtime HMI builder.

## Current Gap Against Commercial Baseline

| Area | Current status | Gap |
| --- | --- | --- |
| 3D rendering | SharpGL viewer renders generated geometry and C3D height-grid sample. | Needs color scale legend, point size/render controls, and denser view-state smoke coverage. |
| Inspection workflow | Recipe load, preview, publish, runner compare exist for one C3D height rule. | Needs visible save/edit path and run-history list. |
| Measurement tools | Synthetic measurement and C3D height deviation exist. | Needs real section/profile extraction and ROI-driven metrics. |
| Result visualization | Basic result overlay exists. | Needs adjustable deviation color map/tolerance legend and snapshot evidence. |
| Data representation | Height-grid point cloud exists. | Needs paired 2D height map/profile view. |
| Coordinate discipline | Units/entities/layers are started. | Needs transform/alignment state visible and serialized. |
| Shell workflow | Viewer hosted with recipe comparison pane. | Needs recipe editor/save command and persisted run records. |

## Development Direction

The Viewer should now evolve in this order:

1. Finish visible recipe save/edit for the current C3D height deviation rule.
2. Add a color scale/tolerance legend for deviation overlays.
3. Add a real section/profile tool over the C3D height grid.
4. Add a compact 2D height-map/profile pane linked to the 3D selection.
5. Add run-history records only after at least two user-created recipe executions exist.
6. Add transform/alignment state before CAD or multi-sensor work.

## Product Differentiation

OpenVisionLab 3D Studio can be better than a typical commercial workflow in one specific way: every viewer action, rule result, correction, and replay can be made audit-friendly for LLM-assisted recipe authoring. The target is not more buttons than commercial products. The target is a clearer evidence loop:

```text
3D sample
  -> visible entity/layer
  -> explicit ROI/measurement/rule
  -> metrics + overlays + screenshot
  -> saved recipe
  -> runner replay
  -> UI/runner comparison
  -> LLM-readable correction evidence
```

That is the product direction to preserve while raising the Viewer to commercial inspection-workbench expectations.
