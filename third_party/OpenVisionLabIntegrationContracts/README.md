# OpenVisionLab Integration Contracts

These fixed local candidate packages are consumed by the 2D and 3D source
trees. They are not published NuGet releases; update both consumers together
and verify the exact SHA-256 before replacing either package.

| Package | Candidate | SHA-256 |
| --- | --- | --- |
| `OpenVisionLab.Integration.Contracts` | `0.2.0-alpha.2` | `9F94B300D8DEBC4B69DC242B5E47018605CAA725A5DA265F32718DBDDF8A1BD5` |
| `OpenVisionLab.Integration.Transport.Tcp` | `0.1.0-alpha.2` | `67E0270C67DCE287E3DCA3D4A01557C526185719E87248FC662FBCF99BBA13A2` |

The contracts package owns the shared Handoff, Acknowledgement, Result,
validation, correlation, error, and JSON fixture contracts. The TCP package
owns the authenticated framed transport and replay/timeout validation used by
the two consumers. Both packages include the MIT `LICENSE` and `NOTICE`.

The package bytes are checked in for deterministic offline builds. Their
source provenance is the current working-tree integration change, not a
published release commit.
