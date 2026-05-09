# Failure Patterns: Board Game Lab

## Networking & Sync
*   **Ownership Race**: Multiple players take ownership simultaneously, causing variable flickering or rollback.
*   **Late Join Mismatch**: New players see default values because `OnDeserialization` or `FieldChangeCallback` failed to trigger visuals.
*   **Serialization Spam**: Calling `RequestSerialization` every frame, causing network lag or dropped packets.
*   **Prefab Instance Setup**: Initializing UdonSharpBehaviours on Scene instances instead of the Prefab Asset. 
    *   *Warning*: `Cannot setup behaviour on prefab instance, original prefab asset needs setup`.
    *   *Fix*: Open the Prefab Asset in Prefab Mode, verify UdonSharp setup, and click 'Apply All' to the Asset itself. Always configure the Asset before the Scene instance.
*   **Master Migration**: Current owner leaves; new owner receives state. Logic must handle the handover.
*   **Out-of-order Events**: `SendCustomNetworkEvent` arriving before or after variable sync.

## Interaction & Physics
*   **Transform Drift**: Minor floating point errors causing pieces to slowly move away from their grid.
*   **Pickup Desync**: One player holds an object while another thinks it is on the table.
*   **Double Interact**: Two clicks registered in the same frame causing unexpected state jumps.
*   **Collider Conflict**: Game objects blocking the raycast of the button/piece below them.
