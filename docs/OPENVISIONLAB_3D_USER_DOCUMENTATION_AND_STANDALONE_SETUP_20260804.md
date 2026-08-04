# User Documentation and Standalone Setup Closure - 2026-08-04

Status: Complete

## Scope

Replace the outdated public README and unreadable development guide, add a
first-time operator tutorial, document the exact operator/developer utility
boundary, make the setup helper distinguish a normal build from full
verification, and ensure the self-contained package carries package-local
documentation that does not depend on repository-only links.

The published implementation baseline is `main` commit
`f7e43d96e8a5ff95206e3b703cd6e125fb0444e1`. No inspection algorithm,
Library-Noah API, recipe arithmetic, Viewer rendering, UI layout, or explicit
Preview/Publish/Run behavior changed.

## Completed work

- Rewrote the root `README.md` in English around operator value, supported
  inspection workflows, the included Thickness Coupon, a fresh-clone build,
  and a direct tutorial entry point.
- Added `OPENVISIONLAB_3D_USER_TUTORIAL.md` with a complete first-run workflow:
  open recipe, review source, select a step, Preview, Publish, Run all, review
  results, save/reopen, and understand Good/Bad/Held-out validation roles.
- Replaced the corrupted development guide with a current English build,
  verification, D-backed evidence, package, CI, and UI-verification guide.
- Updated the system-requirements guide to separate operator runtime, source
  build, and full verification utilities and to document short checkout and
  NuGet-cache paths.
- Added `Build` and `FullVerification` scopes to
  `setup-development-environment.ps1`. The default remains
  `FullVerification`; Build does not require Python.
- Fixed setup PATH refresh so process-only entries are preserved instead of
  being replaced by only Machine and User PATH values.
- Added a dedicated Windows package quick start. The publish script now copies
  it as package `README.md` and copies the user tutorial and system requirements
  under `documentation`.
- Initialized the project-local Proofline issue ledger, resolved the exact
  GitHub publication and clean-clone objective as `PL-0001`, and recorded the
  separate Runner help exit defect as `PL-0002`. That defect is now resolved;
  see `OPENVISIONLAB_3D_RUNNER_HELP_EXIT_20260804.md`.

## Acceptance criteria and verification

| Criterion | Result |
| --- | --- |
| Public README is current, English, user-centered, and locally navigable | Pass: changed public docs contain no suspicious replacement sequences; source local links `12/12` |
| A first-time tutorial covers one complete included inspection | Pass: package/source tutorial hashes match and the Thickness recipe plus its adjacent C3D source are distributed |
| Operator and developer utility requirements are separated | Pass: Build check `4/4`; FullVerification check `5/5`; InstallMissing Build mode `4/4` with no installation needed |
| Setup PATH refresh preserves temporary process entries | Pass: sentinel remained present after both Build and FullVerification checks |
| Exact GitHub revision is independently available | Pass: local implementation commit, `origin/main`, and a new HTTPS clone all resolve to `f7e43d96e8a5ff95206e3b703cd6e125fb0444e1`; clone clean; tracked files `929`; tracked private fixture paths `0` |
| Current source restores and builds without an adjacent Library-Noah checkout | Pass in the clean GitHub clone: six required vendored package/configuration files present; external Noah project/config references `0`; Release build `0` warnings and `0` errors |
| Package documentation is self-contained | Pass: source/package documentation hash pairs `3/3`; required package documents present; package Markdown has no local repository link dependency |
| Self-contained package is internally consistent | Pass from the clean implementation commit: manifest commit equals clone commit; manifest working tree clean; payload `502/502`; missing `0`; size mismatch `0`; SHA-256 mismatch `0`; corrupt fixtures distributed `0` |
| Bundled recipe inputs resolve | Pass: `12/12` source references resolve; declared source-hash mismatches `0` |
| Package runs without a valid system .NET root | Pass: invalid `DOTNET_ROOT`, multilevel lookup disabled, packaged Shell command-line verification `31/31`, exit `0` |
| Script syntax and working-tree formatting are controlled | Pass at implementation checkpoint: both changed PowerShell scripts parse; `git diff --check` passes |

UI screenshots were not required because this slice changes documentation and
repository automation only; no application UI, visible string, layout,
navigation, theme, or responsive behavior changed.

## Evidence

```text
D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260804-user-docs-standalone-clone
```

Principal evidence:

- `setup-build.txt`
- `setup-full-verification.txt`
- `setup-install-missing-build.txt`
- `release-build.binlog`
- `markdown-link-verification.txt`
- `package-verification-final.txt`
- `package-shell-command-line-final.txt`
- `package-output\openvisionlab-3d-studio-win-x64`

Clean GitHub-clone evidence:

```text
D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260804-f7e43d9-github-clone
```

- `clone-identity.txt`
- `dependency-boundary-final.txt`
- `setup-build.txt`
- `release-build.binlog`
- `package-verification.txt`
- `package-shell-command-line.txt`
- `markdown-links.txt`

## Boundary and next dependencies

The implementation is committed and published on `origin/main`. The clean
GitHub clone rebuilt the package with `gitWorkingTree=clean` and the exact
implementation commit in its manifest. `PL-0001` is resolved with direct
evidence for remote identity, clean-clone build, package integrity, recipe
references, and self-contained execution. This closure/Proofline record is a
documentation-only follow-up and does not change the validated implementation
files.

`PL-0002` recorded the separate developer-UX defect that Runner `--help`
printed usage but exited `2`. It is now resolved: case-insensitive help exits
`0`, while invalid and incomplete commands retain exit `2`. It remains
separate from the operator package and tutorial workflow.

Human-owner unaided Wide/Compact R0 remains external and is not replaced by
these automated checks.

## Next priorities

1. Human-owner unaided Wide/Compact R0 | Prerequisite: owner operation and evidence | Recommended model: none until evidence exists | Reasoning effort: none

`K-04` acquisition direction and display-only edge orientation are now
Complete. See
`OPENVISIONLAB_3D_ACQUISITION_DIRECTION_AND_EDGE_ORIENTATION_20260804.md`.
`L-13` identified Surface Match pose and separate-score export is also
Complete. See `OPENVISIONLAB_3D_SURFACE_MATCH_POSE_SCORE_EXPORT_20260804.md`.
