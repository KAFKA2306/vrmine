using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BoardGameShowcaseView : UdonSharpBehaviour
{
    public GameController trickGame;
    public OrapaMineGame orapaGame;
    public ChessGame chessGame;
    public Text trickStatus;
    public Text[] trickCards = new Text[16];
    public Text[] ruleCards = new Text[3];
    public Text orapaStatus;
    public Text chessStatus;
    public Text[] chessPieces = new Text[64];

    void Update()
    {
        RenderTrick();
        RenderOrapa();
        RenderChess();
    }

    void RenderTrick()
    {
        if (trickGame == null || trickGame.board == null || trickStatus == null) return;
        BoardState state = trickGame.board;
        bool seated = HasSeat(state.occupiedPlayerIds, trickGame.localPlayerSeat);
        trickStatus.text = "ROUND " + (state.roundIndex + 1) + "/" + state.playerCount + "  " + state.PhaseLabel()
            + "\nTURN " + (state.currentPlayerSeat + 1) + "  RULES " + state.RuleLabel()
            + "\nSCORES " + Scores(state.scores, state.playerCount)
            + (seated ? "\nSEAT " + (trickGame.localPlayerSeat + 1) : "\nSELECT A SEAT TO PLAY");

        int offset = seated ? trickGame.localPlayerSeat * NetConst.MaxHandSize : -1;
        for (int i = 0; i < trickCards.Length; i++)
            if (trickCards[i] != null) trickCards[i].text = seated ? CardLabel(state.playerHands[offset + i]) : "";
        for (int i = 0; i < ruleCards.Length; i++)
        {
            if (ruleCards[i] == null) continue;
            byte rule = seated ? state.ruleHands[trickGame.localPlayerSeat * 3 + i] : (byte)0;
            ruleCards[i].text = rule == 0 ? "" : "RULE\n" + rule;
        }
    }

    void RenderOrapa()
    {
        if (orapaGame == null || orapaStatus == null) return;
        bool seated = HasSeat(orapaGame.occupiedPlayerIds, orapaGame.localSeat);
        string log = "TURN " + (orapaGame.currentSeat + 1);
        if (seated) log += "  WRONG GUESSES " + orapaGame.attempts[orapaGame.localSeat] + "/2\nSEAT " + (orapaGame.localSeat + 1);
        else log += "\nSELECT A SEAT OR INTERACT TO AUTO-JOIN";
        int first = Mathf.Max(0, orapaGame.logCount - 6);
        for (int i = first; i < orapaGame.logCount; i++)
            log += "\n" + orapaGame.logEntries[i] + " > " + orapaGame.logExits[i] + "  COLOR " + orapaGame.logColors[i] + "  FLAG " + orapaGame.logFlags[i];
        log += "\nGUESS ";
        for (int i = 0; i < OrapaMineGame.PieceCount; i++) log += i + ":" + orapaGame.guessX[i] + "," + orapaGame.guessY[i] + "," + orapaGame.guessRotation[i] + " ";
        if (orapaGame.winnerPlayerId != 0) log += "\nWINNER " + orapaGame.winnerPlayerId;
        orapaStatus.text = log;
    }

    void RenderChess()
    {
        if (chessGame == null || chessStatus == null) return;
        for (int i = 0; i < chessPieces.Length; i++) if (chessPieces[i] != null) chessPieces[i].text = PieceLabel(chessGame.squares[i]);
        string state = chessGame.result == 0 ? chessGame.sideToMove == 0 ? "WHITE TO MOVE" : "BLACK TO MOVE" : chessGame.result == 1 ? "WHITE WINS" : chessGame.result == 2 ? "BLACK WINS" : "DRAW";
        bool seated = HasSeat(chessGame.occupiedPlayerIds, chessGame.localSeat);
        string promotion = chessGame.promotionChoice == 2 ? "N" : chessGame.promotionChoice == 3 ? "B" : chessGame.promotionChoice == 4 ? "R" : "Q";
        chessStatus.text = state + "  MOVE " + chessGame.fullmoveNumber + "  STATUS " + chessGame.status
            + "\nPROMOTION " + promotion
            + (seated ? "  SEAT " + (chessGame.localSeat == 0 ? "WHITE" : "BLACK") : "  SELECT WHITE OR BLACK");
    }

    bool HasSeat(int[] seats, int seat)
    {
        VRCPlayerApi player = Networking.LocalPlayer;
        return player != null && seat >= 0 && seat < seats.Length && seats[seat] == player.playerId;
    }

    string Scores(int[] values, int count)
    {
        string text = "";
        for (int i = 0; i < count; i++) text += (i == 0 ? "" : " / ") + values[i];
        return text;
    }

    string CardLabel(byte card)
    {
        if (card == 0) return "";
        string suit = card >> 4 == 0 ? "F" : card >> 4 == 1 ? "C" : card >> 4 == 2 ? "K" : "G";
        return suit + (card & 15);
    }

    string PieceLabel(byte piece)
    {
        if (piece == 0) return "";
        int type = piece & 7;
        string label = type == 1 ? "P" : type == 2 ? "N" : type == 3 ? "B" : type == 4 ? "R" : type == 5 ? "Q" : "K";
        return (piece & 8) == 0 ? label : label.ToLower();
    }
}