using UnityEngine;
using UnityEngine.UI;

public sealed class WarningPanelView : MonoBehaviour
{
    public BoardState state;
    public LogStream logStream;
    public Text titleText;
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
        if (panelImage != null) panelImage.color = new Color(0.17f, 0.08f, 0.13f, 0.94f);
        if (titleText != null) titleText.text = "WARNING";
        if (bodyText != null) bodyText.text = WarningText();
        if (titleText != null) titleText.color = new Color(0.88f, 0.55f, 0.76f, 1f);
        if (bodyText != null) bodyText.color = new Color(0.98f, 0.94f, 0.96f, 1f);
    }

    string WarningText()
    {
        if (state == null) return "";
        if (state.warningCount == 0) return "NO WARNINGS";
        return "INVALID ACTION x" + state.warningCount;
    }
}
