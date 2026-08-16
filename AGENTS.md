# Repository Guidelines

## Project Structure
Unity/VRChat assets live under `Assets/`, with VRMine-owned code under `Assets/KafkaMade/VRMine/`. Browser games live under `pages/`. VPM and Unity package declarations live under `Packages/`; Unity version is fixed by `ProjectSettings/ProjectVersion.txt`. Do not add generated verification reports, screenshots, `Library/`, downloaded packages, repo-local `.tools/`, or `.artifacts/` evidence to Git.

## Verification Contract
Browser success and VRChat success are separate evidence classes. The verification architecture is tracked by #54:

- U1: VPM/package resolution (`vrc-get`) — automated by `task vpm:check` / `.github/workflows/unity-vpm.yml`
- U2: exact Unity compile + EditMode tests
- U3: PlayMode + ClientSim-supported local semantics
- U4: Windows + actual VRChat Build & Test / multi-client
- U5: private-world release smoke

Do not claim a higher evidence level from a lower one. ClientSim does not certify real VRChat networking, ownership transfer, late join, PC/Quest parity, or uploaded-world behavior.

Legacy Unity MenuItem verification remains only as a temporary local fallback while #49–#53 replace it. Do not add new automation through the removed local MCP PowerShell/request-file path. New verification work belongs in the U1–U4 pipeline and must emit machine-readable evidence.

## Goal Contract
For non-trivial work define before editing:

- **Goal** — the player/developer outcome that must exist at the end;
- **Contract** — what may change and what must remain unchanged;
- **Required Evidence Level** — the minimum U1–U5 level that can actually falsify the changed behavior;
- **Acceptance Criteria** — deterministic conditions for completion at that evidence level;
- **Evidence** — files, tests, workflow runs, runtime artifacts, screenshots only when they are legitimate evidence, or release receipts;
- **Stopping Condition** — the fixed point after which further work is a separate outcome.

The Contract is both the minimum required result and the maximum allowed scope. Do not broaden a browser-game change into Unity work or a package-resolution change into VRChat runtime work unless the acceptance criteria require it.

## Complexity Ratchet
For the same user-visible capability, UX, and required evidence level, prefer the implementation with fewer production responsibilities, files, lines, settings, dependencies, adapters, and execution paths.

- Reuse an existing canonical component before adding a new abstraction.
- A new production file or dependency must own a responsibility that cannot be cleanly absorbed by an existing canonical path.
- Replacing a path means deleting the superseded path in the same workline; Git history is the archive.
- Do not reduce tests, observability, fail-fast behavior, or U1–U5 evidence merely to reduce LOC.
- Keep retry/recovery policy in the execution/workflow layer when possible; domain logic should fail visibly instead of converting invalid state into plausible defaults.
- Use the existing `Taskfile.yml` interface rather than adding parallel shell/PowerShell/npm command surfaces for the same intent.
- Before/after reports for non-trivial refactors must include production file/line delta and user-visible capability/evidence delta. A larger implementation requires an explicit reason.
- Net-new framework code with no new verified player/developer outcome is a regression.

## Goal-Driven Execution Loop
For work that cannot be completed in one edit, keep one Goal active and iterate:

```text
inspect current repository + workline
  -> identify minimum required evidence level
  -> implement smallest coherent change
  -> run cheapest relevant verifier
  -> inspect actual evidence
  -> repair if falsified
  -> escalate toward the required U-level only when necessary
  -> stop at the fixed point
```

A failed check is input to repair, not permission to weaken the gate. A lower-level PASS does not compensate for missing higher-level evidence when the changed surface requires it.

## Durable Continuation
Before creating work:

1. inspect current `main`, relevant Issues, open PRs, branches, CI, canonical manifests, and existing U1–U5 evidence;
2. continue the existing canonical Issue/branch/PR when it already owns the same Goal;
3. otherwise create one bounded workline;
4. do not create competing branches, duplicate verification pipelines, alternate manifests, or replacement implementations for the same outcome.

When work cannot finish, leave the canonical workline resumable. Record in the owning Issue/PR or existing machine-readable evidence surface:

- last verified commit/revision;
- Goal and remaining acceptance criteria;
- highest evidence level actually achieved;
- failing stage or missing environment/evidence;
- exact next action required to advance one evidence level or reach the fixed point.

Do not invent a second state database merely for agent memory. Repository state, canonical Issue/PR state, workflow artifacts, and the U1–U5 evidence model are the continuation authority.

## Build and Test
Canonical bootstrap and fast check:

```text
task setup
task check
```

Useful focused checks:

```text
task vpm:check
node --test pages/games/answer-impostor/engine.test.mjs
node scripts/verify-repository-ratchet.mjs
```

`task setup` installs the pinned `vrc-get` release asset after SHA-256 verification. U1 validates the exact Unity policy, VPM SDK target consistency, `vrc-get resolve` reproducibility, canonical manifest non-mutation, and `vrc-get outdated`; evidence belongs in CI Job Summary / runtime artifacts, not the repository.

