# vrmine

## 0. 目的・範囲

* **目的**：自動生成された同一パズル（盤面）に対し、参加者（=Explorer）が**波を発射**→**出口座標と最終色**の公開ログを基に推理し、**完全一致宣言**で勝利する体験を提供する。
* **人数**：ワールドの **Instance 定員**に依存（ゲームロジックは人数制限を設けない）。**手番はキュー制**で排他。
* **非範囲**：外部サーバ／DB、ランキング、商用・版権判断。

---

## 1. 参照（固定要件）

* **Unity**：VRChat 現行の **2022.3.22f1** を使用（Creator Companion が自動一致）。 ([creators.vrchat.com][1])
* **SDK**：VRChat **SDK3–Worlds**（Udon / UdonSharp）。
* **Networking 基本**：所有権・変数同期・イベント・送信制限の設計は公式仕様に従う。([creators.vrchat.com][2])
* **イベント**：**最大 8 パラメータ**のカスタムネットワークイベント。([creators.vrchat.com][3])
* **ローカル検証**：ClientSim（公式）／CyanEmu（コミュニティ）でテスト。([creators.vrchat.com][4])

---

## 2. 実行環境・配布

* **プロジェクト作成**：VRChat Creator Companion（VPM 管理、Unity バージョン自動選択）。 ([vcc.docs.vrchat.com][5])
* **端末**：PC 必須。Quest（Android）は M3 で軽量化対応（LOD/テクスチャ圧縮）。

---

## 3. ゲーム仕様（最小コアルール）

### 3.1 盤・入口・ラベル

* **盤**：10×8 グリッド（セル基準の格子）。
* **入口**：外周 36 点（上 10、右 8、下 10、左 8）。内部 ID は **0..35**（時計回り）。
* **表示ラベル**：上辺 A–J、右辺 1–8、下辺 K–T、左辺 9–16（表示は任意・内部 ID と分離）。

### 3.2 色・ピース・配置

* **色**：赤 / 青 / 黄 / 透明（白） / 黒（石油）。
* **形**：セル占有前提。**Dot1（1セル）／Line2（2 直線）／L3（3 L字）**。回転可。
* **上限**：各色 ≤5 ピース、**総占有 ≤20 セル**。
* **配置制約**：重なり禁止、接触可、外周接触可。

### 3.3 波の物理・色合成

* **進行**：格子線上を直進。色セルに入射すると**直角反射（90°）**。
* **停止**：盤外到達で**出口座標**確定。**黒**は**吸収（即終了、出口なし）**。
* **色合成（固定）**：

  * 初期色＝白。白＋{赤/青/黄}→その色
  * 2 色 → **紫/橙/緑**（順不同）
  * **3 原色すべて** → **灰色**
  * 透明（白）：色不変
  * 黒（石油）：吸収（色は報告しない）

### 3.4 公開情報・勝利条件

* 公開：毎手番の **入口→出口, 最終色**（吸収時は「吸収」）。
* 勝利：**全セルの色配置**を宣言し**完全一致**で勝ち（最初に達成した者）。
* タイマー：**90 秒/手番**（無操作は自動スキップ）。

---

## 4. 自動化（Director なし）

### 4.1 盤面生成（決定論）

* **Seed**：`boardSeed:uint` を初回所有者が確定（Instance 固有情報のハッシュ）→**同期**。
* **決定論生成**：Seed から PRNG でピース群を配置（制約を満たすまで再試行）。
* **監査**：内部 `boardHash:uint`（CRC32）を保持（チート検知ではなく同一性確認用）。

### 4.2 手番制御

* **TurnQueue**：ワールド参加者から FIFO で手番を割当。
* タイムアウト（90 秒）で自動スキップ。UI で残り時間を表示。

---

## 5. ネットワーク設計

### 5.1 同期モード・所有権

* **Sync**：`BehaviourSyncMode = Manual`（重要状態のみ手動同期）。([creators.vrchat.com][6])
* **所有権**：`GameController`（Board/Wave/Queue/Judge を内包）を**単一所有**に集約。初参加者が所有し、離脱時は**自動再割当**→即時再シリアライズで復元。([creators.vrchat.com][2])

### 5.2 同期変数（UdonSynced）スキーマ

> すべて `GameController` に集約。**文字列は使用しない**（サイズ安定化のため）。

* `byte gridW=10, gridH=8`
* `uint boardSeed` / `uint boardHash` / `byte boardVersion`
* `int turnIndex`（0..）
* **ログ（リングバッファ）**：`byte logCount`（≤20）、`byte[80] logRing`（1 件 = 4B: `entryId(0..35)`, `exitId(0..35|255)`, `colorId(0..7|255)`, `flags(8bit)` → 20 件=80B）
* `bool allowProbe`（座標問い合わせ機能の ON/OFF）

