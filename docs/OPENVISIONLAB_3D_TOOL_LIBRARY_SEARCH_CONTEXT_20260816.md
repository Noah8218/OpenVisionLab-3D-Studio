# OpenVisionLab 3D Tool Library Search Context

Date: 2026-08-16
Status: Complete
Issue: `PL-0012`

## Outcome

Tool Library search now resets at the successful context boundaries where the
operator's next catalog task changes:

- after a recipe opens successfully;
- after a new empty recipe context is created;
- after a compatible tool is added successfully.

A failed recipe open or rejected Add retains the visible query. Search is not
persisted as a preference, no extra clear button or setting was introduced,
and none of these transitions invokes Preview, Publish, Run, or Validation.

This is the shortest deterministic fix for the ten-recipe EXE study finding:
the previous recipe's term could remain unnoticed and hide tools needed for the
next recipe. It follows the commercial-workbench principle that the visible
catalog should match the current authoring context, while keeping
OpenVisionLab's existing terminology, graphite theme, explicit execution
contracts, and file-first recipe model.

## Ownership

- `ToolWorkbenchViewModel` owns the successful context transitions and search
  state.
- `ToolRecipeTeachingVerification` owns success/failure boundary and
  no-execution regression checks.
- No numerical algorithm, Vision SDK package, recipe schema, persisted setup,
  Viewer, docking, or result identity changed.

## Verification

- Debug and Release Shell builds: `0` warnings, `0` errors.
- Tool Recipe teaching verification: `50/50`.
- Workbench docking verification: `84/84`.
- Actual Release EXE, Compact `1280 x 760`, English, leftmost `DISPLAY2`:
  rendered `Warpage`, opened another recipe, then showed a blank search and the
  unfiltered All tools catalog.
- Actual Release EXE, Wide `1920 x 1040`, Korean, leftmost `DISPLAY2`: repeated
  the same non-empty-to-cleared transition without Preview, Publish, or Run.
- Refreshed fixed R0 package: Wide and Compact `-ValidateOnly` pass without
  launching the application.

Before and after evidence:

- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260816-pl0012-tool-search-context\before\compact-1280x760-en-stale-after-open.png`
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260816-pl0012-tool-search-context\after\compact-1280x760-en-search-focused-nonempty.png`
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260816-pl0012-tool-search-context\after\compact-1280x760-en-search-cleared-after-open.png`
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260816-pl0012-tool-search-context\after\wide-1920x1040-ko-search-focused-nonempty.png`
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260816-pl0012-tool-search-context\after\wide-1920x1040-ko-search-cleared-after-open.png`

The existing search field remained consistent with the semantic graphite theme
in normal, keyboard-focus, populated/filtered, and cleared states. The separate
Studio language-selector popup still renders platform-light and visually blank;
that independently reproduced defect remains `PL-0014`.

## Maturity Reassessment

The evidence-bounded commercial authoring-readiness judgment advances from
`7.8/10` after `PL-0013` to `7.9/10`. The change removes repeated clear/retype
friction and a misleading hidden catalog state, but it does not close the
language-popup defect or the product owner's unaided Wide/Compact R0.

This is a qualitative workflow judgment, not telemetry, certified usability,
release acceptance, production approval, or calibrated physical metrology.
The capability inventory is unchanged.

## Completion Record

```text
Status: Complete
Scope: Clear Tool Library search only after successful recipe open, new-recipe context creation, or compatible Add; retain it on failed open/Add
Acceptance criteria: successful context change leaves no hidden stale filter -> pass; failure retains visible query -> pass; behavior is deterministic and invokes no Preview/Publish/Run -> pass; current-build Wide/Compact and English/Korean states remain reachable and themed -> pass
Verification: Debug and Release 0/0; Tool Recipe teaching 50/50; Workbench docking 84/84; actual Release EXE Compact English and Wide Korean on DISPLAY2; refreshed fixed Wide/Compact -ValidateOnly pass; git diff --check pass
Evidence: this document; .proofline/issues/PL-0012.json; D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260816-pl0012-tool-search-context/
Boundary / next dependency: PL-0014 owns the separately reproduced platform-light blank language popup; product-owner unaided Wide/Compact R0 remains external; no numerical, recipe-schema, persisted-search, Viewer, docking, or commercial-platform scope changed
```
