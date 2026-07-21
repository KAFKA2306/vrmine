# VRMine

VRChat向けの3ゲーム統合ワールドです。Unityでプロジェクトを開くと、生成シーン `BoardGameShowcase.unity` に人数選択、中央場札、退室復旧、公開タイトルが自動反映されます。

## 収録ゲーム

| 公開タイトル | 人数 | ルール | 実装 | 検証 |
|---|---:|---|---|---|
| RULEFORGE | 3–5 | [docs/games/trick-meister.md](docs/games/trick-meister.md) | [`GameController.cs`](Assets/KafkaMade/VRMine/Runtime/Game/GameController.cs) | G1/G2 + G3ログ検証 |
| ECHO MINE | 2–5 | [docs/games/orapa-mine.md](docs/games/orapa-mine.md) | [`OrapaMineGame.cs`](Assets/KafkaMade/VRMine/Runtime/Game/OrapaMineGame.cs) | G1/G2 + G3ログ検証 |
| CHESS | 2 | [docs/games/chess.md](docs/games/chess.md) | [`ChessGame.cs`](Assets/KafkaMade/VRMine/Runtime/Game/ChessGame.cs) | G1/G2 + G3ログ検証 |

全リンク、シーン、検証コマンド、証跡の正本は **[PROJECT.md](PROJECT.md)** に集約しています。

## 開始手順

1. VRChat Creator Companionで本プロジェクトを開く。
2. VCCで依存関係をResolveし、Worlds SDK `3.10.4` を取得する。
3. Unity `2022.3.22f1` のコンパイル完了を待つ。生成シーンは自動更新される。
4. `VRMine > Verification > Run Board Games Gate` を実行する。
5. `VRMine > Verification > Run Board Games Runtime Gate` を実行する。
6. `VRMine > Verification > Build And Test Two Clients` を実行し、2クライアント終了後に `Finalize Two Client Logs` を実行する。
7. `VRMine > Release > Validate Upload Readiness` が `Result: PASS` の場合のみアップロードする。

## 動作保証の扱い

このリポジトリは、単なる「Build & Test起動成功」を動作保証として扱いません。アップロード可能状態は、構造検査、規則テスト、中央場札と人数UI、2クライアント同期、遅延復元、所有権移管、3ゲームの再同期がすべて証跡付きでPASSした時だけ成立します。最新結果は [`LatestUploadReadiness.txt`](Assets/KafkaMade/VRMine/Verification/LatestUploadReadiness.txt) を正とします。

履歴上のPASSはコード変更後の保証にはなりません。対象PCでG0–G4を再実行してください。

## 公開ページ

GitHub Pages: <https://kafka2306.github.io/vrmine/>

公開元は [`site/`](site/) です。Pages workflowが成功するまでURLの配信完了とは扱いません。

## 権利と位置づけ

本プロジェクトは非公式の技術検証・ファン実装です。原作のロゴ、製品アート、ルールカード画像、説明書本文は収録しません。ワールド内では独自タイトル `RULEFORGE` と `ECHO MINE` を使用します。参照した製品・競技規則との差分は各ルール文書に明示し、各製品・名称の権利は各権利者に帰属します。
