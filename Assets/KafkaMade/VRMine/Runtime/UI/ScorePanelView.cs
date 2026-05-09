using UnityEngine;
using UnityEngine.UI;

public sealed class ScorePanelView : MonoBehaviour
{
    public BoardState state;
    public Text phaseText;
    public Text playerText;
    public Text roundText;
    public Text trickText;
    public Text scoreText;
    public Text warningText;
    public Text syncText;
    public Text bodyText;
    public Image panelImage;

    void Awake()
    {
        if (panelImage == null) panelImage = GetComponent<Image>();
    }

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
        if (panelImage != null) panelImage.color = new Color(0.10f, 0.08f, 0.16f, 0.92f);
        if (phaseText != null) phaseText.text = state.PhaseLabel();
        if (playerText != null) playerText.text = "PLAYER " + state.currentPlayerSeat;
        if (roundText != null) roundText.text = "ROUND " + state.roundIndex;
        if (trickText != null) trickText.text = "TRICK " + state.trickIndex;
        if (scoreText != null) scoreText.text = "SCORE " + ScoreText();
        if (warningText != null) warningText.text = "WARN " + state.warningCount;
        if (syncText != null) syncText.text = "SYNC " + state.SyncLabel();
        if (bodyText != null) bodyText.text = "PHASE " + state.PhaseLabel() + "\nPLAYER " + state.currentPlayerSeat + "\nROUND " + state.roundIndex + "\nTRICK " + state.trickIndex + "\nSCORE " + ScoreText() + "\nWARN " + state.warningCount + "\nSYNC " + state.SyncLabel();
        if (phaseText != null) phaseText.color = new Color(0.82f, 0.76f, 1f, 1f);
        if (playerText != null) playerText.color = new Color(0.90f, 0.94f, 0.98f, 1f);
        if (roundText != null) roundText.color = new Color(0.90f, 0.94f, 0.98f, 1f);
        if (trickText != null) trickText.color = new Color(0.90f, 0.94f, 0.98f, 1f);
        if (scoreText != null) scoreText.color = new Color(0.55f, 0.92f, 1f, 1f);
        if (warningText != null) warningText.color = new Color(0.88f, 0.55f, 0.76f, 1f);
        if (syncText != null) syncText.color = new Color(0.90f, 0.94f, 0.98f, 1f);
        if (bodyText != null) bodyText.color = new Color(0.90f, 0.94f, 0.98f, 1f);
    }

    string ScoreText()
    {
        return state.scores[0] + " : " + state.scores[1];
    }
}
