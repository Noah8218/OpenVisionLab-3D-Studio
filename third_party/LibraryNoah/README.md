# Library-Noah package input

- Package: `Lib.ThreeD` `2.8.13`
- Source worktree: `C:\Git\Library-Noah-3d-input-contract-2.8.13`
- Source commit: `21f2e3084843ef8a499e6fe02c4326a19813aa2c`
- Target framework: `netstandard2.0`
- SHA-256: `852B5A959A3DD76AF69A7C295CEAC77E13F72BBB969A79FC48D88A83B9D8229D`

This package is intentionally vendored so Studio restore and CI do not depend on an
adjacent local checkout. `scripts/verify-library-noah-package.ps1` verifies the
file hash, package metadata, source commit, license entries, and target assembly.

To update it, first commit the Library-Noah source, pack the exact committed source,
update the package file and checksum together, then update the source commit here.
Do not point Studio at a `ProjectReference` outside this repository.
