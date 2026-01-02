# プロジェクト完遂計画 (Plan to Finish)

SexyDistanceGimmick 完成に向けた、残りの**手動作業手順**をまとめました。
コード、設計、仮素材はすべて実装済みです。あとは「魂」を入れる作業のみです。

---

## 1. 準備フェーズ (Status: ✅ Completed)
* [x] **仕様策定**: `MASTER_SPEC_v2.md`
* [x] **基盤実装**: UdonSharp, Editor Script
* [x] **Unity構築**: Prefab, Scene 自動生成済み
* [x] **仮素材**: プレースホルダー音声・アニメーション生成済み

---

## 2. 実装フェーズ (To Do)

以下の3ステップを順に実行してください。

### Step A: アニメーション作成 (Visual)
* **タスク**: 接近・接触時の「前傾姿勢」を作る
* **参照**: `Docs/ANIMATION_SPEC.md`
* **作業**:
  1. Booth等で「セクシーポーズ集」を入手（または自作）
  2. `Assets/SexyDistanceGimmick/Animations/` 内の `Lean_0` (直立) 〜 `Lean_100` (最大前傾/覆いかぶさり) を差し替え
  3. **Yobai Mode用**: `Crawl_Idle` (四つん這い) 等も必要に応じて作成

### Step B: 音声差し替え (Audio)
* **タスク**: 仮の「ピー音」を本番の「囁きボイス」に差し替える
* **参照**: `Docs/AUDIO_SPEC.md`
* **作業**:
  1. AI音声生成ツール（ElevenLabs等）または声優依頼で wav を作成
  2. `Assets/SexyDistanceGimmick/Audio/` 内の同名ファイルに上書き保存
  3. Unity 上で AudioSource の AudioClip が外れていないか確認

### Step C: アバター適用 (Ghost)
* **タスク**: ゴーストの見た目を決定する
* **参照**: `Docs/AVATAR_SETUP_GUIDE.md`
* **作業**:
  1. `GhostRoot/GhostVisual` の下にお好みのアバターを配置
  2. 必要に応じて半透明シェーダー (Standard Distance Fade 等) を適用

---

## 3. 調整・リリースフェーズ

1. **Unityで再生確認**
   - Head (Camera) に近づいてくるか？
   - 距離に応じて音が変わるか？
   - 接触して反応するか？
2. **ビルド & アップロード**
   - VRChat SDK コントロールパネルからワールドをアップロード
3. **販売 (Optional)**
   - `Docs/BOOTH_DESCRIPTION.txt` を使って商品ページ作成
   - `SexyDistanceGimmick` フォルダを UnityPackage にエクスポート

---

## ゴール
**「何もしなくても、勝手に距離を詰められ、耳元で囁かれる」**
この体験が実機で確認できればプロジェクトは完了です。
