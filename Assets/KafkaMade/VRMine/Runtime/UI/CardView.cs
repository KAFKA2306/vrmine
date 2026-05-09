using UnityEngine;
using UnityEngine.UI;

public sealed class CardView : MonoBehaviour
{
    public const byte Hidden = 0;
    public const byte InHand = 1;
    public const byte Playable = 2;
    public const byte Selected = 3;
    public const byte Played = 4;
    public const byte Won = 5;
    public const byte Disabled = 6;

    public MeshRenderer meshRenderer;
    public Text label;
    public Text subLabel;
    public Renderer frameRenderer;
    public Light glowLight;
    public byte viewState;

    public void SetState(byte value, string mainText, string extraText)
    {
        viewState = value;
        if (label != null) label.text = mainText;
        if (subLabel != null) subLabel.text = extraText;
        if (meshRenderer != null) meshRenderer.enabled = value != Hidden;
        if (frameRenderer != null) frameRenderer.enabled = value != Hidden;
        if (glowLight != null) glowLight.enabled = value == Selected || value == Won;
        if (frameRenderer != null)
        {
            Vector3 scale = Vector3.one;
            if (value == Selected) scale = new Vector3(1.08f, 1.08f, 1.08f);
            if (value == Won) scale = new Vector3(1.04f, 1.04f, 1.04f);
            frameRenderer.transform.localScale = scale;
        }
        if (value == Disabled && label != null) label.color = new Color(0.72f, 0.74f, 0.78f, 0.72f);
        else if (label != null) label.color = new Color(0.92f, 0.95f, 1f, 1f);
    }
}
