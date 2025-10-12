# vrmine — 詳細設計書 v1.0

設計の軸は **単一オーナー権限 / 整数格子 / 最小同期 / UI操作のみ / イベントは要求通知だけ** です。

---

## 1. 目的・前提

* **目的**：同一盤面に対して、参加者（Explorer）が**波**を発射し、公開ログ（入口→出口／最終色）をもとに**盤の色配置**を推理、**完全一致宣言**で勝利する体験を提供。
* **前提**

  * **Unity**：2022.3.22f1（VCCが自動誘導）。([creators.vrchat.com][1])
  * **SDK**：VRChat SDK3–Worlds（Udon / UdonSharp）。UdonSharpはC#→Udon ASM 変換。([creators.vrchat.com][2])
  * **Networking**：**Manual Sync**で重要状態のみ同期、`RequestSerialization()` をOwnerが明示呼出。イベントは**通知**用途。([creators.vrchat.com][3])
  * **イベント拡張**：カスタムネットワークイベントは**最大8パラメータ**（UdonSharpは `[NetworkCallable]`）。本設計は**パラメータ無しRPC＋Mailbox**でより安全に実装。([creators.vrchat.com][4])
  * **テスト**：ClientSim（公式）＋実クライアント、CyanEmuは補助。([creators.vrchat.com][5])

---

## 2. システム構成（コンポーネント）

| 役割        | クラス              |          権限 |       同期 | 要点                                      |
| --------- | ---------------- | ----------: | -------: | --------------------------------------- |
| 中枢・同期集約   | `GameController` | **Owner専用** |   Manual | 盤正本保持／同期、手番・ログ、判定RPC送出                  |
| 波シミュレーション | `WaveSimulator`  |       Owner |       なし | **整数格子**で決定論。角同時衝突の優先規則固定               |
| 手番管理      | `TurnQueue`      |       Owner | 一部Synced | FIFO／90秒タイムアウト                          |
| 宣言判定      | `Judge`          |       Owner |       なし | 宣言ペイロードと盤正本を突合                          |
| UI仲介      | `UIController`   |       Local |       なし | World Space Canvasのボタン → `PlayerClient` |
| 入力Mailbox | `PlayerClient`   |    **各自所有** | 一部Synced | 非Owner→Ownerの**安全ルート**（送信者ID/連打抑止）      |

> `GameController` は**見た目用の極小Quad**を子に持ち、常時視界内で**優先度安定化**。

---

## 3. ルール・盤仕様（確定）

* **グリッド**：10×8（セル整数座標）。
* **入口36点**：上10／右8／下10／左8、ID=0..35（時計回り）。
* **ピース**：色＝赤/青/黄/白(透明)/黒、形＝Dot1/Line2/L3（回転可）。各色≤5、**総占有≤20セル**。
* **波**：格子線直進。**色セル入射→90°反射**。黒は**吸収（即終了・出口なし）**。
* **合成規則**：白→{赤/青/黄}、二色→{紫/橙/緑}、三原色→灰。透明は不変。
* **公開情報**：各手番の「入口→出口, 最終色（吸収時 Absorb）」
* **勝利**：最初に**完全一致宣言**が通った者。
* **手番**：FIFO／**90秒**タイムアウト。

---

## 4. データモデル（ID・エンコード）

### 4.1 列挙

* `colorId`: `0=White, 1=Red, 2=Blue, 3=Yellow, 4=Purple, 5=Orange, 6=Green, 7=Gray, 255=None`
* `flags`（8bit）: `bit0=Absorbed(黒)`, `bit1=Reserved`, `bit2=Looped(巡回検出)`
* `entryId/exitId`: `0..35`（吸収・非到達は `exitId=255`）

### 4.2 UdonSynced（公開状態：**小容量固定**）

* `byte gridW=10, gridH=8`
* `uint boardSeed, boardHash`（**同一性確認用**）
* `int turnIndex`
* **ログ（リング）**

  * `byte ringSize=20, byte logCount, byte logHead`
  * `byte[80] logRing`（**1件=4B**：`entryId, exitId, colorId, flags` →20件=80B）

> Manual Sync 1回あたりの上限（**約280,496B**）を**大幅に下回る**設計。([creators.vrchat.com][6])

### 4.3 盤の正本（**Owner専有・非公開**）

* **フォーマット（可変長・~105B想定）**

  ```
  boardState:
    count: u8 (≤20)
    pieces: count × 5B
      - colorId: u8
      - shapeId: u8     // 0:Dot1, 1:Line2, 2:L3
      - rot: u8         // 0,1,2,3 (90度刻み)
      - x: u8           // 0..9
      - y: u8           // 0..7
    crc32: u32          // IEEE 802.3 (poly 0xEDB88320)
  ```
