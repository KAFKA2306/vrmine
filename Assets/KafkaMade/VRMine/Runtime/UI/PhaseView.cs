using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public sealed class PhaseView : UdonSharpBehaviour
{
    public BoardState state;
    public Text phaseText;
    public Text bodyText;

    void Start()
    {
        Refresh();
    }

    void OnEnable()
    {
        Refresh();
    }

    void Update()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (state == null) return;
        if (phaseText != null) phaseText.text = state.PhaseLabel();
        if (bodyText != null) bodyText.text = state.SyncLabel();
    }
}
