using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class GameController : UdonSharpBehaviour
{
    public BoardState board;
    public BoardView view;
    
    // Legacy / Internal State
    [UdonSynced] public uint boardSeed;
    [UdonSynced] public uint boardHash;
    [UdonSynced] public int turnIndex;
    [UdonSynced] public int winnerPlayerId;
    [UdonSynced] public byte declarationResult;
    [UdonSynced] public byte logHead;
    [UdonSynced] public byte logCount;
    [UdonSynced] public byte[] logData = new byte[NetConst.LogRingSize * 4]; // EntrySize assumed 4
    public PlayerClient[] mailboxes = new PlayerClient[0];
    public WaveSimulator wave;
    public LogStream logStream;
    int[] handledSequence = new int[64];

    [Header("Runtime State")]
    public int localPlayerSeat = 0;

    void Start()
    {
        if (Networking.IsOwner(gameObject))
        {
            if (boardSeed == 0) boardSeed = (uint)Random.Range(1, int.MaxValue);
            SetupGame();
        }
        Render();
    }

    public override void OnDeserialization()
    {
        Render();
    }

    void SetupGame()
    {
        boardHash = board.Bake(boardSeed);
        board.phase = BoardState.PhasePlayCard;
        board.currentPlayerSeat = 0;
        board.warningCount = 0;
        board.syncState = 1;
        
        // Initial setup cards as requested:
        // Fan 1, Coin 6, Koi 10, Gate 15
        byte[] setup = {
            (byte)((NetConst.SuitFan << 4) | 1),
            (byte)((NetConst.SuitCoin << 4) | 6),
            (byte)((NetConst.SuitKoi << 4) | 10),
            (byte)((NetConst.SuitGate << 4) | 15)
        };
        
        for (int i = 0; i < setup.Length; i++)
        {
            board.playerHands[i] = setup[i];
        }
        
        SyncDashboard();
        board.RequestSerialization();
        RequestSerialization();
    }

    public void OnCardClicked(int cardIndex)
    {
        if (board.phase != BoardState.PhasePlayCard) return;
        if (board.currentPlayerSeat != (byte)localPlayerSeat) return;
        
        if (!Networking.IsOwner(gameObject))
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            Networking.SetOwner(Networking.LocalPlayer, board.gameObject);
        }
        
        TryPlayCard(localPlayerSeat, cardIndex);
    }

    public void TryPlayCard(int playerSeat, int handIndex)
    {
        int offset = playerSeat * NetConst.MaxHandSize;
        byte card = board.playerHands[offset + handIndex];
        if (card == 0) return;

        board.trickCards[playerSeat] = card;
        board.playerHands[offset + handIndex] = 0;
        
        turnIndex++;
        board.currentPlayerSeat = (byte)(turnIndex % NetConst.MaxPlayers);
        
        SyncDashboard();
        board.RequestSerialization();
        RequestSerialization();
        Render();
    }

    public void Render()
    {
        if (view != null) view.Render();
    }

    public void Pull()
    {
        if (!Networking.IsOwner(gameObject)) return;
        for (int i = 0; i < mailboxes.Length; i++)
        {
            PlayerClient client = mailboxes[i];
            int slot = client.ownerPlayerId & 63;
            if (handledSequence[slot] == client.requestSequence) continue;
            handledSequence[slot] = client.requestSequence;
            // Handle client requests here
        }
    }

    public void OnDeclare()
    {
        SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(Pull));
    }

    void SyncDashboard()
    {
        if (board == null) return;
        board.roundIndex = (byte)(turnIndex / NetConst.MaxPlayers + 1);
        if (board.syncState != 3) board.syncState = Networking.IsOwner(gameObject) ? (byte)1 : (byte)2;
        
        // Rule Sync
        for (int i = 0; i < 4; i++)
        {
            if (i < board.blocks.Length) board.selectedRules[i] = board.blocks[i];
            board.ruleHandCounts[i] = (byte)(4 - i);
        }
    }
}
