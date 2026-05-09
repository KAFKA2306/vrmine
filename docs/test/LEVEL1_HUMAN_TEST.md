# HUMAN_TEST_3_MIN: Level 1 Dice System

## Objective
Verify that the dice result is owned by the manager, rendered by visuals only, and reconstructed correctly for late joiners.

## Setup
1. Open Unity.
2. Run the Level 1 setup path if available in the editor menu.
3. Confirm the scene has a dice proxy, a manager, and a visual object.
4. Confirm the dice visual has no gameplay logic beyond rendering the synced result.

## Build & Test
1. Open `VRChat SDK > Control Panel > Builder`.
2. Set `Number of Clients` to `2`.
3. Click `Build & Test`.

## Checks
- Client 1 rolls the dice and both clients show the same face.
- Client 2 rolls the dice and both clients show the new face.
- The visual spin happens before the final face appears.
- A late joiner sees the last rolled face immediately.
- The proxy does not change the result directly without manager sync.

## Fail Conditions
- Different faces on host and remote.
- Result resets on join.
- Visual spin happens but the final face never updates.
- Rolling requires editing the visual object instead of the manager.

## Report
Return `Pass` if the same result appears on all clients and late join reconstruction works.
Return `Fail` if the manager state and the displayed face diverge.
