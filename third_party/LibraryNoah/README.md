# Library-Noah package input

- Package: `Lib.ThreeD` `2.7.1`
- Source repository: `C:\Git\Library-Noah`
- Source commit: `8811ca260caf3a6640933624106df23146427d53`
- Target framework: `netstandard2.0`
- SHA-256: `3A873D926764CCC6781413DA62DD7D2F6FDF050058BCEE231279C1C77CEC69DA`

This package is intentionally vendored so Studio restore and CI do not depend on an
adjacent local checkout. `scripts/verify-library-noah-package.ps1` verifies the
file hash, package metadata, source commit, license entries, and target assembly.

To update it, first commit the Library-Noah source, pack the exact committed source,
update the package file and checksum together, then update the source commit here.
Do not point Studio at a `ProjectReference` outside this repository.
