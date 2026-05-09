using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public sealed class ScorePanelView : UdonSharpBehaviour
{
    public BoardState state;
    public Text scoreText;

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
        if (scoreText != null) scoreText.text = "SCORE\n" + state.scores[0] + " : " + state.scores[1];
    }
}
