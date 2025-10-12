# はじめに

## ざっくり世界観
波を撃ちこみ、盤面ログを読み解き、完全一致を宣言できたら勝利する推理系VRChatワールドです。盤は10×8マス、入口は36点、色セルで90°反射、黒セルで吸収。制限時間は毎ターン90秒、だからテンポ良く直感と推理をミックスしよう。

## セットアップ3ステップ
1. Unity 2022.3.22f1 と VRChat SDK3-Worlds を用意し、`vrmine` プロジェクトを Unity Hub で開く。
2. `Assets/Vrmine/` でシーンと UdonSharp スクリプトを確認。Inspector で参照が Missing になっていないか必ずチェック。
3. VRChat Control Panel から「Build & Test」を押し、ローカルで同期挙動をテスト。ネットワークで気になる点は `docs/testing.md` にメモ。

## 遊び方イメージ
- `PlayerClient` が波リクエストを投げ、`GameController` が盤面を更新。
- ログリングは20件ぶん。色と出口IDをヒントに、壁や黒セルの配置を推理しよう。
