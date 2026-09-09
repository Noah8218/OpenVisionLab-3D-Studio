# OpenVisionLab 3D Refactor Batch 0.5.5-dev

Date: 2026-09-10
Status: Complete

## Versioned scope

- Product version: `0.5.5-dev`
- Branch: `main`
- Source commit: this versioned batch is recorded in Git history; the exact
  pushed commit is recorded in the completion evidence for this change.
- Change type: compatible internal structure refactor
- Viewer Host API: `1.1` unchanged
- Viewer, inspection, measurement, recipe, Run Record, and persisted-storage
  contracts remain unchanged.

## Included refactoring

- Moved `WorkbenchViewerDisplayCoordinator` from
  `src/OpenVisionLab.ThreeD.Shell/Views/Workbench` to
  `src/OpenVisionLab.ThreeD.Shell/Coordination/Workbench`.
- Moved `WorkbenchViewerTeachingCoordinator` to the same coordination module.
- Updated namespaces and the lifecycle caller so the two coordinators are
  owned by the Shell coordination boundary.
- Kept the actual WPF Workbench Views in `Views/Workbench`; no placeholder
  folders, forwarding partials, or new abstraction layers were introduced.

## Ownership and call path

`MainWindow` creates the two coordinators and passes them to the existing Shell
lifecycle/display flow. `ShellWorkbenchLifecycleController` owns the teaching
coordinator reference for lifecycle operations. The coordinators continue to
subscribe to and release the same existing events; mutable Workbench and Viewer
state ownership is unchanged.

## Acceptance and verification

- The old coordinator paths no longer exist and the moved files use the Shell
  `Coordination` namespace.
- The solution builds in Release with zero warnings and errors.
- The focused Data test project passes.
- Tracked Markdown/JSON link and syntax checks, staged diff whitespace checks,
  and the final clean-tree check pass.

## Boundary

This batch does not establish GPU-driver qualification, runtime WPF visual
qualification, calibrated physical metrology, or a release/tag/package. Those
remain separate gates. If a regression is found, revert this single versioned
commit; do not rewrite or force-move `main`.
