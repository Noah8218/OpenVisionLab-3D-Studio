# Shell Ordered Thickness Run Closure

Date: 2026-08-17
Status: Complete
Issue: `PL-0016`

## Outcome

Studio now executes a saved, supported ordered Thickness recipe from the
Validate workspace with one explicit **Run current recipe** action. The action
uses the same `ToolRecipeOrderedGraphExecution` engine as Runner, writes a
schema `1.5` Run Record, and immediately projects the result into Results.

The operator no longer has to leave Studio and start Runner to complete the
normal loop:

`load -> source quality -> teach -> Preview -> Publish -> Run -> Results -> save/reopen`

Run remains unavailable until the current recipe is saved, its source is
ready, no parameter draft or other execution is active, and every ordered
step has a supported typed adapter. The visible capability text gives the
exact blocking reason.

## Product And Commercial Boundary

The OpenVisionLab problem was a broken operator handoff: Preview and Publish
were available in Studio, but the same valid Thickness recipe required a
separate Runner process for full execution and evidence review.

The commercial/GoPxL lesson retained here is the abstract principle of keeping
the current task, explicit execution, controlled result, and evidence link in
one understandable workflow. The implementation is independent: it uses
OpenVisionLab terms, visual resources, ordered graph contracts, stable
identities, and Results model. No competitor color, topology, name, asset,
icon artwork, or code was copied.

Camera, lighting, PLC, industrial I/O, robot, cloud, accounts, deployment, and
production-line control remain excluded. Synthetic raw-height evidence is not
calibrated physical metrology, Gauge R&R, or production approval.

## Implemented Contract

- Validate exposes a primary `Run current recipe` action before the separate
  Validation Set action.
- Ready, Running, Pass, Fail, Error, exact capability reason, key metric,
  output identity/hash, and Run Record state remain visible together.
- Studio and Runner share one ordered-step-to-Run-Record projection; Studio
  does not contain a second numerical or replay engine.
- Results reads the new record immediately and shows the ordered step,
  controlled state, metric, output route, and output hash.
- Editing invalidates current evidence and disables Run until save. Open,
  Preview, Publish, save, and reopen do not invoke full Run.
- Navigation and recipe mutation are blocked only while the explicit ordered
  Run is active.
- Non-finite Error metrics remain omitted from JSON without changing the
  controlled Error state or reason.

## Ten-Sample Actual EXE Result

Each saved recipe was opened in a separate current Release EXE at Compact
`1280 x 760`, placed on the dynamically selected leftmost monitor, and run by
the bound `Run current recipe` command after a held pointer-down capture.

| Sample | Expected | Shell | Mean raw-height | Ordered Run ms |
| --- | --- | --- | ---: | ---: |
| `01-nominal` | Pass | Pass | 8.00000 | 508.624 |
| `02-thin-pass` | Pass | Pass | 7.81852 | 451.452 |
| `03-thin-fail` | Fail | Fail | 7.45556 | 517.019 |
| `04-thick-pass` | Pass | Pass | 8.18148 | 468.425 |
| `05-thick-fail` | Fail | Fail | 8.54444 | 515.534 |
| `06-noisy` | Fail | Fail | 8.00173 | 463.280 |
| `07-gradient` | Fail | Fail | 7.98777 | 475.637 |
| `08-missing-40` | Pass | Pass | 8.00000 | 453.380 |
| `09-insufficient` | Error | Error | n/a | 413.326 |
| `10-local-defect` | Fail | Fail | 8.27841 | 533.351 |

All ten EXE processes exited successfully. The controlled state distribution
is `Pass 4 / Fail 5 / Error 1`. Status, metrics, ordered step identity, output
identity, output content SHA-256, and Error representation match the existing
production Runner records `10/10`.

## Performance Target

For this `1280 x 840` synthetic C3D sample class, the current Shell interaction
budget is:

- ordered Run p95 `<= 600 ms`;
- ordered Run maximum `<= 750 ms`.

