using UdonSharp;

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
            if (action == 0) trickGame.OnCardClicked(value);
            else if (action == 1) trickGame.SelectRule(value);
            else if (action == 2) trickGame.ConfirmMarkedCards();
            else if (action == 3) trickGame.JoinGame(value);
            else trickGame.SetupGame();
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
}
