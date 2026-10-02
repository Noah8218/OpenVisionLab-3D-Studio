# Vision SDK package boundary

Studio consumes fixed, vendored Vision SDK packages through repository-relative
NuGet feeds. The exact inputs are owned by the
[Data project](../src/OpenVisionLab.ThreeD.Data/OpenVisionLab.ThreeD.Data.csproj),
[Tools project](../src/OpenVisionLab.ThreeD.Tools/OpenVisionLab.ThreeD.Tools.csproj),
[NuGet configuration](../NuGet.Config), and adjacent package checksum files.

## Ownership

- SDK Tools own numerical algorithms and their typed results.
- Studio Data/Tools own source conversion, identity/unit/frame validation,
  recipe policy, evidence composition, and storage.
- Shell/Viewer own explicit workflow and presentation.
- Runner and Verification exercise the same product/SDK boundary.

An interface adjustment must not add a second numerical implementation in
Studio. A geometry preview must not silently become a calibrated measurement.

## Dependency changes

Preserve immutable vendored package bytes. For a dependency update, verify
license/notice, package identity and checksum, declared source provenance,
framework compatibility, and directly affected product checks. A successful
restore does not by itself prove clean-source provenance or metrology accuracy.

The existing package-check entry point is
[scripts/verify-vision-sdk-package.ps1](../scripts/verify-vision-sdk-package.ps1).
Read its declared expected package and provenance before interpreting its result;
historical package checks do not automatically qualify newer inputs.
