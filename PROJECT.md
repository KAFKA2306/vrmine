# VRMine Project Index

このファイルを、収録ゲーム・ルール・実装・シーン・検証・公開ページへの正本リンクとします。

## Release target

- Unity: `2022.3.22f1`
- VRChat Worlds SDK: `3.10.4`
- Upload scene: [`Assets/KafkaMade/VRMine/Scenes/BoardGameShowcase.unity`](Assets/KafkaMade/VRMine/Scenes/BoardGameShowcase.unity)
- Scene generator: [`BoardGameShowcaseBuilder.cs`](Assets/KafkaMade/VRMine/Editor/BoardGameShowcaseBuilder.cs)
- Scene post-upgrade (player counts and seat lifecycle): [`BoardGameSceneUpgrade.cs`](Assets/KafkaMade/VRMine/Editor/BoardGameSceneUpgrade.cs)
- Upload gate: [`VRMineReleaseGate.cs`](Assets/KafkaMade/VRMine/Editor/VRMineReleaseGate.cs)
- Landing page source: [`site/index.html`](site/index.html)
- Pages deployment: [`.github/workflows/pages.yml`](.github/workflows/pages.yml)
- Expected public URL after merge and successful deployment: <https://kafka2306.github.io/vrmine/>

## Game 1: Trick Meister variant

- Rules: [`docs/games/trick-meister.md`](docs/games/trick-meister.md)
- Rule-card implementation matrix: [`docs/games/trick-meister-rules.md`](docs/games/trick-meister-rules.md)
- Controller: [`GameController.cs`](Assets/KafkaMade/VRMine/Runtime/Game/GameController.cs)
- State: [`BoardState.cs`](Assets/KafkaMade/VRMine/Runtime/Data/BoardState.cs)
- Seat departure recovery: [`TrickSeatLifecycle.cs`](Assets/KafkaMade/VRMine/Runtime/Game/TrickSeatLifecycle.cs)
- View/actions: [`BoardGameShowcaseView.cs`](Assets/KafkaMade/VRMine/Runtime/UI/BoardGameShowcaseView.cs), [`BoardGameAction.cs`](Assets/KafkaMade/VRMine/Runtime/UI/BoardGameAction.cs)
- Runtime test entry: `GameController.VerifyRules()`
- Verification report: [`LatestBoardGamesRuntimeVerification.txt`](Assets/KafkaMade/VRMine/Verification/LatestBoardGamesRuntimeVerification.txt)

## Game 2: Orapa Mine automated-puzzle variant

- Rules and differences from the physical product: [`docs/games/orapa-mine.md`](docs/games/orapa-mine.md)
- Controller/simulator and active-seat turn logic: [`OrapaMineGame.cs`](Assets/KafkaMade/VRMine/Runtime/Game/OrapaMineGame.cs)
- View/actions: [`BoardGameShowcaseView.cs`](Assets/KafkaMade/VRMine/Runtime/UI/BoardGameShowcaseView.cs), [`BoardGameAction.cs`](Assets/KafkaMade/VRMine/Runtime/UI/BoardGameAction.cs)
- Runtime test entry: `OrapaMineGame.VerifySimulation()`
- Verification report: [`LatestBoardGamesRuntimeVerification.txt`](Assets/KafkaMade/VRMine/Verification/LatestBoardGamesRuntimeVerification.txt)

## Game 3: Chess

- Rules and implementation scope: [`docs/games/chess.md`](docs/games/chess.md)
- Controller: [`ChessGame.cs`](Assets/KafkaMade/VRMine/Runtime/Game/ChessGame.cs)
- View/actions: [`BoardGameShowcaseView.cs`](Assets/KafkaMade/VRMine/Runtime/UI/BoardGameShowcaseView.cs), [`BoardGameAction.cs`](Assets/KafkaMade/VRMine/Runtime/UI/BoardGameAction.cs)
- Runtime test entry: `ChessGame.VerifyRules()`
- Verification report: [`LatestBoardGamesRuntimeVerification.txt`](Assets/KafkaMade/VRMine/Verification/LatestBoardGamesRuntimeVerification.txt)

## Verification and evidence

- Verification specification: [`docs/verification.md`](docs/verification.md)
- Current inventory and known limits: [`docs/game-inventory.md`](docs/game-inventory.md)
- Release procedure: [`docs/release.md`](docs/release.md)
- Static repository integrity: [`tools/verify_project.py`](tools/verify_project.py)
- Static CI: [`.github/workflows/project-integrity.yml`](.github/workflows/project-integrity.yml)
- Edit-mode scene gate: [`LatestBoardGamesVerification.txt`](Assets/KafkaMade/VRMine/Verification/LatestBoardGamesVerification.txt)
- Play-mode rule gate: [`LatestBoardGamesRuntimeVerification.txt`](Assets/KafkaMade/VRMine/Verification/LatestBoardGamesRuntimeVerification.txt)
- Two-client network evidence: [`LatestVRChatBuildAndTest.txt`](Assets/KafkaMade/VRMine/Verification/LatestVRChatBuildAndTest.txt)
- Final upload gate: [`LatestUploadReadiness.txt`](Assets/KafkaMade/VRMine/Verification/LatestUploadReadiness.txt)
- Network probe: [`NetworkVerificationProbe.cs`](Assets/KafkaMade/VRMine/Runtime/Net/NetworkVerificationProbe.cs)
- Gate implementation: [`BoardGameVerification.cs`](Assets/KafkaMade/VRMine/Editor/BoardGameVerification.cs)

## Required PASS chain

`G0 environment → G1 scene/Udon structure → G2 deterministic runtime rules → G3 two-client sync and ownership transfer → G4 upload readiness`

`Build And Test`の起動、Unityのコンパイル成功、過去のPASSファイルのいずれか単独では動作保証になりません。最終的なアップロード可否は `LatestUploadReadiness.txt` の最新実行結果のみで判定します。
