# 開発ルールとフロー (ARCHITECTURE)

## 1. 基本思想：分離と統合
- **見た目 (Look)**: `VisualBuilder.cs` が担当。物理オブジェクトとテクスチャを生成。
- **論理 (Logic)**: `UdonSharp` 各スクリプトが担当。ゲームルールを実行。
- **統合 (Wiring)**: `VRMineBridge.cs` が担当。見た目と論理を自動で紐付ける。

## 2. 開発ワークフロー
Unity上での作業は以下のメニュー実行だけで完結します。

1. **`VRMine > build_visuals`**: 
   - 盤面、カード、ポスター等の物理環境をゼロから再構築する。
2. **`VRMine > wire_scene`**: 
   - 生成された物理オブジェクトを、Udonロジックの参照変数に自動配線する。

## 3. 実装の掟
- **Zero-Fat**: デバッグ用のゴミ（浮遊する球体等）をシーンに残さない。
- **Physical-First**: UIは空中に浮かさず、テーブル上の付箋や壁のポスターとして実体化させる。
- **Bit-Packing**: ネットワーク通信量を抑えるため、カード情報は 1バイト以内に収める。

## 4. Quality Gateの分離

**PR merge条件と製品release条件を混ぜない。**

- PR Merge Gate: `main` へ統合してよいかを判定する。static contract、Repository U1、changed-surface test、mergeability、blocking reviewを対象にする。
- Release Candidate Gate: exact `main` commitをUnity/SDKで検証する。Unity compile、canonical scene integrity、SDK Builder validationを対象にする。
- Product Release Gate: actual VRChat clientで製品として成立するかを検証する。1人通し、reset/replay、2-client同期、late join、owner transition、実測性能を対象にする。

Unity/VRChat実行環境が利用できないことだけを理由に、Merge GateをPASSしたPRを保留しない。一方、Merge Gate PASSをProduct Release PASSへ昇格しない。

Canonical policyは `config/quality-gates.json`、詳細は `docs/QUALITY_GATES.md` とする。
