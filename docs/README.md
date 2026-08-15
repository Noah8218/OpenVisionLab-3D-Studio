# OpenVisionLab 3D Documentation Map

This index separates current instructions from user documentation, durable
contracts, completion evidence, and historical records.

## Current Authority

| Information | Owning document |
| --- | --- |
| Repository operating rules | [AGENTS.md](../AGENTS.md) |
| Capability inventory, dependencies, queue | [Master development workflow and backlog](OPENVISIONLAB_3D_MASTER_DEVELOPMENT_WORKFLOW_AND_BACKLOG_20260727.md) |
| Current handoff and external prerequisites | [Current session handoff](OPENVISIONLAB_3D_NEXT_SESSION_HANDOFF.md) |
| Next-chat entry prompt | [Next-chat entry prompt](OPENVISIONLAB_3D_NEXT_CHAT_HANDOFF_PROMPT_20260728.md) |
| Release/version policy | [Release/version policy](OPENVISIONLAB_3D_RELEASE_VERSION_POLICY.md) |
| Vision SDK algorithm boundary | [Vision SDK Tool contract](OPENVISIONLAB_3D_VISION_SDK_TOOL_CONTRACT_AND_MIGRATION_BASELINE_20260805.md) |

Only the master backlog owns the current inventory and development queue.
Dated documents preserve their recorded scope and do not override it.

## Users And Operators

- [Root README](../README.md) — product overview, supported workflows, source build, and
  documentation entry point.
- [User tutorial](OPENVISIONLAB_3D_USER_TUTORIAL.md) — first Thickness Coupon inspection.
- [Windows package quick start](OPENVISIONLAB_3D_WINDOWS_PACKAGE_QUICK_START.md) — self-contained package
  startup.
- [System requirements and setup](OPENVISIONLAB_3D_SYSTEM_REQUIREMENTS_AND_SETUP.md) — operator and developer
  prerequisites.
- [Sample data](OPENVISIONLAB_3D_SAMPLE_DATA.md) and
  [public sample attribution](../3D/PublicSamples/README.md) — sample
  inventory and attribution.

## Contributors And Verification

- [Development and verification guide](OPENVISIONLAB_3D_DEVELOPMENT_AND_VERIFICATION_GUIDE.md) — build, focused
  checks, D-backed evidence, UI verification, and CI scope.
- [Code rules](OPENVISIONLAB_3D_CODE_RULES.md) — source organization and coding rules.
- [Public README and media policy](OPENVISIONLAB_3D_PUBLIC_README_AND_MEDIA_POLICY.md) — public media and README
  requirements.

## Active Product And Architecture Contracts

- [Master development workflow and backlog](OPENVISIONLAB_3D_MASTER_DEVELOPMENT_WORKFLOW_AND_BACKLOG_20260727.md) — full
  235-item inventory and evidence gates.
- [Approved GoPxL benchmark direction](OPENVISIONLAB_3D_GOPXL_BENCHMARK_APPROVED_DIRECTION_20260731.md) — approved
  benchmark principles without visual copying.
- [Workbench v4 layout contract](OPENVISIONLAB_3D_GOPXL_WORKBENCH_V4_LAYOUT_CONTRACT_20260730.md) — current
  Workbench layout contract.
- [Vision SDK Tool contract](OPENVISIONLAB_3D_VISION_SDK_TOOL_CONTRACT_AND_MIGRATION_BASELINE_20260805.md) —
  Tool-only numerical ownership.
- [Vision SDK package boundary](OPENVISIONLAB_3D_VISION_SDK_PACKAGE_BOUNDARY_20260805.md) — vendored
  package provenance and consumer boundary.
- [Human-owner R0 procedure](OPENVISIONLAB_3D_HUMAN_OWNER_R0_EXECUTION_20260729.md) — current external
  Wide/Compact owner acceptance procedure.

## Current External Blockers

- Human-owner R0 — blocks `A-01`, Workspace v3 `8/8`, and human-usability or
  release acceptance.
- `PL-0003` — the authenticated audit and 57-item Actions cleanup are complete;
  GitHub Support ticket `#4633618` is Open, and closure is blocked only on
  GitHub processing plus the resulting old-object access verification.

External prerequisites do not become complete because a document is old.

## Completion Evidence

- [Truthful alignment status summary](OPENVISIONLAB_3D_TRUTHFUL_ALIGNMENT_STATUS_SUMMARY_20260806.md) -
  `PL-0005` stage/state contract, no-action regression, current-build
  Wide/Compact comparison, and refreshed R0 hashes.

- [Shared-chat analysis and immutable C3D load snapshot](OPENVISIONLAB_3D_SHARED_CHAT_ANALYSIS_AND_C3D_LOAD_SNAPSHOT_20260806.md) —
  verified finding matrix, `PL-0004` correction, focused regression, R0 hash
  refresh, and remaining memory boundary.
- [OpenVisionLab Vision SDK 3 migration](OPENVISIONLAB_3D_VISION_SDK_3_MIGRATION_20260805.md) —
  fixed package provenance, compatibility boundary, full regression result,
  sample replay, and self-contained package evidence.

Dated files named for a feature, migration, closure, verification, or audit
are durable evidence for that bounded task. Keep their original status,
commands, hashes, limitations, and evidence paths. When later work supersedes
their current-direction claim, add a short Historical or Superseded banner
rather than rewriting the recorded result.

Use filename/topic search to locate a closure:

```powershell
rg --files docs | rg "SURFACE_MATCH|ACQUISITION|VALIDATION|NOAH"
```

## Historical Records

[The documentation archive](archive/README.md) contains former append-only handoffs and project-instruction
snapshots. These files preserve state-at-the-time claims, including old
versions, priorities, working-tree notes, and evidence inventories. They are
not current instructions.

Older design, concept, prototype, commercial review, and failed/incomplete
replay documents may remain in `docs/` because other evidence links reference
them. Their top status must identify Historical, Superseded, or Active
Reference when the distinction is otherwise ambiguous.

## Status Vocabulary

- `Current` — active navigation or authority document.
- `Active Reference` — durable contract still applicable, but not the queue
  owner.
- `Complete` — all stated criteria passed for the documented scope.
- `Blocked` — an external prerequisite is missing.
- `Incomplete` — a required check failed or remains unresolved.
- `Historical` — state-at-the-time evidence; not a current priority.
- `Superseded` — replaced by a named current document or issue.

Avoid verbose custom status sentences when one vocabulary term plus a short
boundary note is sufficient.

## 2026-08-05 Consolidation Record

```text
Status: Complete
Scope: Documentation authority, current handoffs, historical preservation, navigation index, selected stale-status banners, and external-retention issue registration
Acceptance criteria: master is the only inventory/queue owner -> pass; active handoffs are short -> pass; former operating/handoff bodies preserved -> pass; stale current Push claims removed -> pass; external sensitive-data blocker remains visible -> pass
Verification: archived body tails 3/3 exact; local Markdown links 47/47; user-document script references 6/6; active stale Git claims 0; duplicated inventory snapshots outside master 0; Proofline v2 issues 3/3 valid; git diff --check pass
Evidence: AGENTS.md; this index; current and archived handoffs; master backlog; .proofline/issues/PL-0003.json
Boundary / next dependency: no product code or UI changed; human-owner Wide/Compact R0 remains external; PL-0003 requires GitHub Support to process open ticket #4633618 and the resulting old-object access outcome to be verified
```
