# Library-Noah package input

- Package: `Lib.ThreeD` `2.8.8`
- Source worktree: `C:\Git\Library-Noah-surface-match-kernel`
- Source commit: `0fe04bc967fa89918b3c6d937566cce56de69682`
- Target framework: `netstandard2.0`
- SHA-256: `D62B050710C4CCA0309B3FA49CDCDBB239C675944E29C085E50CD198D4D15405`

This package is intentionally vendored so Studio restore and CI do not depend on an
adjacent local checkout. `scripts/verify-library-noah-package.ps1` verifies the
file hash, package metadata, source commit, license entries, and target assembly.

To update it, first commit the Library-Noah source, pack the exact committed source,
update the package file and checksum together, then update the source commit here.
Do not point Studio at a `ProjectReference` outside this repository.
