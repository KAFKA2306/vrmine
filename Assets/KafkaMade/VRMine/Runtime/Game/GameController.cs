using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class GameController : UdonSharpBehaviour
{
    public BoardState board;
    public BoardView view;
    
    [Header("Runtime State")]
    public int localPlayerSeat = 0;

    void Start()
    {
        if (Networking.IsOwner(gameObject))
        {
            SetupGame();
        }
        Render();
    }

    public override void OnDeserialization()
    {
        Render();
    }

    void SetupGame()
    {
        board.phase = BoardState.PhasePlayCard;
        board.currentPlayerSeat = 0;
        
        // Initial setup cards as requested:
        // Fan 1, Coin 6, Koi 10, Gate 15
        byte[] setup = {
            (byte)((NetConst.SuitFan << 4) | 1),
            (byte)((NetConst.SuitCoin << 4) | 6),
            (byte)((NetConst.SuitKoi << 4) | 10),
            (byte)((NetConst.SuitGate << 4) | 15)
        };
        
        for (int i = 0; i < setup.Length; i++)
        {
            board.playerHands[i] = setup[i];
        }
        
        board.RequestSerialization();
    }

    public void OnCardClicked(int cardIndex)
    {
        if (board.phase != BoardState.PhasePlayCard) return;
        if (board.currentPlayerSeat != localPlayerSeat) return;
        
        // Request ownership to modify state
        if (!Networking.IsOwner(gameObject))
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            Networking.SetOwner(Networking.LocalPlayer, board.gameObject);
        }
        
        TryPlayCard(localPlayerSeat, cardIndex);
    }

    public void TryPlayCard(int playerSeat, int handIndex)
    {
        int offset = playerSeat * NetConst.MaxHandSize;
        byte card = board.playerHands[offset + handIndex];
        if (card == 0) return;

        // Move to trick
        board.trickCards[playerSeat] = card;
        board.playerHands[offset + handIndex] = 0;
        
        // Simple turn advance for MVP
        board.currentPlayerSeat = (byte)((board.currentPlayerSeat + 1) % NetConst.MaxPlayers);
        
        board.RequestSerialization();
        Render();
    }

    public void Render()
    {
        if (view != null) view.Render();
    }
    
    // Legacy support for wiring
    public void OnDeclare() { }
}
