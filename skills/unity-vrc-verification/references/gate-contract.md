# Gate Contract

Use one report per gate. Include the project path, scene path, Unity/SDK versions, timestamp, checks, Console delta, evidence paths, and final `PASS` or `FAIL`. Preserve a failed detail until a verified rerun replaces it.

Minimum evidence:

- G0: VCC project/version, MCP health, connected Unity instance, compile Console state
- G1: release classification, descriptor, spawn, camera, missing scripts, controller wiring, deterministic reset, Udon program validity, per-scene Console delta
- G2: gameplay profile, rule-fidelity matrix, initial/final state by stable identity, interactions, state transitions, invalid-action assertions, completion/restart, Console state, final Editor play state
- G3: build identity, SDK/client versions, executable preflight, client count, per-game remote observation, late join, owner handoff, client logs
- G4: SDK validation, build target, platform constraints, and links to G0-G3 evidence

`PASS` means every named assertion ran and passed. `SKIP`, missing evidence, or a clean screenshot is not `PASS`.

Do not claim a named commercial game is complete when any rule mapping, component geometry, setup constraint, or end condition is inferred. Label it `prototype` until sourced and asserted.

Never compare Unity objects by `FindObjectsOfType` array index across frames. Capture and compare by stable object name, path, or component identity.
