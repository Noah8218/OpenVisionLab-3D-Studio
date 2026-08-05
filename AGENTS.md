# AGENTS.md

This file defines the active working agreement for Codex in this repository.
Historical project instructions are preserved in
`docs/archive/AGENTS_PROJECT_HISTORY_THROUGH_20260804.md` and are not current
instructions.

## Document Authority

Use one owner for each kind of information:

1. `AGENTS.md` owns stable repository operating rules.
2. `docs/OPENVISIONLAB_3D_MASTER_DEVELOPMENT_WORKFLOW_AND_BACKLOG_20260727.md`
   owns the current capability inventory, dependencies, and development queue.
3. `docs/OPENVISIONLAB_3D_NEXT_SESSION_HANDOFF.md` owns the short current
   handoff and external prerequisites.
4. `docs/OPENVISIONLAB_3D_NEXT_CHAT_HANDOFF_PROMPT_20260728.md` owns only the
   next-chat entry prompt.
5. Dated closure, design, audit, and experiment documents are evidence for
   their recorded scope. They do not override the master backlog.

Do not copy mutable Git status, unpushed-commit claims, or current priority
lists into several documents. Run Git commands and update the owning document.
If documents conflict, surface the conflict and correct the owning document.

## Work Location

- Primary work happens in `C:\Git\OpenVisionLab-3D-Studio`.
- `C:\Git\OpenVisionLab_Dev` is a read-only 2D product-contract and UX
  reference unless the user explicitly authorizes changes there.
- Preserve the ignored user-owned folders `3D/TLB`, `3D/SSD-Black`, `3D/fccsp`,
  and `3D/새 폴더`.
- Do not run `git push` unless the user explicitly requests `PUSH` or an
  equivalent direct push instruction.

## Orientation Before Work

At the start of a new chat, after a handoff, or when asked to continue:

1. Run `git status --short` and `git log --oneline -5`.
2. Read this file, the next-chat prompt, the current handoff, the master
   backlog, and the specific active contract for the requested work.
3. Use `docs/README.md` to locate user, development, architecture, evidence,
   and historical documents.
4. Before implementation, documentation edits, or command-driven follow-up,
   tell the user the immediate priority, remaining project priority, product
   identity, current maturity source, commercial lesson being adapted, and
   commercial-platform scope that remains excluded.

Do not treat a narrow screenshot, smoke test, or old completion record as a
replacement for current product orientation.

## Product Identity And Scope

- OpenVisionLab 3D Studio is a local, file-first, deterministic rule-based 3D
  inspection workbench for identified height fields, point clouds, and meshes.
- The Viewer supports teaching, measurement, comparison, and evidence review;
  it is not the entire product.
- Preserve the operator loop: load -> source quality -> teach -> explicit
  Preview -> explicit Publish -> explicit Run -> evidence -> save/reopen.
- Do not expand into camera, lighting, PLC, industrial I/O, robot, cloud,
  account, deployment, or production-line control unless the user explicitly
  changes product direction.
- Do not present raw-height or synthetic software evidence as calibrated
  physical measurement, Gauge R&R, certified metrology, or production approval.

## Current Product Target

As of the 2026-08-05 documentation consolidation:

- The master backlog's current inventory table is canonical. Read it rather
  than copying its counts into this file.
- Inspection Workspace v3: `7/8`; `A-01` remains Partial.
- GoPxL-inspired Workbench v4: `3/3` complete.
- Current acceptance priority: product-owner unaided Wide and Compact R0.
- No dependency-ready software item is selected while waiting for the owner's
  next product decision. Missing R0 does not globally prohibit a newly
  approved deterministic software slice.
- B-12 acquisition provenance, K-04 acquisition direction/orientation, L-13
  Surface Match pose/score export, and PL-0002 Runner help behavior are
  complete for their documented software scopes.
- Vendored `Lib.ThreeD 2.9.1` is built from committed Library-Noah source
  `9dd95690d3e439b459c39aea99878880cdcc5808`; package SHA-256 is
  `BDE8D2C01B6DC380EF4579C89DE495F06F79BA4864D4229CD5CE87713BD1CA4E`.
- Public-sample remote-retention cleanup remains a separate external
  maintenance blocker tracked by `PL-0003`; do not infer its current GitHub
  state without an authenticated re-audit.

The master backlog owns future inventory changes. Do not append completion
chronology to this file.

## Library-Noah Algorithm Ownership

- All new or changed numerical, geometric, feature-extraction, matching, and
  inspection algorithms belong in `Library-Noah` and are consumed through the
  vendored `Lib.ThreeD` package.
- Expose Noah algorithms as public sealed `XxxTool` types with source-neutral
  typed inputs/options, typed controlled results, and explicit `Execute(...)`.
- Studio may own product identity/unit/frame validation, strict adapters,
  recipe and persistence policy, explicit Preview/Publish/Run orchestration,
  evidence composition, and UI. Do not copy Noah arithmetic into Studio.
- Before algorithm work, inspect the vendored public API. If the API is
  absent, update and verify Library-Noah, commit the exact source, pack that
  clean commit, vendor package plus checksum, then adapt Studio.
- Never use an external `ProjectReference` to Library-Noah and never package
  an uncommitted Noah working tree.
- Keep `docs/OPENVISIONLAB_3D_NOAH_TOOL_MIGRATION_BASELINE_20260801.json`
  decreasing. Do not add debt or raise a ceiling to make the guard pass.
- The full ownership contract remains
  `docs/OPENVISIONLAB_3D_NOAH_TOOL_CONTRACT_AND_MIGRATION_BASELINE_20260801.md`.

