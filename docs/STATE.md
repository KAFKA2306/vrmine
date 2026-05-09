# Project State: Board Game Lab

## Current Context
- **Level**: 0 (The Collective Pulse + Bug Window)
- **Goal**: Demonstrate sync via a shared game with visual bug reporting.
- **Pass/Fail State**: WAITING_FOR_HUMAN
- **Blocking Issues**: UdonSharp program assets were missing, then regenerated with Setup. Need Unity editor reopen and scene-level validation.

## Progress
- [x] Architecture Rules (ARCHITECTURE_RULES.md) Defined
- [x] Failure Patterns (FAILURE_PATTERNS.md) Defined
- [x] Folder Structure Created
- [x] Long-Term Master Roadmap (ROADMAP.md) Integrated
- [x] Level 0 Runtime Scripts FIXED (Compile errors resolved)
- [x] Level 0 Game Design (Collective Pulse + Bug Window) Implemented
- [x] Level 0 Editor Automation (Diagnostic Game Setup) Updated
- [x] Level 0 Prefab & Test Scene Generated
- [x] Level 0 Human Test Documentation Updated
- [x] VRChat/UdonSharp skills installed for agent workflow

## Files Created/Changed
- `Assets/BoardGameLab/Runtime/Net/BGL_SyncProxy.cs`: FIXED method name mismatch.
- `Assets/BoardGameLab/Runtime/Net/BGL_SyncManager.cs`: Score logic.
- `Assets/BoardGameLab/Runtime/Net/BGL_SyncVisual.cs`: Pulse effect + Bug Window logic.
- `Assets/BoardGameLab/Editor/BoardGameLabSetup.cs`: Build automated Game + Bug Scene.
- `Assets/BoardGameLab/Runtime/Net/*.asset`: UdonSharp program assets regenerated.
- `docs/STATE.md`: Updated state.

## Next Command
Open Unity, refresh UdonSharp, and verify scene/prefab links in the editor.
