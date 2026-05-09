using UnityEngine;
using UnityEngine.UI;

public sealed class BoardView : MonoBehaviour
{
    public BoardState state;
    public GameController controller;
    public Renderer boardRenderer;
    public Transform cellRoot;
    public Text statusText;
    public Renderer[] cellMarkers = new Renderer[0];
    public Renderer[] blockMarkers = new Renderer[0];
    public Renderer[] trickMarkers = new Renderer[0];

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
        int cellCount = cellMarkers.Length;
        int boardCount = state.cells.Length;
        if (cellCount > boardCount) cellCount = boardCount;
        for (int i = 0; i < cellCount; i++)
        {
            Renderer marker = cellMarkers[i];
            if (marker == null) continue;
            byte value = state.cells[i];
            marker.gameObject.SetActive(true);
            Vector3 scale = Vector3.one;
            if (value == 0) scale = new Vector3(0.9f, 0.9f, 0.9f);
            if (state.selectedCell == i) scale = new Vector3(1.15f, 1.15f, 1.15f);
            marker.transform.localScale = scale;
            if (marker.sharedMaterial != null) marker.sharedMaterial.color = CellColor(value, state.selectedCell == i);
        }
        int blockCount = blockMarkers.Length;
        int tokenCount = state.blocks.Length;
        if (blockCount > tokenCount) blockCount = tokenCount;
        for (int i = 0; i < blockCount; i++)
        {
            Renderer marker = blockMarkers[i];
            if (marker == null) continue;
            bool active = i < tokenCount;
            marker.gameObject.SetActive(active);
            if (active && marker.sharedMaterial != null) marker.sharedMaterial.color = CellColor(state.blocks[i], false);
        }
        int trickCount = trickMarkers.Length;
        if (trickCount > state.trickCards.Length) trickCount = state.trickCards.Length;
        for (int i = 0; i < trickCount; i++)
        {
            Renderer marker = trickMarkers[i];
            if (marker == null) continue;
            bool active = state.trickCards[i] != 0 || state.trickSeats[i] != 0;
            marker.gameObject.SetActive(active);
        }
        if (statusText != null)
        {
            statusText.text = "BOARD " + state.PhaseLabel() + " | CELL " + state.selectedCell + " | HASH " + boardHash();
            statusText.color = new Color(0.55f, 0.92f, 1f, 1f);
        }
        if (boardRenderer != null)
        {
            boardRenderer.enabled = true;
            if (boardRenderer.sharedMaterial != null) boardRenderer.sharedMaterial.color = new Color(0.10f, 0.12f, 0.17f, 1f);
        }
    }

    string boardHash()
    {
        if (controller == null) return "--";
        return controller.boardHash.ToString();
    }

    Color CellColor(byte value, bool selected)
    {
        if (value == NetConst.ColorRed) return selected ? new Color(1f, 0.55f, 0.62f, 1f) : new Color(0.74f, 0.30f, 0.38f, 1f);
        if (value == NetConst.ColorBlue) return selected ? new Color(0.60f, 0.88f, 1f, 1f) : new Color(0.28f, 0.52f, 0.82f, 1f);
        if (value == NetConst.ColorYellow) return selected ? new Color(0.98f, 0.94f, 0.60f, 1f) : new Color(0.72f, 0.64f, 0.28f, 1f);
        if (value == 8) return new Color(0.38f, 0.35f, 0.44f, 1f);
        return new Color(0.18f, 0.20f, 0.24f, 1f);
    }
}
