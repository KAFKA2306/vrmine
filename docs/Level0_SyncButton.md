# Level 0: Sync Button Plan (FieldChangeCallback version)

## Objective
Create a simple button that toggles color globally using `FieldChangeCallback` to ensure robust synchronization and late-joiner support.

## Pattern
- **Variable**: `[UdonSynced, FieldChangeCallback(nameof(IsOn))] private bool _isOn;`
- **Setter**: Updates material color automatically when `IsOn` is assigned.

## Components
1. **SyncButton (UdonSharpBehaviour)**
   - `_isOn`: Synced bool.
   - `OnMaterial`: Material for ON state.
   - `OffMaterial`: Material for OFF state.
   - `Interact()`: Toggles `IsOn` after taking ownership.
   - `UpdateVisuals()`: Private method to swap materials.

## File List
- `Assets/KafkaMade/VRMine/Runtime/Net/SyncButton.cs`
- `Assets/KafkaMade/VRMine/Prefabs/Net/SyncButton.prefab`
