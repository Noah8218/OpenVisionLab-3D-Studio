# OpenVisionLab 3D Studio Language-selector Popup

Date: 2026-08-16
Status: Complete
Issue: `PL-0014`

## Outcome

The Studio language selector no longer opens a white, apparently blank Windows
popup inside the graphite workbench. Its responsive width style now derives
from the existing shared ComboBox style, so the control, popup, items, focus,
selection, hover, disabled state, and open transition keep the product's
semantic dark resources.

Compact remains inside the 60-pixel navigation rail with a 52-pixel selector,
4-pixel outer margin, and reduced content padding. It visibly renders `한` and
`EN`; Wide continues to render `한국어` and `English`. No new theme, control,
settings surface, dependency, or language service was introduced.

This applies the product requirement that a persistent utility must
remain immediately recognizable in every layout. The implementation stays
independent: it preserves OpenVisionLab terminology, graphite resources,
file-first persistence, and explicit Preview/Publish/Run contracts.

## Root Cause And Ownership

`StudioLanguageSelector` had a local responsive `Style` for width but did not
derive from the shared implicit ComboBox style. In WPF, that local style
replaced the Wpf.Ui-based control template, exposing the platform-default light
popup. The fix adds the missing `BasedOn` link and keeps only responsive
margin, padding, and width policy in `StudioNavigationRailView`.

- The shared application theme still owns control and popup semantics.
- The navigation rail owns only Wide/Compact density.
- `OpenVisionLanguageService` continues to own language persistence.
- No recipe, source, ROI, result, Viewer, docking, numerical, or SDK behavior
  changed.

## Verification

- Debug and Release solution builds: `0` warnings, `0` errors.
- Workbench docking verification: `87/87`; it checks the shared style base,
  Wide/Compact selector bounds, and resolved semantic disabled colors.
- Actual Release EXE on dynamically selected leftmost `DISPLAY2`:
  - Wide `1920 x 1040`: `한국어`/`English`, dark `128 x 95` popup, selected and
    keyboard-focus states;
  - Compact `1280 x 760`: visible `한`/`EN`, dark `90 x 95` popup, selected,
    keyboard-focus, and actual pointer-hover states;
  - a pointer click opens the shared dark popup without the former white
    platform surface;
  - selecting English updates the current UI and a normal close/reopen restores
    English.
- Before and after the language change, the loaded `Thickness Coupon V1 · 8
  Pad`, raw-height source, Reference/Measurement ROI, validity/save status,
  `Ready 8`, `Preview 0`, and `Published 0` remain unchanged. No Preview,
  Publish, Run, Validation, source replacement, ROI edit, or result creation
  occurs.
- Refreshed fixed R0 package: Wide and Compact `-ValidateOnly` pass without
  launching the application.

Evidence root:

- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260816-pl0014-language-popup\before\`
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260816-pl0014-language-popup\after\`
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260816-pl0014-language-popup\reports\`

Representative before/after files:

- `before/wide-1920x1040-popup-context.png`
- `before/compact-1280x760-popup-context.png`
- `after/wide-final-1920x1040-en-popup-context.png`
- `after/wide-final-1920x1040-en-popup-selected.png`
- `after/compact-1280x760-ko-popup-context.png`
- `after/compact-1280x760-en-keyboard-focus-transient.png`
- `after/compact-1280x760-en-hover-transient.png`

## Maturity Reassessment

The evidence-bounded operator authoring-readiness judgment advances from
`7.9/10` after `PL-0012` to `8.0/10`. The remaining known recipe-authoring
study theme leak is removed, and the language utility is now readable without
extra navigation in both supported layouts.

This is a qualitative workflow judgment, not telemetry, certified usability,
release acceptance, production approval, or calibrated physical metrology.
The capability inventory is unchanged, and the product owner's unaided
Wide/Compact R0 is still required.

## Completion Record

```text
Status: Complete
Scope: Retain the shared semantic language-selector ComboBox popup, keep Korean/English legible in Wide/Compact, persist language, and preserve inspection state
Acceptance criteria: normal/open/selected/keyboard-focus/pointer-hover/click-open/disabled semantics remain dark and legible -> pass; Wide/Compact selector and popup bounds remain usable -> pass; language survives normal restart without recipe/source/ROI/result/Preview/Publish/Run mutation -> pass
Verification: Debug and Release 0/0; Workbench docking 87/87; actual Release EXE Wide and Compact on DISPLAY2 with Korean/English popup, selection, focus, and hover evidence; refreshed fixed Wide/Compact -ValidateOnly pass; git diff --check pass
Evidence: this document; .proofline/issues/PL-0014.json; D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260816-pl0014-language-popup/
Boundary / next dependency: product-owner unaided Wide/Compact R0 remains external; no dependency-ready software slice is selected; large-C3D work still requires representative input and accepted budgets; no numerical, recipe-schema, Viewer, docking, or excluded-platform scope changed
```
