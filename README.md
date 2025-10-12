# vrmine — ミニマル設計仕様書 v1.0

## 0. 目的・範囲（縮約）

* **目的**：同一盤面に対し、参加者が**波を発射**し、公開ログ（入口→出口／最終色）から推理し、**完全一致宣言**で勝利する。
* **範囲外**：外部サーバ・DB・ランキング・商用/版権判断。
* **プラットフォーム**：PC、Quest 2/3/Pro（同一ワールド）。
* **設計原則**：**単一オーナー権限／整数格子／最小同期／UI操作のみ**。

---

## 1. 技術要件（固定）

* **Unity**：2022.3.22f1（VCC準拠）
* **SDK**：VRChat SDK3–Worlds（Udon / UdonSharp）
* **Networking**：`BehaviourSyncMode=Manual`、カスタムイベントは**最大8引数**
* **テスト**：ClientSim（基本）＋実クライアント最終確認、CyanEmuは補助

---

## 2. ゲームコア（最小ルール）

* **盤**：10×8 セル格子。
* **入口**：外周 36 点（上10/右8/下10/左8）ID=0..35（時計回り）。
* **ピース**：色＝赤/青/黄/白(透明)/黒、形＝Dot1/Line2/L3（回転可）、各色≤5、**総占有≤20セル**。
* **波**：格子線を直進。色セル入射で**90°反射**。盤外で**出口確定**。**黒は吸収**（終了・出口なし）。
* **色合成**：初期白→単色/二色(紫・橙・緑)/三原色(灰)。透明は不変、黒は吸収。
* **公開**：毎手番の「入口→出口／最終色（吸収時“Absorb”）」
* **勝利**：**全セル色配置**の完全一致宣言が最初に通った者。
* **手番**：FIFOキュー。**90秒/手番**で自動スキップ。

---

## 3. 決定論と非公開状態

* **Seed**：初回Ownerが32bit乱数で `boardSeed` を生成・同期。
* **盤面の正本**：**Owner専有の `boardState`（非公開）**。Seed再生成は行わない。
* **移譲**：所有者交代・遅参Owner合流時は**Owner→新Owner宛イベント**で `boardState` を一括送達（≤900B目安）→新Ownerが `RequestSerialization()`。

### boardState（最小表現）

```
pieceCount ≤ 20
Piece { colorId:byte, shapeId:byte, rot:byte, x:byte, y:byte }  // 原点セル(x,y)
boardHash:uint32  // CRC32
```

---

## 4. 同期・イベント設計（必要最小限）

### UdonSynced（公開最小）

* `byte gridW=10, gridH=8`
* `uint boardSeed, boardHash`
* `int turnIndex`（0..）
* **ログ（リング）**

  * `byte ringSize=20, byte logCount, byte logHead`
  * `byte[80] logRing`（1件=4B：`entryId, exitId(0..35|255), colorId(0..7|255), flags`）

### ネットワークイベント

* `WaveRequest(byte entryId, int senderId)` → **Owner**

  * Owner側で `senderId` が**手番者**か検証。レート制限必須。
* `WaveResult(byte entryId, byte exitId, byte colorId, byte flags)` → **All**
* `Declare(byte[] payload≤900)` → **Owner**（宣言内容は圧縮/簡素表現）
* `JudgeResult(bool won, byte errors)` → **All**
* `TransferBoard(byte[] boardState)` → **Target=NewOwner**

> 状態は**Synced変数**（ログ/手番/Seed等）で再現、イベントは**要求/結果通知**のみ。

---

## 5. シミュレーション（整数格子・決定論）

* **座標**：セル整数 `(x∈[0,9], y∈[0,7])` と**方位4値**で遷移。
* **反射**：入射方向に直交反転。**角同時衝突時**は「右手系優先」（例：+X→+Zを優先）を固定。
* **色合成**：単色→二色（順不同）→三色、透明は不変、黒は即吸収（結果報告なし・`exitId=255, flags.Absorbed=1`）。

---

## 6. 手番・検証・ログ

* **TurnQueue**：参加者をFIFO。`turnIndex`は手番進行でOwner更新。
* **検証**：`Declare(payload)` 受信でOwnerが `boardState` と突合→一致時 `JudgeResult(true,0)`。
* **ログ**：`logHead` へ追記。UIは `k=0..logCount-1` を `idx=(logHead-k-1)&(ringMask)` で逆順表示。
* **拒否**：手番外/多発は無効化（WaveRequest検証＋レート制限）。

---

## 7. 空間配置（最小）

* **座標系**：Unity（Y↑, Z前, X右），**1u=1m**
* **ボード中心**：`(0, 0.85, 0)`、**セル**：`cell=0.12`
* **セル中心→世界**

  ```
  worldX = (x - 4.5) * cell
  worldY = 0.85
  worldZ = (y - 3.5) * cell
  ```
* **入口36点**：盤外オフセット `0.05m`（上/右/下/左を時計回りID割当）

---

## 8. UI/UX（最小）

* **World Space Canvas**（`(0,1.35,1.20)`, 反転180°）

  * 左：入口セレクタ＋**発射**（ボタン）
  * 中：**ログ20件**（`A5→K12, Purple` / `A5→Absorb`）
  * 右：**解答宣言**（一括入力→送信）
* **操作**：**UIのみ**。物理トリガ/コライダ発火は**不使用**。
* **配慮**：色は記号/ハッチ併記。

---

## 9. エラー/運用（要点）

* **所有者離脱**：`OnOwnershipTransferred` で新Ownerへ→即 `TransferBoard` 受領→`RequestSerialization()`。
* **通信**：1手番あたりイベント概ね≤3回、宣言は**≤900B**運用。
* **可視性**：`GameController` 子に**小型可視Quad**を置き常時視界内（同期優先度の安定化）。

---

## 10. 受入基準（最小）

* 2–4クライアントで**20ターン**連続：クラッシュ/同期崩壊なし
* **遅参/所有者交代**後も `boardHash` 一致・ログ欠落なし
* **波の結果**：36入口×代表盤でOwner/非Ownerの**一致率100%**

---

## 11. マイルストーン（短期）

1. **M1**：`GameController`（Manual Sync）／`TurnQueue`／`WaveSimulator`（整数格子）／ログUI
2. **M2**：`Declare`→`Judge` 実装／`TransferBoard`／所有者交代ハンドラ
3. **M3**：Quest軽量化（LOD/材質差し替え）／アクセシビリティ微調整

---

## 12. 付録A：実装用ミニYAML

```yaml
Rule:
  grid: {w: 10, h: 8, cell_m: 0.12}
  entries: {offset_m: 0.05, ids: clockwise_0_35}
  colors: {ids: [White,Red,Blue,Yellow,Purple,Orange,Green,Gray], oil: Absorb}
  pieces: {shapes: [Dot1, Line2, L3], per_color_limit: 5, max_cells: 20}
Turn:
  seconds: 90
Net:
  sync: Manual
  ring_log: {size: 20, item_bytes: 4}
  declare_max_bytes: 900
Authority:
  owner: single
  seed: owner_random
Spatial:
  board_origin: [0,0.85,0]
UI:
  canvas_main: {pos: [0,1.35,1.20], rot_deg: [0,180,0]}
Platforms: [PC, Quest2, Quest3, QuestPro]
```

---

## 13. 付録B：UdonSharp スケルトン（名称のみ）

* `GameController`（Owner権限/Sync集約/Ownership移行）
* `WaveSimulator`（整数格子/反射/合成）
* `TurnQueue`（手番制御/タイムアウト）
* `Judge`（宣言突合せ）
* `UIController`（入口選択/発射/ログ/宣言）
