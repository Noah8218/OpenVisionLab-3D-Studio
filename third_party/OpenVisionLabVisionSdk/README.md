# OpenVisionLab Vision SDK package

- Current package: `OpenVisionLab.Vision3D` `3.3.0-rc.1.integration.20261102.138`
- Source version: `3.3.0-rc.1`
- Source repository: https://github.com/Noah8218/OpenVisionLab-Vision-SDK
- Source commit: `a550fadeec5b35103d1288743aa6746e4e4a60a2`
- Target framework: `netstandard2.0`
- SHA-256: `31AAA8974AF8B78057AA5E0214D748A49BA76F7C5C2E3D87C6D24D4F0E91D134`

Required managed dependency: `OpenVisionLab.Compute` exactly `3.3.0-rc.1.integration.20261102.138`, from the same clean committed source.
SHA-256: `D99999D165218B06013B42F421D7317C4E6C3235C03494BD533DD8127DE61CCC`.
This immutable integration package is built locally from that source commit; it does not assert a NuGet registry release.
Both packages include the matching portable PDB; Studio enables the standard `CopyDebugSymbolFilesFromPackages` property.
The existing Vision2D/Core package identities and historical packages below remain unchanged.

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
