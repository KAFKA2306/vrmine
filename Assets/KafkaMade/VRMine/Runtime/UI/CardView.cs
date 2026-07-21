using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public sealed class CardView : UdonSharpBehaviour
{
    public Text label;
    public Text subLabel;

    [Header("Physical Layers")]
    public Renderer suitRenderer;
    public Renderer numberRenderer;
    public Renderer backRenderer;

    public GameController controller;
    public int cardIndex; // Index in hand
    public bool isPlayed;
    public bool isFaceDown;

    public override void Interact()
    {
        if (controller != null && !isPlayed)
        {
            controller.OnCardClicked(cardIndex);
        }
    }

    public void Refresh(byte packed)
    {
        if (packed == 0)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        
        if (isFaceDown)
        {
            if (label != null) label.text = "";
            if (subLabel != null) subLabel.text = "";
            if (suitRenderer != null) suitRenderer.gameObject.SetActive(false);
            if (numberRenderer != null) numberRenderer.gameObject.SetActive(false);
            return;
        }

        if (suitRenderer != null) suitRenderer.gameObject.SetActive(true);
        if (numberRenderer != null) numberRenderer.gameObject.SetActive(true);

        // Bit-packed: Suit (Upper 4 bits), Rank (Lower 4 bits)
        int suitIdx = packed >> 4;
        int rank = packed & 0x0F;

        // Update Text (Fallback)
        if (label != null) label.text = rank.ToString();
        if (subLabel != null) subLabel.text = GetSuitName(suitIdx);

        // Update Physical UVs
        SetSuitUV(suitIdx);
        SetNumberUV(rank);
    }

    void SetSuitUV(int suitIdx)
    {
        if (suitRenderer == null) return;
        Vector2 offset = new Vector2((suitIdx % 2) * 0.5f, (suitIdx / 2) * 0.5f);
        suitRenderer.sharedMaterial.mainTextureOffset = offset;
    }

    void SetNumberUV(int rank)
    {
        if (numberRenderer == null) return;
        int idx = rank - 1;
        Vector2 offset = new Vector2((idx % 4) * 0.25f, 1.0f - ((idx / 4) + 1) * 0.25f);
        numberRenderer.sharedMaterial.mainTextureOffset = offset;
    }

    string GetSuitName(int idx)
    {
        if (idx == 0) return "FAN";
        if (idx == 1) return "COIN";
        if (idx == 2) return "KOI";
        if (idx == 3) return "GATE";
        return "RULE";
    }
}
