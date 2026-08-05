# OpenVisionLab Vision SDK package input

- Package: `OpenVisionLab.Vision3D` `3.0.0`
- Source repository: `C:\Git\OpenVisionLab-Vision-SDK`
- Source commit: `f34fdf912ff38fe20f36dbb063837e14b4f922b3`
- Target framework: `netstandard2.0`
- SHA-256: `F7324DC43ABF8E130D6F88C034287C192CFEA89E16A8A906A60F52DE341045B4`

The package is vendored so a clean Studio clone restores without an adjacent SDK
checkout. `scripts/verify-vision-sdk-package.ps1` verifies its hash, metadata,
source commit, license entries, documentation, and target assembly.

To update it, first commit and verify the SDK source, pack that exact commit, and
update the package, checksum, source commit, and Studio package reference together.
Do not mix this package with an external `ProjectReference` to the SDK.
