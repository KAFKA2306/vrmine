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

        RenderHands();
        RenderTrick();
        
        if (ruleView != null) ruleView.Refresh();
        if (scoreView != null) scoreView.Refresh();
        if (phaseLabel != null) phaseLabel.text = state.PhaseLabel();
    }

    private void RenderHands()
    {
        if (handCards == null) return;
        int offset = 0;
        int limit = Mathf.Min(handCards.Length, 4); 
        for (int i = 0; i < limit; i++)
        {
            if (handCards[i] == null) continue;
            byte packed = state.playerHands[offset + i];
            handCards[i].cardIndex = i;
            handCards[i].isPlayed = false;
            handCards[i].Refresh(packed);
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
