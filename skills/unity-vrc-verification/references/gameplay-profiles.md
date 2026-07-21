# Gameplay Profiles

Classify every discovered scene before testing:

- `complete-game`: Rules, turn or round progression, completion, result, and restart are implemented.
- `human-enforced-tabletop`: Pieces or cards work, but players enforce rules and results.
- `component-demo`: One mechanic is demonstrated without a complete play loop.
- `sample`: Third-party or SDK example content not owned as a release target.
- `archived`: Retained history that is not supported or shipped.

Only `complete-game` can be presented as a finished game. Label `human-enforced-tabletop` explicitly.

Use `prototype` when the loop runs but named-game fidelity remains incomplete or inferred. Technical playability and reproduction fidelity are separate results.

Define a deterministic profile for each release candidate. At minimum prove:

1. Reset produces the expected initial state.
2. A player can perform the primary interaction.
3. The interaction produces the expected visual and domain state.
4. Invalid or out-of-turn actions behave according to the implemented rules.
5. A turn, round, or equivalent progression completes.
6. Win, loss, draw, score, or completion state is observable.
7. Restart returns to the initial state.
8. Console remains clean throughout.

For multiplayer release candidates also prove:

1. Client A changes domain state and client B observes the exact value.
2. A late joiner reconstructs that value without a new action.
3. Ownership transfers after the owner leaves.
4. The new owner changes state and the remaining client observes it.

For human-enforced chess or card tables, replace rule assertions with exact physical invariants such as piece count, deck count, pickup/drop, grid snap, reset, and multi-client transform synchronization. Do not describe those invariants as automated rules.