## Stable Product Contracts

- Preview, Publish, Run, and Validation remain explicit user actions.
- Editing parameters, visibility, layout, source-quality drafts, or restored
  setup must not execute inspection.
- Preview is review state; Publish creates or updates an explicit result.
- Source geometry and result geometry remain separate. Output creation must
  not silently replace or change the input selection.
- Stable step, source, frame, unit, artifact, and content identities own
  persistence and Runner replay; display names and active selection do not.
- Every inspection result requires controlled status, metrics, and visual
  evidence rather than only OK/NG text.
- Measurement inputs are independent from display/render density.
- Preserve Viewer zoom, pan, orbit/drag, picking, ROI overlay, template/editor
  behavior, layer/result comparison, docking, and normal window controls.
- Keep View code-behind a thin WPF/OpenGL bridge. Durable state, policy,
  commands, transformation, and presentation decisions belong with their
  ViewModel, controller, service, adapter, or domain owner.
- Do not split cohesive code solely by file length. A partial type is not an
  architectural boundary.

## Commercial Benchmark Boundary

- Commercial products, including GoPxL, provide workflow principles rather
  than a screen, theme, or implementation template.
- Adapt current-task clarity, linked configuration/Viewer/evidence,
  progressive disclosure, purposeful familiar icons, collapsible support
  panes, and explicit status/next-action feedback.
- Do not copy competitor colors, proportions, topology, names, assets, icon
  artwork, or code. Preserve OpenVisionLab terminology and visual identity.
- A benchmark-driven change must name the OpenVisionLab operator problem, the
  abstract principle, and why the independent design fits this product.

## Human R0

- The product owner's unaided Wide/Compact R0 is required for `A-01`,
  Workspace v3 `8/8`, and human-usability or release-acceptance claims.
- Automated `-ValidateOnly`, scripted operation, or Codex observation does not
  replace the owner outcome.
- Preserve `docs/OPENVISIONLAB_3D_HUMAN_OWNER_R0_EXECUTION_20260729.md` and
  `scripts/start-human-owner-r0.ps1`.
- If a software slice changes an R0 binary, rebuild the fixed package, refresh
  hashes, and rerun both `-ValidateOnly` modes before handoff.

## Public Documentation

- Write root `README.md` in English and lead with supported workflows and user
  value.
- Keep operator setup separate from contributor and verification utilities.
- Name bundled examples by inspection task, such as `Thickness Coupon`.
- Keep generation provenance, reproducibility, data-safety, and development
  evidence out of user-facing workflow copy.
- README media must show only the application window and expose no desktop,
  taskbar, unrelated application, path, account, notification, or private data.
- Root distributions retain `LICENSE`, `NOTICE`, and required attribution.
- Dated closure and historical documents remain evidence. Add a clear
  Historical/Superseded banner instead of rewriting their recorded outcome.

## UI And Layout Integrity

- Every visible UI, UX, text, navigation, docking, theme, or responsive change
  requires current-build checks at Wide `1920 x 1040` and Compact `1280 x 760`.
- Check overlap, required-text clipping, unreachable controls, off-pane
  rendering, unintended horizontal/nested scrolling, long localization,
  docking states, popup states, keyboard focus, hover, selected, disabled,
  read-only, and validation states that the change can affect.
- Reuse semantic theme resources and shared styles. A platform-light or
  browser/default control inside the graphite UI is a defect.
- Capture fresh before and after evidence from the current build. If a true
  before state cannot be reproduced, label the closest baseline honestly.
- Layout, collapse, restore, visibility, and navigation actions must remain
  presentation-only and must not mutate recipe/source/ROI/result state.

## Verification And Local Evidence

- Build current source before EXE UI evidence. Use the smallest focused checks
  that prove the changed behavior, then expand only when risk or failures
  justify it.
- Store local test outputs physically under
  `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio` and route test TEMP/TMP
  there when practical. Source, dependencies, documentation, and user data
  remain in their owning locations.
- Launch actual desktop EXE smokes on the dynamically selected monitor with
  the smallest bounds `Left`; verify the application window intersects it.
- CI is Windows/headless. A local pass does not prove hosted CI.
- Run `git diff --check` before handoff. Do not claim old pass counts as
  current verification after relevant source changes.

## Git And Change Boundaries

- Preserve unrelated user changes and ignored user-owned data.
- Do not use destructive reset or checkout commands unless explicitly asked.
- Commit only the approved repository and scope.
- Do not commit generated local test evidence from D-backed paths.
- Commit and push require explicit user authorization; pushing does not imply
  permission to merge a feature branch or create a release.

## Completion And Next Priority

Use exactly one closure state: `Complete`, `Blocked`, or `Incomplete`.

A task is Complete only when every requested deliverable exists, required
verification passed, and durable evidence is recorded where the project can
reuse it. Report commands actually run, evidence locations, and boundaries.
Do not repeat completed work unless requirements, source, environment, or
evidence validity changed.

Every next priority must include `Recommended model` and `Reasoning effort`:

- documentation/status/simple verification: `gpt-5.6-terra`, `low`;
- localized clear code change: `gpt-5.6-terra`, `low` or `medium`;
- normal multi-file feature or difficult regression: `gpt-5.6-sol`, `medium`;
- architecture, numerical reliability, metrology, security, or difficult
  performance: `gpt-5.6-sol`, `high`.

If credentials, owner input, hardware, calibration, or source evidence is
missing, state the prerequisite first and recommend no model execution until
it is available.
