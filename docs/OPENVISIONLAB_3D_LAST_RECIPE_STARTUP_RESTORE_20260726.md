# Last-recipe startup restoration - 2026-07-26

## Status

Status: Complete

## Owner finding

A normal application restart opened `Untitled 3D Inspection` with no source
even though the bounded recent-recipe file retained ordered recipe paths.
The store populated Recipe Center history but no normal-start path consumed
its first available entry.

## Correction

- `ToolWorkbenchViewModel` exposes the most recent available recipe path.
- Normal Shell composition explicitly owns the persistent
  `%LOCALAPPDATA%\OpenVisionLab\ThreeDStudio\recent-recipes.json` path.
- Verification ViewModels default to a process-temporary recent file, so test
  recipes cannot replace the operator's startup recipe.
- A normal start with no explicit recipe opens the most recent available recipe
  through the existing Shell recipe/source loading adapter.
- Automated runs and explicit command-line recipes do not invoke the restore
  fallback.
- Startup restoration does not invoke Preview, Run, or Publish.

## Verification

- Release build: `0` warnings, `0` errors.
- Recipe Manager/WPG: `35/35`, including missing-entry skip behavior.
- Shell command-line options: `9/9`.
- Tool recipe teaching: `27/27`.
- Actual normal Release startup log:
  `Workbench[Open] Restoring most recent recipe ... preview=false | run=false | publish=false`.
- Actual current Windows capture shows the prior empty start before correction
  and the restored recipe/source/steps after correction.

## Evidence

- `artifacts/current/20260726-last-recipe-startup/before-last-recipe-not-restored-window.png`
- `artifacts/current/20260726-last-recipe-startup/after-last-recipe-restored-window.png`
- `artifacts/current/20260726-last-recipe-startup/recipe-manager-wpg.txt`
- `artifacts/current/20260726-last-recipe-startup/shell-command-line.txt`
- `artifacts/current/20260726-last-recipe-startup/tool-recipe-teaching.txt`

## Completion record

Status: Complete

Scope: restore the most recent available recipe on a normal application start
without automatic inspection execution, while isolating automated recent state.

Acceptance criteria: normal start restores recipe/source/steps; missing recent
entries are skipped; automated runs do not consume persistent recent state;
Preview, Run, and Publish remain explicit.

Verification: Release build `0/0`; Recipe Manager/WPG `35/35`; Shell
command-line `9/9`; recipe teaching `27/27`; structured startup log; actual
before/after Windows captures.

Evidence: `artifacts/current/20260726-last-recipe-startup/`.

Boundary / next dependency: this closes only startup recipe continuity. The
owner must continue unaided replay attempt 6 to prove the complete
create/teach/Preview/save/close/reopen workflow. Physical
calibration/metrology remain unverified.
