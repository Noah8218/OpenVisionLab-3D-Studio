# Run Record History And Export UX

Date: 2026-07-23
Status: Complete for local recent-file access and bundle export

## Outcome

The existing docked bilingual `Run Record` tab now exposes the result actions
that were previously hidden in an older diagnostic screen:

- open another Run Record JSON;
- open the current JSON, HTML, or CSV;
- open the current record folder;
- export the current JSON/HTML/CSV as one collision-safe folder;
- reopen one of the ten most recent valid Run Record paths.

No new result database, result window, or inspection executor was added.
Opening or selecting a record only reads evidence. It never changes the
recipe, loads a 3D source, or runs inspection.

## Local History Contract

Recent paths are stored with the existing atomic recent-file mechanism:

```text
%LOCALAPPDATA%\OpenVisionLab\ThreeDStudio\recent-run-records.json
```

The list is newest-first, case-insensitive, deduplicated, and bounded to ten
paths. A valid schema `1.4` or compatible older record becomes the current
read-only view. Invalid JSON is rejected without replacing the current record.

The recent list stores paths only. It is not a production result archive, a
batch database, or a trend store.

## Export Contract

`Export bundle` creates a new folder under the operator-selected directory:

```text
RunRecord-<RunId>\
  <current>.json
  <current>.html
  <current>.csv
```

If the folder already exists, a numeric suffix is added. Existing target files
are never silently overwritten. Only companions that actually exist are
copied. Verification compares all three exported files byte-for-byte with the
current sources.

## UI Contract

The actions remain inside the dockable Pipeline / Validation pane. WPF UI
icons improve recognition while every action retains visible bilingual text
and an accessible automation name.

At `1920 x 1040`, the toolbar, recent selector, and step rows are visible
together. At `1280 x 760`, the same actions remain available without
horizontal clipping; the existing docked step list uses its normal vertical
scrolling area.

## Verification

| Check | Result | Evidence |
| --- | --- | --- |
| Shell project build | Pass, 0 warnings / 0 errors | current command output |
| History/load/export verification | Pass, 8/8 | `artifacts/current/20260723-run-record-history-ux/run-record-history-verification.txt` |
| Docking regression | Pass, 27/27 | `docking-regression.txt` |
| Recipe teaching regression | Pass, 25/25 | `recipe-teaching-regression.txt` |
| Before current-EXE baseline | Pass, quality attempt 1 | `before-ko.png`, `before-ko-quality.txt` |
| Korean current-EXE after capture | Pass, quality attempt 1 | `after-ko.png`, `after-ko-quality.txt` |
| English `1920 x 1040` capture | Pass, quality attempt 1 | `after-en-1920x1040.png`, `after-en-1920x1040-quality.txt` |
| Korean `1280 x 760` capture | Pass, quality attempt 1 | `after-ko-1280x760.png`, `after-ko-1280x760-quality.txt` |

Evidence root:

```text
artifacts/current/20260723-run-record-history-ux/
```

## Completion Record

```text
Status: Complete
Scope: Local recent Run Record paths, compatible JSON open, current artifact open/folder commands, collision-safe JSON/HTML/CSV bundle export, and bilingual docked UI.
Acceptance criteria: schema 1.4 and 1.3 load; invalid JSON retains the current record; recent paths persist newest-first and bounded; export files are byte-identical; controls remain usable at 1920 x 1040 and 1280 x 760.
Verification: Shell build 0/0; focused history 8/8; docking 27/27; teaching 25/25; four current-task screenshot quality checks pass on attempt 1.
Evidence: artifacts/current/20260723-run-record-history-ux/
Boundary / next dependency: This is local file history, not a production result database, batch execution, trend analytics, audit retention policy, physical calibration, or metrology. The next evidence gate is an unaided owner open/reopen/export replay; the next algorithm trust gate still requires distinct real four-landmark acquisitions with unit/frame/provenance.
```
