# OpenVisionLab Integration Contracts

The active package versions are pinned in
[the Reporting project](../../src/OpenVisionLab.ThreeD.Reporting/OpenVisionLab.ThreeD.Reporting.csproj).
Every vendored package has an adjacent `.sha256` file. License, notices,
protocol documentation, and contract fixtures remain inside the immutable
packages. A source checkout needs no adjacent integration repository.

The preserved Contracts `0.2.0-alpha.3` and TCP `0.1.0-alpha.3` pair is the
earlier clean-source qualification input. The current `alpha.4` pair is a
development input; matching hashes and successful restore/build do not prove
its separate source qualification or a cross-application operational round trip.

This fixed local package owns the shared Handoff, Acknowledgement, Result,
validation, correlation, error, and JSON fixture contracts. Schema `2.0` is
the current generic 2D/3D/AI contract and schema `1.0` remains readable for
existing Machine-to-3D transactions. Do not replace it without updating all
OpenVisionLab consumers to the same exact package hash.

The TCP package synchronizes immutable transaction bytes between peer-local
exchange roots. It never acknowledges, opens, previews, or runs a Handoff.
The historical alpha.3 verification command is
`scripts/verify-integration-contracts-package.ps1`; it does not qualify alpha.4.
