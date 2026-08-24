# OpenVisionLab 3D Next-Chat Entry Prompt

Updated: 2026-08-24
Status: Current Luna continuation entry point

Use `gpt-5.6-luna` with `high` reasoning effort and paste the following request
into the next task:

```text
Continue OpenVisionLab 3D Studio work in
C:\Git\OpenVisionLab-3D-Studio using gpt-5.6-luna with high reasoning effort.

Orientation and mandatory modes

1. Run git status --short and git log --oneline -5.
2. Read AGENTS.md completely and load the mandatory Ponytail, Proofline
   baseline-quality, and applicable Proofline skills named there.
3. Read docs/README.md,
   docs/OPENVISIONLAB_3D_NEXT_SESSION_HANDOFF.md, and
   docs/OPENVISIONLAB_3D_MASTER_DEVELOPMENT_WORKFLOW_AND_BACKLOG_20260727.md.
4. Read C:\Users\USER\.codex\docs\WPF_UI_UX_RULES.md completely before any
   E-15 UI work.
5. Read the E-13 and E-14 closure documents and their Proofline issues:
   docs/OPENVISIONLAB_3D_SELECTION_KIND_ROLE_MATRIX_CLOSURE_20260824.md,
   docs/OPENVISIONLAB_3D_GRID_CIRCLE_SELECTION_CLOSURE_20260824.md,
   .proofline/issues/PL-0047.json, and .proofline/issues/PL-0048.json.
6. Read .proofline/issues/PL-0049.json before changing its repair.

Before changing files, state the immediate gate, the remaining project
priority, product identity, evidence-based maturity source, retained operator
workflow, and excluded platform scope.

Current baseline

- Commit 00752b4cedc0a33645a16b0437845650fb6eeddc is on origin/main.
- Product version remains 0.1.1-dev and Generic Tool Recipe schema remains 1.6.
- PL-0049 is resolved. Targeted Tools execution and the preprocessing Workbench
  Preview boundary use selected-step validation; selected-step errors and
  whole-recipe Run remain strict and fail-closed.
- Local evidence under
  D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\20260824-pl0049-targeted-validation
  passes the formerly failing command with unchanged SHA-256, selection 51/51,
  teaching 55/55, affected typed adapters, integration 16/16, standard tests
  2/2, structure 68/68, Release 0/0, and git diff --check.
- Actual Filter Preview/Publish EXE evidence and the three sibling preprocessing
  Workbench checks are under
  D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\20260824-pl0049-shell-smoke.
- Hosted CI run 32692639982 passed the complete workflow at the exact repair
  commit in 6m14s. Do not reopen PL-0049 without changed requirements, source,
  environment, or failed current evidence.

Immediate project priority

E-15 GridPolygon selection for irregular masks. Recommended model:
gpt-5.6-luna. Reasoning effort: high.

Before E-15 implementation, create/update its durable Proofline issue and
define concrete acceptance criteria from current source and the master backlog.
Do not assume that any current inspection tool consumes GridPolygon. Inspect
the vendored OpenVisionLab.Vision3D API and the SDK ownership contract before
adding polygon-to-mask geometry; new numerical or geometric algorithms belong
in OpenVisionLab-Vision-SDK when the required public API is absent.

E-15 must, at minimum, prove:

1. A typed, versioned GridPolygon contract with stable identity, source/frame
   binding, finite in-grid vertices, deterministic validity rules, strict
   fail-closed malformed/incompatible handling, and exact save/reopen/Runner
   round-trip.
2. Explicit vertex authoring and editing in Viewer/Workbench with visible
   coordinates and vertex order, Apply/Cancel, keyboard recovery, and no
   automatic Preview, Publish, Run, source replacement, or selection change.
3. Deterministic mask-output semantics and evidence only after ownership is
   established. Do not infer calibrated area, physical metrology, or an
   inspection consumer that the matrix does not declare.
4. E-13 remains the single supported kind/role declaration and fails closed;
   any authoring-only pseudo-step must be explicit.
5. Current-build Wide 1920x1040 and Compact 1280x760 runtime evidence on the
   dynamically selected test monitor, both supported themes/layouts, relevant
   normal/hover/pressed/focused/selected/disabled/error states, popup and
   keyboard paths, longest plausible values, save/reopen, Runner parity,
   focused smoke, proportional Release build, and git diff --check. Record
   unrun DPI scales as unverified.

Preserve the product identity: a local, file-first, deterministic rule-based
3D inspection workbench. Preserve load -> source quality -> teach -> explicit
Preview -> explicit Publish -> explicit Run -> evidence -> save/reopen,
source/result separation, Viewer and docking behavior, semantic themes, and
MVVM ownership. Camera, lighting, PLC, industrial I/O, robot, cloud, account,
deployment, production-line control, calibrated metrology, package, tag, RC,
and release work remain out of scope. Human Wide/Compact R0 is owner-deferred
and is not replaced by automation.

Store test-only outputs physically under
D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio and follow the dynamic
monitor rule for every actual EXE launch. Preserve ignored user-owned folders
3D/TLB, 3D/SSD-Black, 3D/fccsp, and 3D/새 폴더. Do not modify
C:\Git\OpenVisionLab_Dev. Do not commit, push, merge, change product version,
package, tag, publish, release, or deploy without separate explicit user
authorization.

Finish with exactly one closure state: Complete, Blocked, or Incomplete. Name
commands actually run, evidence paths, boundaries, and the next priority with
Recommended model and Reasoning effort.
```

Private research and former chronological records are not part of the tracked
public documentation.
