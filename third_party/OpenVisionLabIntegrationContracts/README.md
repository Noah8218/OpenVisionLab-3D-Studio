# OpenVisionLab Integration Contracts

These fixed local candidate packages are consumed by the 2D and 3D source
trees. They are not published NuGet releases; update both consumers together
and verify the exact SHA-256 before replacing either package.

| Package | Candidate | SHA-256 |
| --- | --- | --- |
| `OpenVisionLab.Integration.Contracts` | `0.2.0-alpha.3` | `25CADF8BD6EDBC7E9C089BE6CE2286A7ADA5A335A3DEA5FBDBCCEF63343E4A24` |
| `OpenVisionLab.Integration.Transport.Tcp` | `0.1.0-alpha.3` | `5FBFE95358554D47A047305D589614832EAF775A05100B2B8E626D8DDDEC424F` |

The contracts package owns the shared Handoff, Acknowledgement, Result,
validation, correlation, error, and JSON fixture contracts. The TCP package
owns the authenticated framed transport and replay/timeout validation used by
the two consumers. Both packages include the MIT `LICENSE` and `NOTICE`.

The package bytes are checked in for deterministic offline builds. Their
source provenance is the clean Shared integration-contracts commit
`f4743f3307d20a963b2197f2019713320b9859b9`, not a published release.
