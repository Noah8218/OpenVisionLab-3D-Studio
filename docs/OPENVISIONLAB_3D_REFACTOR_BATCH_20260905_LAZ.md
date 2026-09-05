# OpenVisionLab 3D Studio Refactor Batch 0.5.3-dev

This document records the public scope of the `0.5.3-dev` Viewer LAZ/LAS
refactor batch. It is a development-line boundary, not a claim of production
metrology readiness.

## Source and compatibility boundary

- Development source commit: `5d6c384cca40fa1cc66a975a30c8fe5914bb31c2`
- Original source base: `3dc9f8c9187ad12a18f2e2c877ab1ae84addcc04`
- Product version: `0.5.3-dev`
- The public product snapshot passed its private-marker scan with 888 files.
- Viewer Host API, recipe, Run Record, integration, and persisted-storage
  contracts remain unchanged. Preview, Publish, Run, Validate, Save, and
  Reopen remain explicit actions.

## Included refactoring

- `LazPointCloudSampleCache` now uses a three-entry LRU default aligned with the
  supported Viewer density choices. A capacity override remains available to
  the focused verification path so concurrency behavior is tested explicitly.
- Viewer-only import, density reload, and asynchronous LAZ recipe loading now
  carry a typed load result. Scene state and telemetry are applied only after
  the caller proves that the operation is current.
- Superseded asynchronous loads no longer advance decoded/cache counters or
  present stale source state before the apply boundary.

## Verification boundary

- Dev source build: Release `0` warnings, `0` errors; direct Data tests `2/2`.
- Focused Viewer lifetime checks: `22/22`; LAZ coordinator/cache checks:
  `23/23`; structure checks: `222/222`.
- Actual WPF LAZ density-race Smoke exits `0` with two requests, one decode,
  one cache hit, one cancellation, and final `Balanced/50,000` sampling.
- Hosted CI is a post-push check and is not implied by this local evidence or
  by the public branch push until its run completes.
- The batch does not establish calibration, uncertainty, Gauge R&R,
  production measurement capability, universal process-memory bounds,
  native/GPU release timing, or streaming/LOD/out-of-core performance.

## Rollback

The source rollback point is Original commit
`3dc9f8c9187ad12a18f2e2c877ab1ae84addcc04`. No recipe or project migration is
required. If a regression is found, revert the single versioned batch commit;
do not rewrite or force-move the public branch.
