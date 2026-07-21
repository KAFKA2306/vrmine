# VRChat Release Procedure

## 1. Prerequisites

- Windows PC with VRChat and Steam installed.
- VRChat Creator Companion.
- Unity `2022.3.22f1`.
- Repository working tree at the commit intended for upload.
- VRChat account with world upload permission.

Official references:

- VRChat supported Unity version: https://creators.vrchat.com/sdk/upgrade/current-unity-version/
- VRChat SDK release notes: https://creators.vrchat.com/releases/
- Build & Test: https://creators.vrchat.com/worlds/creating-your-first-world/

## 2. Resolve the project

1. Add or open the repository from VCC.
2. Run Resolve so `com.vrchat.base` and `com.vrchat.worlds` are installed as `3.10.4`.
3. Open with Unity `2022.3.22f1`.
4. Wait until compilation and UdonSharp compilation finish.
5. Confirm zero Console errors.

The showcase scene is generated and then repaired by an idempotent upgrade. Missing controls, central Trick displays, lifecycle behaviour, and public titles are added; an unchanged scene is not saved again. Manual rebuild is available at:

`VRMine > Build Board Game Showcase`

## 3. Run G1

`VRMine > Verification > Run Board Games Gate`

Required result:

`Assets/KafkaMade/VRMine/Verification/LatestBoardGamesVerification.txt`

must end with:

```text
Result: PASS
```

## 4. Run G2

`VRMine > Verification > Run Board Games Runtime Gate`

Required result:

`Assets/KafkaMade/VRMine/Verification/LatestBoardGamesRuntimeVerification.txt`

must end with:

```text
Result: PASS
```

## 5. Run G3

1. Select `VRMine > Verification > Build And Test Two Clients`.
2. Confirm the report records a positive `RunToken` and `StartedUtc`.
3. Confirm two desktop clients join the same local test instance.
4. Leave both clients open until the automatic probe completes and restores all games.
5. Close both clients.
6. Select `VRMine > Verification > Finalize Two Client Logs`.

The finalizer accepts only log lines whose `run=<RunToken>` matches the current launch report and whose log file was updated after the recorded start time.

Required result:

`Assets/KafkaMade/VRMine/Verification/LatestVRChatBuildAndTest.txt`

must end with:

```text
Result: PASS
```

A report ending in `RUNNING`, `LAUNCHED`, or `FAIL` is not acceptable. This automated two-client run does not prove late join because both clients are launched together.

## 6. Run G4

`VRMine > Release > Validate Upload Readiness`

Required result:

`Assets/KafkaMade/VRMine/Verification/LatestUploadReadiness.txt`

must end with:

```text
Result: PASS
```

This gate is fail-closed. It checks the project version, build target, SDK version, descriptor, generated controls, public titles, and the latest G1–G3 evidence.

## 7. Upload

1. Open `VRChat SDK > Show Control Panel`.
2. Use the Builder tab and run SDK validation.
3. Resolve every error and warning that prevents upload.
4. Build and upload the Windows world.
5. Set name, description, capacity, thumbnail, and visibility in the SDK panel.
6. After upload, create a private instance and repeat a manual smoke test with at least two accounts:
   - join every game;
   - complete one legal action in every game;
   - start play with account A, then join later with account B and verify restored board, turn, score, and seat availability;
   - transfer or change ownership through normal play;
   - reset each game;
   - test every player-count mode intended for release.

## 8. Post-upload evidence

Record in the release PR or release issue:

- uploaded commit SHA;
- Unity and SDK versions;
- world blueprint ID or public world URL, where disclosure is acceptable;
- G1–G4 report contents;
- uploader account and date;
- two-account manual smoke-test and late-join outcome;
- known limitations from `docs/game-inventory.md`.

## Prohibited release claims

Do not state any of the following unless supported by the latest reports and manual post-upload test:

- “all operations guaranteed”;
- “official rules fully reproduced”;
- “upload and immediately works”;
- “late join and ownership transfer verified”;
- “all 60 rules fully tested.”

Use the narrower claim: “The specified G0–G4 gates passed for the cited commit, environment, and test evidence; the recorded private-instance smoke test also passed.”
