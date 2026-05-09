# Level 1: Dice System Implementation Plan

## Objective
Implement a synchronized dice system using the Manager/Proxy/Visual pattern. The Master (Manager) calculates the random result, ensuring all players see the exact same outcome simultaneously.

## Architecture
- **BGL_DiceManager (Manager)**:
    - `[UdonSynced] int Result`: The current face (1-6).
    - `[UdonSynced] int RollCount`: Increment to trigger visual reset.
    - `_Roll()`: Master-only method to generate `Random.Range(1, 7)`.
- **BGL_DiceProxy (Proxy)**:
    - Cylinder/Button on the table.
    - `Interact()` -> `SendCustomNetworkEvent(Owner, "_Roll")`.
- **BGL_DiceVisual (Visual)**:
    - 6 Mesh variants or a single mesh with 6 Material slots.
    - `FieldChangeCallback(nameof(Result))` -> Updates the visible face.
    - `_OnRollStart()` -> Trigger spinning animation/particle pulse.

## Visual Feedback
- **Spinning**: The dice should spin randomly for 1 second before snapping to the result.
- **Pulse**: Emit a white "Sync Pulse" when the result is serialized.
- **HUD**: Floating text showing "LAST ROLL: X".

## Failure Mitigation
- **Ownership Race**: Proxy uses RPC instead of taking ownership of the Manager.
- **Late Join**: `FieldChangeCallback` ensures the last roll is visible immediately upon joining.

## Next Steps
1. Create `BGL_DiceManager.cs`, `BGL_DiceProxy.cs`, `BGL_DiceVisual.cs`.
2. Update `BoardGameLabSetup.cs` with `SetupLevel1Dice`.
3. Create `docs/test/LEVEL1_HUMAN_TEST.md`.
