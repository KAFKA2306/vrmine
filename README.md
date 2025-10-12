# vrmine — VRChat推理ゲーム

**Unity 2022.3.22f1 / SDK3-Worlds / UdonSharp / Manual Sync**

## ルール
- 10×8盤、入口36点
- 波は直進、色セルで90°反射、黒で吸収
- 手番90秒、完全一致宣言で勝利

## コア構成
- `NetConst` : 盤面サイズとリング長を定数化
- `PlayerClient` : 所有者IDと要求を同期するメールボックス
- `GameController` : ログリングとターン管理、`WaveSimulator`呼出
- `WaveSimulator` : 整数格子レイで入口→出口/色/フラグ算出

## 同期チェック
- `[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]`
- `[UdonSynced]`配列は宣言時に`new`で初期化
- 所有者切替後に`RequestSerialization()`
- 参照はインスペクタで手動割当

## 参考
- UdonSharp制限: https://udonsharp.docs.vrchat.com/
- Manual Sync: https://creators.vrchat.com/worlds/udon/networking/variables/
