# Library-Noah package input

- Package: `Lib.ThreeD` `2.7.7`
- Source repository: `C:\Git\Library-Noah`
- Source commit: `6aba3d5b37e9d10f2d90977e483956b6d57e2aaf`
- Target framework: `netstandard2.0`
- SHA-256: `B2909B939EEEF1000F22BDBED96D7A3AC1F67E2F6068AEC2F658ED1FF10E4708`

This package is intentionally vendored so Studio restore and CI do not depend on an
adjacent local checkout. `scripts/verify-library-noah-package.ps1` verifies the
file hash, package metadata, source commit, license entries, and target assembly.

To update it, first commit the Library-Noah source, pack the exact committed source,
update the package file and checksum together, then update the source commit here.
Do not point Studio at a `ProjectReference` outside this repository.
