# Shared-chat analysis and immutable C3D load snapshot

> Current-priority note (2026-08-15): the bounded Workbench run-log item named
> below was completed as `PL-0008`. The large-C3D target remains blocked on a
> representative maximum input and accepted memory/load-time budgets. Use the
> master backlog for current priority; retain the analysis below as evidence
> for its recorded scope.

Date: 2026-08-06

Status: Complete

## Scope

This document records the verified parts of the project analysis shared at
<https://chatgpt.com/share/6a7355bc-a328-83ee-83ee-044ed2616414> and the first
approved correction selected from that analysis.

The analysis was treated as a list of claims to check, not as project
authority. Current source, Git state, GitHub metadata, the master backlog, and
focused execution remain authoritative.

## Verified findings and disposition

| Finding | Current evidence | Disposition |
| --- | --- | --- |
| One open `C3DHeightGrid` could retain load-time SHA/statistics while later point, row, profile, full-map, and inspection reads reopened the mutable source path. | The pre-change read methods and four Viewer inspection resampling paths reopened `SourcePath`. | Confirmed high-risk identity defect; closed as `PL-0004`. |
| Full-resolution C3D paths have a material memory budget that is not yet stated or stress-tested. | `C3DHeightFieldSnapshot` reads the complete file and creates a `double[]`; `C3DHeightGrid` now deliberately retains one `float[]`, and `ReadHeightMapValues` creates a transient `double[]`. | Confirmed design boundary, not a proven runtime failure. Requires a representative maximum grid and memory budget before optimization. |
| The header can report that A3 re-grid is not implemented. | Pre-change `AlignmentStatusSummary` contained that text while `ToolWorkbenchViewModel.RegridHeightFieldExecution.cs` implements the A3 path; current-build Wide/Compact captures reproduced the mismatch. | Confirmed stale visible status; closed as `PL-0005`. |
| The release policy claims a published `v0.1.0-rc.1`, but current remote release identity does not support that claim. | The policy named the release; current GitHub Releases reports none, the Tags page exposes none, `git ls-remote --tags origin` returns zero refs, and `ac57687` has no current remote ref. | Confirmed documentation mismatch; closed as `PL-0006` by correcting the policy. No release operation occurred. |
| `ToolWorkbenchViewModel` is large. | Its 33 partial files currently total 14,040 lines. | Observation only. A partial split is not an architectural boundary and file size alone is not a defect; refactor only around an independently testable owner. |
| Workbench run-log retention is unbounded. | `AppendLog` inserts into `RunLog` without a cap. | Confirmed maintenance risk, but no runtime failure was observed. Define retention/export behavior before changing it. |
| The migrated main branch CI is currently failing. | Main commit `8400b89a788b2a59affb713833001fff15c6aff0` has successful GitHub Actions run `31012735944`. | Refuted for the current committed main branch. This uncommitted slice still needs hosted CI after a future authorized push. |

## Implemented correction

`C3DHeightGrid` now owns the exact raw `float[]` parsed during load. The
following operations use that immutable loaded snapshot and never reopen the
path:

- `ReadPoint`;
- `ReadRowRange`;
- `ReadLineProfile`;
- `ReadHeightMapValues`;
- `WithMaxRenderedPoints`, used for Viewer display-density changes and the
  plane, flatness, Gap/Flush, and Volume inspection sampling paths.

The data flow is now:

```text
C3D Load -> byte identity + statistics + private raw samples
         -> point / row / profile / full-map reads
         -> render-density and inspection sampling views
```

Changing or replacing the path after load can no longer combine new samples
with the already recorded identity. An explicit later load still creates a
new source snapshot, as expected.

## Verification

- Debug solution build: `0` warnings, `0` errors.
- Release solution build: `0` warnings, `0` errors.
- C3D loaded-snapshot/profile verification: `14/14`.
- Generic height-measurement Workbench: `54/54`.
- 3-Point Plane Workbench: `11/11`.
- Datum Plane Raw-Height Deviation Workbench: `12/12`.
- C3D map fidelity: `10/10`.
- Plane flatness: `9/9`.
- Gap / Flush: `8/8`.
- Volume: `9/9`.
- Source-path coupling search: no remaining
  `C3DHeightGrid.Load(c3dSample.SourcePath, ...)` in Viewer.
