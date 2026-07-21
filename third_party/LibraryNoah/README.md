# Library-Noah package input

- Package: `Lib.ThreeD` `2.3.0`
- Source repository: `C:\Git\Library-Noah`
- Source commit: `630e37b9111f3223217c815e19c480546fde8ad7`
- Target framework: `netstandard2.0`
- SHA-256: `5143A6D270DB60751EDD825ABBC64A49B4612E149A60DF094F24D1ED3A7F21F8`

This package is intentionally vendored so Studio restore and CI do not depend on an
adjacent local checkout. `scripts/verify-library-noah-package.ps1` verifies the
file hash, package metadata, source commit, license entries, and target assembly.

To update it, first commit the Library-Noah source, pack the exact committed source,
update the package file and checksum together, then update the source commit here.
Do not point Studio at a `ProjectReference` outside this repository.
