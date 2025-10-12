# VRMine 最小動作計画

## ゴール
Unity 2022.3.22f1 と VRChat SDK で、単一シーン上で波射出→ログ表示→完全一致判定まで確認する。チームは1〜2名、1日で完了を目指す。

## 必須要素
- シーン: `Assets/Vrmine/Scenes/MVP.unity`
- プレハブ: `GameController`, `PlayerClient`, `WaveSimulator`, `LogBoard`
- UdonSharpスクリプト: 既存3本を転用し、定数/宣言の初期化を確認
- UI: シンプルなログテキスト＋完全一致ボタンのみ

## タスク
1. シーン作成とプレハブ配置。Inspector参照を全て埋め、同期モードをManualに設定。
2. 波シミュレーションの経路テスト: 入口3箇所でPlay Mode実行し、ログが更新されることを確認。
3. 完全一致ボタン押下で勝利演出（UIテキスト切替）を実装。
4. Control Panel「Build & Test」でローカルワールドを起動し、2クライアントでログ同期を確認。

## 検証リスト
- `[ ]` UdonSynced配列が全て初期化済み
- `[ ]` `RequestSerialization()` 呼び出しが操作後に実行される
- `[ ]` ログリング20件が循環して表示される
- `[ ]` 完全一致失敗時のペナルティ説明をUIに表示
- `[ ]` Build & TestのUploadログにエラーなし

## 次の拡張 (任意)
- 波経路の可視化ライン
- 盤面編集ツール
- リプレイ表示と共有ログエクスポート
