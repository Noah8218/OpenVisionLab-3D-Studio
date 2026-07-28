# Open3D Clean-Host Execution Protocol - 2026-07-16

## Scope And Decision Boundary

This runbook defines the evidence required to evaluate the separate-process Open3D registration candidate on an isolated Windows x64 host. It is an execution protocol, not clean-host evidence by itself.

Do not add Open3D or VTK to the product, change the Viewer or Runner, distribute a bundle, or claim deployment readiness from this document. The protocol applies only to the current `USE_SYSTEM_VTK=ON` Open3D `0.19.0` candidate described in `docs/OPENVISIONLAB_3D_OPEN3D_VTK_CANDIDATE_RUNTIME_20260716.md`.

## Required Host Conditions

1. Use an isolated Windows x64 VM or a restored disposable snapshot. Record the OS edition, build, architecture, VM/snapshot identity, UTC start time, and operator.
2. Do not copy VC/OpenMP DLLs next to the staged probe. The preflight rejects `MSVCP140.dll`, `VCRUNTIME140.dll`, `VCRUNTIME140_1.dll`, and `VCOMP140.dll` beside the probe.
3. Before installation, the staged `tools\verify-open3d-runtime-prerequisites.ps1` must return exit `1`. Capture its report. If it passes before installation, the host is not valid clean-host prerequisite evidence.
4. Record the pre-install probe launch result without inventing a loader exit code. A successful pre-install registration report means the host is not valid clean-host prerequisite evidence.
5. Use only an installer whose file version, SHA-256, Authenticode signature, source URL, and applicable REDIST terms were reviewed before the run. The current 2026-07-16 installer identity is recorded in `docs/OPENVISIONLAB_3D_OPEN3D_DISTRIBUTION_AUDIT_20260713.md`; revalidate it rather than assuming it remains current.

## Fixed Candidate Inputs

Stage the candidate, verifier, fixed baseline, and public alignment golden in separate directories. Record the copied-file hashes again on the clean host.

| Input | Size | SHA-256 |
| --- | ---: | --- |
| `open3d-registration-probe.exe` | `70,656` | `a4425b268bf7f7ab0ff58ad0e006f878a023a223cce7df6bfab5e363a66293fb` |
| `Open3D.dll` | `58,220,032` | `88ab8ee38f218c9ba02a929f07462554d555e0c131d771a5343777445ec2e19f` |
| `tbb12.dll` | `328,704` | `2785a4688dcb2aa197a49b4be7a94b471a1fb951bd8efe46476f5ba95ad449e3` |
| `tools\\verify-open3d-runtime-prerequisites.ps1` | `4,842` | `6e5b1c2cbf62cf6998340207ed73d5965281b6dd0563ccf170a4c8115210fbc5` |
| `baseline\\candidate_repeat_1.json` | `793` | `8eae43d0bb64f7047a241a3a681aa9f9c60124820d1c955e79893e23151ee72f` |
| `cloud_bin_0.pcd` | `6,362,965` | `e1e100802c29ef454c6b523084668ee0e2f365ec52eaeebe79ae804c20447b15` |
| `cloud_bin_1.pcd` | `4,410,901` | `a4c3dc0ad7b1279736491b9b2638991d4c808605997be4f9ab174c24a9fa6e52` |
| `init.log` | `385` | `609896dbdd666b7ae0bb7390c52730ca8aca10c2b5886b895acdb36f4a202156` |

The point-cloud inputs are external alignment goldens, not physical measured/nominal evidence.

## Procedure

### 1. Stage And Record Baseline

Use a directory outside the product source and release bundle, for example:

```text
C:\OVL3D-CleanHost\bundle
C:\OVL3D-CleanHost\input
C:\OVL3D-CleanHost\tools
C:\OVL3D-CleanHost\baseline
C:\OVL3D-CleanHost\evidence
```