> Manual Sync の送信は**直列化**し、上限（**280,496 bytes/回**）に対して桁違いの小サイズで運用。([creators.vrchat.com][7])

### 5.3 ネットワークイベント

* `WaveRequest(byte entryId)` → **Owner**
* `WaveResult(byte entryId, byte exitId, byte colorId, byte flags)` → **All**
* `Declare(byte[] payload≤1024)` → **Owner**（判定）
* `JudgeResult(bool won, byte errors)` → **All**

> 1 イベントあたり **最大 8 パラメータ**仕様内。([creators.vrchat.com][3])

---

## 6. データモデル

### 6.1 列挙・ID

* `colorId`: 0=White(Transparent), 1=Red, 2=Blue, 3=Yellow, 4=Purple, 5=Orange, 6=Green, 7=Gray, 255=None
* `flags`: bit0=Absorbed, bit1=AskedProbe, 以降予約
* `entryId/exitId`: 0..35（時計回り; 吸収時 `exitId=255`）

### 6.2 シリアライズ設計

* 重要状態は**変数同期**、ログは**固定長バイナリ**（イベントは内容通知のみ）。
* `RequestSerialization()` を Owner 側で明示呼び出し。([creators.vrchat.com][6])

---

## 7. 3D 配置（Unity ワールド座標）

### 7.1 原点・スケール

* **座標系**：Unity 既定（Y↑, Z 前, X 右）。**1 unit = 1 m**。
* **原点**：ボード中心 `(0, **0.85**, 0)`（テーブル天面を 0.85 m）。

### 7.2 盤・セル

* **セル**：`cell = 0.12 m`
* **盤寸**：`W = 1.20 m (10*cell)`, `D = 0.96 m (8*cell)`
* **セル中心→世界座標**

  ```
  worldX = (x - 4.5) * cell
  worldY = 0.85
  worldZ = (y - 3.5) * cell
  ```

### 7.3 入口 36 点（ID 0..35）

* **オフセット**：盤外 **0.05 m**

  * 上辺（j=0..9, id=j）: `((j-4.5)*cell, 0.85, -D/2 - 0.05)`
  * 右辺（i=0..7, id=10+i）: `(+W/2 + 0.05, 0.85, (i-3.5)*cell)`
  * 下辺（j=0..9, id=18+j）: `((j-4.5)*cell, 0.85, +D/2 + 0.05)`
  * 左辺（i=0..7, id=28+i）: `(-W/2 - 0.05, 0.85, (i-3.5)*cell)`
* **入口コライダ**：BoxCollider `size=(0.08, 0.04, 0.08)`, `isTrigger=true`。

### 7.4 共有 UI（World Space Canvas）

* `Canvas_Main`: **位置** `(0, 1.35, 1.20)`, **回転** `(0,180,0)`, **スケール** `(0.001,0.001,0.001)`

  * 左：入口セレクタ＋**発射**
  * 中：**ログ 20 件**（`A5→K12, Purple` / `A5→Absorb`）
  * 右：**解答宣言**（座標・色を入力→一括検証）

### 7.5 補助オブジェクト

| 名称             |     Position (m) | Rotation (deg) | 備考                 |
| -------------- | ---------------: | -------------: | ------------------ |
| BoardRoot      |     (0, 0.85, 0) |        (0,0,0) | グリッド可視化            |
| GameController |     (0, 0.90, 0) |        (0,0,0) | UdonBehaviour（不可視） |
| TurnIndicator  | (0, 1.02, −0.70) |        (0,0,0) | 手番表示               |
| WaveVisualizer |       +0.01 (板上) |              – | LineRenderer（任意）   |

---

## 8. UI/UX 指針（最小）

* **誤操作防止**：選択→確定の二段階。
* **アクセシビリティ**：全色に**記号/ハッチ**併記。
* **表示**：ログはリング 20 件、フィルタ（入口/色）付き。

---

## 9. エラーハンドリング

* **所有者離脱**：`OnOwnershipTransferred` で新 Owner へ即時移行→`RequestSerialization()`。([creators.vrchat.com][2])
* **宣言不一致**：誤箇所数を `errors` に格納して返却（UI に反映）。
* **通信混雑**：イベントは 1 手番 ≤3、再送は次手番頭で 1 回まで。

---

## 10. セキュリティ・運用

* **チート耐性**：クライアント権限のため**強耐性は不可**。盤配置は非公開同期、`boardHash` の一致で同一性を確認。
* **更新**：Unity/SDK の更新に追随（Creator Companion 経由）。([creators.vrchat.com][8])

---

## 11. テスト計画・受入基準

### 11.1 ユニット（UdonSharp）

