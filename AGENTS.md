# VRMine Agent Contract

このファイルを、VRMineで作業するエージェント向けルールの正準とする。
CI/CDは必須。変更したsurfaceのexact headを既存gateで検証する。

## Short-context start

Read this file, then only the files that own the current task. Do not preload all scenes, scripts, Pages, Issues, PR history, or docs.

Before editing, identify:

1. requested outcome
2. affected runtime: browser / Unity / ClientSim / VRChat client / uploaded world
3. canonical implementation/config
4. smallest verifier required for that claim
5. existing Issue/PR/workline, if any

Continue an existing workline when it owns the same outcome. README/Issue prose does not override current code, config, CI, or runtime evidence.

## Canonical surfaces

- Unity/VRChat assets: `Assets/`
- VRMine-owned Unity code: `Assets/KafkaMade/VRMine/`
- browser surface: `pages/`
- package config: `Packages/`
- Unity version: `ProjectSettings/ProjectVersion.txt`
- machine-readable config: `config/`
- verification/automation: `scripts/`
- operator entry point: `Taskfile.yml`

Do not commit `Library/`, downloaded packages, temporary reports, or runtime artifacts.

## Verification levels

Never promote evidence from a lower runtime to a higher one.

- **U1** — package/config/static contract
- **U2** — specified Unity compile/EditMode
- **U3** — PlayMode/ClientSim
- **U4** — real VRChat Build & Test / multi-client
- **U5** — uploaded private/public world

ClientSim does not prove real-client sync, ownership transfer, late join, owner departure behavior, PC/Android parity, or uploaded-world behavior. Unrun verification is not PASS.

## Commands

Use existing Taskfile entries instead of adding parallel wrappers.

```text
task setup
task check
task vpm:check
task release:perspective-cage:u2
task gaussian:open
task gaussian:verify-u2
task gaussian:verify-sdk
task pages:test
```

Read `Taskfile.yml` only as needed for the current surface.

## Change rules

- one responsibility, one implementation/config/state/verification path.
- `DELETE > MERGE > REPLACE > ADD`; remove superseded paths after current references prove them unused.
- prefer existing standard APIs/frameworks and canonical implementations.
- do not hide invalid state with plausible defaults, silent fallback, broad exception handling, or unverified success.
- keep retries/recovery at the execution/workflow boundary when possible.
- preserve Unity serialized references and GUIDs; do not change tracked `.meta` files unintentionally.
- make ownership, serialization, synced state, and late-join behavior explicit when relevant.
- comments should explain non-obvious rationale/external constraints, not narrate code.

## Generated assets: render first, never stop on appearance

For generated 3D/image assets, appearance is evidence for the user, not a merge gate.

- Once generation starts, continue mechanically through generation, file-integrity verification, multi-angle rendering, PR/Issue/comment publication, merge, exact-main read-back, and public Pages verification. Do not pause for visual approval.
- Visual PASS/FAIL, taste, polish, product-readiness, or reviewer preference must never cause Draft conversion, REQUEST_CHANGES, manual-approval waiting, or a merge hold.
- A generated-asset PR must not require an unresolved-review-free state. Technical generation failure may fail loudly only when the requested result was not produced or the produced files are structurally invalid.
- Every generated-asset workline must publish rendered images in the PR body, the owning Issue body, and comments on both. Use direct-render URLs, not artifact ZIP/workflow/repository-file pages.
- For a 3D scene or asset set, publish multiple viewpoints. Retro Café requires at least hero, front, rear, left, right, and top.
- If no owning Issue is linked, automation creates one rather than stopping the merge.
- Missing U2-U5 runtime evidence remains `UNVERIFIED`; it is recorded, not used to hold generated work outside `main`.

## CI, Pages, and release

Use a PR for traceability and run the exact-head checks required by the changed surface. `task check` is the repository-level gate when applicable. Generated-asset PRs follow the automatic integration rule above and do not wait for visual/manual approval.

After merge, read back exact `main`. Pages changes additionally require deployment success and direct verification of `https://kafka2306.github.io/vrmine/`. Browser evidence never proves Unity/VRChat behavior.

When a workflow produces a PNG intended for review, the PNG must also have a stable public URL that opens the image itself in one click. GitHub Actions artifact ZIPs, workflow pages, or repository file pages are not substitutes. The public PNG must be verified after deployment; for generated assets, CI must also prove that the published PNG matches the current generated PNG.

CI, merge, Pages deployment, U2-U5 verification, and world release are separate claims. Report only the layer directly observed.

## Continuation and completion

If work stops, update the existing Issue/PR with the last verified commit, outcome, achieved verification level, blocker/failure, and one exact next action. Do not create a second agent-state database.

Complete only when the requested outcome exists, the required verification level has direct evidence, exact-head CI/main read-back are complete when applicable, Pages production is checked when affected, and obsolete task-created paths are removed. Separate new ideas into new outcomes.
