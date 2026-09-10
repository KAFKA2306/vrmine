using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BoardGameAction : UdonSharpBehaviour
{
    public GameController trickGame;
    public OrapaMineGame orapaGame;
    public ChessGame chessGame;
    public int game;
    public int action;
    public int value;

    const float ResetConfirmMinDelay = 0.35f;
    const float ResetConfirmTimeout = 5f;
    float resetArmedAt = -1f;

    void Update()
    {
        if (game != 0 || action < 4 || resetArmedAt < 0f) return;
        if (trickGame == null || trickGame.board.phase != BoardState.PhaseComplete || Time.time - resetArmedAt > ResetConfirmTimeout) DisarmReset();
    }

    public override void Interact()
    {
        if (game == 0)
        {
            if (action == 0) trickGame.OnCardClicked(value);
            else if (action == 1) trickGame.SelectRule(value);
            else if (action == 2) trickGame.ConfirmMarkedCards();
            else if (action == 3) trickGame.JoinGame(value);
            else HandleTrickReset();
            return;
        }
        if (game == 1)
        {
            if (action == 0) orapaGame.QueryWave(value);
            else if (action == 1) orapaGame.JoinGame(value);
            else if (action == 2) orapaGame.ResetGame();
            else if (action == 3) orapaGame.SubmitGuess();
            else if (action == 4) orapaGame.SelectGuessPiece(value);
            else if (action == 5) orapaGame.MoveGuess(value, 0);
            else if (action == 6) orapaGame.MoveGuess(0, value);
            else orapaGame.RotateGuess();
            return;
        }
        if (action == 0) chessGame.SelectSquare(value);
        else if (action == 1) chessGame.JoinGame(value);
        else if (action == 2) chessGame.ResetGame();
        else chessGame.Resign();
    }

    void HandleTrickReset()
    {
        if (trickGame == null || trickGame.board.phase != BoardState.PhaseComplete)
        {
            DisarmReset();
            return;
        }

        float now = Time.time;
        if (resetArmedAt < 0f || now - resetArmedAt > ResetConfirmTimeout)
        {
            resetArmedAt = now;
            SetActionLabel("CONFIRM RESET");
            return;
        }
        if (now - resetArmedAt < ResetConfirmMinDelay) return;

        DisarmReset();
        trickGame.SetupGame();
    }

    void DisarmReset()
    {
        if (resetArmedAt < 0f) return;
        resetArmedAt = -1f;
        SetActionLabel("RESET");
    }

    void SetActionLabel(string text)
    {
        Text label = GetComponentInChildren<Text>();
        if (label != null) label.text = text;
    }
}
