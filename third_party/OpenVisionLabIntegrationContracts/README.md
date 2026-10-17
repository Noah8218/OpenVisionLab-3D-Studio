# OpenVisionLab Integration Contracts

The [Reporting project](../../src/OpenVisionLab.ThreeD.Reporting/OpenVisionLab.ThreeD.Reporting.csproj)
selects the pinned contracts and transport packages from this local feed.
Adjacent `.sha256` files own their expected byte identities. Immutable packages
contain license, notice, protocol documentation, and contract fixtures; a source
checkout requires no adjacent integration repository.

The contracts define Handoff, Acknowledgement, Result, validation, correlation,
error, and JSON formats. Preserve compatibility across consumers when changing
the selected package pair. A successful restore does not prove a cross-application
operational round trip.

The TCP transport synchronizes immutable transaction bytes between peer-local
exchange roots. It does not acknowledge, open, preview, or run a Handoff. Read
the expected inputs of `scripts/verify-integration-contracts-package.ps1` before
using that package check; its scope does not automatically cover another version.
