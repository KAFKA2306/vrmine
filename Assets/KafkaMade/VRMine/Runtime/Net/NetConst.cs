using UdonSharp;

public class NetConst : UdonSharpBehaviour
{
    // Board Game Constants (Stich-Meister)
    public const int MaxPlayers = 4;
    public const int MaxHandSize = 15;
    public const int SuitFan = 0;
    public const int SuitCoin = 1;
    public const int SuitKoi = 2;
    public const int SuitGate = 3;
    
    // Legacy VRMine Constants
    public const byte GridWidth = 10;
    public const byte GridHeight = 8;
    public const byte LogRingSize = 20;
    public const byte EntryCount = 36;
    public const byte ColorRed = 1;
    public const byte ColorBlue = 2;
    public const byte ColorYellow = 4;
    public const byte FlagAbsorb = 1;
    public const byte FlagLoop = 2;
}
