using UdonSharp;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class BoardState : UdonSharpBehaviour
{
    // --- STICH-MEISTER STATE ---
    
    // Hands: Packed as (suit << 4) | rank. 0 means empty.
    [UdonSynced] public byte[] playerHands = new byte[NetConst.MaxPlayers * NetConst.MaxHandSize];
    
    // Current Trick: Packed values for the cards currently on the table.
    [UdonSynced] public byte[] trickCards = new byte[NetConst.MaxPlayers];
    [UdonSynced] public byte[] trickSeats = new byte[NetConst.MaxPlayers]; // Which seat played which card
    
    // Rules: Indices into the rule card list
    [UdonSynced] public byte trumpRule;
    [UdonSynced] public byte basicRule;
    [UdonSynced] public byte scoringRule;
    
    // Legacy / Extended Fields for Rules
    [UdonSynced] public byte[] selectedRules = new byte[4];
    [UdonSynced] public byte[] ruleHandCounts = new byte[4];
    
    // Game Metadata
    [UdonSynced] public byte phase;
    [UdonSynced] public byte roundIndex;
    [UdonSynced] public byte currentPlayerSeat;
    [UdonSynced] public byte dealerSeat;
    [UdonSynced] public byte trickIndex;
    [UdonSynced] public byte[] scores = new byte[NetConst.MaxPlayers];
    [UdonSynced] public byte[] takenTricks = new byte[4];
    [UdonSynced] public byte warningCount;
    [UdonSynced] public byte syncState;
    [UdonSynced] public byte selectedCell;

    // Legacy Grid Fields (for WaveSimulator compatibility)
    [UdonSynced] public byte[] cells = new byte[NetConst.GridWidth * NetConst.GridHeight];
    public byte[] blocks = new byte[] { NetConst.ColorRed, NetConst.ColorBlue, NetConst.ColorYellow, 8, 8 };

    public const byte PhaseSetup = 0;
    public const byte PhaseRuleSelect = 1;
    public const byte PhasePlayCard = 2;
    public const byte PhaseResolveTrick = 3;
    public const byte PhaseScore = 4;
    public const byte PhaseWarning = 6;

    public string PhaseLabel()
    {
        switch (phase)
        {
            case PhaseRuleSelect: return "RULE SELECTION";
            case PhasePlayCard: return "TRICK TAKING";
            case PhaseResolveTrick: return "RESOLVING";
            case PhaseScore: return "SCORING";
            case PhaseWarning: return "WARNING";
            default: return "WAITING";
        }
    }

    public string SuitName(int suit)
    {
        switch (suit)
        {
            case NetConst.SuitFan: return "FAN";
            case NetConst.SuitCoin: return "COIN";
            case NetConst.SuitKoi: return "KOI";
            case NetConst.SuitGate: return "GATE";
            default: return "NONE";
        }
    }

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
        if (data == null || data.Length != size) return false;
        for (int i = 0; i < size; i++)
        {
            if (cells[i] != data[i]) return false;
        }
        return true;
    }

    public string SyncLabel()
    {
        if (syncState == 1) return "OWNER";
        if (syncState == 2) return "REMOTE";
        if (syncState == 3) return "WARN";
        return "OK";
    }

    public string RuleLabel()
    {
        if (selectedRules == null || selectedRules.Length == 0) return "NONE";
        string label = "";
        for (int i = 0; i < selectedRules.Length; i++)
        {
            if (selectedRules[i] == 0) continue;
            label += "[" + selectedRules[i] + "]";
        }
        return label == "" ? "HIDDEN" : label;
    }
}
