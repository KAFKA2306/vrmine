# VRMine Game Hub / VRChat Board Game Lab

複数のボードゲーム・会話ゲームを、同じGitHub Pages基盤とVRChat開発リポジトリで管理するプロジェクトです。

## 公開ページ

- Game Hub: https://kafka2306.github.io/vrmine/
- Answer Impostor: https://kafka2306.github.io/vrmine/games/answer-impostor/
- 深淵侵蝕: https://kafka2306.github.io/vrmine/games/abyss-invasion/
- Stich-Meister: https://kafka2306.github.io/vrmine/games/stich-meister/

## Pagesでプレイ可能なゲーム

### Answer Impostor 〜フレンド擬態クイズ〜

4〜8人・1端末のパス＆プレイです。

- 擬態者と擬態対象のランダム選出
- 個別の秘密役割確認
- 3候補からの質問投票
- 個別回答入力と匿名シャッフル
- 議論タイマー
- 個別の予測投票
- 通常プレイヤー、擬態者、対象者ボーナスの自動得点
- 3〜8ラウンドの累計ランキング
- LocalStorage自動保存、JSONエクスポート／インポート

### 深淵侵蝕

4人・最大7ラウンドの物理盤面／VRChat盤面用進行補助です。ローカル進行に加え、WebSocketサーバーのIPルームへ4端末で接続できます。

- 支配区域と最大連続領域群の記録
- 侵蝕／潜伏／抗争／儀式の行動ログ
- 抗争用1D6対決
- 最終順位の自動計算
- IPルームのホスト開始と手番同期

IPルームの起動方法は[深淵侵蝕ネットワーク手順](docs/abyss-invasion-network.md)を参照してください。

## VRChatゲーム: Stich-Meister

「ルールを奪い合う、手仕事感あふれるトリックテイキング・ボードゲーム」。VRChat SDK3 / UdonSharp 1.1.xを使用します。

1. `VisualBuilder.cs`: 盤面、カード、宝石、ポスター等の物理環境を生成
2. `VRMineBridge.cs`: 生成オブジェクトをUdonSharpコンポーネントへ自動配線
3. `BoardState / GameController`: ビットパック状態でゲーム進行とネットワーク同期を管理

Unityメニュー:

- `VRMine > build_visuals`
- `VRMine > wire_scene`

資料:

- [Rulebook](docs/Rulebook.md)
- [STATE](docs/STATE.md)
- [ARCHITECTURE_RULES](docs/ARCHITECTURE_RULES.md)

## Pagesゲーム基盤

```text
pages/
├── index.html                  # ゲームポータル
├── games/registry.js           # ゲーム登録
├── assets/
│   ├── styles.css              # 共通デザインシステム
│   └── platform.js             # 保存・入出力・通知
├── games/<game-id>/            # 独立ゲームモジュール
└── service-worker.js           # PWAキャッシュ
```

新しいゲームは `pages/games/<game-id>/` に追加し、`pages/games/registry.js` にメタデータを登録します。ゲーム状態は `vrmine.games.<game-id>.state` の名前空間で分離します。

## ローカル検証

```bash
node --test pages/games/answer-impostor/engine.test.mjs
python3 -m http.server 8000 --directory pages
```

GitHub Actionsはロジック試験、静的リンク整合性、JavaScript構文を確認し、Pages配信後に3ゲームの実URLを再取得して検証します。
