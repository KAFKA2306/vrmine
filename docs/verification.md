# VRMine Verification System

アップロード前にG0からG4を順番に通す。MCP接続、Unityコンパイル、シーン生成、Build & Test起動、スクリーンショットのいずれか単独では動作保証にしない。

## G0: Environment

- VRChat Creator Companionでプロジェクトを開く。
- Unity `2022.3.22f1` を使用する。
- VCC Resolve後、`Packages/manifest.json` とVCC表示がWorlds SDK `3.10.4`で一致する。
- Build TargetをWindows 64-bitにする。
- Consoleのコンパイルエラーを0にする。

## G1: Scene and Udon structure

Unityメニュー:

`VRMine > Verification > Run Board Games Gate`

検査項目:

- `BoardGameShowcase.unity`
- `VRCSceneDescriptor`が1個
- spawnとreference camera
- 3ゲームmanagerが各1個
- `NetworkVerificationProbe`が1個
- 145以上の操作対象
- 全UdonSharp behaviourにprogram asset
- Trickの同期配列容量

証跡: `Assets/KafkaMade/VRMine/Verification/LatestBoardGamesVerification.txt`

## G2: Deterministic desktop rules

Unityメニュー:

`VRMine > Verification > Run Board Games Runtime Gate`

検査項目:

- Trick: 3人配札、フォロー、低数字優先、複合切り札
- Orapa: 代表的な反射・吸収
- Chess: 基本移動、キャスリング、メイト、ステイルメイト、4種昇格
- Play Mode終了とEdit Mode復帰

証跡: `Assets/KafkaMade/VRMine/Verification/LatestBoardGamesRuntimeVerification.txt`

G2は自己テストに列挙されたケースだけを証明する。全60ルール、全Orapa配置、全チェス合法局面の網羅証明ではない。

## G3: VRChat two-client evidence

### 実行

1. `VRMine > Verification > Build And Test Two Clients`
2. 2クライアントが同一インスタンスへ入るまで待つ。
3. probeが自動で以下を行う。
   - 初期ownerがTrick/Orapa/Chessへ識別値を同期
   - 両クライアントが3ゲームの値を観測
   - 3ゲームとprobeのownershipを2番目のplayerへ移管
   - 新ownerが別の識別値を同期
   - 両クライアントが再同期を観測
   - 各ゲームを通常初期状態へ復元
4. 両クライアントを終了する。
5. `VRMine > Verification > Finalize Two Client Logs`

### PASS条件

- 異なるlocal player IDを2個以上検出
- `PUBLISH_BASELINE`
- `OBSERVE_BASELINE`
- `OWNERSHIP_TRANSFERRED`
- `REPUBLISH_BY_NEW_OWNER`
- `OBSERVE_REPUBLISH`
- `RESTORE_OR_LATE_JOIN`
- `RESTORED_AFTER_TEST`
- `TRICK`, `ORAPA`, `CHESS`の各ゲームについてphase 1とphase 2を2クライアントが観測

証跡: `Assets/KafkaMade/VRMine/Verification/LatestVRChatBuildAndTest.txt`

`RUNNING`または`LAUNCHED`はPASSではない。ClientSimもG3の代替にならない。

## G4: Upload readiness

Unityメニュー:

`VRMine > Release > Validate Upload Readiness`

G4は次をすべて再確認する。

- Unity 2022.3.22f1
- Windows 64-bit target
- Worlds SDK 3.10.4
- descriptor存在
- G1 `Result: PASS`
- G2 `Result: PASS`
- G3 `Result: PASS`

証跡: `Assets/KafkaMade/VRMine/Verification/LatestUploadReadiness.txt`

`Result: BLOCKED`の場合はアップロードしない。

## 現在の証拠状態

2026-07-20に旧コードでG1/G2はPASSしたが、その結果は本ブランチのコード変更後の保証ではない。旧G3はVRChatクライアントの実行ファイルパスで失敗し、しかも旧実装は起動後に無条件PASSを書ける欠陥があった。

本ブランチではSDKを3.10.4へ更新し、G3をログ証跡方式へ置換した。対象Windows PC上でG0–G4を再実行していないため、リポジトリ内の過去レポートを現在のPASSとして流用しない。