Observed values are p50 `468.425 ms`, p95 `533.351 ms`, and maximum
`533.351 ms`. These values exclude EXE startup and screenshot time and come
from each Run Record's ordered graph duration. The target keeps the explicit
action visibly sub-second while allowing a practical regression margin above
the ten observations. It is not a maximum-input, hardware-independent, or
production SLA. Large-C3D work remains blocked until a representative maximum
input and accepted memory/load-time limits exist.

## Verification

- Release solution build: `0` warnings, `0` errors.
- Ordered Run contract: `13/13`.
- Tool Recipe teaching: `50/50`.
- Run Record history: `12/12`.
- Recipe Manager/WPG: `52/52`.
- Shell smoke command-line options: `40/40`.
- Ten actual Release EXE Runs: `10/10` exit success and expected state.
- Shell/Runner Run Record parity: `10/10`.
- Wide `1920 x 1040` and Compact `1280 x 760`: ready and held
  pointer-down captures accepted; window/selected-monitor intersection true.
- Latest-build direct clicks: Wide and Compact Fail status cards are readable;
  Wide Results immediately reads schema `1.5`, one ordered Fail step, output
  identity, and evidence actions without overlap or platform-light leakage.

Durable evidence:

- `.proofline/issues/PL-0016.json`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260817-pl0016-shell-ordered-thickness-run\logs\ordered-run-verification.txt`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260817-pl0016-shell-ordered-thickness-run\logs\ten-sample-shell-run-summary.txt`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260817-pl0016-shell-ordered-thickness-run\logs\ten-sample-shell-run-parity.txt`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260817-pl0016-shell-ordered-thickness-run\after\`.

## Maturity And Remaining Priority

The evidence-bounded commercial authoring-readiness assessment moves from
`8.2/10` to `8.5/10`. This is a qualitative workflow judgment: the primary
operator loop now reaches a controlled Run and Results inside Studio, but
coordinate-confident ROI teaching and product-owner unaided Wide/Compact R0
remain open. It is not telemetry, certified usability, release acceptance,
production approval, or physical-metrology evidence.

1. Product-owner unaided Wide and Compact R0 | Prerequisite: owner operation
   and observer record | Recommended model: none | Reasoning effort: none.
2. `PL-0017 coordinate-confident grid ROI teaching` | Recommended model:
   `gpt-5.6-sol` | Reasoning effort: `medium`.
3. Large-C3D memory/performance target | Prerequisite: representative maximum
   C3D plus accepted process-memory and load-time limits | Recommended model:
   none until available | Reasoning effort: none.

## Closure Record

```text
Status: Complete
Scope: Shell explicit ordered Run for saved supported Thickness recipes, shared Runner projection, Run Record/Results routing, no-auto-run invalidation, current UI states, and ten-sample interaction budget
Acceptance criteria: valid saved Thickness enables Run and exact invalid/unsupported reasons disable it -> pass; Pass/Fail/Error status, metrics, step/output/content identities and Run Record match Runner -> pass 10/10; edits/Preview/Publish/save/reopen do not auto-run -> pass; Wide/Compact current-build ready/pressed/result states are themed, readable, bounded, and keyboard reachable -> pass
Verification: Release 0/0; ordered Run 13/13; Tool Recipe teaching 50/50; Run Record history 12/12; Recipe Manager/WPG 52/52; Shell options 40/40; ten actual EXE Runs and expected states 10/10; Runner parity 10/10; p95 533.351 ms <= 600 ms and max 533.351 ms <= 750 ms; Wide/Compact monitor intersection and current direct-click review pass
Evidence: docs/OPENVISIONLAB_3D_SHELL_ORDERED_THICKNESS_RUN_CLOSURE_20260817.md; .proofline/issues/PL-0016.json; D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260817-pl0016-shell-ordered-thickness-run/
Boundary / next dependency: synthetic raw-height is not physical metrology; product-owner unaided R0 remains external; PL-0017 owns coordinate-confident grid ROI teaching; large-C3D claims require a representative maximum input and accepted budgets
```
