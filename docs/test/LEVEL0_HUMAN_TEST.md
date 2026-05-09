# HUMAN_TEST_3_MIN: Level 0 Architecture Visualization

## Objective
Verify the Manager/Proxy/Object separation in VRChat. The scene should visually demonstrate state ownership, network event propagation, and late join reconstruction.

## Instructions
1.  **Setup Environment**:
    *   Open Unity.
    *   Go to menu: **Tools > BoardGameLab > Setup Level0 SyncButton**.
    *   Confirm a new scene `BGL_Level0_Architecture_Test` is opened.
    *   **Visual Check**: You should see a giant Cyan "SOURCE OF TRUTH" Manager, a Proxy cube, and a Visual cube with a floating text displaying "WAITING FOR SYNC...".
2.  **Build & Test**:
    *   Open `VRChat SDK` > `Control Panel` > `Builder`.
    *   Set `Number of Clients` to `2`.
    *   Click `Build & Test`.
3.  **In-Game Test**:
    *   **Client 1 (Master)**: Look at the text. It should say `OWNER: Player1` and `STATE: OFF` (Red).
    *   **Client 2**: Walk up to the **INPUT PROXY** cube and `Interact`.
    *   **Check**: Does the visual instantly turn **Yellow** (Pending) locally for Client 2?
    *   **Check**: A moment later, does it turn **Green** for BOTH clients?
    *   **Check**: Does the `SERIALIZED` count increase by 1?
4.  **Late Join Demo**:
    *   Close **Client 2**.
    *   **Client 1**: Toggle state to **Green**.
    *   Relaunch **Client 2** (Join the same instance).
    *   **Check**: Does Client 2 see the Visual turn **Blue** (Reconstructing) for 2 seconds before snapping to **Green**?

## Reporting
Return **Pass** if the Manager/Proxy/Visual separation works and all color states (Red/Green/Yellow/Blue) appear correctly.
Return **Fail** if any visual fails to update or the RPC fails.
