# VRMine — VRChat・ブラウザ向けゲーム開発基盤

**ブラウザでゲームが動いた。それでも、VRChatで動く証拠にはならない。**

同じゲームでも、状態遷移、同期、保存、入力、実行環境が変われば、壊れ方も変わります。VRMineはその差を潰さず、ブラウザ版とVRChat版を別々に検証しながら、複数のボードゲーム・会話ゲームを共通基盤で育てるプロジェクトです。

**公開ゲームハブ:** https://kafka2306.github.io/vrmine/

ゲームごとの状態、得点、同期、保存、検証を共通基盤へまとめながら、ブラウザで動いたこととVRChatで動いたことを別の証拠として扱います。

## 正準ユーザーフロー

VRMineで維持する主要フローは次の1本です。

```text
ゲームを選ぶ
  → ゲーム固有の入力・操作
  → 共通/固有ロジックで状態遷移
  → 実行環境ごとの検証
  → 公開可能な成果だけをPagesまたはVRChatへ出す
```

- **ブラウザ入口:** `pages/index.html` → `pages/games/registry.js` → `pages/games/<game-id>/`
- **ブラウザ状態:** ゲーム固有stateを `vrmine.games.<game-id>.state` に分離し、同じ状態を別形式で二重正準化しない
- **VRChat入口:** Unityプロジェクトの `Assets/`。Unity versionは `ProjectSettings/ProjectVersion.txt` を正準とする
- **公開判定:** ブラウザのNode/静的検証と、Unity/UdonSharp/VRChat実行証拠を別gateとして扱う

### 非目標

- 実験機能や調査workflowをこのrepoの恒常的な主要フローにしない
- ブラウザtestの成功をVRChat実機成功へ読み替えない
- 使われていないadapter・別正準store・生成途中artifactを履歴保存のためだけに残さない
- 新しい抽象化は、少なくとも2つの実利用経路で必要になるまで追加しない

### Ratchet KPI

主要KPIは3つに限定します。

1. **主要フロー検証成功率** — CI/実機gateの成功・失敗をそのまま記録する
2. **手動操作数** — build / wire / verify / publishで人手が必要な操作を減らす
3. **再現可能成果数** — 同じ入力・commit・実行環境から再検証できるゲーム成果のみ数える

## 公開中のゲーム

- [Answer Impostor](https://kafka2306.github.io/vrmine/games/answer-impostor/) — 4〜8人、1端末の擬態クイズ
- [深淵侵蝕](https://kafka2306.github.io/vrmine/games/abyss-invasion/) — 4人用の領域支配・進行補助
- [Stich-Meister](https://kafka2306.github.io/vrmine/games/stich-meister/) — ルールを奪い合うトリックテイキング

## Answer Impostor

主な機能:

- 擬態者と擬態対象のランダム選出
- 個別の秘密役割確認
- 質問候補への投票
- 回答の匿名シャッフル
- 議論タイマー
- 個別予測投票
- 自動得点と累計ランキング
- `localStorage`への保存
- JSONのエクスポート・インポート

## 深淵侵蝕

物理盤面またはVRChat盤面の進行を補助します。

- 支配区域と連続領域の記録
- 侵蝕・潜伏・抗争・儀式の行動ログ
- 1D6による抗争処理
- 最終順位の自動計算
- WebSocketを使った4端末同期

ネットワーク手順は[docs/abyss-invasion-network.md](docs/abyss-invasion-network.md)を参照してください。

## VRChat版 Stich-Meister

VRChat SDK3 / UdonSharpを使用し、盤面生成、オブジェクト配線、ゲーム状態同期を分離しています。

- `VisualBuilder.cs` — 盤面、カード、宝石、ポスターなどを生成
- `VRMineBridge.cs` — 生成物をUdonSharpコンポーネントへ配線
- `BoardState` / `GameController` — ゲーム進行とネットワーク同期

旧Unity MenuItem verificationはローカルfallbackとしてのみ残し、新しい自動化をそこへ追加しません。正準化中のverification architectureは [#54](https://github.com/KAFKA2306/vrmine/issues/54) を参照してください。

関連資料:

- [Rulebook](docs/Rulebook.md)
- [STATE](docs/STATE.md)
- [ARCHITECTURE_RULES](docs/ARCHITECTURE_RULES.md)

## Unity / VRChat verification

検証を証拠レベルに分離します。

```text
U1  VPM/package resolve
 ↓
U2  exact Unity compile + EditMode
 ↓
U3  PlayMode + ClientSim-supported semantics
 ↓ 必要な変更だけ
U4  Windows + actual VRChat multi-client
 ↓ release時のみ
U5  private-world smoke
```

実装workstream:

- [#48](https://github.com/KAFKA2306/vrmine/issues/48) — `vrc-get` headless VPM resolve
- [#49](https://github.com/KAFKA2306/vrmine/issues/49) — Ubuntu + official Unity CLI PoC
- [#50](https://github.com/KAFKA2306/vrmine/issues/50) — EditMode/NUnit化
- [#51](https://github.com/KAFKA2306/vrmine/issues/51) — PlayMode + ClientSim
- [#52](https://github.com/KAFKA2306/vrmine/issues/52) — PR merge gate / artifacts
- [#53](https://github.com/KAFKA2306/vrmine/issues/53) — real VRChat 2-client machine gate

ClientSimの成功をreal VRChat networkingの成功として扱いません。生成された `Latest*.txt` やdated screenshotはGitへ保存せず、CI/target-machine artifactとして保持します。

## Pagesゲーム基盤

```text
pages/
├── index.html                  # ゲームポータル
├── games/registry.js           # ゲーム登録
├── assets/styles.css           # 共通デザイン
├── assets/platform.js          # 保存・入出力・通知
├── games/<game-id>/            # 各ゲーム
└── service-worker.js           # PWAキャッシュ
```

新しいゲームは`pages/games/<game-id>/`へ追加し、`pages/games/registry.js`へ登録します。保存領域は`vrmine.games.<game-id>.state`でゲームごとに分離します。

## ローカル検証

```bash
node --test pages/games/answer-impostor/engine.test.mjs
node scripts/verify-repository-ratchet.mjs
python3 -m http.server 8000 --directory pages
```

GitHub Actionsでは、ゲームロジック、正準構造、静的リンク、JavaScript構文、Pages公開後の実URLを検証します。Unity/VRChat側はU1–U4への移行中で、移行完了後はユーザーのUnity手動起動を通常経路にしません。

## 検証の原則

```text
入力・操作
  → 状態遷移
  → 得点・合法手・同期結果
  → 実行環境ごとの証拠
  → release pass / fail
```

ブラウザ単体試験だけで、Unity、UdonSharp、VRChatクライアント上の動作を証明したことにはしません。機械可読な定義は[`ontology/project.yaml`](ontology/project.yaml)にあります。

**README最終監査:** 2026-08-14