* **boardHash**：`crc32(boardState[0 : 1+count*5])` の値をUdonSyncedへ出す（**盤の同一性確認**のみ）。

### 4.4 Mailbox（各プレイヤー所有）

* `ownerPlayerId:int, reqSeq:int, reqType:byte(0/1/2)`
* `entryId:byte`（Wave用）
* `declLen:int, decl[≤900B]`（宣言ペイロード）

> 宣言ペイロードは**≤900B運用**（将来拡張・内部分割リスク回避）。([creators.vrchat.com][6])

---

## 5. アルゴリズム（決定論・整数格子）

### 5.1 入口ID ↔ 初期状態

* **初期状態**：`(x, y, dir)`

  * 上辺ID `0..9`: `x=j, y=-1, dir=+Z`
  * 右辺ID `10..17`: `x=10, y=i, dir=-X`
  * 下辺ID `18..27`: `x=j, y=8, dir=-Z`
  * 左辺ID `28..35`: `x=-1, y=i, dir=+X`
* **最初の進入**：1ステップで盤内へ（境界外→境界内へ座標更新）。

### 5.2 遷移（1ステップ）

1. `pos += dir`（格子1マス）
2. `pos` が盤外なら **終了** → `exitId =` 出口側ID、`colorId=現在色`
3. `pos` が黒セルなら **吸収** → `exitId=255, flags|=Absorbed`
4. `pos` が色セルなら **色合成** & **直角反射**（下表）
5. **ループ検出**：既訪 `(x,y,dir)` 出現で `flags|=Looped, exitId=255` で終了
6. **安全弾数**：`maxSteps = 4096` 超で**強制終了**（`Looped`相当）

**反射規則（角同時衝突時の優先）**

* **右手系優先**（例：進行+Xのまま角に入る場合、+Z方向へ先に反射判定→該当ならそちらを採用）。
* この規則は**固定**し、全クライアントで一致させる。

**色合成表（順不同）**

| 入射前   | 入射セル  | 出力色        |
| ----- | ----- | ---------- |
| 白     | 赤/青/黄 | その色        |
| 赤+青   | –     | 紫          |
| 赤+黄   | –     | 橙          |
| 青+黄   | –     | 緑          |
| 赤+青+黄 | –     | 灰          |
| 任意    | 透明    | 変化なし       |
| 任意    | 黒     | **吸収**（終了） |

> **実装注意**：**浮動小数のRay**は禁物。**整数格子**と離散方位4値で必ず計算。
> Manual/Continuousの違いと同期の信頼性は公式推奨のとおり**重要値はManual**で。([creators.vrchat.com][3])

### 5.3 出口ID算出

* 盤外に出た座標 `(x,y)` と方位 `dir` から**一意**に決定。

  * 上辺：`y==-1` → `id = x`
  * 右辺：`x==10` → `id = 10 + y`
  * 下辺：`y==8` → `id = 18 + x`
  * 左辺：`x==-1` → `id = 28 + y`

---

## 6. 同期・イベント（フロー）

### 6.1 波（Wave）

1. **Local**：UI→`PlayerClient.Client_SubmitWave(entryId)`
2. **Mailbox**：`reqType=Wave, entryId, reqSeq++` を同期（自分所有）。
3. **通知**：`GameController.Owner_OnMailboxUpdated` を**Owner宛**RPCで起動。
4. **Owner**：Mailboxを走査。`senderId==currentTurnPlayerId` のみ受理。
5. **Owner**：`WaveSimulator.Simulate()` で `(exitId, colorId, flags)` 決定→**ログ追記**。
6. **Owner**：`turnIndex++` → `TurnQueue.Owner_NextTurn()` → `RequestSerialization()`
7. **All**：`RPC_WaveResult_Sfx`（演出）。**表示はSyncedログから復元**。

> **Manual Sync**は**Owner→全員**で確定値を配布。`RequestSerialization()` 必須。([creators.vrchat.com][7])

### 6.2 宣言（Declare）

1. **Local**：UI→`Client_SubmitDeclare(payload≤900B)`
2. **Owner**：`Judge.Validate(payload, boardState)` → `ok/errors`
3. **All**：`RPC_WonSfx` or `RPC_DeclareFailSfx`（必要ならエラー数の表示は別Synced変数で共有）

### 6.3 所有権移行（Owner Leave）

