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

実行前に、生成シーンへ不足要素だけを追加するidempotent upgradeと公開タイトル置換を適用する。同じシーンを再importしても、変更がなければ保存しない。

検査項目:

- `BoardGameShowcase.unity`
- `VRCSceneDescriptor`が1個
- spawnとreference camera
- RULEFORGE、ECHO MINE、CHESSのmanagerが各1個
- `NetworkVerificationProbe`が1個
- `TrickSeatLifecycle`が1個
- 152以上の操作対象
- 全UdonSharp behaviourにprogram asset
- RULEFORGEの同期配列容量
- 人数選択7ボタン
- 中央場札5表示
- 公開タイトル`RULEFORGE`と`ECHO MINE`

証跡: `Assets/KafkaMade/VRMine/Verification/LatestBoardGamesVerification.txt`

## G2: Deterministic desktop rules

Unityメニュー:

`VRMine > Verification > Run Board Games Runtime Gate`

検査項目:

- RULEFORGE: 3人配札、フォロー、低数字優先、複合切り札、ルール26の伏せ札→公開処理、満席待機
- ECHO MINE: 代表的な反射・吸収、未参加席スキップ、誤答上限席スキップ
- CHESS: 基本移動、キャスリング、メイト、ステイルメイト、4種昇格
- Play Mode終了とEdit Mode復帰

証跡: `Assets/KafkaMade/VRMine/Verification/LatestBoardGamesRuntimeVerification.txt`

G2は自己テストに列挙されたケースだけを証明する。全60ルールの全組合せ、全ECHO MINE配置、全チェス合法局面の網羅証明ではない。

## G3: VRChat two-client evidence

### 実行

1. `VRMine > Verification > Build And Test Two Clients`
2. Editorが試験固有の正整数`RunToken`と`StartedUtc`を証跡へ記録する。
3. 2クライアントが同一インスタンスへ入るまで待つ。
4. probeが自動で以下を行う。
   - 初期ownerがRULEFORGE/ECHO MINE/CHESSへ識別値を同期
   - 2クライアントが3ゲームの値を観測
   - 3ゲームとprobeのownershipを2番目のplayerへ移管
   - 新ownerが別の識別値を同期
   - 2クライアントが再同期を観測
   - 各ゲームを通常初期状態へ復元
5. 両クライアントを終了する。
6. `VRMine > Verification > Finalize Two Client Logs`

finalizerは、`StartedUtc`以降に更新され、かつログ行の`run=<RunToken>`が一致する証拠だけを採用する。直近60分の別試験や過去ログを混在させない。

### PASS条件

- 異なるlocal player IDを2個以上検出
- `PUBLISH_BASELINE`
- `OBSERVE_BASELINE`
- `SECOND_CLIENT_SYNC_OBSERVED`
- `OWNERSHIP_TRANSFERRED`
- `REPUBLISH_BY_NEW_OWNER`
- `OBSERVE_REPUBLISH`
- `RESTORED_AFTER_TEST`
- `TRICK`, `ORAPA`, `CHESS`の各game keyについてphase 1とphase 2を2クライアントが観測

証跡: `Assets/KafkaMade/VRMine/Verification/LatestVRChatBuildAndTest.txt`

`RUNNING`または`LAUNCHED`はPASSではない。ClientSimもG3の代替にならない。

### Late join

Build & Testの2クライアントは同時起動されるため、G3は遅延参加を自動証明しない。private upload後、2番目のアカウントをゲーム進行後に参加させ、盤面、手番、得点、参加可能性を手動確認する。これはrelease issueの未完了項目として扱う。

## G4: Upload readiness

Unityメニュー:

`VRMine > Release > Validate Upload Readiness`

G4はidempotent scene upgradeを実行した後、次をすべて再確認する。

- Unity 2022.3.22f1
- Windows 64-bit target
- Worlds SDK 3.10.4
- descriptor存在
- 3ゲームmanagerが各1個
- `NetworkVerificationProbe`が1個
- `TrickSeatLifecycle`が1個
- RULEFORGE人数ボタン `3P/4P/5P`
- ECHO MINE人数ボタン `2P/3P/4P/5P`
- 中央場札5表示とview wiring
- 公開タイトル`RULEFORGE`と`ECHO MINE`
- G1 `Result: PASS`
- G2 `Result: PASS`
- G3 `Result: PASS`

証跡: `Assets/KafkaMade/VRMine/Verification/LatestUploadReadiness.txt`

`Result: BLOCKED`の場合はアップロードしない。

## Static CI

GitHub Actionsの`Project integrity`は、以下のリポジトリ整合性を検査する。

- Unity/SDKバージョン宣言
- PROJECTとMarkdownのローカルリンク
- RULEFORGEルール番号1–60の文書化とルール26の実装説明
- 人数変更、中央場札、退室復旧
- scene upgradeのreentrancy guardと変更時のみ保存する条件
- G3の`RunToken`、開始時刻、同一試験ログ限定
- G3/G4のfail-closed条件
- Pagesの独自タイトルとstatus JSON生成

このCIのPASSはUnityコンパイル、UdonSharpコンパイル、VRChat実クライアント動作を証明しない。G0–G4の代替ではない。

## 現在の証拠状態

2026-07-20に旧コードでG1/G2はPASSしたが、その結果は本ブランチのコード変更後の保証ではない。旧G3はVRChatクライアントの実行ファイルパスで失敗し、旧実装には起動後に無条件PASSを書ける欠陥と、複数試験のログを混在できる欠陥があった。

本ブランチではSDKを3.10.4へ更新し、G3を試験固有トークン付きログ証跡方式へ置換し、人数変更、中央場札、退室復旧、独自公開タイトルを追加した。対象Windows PC上でG0–G4を再実行していないため、過去レポートを現在のPASSとして流用しない。
