# Changelog

This forward-looking log records notable user-visible changes from the current
`0.1.1-dev` development line onward. It does not claim that a version was
released; publication state is owned by the release and version policy.

## [Unreleased] - 0.1.1-dev

### Added

- Added one first-use Recipe Center for recipe identity, C3D source, compatible
  starter selection, remembered setup, and explicit reset.
- Added contextual tool setup, compatible input routing, recipe-health counts,
  and direct navigation to incomplete requirements.
- Added safe same-grid Thickness variants and an explicit saved-recipe ordered
  Run that writes reviewable Results evidence.
- Added Top-view GridRectangle teaching with exact live row, column, width, and
  height before Apply.
- Added persistent selected `X / Y / Z` and C3D raw-height status in the Viewer.
- Added Run Record stage timing, exact Source Quality evidence, and exact
  Completeness per-cell JSON, HTML, and CSV results.

### Improved

- Improved Tool Library search-context recovery and language-selector popup
  readability in Wide and Compact layouts.
- Improved Results density while retaining timing, source quality, status, and
  evidence identity.

### Compatibility

- Current ordered and Surface Match Run Records use schema `1.9`.
- Schema `1.8` and older optional-field records remain readable under their
  documented compatibility boundaries.
- Product version remains `0.1.1-dev`; no tag, release candidate, or public
  release is created by these changes.
