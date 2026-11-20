<!-- Generated from config/star-schema.json by scripts/generate-star-schema.mjs. Do not edit; change the schema and regenerate. -->

# VRMine Unity project context

## Runtime and packages

- Unity: `2022.3.22f1`
- VRChat Worlds SDK: `3.9.0`
- vrc-get: `1.9.2`
- Blender: `4.2`
- Unity MCP (com.tunasync.unity-mcp) revision: `2a2cae188d2f01d261bcf97306fe19ff37b57107`
- Render pipeline: Built-in Render Pipeline
- World authoring: VRChat SDK world components and UdonSharp

## Canonical project surfaces

- Mission and deliverables: `MISSION.md`
- Unity assets: `Assets/KafkaMade/VRMine/`
- Public pages: `pages/`
- 3D item specs: `config/world-items/`
- World design specs: `config/world-design/`
- Unity packages: `Packages/`
- Commands: `Taskfile.yml`
- Verification scripts: `scripts/`
- Verification and release policy: `config/quality-gates.json`
- Agent knowledge authority: `config/agent-knowledge.json`
- Always-on agent config: `config/always-on-agent.json`
- Star schema SSOT: `config/star-schema.json`
- VRChat toolchain pin: `config/vrchat-toolchain.json`
- Project version: `ProjectSettings/ProjectVersion.txt`

## Flagship world: Tactile Rain and Fabric Courtyard

- Flagship courtyard spec: `config/world-design/generated/rain-fabric-courtyard-v0.json`
- Target platform: `CROSS_PLATFORM_QUEST`
- Quest max bundle size (MB): `28`
- Realtime lights: `0`

## Verification entry points

- `task check`
- `task merge:check`
- `task ledger:check`
- `task vpm:check`
- `task world-design:check`
- `task courtyard:contracts`
- `task pages:test`
- `task agent:verify`
- `task agent:tick`
- `task agent:status`
- `task cafe:build`
- VRChat publication requires authenticated VRChat SDK state and a deliberate upload action. A local SDK build is not evidence of a published world. Both PC and Quest (Android) actual-client evidence are required for release completion.
