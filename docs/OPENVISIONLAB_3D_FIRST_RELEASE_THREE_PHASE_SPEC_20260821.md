# OpenVisionLab 3D First Release Three-Phase Development Specification

Updated: 2026-08-21
Status: Current
Owner issue: `PL-0029`

## 1. Decision

OpenVisionLab 3D Studio is ready to start first-release qualification, but it
is not ready for an immediate public stable release.

The first release will proceed through three gates:

1. Phase 1 — internal development freeze and owner qualification;
2. Phase 2 — limited release candidate distribution;
3. Phase 3 — first public stable release.

No phase is skipped. A later phase starts only after every exit criterion of
the previous phase passes against one exact source commit and package identity.

## 2. Product And Release Scope

The release target is the current local, file-first, deterministic rule-based
3D inspection workbench for identified height fields, point clouds, and meshes.
It includes source review, teaching, explicit Preview/Publish/Run, validation,
Viewer evidence, Run Records, recipe save/reopen, and the privacy-safe support
bundle described by the root README.

The first release does not include camera acquisition, lighting, PLC or
industrial I/O, robot integration, cloud accounts, production-line control,
calibrated metrology certification, or an installer. Distribution remains a
self-contained Windows x64 folder package. Raw-height and synthetic results
must not be advertised as calibrated physical measurement.

## 3. Current Self-Evaluation

Evidence baseline: `main` commit `60f86973a117674017f79317e9219b13433a5491`
and GitHub Actions CI run `32434992738` (`#93`, success).

| Area | Assessment | Current evidence | Release consequence |
| --- | --- | --- | --- |
| Supported operator workflow | Candidate-ready | README, tutorial, explicit Preview/Publish/Run, save/reopen, Results and support-bundle contracts | Freeze the documented workflow; do not add unrelated features during qualification |
| MVVM and code ownership | Ready for qualification | `PL-0026` M1-M7 complete; structure `67/67`; concrete ViewModel, execution-owner, session, service, command, converter, and behavior boundaries | No broad refactor is required before Phase 1 |
| Numerical ownership | Ready for current software claims | Studio numerical migration debt is zero; committed and vendored `OpenVisionLab.Vision3D 3.0.0` owns reusable algorithms | Preserve package identity and do not copy algorithm arithmetic back into Studio |
| Automated verification | Ready baseline | Current hosted Windows CI `#93` passed | Re-run the full release gate on the exact frozen candidate |
| Windows package path | Partial | `-OutputRoot` produced a D-backed self-contained preflight package with 506 verified manifest entries; its manifest truthfully records a dirty tree | Commit the approved Phase 1 changes, then generate and qualify the clean frozen package |
| Human usability acceptance | Blocked | Current fixed inputs pass Wide/Compact `-ValidateOnly`, but unaided owner R0 has not passed | Blocks Phase 2 and all release-acceptance claims |
| Publication | Not started | Product version is `0.1.1-dev`; current GitHub release count and tag count are both zero | No public link or released-version claim is allowed yet |
| Physical/production credibility | Outside current claim | Current evidence is software and raw-height/synthetic evidence | Do not claim calibrated metrology, Gauge R&R, or production approval |

### Current blockers and risks

- Product-owner unaided Wide and Compact R0 is required before a release
  candidate can be approved.
- The repository `artifacts` junction still targets
  `E:\OpenVisionLab-3D-Studio\artifacts`. The packaging script now accepts an
  explicit `-OutputRoot`, so new package and build evidence can be written
  directly under `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio` without
  moving or deleting existing data.
- `PL-0003` remains externally blocked on GitHub Support processing and a fresh
  retired-object reachability check. Phase 3 requires its resolution or an
  explicit owner risk decision.
- Code signing is not established by current evidence. Before Phase 3, the
  owner must choose a signed distribution or approve a clearly documented
  unsigned ZIP and its Windows warning implications.

## 4. Phase 1 — Internal Freeze And Owner Qualification

### Goal

Produce one reproducible internal `0.1.1-dev` qualification package, complete
the automated release gate, and obtain unaided owner acceptance without
creating a tag or GitHub Release.

### Included

- freeze current supported behavior and defer unrelated feature work;
- resolve the new-artifact physical storage path before generating evidence;
- confirm clean source, exact commit, central version, dependency identities,
  current documentation, and zero tracked/loose DLL-only dependencies;
- build the Release solution and self-contained Windows x64 package;
- verify package manifest, payload count, required files, licenses, samples,
  documentation, file sizes, and SHA-256 values;
- run the release-policy build, Viewer host, structure, Vision SDK package,
  NuGet health, data-loading, Runner/golden, map-fidelity, and compatibility
  gates on the same commit;
- run Wide and Compact `-ValidateOnly`, then the product-owner's unaided Wide
  and Compact R0 on that exact package;
- record defects by severity and allow only release-blocking corrections after
  the freeze.

### Entry criteria

- current branch and remote commit are identified;
- hosted CI for that commit passed;
- the tracked worktree contains only approved Phase 1 changes;
- the physical artifact destination satisfies workstation storage rules.

### Exit criteria

- one clean candidate commit and package identity are frozen;
- all applicable release-policy checks pass on that exact commit;
- package manifest and an archive SHA-256 are recorded;
- Wide and Compact unaided owner R0 both pass;
- no unresolved critical or high release defect remains;
- the owner explicitly approves moving to Phase 2.

### Output

An internal qualification record and immutable package path. There is no tag,
GitHub Release, public version claim, or external distribution in Phase 1.

### Execution status

`Doing`. The current repository/CI/version assessment, three-phase contract,
D-backed output route, and preflight package are complete. A clean frozen
package, full release gate, and owner R0 remain.

