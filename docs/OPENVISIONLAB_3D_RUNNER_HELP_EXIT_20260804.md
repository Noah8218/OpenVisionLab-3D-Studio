# OpenVisionLab 3D Runner Help Exit

Date: 2026-08-04

Status: Complete

## Outcome

`PL-0002 Runner help exits with usage failure code` is resolved. The Runner
now recognizes explicit `--help` case-insensitively, writes the existing
shared usage text to standard output, and exits `0`.

Invalid command combinations and missing required values continue to write
the same controlled usage text to standard error and exit `2`. The help route
does not load a recipe, execute a Tool, write a report, or change application
state.

## Implementation

`RunnerCommandRouter` now has one early help branch and one shared
`WriteUsage(TextWriter)` implementation. The previous invalid-input branch
uses the same writer, so help and error output cannot drift into separate
copies.

No new dependency, command framework, UI, recipe contract, numerical code,
or Library-Noah package was added.

## Verification evidence

Physical evidence root:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\20260804-pl0002-runner-help\`

| Gate | Result | Evidence |
| --- | --- | --- |
| Release solution build | Pass, `0` warnings / `0` errors | `release-build.log` |
| `--help` | Pass, exit `0`; stdout usage; empty stderr | `help.direct.stdout.txt`, `help.direct.stderr.txt` |
| `--HELP` | Pass, exit `0`; stdout usage; empty stderr | `help-upper.stdout.txt`, `help-upper.stderr.txt` |
| Unknown command | Pass, exit `2`; empty stdout; stderr usage | `invalid.direct.stdout.txt`, `invalid.direct.stderr.txt` |
| Missing required `--report` | Pass, exit `2`; empty stdout; stderr usage | `missing-report.stdout.txt`, `missing-report.stderr.txt` |
| Shared usage body | Pass; all four bodies SHA-256 `CEFCB5D382309FC56DD9053EBE63CADFCC25242602983CCCD646D8CDAF87B2FC` | Files above |
| Existing L-13 Runner route | Pass, `19/19` | `surface-match-run-record-regression.txt` |
| Code structure and Noah ownership | Pass, `29/29`; migration debt `0` | `code-structure-report.txt` |

This is a command-line-only change. No UI, visible desktop workflow, layout,
theme, or Viewer renderer changed, so Wide/Compact screenshot evidence is not
applicable.

## Completion record

Status: Complete

Scope: Explicit case-insensitive Runner help success path with shared usage
output; preserved invalid and incomplete command failure behavior

Acceptance criteria: `--help` and `--HELP` print usage and exit `0` -> pass;
missing values and unknown commands retain controlled nonzero exits -> pass;
shared usage text remains identical -> pass

Verification: Release solution `0/0`; direct command matrix `4/4`; existing
L-13 Runner regression `19/19`; structure `29/29`

Evidence: This document, `.proofline/issues/PL-0002.json`, and the D-drive
evidence root above

Boundary / next dependency: No UI, recipe, execution, numerical, matching,
Viewer, or Library-Noah change. No dependency-ready software item is selected.
The next acceptance priority is human-owner unaided Wide/Compact R0.
Prerequisite: owner operation and evidence. Recommended model: none until
evidence exists; reasoning effort: none.
