# Level 0: Sync Button Test Checklist

## 1. Unity Inspector Setup
- [ ] Create a Cube in the scene named `SyncButton`.
- [ ] Attach `SyncButton.cs` to the `SyncButton` object.
- [ ] Assign `MeshRenderer` of the cube to the `_renderer` field.
- [ ] Create two Materials: `Mat_On` (Green) and `Mat_Off` (Red).
- [ ] Assign `Mat_On` to `_onMaterial` and `Mat_Off` to `_offMaterial`.
- [ ] Ensure `VRC_SceneDescriptor` is in the scene.
- [ ] Ensure `UdonBehaviour` on the object has `Sync Method` set to `Manual`.

## 2. Prefab Creation Steps
- [ ] Create folder: `Assets/KafkaMade/VRMine/Prefabs/Net/`.
- [ ] Drag the `SyncButton` object from Hierarchy into this folder.
- [ ] Verify the Prefab Asset has the script and materials correctly assigned.

## 3. Build & Test 2-Client Procedure
- [ ] Open `VRChat SDK` > `Control Panel` > `Builder`.
- [ ] Set `Number of Clients` to `2`.
- [ ] Click `Build & Test`.
- [ ] Wait for two VRChat instances to load.

## 4. Expected Result
- **Interaction**: Player A clicks the button. It immediately turns green (ON).
- **Synchronization**: Player B sees the button turn green (ON) at the same time.
- **Toggle**: Player B clicks the button. It turns red (OFF) for both players.
- **Late Joiner**: If Player C joins while the button is ON, it must appear Green immediately upon entry.

## 5. Failure Cases
- **Ownership Race**: Multiple players clicking simultaneously causes state flickering.
- **Desync**: One player sees Green, the other sees Red.
- **Late Joiner Bug**: Joining player sees the default Red even if the state is ON.
- **No Interact**: Button cannot be clicked (Collider missing or UI layer issue).

## 6. Fixes to Try
- **Desync**: Ensure `RequestSerialization()` is called after setting `IsOn`.
- **Late Joiner**: Confirm `FieldChangeCallback` is correctly spelled and the setter calls `UpdateVisuals()`.
- **No Interact**: Ensure the Cube has a `BoxCollider` and its layer is not `Ignore Raycast`.

## 7. Pass/Fail Checklist
- [ ] Local interaction works (Player A)
- [ ] Remote synchronization works (Player B)
- [ ] Late Joiner synchronization works (Player C)
- [ ] Material swap is correct (On=Green, Off=Red)
