# Architecture Rules: Board Game Lab

## Core Principles
1.  **One Source of Truth**: Only the Manager (Master) knows the real game state.
2.  **Manager Owns State**: Game logic and [UdonSynced] variables live in the Manager.
3.  **Objects are Visual Only**: Pieces, dice, and tiles only display what the Manager tells them.
4.  **No Gameplay Logic in Pieces**: Pieces report interaction; they do not calculate outcomes.
5.  **No Transform Truth**: Never rely on an object's world position as the state. Use integer indices (Grid ID, Slot ID).
6.  **Late Join Reconstruction**: Every visual state must be reconstructible from synced variables via `FieldChangeCallback`.
7.  **Explicit Ownership**: Every synced variable must have a clear owner-update flow to prevent collisions.
8.  **Independent Levels**: Every level must be testable in its own isolated scene.
