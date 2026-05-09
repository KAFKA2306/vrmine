using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public sealed class RuleView : UdonSharpBehaviour
{
    public BoardState state;
    public Text ruleText;

    void Start()
    {
        Refresh();
    }

    void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (state == null) return;
        if (ruleText != null) ruleText.text = state.RuleLabel();
    }
}

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public sealed class PhaseView : UdonSharpBehaviour
{
    public BoardState state;
    public Text phaseText;
    public Text bodyText;
    {
        Refresh();
    }

    public void Refresh()
    {
        if (state == null) return;
        if (panelImage != null) panelImage.color = new Color(0.09f, 0.11f, 0.16f, 0.92f);
        if (phaseText != null) phaseText.text = state.PhaseLabel();
        if (playerText != null) playerText.text = "PLAYER " + state.currentPlayerSeat;
        if (roundText != null) roundText.text = "ROUND " + state.roundIndex;
        if (trickText != null) trickText.text = "TRICK " + state.trickIndex;
        if (warningText != null) warningText.text = "WARN " + state.warningCount;
        if (bodyText != null) bodyText.text = "PLAYER " + state.currentPlayerSeat + "\nROUND " + state.roundIndex + "\nTRICK " + state.trickIndex + "\nWARN " + state.warningCount;
        if (phaseText != null) phaseText.color = new Color(0.55f, 0.92f, 1f, 1f);
        if (playerText != null) playerText.color = new Color(0.90f, 0.94f, 0.98f, 1f);
        if (roundText != null) roundText.color = new Color(0.90f, 0.94f, 0.98f, 1f);
        if (trickText != null) trickText.color = new Color(0.90f, 0.94f, 0.98f, 1f);
        if (warningText != null) warningText.color = new Color(0.86f, 0.75f, 0.94f, 1f);
        if (bodyText != null) bodyText.color = new Color(0.90f, 0.94f, 0.98f, 1f);
    }
}
