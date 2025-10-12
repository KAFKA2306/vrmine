**UdonSynced 変数定義**と**安全なイベント伝達（Mailbox/Fallback）**を備えた **UdonSharp 雛形**をまとめて置きます。
（※ VRChat の“パラメータ付きネットワークイベント”を使わない前提。全て **Owner単一権限／整数格子／UIトリガのみ** で完結）

> 配置：`Assets/Vrmine/` 直下に下記ファイルを作成（ファイル名コメント参照）。
> インスペクタで **BehaviourSyncMode = Manual** を設定し、参照を結線してください。

---

```csharp
// Assets/Vrmine/NetCommon.cs
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public static class NetConst
{
    // Colors
    public const byte C_White = 0, C_Red = 1, C_Blue = 2, C_Yellow = 3, C_Purple = 4, C_Orange = 5, C_Green = 6, C_Gray = 7, C_None = 255;

    // Flags bit
    public const byte F_Absorbed = 1 << 0;   // 黒で吸収
    public const byte F_AskedProbe = 1 << 1; // 予約

    // Grid
    public const byte GRID_W = 10;
    public const byte GRID_H = 8;

    // Log ring buffer
    public const int RING_SIZE = 20;
    public const int LOG_ITEM_BYTES = 4; // entry, exit, color, flags
    public const int LOG_BYTES = RING_SIZE * LOG_ITEM_BYTES;

    // Board transfer
    public const int BOARD_MAX_PIECES = 20;
    public const int DECLARE_MAX_BYTES = 900; // 安全上限

    // Mailbox types
    public const byte REQ_NONE = 0;
    public const byte REQ_WAVE = 1;
    public const byte REQ_DECLARE = 2;

    // Players
    public const int MAX_PLAYERS = 32;
}
```

```csharp
// Assets/Vrmine/PlayerClient.cs
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

// 各参加者が「自分所有」で持つ“郵便箱（MailBox）”。
// 非Owner→Ownerへ「手番入力」を安全に伝える最小パターン。
public class PlayerClient : UdonSharpBehaviour
{
    [UdonSynced] public int ownerPlayerId;   // このMailboxの持ち主（固定）
    [UdonSynced] public int reqSeq;          // 要求シーケンス（単調増加）
    [UdonSynced] public byte reqType;        // REQ_WAVE / REQ_DECLARE
    [UdonSynced] public byte entryId;        // Wave入口ID
    [UdonSynced] public int declLen;         // 宣言ペイロード長
    [UdonSynced] public byte[] decl = new byte[NetConst.DECLARE_MAX_BYTES]; // 宣言ペイロード（先頭declLenのみ有効）

    // 参照（インスペクタで割当）
    public GameController game;

    void Start()
    {
        if (Networking.LocalPlayer != null && Networking.IsOwner(gameObject))
        {
            ownerPlayerId = Networking.LocalPlayer.playerId;
            RequestSerialization();
        }
    }

    // UI から呼ぶ：波の発射要求
    public void Client_SubmitWave(byte inEntryId)
    {
        // 手番外はGameController側で弾く。ここでは投函のみ。
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        reqType = NetConst.REQ_WAVE;
        entryId = inEntryId;
        reqSeq++;
        RequestSerialization();
        // Ownerへ通知（パラメータ無しイベント）
        game.SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, nameof(GameController.Owner_OnMailboxUpdated));
    }

    // UI から呼ぶ：解答宣言
    public void Client_SubmitDeclare(byte[] payload, int length)
    {
        if (length > NetConst.DECLARE_MAX_BYTES) length = NetConst.DECLARE_MAX_BYTES;
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        reqType = NetConst.REQ_DECLARE;
        declLen = length;
        // コピー
        for (int i = 0; i < length; i++) decl[i] = payload[i];
        reqSeq++;
        RequestSerialization();
        game.SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, nameof(GameController.Owner_OnMailboxUpdated));
    }
}
```

```csharp
// Assets/Vrmine/WaveSimulator.cs
using UdonSharp;
using UnityEngine;

// 整数格子での決定論シミュレーション（最小コア）
public class WaveSimulator : UdonSharpBehaviour
{
    // 盤の非公開正本（Owner専有）を参照として受け取って計算する前提
    // boardState フォーマットは GameController 側コメント参照

    // 出力保持
    public byte lastExitId;
    public byte lastColorId;
    public byte lastFlags;

    // 入口ID(0..35)と boardState から結果を計算
    public void Simulate(byte entryId, byte[] boardState, int boardLen)
    {
        // TODO: 実装
        // 1) entryId→初期セルと方位に変換
        // 2) 整数格子で遷移（角同時衝突は「右手系優先」）
        // 3) 色合成（透明=不変、黒=即吸収）
        // 4) 盤外で出口ID確定／黒なら exit=255, F_Absorbed
        lastExitId  = 0;
        lastColorId = NetConst.C_White;
        lastFlags   = 0;
    }
}
```

