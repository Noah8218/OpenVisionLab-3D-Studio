# Library-Noah package input

- Package: `Lib.ThreeD` `2.7.8`
- Source repository: `C:\Git\Library-Noah`
- Source commit: `d1dff41ca0ce940492930267aa0ae7430e73e437`
- Target framework: `netstandard2.0`
- SHA-256: `D7C0BD0ED60249870BD8B0A6DAC7D69A7A608FD23347E5440DF8ED30C3A90F2F`

This package is intentionally vendored so Studio restore and CI do not depend on an
adjacent local checkout. `scripts/verify-library-noah-package.ps1` verifies the
file hash, package metadata, source commit, license entries, and target assembly.

To update it, first commit the Library-Noah source, pack the exact committed source,
update the package file and checksum together, then update the source commit here.
Do not point Studio at a `ProjectReference` outside this repository.