Phase 1 preflight on 2026-08-21 passed the full-verification environment check
(`5/5` required tools), `PL-0029` schema validation, changed-document local
links (`0` missing), and `git diff --check`. Evidence is stored under
`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260821-pl0029-phase1-preflight`.
The environment checker also wrote its fixed repository-local report through
the existing E-backed `artifacts` junction. Existing E data was left unchanged.

`publish-windows-app.ps1 -OutputRoot` then created the D-backed package under
`20260821-pl0029-phase1-package-preflight`. Release publish exited `0`; required
application files passed `11/11`; all `506` manifest entries matched their
actual lengths and SHA-256 values; the package contains `507` files and
`242,409,450` bytes. The manifest SHA-256 is
`AAD83CBEEC0F1D63C617F808EA081A3E18AD79E31A7BE5C722BFDE13B6B58320`.
The manifest correctly records commit `60f8697`, version `0.1.1-dev`, and a
dirty working tree, so this proves the output route and package composition but
is not the frozen Phase 1 candidate. A direct `dotnet Shell.dll --help` probe
was stopped after it entered the interactive WPF path; packaged EXE/R0 evidence
remains unverified.

Recommended model: `gpt-5.6-sol`
Reasoning effort: `medium`

## 5. Phase 2 — Limited Release Candidate

### Goal

Create an owner-approved `0.1.1-rc.1` candidate and validate the packaged
operator workflow outside the development checkout before public stable
publication.

### Entry criteria

- every Phase 1 exit criterion passed;
- the owner approved the RC version change and limited distribution;
- the exact candidate source and package are clean and reproducible.

### Required work

- change the central product version to `0.1.1-rc.1` only;
- update the changelog and release notes for the frozen scope;
- repeat the complete release gate and hosted CI on the exact RC commit;
- create a prerelease tag and GitHub prerelease only after explicit publication
  approval;
- verify extraction, first launch, included tutorial, Preview, Publish, Run all,
  Results, recipe save/reopen, Run Record, and privacy-safe support export on at
  least one clean Windows environment separate from the development checkout;
- collect limited tester defects without adding unrelated features.

### Exit criteria

- the downloaded RC asset matches the frozen archive size and SHA-256;
- the clean-environment operator path passes;
- no unresolved critical or high defect remains;
- medium defects have an explicit fix or accepted deferral;
- the owner explicitly approves stable promotion.

Recommended model: `gpt-5.6-sol`
Reasoning effort: `medium`

## 6. Phase 3 — First Public Stable Release

### Goal

Publish `0.1.1` as the first stable OpenVisionLab 3D Studio release with exact
public readback and truthful capability boundaries.

### Entry criteria

- every Phase 2 exit criterion passed;
- `PL-0003` is resolved or the owner records an explicit publication-risk
  decision;
- the signing/unsigned-distribution decision is recorded;
- stable publication is explicitly approved.

### Required work

- remove the prerelease suffix centrally to produce `0.1.1`;
- permit only RC fixes and release-document corrections;
- run the full release gate and hosted CI again on the stable commit;
- create tag `v0.1.1` and a GitHub Release with the self-contained Windows ZIP,
  release notes, requirements, SHA-256, license/notice retention, and known
  limitations;
- download the public asset and verify version, commit, manifest, size,
  SHA-256, package contents, and launch identity against the frozen candidate;
- update public documentation from active development to the exact released
  state without claiming calibrated or production approval.

### Exit criteria

- tag, release metadata, and public asset all identify the same clean commit;
- public readback size and SHA-256 match the approved stable package;
- the documented tutorial path runs from the downloaded package;
- all release evidence and known limitations are durable and linked;
- no release-blocking defect remains.

Recommended model: `gpt-5.6-sol`
Reasoning effort: `medium`

## 7. Defect And Change Policy During Qualification

| Severity | Meaning | Phase action |
| --- | --- | --- |
| Critical | data loss, unsafe state mutation, security/privacy exposure, package cannot run | Block the phase and fix before continuing |
| High | supported primary workflow cannot complete or evidence is incorrect | Block the phase and fix before continuing |
| Medium | supported workflow has a bounded workaround or non-critical usability defect | Fix or obtain an explicit deferral before stable promotion |
| Low | cosmetic or non-blocking documentation issue | May be deferred with a recorded next action |

Every correction receives a focused issue and evidence. A fix invalidates the
candidate identity and requires the affected gates plus proportional build,
CI, package, and owner checks to be rerun.

## 8. Approval Boundaries

The following actions always require explicit owner approval:

- moving or deleting existing artifact storage;
- changing `0.1.1-dev` to an RC or stable version;
- creating or pushing a release tag;
- creating a GitHub prerelease or stable Release;
- distributing the package to external testers;
- accepting an unresolved privacy, signing, critical, or high-severity risk.

## 9. Current Completion Record

```text
Status: Incomplete
Scope: Three-phase first-release contract, current readiness assessment, D-backed output route, and dirty-tree package preflight
Acceptance criteria: current state grounded -> pass; phase gates and approval boundaries documented -> pass; D-backed package composition preflight -> pass; clean frozen package and full release gate -> fail; owner Wide/Compact R0 -> fail; RC/stable publication -> not started
Verification: main 60f8697; hosted CI #93 success; product 0.1.1-dev; GitHub releases 0; tags 0; tracked DLL 0; loose DLL 0; full-verification environment 5/5; output boundary checks pass; Release publish exit 0; application files 11/11; manifest entries 506/506; local links missing 0; git diff --check pass
Evidence: this document; PL-0029; release/version policy; current handoff; CI run 32434992738; D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260821-pl0029-phase1-preflight/; D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260821-pl0029-phase1-package-preflight/
Boundary / next dependency: commit the approved Phase 1 source and documentation, generate a clean frozen package, run the full release gate, and complete owner R0; no tag or release is authorized
```