```csharp
// Assets/Vrmine/Judge.cs
using UdonSharp;
using UnityEngine;

// 宣言（payload）と boardState を突合し、完全一致かを判定
public class Judge : UdonSharpBehaviour
{
    public bool Validate(byte[] declarePayload, int declLen, byte[] boardState, int boardLen, out byte errors)
    {
        // TODO: 実装（declarePayload のエンコード設計に依存）
        errors = 0;
        return false;
    }
}
```

```csharp
// Assets/Vrmine/TurnQueue.cs
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// FIFO 手番管理（最小）。Ownerのみが状態を進める。
public class TurnQueue : UdonSharpBehaviour
{
    [UdonSynced] public int turnIndex;    // 0..（手番カウンタ）
    [UdonSynced] public int currentPlayerId; // 現在の手番プレイヤー

    public int secondsPerTurn = 90;

    // 内部
    private float _deadline;
    private VRCPlayerApi[] _players = new VRCPlayerApi[NetConst.MAX_PLAYERS];
    private int _playerCount;

    public void Owner_InitQueue()
    {
        _playerCount = VRCPlayerApi.GetPlayerCount();
        VRCPlayerApi.GetPlayers(_players);
        currentPlayerId = _players[0].playerId;
        _deadline = Time.time + secondsPerTurn;
        RequestSerialization();
    }

    public bool Owner_IsTurn(int playerId)
    {
        return playerId == currentPlayerId;
    }

    public void Owner_NextTurn()
    {
        // 次の在室プレイヤーに回す
        _playerCount = VRCPlayerApi.GetPlayerCount();
        VRCPlayerApi.GetPlayers(_players);
        int idx = 0;
        for (int i = 0; i < _playerCount; i++) if (_players[i].playerId == currentPlayerId) { idx = i; break; }
        idx = (_playerCount == 0) ? 0 : (idx + 1) % _playerCount;
        currentPlayerId = (_playerCount == 0) ? -1 : _players[idx].playerId;
        turnIndex++;
        _deadline = Time.time + secondsPerTurn;
        RequestSerialization();
    }

    public bool Owner_IsTimeout()
    {
        return Time.time >= _deadline;
    }
}
```

```csharp
// Assets/Vrmine/UIController.cs
using UdonSharp;
using UnityEngine;

// World Space Canvas からの最小フック
public class UIController : UdonSharpBehaviour
{
    public PlayerClient mailbox; // LocalPlayer 所有の PlayerClient を割当

    // ボタン：入口IDで発射
    public void OnClick_Fire_Entry00() { mailbox.Client_SubmitWave(0); }
    // 必要数分だけボタン→メソッドを用意（または入力フィールド→変換）
    // public void OnClick_Fire_Entry01() { mailbox.Client_SubmitWave(1); } ...

    // 宣言送信（入力UIからシリアライズしたbyte配列を渡す）
    public void OnClick_Declare(byte[] payload, int length)
    {
        mailbox.Client_SubmitDeclare(payload, length);
    }
}
```

