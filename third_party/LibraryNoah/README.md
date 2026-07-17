# Library-Noah package input

- Package: `Lib.ThreeD` `2.1.0`
- Source repository: `C:\Git\Library-Noah`
- Source commit: `b113ee8099ffcfe9f75f34928b0e214b542b75fb`
- Target framework: `netstandard2.0`
- SHA-256: `C4D119D12EB607874882BB34E65EC264A9F78CF188C785A61FF79CEFF1D895E5`

This package is intentionally vendored so Studio restore and CI do not depend on an
adjacent local checkout. `scripts/verify-library-noah-package.ps1` verifies the
file hash, package metadata, source commit, license entries, and target assembly.

To update it, first commit the Library-Noah source, pack the exact committed source,
update the package file and checksum together, then update the source commit here.
Do not point Studio at a `ProjectReference` outside this repository.
