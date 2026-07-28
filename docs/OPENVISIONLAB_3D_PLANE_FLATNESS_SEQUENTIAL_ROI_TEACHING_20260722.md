# Plane Flatness Sequential ROI Teaching

Updated: 2026-07-22  
Status: **Complete for Workbench teaching UX; physical/metrology validation unverified**

## Operator workflow

Plane Flatness remains one composable Measure tool. Its authored input contract
is fixed and visible:

```text
TransformedHeightField
  -> 1. Reference ROI
  -> 2. Measurement ROI
  -> explicit Preview
  -> explicit Publish
```

The Step Parameters dock now shows two numbered role cards. Measurement ROI is
disabled until Reference ROI exists. Capturing or reusing the Reference ROI
automatically advances the active role to Measurement ROI. Capture, replace,
and compatible-ROI reuse preserve the exact input order
`[HeightField, Reference, Measurement]`. Teaching changes recipe geometry only;
it does not invoke Preview or Publish.

The panel uses the shared application localization service and has separate
Korean and English labels. At `1920 x 1080`, the generic input list defaults to
collapsed for Plane Flatness so both role cards remain visible in the dock.

## Acceptance evidence

- Solution build: `0` warnings, `0` errors.
- Focused Workbench verification: `23/23`.
- Viewer teaching-capture ViewModel: `18/18`, including distinct Plane
  Flatness Reference/Measurement role identities.
- Existing Tool Recipe selection regression: `17/17`.
- Current-build Shell-to-Viewer pointer smoke: pass. Two real OS-pointer picks,
  cancel/restart/apply, schema promotion, and route mutation passed while
  `Preview=NotRun` and Viewer result count remained zero.
- Current-build Shell screenshot quality: accepted on attempt 1;
  black ratio `0.0008`, white ratio `0.3488`, luminance `0..255`.
- Before: `artifacts/ui/20260722-plane-flatness-sequential-roi/before.png`.
- After: `artifacts/ui/20260722-plane-flatness-sequential-roi/after.png`.
- Reports:
  - `artifacts/verification/plane-flatness-viewer-capture-shell.txt`
  - `artifacts/verification/plane-flatness-viewer-capture-viewmodel.txt`
  - `artifacts/verification/plane-flatness-viewer-capture-selection-regression.txt`
  - `artifacts/verification/plane-flatness-viewer-pointer-shell.txt`
- Current pointer-apply capture:
  `artifacts/ui/20260722-plane-flatness-viewer-capture/after-pointer-apply.png`.

## Completion record

```text
Status: Complete
Scope: Add a dedicated ordered Reference ROI -> Measurement ROI teaching UX for the generic Plane Flatness tool.
Acceptance criteria: step 2 blocked before step 1 -> pass; automatic role advance -> pass; role-specific Viewer request/candidate identity -> pass; capture/replace/reuse preserve three-input order -> pass; artifact-owned role persistence -> pass; Korean/English labels -> pass; teaching does not run Preview -> pass; shared real-pointer bridge -> pass; both role cards visible at 1920 x 1080 -> pass.
Verification: Studio build 0 warnings/errors; focused Workbench 23/23; Viewer capture 18/18; selection regression 17/17; real-pointer Shell smoke pass; current-build Shell captures accepted on attempt 1.
Evidence: artifacts/ui/20260722-plane-flatness-sequential-roi/; artifacts/ui/20260722-plane-flatness-viewer-capture/; artifacts/verification/plane-flatness-viewer-capture-*.txt; artifacts/verification/plane-flatness-viewer-pointer-shell.txt.
Boundary / next dependency: The exact role logic, Viewer candidate generation, artifact-owned persistence, and shared real-pointer bridge are proven separately. A single live run that first Publishes a real A3 TransformedHeightField and then performs both Plane Flatness role picks remains unverified. Trusted real A1 landmark XYZ/frame/unit/provenance/revision/threshold data is still required before real XYZ Affine and physical/metrology claims.
```
