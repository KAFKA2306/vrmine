# VRMine 最小動作計画

## ゴール
Unity 2022.3.22f1 と VRChat SDK3-Worlds を使用し、`Assets/KafkaMade/VRMine/Scenes/MVP.unity` 上で波の発射からログ表示、完全一致判定までをホスト/リモート両方で確認する。1〜2名、1営業日内にPlay Mode検証とBuild & Testの同期確認を完了する。

## 実施前提
- 開発環境は `docs/DEV_SETUP.md` 手順で整備済みで、`unity-editor` コマンドが利用可能
- VRChat SDK Control Panel にログイン済みでローカル Build & Test が即時実行できる
- 既存 Prefab と UdonSharp スクリプトが `Assets/KafkaMade/VRMine/` 配下で編集可能な状態

## 使用アセット
- シーン: `Assets/KafkaMade/VRMine/Scenes/MVP.unity`
- Prefab: `GameController`, `PlayerClient`, `WaveSimulator`, `LogBoard`（`Assets/KafkaMade/VRMine/Prefabs/`）
- スクリプト: `GameController`, `PlayerClient`, `WaveSimulator` の UdonSharpBehaviour（`Assets/KafkaMade/VRMine/Udon/`）
- UI: ログテキスト、完全一致ボタン、勝利メッセージ用プレハブ
- 参照基準: Inspector 参照は Missing なし、`BehaviourSyncMode` は Manual を維持

## 1日タイムライン
1. 09:00-09:30 セットアップ確認
   - `unity-editor -projectPath .` でプロジェクトを開き、`docs/GUIDE.md` に沿ってシーンを表示
   - Prefab 配置と Inspector 参照を確認し、差分をメモ
2. 09:30-11:00 シーン整備
   - `PlayerClient` → `GameController` → `WaveSimulator` → `LogBoard` の参照を点検
   - `[UdonSynced]` 配列を宣言位置で初期化し、`Networking.SetOwner` 呼び出し箇所を確認
   - 完全一致ボタンの UI 状態（待機/成功）を 1 Canvas 内で切り替え
3. 11:00-13:00 波経路動作テスト
   - 入口 A1/A8/B4 の経路で色と出口ログを記録し、`LogBoard` 表示を確認
   - ループ/吸収ケースを `WaveSimulator` のテストセルで再現し、ログ書式を揃える
   - 操作後に `RequestSerialization()` が必ず呼ばれることを UdonBehaviour で確認
4. 13:00-15:00 完全一致フロー実装
   - ボタン押下で `GameController` が盤面を判定し、勝利テキストを表示
   - 判定失敗時のペナルティ文言を UI に追加し、`docs/GUIDE.md` の説明と一致させる
   - ログリング 20 件が循環表示するようリングバッファを更新
5. 15:00-16:30 ビルドと同期検証
   - Control Panel の Build & Test を実行し、ホストとクライアント 2 枠で検証
   - 波の色とログの同期、完全一致フロー完遂を双方でキャプチャ
   - Upload ログの警告/エラー有無を記録
6. 16:30-17:00 仕上げ
   - 変更差分を確認し、Prefab/Scene の参照崩れが無いことを検証
   - `docs/testing.md` に追加で判明した確認手順を記録
   - 次回タスクやリスクをメモして終了

## 成果物
- 更新済み `Assets/KafkaMade/VRMine/Scenes/MVP.unity`（参照安定）
- 波ログと完全一致 UI が整った Prefab/UdonSharp スクリプト
- `docs/testing.md` に追記した検証記録
- Build & Test のホスト/クライアントスクリーンショット

## 検証リスト
- `[ ]` `PlayerClient` と `GameController` が Manual Sync で同一 Owner を維持
- `[ ]` 全 `[UdonSynced]` 配列が宣言位置で初期化済み
- `[ ]` 波を 20 回連続で撃ってもログ 20 件が循環表示される
- `[ ]` 完全一致成功時に UI が勝利メッセージへ即反映し、失敗時にペナルティ文言が表示される
- `[ ]` Build & Test の Upload ログに警告/エラーが無く、2 クライアントでログ同期を確認
