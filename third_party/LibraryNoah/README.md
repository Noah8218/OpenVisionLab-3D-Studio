# Library-Noah package input

- Package: `Lib.ThreeD` `2.7.4`
- Source repository: `C:\Git\Library-Noah`
- Source commit: `5d06460c14b1edf390241b28511ce4997f70dc28`
- Target framework: `netstandard2.0`
- SHA-256: `BB44D30F8D3AB9C1CF528482CFA2A5A804D9222FFBAE258C765CEF2696EB2573`

This package is intentionally vendored so Studio restore and CI do not depend on an
adjacent local checkout. `scripts/verify-library-noah-package.ps1` verifies the
file hash, package metadata, source commit, license entries, and target assembly.

To update it, first commit the Library-Noah source, pack the exact committed source,
update the package file and checksum together, then update the source commit here.
Do not point Studio at a `ProjectReference` outside this repository.