Build the self-contained staging directory before copying it to the isolated host. The builder verifies the fixed source hashes, refuses an existing output directory, and excludes Microsoft runtime sidecars:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\stage-open3d-clean-host-evidence-bundle.ps1 `
  -OutputDirectory artifacts\open3d-clean-host-evidence-bundle-20260716
```

Copy the generated `bundle`, `input`, `tools`, `baseline`, and `instructions` directories to `C:\OVL3D-CleanHost`; the reviewed installer is intentionally separate.

Run the preflight before installing the Microsoft prerequisite:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\OVL3D-CleanHost\tools\verify-open3d-runtime-prerequisites.ps1 `
  -RuntimeDirectory C:\OVL3D-CleanHost\bundle `
  -MinimumRuntimeVersion 14.44.35211.0 `
  -ReportPath C:\OVL3D-CleanHost\evidence\pre-install-prerequisites.txt
```

Expected result: exit `1`, `bundle=3/3`, `adjacentRuntime=0/4`, and missing prerequisite evidence. Record the process exit code, report, standard output, standard error, and whether `pre-install-probe.json` was created. Do not normalize a host that already passes or produces a successful probe report as clean-host evidence.

### 2. Install And Handle Restart State

Run the reviewed Microsoft installer with the documented unattended options. Preserve the exact command, installer hash, signature result, installer log, exit code, and pending-restart state:

```powershell
& C:\OVL3D-CleanHost\installer\vc_redist.x64.exe /install /quiet /norestart /log C:\OVL3D-CleanHost\evidence\vc-redist-install.log
$installerExitCode = $LASTEXITCODE
```

Do not reinterpret an installer failure as a probe failure. If the installer or Windows reports that a restart is required, restart the isolated host, record the action, and continue only after the restart. Do not use a rebooted result to replace the pre-install evidence.

### 3. Verify Installed Prerequisites

After installation and any required restart, rerun the same preflight:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\OVL3D-CleanHost\tools\verify-open3d-runtime-prerequisites.ps1 `
  -RuntimeDirectory C:\OVL3D-CleanHost\bundle `
  -MinimumRuntimeVersion 14.44.35211.0 `
  -ReportPath C:\OVL3D-CleanHost\evidence\post-install-prerequisites.txt
```

Expected result: exit `0`, `bundle=3/3`, `adjacentRuntime=0/4`, `system=4/4`, and an installed runtime version no lower than `14.44.35211.0`.

### 4. Run The Fixed Registration Probe

Run the current valid `0 -> 1` DemoICP pair only. The probe usage is:

```text
open3d-registration-probe <source.pcd> <target.pcd> <init.log> <report.json> [log-record-index]
```

```powershell
& C:\OVL3D-CleanHost\bundle\open3d-registration-probe.exe `
  C:\OVL3D-CleanHost\input\cloud_bin_0.pcd `
  C:\OVL3D-CleanHost\input\cloud_bin_1.pcd `
  C:\OVL3D-CleanHost\input\init.log `
  C:\OVL3D-CleanHost\evidence\post-install-probe.json `
  0
$probeExitCode = $LASTEXITCODE
```

Expected result: exit `0`, a parseable JSON report, source/target counts `198,835 / 137,833`, refined correspondences `123,483`, fitness `0.621032514396`, and inlier RMSE `0.006565226689`. The staged `baseline\candidate_repeat_1.json` provides the current baseline transformation and all non-timing fields.

Compare the result to that baseline after removing only `elapsedMilliseconds`:

```powershell
function Get-NormalizedProbeJson([string]$Path) {
  $value = Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
  $value.PSObject.Properties.Remove('elapsedMilliseconds')
  $value | ConvertTo-Json -Depth 8 -Compress
}

