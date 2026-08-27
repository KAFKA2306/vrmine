# VRMine — VRChat・3D spatial project

[![Build and deploy VRMine Pages](https://github.com/KAFKA2306/vrmine/actions/workflows/pages.yml/badge.svg)](https://github.com/KAFKA2306/vrmine/actions/workflows/pages.yml)
[![Unity VPM verification](https://github.com/KAFKA2306/vrmine/actions/workflows/unity-vpm.yml/badge.svg)](https://github.com/KAFKA2306/vrmine/actions/workflows/unity-vpm.yml)

## Vision

ブラウザ版とVRChat版を同じrepositoryで育てながら、**どの実行環境で何が検証済みかを混同せず、公開可能な状態まで進められること**を目標にします。ブラウザで動いたことをUnity/UdonSharp/VRChat上の動作証拠として扱いません。

## 公開Pages

公開中のPages URLは、コピーしやすいようにURLをそのまま記載します。

https://kafka2306.github.io/vrmine/
https://kafka2306.github.io/vrmine/games/perspective-cage/
https://kafka2306.github.io/vrmine/games/stich-meister/
https://kafka2306.github.io/vrmine/games/answer-impostor/
https://kafka2306.github.io/vrmine/games/abyss-invasion/
https://kafka2306.github.io/vrmine/3dgs/
https://kafka2306.github.io/vrmine/organizers/
https://kafka2306.github.io/vrmine/events/demo/

Pagesへの公開は、Unity Editor・ClientSim・実VRChat clientでの動作確認を意味しません。

- Answer Impostor — 4〜8人、1端末の擬態クイズ
- 深淵侵蝕 — 4人用の領域支配・進行補助
- Stich-Meister — VRChat版の設計・実装状況を公開中。実VRChatでの通し対局は未検証（Issue #68）
- 視点の檻 / CAGE OF PERSPECTIVE — 1〜4人向けVRChat空間謎解き。実機releaseはIssue #145で検証する
- 3DGS — Gaussian Splatのブラウザ展示
- Organizers — イベント主催者向け案内
- Event Demo — イベント用Hubのデモ

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
コンテンツを選ぶ
  → ゲーム固有の操作または3D展示を開く
  → 状態遷移 / scene生成
  → 実行環境ごとの検証
  → PagesまたはVRChatへ公開
```

- **ブラウザ:** `pages/index.html` → `pages/games/registry.js` → `pages/games/<game-id>/`
- **ブラウザ状態:** `vrmine.games.<game-id>.state` でゲームごとに分離
- **VRChat / spatial:** Unityプロジェクトの `Assets/`
- **Unity version:** `ProjectSettings/ProjectVersion.txt`
- **VRChat toolchain:** `config/vrchat-toolchain.json`

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

ネットワーク手順は `docs/abyss-invasion-network.md` を参照してください。

### VRChat版 Stich-Meister

VRChat SDK3 / UdonSharpを使用し、盤面生成、オブジェクト配線、ゲーム状態同期を分離しています。

- `VisualBuilder.cs` — 盤面、カード、宝石、ポスターなどを生成
- `VRMineBridge.cs` — 生成物をUdonSharpコンポーネントへ配線
- `BoardState` / `GameController` — ゲーム進行とネットワーク同期

関連資料:

- `docs/Rulebook.md`
- `docs/STATE.md`
- `docs/ARCHITECTURE_RULES.md`

## Perspective Cage

短編VRChat謎解きWorld **「視点の檻 / CAGE OF PERSPECTIVE」** は、5つの空間パズルを `config/perspective-cage.json` で定義し、同じUnity project内の `Assets/KafkaMade/VRMine/Puzzles/PerspectiveCage/` にruntimeとdeterministic builderを持ちます。

Repository側では、scene shell生成、UdonSharpによる公開状態同期、3段階hint、reset、late-join時のpresentation再構築まで実装済みです。これはUnity Editorやactual VRChat clientでの成功を意味しません。製品release判定はIssue #145がauthorityです。

ローカルのrepository-level / Unity-level入口:

```bash
task check
task release:perspective-cage:u2
```

`task check` はrepository contractを検証します。`task release:perspective-cage:u2` はexact Unity `2022.3.22f1` が利用できる環境でRelease Candidate Gateを実行する入口です。actual VRChat clientの1-player通し、reset/replay、2-client sync、late joinは別途U4 evidenceが必要です。

## Gaussian Splat展示をローカルで開く

目標は **clone → PLY自動取得 → VCC/Unityでprojectを開く** だけです。PLYの件数はコードに固定せず、`config/gaussian-splats.json` に登録された現在の件数 `N` をそのまま処理します。

### 現在の最短手順

```bash
git clone https://github.com/KAFKA2306/vrmine.git
cd vrmine
task gaussian:open
```

`task gaussian:open` を使うと、`hf-cache-hub` の共有 Storage Bucket から登録済みPLYを取得・検証します。その後、VCC CLIが利用可能な環境ではprojectを登録・packageをresolveし、Unity `2022.3.22f1` を起動します。VCC CLIが見つからない場合も、Unityは直接起動されます（CLIを使う場合は `VPM_EXE` を指定）。Unity Editorは一回限りの準備要求を読み、`Assets/KafkaMade/VRMine/Scenes/GaussianSplatExhibition.unity` をregistry件数に合わせて生成して開きます。PLYだけ取得したい場合は `task gaussian:prepare` を使います。

PLY取得には `hf-cache-hub` の checkout、`HF_CACHE_HUB_ROOT`、および private Storage Bucket を読む Hugging Face 認証が必要です。resolver の Python 環境には `huggingface-hub>=1.0`、`filelock`、`PyYAML` を入れ、必要なら `HF_CACHE_HUB_PYTHON` で指定します。

### `task gaussian:prepare` が自動で行うこと

- pinned `VRChatGaussianSplatting` rendererを取得
- `config/gaussian-splats.json` に登録されたPLYを `config/gaussian-artifacts.yaml` の hf-cache-hub artifact IDから取得
- PLYのbyte-sizeとSHA-256を検証
- 既に検証済みのrenderer/PLYは再利用
- Unity Editorでscene生成を1回実行するための準備要求を作成
- `task gaussian:open` ではVCC CLIへのproject登録・package resolveと、固定Unityバージョンの起動まで実行

Unity Editor側では、登録済みの `N` 件を入力として次を自動生成します。

```text
N PLY
→ N LOD prefabs
→ 約1 mへnormalize
→ N件を2列へ自動配置
→ 件数に合わせた床とworld shell
→ collider / spawn / Reference Camera
→ exhibit labels
→ light probes / lighting設定
→ GaussianSplatRenderer 1個
→ GaussianSplatExhibition.unity を保存して開く
```

通常経路では、**手動PLY download、手動hash確認、`Gaussian Splatting / Import Splats...`、prefab手配置、床・spawn・material・lightingの手修正は不要**にします。UdonSharpはVRChat内で必要なruntime挙動（最終動画playlist、同期、操作UI）だけに使い、PLY取得・hash・import・scene authoringには使いません。

現在のdownstream registryは20件です。ただし、20件が登録されていることとprivate artifact bytesが20/20検証済みであることは別です。現時点の残件はIssue #138のprivate bucket exact-hash readback、Issue #132のphysical-up evidence、Issue #139のUnity / SDK / actual VRChat client検証です。これらが揃うまで20展示worldをruntime完成扱いしません。

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

`task setup` はpinned `vrc-get` を準備します。`task check` はAnswer ImpostorのNode.js test、Perspective Cage contract、Gaussian Splatのsource/exhibition/video contract、VPM package graphの解決とmanifest drift検証を実行します。VPMだけを再実行する場合は `task vpm:check` を使います。

個別に実行する場合:

```bash
node --test pages/games/answer-impostor/engine.test.mjs
node scripts/verify-perspective-cage.mjs
node scripts/verify-gaussian-fixtures.mjs
node scripts/verify-gaussian-exhibition.mjs
node scripts/verify-gaussian-video-playlist.mjs
node scripts/verify-vpm.mjs
python3 -m http.server 8000 --directory pages
```

Pages全体の検証は `task pages:test`、深淵侵蝕のserverは `task abyss:server` です。

## CI/CD

変更はPull RequestでCIを通してからmergeします。`main` へ反映されたPages関連変更は `.github/workflows/pages.yml` から自動公開し、workflow内で公開URLを確認します。

CI成功だけでUnity、UdonSharp、実VRChat client上の動作を証明したことにはしません。コード・tests・実行結果を文書より優先し、未実行の環境は未検証として扱います。
