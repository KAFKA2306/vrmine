# Unity MCP and VRChat Verification Research

Date: 2026-07-20

## Findings

### Unity official MCP

Unity AI Open Beta includes an official Unity MCP Server. Unity describes it as an editor-connected integration that can inspect project context, scene state, components, Console logs, and trigger editor actions. The current public material targets Unity 6.

- https://unity.com/blog/unity-ai-how-to-get-started
- https://unity.com/blog/mcp-servers-game-development

### MCP for Unity

MCP for Unity supports Unity 2021.3 LTS through Unity 6 and provides scene, GameObject, asset, script, test, and screenshot-related tools. The documented installation path is a Unity Package Manager Git URL. Its release notes include fixes for Unity 2022.3 compatibility.

- https://coplaydev.github.io/unity-mcp/
- https://coplaydev.github.io/unity-mcp/getting-started/install
- https://coplaydev.github.io/unity-mcp/releases

This is the selected bridge for VRMine because the project is Unity 2022.3.22f1.

The installed package and local HTTP server were both verified at version 10.1.0. The server health endpoint responded and Unity registered the `vrmine` editor instance. Project operations are reproducible through `Taskfile.yml` and `src/io/mcp.ps1`.

### Unity tests

Unity Test Framework provides Edit Mode and Play Mode tests. Edit Mode tests are suitable for deterministic board and rule checks. Play Mode tests are suitable for runtime interaction and visual state transitions.

- https://docs.unity3d.com/Packages/com.unity.test-framework@2.0/manual/edit-mode-vs-play-mode-tests.html

### VRChat tests

ClientSim provides local Editor testing for interactions, UI, pickups, stations, and Udon variables. It does not simulate remote players completely, so it cannot replace VRChat client testing.

VRChat Build & Test launches the actual VRChat client. The SDK supports multiple local clients for synced variables, network events, late join behavior, and ownership checks.

- https://creators.vrchat.com/worlds/clientsim/
- https://creators.vrchat.com/worlds/udon/using-build-test/
- https://creators.vrchat.com/worlds/creating-your-first-world/

## Adopted architecture

```text
MCP
  -> observe Unity state
  -> run deterministic verification
  -> capture evidence

Unity Test Framework
  -> rule and state tests

ClientSim
  -> local interaction tests

VRChat Build & Test
  -> actual client and multi-client network tests

SDK Builder validation
  -> upload readiness
```

MCP accelerates the workflow but is not itself evidence. Evidence is the test result, Console output, screenshot, or client result produced by the corresponding gate.

## Implementation result

The first runtime run exposed three cards being expelled by active Rigidbody physics. The accepted board-game interaction model keeps pieces kinematic while idle, lets pickup interaction move their transforms, and snaps them on drop. The repeated ClientSim run passed five-of-five position and velocity checks after ten seconds, movement, snapping, Udon program validity, and a zero-error Console check.