Unity automation must use the exact version from `ProjectSettings/ProjectVersion.txt`. VPM dependencies must be reproducible from the canonical manifests. Runtime evidence belongs in CI/workflow artifacts, not committed `Latest*.txt` or dated screenshots.

## Evidence-Driven Completion
Do not equate code written, files generated, browser CI green, or a successful Unity import with completion.

Completion evidence must match the changed surface:

- browser-only behavior may be proven with the relevant browser/static/unit path;
- VPM/package integrity requires U1 evidence;
- Unity compile/editor semantics require U2;
- ClientSim/local runtime semantics require U3;
- actual networking/build behavior requires U4 when the contract claims it;
- uploaded/private-world release behavior requires U5 when the contract claims it.

Treat material claims as:

- **VERIFIED** — directly supported by current repository/test/CI/runtime evidence at the required level;
- **OBSERVED** — explicitly supplied observation;
- **INFERRED** — derived from evidence and reported as inference;
- **UNVERIFIED** — not inspected and never stated as fact;
- **FABRICATED** — forbidden.

A verifier that did not run is not PASS. A screenshot cannot prove networking semantics that require U4/U5. ClientSim cannot be promoted into actual VRChat evidence.

## Tooling Applicability
Do not add generic tooling merely because it is part of a cross-repository baseline. For the current repository state:

- Unity/VRChat/VPM: native Unity + VRChat/VPM verification is authoritative.
- Browser JavaScript: Node syntax/unit/static checks are authoritative; there is no package-managed TypeScript application here.
- Python / Pyrefly / Ruff / Pydantic: N/A unless maintained Python code is introduced.
- TypeScript / Biome / Oxlint / `tsc` / Zod: N/A for the current app surface.
- Nx/Turborepo: N/A; there is no independently buildable JS/TS monorepo graph requiring orchestration.
- `prek`: N/A; do not introduce a Python hook runtime solely to wrap the existing native checks.
- Blender: no `.blend` or Blender automation is canonical in current main; future DCC headless verification is tracked by #58 and must remain a separate evidence class from Unity/VRChat success.

## Coding Style
Use four-space indentation and UTF-8 for C#/UdonSharp. Preserve serialized inspector references and GUIDs. Prefer the smallest implementation that fixes the root cause. Keep networking behavior explicit: ownership, serialization, synced state, and late-join behavior must be testable rather than implied.

## Change Rules
- One canonical implementation per responsibility; delete superseded adapters and temporary shims after replacement.
- Do not preserve dead structures for history; Git history is the archive.
- Do not introduce machine-specific absolute paths into canonical tasks or docs.
- Do not commit generated verification output.
- Do not make third-party Unity MCP transport a required CI or release dependency.
- Do not attach credential-bearing Windows execution directly to public-repository pull-request code.
- Changes affecting Unity/VRChat behavior must identify the minimum required evidence level (U1–U5).
- SDK upgrades must be coupled to the lowest Unity/VRChat execution evidence required to prove compatibility; U1 drift reporting alone is not permission to auto-upgrade.

## Builder / Auditor Separation
Treat implementation and acceptance as separate phases even when one agent performs both sequentially.

### Builder
May modify code, scenes, prefabs, manifests, workflows, tests, browser content, and documentation within the bounded Goal Contract.

### Auditor
Independently verifies:

- the requested player/developer outcome exists;
- the claimed U-level is actually supported by evidence from that level;
- lower evidence was not promoted into a stronger runtime claim;
- exact-head CI belongs to the reviewed revision;
- required runtime artifacts/release receipts correspond to the intended commit/build;
- no serialized GUID/reference or canonical manifest boundary was silently broken;
- task-created residue and duplicate worklines are removed.

Implementation intent is never acceptance evidence.

## Pull Requests
Describe player/developer impact, changed scenes/prefabs/scripts, required evidence level, and produced evidence. A PR is not complete merely because browser/static CI passes when the changed surface requires Unity or VRChat execution.

## Fixed Point
Stop when all are true:

- the requested Goal exists;
- the minimum required U1–U5 evidence level has been reached and inspected, or an exact external-environment blocker is recorded;
- all acceptance criteria that can be proven in the available environment are satisfied without overstating unavailable runtime evidence;
- exact-head CI is verified when applicable;
- owning Issue/PR state is correct;
- superseded temporary code, duplicate branches/PRs, and task-created residue are removed;
- additional ideas would not change the current Goal or required evidence and therefore belong to a separate workline.

At the fixed point, stop. Do not keep expanding the task merely because adjacent Unity/VRChat work is possible.

## Final Report Contract
Report verified state rather than activity. Include as applicable:

- Goal and player/developer impact;
- Issue/PR/commit URL;
- required and achieved evidence level (U1–U5);
- exact tests/CI/runtime evidence;
- merge/release result when in scope;
- cleanup result;
- production file/line delta and user-visible capability/evidence delta for non-trivial refactors;
- blocker and exact next action when unfinished.

Never claim a Unity/VRChat behavior at a stronger evidence level than was actually executed.
