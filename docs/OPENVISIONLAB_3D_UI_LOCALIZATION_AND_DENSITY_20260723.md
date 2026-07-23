# OpenVisionLab 3D UI Localization and Density Checkpoint

Date: 2026-07-23
Status: Complete

## Decision

The user-facing structure of Tool Lab, Calibration, and Expert now follows the
application language instead of mixing fixed English labels into Korean mode.
This checkpoint does not translate technical entity IDs, coordinate symbols,
algorithm type names, stored recipe values, or execution evidence.

The product remains a generic rule-based 3D inspection recipe workbench:

`3D input -> typed tools -> explicit teaching/Preview -> Publish -> validation/Run -> evidence`

This work does not add an algorithm, change recipe validation, or broaden the
product into camera, PLC, cloud, production-line, physical-calibration, or
metrology scope.

## Implemented scope

- Added a small WPF markup extension backed by the existing
  `ThreeDLocalization` service, so fixed view text changes immediately between
  Korean and English.
- Localized the custom title, subtitle, command labels, input/output headings,
  and role text in all ten currently exposed Tool Lab windows.
- Shortened the XYZ Affine Solve title badge to the typed route
  `CorrespondenceSet -> AffineTransform3D`; the previous badge was clipped.
- Added a view-only localized PropertyGrid descriptor wrapper. It changes
  display names and categories only; the wrapped parameter object, property
  names, recipe JSON, and typed adapter contracts remain unchanged.
- Replaced the PropertyGrid's fixed English search box with a localized host
  search field and widened the property-name column.
- Localized Calibration dock titles, navigation, status, tables, chart labels,
  inspector fields, commands, and unavailable-state placeholders.
- Localized Expert dock titles, sections, fixed form labels, commands, metrics,
  and evidence tabs. Coordinate abbreviations such as `T X`, `R Z`, and `C X`
  remain technical symbols.
- Kept Korean and English as separate switchable UI states; no combined
  bilingual label is used as a workaround.

## Deliberate boundary

The shared Viewer still contains English-heavy HUD/control text, and several
ViewModel-generated execution/evidence summaries are English. Those are the
next localization slice because they are shared runtime contracts rather than
fixed view labels. Disabled legacy Expert values also retain their technical
runtime text.

This checkpoint therefore means “fixed structural labels and form density are
complete for the selected views,” not “every runtime string in the product is
translated.”

## Current verification

| Check | Result | Evidence |
| --- | --- | --- |
| Debug solution build | Pass, 0 warnings / 0 errors | `artifacts/current/20260723-ui-localization-density/build-final-current.txt` |
| Calibration ViewModel and language state | Pass, 72/72 | `artifacts/current/20260723-ui-localization-density/calibration-viewmodel-verification-current.txt` |
| Docking contracts | Pass, 27/27 | `artifacts/current/20260723-ui-localization-density/workbench-docking-verification-current.txt` |
| Recipe Center / localized WPG / message dialog | Pass, 28/28 | `artifacts/current/20260723-ui-localization-density/recipe-manager-wpg-verification-current.txt` |
| Affine Solve and Re-grid Tool Labs, Korean and English actual EXE | Accepted on attempt 1 | `artifacts/current/20260723-ui-localization-density/after/tool-lab-*-current.*` |
| Calibration, Korean and English, 1280 x 760 actual EXE | Accepted on attempt 1 | `artifacts/current/20260723-ui-localization-density/after/calibration-*-current-1280x760.*` |
| Expert, Korean 1920 x 1040 and English 1280 x 760 actual EXE | Accepted on attempt 1 | `artifacts/current/20260723-ui-localization-density/after/expert-*-current*` |

Visual comparison confirms that the earlier Korean Tool Lab still using
`Full-XYZ Affine Solve`, English PropertyGrid fields, and a clipped title badge
now shows Korean structural labels, readable property names, and an unclipped
typed route. Calibration reaches the bottom status bar at `1280 x 760` in both
languages. Expert retains its dock layout and exposes localized structural
labels at both tested sizes.

## Completion record

Status: Complete
Scope: Fixed Tool Lab, Calibration, and Expert structural labels; localized
PropertyGrid display metadata/search; representative two-language density
checks.
Acceptance criteria: Debug build passes; localization does not mutate stored
parameter contracts; Korean and English actual-EXE captures pass quality;
representative 1280 x 760 and 1920 x 1040 layouts show no structural clipping.
Verification: `dotnet build "OpenVisionLab.ThreeDStudio.slnx" -c Debug
-p:Platform="Any CPU"`; Calibration `72/72`; docking `27/27`; Recipe
Center/WPG `28/28`; eight current actual-EXE screenshot-quality reports accepted
on attempt 1.
Evidence: `artifacts/current/20260723-ui-localization-density/`.
Boundary / next dependency: Shared Viewer HUD/control text and dynamic
execution/evidence summaries remain the next bounded localization priority.
The overall owner UI score remains the previously accepted scoped `85/100`
until a new owner acceptance replay; physical calibration and metrology remain
unverified.