* `OnOwnershipTransferred(newOwner)`

  * 旧Owner在室→**TransferBoard**（Owner→新Owner宛RPC）で `boardState` を**一括転送**（≤900B）。
  * 新Ownerは受信後に `RequestSerialization()` し、公開状態（Seed/Hash/ログ/手番）を配り直す。
  * **Seed再生成・盤再生成は行わない**（決定論相違リスクの回避）。
* Manual Syncの上限や送信間隔は**送信量依存でレート制限**される点に注意。([creators.vrchat.com][6])

---

## 7. UI/UX（最小）

* **World Space Canvas**：`pos=(0,1.35,1.20), rot=(0,180,0)`
* **3ペイン**

  1. 入口セレクタ＋**発射**ボタン（選択→確定の二段階）
  2. **ログ20件**（新しい順：`idx=(logHead-k-1)&(ringMask)`）
  3. **解答宣言**（座標・色の一括入力→送信）
* **アクセシビリティ**：全色に**ハッチ／記号**併記（色弱配慮）
* **物理トリガ不使用**：誤発火防止。Interact/UIのみ。

---

## 8. 空間仕様（Unity座標）

* **1u=1m**、ボード中心 `(0,0.85,0)`、セル `cell=0.12m`
* **セル中心→世界**

  ```
  worldX = (x - 4.5) * cell
  worldY = 0.85
  worldZ = (y - 3.5) * cell
  ```
* **入口36点**：盤外 `0.05m` オフセット配置（IDは時計回り）

---

## 9. エラー処理・検証

* **手番外入力**：Ownerで**棄却**（`senderId==currentTurn` を厳格チェック）。
* **連打**：Mailboxの `reqSeq` で**重複排除**。UdonSharpの `[NetworkCallable(MaxEventsPerSecond=…)]` を**Ownerハンドラに付与**推奨。([creators.vrchat.com][4])
* **巡回**：`Looped` フラグで終了（`exitId=255`）。
* **タイムアウト**：`TurnQueue.secondsPerTurn=90`、期限到来で**自動スキップ**。
* **遅参**：Synced（Seed/Hash/ログ/手番）で**即復元**、盤正本はOwnerのみ保持（非公開）。

---

## 10. パフォーマンス・帯域見積

* **Synced公開状態**：`~100B/更新`（ログ20×4B=80B＋α）
* **1手番の通信**：

  * Mailbox（自分所有Synced）小容量更新 → Owner処理 → Manual Sync（~100B）
  * 見た目RPC（引数無し）1回
* **上限**：Manual Syncの**~280KB/回**に対して十分小さい。イベントは**パラメータ無し**運用で安全。([creators.vrchat.com][6])

---

## 11. プラットフォーム（Quest最適化）

* **対象**：PC、**Quest 2/3/Pro**（同一ワールド）。
* **指針**：DrawCall削減、シンプルなUnlit、テクスチャ圧縮、LOD。
* **Android/iOSのビルド手順・推奨Unity**は公式手順を参照（**2022.3.22f1**）。([creators.vrchat.com][8])

---

## 12. テスト計画

* **ユニット（UdonSharp）**

  * 色合成：白→単色、二色→（紫/橙/緑）、三色→灰、透明→不変、黒→吸収
  * 反射：4方向＋角同時衝突（右手系優先）
  * 出口：**36入口×代表盤**でOwner/非Ownerの一致率100%
  * ループ：人工的な鏡配置で巡回検出
* **結合**

  * ClientSim：2–4クライアントで**遅参復元／所有者交代**の継続性検証。([creators.vrchat.com][5])
  * **最終は実クライアント検証**（ClientSimとの差異を吸収）。
  * CyanEmuはUI確認や簡易ネット挙動確認に補助的使用。([udonsharp.docs.vrchat.com][9])
* **受入基準**

  * 2–4人×20ターン連続で**クラッシュ/同期崩壊なし**
  * 所有者離脱後も**boardHash一致**・ログ欠落なし
  * 1手番あたりイベント≦3、ManualSync `byteCount < 1KB` を継続

---

## 13. セキュリティ・運用

* **改ざん耐性**：クライアント権限のため**強耐性不可**。
* **防御**：Owner側で**手番・送信者ID**検証／Mailbox `reqSeq`／RPC**レート制限**。
* **同一性**：`boardHash`（CRC32）で**同一盤**を確認。
* **将来互換**：盤正本は**bit/byte設計**を固定し、バージョン番号付与で後方互換。

---

## 14. 宣言ペイロード（推奨エンコード）

