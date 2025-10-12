# Repository Guidelines

## Project Structure & Module Organization
Project assets live under `Assets/`, with UdonSharp scripts and prefabs organized in `Assets/KafkaMade/VRMine/`. Keep inspector-assigned references stable to avoid breaking serialized scenes. Use `docs/` for gameplay notes, network diagrams, and troubleshooting checklists; add concise README files per subdirectory when introducing new systems.

## Build, Test, and Development Commands
Open the project with Unity 2022.3.22f1 (`unity-editor -projectPath .`) to edit scenes and run Play Mode smoke checks. For creator testing, export the world via the VRChat SDK Control Panel and push a local build to VRChat for multi-user validation. Regenerate meta files with `unity-editor -quit -batchmode -projectPath . -executeMethod VRChat.Batcher.ReimportAll` when asset GUID drift is suspected.

## Coding Style & Naming Conventions
We write UdonSharp C# with four-space indentation and UTF-8 files. Mirror existing class names: `PascalCase` for behaviours, `camelCase` for fields and locals, `ALL_CAPS` for constants (see `NetConst`). Avoid `static` classes; prefer `UdonSharpBehaviour` components with serialized fields. Keep networking annotations explicit—`[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]` on sync-heavy behaviours—and initialize `[UdonSynced]` arrays in place. No comments. Minimal codes. Alaways Reduce codes. No error-handling.No try-catch. Find Root cause and fix codes in minimal.

## Testing Guidelines
There is no automated test suite yet; rely on Unity Play Mode for fast iteration and VRChat client builds for authority/synchronization checks. Name new manual test scenes `Tests_<feature>` and store them under `Assets/Vrmine/Tests/`. Document edge cases (loop detection, mailbox contention) in `docs/testing.md` so future work can script them. Target parity across host and remote clients before merging.

## Commit & Pull Request Guidelines
Match the short, imperative commit style already in history (e.g., "Compress docs", "Fix UdonSharp errors"). Scope each commit to one gameplay mechanic or tooling change. Pull requests should describe the player-facing impact, list touched prefabs or scripts, and call out required inspector reassignments. Attach screenshots or GIFs for UI or VFX tweaks, and link VRChat build IDs when asking for validation.

## VRChat & Networking Notes
When adding synced data, track `RequestSerialization()` calls and guard owner-only code with `Networking.IsOwner`. Prefer inspector-assigned references over runtime discovery, and keep serialized array sizes capped (≤900 bytes) to satisfy Udon limits. Document new network events in `docs/networking.md` before shipping.
