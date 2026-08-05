# OpenVisionLab 3D Studio Commercial User Scenario Self-Validation

## Status

Status: Historical — Incomplete for this recorded self-validation matrix.
Current software inventory and human-owner acceptance are owned by the master
backlog and Wide/Compact R0 procedure.

Scope: Current-source novice UI operation, Thickness/Warpage/Median Filter numerical checks, ordered Runner parity, warm/cold/long repeatability, performance, error recovery, and Wide/Compact evidence.

Acceptance criteria: Release/core/numerical/repeat/P0/artifact and nine-category recipe-specific Validation gates pass. Warpage/Filter blank-to-reopen actual-pointer UI and explicit re-Preview pass with current app-only before/reopen screenshots and saved recipes; this evidence is not a continuous video. Isolated read-only, invalid-parameter, rapid-click, overwrite-cancel, mid-run termination/recovery, and stage-transition race checks are recorded. DPI/GPU/source-dialog-race/Preview-counter gaps and reliable first-success/click timing remain incomplete.

Verification: Release build 0 warnings/0 errors; code structure 17/17; Run Record 12/12; Recipe Manager/WPG 37/37; docking 59/59; Validation Set 84/84; Warpage golden 5/5; Median Filter golden 13/13; synthetic downstream 18/18; UI-authored Warpage and Filter Runner replay 1/1 each; 272 recorded repeat rows with 0 status mismatch; isolated negative UI evidence in `logs/negative-isolated-ui-results.txt`.

Evidence: `artifacts/self-test/20260730-1016-commercial-user-validation/`

Boundary / next dependency: The evidence set is reusable but the full requested validation task is incomplete. Commercial production readiness is No-Go until the remaining negative/timing gates, independent human-owner Wide/Compact R0, and physical sensor/calibration/GR&R evidence exist.

## Findings

Two P0 defects were found only through direct novice-style operation:

1. Bundled WPF PropertyGrid Tab traversal could terminate the application after recomposition/scrolling.
2. Normal startup and recipe/source changes could present an unrelated historical Run Record as current Results evidence.

The PropertyGrid host now commits the active editor and owns safe Tab/Shift+Tab traversal. The Shell now clears current evidence when recipe context changes while retaining recent records without automatic selection. Actual-pointer replay and focused verification passed.

The remaining Warpage/Filter UI gate also passed:

- Warpage: blank recipe, generated C3D, tool add, two-point ROI, `P2V <= 2.1`, `RMS <= 0.1`, explicit Preview, save, clean close, reopen, no implicit Preview, explicit re-Preview; both Preview results were `Pass`, P2V `2.00059`, RMS `0.0769221`.
- Median Filter: blank recipe, generated C3D, tool add, `Median / Kernel 3 / PreserveMask / AvailableNeighbors`, explicit Preview, save, clean close, reopen, no implicit Preview, explicit re-Preview; both display fingerprints were `D19CA4164C72`.
- Evidence: `screenshots/warpage-ui-01-preview-pass-printwindow.png`, `screenshots/warpage-ui-02-reopen-preview-pass.png`, `screenshots/filter-ui-01-preview.png`, `screenshots/filter-ui-02-reopen-preview.png`, `recipes/commercial-warpage-ui.ov3d-recipe.json`, and `recipes/commercial-filter-ui.ov3d-recipe.json` under the evidence root.

An isolated read-only save test also passed the safety boundary: the application stayed alive, the recipe SHA remained unchanged, and the dialog exposed `Access to the path is denied.` in expanded details. It also exposed `CV-011`: the primary message is generic, gives no direct recovery action, and leaves the precise cause in English-only details.

An isolated mid-run termination test also passed recovery: the dedicated Shell was force-terminated 184.5 ms after starting a seven-sample Validation run, the copied recipe and manifest SHA values stayed unchanged, and reopening restored seven Pending rows. A subsequent explicit rerun completed with `3 Pass / 2 Fail / 2 Error` in 1,597 ms.

The current information architecture remains the product direction:

- Setup owns Tool Library and Recipe Chain.
- Teach owns step rail, Viewer, ROI/parameters, Preview and Publish.
- Validate owns samples, run results, failure analysis, thresholds and Held-out.
- Results owns decision, run evidence, comparison and reports.
- Advanced owns expert diagnostics.

Do not merge these responsibilities back into one dense screen. Camera, lighting, PLC, robot, cloud and production-control platform scope remain out of scope.

## Next gate

1. Complete the isolated DPI/GPU/source-dialog-race/Preview-counter gaps and reliable first-success timing | Recommended model: gpt-5.6-sol | Reasoning effort: high
2. Fix novice-entry, localization, Source Quality and corrupt-file messaging P1 items | Recommended model: gpt-5.6-terra | Reasoning effort: medium
3. Independent human-owner Wide/Compact R0 | Prerequisite: unaided operator and screen recording; do not spend model tokens on another automated replay
4. After R0 passes, start `J-01/J-03/J-04 SurfaceModel` | Recommended model: gpt-5.6-sol | Reasoning effort: high
