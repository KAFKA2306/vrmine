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
    
    // Game Metadata
    [UdonSynced] public byte phase;
    [UdonSynced] public byte currentPlayerSeat;
    [UdonSynced] public byte dealerSeat;
    [UdonSynced] public byte trickIndex;
    [UdonSynced] public byte[] scores = new byte[NetConst.MaxPlayers];

    public const byte PhaseSetup = 0;
    public const byte PhaseRuleSelect = 1;
    public const byte PhasePlayCard = 2;
    public const byte PhaseResolveTrick = 3;
    public const byte PhaseScore = 4;

    public string PhaseLabel()
    {
        switch (phase)
        {
            case PhaseRuleSelect: return "RULE SELECTION";
            case PhasePlayCard: return "TRICK TAKING";
            case PhaseResolveTrick: return "RESOLVING";
            case PhaseScore: return "SCORING";
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
}
