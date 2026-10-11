<!-- Generated from config/star-schema.json by scripts/generate-star-schema.mjs. Do not edit; change the schema and regenerate. -->

# VRMine Unity project context

## Runtime and packages

- Unity: `2022.3.22f1`
- VRChat Worlds SDK: `3.9.0`
- Unity MCP (com.tunasync.unity-mcp) revision: `50597089589137c5186f5badbde1d72f5d335243`
- Gaussian splat renderer: `MichaelMoroz/VRChatGaussianSplatting@f96c0117cba518ff84d059d36f16909b873e23aa`
- Render pipeline: Built-in Render Pipeline
- World authoring: VRChat SDK world components and UdonSharp

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
- Canonical platform: `windows`
- Target extent (m): `1`
- Presentation scale multiplier: `5`
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
- VRChat publication requires authenticated VRChat SDK state and a deliberate upload action. A local SDK build is not evidence of a published world. Android/Quest performance and public artifact hosting require separate evidence.
