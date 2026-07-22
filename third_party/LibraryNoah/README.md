# Library-Noah package input

- Package: `Lib.ThreeD` `2.7.9`
- Source repository: `C:\Git\Library-Noah`
- Source commit: `e36d9c07baab967fd4252e7052345563f29872a3`
- Target framework: `netstandard2.0`
- SHA-256: `B21A6266AFD470B7EE8A4C857496E53561F4D399F2460FEE2939AAE85AD0FF92`

This package is intentionally vendored so Studio restore and CI do not depend on an
adjacent local checkout. `scripts/verify-library-noah-package.ps1` verifies the
file hash, package metadata, source commit, license entries, and target assembly.

To update it, first commit the Library-Noah source, pack the exact committed source,
update the package file and checksum together, then update the source commit here.
Do not point Studio at a `ProjectReference` outside this repository.
