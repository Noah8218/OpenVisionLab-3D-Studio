# OpenVisionLab 3D Studio Development and Verification Guide

This guide is for contributors building and verifying the source. Operators
using the self-contained Windows package should use the root README and the
user tutorial instead.

## 1. Development environment

Required for a normal source build:

- Windows 10 build 19041 or later, or Windows 11, on x64
- Windows PowerShell 5.1 or later
- Git
- .NET 10 SDK `10.0.300` or later in the .NET 10 line
- OpenGL-compatible GPU and current driver for an actual Viewer/Shell run

Python 3.13 is additionally required for the independent C3D and NuGet-health
verification gates. FFmpeg and FFprobe are needed only for approved operator
video evidence.

Check build prerequisites without changing the machine:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\setup-development-environment.ps1 -CheckOnly -Scope Build
```

Check the complete verification environment:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\setup-development-environment.ps1 -CheckOnly -Scope FullVerification
```

Visual Studio is optional. The command-line build is the authoritative path and
does not require a separate IDE installation.

## 2. Clone-path and NuGet-path guidance

Use a short local checkout such as:

```text
C:\src\OpenVisionLab-3D-Studio
```

Deep checkout paths combined with a deep NuGet global-packages directory can
exceed Windows path limits during restore. If restore reports a package path or
file-not-found error for a file that exists in `third_party`, retry with a short
temporary NuGet cache:

```powershell
$env:NUGET_PACKAGES = 'C:\nuget'
dotnet restore OpenVisionLab.ThreeDStudio.sln
```

Do not point the solution at an adjacent `OpenVisionLab-Vision-SDK` checkout.
The repository contains the exact vendored `OpenVisionLab.Vision3D` package
and checksum required by the current source.

## 3. Restore and build

Debug:

```powershell
dotnet restore OpenVisionLab.ThreeDStudio.sln
dotnet build OpenVisionLab.ThreeDStudio.sln -c Debug -p:Platform="Any CPU"
```

Release:

```powershell
dotnet restore OpenVisionLab.ThreeDStudio.sln
dotnet build OpenVisionLab.ThreeDStudio.sln -c Release -p:Platform="Any CPU"
```