- Refreshed fixed R0 inputs: Wide and Compact `-ValidateOnly` both passed;
  no application was launched.
- No visible UI, layout, text, or workflow changed in this slice, so a new UI
  before/after capture was not applicable.

Local evidence:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260806-c3d-immutable-load-snapshot\`

The focused verifier is also part of `.github/workflows/ci.yml` so the loaded
snapshot contract runs in hosted CI after the change is committed and pushed.

## Follow-up correction: truthful alignment status

`PL-0005` replaces the obsolete message with a direct projection of the most
downstream present A3, A2, A1, or legacy step and its actual `State`. State
changes now notify the header binding without executing Preview, Publish, Run,
or Validation. Debug and Release builds pass `0/0`; the CI-routed Tool Recipe
teaching verifier passes `35/35`; current-build application-only Wide and
Compact before/after captures are accepted; and refreshed R0 `-ValidateOnly`
passes in both layouts. Preserve
`OPENVISIONLAB_3D_TRUTHFUL_ALIGNMENT_STATUS_SUMMARY_20260806.md` and
`.proofline/issues/PL-0005.json`.

## Follow-up correction: release-policy reconciliation

`PL-0006` makes current publication state explicit: the public repository has
no GitHub Release or tag, and historical `v0.1.0-rc.1` candidate records are
not a current distribution claim. The policy current-values table now matches
product `0.1.1-dev`, Viewer Host API `1.0`, Viewer manifest `1.0`, Run Record
`1.6`, and generic Tool Recipe `1.5`. Future publication still requires
explicit owner approval, the complete release gate, and the required owner R0
for the exact release target. No tag, release, asset, commit, or push was
created.

## Memory boundary

The simplest correctness fix retains one four-byte raw sample per C3D cell
for the lifetime of the open Viewer grid. Full-map consumers can additionally
allocate an eight-byte value per cell, and the separate
`C3DHeightFieldSnapshot` load path currently holds a `double[]` after reading
the complete source bytes.

This task does not claim out-of-core operation, memory mapping, a maximum
supported grid, or large-file stability. Do not add a cache or storage
abstraction until a representative maximum input and an accepted memory/time
budget exist.

## Next priorities

1. Define and implement bounded Workbench run-log retention without weakening durable `OVLog` evidence. | Recommended model: `gpt-5.6-terra` | Reasoning effort: `low`.
2. Establish a large-C3D memory/performance target before storage redesign. Prerequisite: representative maximum C3D input and accepted process-memory/load-time limits. | Recommended model: none until the prerequisite exists | Reasoning effort: none.

The product-owner unaided Wide/Compact R0 remains the separate highest
acceptance priority and needs owner operation, not model execution.

## Completion record

```text
Status: Complete
Scope: Verify the shared-chat findings; close PL-0004 with one immutable C3D load snapshot; close PL-0005 with truthful A1/A2/A3 header state; close PL-0006 by reconciling release policy with current remote state; retain focused CI coverage and refreshed R0 fixed inputs
Acceptance criteria: every C3D read shape uses loaded samples -> pass; Viewer resampling does not reopen the path -> pass; alignment header reports the most downstream actual step state without execution -> pass; release policy no longer presents historical candidate evidence as a current distribution -> pass; focused and solution verification -> pass; current docs and issue ledgers record results and boundaries -> pass
Verification: PL-0004 Debug/Release 0/0, focused C3D 14/14, affected checks 113/113; PL-0005 Debug/Release 0/0, Tool Recipe teaching 35/35, current Wide/Compact captures accepted; PL-0006 GitHub Releases none, Tags/remote refs zero, source version values matched; R0 Wide/Compact ValidateOnly pass
Evidence: this document; .proofline/issues/PL-0004.json; .proofline/issues/PL-0005.json; .proofline/issues/PL-0006.json; docs/OPENVISIONLAB_3D_RELEASE_VERSION_POLICY.md; D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260806-c3d-immutable-load-snapshot\; D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260806-alignment-status-summary\
Boundary / next dependency: PL-0006 is complete; hosted CI for the uncommitted code slices is not yet available; memory scalability needs a representative maximum input and budget; human-owner R0 remains external
```