* **色合成**：全遷移（単色/二色/三色/透明/黒）。
* **反射**：四方向＋角入射。
* **出口**：36 入口×代表ケース。

### 11.2 結合

* **ClientSim** 2–4 クライアント：遅参復元（ログ 20 件・手番）／所有者交代の継続性。([creators.vrchat.com][4])
* **CyanEmu**：UI 操作・多人数入力の擬似検証。([GitHub][9])

### 11.3 受入

* 20 ターン×N 人で**クラッシュ/同期崩壊なし**。
* 所有者離脱時も**状態欠落なし**（ログ・Seed が一致）。
* 1 手番あたりイベント ≤3、Manual Sync の `byteCount` が **1KB 未満**を継続。([creators.vrchat.com][7])

---

## 12. マイルストーン（短期）

1. **M1**：`GameController`（Manual Sync）／`WaveSimulator`／`TurnQueue`／ログ UI
2. **M2**：宣言→自動検証、座標問い合わせ、所有者交代ハンドラ
3. **M3**：Quest 軽量化（LOD/材質）、可読性微調整

---

## 13. 付録 A：設定ファイル（実装用 YAML）

```yaml
RuleConfig:
  grid: { width: 10, height: 8, cell_m: 0.12 }
  entries:
    offset_m: 0.05
    id_order: clockwise_0_35
  colors:
    ids: [White, Red, Blue, Yellow, Purple, Orange, Green, Gray]
    mix:
      two: { R+B: Purple, R+Y: Orange, B+Y: Green }
      three_primary: Gray
    oil_behavior: Absorb
    transparent_behavior: NoChange
  pieces:
    shapes:
      - { name: Dot1, cells: [[0,0]] }
      - { name: Line2, cells: [[0,0],[1,0]] }
      - { name: L3, cells: [[0,0],[1,0],[0,1]] }
    per_color_limit: 5
    max_occupied_cells: 20

AutoRun:
  seed_source: InstanceIdHash
  owner: SingleAuthority
  logs:
    ring_size: 20
    item_bytes: 4
  timers:
    turn_seconds: 90
  features:
    allow_probe: false

Spatial:
  board_origin: [0, 0.85, 0]
  canvas_main: { pos: [0, 1.35, 1.20], rot_deg: [0,180,0], scale: [0.001,0.001,0.001] }
```

---

## 14. 付録 B：主要クラス（UdonSharp スケルトン名のみ）

* `GameController`（Manual / Sync 集約 / 所有権管理）
* `WaveSimulator`（入口→出口・色／吸収の決定論計算）
* `TurnQueue`（手番・タイムアウト）
* `Judge`（宣言検証）
* `UIController`（入口選択／ログ／宣言）

---

## 参考（根拠）

* **Networking（所有権・変数・イベント）**：VRChat Creator Docs（Networking, Variables, Events, Network Specs）。([creators.vrchat.com][2])
* **Unity/SDK/Upgrade**：Current Unity Version / Creator Companion / Unity 2022 への移行。([creators.vrchat.com][1])
* **テスト**：ClientSim（公式）／CyanEmu（コミュニティ）。([creators.vrchat.com][4])
* **UdonSharp**：公式ドキュメント（コンパイラ／制約／API）。([udonsharp.docs.vrchat.com][10])

---

[1]: https://creators.vrchat.com/sdk/upgrade/current-unity-version/?utm_source=chatgpt.com "Current Unity Version | VRChat Creation"
[2]: https://creators.vrchat.com/worlds/udon/networking/?utm_source=chatgpt.com "Networking | VRChat Creation"
[3]: https://creators.vrchat.com/worlds/udon/networking/events/?utm_source=chatgpt.com "Network Events - Udon"
[4]: https://creators.vrchat.com/worlds/clientsim/?utm_source=chatgpt.com "ClientSim | VRChat Creation"
[5]: https://vcc.docs.vrchat.com/?utm_source=chatgpt.com "VRChat Creator Companion"
[6]: https://creators.vrchat.com/worlds/udon/networking/variables/?utm_source=chatgpt.com "Network Variables | VRChat Creation"
[7]: https://creators.vrchat.com/worlds/udon/networking/network-details/?utm_source=chatgpt.com "Networking Specs & Tricks - Udon"
[8]: https://creators.vrchat.com/sdk/upgrade/unity-2022/?utm_source=chatgpt.com "Upgrading Projects to 2022 | VRChat Creation"
[9]: https://github.com/CyanLaser/CyanEmu?utm_source=chatgpt.com "CyanEmu is a VRChat client emulator in Unity. Includes ..."
[10]: https://udonsharp.docs.vrchat.com/?utm_source=chatgpt.com "UdonSharp | UdonSharp"
