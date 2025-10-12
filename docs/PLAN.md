# VRMine 最小動作計画

## ゴール
- Unity 2022.3.22f1 と VRChat SDK3-Worlds を使用し、`Assets/KafkaMade/VRMine/Scenes/MVP.unity` 上で波の発射からログ表示、完全一致判定、勝利演出までをホスト/リモート両方で検証する。
- 1 営業日内に Play Mode 検証と Build & Test を完了し、検証ログとスクリーンショットを `docs/` 配下に集約する。
- 完全一致フローで使用する UI・同期データの差分を README/ガイドへ反映し、次タスクへ引き継げる状態に整える。

## 実施前提
- 開発環境は `docs/DEV_SETUP.md` 手順で整備済みで、`unity-editor` コマンドが利用可能。
- VRChat SDK Control Panel にログイン済みでローカル Build & Test が即時実行できる。
- Prefab と UdonSharp スクリプトが `Assets/KafkaMade/VRMine/` 配下で編集可能な状態である。
- Git LFS で大容量アセットが取得済みで、差分管理に支障がないことを確認している。

## 使用アセット
- シーン: `Assets/KafkaMade/VRMine/Scenes/MVP.unity`
- Prefab: `GameController`, `PlayerClient`, `WaveSimulator`, `LogBoard`（`Assets/KafkaMade/VRMine/Prefabs/`）
- スクリプト: `GameController`, `PlayerClient`, `WaveSimulator` の UdonSharpBehaviour（`Assets/KafkaMade/VRMine/Udon/`）
- UI: ログテキスト、完全一致ボタン、勝利メッセージ用プレハブ
- 参照基準: Inspector 参照は Missing なし、`BehaviourSyncMode` は Manual を維持

## タイムライン (1 日)
1. 09:00-09:30 セットアップ確認
   - `unity-editor -projectPath .` でプロジェクトを開き、`docs/GUIDE.md` と照合してシーンをロード。
   - Prefab 配置と Inspector 参照を確認し、差分候補を `docs/` 内にメモ。
2. 09:30-11:00 シーン整備
   - `PlayerClient` → `GameController` → `WaveSimulator` → `LogBoard` の参照を点検し、`[UdonSynced]` 配列が宣言位置で初期化されているか確認。
   - 完全一致ボタンの UI 状態（待機/成功/失敗）を 1 Canvas 内で切り替える仕組みを実装・確認。
   - Inspector の Owner 設定と `Networking.SetOwner` 呼び出し順を見直し、同期破綻の原因を排除。
3. 11:00-13:00 波経路動作テスト
   - 入口 A1/A8/B4 の経路で色と出口ログを記録し、`LogBoard` 表示とスクリーンショットを取得。
   - ループ/吸収ケースを `WaveSimulator` のテストセルで再現し、ログ書式を揃える。
   - 操作後に `RequestSerialization()` が必ず呼ばれることを UdonBehaviour で確認。
4. 13:00-15:00 完全一致フロー実装
   - ボタン押下で `GameController` が盤面を判定し、勝利テキストとペナルティ文言を切り替える挙動を確認。
   - ログリング 20 件が循環表示するようリングバッファを更新し、Scene/Prefab 双方で参照ズレが無いことを確認。
   - `docs/GUIDE.md` に UI 操作フローと判定仕様の変更点を追記。
5. 15:00-16:30 ビルドと同期検証
   - Control Panel の Build & Test を実行し、ホストとクライアント 2 枠で検証。
   - 波の色とログの同期、完全一致フロー完遂を双方でキャプチャし、差異があれば原因と対処を記録。
   - Upload ログの警告/エラー有無を記録し、必要であれば `docs/testing.md` に追記。
6. 16:30-17:00 仕上げ
   - 変更差分を確認し、Prefab/Scene の参照崩れが無いことを検証。
   - `docs/testing.md` に追加で判明した確認手順を記録し、次の改善タスクを箇条書きで残す。
   - Build & Test の成果物を整理し、再検証時に使えるチェックリストを整備。

## 成果物
- 更新済み `Assets/KafkaMade/VRMine/Scenes/MVP.unity`（参照安定）。
- 波ログと完全一致 UI が整った Prefab/UdonSharp スクリプト。
- `docs/testing.md` と `docs/GUIDE.md` に追記した検証記録と操作手順。
- Build & Test のホスト/クライアントスクリーンショット。

## 検証リスト
- `[ ]` `PlayerClient` と `GameController` が Manual Sync で同一 Owner を維持。
- `[ ]` 全 `[UdonSynced]` 配列が宣言位置で初期化済み。
- `[ ]` 波を 20 回連続で撃ってもログ 20 件が循環表示される。
- `[ ]` 完全一致成功時に UI が勝利メッセージへ即反映し、失敗時にペナルティ文言が表示される。
- `[ ]` Build & Test の Upload ログに警告/エラーが無く、2 クライアントでログ同期を確認。
- `[ ]` `docs/` 配下に検証結果とスクリーンショットが整理され、共有可能な状態になっている。
