using UdonSharp;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class BoardState : UdonSharpBehaviour
{
    [UdonSynced] public byte[] cells = new byte[NetConst.GridWidth * NetConst.GridHeight];
    public byte[] blocks = new byte[] { NetConst.ColorRed, NetConst.ColorBlue, NetConst.ColorYellow, 8, 8 };
    [UdonSynced] public byte phase;
    [UdonSynced] public byte roundIndex;
    [UdonSynced] public byte trickIndex;
    [UdonSynced] public byte currentPlayerSeat;
    [UdonSynced] public byte warningCount;
    [UdonSynced] public byte syncState;
    [UdonSynced] public byte selectedCell;
    [UdonSynced] public byte selectedTrickCard;
    [UdonSynced] public byte[] scores = new byte[4];
    [UdonSynced] public byte[] takenTricks = new byte[4];
    [UdonSynced] public byte[] trickSeats = new byte[4];
    [UdonSynced] public byte[] trickCards = new byte[4];
    [UdonSynced] public byte[] selectedRules = new byte[4];
    [UdonSynced] public byte[] ruleHandCounts = new byte[4];

    public const byte PhaseWaiting = 0;
    public const byte PhaseRuleSelect = 1;
    public const byte PhasePlayCard = 2;
    public const byte PhaseResolveTrick = 3;
    public const byte PhaseScore = 4;
    public const byte PhaseRoundEnd = 5;
    public const byte PhaseWarning = 6;

    public uint Bake(uint seed)
    {
        uint s = seed;
        if (s == 0) s = 1;
        int size = cells.Length;
        uint sizeU = (uint)size;
        for (int i = 0; i < size; i++) cells[i] = 0;
        int blockCount = blocks.Length;
        for (int i = 0; i < blockCount; i++)
        {
            for (int t = 0; t < 160; t++)
            {
                s = s * 1664525u + 1013904223u;
                int index = (int)(s - s / sizeU * sizeU);
                if (cells[index] != 0) continue;
                cells[index] = blocks[i];
                break;
            }
        }
        uint hash = 0;
        for (int i = 0; i < size; i++) hash = hash * 16777619u + cells[i];
        return hash;
    }

    public bool Matches(byte[] data)
    {
        int size = cells.Length;
        if (data.Length != size) return false;
        for (int i = 0; i < size; i++)
        {
            if (cells[i] != data[i]) return false;
        }
        return true;
    }

    public string PhaseLabel()
    {
        if (phase == PhaseRuleSelect) return "RULE SELECT";
        if (phase == PhasePlayCard) return "PLAY CARD";
        if (phase == PhaseResolveTrick) return "RESOLVE TRICK";
        if (phase == PhaseScore) return "SCORE";
        if (phase == PhaseRoundEnd) return "ROUND END";
        if (phase == PhaseWarning) return "WARNING";
        return "WAITING";
    }

    public string SyncLabel()
    {
        if (syncState == 1) return "OWNER";
        if (syncState == 2) return "REMOTE";
        if (syncState == 3) return "WARN";
        return "OK";
    }
}