**可逆・小容量**を優先した最小案：

```
payload:
  version: u8 (=1)
  count:   u8 (≤20)
  items:   count × 3B
    - x: u8 (0..9)
    - y: u8 (0..7)
    - colorId: u8 (0..7)
```

* **検証**：`Judge.Validate()` は `count` 件の `(x,y,colorId)` を `boardState` と突合。
* **合格条件**：**全セル**が一致（= 盤に存在しないセルは白として扱うか、事前合意で明記）。
* 上記で**最大 1 + 1 + 20×3 = 62B** と小さく、余裕で900B内。

---

## 15. 実装ロードマップ

1. **M1（Core）**：`GameController`（Manual）／`TurnQueue`／`WaveSimulator(int格子)`／ログUI
2. **M2（堅牢化）**：`Judge` 完成／`TransferBoard`（Owner→新Owner）／所有者交代ハンドラ
3. **M3（最適化）**：Quest向け軽量化（LOD/材質）／アクセシビリティ微調整

---

## 16. 付録：インタフェース仕様（確定版）

### 16.1 UdonSynced（`GameController`）

```text
gridW:u8 (=10), gridH:u8 (=8)
boardSeed:u32, boardHash:u32
turnIndex:i32
ringSize:u8 (=20), logCount:u8, logHead:u8
logRing:u8[80]  // 20×4B (entry, exit, color, flags)
```

### 16.2 RPC / メソッド

* **Local → 自分Mailbox**

  * `Client_SubmitWave(entryId:u8)`
  * `Client_SubmitDeclare(payload:u8[], len:int)`
* **All/Owner**

  * `Owner_OnMailboxUpdated()`（Ownerだけが実処理）
  * `RPC_WaveResult_Sfx()`（見た目のみ）
  * `RPC_WonSfx() / RPC_DeclareFailSfx()`

> イベントのパラメータ上限や `[NetworkCallable]` は公式アナウンスを参照。**本設計は無引数RPC＋同期変数の原則**で安定化。([creators.vrchat.com][4])

---

## 17. 参考（根拠）

* **Unity/SDK バージョン**：2022.3.22f1 推奨・導入手順。([creators.vrchat.com][1])
* **Networking 概要/ManualSync/変数**：設計原則・`RequestSerialization()` 必須。([creators.vrchat.com][3])
* **ManualSync 上限**（約280,496B/回）：帯域見積の根拠。([creators.vrchat.com][6])
* **イベント機能拡張（8パラメータ / NetworkCallable）**：選択肢の最新仕様。([creators.vrchat.com][4])
* **ClientSim / CyanEmu**：テスト運用の根拠。([creators.vrchat.com][5])
* **プレイヤーAPI**（`VRCPlayerApi`, Join/Leave）：手番キュー管理の基礎。([creators.vrchat.com][10])

---

### 次の一手

* この設計に合わせて、**`WaveSimulator.Simulate` の完全実装仕様（擬似コード＋全ケース表）**と**`TransferBoard` の送受信チャンク設計**も即時に出せます。必要なら続けます。

[1]: https://creators.vrchat.com/sdk/upgrade/current-unity-version/?utm_source=chatgpt.com "Current Unity Version | VRChat Creation"
[2]: https://creators.vrchat.com/worlds/udon/udonsharp/?utm_source=chatgpt.com "UdonSharp | VRChat Creation"
[3]: https://creators.vrchat.com/worlds/udon/networking/?utm_source=chatgpt.com "Networking | VRChat Creation"
[4]: https://creators.vrchat.com/releases/release-3-8-1/?utm_source=chatgpt.com "Release 3.8.1 | VRChat Creation"
[5]: https://creators.vrchat.com/worlds/clientsim/?utm_source=chatgpt.com "ClientSim | VRChat Creation"
[6]: https://creators.vrchat.com/worlds/udon/networking/network-details/?utm_source=chatgpt.com "Networking Specs & Tricks - Udon"
[7]: https://creators.vrchat.com/worlds/udon/networking/variables/?utm_source=chatgpt.com "Network Variables | VRChat Creation"
[8]: https://creators.vrchat.com/platforms/android/build-test-mobile/?utm_source=chatgpt.com "Build and Test for Android Mobile | VRChat Creation"
[9]: https://udonsharp.docs.vrchat.com/community-resources/?utm_source=chatgpt.com "Community Resources | UdonSharp - VRChat"
[10]: https://creators.vrchat.com/worlds/udon/players/?utm_source=chatgpt.com "Player API | VRChat Creation"
