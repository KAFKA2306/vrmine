using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public sealed class LogBoardView : UdonSharpBehaviour
{
    public LogStream stream;
    public LogBoard board;
    public Text titleText;
    public Text footerText;
    public Image panelImage;
    public Renderer faceRenderer;

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
            UpdateExpression();
        }
    }

    void UpdateExpression()
    {
        if (faceRenderer == null || stream == null) return;
        // Simple expression logic based on event count or last event
        // 0: Normal (Top-Left), 1: Joy (Top-Right), 2: Thinking (Bottom-Left), 3: Warning (Bottom-Right)
        int expressionIndex = 0;
        if (stream.count > 0)
        {
            // Just a demo logic: cycles expressions
            expressionIndex = (stream.count / 5) % 4;
        }

        Vector2 offset = Vector2.zero;
        if (expressionIndex == 1) offset = new Vector2(0.5f, 0.5f);
        else if (expressionIndex == 2) offset = new Vector2(0f, 0f);
        else if (expressionIndex == 3) offset = new Vector2(0.5f, 0f);
        else offset = new Vector2(0f, 0.5f);

        faceRenderer.material.SetTextureOffset("_MainTex", offset);
        faceRenderer.material.SetTextureScale("_MainTex", new Vector2(0.5f, 0.5f));
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
