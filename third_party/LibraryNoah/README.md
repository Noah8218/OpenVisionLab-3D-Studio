# Library-Noah package input

- Package: `Lib.ThreeD` `2.8.6`
- Source worktree: `C:\Git\Library-Noah-surface-match-kernel`
- Source commit: `3ef2f52546a9187df465bf8973e26426c30f7634`
- Target framework: `netstandard2.0`
- SHA-256: `02E0D0B69F9D7CECBA958BF4BDC7F2999D0902539C33CD0F133C48C08C3A25B0`

This package is intentionally vendored so Studio restore and CI do not depend on an
adjacent local checkout. `scripts/verify-library-noah-package.ps1` verifies the
file hash, package metadata, source commit, license entries, and target assembly.

To update it, first commit the Library-Noah source, pack the exact committed source,
update the package file and checksum together, then update the source commit here.
Do not point Studio at a `ProjectReference` outside this repository.
