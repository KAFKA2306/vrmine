using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BoardGameAction : UdonSharpBehaviour
{
    public GameController trickGame;
    public OrapaMineGame orapaGame;
    public ChessGame chessGame;
    public int game;
    public int action;
    public int value;

    public override void Interact()
    {
        if (game == 0)
        {
            HandleTrick();
            return;
        }

        if (game == 1)
        {
            HandleOrapa();
            return;
        }

        HandleChess();
    }

    void HandleTrick()
    {
        if (trickGame == null) return;

        if (action == 3)
        {
            JoinTrickSeat(value);
            return;
        }

        if (action == 4)
        {
            if (CanReset()) trickGame.SetupGame();
            return;
        }

        if (!HasTrickSeat()) return;
        if (action == 0) trickGame.OnCardClicked(value);
        else if (action == 1) trickGame.SelectRule(value);
        else if (action == 2) trickGame.ConfirmMarkedCards();
    }

    void HandleOrapa()
    {
        if (orapaGame == null) return;

        if (action == 1)
        {
            JoinOrapaSeat(value);
            return;
        }

        if (action == 2)
        {
            if (CanReset()) orapaGame.ResetGame();
            return;
        }

        // The generated scene from the previous schema did not contain Orapa seat buttons.
        // Auto-claiming the first free seat keeps that scene playable while the editor builder
        // upgrades it to the explicit-seat version.
        if (!EnsureOrapaSeat()) return;

        if (action == 0) orapaGame.QueryWave(value);
        else if (action == 3) orapaGame.SubmitGuess();
        else if (action == 4) orapaGame.SelectGuessPiece(value);
        else if (action == 5) orapaGame.MoveGuess(value, 0);
        else if (action == 6) orapaGame.MoveGuess(0, value);
        else orapaGame.RotateGuess();
    }

    void HandleChess()
    {
        if (chessGame == null) return;

        if (action == 1)
        {
            JoinChessSeat(value);
            return;
        }

        if (action == 2)
        {
            if (CanReset()) chessGame.ResetGame();
            return;
        }

        if (!HasChessSeat()) return;
        if (action == 0) chessGame.SelectSquare(value);
        else if (action == 3) chessGame.Resign();
        else if (action == 4) chessGame.SetPromotion(value);
        else if (action == 5) chessGame.ClaimDraw();
    }

    void JoinTrickSeat(int seat)
    {
        VRCPlayerApi player = Networking.LocalPlayer;
        if (player == null || seat < 0 || seat >= trickGame.board.playerCount) return;
        int existing = FindSeat(trickGame.board.occupiedPlayerIds, player.playerId);
        if (existing >= 0 && existing != seat) return;
        trickGame.JoinGame(seat);
    }

    void JoinOrapaSeat(int seat)
    {
        VRCPlayerApi player = Networking.LocalPlayer;
        if (player == null || seat < 0 || seat >= orapaGame.playerCount) return;
        int existing = FindSeat(orapaGame.occupiedPlayerIds, player.playerId);
        if (existing >= 0 && existing != seat) return;
        orapaGame.JoinGame(seat);
    }

    void JoinChessSeat(int seat)
    {
        VRCPlayerApi player = Networking.LocalPlayer;
        if (player == null || seat < 0 || seat > 1) return;
        int existing = FindSeat(chessGame.occupiedPlayerIds, player.playerId);
        if (existing >= 0 && existing != seat) return;
        chessGame.JoinGame(seat);
    }

    bool EnsureOrapaSeat()
    {
        if (HasOrapaSeat()) return true;
        VRCPlayerApi player = Networking.LocalPlayer;
        if (player == null) return false;
        for (int seat = 0; seat < orapaGame.playerCount; seat++)
        {
            if (orapaGame.occupiedPlayerIds[seat] != 0) continue;
            orapaGame.JoinGame(seat);
            return HasOrapaSeat();
        }
        return false;
    }

    bool HasTrickSeat()
    {
        VRCPlayerApi player = Networking.LocalPlayer;
        int seat = trickGame.localPlayerSeat;
        return player != null && seat >= 0 && seat < trickGame.board.playerCount && trickGame.board.occupiedPlayerIds[seat] == player.playerId;
    }

    bool HasOrapaSeat()
    {
        VRCPlayerApi player = Networking.LocalPlayer;
        int seat = orapaGame.localSeat;
        return player != null && seat >= 0 && seat < orapaGame.playerCount && orapaGame.occupiedPlayerIds[seat] == player.playerId;
    }

    bool HasChessSeat()
    {
        VRCPlayerApi player = Networking.LocalPlayer;
        int seat = chessGame.localSeat;
        return player != null && seat >= 0 && seat < 2 && chessGame.occupiedPlayerIds[seat] == player.playerId;
    }

    int FindSeat(int[] seats, int playerId)
    {
        for (int i = 0; i < seats.Length; i++) if (seats[i] == playerId) return i;
        return -1;
    }

    bool CanReset()
    {
        VRCPlayerApi player = Networking.LocalPlayer;
        return player != null && player.isMaster;
    }
}