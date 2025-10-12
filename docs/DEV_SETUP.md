# VRMine Dev Setup (Minimal)

## Prereqs
- Unity Hub (CLI) with Unity 2022.3.22f1 installed at `~/Unity/Hub/Editor/2022.3.22f1/Editor/Unity`
- `~/.local/bin` on `PATH`
- .NET 8 SDK for the official `vpm` CLI

## First-Time Setup
1. Symlink the Unity editor for tooling parity:
   ```bash
   ln -sf ~/Unity/Hub/Editor/2022.3.22f1/Editor/Unity ~/.local/bin/unity-editor
   ```
2. Install `vrc-get` and confirm it resolves:
   ```bash
   curl -L https://github.com/vrc-get/vrc-get/releases/download/v1.9.1/x86_64-unknown-linux-musl-vrc-get -o ~/.local/bin/vrc-get
   chmod +x ~/.local/bin/vrc-get
   vrc-get --version
   ```
3. Ensure the official `vpm` CLI is present:
   ```bash
   dotnet tool install --global vrchat.vpm.cli
   vpm --version
   ```
4. Register shared VPM repos and export the list:
   ```bash
   vrc-get repo add https://vpm.nadena.dev/vpm.json nadena
   vrc-get repo add https://vcc.vrcfury.com VRCFury
   vrc-get repo add https://lilxyzw.github.io/vpm-repos/vpm.json lilxyzw
   mkdir -p tools
   vrc-get repo export > tools/vpm-repos.txt
   ```

## Project Sync
- Place a Unity project here so `Packages/manifest.json` and `Packages/vpm-manifest.json` exist.
- Run `vpm resolve project .` to install locked packages.
- Use `vrc-get repo import tools/vpm-repos.txt` on new machines before resolving.
- Authenticate to the VRChat package feed (VCC token) prior to adding `com.vrchat.*` packages.

## Quick Smoke
- Launch Play Mode with `unity-editor -projectPath .`.
- Before pushing, rerun `vpm resolve project .` to ensure manifests stay clean.
