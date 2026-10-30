# Contributor operating rules

## Start here

Read [README](README.md), then the canonical
[source setup and project navigation](docs/OPENVISIONLAB_3D_SYSTEM_REQUIREMENTS_AND_SETUP.md).
Use the solution and each project's references to identify the executable,
library, and test boundary before editing.

## Product and code boundaries

- Keep the file-first recipe teaching and explicit inspection workflow.
  Opening, selecting, restoring, or changing display settings must not run a tool.
- Numerical algorithms belong to the pinned Vision SDK. Studio owns source
  identity, units, recipe policy, validation, persistence, and presentation.
- Reuse the existing feature owner. Do not split or move a completed owner for
  file length or style alone.
- Use MVVM for WPF. Views own presentation and framework plumbing; ViewModels
  own application state and commands. Keep dialogs behind their existing hosts.
- Preserve command/binding contracts, cancellation, stale-result rejection, and
  deterministic release of subscriptions and rendering resources.
- Keep user data and unrelated work intact. Never commit customer scans,
  credentials, or generated local test outputs.

## Verification

Build the changed project and run its focused checks. For UI work, check the
changed control and representative shared-style consumers in normal, focused,
selected, pressed, disabled, error, and popup states. Check supported layouts,
languages, and native scaling when they are affected.

Use the existing test projects and Verification command router. Do not launch
the complete unrelated regression suite as a substitute for a focused check.
Record which checks ran and separate build, automated, rendered, and actual
user-flow evidence. A bitmap or source review does not prove native interaction.

## Public documentation and delivery

Keep public documentation limited to implemented behavior, source-build
instructions, stable contracts, contribution guidance, and legal attribution.
Internal plans, handoffs, issue ledgers, local validation logs, and unfinished
roadmaps do not belong in this tree.

The canonical version is in `Directory.Build.props`. An authorized versioned
source update must agree with README's current version and recent history.
Keep README's current-version link aligned with CHANGELOG.md, which owns the
version history; preserve older traceability in Git history. Use Semantic Versioning and retain development suffixes until
the appropriate release is qualified.

Commit, push, tag, release publication, and deployment are separate actions.
Act only within the user's authorized repository and stage. Never force-push
or rewrite published history to clean up development records.