$actual = Get-NormalizedProbeJson 'C:\OVL3D-CleanHost\evidence\post-install-probe.json'
$baseline = Get-NormalizedProbeJson 'C:\OVL3D-CleanHost\baseline\candidate_repeat_1.json'
if ($actual -cne $baseline) {
  throw 'Clean-host probe differs from the fixed candidate baseline.'
}
```

## Acceptance And Abort Rules

| Condition | Decision |
| --- | --- |
| Pre-install preflight fails and no successful probe report exists | Continue to reviewed installer step |
| Pre-install preflight passes or the probe succeeds | Reject as clean-host evidence; host was not clean for this gate |
| Installer fails or required restart is not completed | Stop; do not claim post-install evidence |
| Post-install preflight fails, finds sidecar runtime DLLs, or reports an old runtime | Stop; do not run product integration work |
| Probe exits nonzero, report is absent, or normalized JSON differs | Stop; record the discrepancy and keep runtime integration blocked |
| All rows pass and the evidence archive is complete | Clean-host prerequisite gate has evidence only; distribution approval and product integration remain separate gates |

## Required Evidence Archive

Record all of the following under an ignored, date-stamped artifact directory:

1. Host identity, snapshot identity, operator, UTC timestamps, and source/bundle input hashes.
2. Pre-install preflight report, process output, exit code, and probe-launch outcome.
3. Installer file hash, signature evidence, command, installer log, exit code, and restart evidence.
4. Post-install preflight report, process output, and exit code.
5. Post-install probe JSON, normalized baseline comparison result, standard output/error, and exit code.
6. A concise summary that labels the result as clean-host prerequisite evidence only.

## Executed Windows Sandbox Evidence - 2026-07-16

The protocol was executed once in an isolated Windows Sandbox guest, not on the development host. The generated `.wsb` disabled networking and vGPU, mapped the nine-file payload, installer, and runner script read-only, and exposed only the dated evidence directory as writable. The guest reported Windows 10 Enterprise build `19041`, x64, under `WDAGUtilityAccount`.

| Check | Observed result |
| --- | --- |
| Guest payload integrity | `9/9` fixed manifest files passed their size/SHA-256 checks; no adjacent VC/OpenMP runtime DLL was present. |
| Pre-install prerequisite | Exit `1`, `bundle=3/3`, `adjacentRuntime=0/4`, `system=0/4`, and no VC x64 runtime registry key. |
| Pre-install probe | Exit `-1073741515`; no `pre-install-probe.json` was created. |
| Reviewed installer | Microsoft-signed `vc_redist.x64.exe` `14.51.36247.0`, exact size/SHA-256/signature; `/install /quiet /norestart` exited `0` without a restart requirement. |
| Post-install prerequisite | Exit `0`, `bundle=3/3`, `adjacentRuntime=0/4`, `system=4/4`, installed version `14.51.36247.0`. |
| Fixed `0 -> 1` registration probe | Exit `0`, report present, and normalized JSON exactly matched `candidate_repeat_1.json` after removing only `elapsedMilliseconds`. |

Ignored evidence is under `artifacts/windows-sandbox-clean-host-20260716-run2/evidence`, including the guest summary, prerequisite reports, installer logs, probe JSON, normalized parity record, and host cleanup record. The disposable Sandbox guest's shutdown request did not terminate its outer host processes, so the completed host orchestrator now explicitly terminates the exact `WindowsSandbox.exe` parent and its directly spawned `WindowsSandboxClient.exe` child after the guest summary is persisted. The recorded run then terminated its verified Sandbox PIDs with no remaining Sandbox process.

`scripts/invoke-open3d-clean-host-sandbox.ps1` prepares this bounded configuration, verifies the payload and installer both before and inside the guest, waits for a completed summary, and cleans up only the launched Sandbox PID. Its host and generated guest scripts passed PowerShell AST parsing before execution.

This closes the technical clean-host VC/OpenMP prerequisite gate for this fixed candidate and fixed `0 -> 1` probe only. It does not approve redistribution, satisfy final notice/legal review, add Open3D to the product, prove Viewer/Runner result mapping, or establish metrology accuracy.

## Remaining Boundary

A passing run does not authorize redistribution, replace notice/legal review, prove arbitrary registration recovery, establish Viewer/Runner result mapping, or establish physical calibration or metrology accuracy.