For code-ownership and algorithm-boundary changes, also run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-vision-sdk-package.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-code-structure.ps1
```

Run the conventional facade for the two selected public Data verifiers:

```powershell
dotnet test --project tests\OpenVisionLab.ThreeD.Data.Tests\OpenVisionLab.ThreeD.Data.Tests.csproj -c Release --no-build --no-restore --minimum-expected-tests 2
```

The repository `global.json` selects Microsoft Testing Platform for .NET 10.
This facade improves standard discovery; it does not replace the broader
custom Runner, Shell, Viewer, and script verification catalog below.

## 4. Run the applications

Normal Inspection Workbench:

```powershell
dotnet run --no-build --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Release
```

Standalone Viewer host:

```powershell
dotnet run --no-build --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Release
```

Headless Runner commands use the Runner project:

```powershell
$runnerProject = 'src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj'
```

## 5. Test-output storage

On the project workstation, store verification reports, screenshots, recordings,
and generated test data physically under:

```text
D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio
```

Route `TEMP` and `TMP` there for large local verification when practical. A
machine without `D:` may use an available temporary location, but the fallback
must be recorded with the evidence. Do not move source, product dependencies,
documentation, or user datasets under the test-output rule.

Example focused-report folder:

```powershell
$artifactDir = 'D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\verification\local-workbench'
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null
```

## 6. Focused Workbench verification

Build Release first, then use `--no-build` for the smallest relevant checks:

```powershell
dotnet run --no-build --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Release -- --verify-tool-recipe-selections "$artifactDir\tool-recipe-selections.txt"
dotnet run --no-build --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Release -- --verify-source-acquisition-provenance "$artifactDir\source-acquisition-provenance.txt"
dotnet run --no-build --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Release -- --verify-tool-height-measurement-workbench "$artifactDir\height-measurement-workbench.txt"
dotnet run --no-build --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Release -- --verify-inspection-workspace-selection "$artifactDir\inspection-workspace-selection.txt"
dotnet run --no-build --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Release -- --verify-validation-set "$artifactDir\validation-set.txt"
dotnet run --no-build --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Release -- --verify-workbench-docking "$artifactDir\workbench-docking.txt"
dotnet run --no-build --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Release -- --verify-shell-smoke-command-line "$artifactDir\shell-command-line.txt"
```

Do not copy historical pass counts into a current completion claim. Record the
actual output from the current source and current command.

The current Validation Set verifier succeeds only at exactly `87/87`. Its
no-leakage matrix changes only the Held-out value and identity, then requires
the complete development candidate, limit, ranking, warning, confusion, and
sample-decision fingerprint to remain byte-identical. The hosted workflow
already invokes this exact verifier; a separate no-leakage test command is not
required.

The hosted Workbench gate also checks the exact current Inspection Workspace
report total so a passing but incomplete cross-view selection matrix fails CI.

The acquisition/source provenance verifier also covers the optional structured
SensorToScene direction: normalization, exact source frame, save/reopen,
legacy missing-direction fallback, invalid/zero-vector rejection, source
change isolation, draft/reset behavior, and the absence of Preview, Publish,
Run, or Validation execution. See
`OPENVISIONLAB_3D_ACQUISITION_SOURCE_PROVENANCE_20260804.md` and
`OPENVISIONLAB_3D_ACQUISITION_DIRECTION_AND_EDGE_ORIENTATION_20260804.md`.

## 7. Runner and algorithm verification

Representative focused commands:

```powershell
dotnet run --no-build --project $runnerProject -c Release -- --verify-source-quality-report --report "$artifactDir\source-quality-edge-fixtures.txt"
dotnet run --no-build --project $runnerProject -c Release -- --verify-c3d-completeness-grid --report "$artifactDir\completeness-grid.txt"
dotnet run --no-build --project $runnerProject -c Release -- --verify-c3d-map-fidelity --report "$artifactDir\c3d-map-fidelity.txt"
dotnet run --no-build --project $runnerProject -c Release -- --verify-mesh-deviation --report "$artifactDir\mesh-deviation.txt"
dotnet run --no-build --project $runnerProject -c Release -- --verify-nominal-actual-comparison --report "$artifactDir\nominal-actual.txt"
dotnet run --no-build --project $runnerProject -c Release -- --verify-registration-acceptance --report "$artifactDir\registration-acceptance.txt"
dotnet run --no-build --project $runnerProject -c Release -- --verify-vision-sdk-3d --report "$artifactDir\vision-sdk-3d.txt"
dotnet run --no-build --project $runnerProject -c Release -- --verify-surface-edge-acquisition-direction --report "$artifactDir\surface-edge-acquisition-direction.txt"
dotnet run --no-build --project $runnerProject -c Release -- --verify-oriented-box-3d --report "$artifactDir\oriented-box-3d.txt"
dotnet run --no-build --project $runnerProject -c Release -- --verify-c3d-filter --report "$artifactDir\median-filter.txt"
dotnet run --no-build --project $runnerProject -c Release -- --verify-c3d-remove-outliers --report "$artifactDir\remove-outliers.txt"
dotnet run --no-build --project $runnerProject -c Release -- --verify-c3d-level-surface --report "$artifactDir\level-surface.txt"
dotnet run --no-build --project $runnerProject -c Release -- --verify-c3d-roi-crop --report "$artifactDir\roi-crop.txt"
```

The OrientedBox command is complete only when its report contains both exact
lines below. The named eleven-case set covers schema `1.4` and current-schema
acceptance, rotated exact reopen, old/mixed rejection, and zero, non-unit,
parallel, left-handed, non-finite, and non-positive geometry rejection.

```text
OrientedBox3DContractVerification|PASS|cases=11|passed=11|failed=0
GridCircleContractVerification|PASS|cases=9|passed=9|failed=0
Result: Pass (49/49 checks)
```

The preparation source-immutability qualification covers exactly the current
Prepare catalog; Transform tools are excluded. Require exact source path,
length, SHA-256, retained source values/counts, separate derived output/root
provenance, and these complete report markers:

- `C3DMedianFilterGoldenVerification|Pass|cases=13|passed=13|failed=0`;
- `C3DRemoveOutlierPixelsGoldenVerification|PASS|cases=9|passed=9|failed=0`;
- `C3DLevelSurfaceGoldenVerification|PASS|cases=9|passed=9|failed=0`;
- `C3DRoiCropGoldenVerification|PASS|cases=6|passed=6|failed=0`.

The existing CI preparation step also requires each new evidence marker and
the aggregate
`PreparationSourceImmutabilityVerification|PASS|tools=4|passed=4|failed=0`.
An omitted, partial, or reverted tool report must fail the gate.

The Completeness command is complete only when its report contains
`C3DCompletenessGridGoldenVerification|PASS|cases=31|passed=31|failed=0`.
This protects the exact known-cell matrix and the persisted Source Quality
grid-diagnostic CSV projection from a partial or stale passing report while
retaining the existing Runner verifier as the single owner.

For K-04, also generate the existing surface-edge diagnostic/review fixtures
and run `--verify-surface-edge-diagnostic-review-workbench-parity`. The gate
must prove that changing direction removes only stale orientation evidence;
the raw overlay, surface/edge score, and assessment identities must remain
unchanged.

Run a specific recipe:

```powershell
dotnet run --no-build --project $runnerProject -c Release -- --recipe <recipe.ov3d-recipe.json> --report "$artifactDir\recipe-run.txt"
```

Viewer/Runner parity claims require the same source identity, recipe, declared
unit, and coordinate/frame contracts. A similar-looking input is not parity
evidence.

## 8. Data-loading checks

Run the public loading matrix:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\run-data-loading-matrix-smoke.ps1
```

