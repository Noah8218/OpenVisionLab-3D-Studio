# OpenVisionLab 3D Studio

OpenVisionLab 3D Studio is an early-stage desktop project for rule-based 3D inspection.

The goal is to build a practical 3D vision workbench where users can load 3D data, inspect geometry, visualize measurements and result overlays, and create repeatable validation recipes.

## Status

This repository is under active development and is not production-ready yet.

The current focus is the 3D viewer foundation:

- SharpGL/WPF based local viewer
- Camera control for orbit, pan, zoom, reset, and fit-to-view
- Basic mesh and point-cloud visualization
- Entity visibility and selection
- Measurement and inspection result overlays
- Early recipe workflow for repeatable 3D rule checks

## Direction

Development is intentionally viewer-first. The viewer must become reliable before the project expands into deeper 3D inspection algorithms.

The intended product direction is:

1. Complete the 3D viewer foundation.
2. Add clear 3D entity, layer, measurement, and result data models.
3. Build simple rule-based 3D inspection tools.
4. Save and replay validation recipes.
5. Grow into a practical local 3D vision inspection workbench.

Large production-line integrations such as camera control, PLC, robot, cloud, or deployment management are not the current focus.
