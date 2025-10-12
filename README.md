# vrmine — VRChat推理ゲーム

**Unity 2022.3.22f1 / SDK3-Worlds / UdonSharp / Manual Sync**

## コンセプト

波を発射→公開ログ（入口→出口/色）→盤面推理→完全一致宣言で勝利

**ルール**: 10×8盤、入口36点、波は格子直進/色セルで90°反射/黒で吸収、手番90秒

## 実装（エラー回避済み）

### 1. 定数
```csharp
// NetConst.cs (static class禁止)
using UdonSharp;
public class NetConst : UdonSharpBehaviour {
    public const byte GRID_W=10, GRID_H=8, RING_SIZE=20;
}
```

### 2. Mailbox
```csharp
// PlayerClient.cs
using UdonSharp;
using VRC.SDKBase;
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PlayerClient : UdonSharpBehaviour {
    [UdonSynced] public int ownerPlayerId, reqSeq;
    [UdonSynced] public byte reqType, entryId;
    [UdonSynced] public byte[] decl = new byte[900]; // 初期化必須
    public GameController game;

    void Start() {
        if (Networking.IsOwner(gameObject)) {
            ownerPlayerId = Networking.LocalPlayer.playerId;
            RequestSerialization();
        }
    }

    public void SubmitWave(byte id) {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        reqType=1; entryId=id; reqSeq++;
        RequestSerialization();
        game.SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, nameof(GameController.Pull));
    }
}
```

### 3. GameController
```csharp
// GameController.cs
using UdonSharp;
using VRC.SDKBase;
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class GameController : UdonSharpBehaviour {
    [UdonSynced] public uint boardSeed, boardHash;
    [UdonSynced] public int turnIndex;
    [UdonSynced] public byte logCount, logHead;
    [UdonSynced] public byte[] logRing = new byte[80]; // 20×4B、初期化必須

    public PlayerClient[] mailboxes; // インスペクタで手動割当（FindObjectsOfType禁止）
    public WaveSimulator wave;
    private byte[] boardState = new byte[128];
    private int[] handledSeq = new int[32];

    void Start() {
        if (Networking.IsOwner(gameObject) && boardSeed==0) {
            boardSeed = (uint)Random.Range(1, int.MaxValue);
            RequestSerialization();
        }
    }

    public void Pull() {
        if (!Networking.IsOwner(gameObject)) return;
        for (int i=0; i<mailboxes.Length; i++) {
            var mb = mailboxes[i];
            int pid = mb.ownerPlayerId;
            if (pid>0 && mb.reqSeq!=handledSeq[pid]) {
                handledSeq[pid] = mb.reqSeq;
                if (mb.reqType==1) HandleWave(mb.entryId);
            }
        }
    }

    void HandleWave(byte entryId) {
        wave.Simulate(entryId, boardState);
        int ofs = logHead*4;
        logRing[ofs]=entryId; logRing[ofs+1]=wave.exitId; logRing[ofs+2]=wave.colorId; logRing[ofs+3]=wave.flags;
        logHead = (byte)((logHead+1)%20);
        if (logCount<20) logCount++;
        turnIndex++;
        RequestSerialization();
    }
}
```

### 4. WaveSimulator
```csharp
// WaveSimulator.cs
using UdonSharp;
public class WaveSimulator : UdonSharpBehaviour {
    public byte exitId, colorId, flags;

    public void Simulate(byte entryId, byte[] board) {
        int x=0, y=0, dx=0, dz=1;
        byte color=0;

        // 入口→初期位置（整数のみ）
        if (entryId<10) { x=entryId; y=-1; dz=1; }
        else if (entryId<18) { x=10; y=entryId-10; dx=-1; dz=0; }
        // 他の辺も同様

        // 格子遷移（浮動小数禁止）
        for (int step=0; step<4096; step++) {
            x+=dx; y+=dz;
            if (x<0||x>=10||y<0||y>=8) {
                exitId = (byte)(y==-1?x : x==10?10+y : y==8?18+x : 28+y);
                colorId = color;
                return;
            }
            // 反射・合成ロジック（整数演算のみ）
        }
        flags=1; exitId=255; // Looped
    }
}
```

## エラー回避チェック

- [x] `static class` → 通常クラス
- [x] UdonSynced配列 → `new byte[N]`で初期化
- [x] `FindObjectsOfType` → 手動割当
- [x] 浮動小数 → 整数格子のみ
- [x] `[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]` 必須
- [x] `RequestSerialization()` 呼出

## 参考

- UdonSharp制限: https://udonsharp.docs.vrchat.com/
- Manual Sync: https://creators.vrchat.com/worlds/udon/networking/variables/
