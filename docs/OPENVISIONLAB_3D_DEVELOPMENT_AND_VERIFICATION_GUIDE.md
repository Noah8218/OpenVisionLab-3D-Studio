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

Do not point the solution at an adjacent `Library-Noah` checkout. The repository
contains the exact vendored `Lib.ThreeD` package and checksum required by the
current source.

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
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-code-structure.ps1
```

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
dotnet run --no-build --project $runnerProject -c Release -- --verify-c3d-map-fidelity --report "$artifactDir\c3d-map-fidelity.txt"
dotnet run --no-build --project $runnerProject -c Release -- --verify-mesh-deviation --report "$artifactDir\mesh-deviation.txt"
dotnet run --no-build --project $runnerProject -c Release -- --verify-nominal-actual-comparison --report "$artifactDir\nominal-actual.txt"
dotnet run --no-build --project $runnerProject -c Release -- --verify-registration-acceptance --report "$artifactDir\registration-acceptance.txt"
dotnet run --no-build --project $runnerProject -c Release -- --verify-library-noah-3d --report "$artifactDir\library-noah-3d.txt"
dotnet run --no-build --project $runnerProject -c Release -- --verify-surface-edge-acquisition-direction --report "$artifactDir\surface-edge-acquisition-direction.txt"
```

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

A local pass does not establish a hosted CI pass. Check the actual GitHub
Actions result after pushing when the user has explicitly authorized a push.

## 12. Completion checklist

- [ ] The approved scope and acceptance criteria are explicit.
- [ ] Build and verification use the current source revision.
- [ ] The smallest relevant focused checks pass.
- [ ] Structural changes pass `verify-code-structure.ps1`.
- [ ] UI changes include fresh Wide and Compact current-build evidence.
- [ ] Algorithm work remains owned by committed Library-Noah source and the
      verified vendored `Lib.ThreeD` package.
- [ ] Test outputs are D-backed on the project workstation, or the fallback is
      recorded.
- [ ] `git diff --check` passes.
- [ ] Unrelated user changes are not staged or overwritten.
