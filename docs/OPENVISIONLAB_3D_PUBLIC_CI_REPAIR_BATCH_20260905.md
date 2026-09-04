# OpenVisionLab 3D Studio Public CI Repair Batch — 2026-09-05

## Versioned scope

- Product version: `0.5.2-dev`
- Public base commit: `8f9b3a093c22db394f5f267e3d715e02b38e9fb8` (`v0.5.1-dev`)
- Dev workflow source: `d2e877d187df0514f2885450e5843e9fea84e882`
- Change type: public source-tracking and clean-checkout build-integrity patch

This batch is intentionally separate from the aggregated `v0.5.1-dev`
refactor batch. It does not change runtime behavior, recipe formats, the Viewer
Host API, Run Record contracts, or the explicit Preview/Publish/Run workflow.

## Root cause and correction

The public checkout ignored directories named `artifacts/`. Six source files
under `src/OpenVisionLab.ThreeD.Verification/Shell/Artifacts/` were therefore
present in the local working copy but absent from the committed public tree.
The dedicated `OpenVisionLab.ThreeD.Verification` project then failed to build
from a clean GitHub checkout because `VerificationCommandRouter` referenced
those missing namespaces.

The six existing Verification sources are restored as tracked public files:

- `MultipleSurfaceMatchWorkbenchVerification.cs`
- `RenderableC3DCatalogVerification.cs`
- `SurfaceEdgeDiagnosticReviewWorkbenchParityVerification.cs`
- `SurfaceEdgeWorkbenchParityVerification.cs`
- `SurfaceMatchPublishedEvidenceOwnerVerification.cs`
- `SurfaceMatchWorkbenchParityVerification.cs`

## Evidence

- Local Release build after the preceding public batch: 0 warnings, 0 errors.
- Local Data test after the preceding public batch: 2/2 passed.
- Local Dev Debug build after the CI routing correction: 0 warnings, 0 errors.
- Local `Multiple Surface Match Workbench` verification after the correction:
  10/10 passed.
- Public CI failure: run `33930345240` failed at clean-checkout build with the
  missing `OpenVisionLab.ThreeD.Verification.Shell.Artifacts` namespace.
- A rollback archive of the public base commit is retained outside the
  repository in the local verification evidence store.

## Verification boundary

This batch proves source inclusion and build-path repair only. It does not
establish calibration, uncertainty, Gauge R&R, production measurement
capability, WPF runtime/DPI coverage, or large-data performance. No tag,
release, package publication, deployment, or shutdown is performed by this
batch.