```csharp
// Assets/Vrmine/GameController.cs
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

// 中枢：Ownerに集約された同期・判定・結果配信
public class GameController : UdonSharpBehaviour
{
    // ===== UdonSynced（公開最小） =====
    [UdonSynced] public byte gridW = NetConst.GRID_W, gridH = NetConst.GRID_H;

    [UdonSynced] public uint boardSeed;      // 初回Ownerが生成
    [UdonSynced] public uint boardHash;      // CRC32 等（同一性確認）

    [UdonSynced] public int  turnIndex;      // 0..（TurnQueue と重複管理可）
    [UdonSynced] public byte ringSize = NetConst.RING_SIZE;
    [UdonSynced] public byte logCount;
    [UdonSynced] public byte logHead;        // 書込位置
    [UdonSynced] public byte[] logRing = new byte[NetConst.LOG_BYTES]; // 1件=4B: entry, exit(0..35|255), color(0..7|255), flags

    // ===== Owner専有（非公開）=====
    // boardState: 可変長シリアライズ（Piece列）：[count(1B)] + count*5B + hash(4B) など。最大 ~ (1 + 20*5 + 4) = 105B 程度を想定。
    private byte[] boardState = new byte[128];
    private int boardLen;

    // ===== 参照 =====
    public WaveSimulator wave;
    public TurnQueue    queue;
    public Judge        judge;

    // ===== 内部（Owner側処理状態） =====
    private int[] handledSeqByPlayerId = new int[NetConst.MAX_PLAYERS]; // 各プレイヤーの処理済みreqSeq

    void Start()
    {
        // 初回Ownerが Seed と盤を確定
        if (Networking.IsOwner(gameObject))
        {
            if (boardSeed == 0) boardSeed = (uint)Random.Range(1, int.MaxValue);
            Owner_GenerateBoardFromSeed();
            RequestSerialization();
        }
    }

    // 盤生成（決定論／Ownerのみ）
    private void Owner_GenerateBoardFromSeed()
    {
        // TODO: PRNGでPiece群を配置→ boardState へエンコード、boardHash算出
        boardLen = 0;
        boardHash = 0;
    }

    // ====== Mailbox 経由イベント（Ownerでのみ実行）======
    public void Owner_OnMailboxUpdated()
    {
        if (!Networking.IsOwner(gameObject)) return;

        // すべての PlayerClient を走査して未処理要求を処理
        PlayerClient[] mailboxes = (PlayerClient[])FindObjectsOfType(typeof(PlayerClient));
        int mCount = mailboxes.Length;
        for (int i = 0; i < mCount; i++)
        {
            PlayerClient mb = mailboxes[i];
            int pid = mb.ownerPlayerId;
            if (pid <= 0 || pid >= NetConst.MAX_PLAYERS) continue;

            if (mb.reqSeq != handledSeqByPlayerId[pid])
            {
                handledSeqByPlayerId[pid] = mb.reqSeq; // 先に進めて多重処理を抑止

                if (mb.reqType == NetConst.REQ_WAVE)       Owner_HandleWave(pid, mb.entryId);
                else if (mb.reqType == NetConst.REQ_DECLARE) Owner_HandleDeclare(pid, mb.decl, mb.declLen);
            }
        }
    }

    private void Owner_HandleWave(int senderPlayerId, byte entryId)
    {
        if (!queue.Owner_IsTurn(senderPlayerId)) return; // 手番外は無視

        // シミュレート
        wave.Simulate(entryId, boardState, boardLen);
        byte exitId  = wave.lastExitId;
        byte colorId = wave.lastColorId;
        byte flags   = wave.lastFlags;

        // ログ追記（リング）
        int head = logHead; // 0..RING_SIZE-1
        int ofs = head * NetConst.LOG_ITEM_BYTES;
        logRing[ofs + 0] = entryId;
        logRing[ofs + 1] = exitId;
        logRing[ofs + 2] = colorId;
        logRing[ofs + 3] = flags;

        head = (head + 1) % NetConst.RING_SIZE;
        if (logCount < NetConst.RING_SIZE) logCount++;
        logHead = (byte)head;

        // 1手番終了（本作は1発/手番想定）
        turnIndex++;
        queue.Owner_NextTurn();

        RequestSerialization();
        // 全員へ結果通知（演出用／状態はSyncedで反映済み）
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(RPC_WaveResult_Sfx));
    }

    private void Owner_HandleDeclare(int senderPlayerId, byte[] payload, int length)
    {
        if (!queue.Owner_IsTurn(senderPlayerId)) return;

        byte errors;
        bool ok = judge.Validate(payload, length, boardState, boardLen, out errors);

        if (ok)
        {
            // 勝利演出など
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(RPC_WonSfx));
        }
        else
        {
            // 失敗演出など（必要なら errors を UI に反映：UI側はSyncedを監視 or 別フィールドで共有）
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(RPC_DeclareFailSfx));
        }
    }

    // ====== 見た目用RPC（状態はSyncedで共有済み）======
    public void RPC_WaveResult_Sfx() { /* TODO: ライン描画/SE */ }
    public void RPC_WonSfx()         { /* TODO: 勝利演出 */ }
    public void RPC_DeclareFailSfx() { /* TODO: 失敗演出 */ }

    // ====== 所有権移行（Owner→新Ownerへ盤の正本を手渡し）======
    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        // 新Ownerはここで盤を再生成せず、旧Ownerから受領する想定。
        // Mailboxパターンでの明示転送が難しい場合は、boardStateを UdonSynced にせずとも
        // 旧Owner在室中にすでに同期済みの別コンポーネント経由で渡す等の簡易策も可。
        // 最小雛形では割愛。必要なら TransferBoard 実装を追加。
    }
}
```

---

## 使い方（最小の結線メモ）

1. シーンに空オブジェクト `GameController` を置き `GameController.cs` を付与（**Ownerに集約**）。
   　- `WaveSimulator`, `TurnQueue`, `Judge` をそれぞれ別オブジェクトに付与して参照を結線。
2. 各参加者がスポーン時に **自分所有**の `PlayerClient` を生成/保持（プレハブを VRCStation 近くに配置し、`Networking.SetOwner(LocalPlayer)` を `Start()` で確実化）。
3. `UIController` は World Space Canvas に置き、ボタン `OnClick()` から `mailbox.Client_SubmitWave(entryId)` を呼ぶ。
4. `TurnQueue.secondsPerTurn=90`。`Owner_InitQueue()` を初回Ownerの `Start()` か `OnPlayerJoined` で実行。

---

## 追加メモ（実装時の“落とし穴”回避）

* **整数格子**：座標は `(x, y, dir4)` のみで遷移。**浮動小数**でのレイ判定は非決定性を招くので禁止。
* **ログ表示**：`idx=(logHead - k - 1 + RING_SIZE) % RING_SIZE` で新しい順に読む。
* **レート制限**：UI側で二段階（選択→発射）＋ `PlayerClient` 側で**連打抑止**（短時間の重複Submitを無視）を入れると安定。
* **Owner可視化**：`GameController` 子に小さなUnlit Quadを置いて**常時視界内**に。

---

必要ならこの雛形に **`TransferBoard`（Owner→新Ownerの非公開盤受け渡し）** と **`WaveSimulator.Simulate` の具体実装**、**宣言ペイロードのエンコード仕様**（例：`count + [x,y,color]*`）を追加した完全版まで一気に出します。
