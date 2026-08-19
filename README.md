# VRMine — VRChat・ブラウザ向けゲーム開発基盤

[![Build and deploy VRMine Pages](https://github.com/KAFKA2306/vrmine/actions/workflows/pages.yml/badge.svg)](https://github.com/KAFKA2306/vrmine/actions/workflows/pages.yml)
[![Unity VPM verification](https://github.com/KAFKA2306/vrmine/actions/workflows/unity-vpm.yml/badge.svg)](https://github.com/KAFKA2306/vrmine/actions/workflows/unity-vpm.yml)

## Vision

ブラウザ版とVRChat版を同じrepositoryで育てながら、**どの実行環境で何が検証済みかを混同せず、公開可能な状態まで進められること**を目標にします。ブラウザで動いたことをUnity/UdonSharp/VRChat上の動作証拠として扱いません。

**公開ゲームハブ:** https://kafka2306.github.io/vrmine/

## Design philosophy

- 実行環境ごとに検証結果を分離し、未実行の環境をPASS扱いしない
- ゲーム固有の状態と共通の公開・検証責務を分ける
- 同じ責務の実装・設定・実行経路を並立させない
- 複数の実利用経路で必要と確認できるまで新しい抽象化を増やさない
- 操作を自動化しても、必要なUnity/VRChat実行証拠は削らない

## Why

VRMineの差分はUnity、UdonSharp、PWA、WebSocketそのものではなく、**ブラウザで確認できることとUnity/VRChatで確認すべきことを別の証拠として扱うこと**です。公開ページ、Unity Editor、ClientSim、実VRChat clientで証明できる範囲を分け、低い証拠レベルの成功を実機成功へ読み替えません。

## Player / developer journey

```text
ゲームを選ぶ
  → ゲーム固有の入力・操作
  → 状態遷移
  → 実行環境ごとの検証
  → PagesまたはVRChatへ公開
```

- **ブラウザ:** `pages/index.html` → `pages/games/registry.js` → `pages/games/<game-id>/`
- **ブラウザ状態:** `vrmine.games.<game-id>.state` でゲームごとに分離
- **VRChat:** Unityプロジェクトの `Assets/`
- **Unity version:** `ProjectSettings/ProjectVersion.txt`
- **VRChat toolchain:** `config/vrchat-toolchain.json`

## 公開Pages

以下はGitHub Pagesで公開しているブラウザ画面です。Pagesへの公開は、Unity Editor・ClientSim・実VRChat clientでの動作確認を意味しません。

- [Answer Impostor](https://kafka2306.github.io/vrmine/games/answer-impostor/) — 4〜8人、1端末の擬態クイズ
- [深淵侵蝕](https://kafka2306.github.io/vrmine/games/abyss-invasion/) — 4人用の領域支配・進行補助
- [Stich-Meister](https://kafka2306.github.io/vrmine/games/stich-meister/) — VRChat版の設計・実装状況を公開中。実VRChatでの通し対局は未検証（[#68](https://github.com/KAFKA2306/vrmine/issues/68)）

### Answer Impostor

- 擬態者と擬態対象のランダム選出
- 個別の秘密役割確認
- 質問候補への投票
- 回答の匿名シャッフル
- 議論タイマー
- 個別予測投票
- 自動得点と累計ランキング
- `localStorage`への保存
- JSONのエクスポート・インポート

### 深淵侵蝕

- 支配区域と連続領域の記録
- 侵蝕・潜伏・抗争・儀式の行動ログ
- 1D6による抗争処理
- 最終順位の自動計算
- WebSocketを使った4端末同期

ネットワーク手順は[docs/abyss-invasion-network.md](docs/abyss-invasion-network.md)を参照してください。

### VRChat版 Stich-Meister

VRChat SDK3 / UdonSharpを使用し、盤面生成、オブジェクト配線、ゲーム状態同期を分離しています。

- `VisualBuilder.cs` — 盤面、カード、宝石、ポスターなどを生成
- `VRMineBridge.cs` — 生成物をUdonSharpコンポーネントへ配線
- `BoardState` / `GameController` — ゲーム進行とネットワーク同期

関連資料:

- [Rulebook](docs/Rulebook.md)
- [STATE](docs/STATE.md)
- [ARCHITECTURE_RULES](docs/ARCHITECTURE_RULES.md)

## Unity / VRChat の検証

現在のtargetはUnity `2022.3.22f1` / VRChat SDK `3.9.0`です。versionは `ProjectSettings/ProjectVersion.txt` と `config/vrchat-toolchain.json` で管理します。

GitHub Actionsの `unity-vpm.yml` はUnity Editorを起動せず、次を検証します。

- pinned `vrc-get` のchecksum
- VPM package graphの解決
- manifestが検証中に変化しないこと
- 利用可能なpackage update

Unity compile、PlayMode、実際のVRChat clientでの確認は、このVPM検証とは別に扱います。

## Pages

```text
pages/
├── index.html
├── games/registry.js
├── assets/styles.css
├── assets/platform.js
├── games/<game-id>/
└── service-worker.js
```

新しいゲームは `pages/games/<game-id>/` へ追加し、`pages/games/registry.js` へ登録します。

## ローカル検証

通常は次を実行します。

```bash
task setup
task check
```

`task setup` はpinned `vrc-get` を準備します。`task check` はAnswer ImpostorのNode.js test、Gaussian Splat fixtureの検証、VPM package graphの解決とmanifest drift検証を実行します。VPMだけを再実行する場合は `task vpm:check` を使います。

個別に実行する場合:

```bash
node --test pages/games/answer-impostor/engine.test.mjs
node scripts/verify-gaussian-fixtures.mjs
node scripts/verify-vpm.mjs
python3 -m http.server 8000 --directory pages
```

Pages全体の検証は `task pages:test`、深淵侵蝕のserverは `task abyss:server` です。

## 検証方針

ブラウザのtestだけでUnity、UdonSharp、VRChat client上の動作を証明したことにはしません。コード・tests・実行結果を文書より優先し、未実行の環境は未検証として扱います。
