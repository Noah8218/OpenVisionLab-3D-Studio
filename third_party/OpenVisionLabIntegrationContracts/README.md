# OpenVisionLab Integration Contracts

- Package: `OpenVisionLab.Integration.Contracts` 0.2.0-alpha.3
- SHA-256: `25CADF8BD6EDBC7E9C089BE6CE2286A7ADA5A335A3DEA5FBDBCCEF63343E4A24`
- TCP package: `OpenVisionLab.Integration.Transport.Tcp` 0.1.0-alpha.3
- TCP SHA-256: `5FBFE95358554D47A047305D589614832EAF775A05100B2B8E626D8DDDEC424F`
- Package source state: `clean` at Shared commit
  `f4743f3307d20a963b2197f2019713320b9859b9`
- License: MIT; `LICENSE` and `NOTICE` are included inside the package.

This fixed local package owns the shared Handoff, Acknowledgement, Result,
validation, correlation, error, and JSON fixture contracts. Schema `2.0` is
the current generic 2D/3D/AI contract and schema `1.0` remains readable for
existing Machine-to-3D transactions. Do not replace it without updating all
OpenVisionLab consumers to the same exact package hash. The previous alpha.2
bytes remain beside this alpha.3 pair and are not overwritten.

The TCP package synchronizes immutable transaction bytes between peer-local
exchange roots. It never acknowledges, opens, previews, or runs a Handoff.
This is a fixed clean-source qualification candidate from the committed
Shared source above, not a published release. Its nuspecs are bound to that
source commit.
