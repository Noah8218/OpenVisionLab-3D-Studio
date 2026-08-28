# OpenVisionLab Vision SDK package

- Package: `OpenVisionLab.Vision3D` `3.0.1-dev.20260828.point-cloud-background-filter.1`
- Source commit: `35f1eef6626db710ac18452cd1e729530f2c0f2f`
- Target framework: `netstandard2.0`
- SHA-256: `0ECAB9C99F9DA5FCB3D7FB49DFFEBF23AB8F9950AE47D33A95CC767449A6CF7A`

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
