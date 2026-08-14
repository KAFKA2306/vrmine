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

## Pull Requests
Describe player/developer impact, changed scenes/prefabs/scripts, required evidence level, and produced evidence. A PR is not complete merely because browser/static CI passes when the changed surface requires Unity or VRChat execution.
