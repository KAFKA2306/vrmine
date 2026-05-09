using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class GameController : UdonSharpBehaviour
{
    [UdonSynced] public uint boardSeed;
    [UdonSynced] public uint boardHash;
    [UdonSynced] public int turnIndex;
    [UdonSynced] public int winnerPlayerId;
    [UdonSynced] public byte declarationResult;
    [UdonSynced] public byte logHead;
    [UdonSynced] public byte logCount;
    [UdonSynced] public byte[] logData = new byte[NetConst.LogRingSize * LogStream.EntrySize];
    public PlayerClient[] mailboxes = new PlayerClient[0];
    public WaveSimulator wave;
    public LogStream logStream;
    public BoardState board;
    int[] handledSequence = new int[64];

    void Start()
    {
        logStream.Push(logHead, logCount, logData);
        SyncDashboard();
        if (!Networking.IsOwner(gameObject)) return;
        PushEvent(LogStream.KindStart, 0);
        if (boardSeed == 0) boardSeed = (uint)Random.Range(1, int.MaxValue);
        BakeBoard();
        PushEvent(LogStream.KindRule, 0);
        RequestSerialization();
    }

    public override void OnDeserialization()
    {
        logStream.Push(logHead, logCount, logData);
        SyncDashboard();
    }

    void BakeBoard()
    {
        boardHash = board.Bake(boardSeed);
        board.phase = BoardState.PhaseRuleSelect;
        board.warningCount = 0;
        board.syncState = 1;
        SyncRules();
        board.RequestSerialization();
        SyncDashboard();
    }

    public void Pull()
    {
        if (!Networking.IsOwner(gameObject)) return;
        int limit = mailboxes.Length;
        for (int i = 0; i < limit; i++)
        {
            PlayerClient client = mailboxes[i];
            int slot = client.ownerPlayerId & 63;
            int sequence = client.requestSequence;
            if (handledSequence[slot] == sequence) continue;
            handledSequence[slot] = sequence;
            if (client.requestType == 1) HandleWave(client.entryId);
            else if (client.requestType == 2) HandleDeclaration(client.ownerPlayerId, client.declaration);
        }
    }

    public void OnDeclare()
    {
        SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(Pull));
    }

    void HandleWave(byte entryId)
    {
        // Stitch-Meister Trick-taking logic
        board.phase = BoardState.PhaseResolveTrick;
        byte seat = (byte)(turnIndex % mailboxes.Length);
        board.currentPlayerSeat = seat;
        
        // Derive suit and number from entryId (1-60)
        // 0-14: Fan, 15-29: Coin, 30-44: Koi, 45-59: Gate
        int suit = entryId / 15;
        int number = (entryId % 15) + 1;

        board.trickSeats[turnIndex % 4] = seat;
        board.trickCards[turnIndex % 4] = entryId;

        // If it's the first card of the trick, set lead suit
        if ((turnIndex % mailboxes.Length) == 0)
        {
            // Lead suit logic would go here
        }

        wave.Simulate(entryId, board.cells);
        RecordLog(entryId, (byte)suit, (byte)number, 0);
        
        turnIndex++;
        if (turnIndex % mailboxes.Length == 0)
        {
            ResolveWinner();
        }
        
        board.trickIndex = (byte)turnIndex;
        RequestSerialization();
        board.RequestSerialization();
        SyncDashboard();
    }

    void ResolveWinner()
    {
        // Simple trick winner determination
        int leadSuit = board.trickCards[0] / 15;
        int bestSeat = board.trickSeats[0];
        int bestNumber = (board.trickCards[0] % 15) + 1;

        for (int i = 1; i < mailboxes.Length; i++)
        {
            int currentSuit = board.trickCards[i] / 15;
            int currentNumber = (board.trickCards[i] % 15) + 1;

            // Basic follow-suit winner logic (no trump yet for simplicity in MVP)
            if (currentSuit == leadSuit && currentNumber > bestNumber)
            {
                bestNumber = currentNumber;
                bestSeat = board.trickSeats[i];
            }
        }

        board.takenTricks[bestSeat]++;
        PushEvent(LogStream.KindScore, (byte)bestSeat);
    }

    void HandleDeclaration(int playerId, byte[] data)
    {
        bool match = board.Matches(data);
        winnerPlayerId = match ? playerId : 0;
        declarationResult = match ? (byte)1 : (byte)2;
        int seat = playerId & 3;
        if (match)
        {
            board.scores[seat]++;
            board.takenTricks[seat]++;
            board.phase = BoardState.PhaseScore;
            board.syncState = 1;
            PushEvent(LogStream.KindScore, (byte)seat);
        }
        else
        {
            board.warningCount++;
            board.phase = BoardState.PhaseWarning;
            board.syncState = 3;
            PushEvent(LogStream.KindWarning, 0);
        }
        RequestSerialization();
        board.RequestSerialization();
        SyncDashboard();
    }

    void RecordLog(byte entryId, byte exitId, byte colorId, byte flag)
    {
        int offset = logHead * LogStream.EntrySize;
        logData[offset] = entryId;
        logData[offset + 1] = exitId;
        logData[offset + 2] = colorId;
        logData[offset + 3] = flag;
        logHead++;
        if (logHead >= NetConst.LogRingSize) logHead = 0;
        if (logCount < NetConst.LogRingSize) logCount++;
        logStream.Push(logHead, logCount, logData);
    }

    void SyncRules()
    {
        int limit = board.blocks.Length;
        int total = board.selectedRules.Length;
        for (int i = 0; i < total; i++)
        {
            if (i < limit) board.selectedRules[i] = board.blocks[i];
            else board.selectedRules[i] = 0;
            if (i < limit) board.ruleHandCounts[i] = (byte)(limit - i);
            else board.ruleHandCounts[i] = 0;
        }
    }

    void SyncDashboard()
    {
        if (board == null) return;
        int seats = mailboxes.Length;
        if (seats < 1) seats = 1;
        board.roundIndex = (byte)(turnIndex / seats + 1);
        board.currentPlayerSeat = (byte)(turnIndex % seats);
        if (board.cells.Length > 0) board.selectedCell = (byte)(turnIndex % board.cells.Length);
        if (board.syncState != 3) board.syncState = Networking.IsOwner(gameObject) ? (byte)1 : (byte)2;
        SyncRules();
    }

    void PushEvent(byte kind, byte value)
    {
        RecordEvent(kind, value);
    }

    void RecordEvent(byte kind, byte value)
    {
        int offset = logHead * LogStream.EntrySize;
        logData[offset] = 255;
        logData[offset + 1] = kind;
        logData[offset + 2] = value;
        logData[offset + 3] = 0;
        logHead++;
        if (logHead >= NetConst.LogRingSize) logHead = 0;
        if (logCount < NetConst.LogRingSize) logCount++;
        logStream.Push(logHead, logCount, logData);
    }
}
