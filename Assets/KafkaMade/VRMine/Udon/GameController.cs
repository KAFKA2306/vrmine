using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

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
    public PlayerClient[] mailboxes;
    public WaveSimulator wave;
    public LogStream logStream;
    public BoardState board;
    int[] handledSequence = new int[64];

    void Start()
    {
        logStream.Pull(logHead, logCount, logData);
        if (!Networking.IsOwner(gameObject)) return;
        if (boardSeed == 0) boardSeed = (uint)Random.Range(1, int.MaxValue);
        BakeBoard();
        RequestSerialization();
    }

    public override void OnDeserialization()
    {
        logStream.Pull(logHead, logCount, logData);
    }

    void BakeBoard()
    {
        boardHash = board.Bake(boardSeed);
        board.RequestSerialization();
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

    void HandleWave(byte entryId)
    {
        wave.Simulate(entryId, board.cells);
        RecordLog(entryId, wave.exitId, wave.colorId, wave.flags);
        turnIndex++;
        RequestSerialization();
    }

    void HandleDeclaration(int playerId, byte[] data)
    {
        bool match = board.Matches(data);
        winnerPlayerId = match ? playerId : 0;
        declarationResult = match ? (byte)1 : (byte)2;
        RequestSerialization();
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
        logStream.Pull(logHead, logCount, logData);
    }
}
