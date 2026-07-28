# Public README, Example, License, and Media Policy

Date: 2026-07-28

## Purpose

The public repository should explain what an operator can accomplish with
OpenVisionLab 3D Studio. Internal scope boundaries, data-generation details,
and verification mechanics must not replace the product story.

## Public README contract

1. The root `README.md` is written in English.
2. The opening section states the supported inspection workflow and user value.
3. Product copy does not enumerate unsupported camera, lighting, PLC,
   industrial I/O, robot, cloud, or production systems.
4. Included examples are named by their inspection task, such as
   `Thickness Coupon`, rather than by their production method.
5. Generation provenance, hashes, and data-safety evidence stay in development
   evidence records and are not promoted as user-facing sample features.
6. The README links directly to the runnable example recipe, build command,
   keyboard shortcuts, development documentation, and actual project license.

## Public media contract

- A README GIF or screenshot contains only the application window.
- No frame may expose the desktop, taskbar, unrelated applications,
  notifications, account information, or local operator paths.
- The media must come from the current Release build after the relevant source
  and example changes.
- A Thickness demonstration keeps the Reference and Measurement ROI on the
  intended surfaces inside the same visible part.
- Before and after frames or contact sheets are retained as development
  evidence, but only the clean application-only media is published.

## License contract

- The root `LICENSE` contains Apache License 2.0.
- The root `NOTICE` records the project copyright and attribution.
- The README identifies Apache 2.0, links to `LICENSE`, and states that
  redistribution must retain the license and attribution notices.
- Third-party components remain under their respective licenses.

## Completion record

```text
Status: Complete
Scope: English README, task-oriented example naming, Apache-2.0 license, NOTICE, application-only GIF, and same-pad Thickness ROI demonstration.
Acceptance criteria: README is English and capability-first -> pass; excluded limitation and generation-method copy is absent from README -> pass; Apache-2.0 LICENSE and NOTICE exist -> pass; Thickness example paths and identities are task-oriented -> pass; Reference and Measurement ROI stay inside the same pad -> pass; every GIF contact-sheet frame is application-only -> pass.
Verification: Release build 0 warnings/0 errors; code structure 17/17; tool recipe selections 29/29; height measurement workbench 46/46; Thickness repeat authoring 20/20; Runner 8/8; README local links 8/8; changed JSON parse 12/12; git diff --check pass; GIF probe 960 x 520, 25.51 s, 204 frames, 1,325,086 bytes.
Evidence: artifacts/current/20260728-readme-license-roi-gif/
Boundary / next dependency: This closes public README, license, example naming, and published GIF presentation. It does not certify physical calibration or metrology.
```
