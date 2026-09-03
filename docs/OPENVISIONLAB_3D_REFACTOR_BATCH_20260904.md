# OpenVisionLab 3D Studio Refactor Batch 0.5.0-dev

This document records the public scope of the `0.5.0-dev` refactoring batch.
It is a release-boundary note, not a claim of production metrology readiness.

## Source and compatibility boundary

- Development source baseline: `58e7104ebd15e37c4b9b081f35d76137bb0bc51c`
- Original source base: `6653a0ab95ca14e123874866b42b379ae69c1c77`
- Product version: `0.5.0-dev`
- Existing recipe, project, run-record, and storage contracts remain the
  compatibility boundary. Preview, Publish, Run, Validate, Save, and Reopen
  remain explicit actions.

## Included refactoring

- Shell and Viewer asynchronous source loading, teaching preparation, display
  projection, dispatcher refresh, timer, event, callback, and GPU/managed
  buffer lifetimes now have explicit owners and shutdown disposal paths.
- Verification code is compiled in a dedicated
  `OpenVisionLab.ThreeD.Verification` project instead of being coupled to the
  Shell and Viewer assemblies.
- The binary-host sample documents the independent Viewer DLL boundary and
  supports scene replacement, camera/selection/overlay checks, and disposal
  cycles without a Shell or Reporting reference.
- Integration contract fixtures and checks use the alpha.3 package pair while
  preserving V1 and V2 schemas and the public V2 exchange contract.

## Evidence for this batch

- The complete solution Release build passed with zero warnings and errors.
- Direct Data tests passed 2/2 and Reporting tests passed 10/10.
- Vision SDK, WPF property-grid, and Integration Contracts package checks
  passed with SHA-256 verification.
- The independent Viewer consumer project restored and built successfully.

## Unverified boundary

- Desktop WPF/OpenGL runtime checks at supported monitor layouts and DPI values
  were not executed for this batch.
- The cross-process consumer smoke requires a separate Machine integration
  producer and was not executed.
- These checks do not establish sensor calibration, uncertainty, Gauge R&R, or
  production-line measurement capability.

## Rollback

The source rollback point for this batch is the Original base commit listed
above. No recipe or project migration is required by the refactor; if a
consumer depends on the alpha.3 integration package, restore the prior package
pair and corresponding source together.
