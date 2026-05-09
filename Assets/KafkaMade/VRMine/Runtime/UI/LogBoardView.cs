using UnityEngine;
using UnityEngine.UI;

public sealed class LogBoardView : MonoBehaviour
{
    public LogStream stream;
    public LogBoard board;
    public Text titleText;
    public Text footerText;
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
        if (board != null && stream != null)
        {
            board.Render(stream);
        }
    }

    public void Refresh()
    {
        if (panelImage != null) panelImage.color = new Color(0.07f, 0.10f, 0.15f, 0.94f);
        if (titleText != null) titleText.text = "LOG";
        if (footerText != null) footerText.text = Footer();
        if (titleText != null) titleText.color = new Color(0.55f, 0.92f, 1f, 1f);
        if (footerText != null) footerText.color = new Color(0.90f, 0.94f, 0.98f, 1f);
    }

    string Footer()
    {
        if (stream == null) return "";
        return "EVENTS " + stream.count;
    }
}
