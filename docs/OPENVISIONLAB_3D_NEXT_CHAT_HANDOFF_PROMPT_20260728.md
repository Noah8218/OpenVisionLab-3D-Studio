# OpenVisionLab 3D Next-Chat Entry Prompt

Updated: 2026-08-24
Status: Current Luna continuation entry point

Use `gpt-5.6-luna` with `high` reasoning effort and paste the following request
into the next task:

```text
Continue OpenVisionLab 3D Studio work in
C:\Git\OpenVisionLab-3D-Studio using gpt-5.6-luna with high reasoning effort.

Do not begin by guessing from this prompt alone. Reconstruct the current
project context from the repository authorities and live Git state, report the
analysis and ordered priority list, and then continue the first dependency-
ready item unless current evidence changes the order.

Mandatory modes and live orientation

1. Read C:\Users\USER\.codex\AGENTS.md completely, then read the repository
   AGENTS.md completely. The nearest repository instructions own product
   direction and document authority.
2. Load and follow the mandatory Ponytail and Proofline skills named by the
   global instructions. Load issue-ledger, scope-integrity, refactor-proof,
   completion-evidence, or other Proofline skills when their descriptions
   match the actual work.
3. Run:
   - git status --short
   - git log --oneline -5
   - git rev-parse HEAD
   - git rev-parse origin/main
4. Preserve the pre-existing user-owned untracked
   .proofline/issue-drafts/ directory. Do not clean, stage, edit, or delete it.
5. Inventory the documentation with `rg --files docs` and read docs/README.md
   completely as the repository document map. This repository does not
   currently contain docs/LLM_DOCUMENT_INDEX.json; do not invent or depend on
   that file.

Required document reading before implementation

Read every current-authority or active-contract document below completely:

1. AGENTS.md
2. docs/README.md
3. docs/OPENVISIONLAB_3D_MASTER_DEVELOPMENT_WORKFLOW_AND_BACKLOG_20260727.md
4. docs/OPENVISIONLAB_3D_NEXT_SESSION_HANDOFF.md
5. docs/OPENVISIONLAB_3D_NEXT_CHAT_HANDOFF_PROMPT_20260728.md
6. docs/OPENVISIONLAB_3D_FIRST_RELEASE_THREE_PHASE_SPEC_20260821.md
7. docs/OPENVISIONLAB_3D_RELEASE_VERSION_POLICY.md
8. docs/OPENVISIONLAB_3D_VISION_SDK_TOOL_CONTRACT_AND_MIGRATION_BASELINE_20260805.md
9. docs/OPENVISIONLAB_3D_VISION_SDK_PACKAGE_BOUNDARY_20260805.md
10. docs/OPENVISIONLAB_3D_CODE_RULES.md
11. docs/OPENVISIONLAB_3D_DEVELOPMENT_AND_VERIFICATION_GUIDE.md

Read the prior GPT Pro repository analysis completely:

- docs/OPENVISIONLAB_3D_PROJECT_ANALYSIS_20260822.md

Treat it as recorded audit evidence, not the current queue owner. Reconcile
its findings against the master backlog and current handoff. PL-0030,
PL-0034, PL-0035, PL-0036, PL-0037, PL-0038, and PL-0039 are already closed;
do not repeat them merely because the dated analysis originally recommended
them.

Read the completed E-15 dependency chain and latest regression closure when
auditing or continuing this work:

- docs/OPENVISIONLAB_3D_SELECTION_KIND_ROLE_MATRIX_CLOSURE_20260824.md
- docs/OPENVISIONLAB_3D_GRID_CIRCLE_SELECTION_CLOSURE_20260824.md
- docs/OPENVISIONLAB_3D_GRID_POLYGON_SELECTION_CLOSURE_20260824.md
- .proofline/issues/PL-0047.json
- .proofline/issues/PL-0048.json
- .proofline/issues/PL-0049.json
- .proofline/issues/PL-0050.json

Use docs/README.md to classify every other document as current authority,
active product/architecture contract, user/operator material, contributor
guide, completion evidence, historical evidence, or superseded evidence.
Read task-relevant documents completely. Do not load every dated closure into
working context indiscriminately, and never let a dated status or priority
override the master backlog.

Before any E-15 UI implementation, read
C:\Users\USER\.codex\docs\WPF_UI_UX_RULES.md completely. Before any release,
version, package, tag, publication, or deployment planning or mutation, read
C:\Users\USER\.codex\docs\RELEASE_DEPLOYMENT_RULES.md completely. Reading the
release specification is context gathering only and does not authorize release
work.

Required analysis report before editing

After orientation and before changing files, give the user a compact report
that contains all of the following:

1. Live branch, HEAD, origin/main relationship, and worktree boundary.
2. Product identity and evidence-based maturity, naming the master backlog as
   inventory/queue owner and the current handoff as live status owner.
3. The retained operator workflow:
   load -> source quality -> teach -> explicit Preview -> explicit Publish ->
   explicit Run -> evidence -> save/reopen.
4. Completed work that must not be reopened without changed requirements,
   source, environment, or failed current evidence.
5. A dependency-aware priority list split into:
   - executable now;
   - blocked on owner/data/hardware/external prerequisites;
   - deliberately excluded platform scope.
6. The exact E-15 gap found in current schema, Core/Data contracts, E-13
   declaration, Viewer/Workbench authoring, persistence, Runner, SDK API, and
   verification. Distinguish inspected facts from proposals and unknowns.
7. Concrete E-15 outcome, included/excluded scope, acceptance criteria,
   verification/evidence plan, risk, and whether SDK work is actually required.

Current verified baseline

- Current pushed documentation head at handoff creation:
  eb4ddb7d8d0aad8269cb43693ce50e0a9a02c1f4.
- Repair commit 00752b4cedc0a33645a16b0437845650fb6eeddc
  passed the complete GitHub Actions workflow in run 32692639982.
- Documentation/closure commit
  eb4ddb7d8d0aad8269cb43693ce50e0a9a02c1f4 passed the complete workflow in
  run 32693132414. Recheck live Git state instead of assuming these remain HEAD.
- Product version remains 0.1.1-dev. Generic Tool Recipe schema is now 1.7;
  schema 1.6 remains the durable GridCircle version.
  No package, tag, RC, GitHub Release, publication, or deployment was created.
- PL-0049 is resolved. Targeted Tools execution and Filter, Remove Outliers,
  Level Surface, and ROI Crop Workbench Preview readiness use selected-step
  validation. Selected-step errors, incompatible routes, and whole-recipe Run
  remain strict and fail closed.
- PL-0049 evidence:
  - D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\20260824-pl0049-targeted-validation
  - D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\20260824-pl0049-shell-smoke
  - GitHub Actions runs 32692639982 and 32693132414
- The actual Filter Preview/Publish EXE screenshot passed quality at 125% DPI
  with an intersecting recorded window rectangle. Selection 51/51, Teaching
  55/55, affected Tools/Workbench checks, Release build, and full hosted CI
  passed. Do not reopen PL-0049 without new contrary evidence.
- PL-0050 / E-15 is resolved. The GridPolygon authoring/persistence closure is
  recorded in `docs/OPENVISIONLAB_3D_GRID_POLYGON_SELECTION_CLOSURE_20260824.md`
  with issue evidence under `.proofline/issues/PL-0050.json` and D-backed
  evidence under `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\20260824-e15`.

Ordered project priorities

1. Human-owner unaided Wide/Compact R0 | blocked until the owner explicitly
   approves an exact frozen candidate, a fixed package is built and validated,
   and the owner performs the replay | Recommended model: none until the
   prerequisite exists | Reasoning effort: none
2. Representative maximum-C3D memory/load-time qualification | blocked until
   the owner supplies a representative maximum input and accepted process-
   memory/load-time limits | Recommended model: none until supplied |
   Reasoning effort: none
3. PL-0003 remote-retention closure | blocked on GitHub Support processing and
   a fresh authenticated retired-object reachability check | Recommended model:
   none until external state changes | Reasoning effort: none
4. Phase 1 freeze/package/R0 and later release phases | conditional on explicit
   owner approval and the release specification; not authorized by this prompt |
   Recommended model: none until approved | Reasoning effort: none
5. Physical calibration, traceability, uncertainty, and Gauge R&R evidence |
   blocked until calibration artifacts plus repeated hardware/operator data
   exist | Recommended model: none until supplied | Reasoning effort: none

E-15 is closed; do not re-run it or preselect another feature from a dated analysis.
Re-read the master backlog and choose the next dependency-ready item from the
then-current evidence, then report its Recommended model and Reasoning effort.

Completed E-15 implementation contract (reference only; do not re-run)

Create or update a durable Proofline issue before implementation and define
criteria from current source and the master backlog. Inspect current code and
vendored OpenVisionLab.Vision3D public API before deciding ownership. Do not
assume an inspection tool consumes GridPolygon and do not invent polygon-to-
mask arithmetic in Studio. If a required reusable geometry/mask algorithm is
absent, it belongs in OpenVisionLab-Vision-SDK, followed by committed clean SDK
source, package creation, checksum, vendoring, and Studio adaptation under the
repository contract. If E-15 can close as an authoring/persistence selection
contract without mask generation, state that evidence-based boundary instead
of manufacturing an unowned consumer.

At minimum, prove:

1. A typed, versioned GridPolygon contract with stable identity, exact source/
   frame binding, finite in-grid ordered vertices, deterministic validity
   rules, fail-closed malformed/incompatible handling, and exact save/reopen/
   Runner round-trip.
2. Explicit Viewer/Workbench vertex authoring and editing with visible
   coordinates/order, Apply/Cancel, keyboard recovery, and no automatic
   Preview, Publish, Run, source replacement, or unrelated selection change.
3. E-13 remains the single supported kind/role declaration. Any authoring-only
   pseudo-step is explicit and must not imply an inspection consumer.
4. Mask-output semantics only when an actual owner, typed route, and SDK
   algorithm contract are established. Do not claim calibrated area, physical
   metrology, Gauge R&R, or production suitability.
5. Current-build Wide 1920x1040 and Compact 1280x760 evidence on the dynamically
   selected monitor; all supported themes/layouts; relevant normal, hover,
   actual pressed, focus, selected, disabled, validation-error, mouse-leave,
   popup, and keyboard states; longest plausible coordinates/vertex counts;
   save/reopen; Runner parity; focused smoke; proportional Release build;
   structure guard; and git diff --check. Record every unrun DPI scale as
   unverified.

Product and action boundaries

Preserve OpenVisionLab 3D Studio as a local, file-first, deterministic rule-
based 3D inspection workbench. Preserve source/result separation, Viewer and
docking behavior, semantic themes, stable identity routing, and MVVM ownership.
Camera, lighting, PLC, industrial I/O, robot, cloud, account, deployment,
production-line control, calibrated metrology, package, tag, RC, release, and
publication remain out of scope unless the user separately authorizes them.
Human R0 is not replaced by automated or Codex-observed UI evidence.

Store test-only outputs physically under
D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio and follow the dynamic monitor
rule for every actual EXE launch. Preserve ignored user-owned folders 3D/TLB,
3D/SSD-Black, 3D/fccsp, and 3D/새 폴더. Do not modify
C:\Git\OpenVisionLab_Dev. Do not commit, push, merge, change product version,
package, tag, publish, release, or deploy without separate explicit user
authorization.

Proceed autonomously after the required analysis when the next step is clear,
low-risk, and inside E-15. Finish with exactly one closure state: Complete,
Blocked, or Incomplete. Name commands actually run, evidence paths, affected
files/contracts, remaining boundaries, and the next priority with Recommended
model and Reasoning effort.
```

Private research, vendor comparisons, supplied-media reviews, and former
chronological records are outside the tracked public documentation.
