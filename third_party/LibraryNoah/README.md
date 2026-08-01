# Library-Noah package input

- Package: `Lib.ThreeD` `2.8.7`
- Source worktree: `C:\Git\Library-Noah-surface-match-kernel`
- Source commit: `20963c12b50dfc0658110e2037961d3224feb2d6`
- Target framework: `netstandard2.0`
- SHA-256: `C40A2EB0239C5BF6063984429CEDB580608CD7EF8C96D08AA13A67C2B3ACF33B`

This package is intentionally vendored so Studio restore and CI do not depend on an
adjacent local checkout. `scripts/verify-library-noah-package.ps1` verifies the
file hash, package metadata, source commit, license entries, and target assembly.

To update it, first commit the Library-Noah source, pack the exact committed source,
update the package file and checksum together, then update the source commit here.
Do not point Studio at a `ProjectReference` outside this repository.
