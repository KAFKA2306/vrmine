using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public sealed class RuleView : UdonSharpBehaviour
{
    public BoardState state;
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
        if (state == null) return;
        if (panelImage != null) panelImage.color = new Color(0.08f, 0.09f, 0.14f, 0.92f);
        if (titleText != null) titleText.text = "CURRENT RULE";
        if (bodyText != null) bodyText.text = BodyText();
        if (titleText != null) titleText.color = new Color(0.98f, 0.94f, 0.60f, 1f);
        if (bodyText != null) bodyText.color = new Color(0.90f, 0.94f, 0.98f, 1f);
    }

    string BodyText()
    {
        string body = "";
        int total = state.selectedRules.Length;
        for (int i = 0; i < total; i++)
        {
            if (i > 0) body += "\n";
            byte value = state.selectedRules[i];
            body += value == 0 ? "HIDDEN" : "RULE " + (i + 1) + " / " + value;
        }
        return body;
    }
}
