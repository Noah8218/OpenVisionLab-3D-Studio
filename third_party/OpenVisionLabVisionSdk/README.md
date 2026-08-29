# OpenVisionLab Vision SDK package

- Current package: `OpenVisionLab.Vision3D` `3.0.1-dev.20260829.normal-preparation.1`
- Source commit: `6da3bcf521efb88681a17e4a7b23a091e7fcbacf`
- Target framework: `netstandard2.0`
- SHA-256: `E0A2EB2021EB041D93225D00EEF71F028BD79CE3BA7F9CF27F539634F8D506A2`

Historical package retained for D-13 evidence:

- Package: `OpenVisionLab.Vision3D` `3.0.1-dev.20260829.voxel-downsample.1`
- Source commit: `b15490c5717566effae891eb64c7b832c4fea8be`
- Target framework: `netstandard2.0`
- SHA-256: `938DDB3C7194C192697B9C669D6B2358D78C8784C59541D4D657CA74B8BBB0D5`

- Package: `OpenVisionLab.Vision2D` `3.0.1-dev.20260823.grid-diagnostics.1`
- Source commit: `8be38403d0d00698431d7ffa4de60a63289672c6`
- Target framework: `netstandard2.0`
- SHA-256: `01FF9B2056A29139351ED619E3E2C6F484E71E057CF16471076A336A3DA85E2F`
- Package: `OpenVisionLab.Core` `3.0.1-dev.20260823.grid-diagnostics.1`
- Source commit: `8be38403d0d00698431d7ffa4de60a63289672c6`
- Target framework: `netstandard2.0` (Windows x64 native OpenCV asset)
- SHA-256: `D06A8D7F9453DFAE8A12D6DE467FBE32F72CE5BDC6866E22E1D3608F2631E022`

The packages are vendored so a clean Studio clone restores without an adjacent
SDK checkout. `scripts/verify-vision-sdk-package.ps1` verifies the Vision3D
package; `scripts/verify-vision2d-package.ps1` verifies the Vision2D/Core
identity, checksum, license/notice, documentation, assemblies, source commit,
target framework, and native OpenCV asset.
