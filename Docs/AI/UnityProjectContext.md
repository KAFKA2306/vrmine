# VRMine Unity project context

## Runtime and packages

- Unity: `2022.3.22f1`
- VRChat Worlds SDK: `3.9.0`
- Render pipeline: Built-in Render Pipeline
- World authoring: VRChat SDK world components and UdonSharp
- Gaussian splat renderer: `MichaelMoroz/VRChatGaussianSplatting@f96c0117cba518ff84d059d36f16909b873e23aa`
- Unity MCP: project package `com.tunasync.unity-mcp` pinned in `Packages/manifest.json`

## Canonical project surfaces

- Unity assets: `Assets/KafkaMade/VRMine/`
- World configs: `config/gaussian-worlds/`
- Commands: `Taskfile.yml`
- Verification scripts: `scripts/`
- Project version: `ProjectSettings/ProjectVersion.txt`

## Minoo River Osaka standalone world

- World config: `config/gaussian-worlds/minoo-river-osaka.json`
- Source registry: `config/gaussian-worlds/minoo-river-osaka-sources.json`
- Artifact manifest: `config/gaussian-worlds/minoo-river-osaka-artifacts.yaml`
- Scene: `Assets/KafkaMade/VRMine/Scenes/MinooRiverOsaka.unity`
- Imported prefab: `Assets/KafkaMade/VRMine/GaussianSplatting/Prefabs/minoo-river-osaka.prefab`
- Canonical platform: Windows
- Presentation scale: 5m measured Gaussian extent
- Scene topology: one Gaussian splat object, one Gaussian renderer, one VRC scene descriptor, one PipelineManager, one spawn point, one reference camera
- Local source materialization: `VRMINE_MINOO_PLY` is required by `task minoo:stage-local`; there is no silent source fallback

## Verification entry points

- `task minoo:contracts`
- `task minoo:stage-local`
- `task minoo:prepare-local`
- `task minoo:verify-u2`
- `task minoo:verify-sdk`
- `task minoo:measure`
- `task minoo:build`
- `task minoo:release-candidate`

VRChat publication requires authenticated VRChat SDK state and a deliberate upload action. A local SDK build is not evidence of a published world. Android/Quest performance and public artifact hosting require separate evidence.
