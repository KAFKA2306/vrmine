using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public sealed class BoardView : UdonSharpBehaviour
{
    public BoardState state;
    public GameController controller;
    
    [Header("Sub Views")]
    public CardView[] handCards;
    public CardView[] trickCards;
    public RuleView ruleView;
    public ScorePanelView scoreView;
    public Text phaseLabel;
    public Renderer boardRenderer;

    public void Render()
    {
        if (state == null) return;

        for (int s = 0; s < NetConst.MaxPlayers; s++)
        {
            RenderHand(s);
        }
        RenderTrick();
        
        if (ruleView != null) ruleView.Refresh();
        if (scoreView != null) scoreView.Refresh();
        if (phaseLabel != null) phaseLabel.text = state.PhaseLabel();
    }

    private void RenderHand(int seat)
    {
        if (handCards == null) return;
        int offset = seat * NetConst.MaxHandSize;
        int limit = Mathf.Min(handCards.Length, NetConst.MaxHandSize);
        bool isLocal = (seat == controller.localPlayerSeat);

        for (int i = 0; i < limit; i++)
        {
            int viewIdx = seat * NetConst.MaxHandSize + i;
            if (viewIdx >= handCards.Length || handCards[viewIdx] == null) continue;
            
            byte packed = state.playerHands[offset + i];
            CardView cv = handCards[viewIdx];
            cv.controller = controller;
            cv.cardIndex = i;
            cv.isPlayed = false;
            cv.isFaceDown = !isLocal;
            cv.Refresh(packed);
        }
    }

    private void RenderTrick()
    {
        if (trickCards == null) return;
        int limit = Mathf.Min(trickCards.Length, state.trickCards.Length);
        for (int i = 0; i < limit; i++)
        {
            if (trickCards[i] == null) continue;
            byte packed = state.trickCards[i];
            trickCards[i].isPlayed = true;
            trickCards[i].Refresh(packed);
        }
    }
}
