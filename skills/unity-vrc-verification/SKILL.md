---
name: unity-vrc-verification
description: Evidence-based verification of Unity and VRChat worlds using Unity Editor, MCP, ClientSim, UdonSharp, and VRChat Build & Test. Use when Codex must connect Unity MCP, inventory or classify playable scenes, inspect or wire a VRChat world, run deterministic editor and play-mode gates, diagnose the first real failure, capture Console and screenshot evidence, or establish customer-ready upload confidence.
---

# Unity VRChat Verification

Treat MCP as an automation interface and test artifacts as evidence. A successful tool call, generated scene, structural inspection, or screenshot is not gameplay proof.

## Skill boundaries

- Use `unity-vrc-udon-sharp` for UdonSharp language, ownership, serialization, and networking implementation.
- Use `unity-vrc-world-sdk-3` for SDK components, scene setup, optimization, platform constraints, and upload procedure.
- Use this skill for inventory classification, deterministic gates, runtime evidence, failure isolation, and release confidence.

## Workflow

1. Identify the VCC project, Unity version, active scene, installed SDK and UdonSharp versions, current VRChat client executable, MCP endpoint, verification assets, and Taskfile entry points.
2. Inventory candidate scenes and classify each as `complete-game`, `human-enforced-tabletop`, `component-demo`, `sample`, or `archived`. Select one release candidate; never promote every discovered scene to the game list.
3. Connect MCP and prove the connection with `status`, a connected Unity instance, and a read-only active-scene or hierarchy query. If native MCP tools are unavailable, follow [mcp-http.md](references/mcp-http.md) and reconnect after every Unity domain reload.
4. Run G0 through G4 in order. Stop at the first failure, preserve its exact report and Console output, apply the smallest root-cause fix, and rerun that gate.
5. Leave Unity in Edit Mode with the target scene saved and reports updated.

## Gates

### G0: Environment

- Confirm VCC opened the intended project and Unity version.
- Confirm MCP package/server compatibility and the active local connection.
- Require a clean compile Console.
- A healthy MCP server or successful tool call does not replace clean Console evidence.

### G1: Editor structure

- Require one `VRCSceneDescriptor`, valid spawn points, a valid reference camera, and no missing scripts.
- Require valid UdonSharp backing behaviours, program assets, controller references, pickups, rigidbodies, object sync, and colliders used by the selected game.
- Audit Console deltas while opening each candidate scene. A structurally complete scene with an import or preprocessing error fails.
- Exercise deterministic setup or reset using stable object identity.

### G2: Desktop runtime

- Enter Play Mode and run the selected gameplay profile from reset through restart.
- Capture initial and final state by stable names or IDs, not discovery-array order.
- Exercise real interactions and assert state transitions, transform stability, snapping, counts, scoring, and clean Console state as applicable.
- Stop Play Mode automatically and write a runtime report.

ClientSim proves local VRChat behavior only. It does not prove remote synchronization.

For a named commercial game, run a rule-fidelity audit before assigning `complete-game`. Record every rule, component shape, setup constraint, claim procedure, and draw condition that is implemented, approximated, or missing. A deterministic self-test proves only the assertions it contains.

### G3: VRChat Build & Test

- Require the project SDK version to match the current supported SDK line and require its configured client executable to exist before building.
- Launch two actual clients.
- Prove one real state-changing action for every release-candidate game from client A is observed by client B.
- Launch the second client after the first state change and prove late-join reconstruction.
- Close the owner, perform another action, and prove owner handoff.
- Record build identity, SDK/client versions, client count, per-game state before and after, player IDs, ownership, serialization/deserialization markers, and client log paths.
- Do not replace actual clients with ClientSim or infer synchronization from `[UdonSynced]` fields.

### G4: Upload readiness

- Require SDK validation, correct build target, descriptor, spawn, layers, collision matrix, platform constraints, and passing G0-G3 evidence.
- Do not perform a public upload without explicit authority.

## Failure protocol

Treat the first real failure as the only debugging target. Read the exact stack trace, identify the owning asset or lifecycle boundary, fix the smallest cause, and rerun the same gate. Do not suppress Console output or replace failed assertions with looser thresholds.

When using the HTTP fallback, preserve the MCP session ID for each request. A Unity domain reload can invalidate the bridge; create a new session, re-check the Unity instance, and then repeat the read-only connection proof.

If the SDK expects an executable absent from the installed client, record the configured path, SDK version, client directory, generated `.vrcw`, and launch exception. Update through VCC before adding launch wrappers, copies, symlinks, or registry changes.

## Reference routing

- Read [gate-contract.md](references/gate-contract.md) before creating or reviewing evidence.
- Read [gameplay-profiles.md](references/gameplay-profiles.md) when defining what a playable game must prove.
- Read [scene-audit-and-legacy.md](references/scene-audit-and-legacy.md) when inventorying scenes or diagnosing old prefabs, Udon proxies, reference cameras, or ClientSim physics errors.
- Read [mcp-http.md](references/mcp-http.md) when native MCP tools are unavailable or Unity reconnects after domain reload.
