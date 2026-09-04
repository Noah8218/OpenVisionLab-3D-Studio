# OpenVisionLab 3D Studio Refactor Batch 0.5.1-dev

This document records the public scope of the `0.5.1-dev` refactoring batch.
It is a release-boundary note, not a claim of production metrology readiness.

## Source and compatibility boundary

- Development source commit: `8c0ae036e37141f20c1389dc52038b3428fb104c`
- Original source base: `346c58e2f397f57967b64510d0f986bfc68884c0`
- Product version: `0.5.1-dev`
- Existing recipe, project, Run Record, integration, and storage contracts
  remain the compatibility boundary. Preview, Publish, Run, Validate, Save,
  and Reopen remain explicit actions.

## Included refactoring

- Consolidated Shell startup, dialog, persistence, smoke, dispatcher, event,
  and cancellation ownership behind explicit coordinators and services.
- Extracted Viewer point-cloud loading, source snapshots, scene transforms,
  render-resource state, GPU vertex preparation, caches, and display-color
  policy from the control's shared state.
- Preserved the existing public Viewer API, V2 integration exchange, recipe
  formats, Run Record formats, and raw-height unit semantics.

## Evidence for this batch

- Dev `main` was pushed at the exact source commit above after a Release build
  with zero warnings and errors and standard Data tests passing 2/2.
- The approved public product snapshot passed the private-marker scan with
  888 files before it was applied to this public checkout.
- The Original candidate Release build passed with zero warnings and errors
  before version and release-note edits; the final versioned commit is rebuilt
  and tested after those edits.

## Unverified boundary

- Hosted CI for the pushed public commit is pending and is not implied by the
  local build or push.
- Desktop WPF/OpenGL runtime checks at every supported monitor layout and DPI,
  large-data streaming/LOD/out-of-core behavior, and external hardware
  integration were not executed for this batch.
- These checks do not establish sensor calibration, uncertainty, Gauge R&R, or
  production-line measurement capability.

## Rollback

The source rollback point for this batch is Original commit
`346c58e2f397f57967b64510d0f986bfc68884c0`. No recipe or project migration is
required by the refactor. If a regression is found, restore that commit and
retain the versioned batch commit for traceability; do not rewrite or force
move the public branch.
