# OpenVisionLab 3D Studio — GoPxL-Inspired Workbench v4 Layout Contract

Date: 2026-07-30
Status: Complete — v4-1, v4-2, and v4-3

## 1. Decision

OpenVisionLab 3D Studio will use GoPxL as a structural benchmark for the
operator workbench. The product will not copy GoPxL's brand, hardware platform,
or every visible control. It will adopt the recurring responsibility layout
that makes a rule-based inspection job understandable:

```text
Job Bar
┌────────────┬──────────────────┬──────────────────┬──────────────────────────┐
│ Work rail  │ Tools / Recipe   │ Selected Tool    │ Viewer / Displayed       │
│            │ Chain            │ configuration    │ Outputs                  │
└────────────┴──────────────────┴──────────────────┴──────────────────────────┘
Status / messages
```

The primary improvement is information architecture, not a dark-theme
restyle. The operator should see the inspection sequence, the selected rule,
and its geometric result without moving through multiple full-page setup
surfaces.

## 2. Evidence From The Supplied GoPxL Screenshots

The five supplied screenshots consistently show:

- one slim global job/replay bar;
- one stable left responsibility rail;
- a tool palette or ordered tool chain;
- a selected-tool configuration surface;
- a dominant 2D/3D viewer;
- displayed-output visibility close to the viewer;
- contextual help and system status at the bottom;
- progressive disclosure through tabs, collapsible sections, icons, and
  tooltips instead of repeated explanatory text.

The useful lesson is the stable spatial relationship between authoring
responsibilities. The following GoPxL platform scope is explicitly not part of
this benchmark:

- sensor discovery, live scan acquisition, replay transport, and frame-rate
  ownership;
- camera, lighting, PLC, robot, HMI, cloud, account, or production-controller
  functions;
- proprietary branding, tiny typography, or wholesale visual imitation.

## 3. OpenVisionLab Product Boundary

OpenVisionLab 3D Studio remains a local/file-first deterministic 2.5D/3D
rule-based inspection workbench. Its normal workflow remains:

```text
C3D source
→ ordered typed recipe
→ explicit Preview
→ explicit Publish
→ explicit validation run
→ evidence and result review
```

Core, Data, Tools, Viewer, and Runner contracts are not being rewritten for
this layout work. The Shell and Workbench presentation layers own this change.

## 4. Workbench v4 Target

### 4.1 One global Job Bar

The application has one 56-pixel title/job bar. It owns product identity,
recipe/source context, dirty state, status, and native window controls.

The former second horizontal stage-navigation row is removed. The former
full-width workspace command/metadata row is removed. Contextual authoring
actions belong to Selected Tool.

### 4.2 Left responsibility rail

The rail exposes these stable destinations:

1. Authoring
2. Validate
3. Results
4. Calibration
5. Advanced

Recipe Manager, Tool Labs, and language selection remain available at the
bottom of the rail. Wide mode shows icon and text. Compact mode shows familiar
icons with tooltips and accessible names.

### 4.3 Unified authoring cockpit

Setup and Teach become one visible Authoring destination. Their existing
state, commands, transition guard, and lifecycle contracts remain available
internally; the user no longer has to understand an artificial full-page
boundary between composing and teaching.

Wide mode:

```text
┌──────────────────────┬───────────────────────┬──────────────────────────────┐
│ Tool Library /       │ Selected Tool         │ Viewer / Displayed Outputs   │
│ Recipe Chain tabs    │ PropertyGrid + ROI    │ dominant                     │
└──────────────────────┴───────────────────────┴──────────────────────────────┘
```

Compact mode:

```text
┌──────────────────────┬──────────────────────────────────────────────────────┐
│ Tools / Chain /      │ Viewer dominant                                      │
│ Selected Tool /      │                                                      │
│ Outputs tabs         │                                                      │
└──────────────────────┴──────────────────────────────────────────────────────┘
```

Only one compact support surface is active at a time. Viewer remains dominant.

### 4.4 Command ownership

Selected Tool owns visible Preview, Publish, Cancel, and Save actions.
Navigation owns stage changes. Validate owns sample execution. Results remains
read-only and owns correction routing. This removes repeated stage, recipe,
and source text while preserving explicit action contracts.

## 5. Preserved Contracts

- Preview, Publish, Run All, and sample-set execution remain explicit.
- Selecting a step, switching a tab, opening a pane, changing visibility, or
  restoring layout never executes inspection.
- Output creation never changes the input layer or active recipe step.
- PropertyGrid remains the algorithm-tool editing surface.
- Viewer zoom, pan, drag, ROI overlays, height range, comparison, and docking
  remain available.
- Recipe, source, selected step, draft parameters, ROI lifecycle, validation
  roles, run evidence, and results survive presentation-only navigation.
- Good, Bad, and Held-out roles and Held-out exclusion rules are unchanged.
- Results remains read-only.
- Main window minimize, maximize/restore, close, and resize behavior remain.

## 6. Delivery Slices

### v4-1 — Shell and Authoring

Included:

- one Job Bar;
- responsive left responsibility rail;
- unified Authoring entry;
- Tool Library / Recipe Chain support tabs;
- Recipe Chain → Selected Tool → Viewer order;
- Viewer / Displayed Outputs adjacency;
- Selected Tool action ownership;
- current-build Wide and Compact evidence.

Excluded:

- redesign of Validate and Results internal evidence hierarchy;
- full application dark-theme conversion;
- new inspection algorithms or Runner schema changes;
- industrial-device platform expansion.

