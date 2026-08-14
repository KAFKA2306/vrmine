# Repository Guidelines

## Project Structure
Unity/VRChat assets live under `Assets/`, with VRMine-owned code under `Assets/KafkaMade/VRMine/`. Browser games live under `pages/`. VPM and Unity package declarations live under `Packages/`; Unity version is fixed by `ProjectSettings/ProjectVersion.txt`. Do not add generated verification reports, screenshots, `Library/`, downloaded packages, or local machine state to Git.

## Verification Contract
Browser success and VRChat success are separate evidence classes. The target verification architecture is tracked by #54:

- U1: VPM/package resolution (`vrc-get`)
- U2: exact Unity compile + EditMode tests
- U3: PlayMode + ClientSim-supported local semantics
- U4: Windows + actual VRChat Build & Test / multi-client
- U5: private-world release smoke

Do not claim a higher evidence level from a lower one. ClientSim does not certify real VRChat networking, ownership transfer, late join, PC/Quest parity, or uploaded-world behavior.

Legacy Unity MenuItem verification remains only as a temporary local fallback while #48–#53 replace it. Do not add new automation through the removed local MCP PowerShell/request-file path. New verification work belongs in the U1–U4 pipeline and must emit machine-readable evidence.

## Build and Test
Browser checks currently available in-repo:

```text
node --test pages/games/answer-impostor/engine.test.mjs
node scripts/verify-repository-ratchet.mjs
```

Unity automation must use the exact version from `ProjectSettings/ProjectVersion.txt`. VPM dependencies must be reproducible from the canonical manifests. Runtime evidence belongs in CI/workflow artifacts, not committed `Latest*.txt` or dated screenshots.

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

## Pull Requests
Describe player/developer impact, changed scenes/prefabs/scripts, required evidence level, and produced evidence. A PR is not complete merely because browser/static CI passes when the changed surface requires Unity or VRChat execution.