Probe one included sample:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\probe-3d-sample.ps1 `
  -SamplePath 3D\PublicSamples\PointCloud\interesting.las `
  -ArtifactDir "$artifactDir\probe-las"
```

The public sample inventory and attribution are in
`3D\PublicSamples\README.md`.

## 9. Self-contained package

Build the operator package:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publish-windows-app.ps1
```

On the project workstation, keep generated package and build output physically
under the D-backed test root without changing the repository `artifacts`
junction:

```powershell
$releaseRoot = 'D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\<release-evidence-id>'
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publish-windows-app.ps1 `
  -OutputRoot $releaseRoot `
  -BuildArtifactsPath (Join-Path $releaseRoot 'dotnet-build')
```

`-OutputRoot` always creates or replaces only its fixed
`openvisionlab-3d-studio-win-x64` child. Do not combine it with
`-OutputDirectory`. Existing callers that omit `-OutputRoot` retain the
repository-local default below.

The default package path is:

```text
artifacts\release\openvisionlab-3d-studio-win-x64
```

Confirm that the package contains the Shell executable, the tutorial recipe and
source, `README.md`, `documentation\USER_TUTORIAL.md`, `LICENSE`, `NOTICE`, and
`openvisionlab-3d-studio-manifest.json`. Verify the manifest payload count,
sizes, and SHA-256 values before distribution.

## 10. UI and media verification

Any UI, layout, visible text, localization, docking, or responsive change must
be checked in a current build at both supported sizes:

- Wide: 1920 × 1040
- Compact: 1280 × 760

Capture fresh before and after evidence. Check required text, overlap,
clipping, unreachable controls, popup/theme states, and unintended nested or
horizontal scrolling. Actual desktop EXE captures must use the active leftmost
monitor. Documentation-only changes do not require UI screenshots.

For OrientedBox pointer qualification, launch the current Release Shell apphost
with the existing sample and run the same command once per supported size:

```powershell
& $shellExe `
  --smoke-software-rendering `
  --tool-teaching-recipe 3D\Samples\ThicknessCouponV1\oriented-box-demo.ov3d-recipe.json `
  --tool-teaching-step step.oriented-box-authoring.01 `
  --smoke-oriented-box-pointer-report $pointerReport `
  --shell-smoke-leftmost `
  --shell-smoke-width $width `
  --shell-smoke-height $height `
  --shell-smoke-screenshot $screenshot `
  --shell-screenshot-quality-report $qualityReport
```

Require every gesture row, all interaction-state booleans, exact marker
`OrientedBox3DPointerVerification|PASS|gestures=7|projections=3|handlesPerProjection=8|actualWindowsPointer=true|hoverLeaveRecovery=True`, accepted screenshot
quality, and `intersects=True`. A pass at one size does not replace the other.

For an explicitly requested operator video, first verify the workflow through
the fast deterministic path, then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\run-operator-video-self-review.ps1
```

Operator media must show only the application window and must not expose the
desktop, taskbar, unrelated applications, account information, notifications,
or local paths.

## 11. CI scope

`.github\workflows\ci.yml` runs the Windows restore/build, vendored-package
health, structural guards, Viewer/Shell checks, Runner goldens, data-loading
checks, independent Python gates, and selected verification-report uploads.
Its existing typed-preparation step aggregates all four exact Prepare reports
and fails on an omitted, partial, or source-identity-incomplete report.
The OrientedBox step requires the exact named `11/11` subset plus the shared
`32/32` result; Workbench and Shell retain the authoring/round-trip and pointer-
routing gates. Actual desktop pointer evidence remains a local current-Release
gate rather than a hosted headless claim.

A local pass does not establish a hosted CI pass. Check the actual GitHub
Actions result after pushing when the user has explicitly authorized a push.

## 12. Completion checklist

- [ ] The approved scope and acceptance criteria are explicit.
- [ ] Build and verification use the current source revision.
- [ ] The smallest relevant focused checks pass.
- [ ] Structural changes pass `verify-code-structure.ps1`.
- [ ] UI changes include fresh Wide and Compact current-build evidence.
- [ ] Algorithm work remains owned by committed OpenVisionLab-Vision-SDK source
      and the verified vendored `OpenVisionLab.Vision3D` package.
- [ ] Test outputs are D-backed on the project workstation, or the fallback is
      recorded.
- [ ] `git diff --check` passes.
- [ ] Unrelated user changes are not staged or overwritten.
