# VRMine ポップスターター

## ざっくり世界観
波を撃ちこみ、盤面ログを読み解き、完全一致を宣言できたら勝利する推理系VRChatワールドです。盤は10×8マス、入口は36点、色セルで90°反射、黒セルで吸収。制限時間は毎ターン90秒、だからテンポ良く直感と推理をミックスしよう。

## セットアップ3ステップ
1. Unity 2022.3.22f1 と VRChat SDK3-Worlds を用意し、`vrmine` プロジェクトを Unity Hub で開く。
2. `Assets/Vrmine/` でシーンと UdonSharp スクリプトを確認。Inspector で参照が Missing になっていないか必ずチェック。
3. VRChat Control Panel から「Build & Test」を押し、ローカルで同期挙動をテスト。ネットワークで気になる点は `docs/testing.md` にメモ。

## 遊び方イメージ
- `PlayerClient` が波リクエストを投げ、`GameController` が盤面を更新。
- ログリングは20件ぶん。色と出口IDをヒントに、壁や黒セルの配置を推理しよう。
- 完全一致宣言の前に、仲間内で「出口ID読み合わせタイム」を作ると盛り上がる！

## トラブルシュート
- 波がループする場合は `WaveSimulator` の `flags` を確認し、該当セル配置を調整。
- `RequestSerialization()` が抜けていると同期しないので、変更箇所の呼び出し漏れをチェック。
- メタファイルのGUIDがズレたら `unity-editor -quit -batchmode -projectPath . -executeMethod VRChat.Batcher.ReimportAll` を実行。

## チームプレイのヒント
- コミットは「Fix wave exit calc」など短く命令形で。担当したプレハブやシーンをPR本文に書こう。
- `docs/` にルール説明や推理手順を追記すると初見勢も安心。ポップな図解やログ例でワイワイ共有しよう！
