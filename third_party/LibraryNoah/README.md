# Library-Noah package input

- Package: `Lib.ThreeD` `2.9.1`
- Source worktree: `C:\Git\Library-Noah`
- Source commit: `9dd95690d3e439b459c39aea99878880cdcc5808`
- Target framework: `netstandard2.0`
- SHA-256: `BDE8D2C01B6DC380EF4579C89DE495F06F79BA4864D4229CD5CE87713BD1CA4E`

This package is intentionally vendored so Studio restore and CI do not depend on an
adjacent local checkout. `scripts/verify-library-noah-package.ps1` verifies the
file hash, package metadata, source commit, license entries, and target assembly.

To update it, first commit the Library-Noah source, pack the exact committed source,
update the package file and checksum together, then update the source commit here.
Do not point Studio at a `ProjectReference` outside this repository.