### v4-2 — Validate and Results

Replace blank or text-first regions with linked sample, rule, metric, overlay,
and Viewer evidence while preserving explicit run and correction boundaries.

Status: Complete. Validate now composes its five evidence sections beside the
same Viewer; staged-sample selection is presentation-only. Results composes a
concise decision and read-only Run Record/Output Compare/Reports evidence
beside the Viewer.

### v4-3 — Visual system and persisted layout

Apply a deliberate high-contrast workbench theme, persist only safe
presentation preferences at workspace scope, validate restored values, and
provide reset-to-default without executing inspection.

Status: Complete. The graphite role system, versioned allowlisted layout
profile, atomic save, validated restore, corrupt/incompatible fallback, and
explicit Reset layout route are recorded in
`OPENVISIONLAB_3D_GOPXL_WORKBENCH_V4_EVIDENCE_AND_SAFE_LAYOUT_20260730.md`.

## 7. v4-1 Acceptance Criteria

1. Exactly one global horizontal Job Bar remains above the work surface.
2. Authoring, Validate, Results, Calibration, and Advanced are available from
   an accessible left rail.
3. Workbench and Teach internal modes both compose the same visible Authoring
   cockpit.
4. Wide Authoring orders Recipe Chain, Selected Tool, and Viewer from left to
   right; Viewer is dominant.
5. Tool Library is available beside Recipe Chain without another full-page
   Setup route.
6. Displayed Outputs is available beside Viewer without opening Results.
7. Preview, Publish, Cancel, and Save are visible in Selected Tool and remain
   explicit.
8. Compact mode uses one support tab group plus a dominant Viewer.
9. Stage/layout changes preserve state and cause no Preview, Publish, Run,
   layer mutation, or recipe mutation.
10. Release build, focused Workbench/docking verification, Validation Set
    regression, and structure verification pass.
11. Fresh current-build Wide and Compact before/after screenshots are stored
    under `artifacts/current/20260730-gopxl-workbench-v4-shell/`.

## 8. v4-1 Result And Evidence

Implemented:

- replaced the second horizontal stage-navigation row with a responsive left
  responsibility rail;
- collapsed the repeated full-width workspace metadata/command row;
- normalized both internal Workbench and Teach modes to the same visible
  Authoring cockpit;
- grouped Tool Library with Recipe Chain and Displayed Outputs with Viewer;
- reordered Wide Authoring to support/chain, Selected Tool, then dominant
  Viewer;
- grouped compact support surfaces into one tabbed pane beside the dominant
  Viewer;
- moved explicit Preview, Publish, Cancel, and Save actions into Selected
  Tool;
- retained accessible names and tooltips for icon-only rail and authoring
  controls.

Current-build comparison:

- Wide before used the title bar, horizontal stage bar, and full-width
  workspace command/metadata row before the work surface. Wide after uses one
  Job Bar, a labeled 116-pixel rail, and the three-responsibility Authoring
  cockpit.
- Compact before used the same stacked horizontal rows. Compact after uses a
  60-pixel icon rail, one support tab group, and a dominant Viewer.
- Both after captures expose `3D View / Displayed Outputs` beside the Viewer.
  Wide also keeps `Recipe Chain / Tool Library` beside Selected Tool.

Verification:

- `dotnet build OpenVisionLab.ThreeDStudio.sln -c Release -p:Platform="Any CPU"`
  — `0` warnings, `0` errors;
- `--verify-workbench-docking` — `64/64`;
- `--verify-inspection-workspace-selection` — `63/63`;
- `--verify-validation-set` — `84/84`;
- `scripts/verify-code-structure.ps1` — `17/17`;
- Wide `1920 x 1040` and Compact `1280 x 760` application-only after
  screenshots — accepted on the first quality attempt.

Evidence:

- `artifacts/current/20260730-gopxl-workbench-v4-shell/before/`;
- `artifacts/current/20260730-gopxl-workbench-v4-shell/after/`;
- `artifacts/current/20260730-gopxl-workbench-v4-shell/verification/`.

## 9. Completion Record

```text
Status: Complete
Scope: v4-1 Shell and unified Authoring layout
Acceptance criteria: 1-11 pass in current Release and current application-only captures
Verification: Release 0/0; Workbench 64/64; Inspection Workspace 63/63; Validation Set 84/84; structure 17/17
Evidence: docs/OPENVISIONLAB_3D_GOPXL_WORKBENCH_V4_LAYOUT_CONTRACT_20260730.md; artifacts/current/20260730-gopxl-workbench-v4-shell/
Boundary / next dependency: At v4-1 closure, v4-2 still owned Validate/Results and v4-3 still owned visual/layout persistence. Those slices are now complete in section 10. This slice does not prove human-owner R0, physical calibration, metrology, or commercial-platform scope.
```

## 10. v4-2/v4-3 Closure

The complete implementation, acceptance criteria, safe persisted-field
allowlist, restore/reset rules, current Wide/Compact screenshots, and
verification reports are preserved in:

- `docs/OPENVISIONLAB_3D_GOPXL_WORKBENCH_V4_EVIDENCE_AND_SAFE_LAYOUT_20260730.md`;
- `artifacts/current/20260730-gopxl-workbench-v4-evidence-and-layout/`.

Workbench v4 is now `3/3` complete. The next dependency is the human owner's
unaided Wide/Compact R0 on the refreshed fixed-hash package.
