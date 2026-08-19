# OpenVisionLab 3D Privacy-safe Support Bundle

Date: 2026-08-18
Status: Complete

## Outcome

`PL-0024` / `L-14` adds one explicit support-sharing path for the current Run
Record. The operator chooses **Results → Run Record → Export privacy-safe
support bundle**, selects a folder, and receives one collision-safe ZIP. The
action is separate from the full Run Record bundle so its privacy boundary is
visible before export.

The operator problem was that useful diagnostic evidence was split across a
recipe, session log, source identity, quality report, and current result, while
the existing full export could include paths and other data unsuitable for
routine support sharing. The product principle is one deliberate, reviewable
action with explicit omissions and no hidden execution.

## ZIP contract

| Entry | Included evidence | Privacy treatment |
| --- | --- | --- |
| `manifest.json` | schema, privacy mode, pseudonymous current-run identity, included data classes, default omissions, payload byte lengths and SHA-256 | no source path or workstation identity |
| `recipe.json` | current algorithm steps, routes, parameters, ROI and acceptance configuration | free-form names, notes, provenance text, and paths omitted or redacted |
| `log-excerpt.json` | newest in-memory Workbench session entries | newest-first, maximum 200, sensitive text redacted; not the full rolling log |
| `source-identity.json` | stable source identity, SHA-256, byte length, declared unit and frame | source path omitted; source or mesh bytes never included |
| `source-quality.json` | exact Source Quality evidence already recorded in the current Run Record | report paths and sensitive free text redacted; no recalculation |
| `current-result.json` | status, message, metrics, overlays, comparison, duration, per-step results, and timing | artifact paths, execution environment, user/machine identity, and duplicate path-bearing evidence omitted |

Missing recipe or legacy Source Quality evidence remains an explicit
`Unavailable` payload. A mismatched current recipe or Source Quality identity
fails closed. The writer uses `CreateNew` collision handling and does not leave
a ZIP when validation fails.

## Product behavior

- Export is enabled only when a current Run Record is available.
- Selecting the command requests a folder; it does not export before the
  operator confirms a destination.
- Export reads the current Run Record and the existing in-memory Workbench log.
- It does not load raw source geometry, recalculate Source Quality, run an
  algorithm, or change recipe, selection, Preview, published output, or Run
  Record state.
- The new button uses the existing primary semantic button and lock-document
  icon, includes tooltip and accessible name/help text, and is accompanied by
  a visible privacy notice. The full evidence export remains secondary.

## Verification

| Gate | Result |
| --- | --- |
| Release solution build | Pass, 0 warnings / 0 errors |
| Privacy-safe support bundle | Pass, 14/14 |
| Run Record history | Pass, 12/12 |
| Workbench docking/theme | Pass, 87/87 |
| Shell smoke command line | Pass, 41/41 |
| Code structure | Pass, 29/29 |
| Wide 1920 × 1040 current Release | Pass; readable action and privacy notice on selected leftmost monitor |
| Compact 1280 × 760 current Release | Pass; wrapping remains readable without required-text clipping |
| Held pointer-down | Pass; primary chrome, icon, and text remain themed without a platform-light flash |
| R0 fixed inputs | Wide and Compact `-ValidateOnly` pass after hash refresh |

Actual EXE operation confirmed that the new button opens the localized native
folder picker. The automation could navigate the picker to the D-backed bundle
folder but could not reliably target its final **Select Folder** control, so it
cancelled without creating an operator-path ZIP. This does not replace or
invalidate the separate 14/14 writer and ViewModel export verification, but it
is an explicit UI-automation boundary.

Evidence root:

```text
D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260818-pl0024-support-bundle\
```

Key UI evidence:

- `before/before-wide-results.png`
- `before/before-compact-results.png`
- `after/after-wide-results.png`
- `after/after-compact-results.png`
- `after/after-wide-support-pressed.png`

## Current product judgment

The canonical inventory becomes `143 C / 17 P / 50 N / 9 E / 16 O`.
Readiness remains `8.6/10`: routine support sharing is safer and more
auditable, but the product owner's unaided Wide and Compact R0 is still needed
for `A-01`, Workspace v3 `8/8`, and human-usability or release acceptance.
This bundle does not turn raw-height or synthetic evidence into calibrated
metrology, and it does not add cloud upload, accounts, remote support, or
production-line control.

## Completion record

```text
Status: Complete
Scope: Complete PL-0024/L-14 explicit privacy-safe support ZIP with six documented entries, payload hashes, sanitized and bounded current evidence, fail-closed identity handling, and localized Results/Run Record actions
Acceptance criteria: manifest schema/privacy/run/payload length and SHA -> pass; recipe/log/source/quality/result contents and default omissions -> pass; collision safety/unavailable/fail-closed/no-mutation behavior -> pass; localized themed accessible Wide/Compact action and privacy notice -> pass; focused/regression/Release/UI/R0/documentation/Proofline/diff gates -> pass
Verification: Release 0/0; privacy bundle 14/14; history 12/12; docking/theme 87/87; Shell options 41/41; structure 29/29; current Release Wide/Compact screenshot quality, monitor intersection, and held pointer-down pass; actual button opens the native folder picker; R0 Wide/Compact ValidateOnly pass; git diff --check pass
Evidence: docs/OPENVISIONLAB_3D_PRIVACY_SAFE_SUPPORT_BUNDLE_20260818.md; .proofline/issues/PL-0024.json; D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260818-pl0024-support-bundle/
Boundary / next dependency: native folder-picker final confirmation was not completed by automation, while the writer and ViewModel export path pass 14/14; product-owner unaided Wide/Compact R0 remains external; no dependency-ready software slice is selected
```
