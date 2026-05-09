# Testing Guide

## Scope
This repo is validated by Unity Play Mode and VRChat Build & Test. There is no automated test suite yet, so the main goal is to verify sync, late join, and inspector wiring without breaking scene reconstruction.

## Before You Test
1. Open the project with Unity 2022.3.22f1.
2. Run `VRChat SDK > Udon Sharp > Refresh All UdonSharp Assets`.
3. Open `Assets/KafkaMade/VRMine/Scenes/MVP.unity`.
4. Check that `GameController`, `PlayerClient`, `WaveSimulator`, `BoardState`, `LogStream`, and `LogBoard` still resolve in the scene and prefab.

## Level 0: Sync Button
- Take ownership on first interact.
- Toggle the material on both clients.
- Confirm `RequestSerialization()` is called after the new value is set.
- Join late and confirm the current color is visible immediately.

## Level 1: Dice System
- Roll from the proxy or manager entry point.
- Confirm the same face is shown on host and remote.
- Confirm the result is visible after a late join.
- Check that the visual only reflects the synced result and does not own the roll logic.

## VRMine Core Flow
- Start a local 2-client Build & Test.
- Trigger a wave through `PlayerClient`.
- Confirm `GameController` updates `BoardState` and `LogStream`.
- Confirm `LogBoard` renders the newest log at the top.
- Declare a match and confirm the board phase and winner state update on both clients.

## Common Failure Patterns
- Missing references in scene or prefab after reload.
- Manual sync state changing without `RequestSerialization()`.
- Late joiner seeing default visuals instead of synced state.
- Log ring wrapping incorrectly after repeated wave events.

## Notes To Record
- Scene used.
- Client count.
- Which object owned the synced state.
- Whether the failure was host-only, remote-only, or both.
- Any inspector field that had to be reassigned.
