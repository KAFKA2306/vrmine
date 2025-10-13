using UnityEngine;

[ExecuteAlways]
public class RoomLayout : MonoBehaviour
{
    public Transform board;
    public Transform playerMarker;
    public Transform logBoard;
    public Transform declarePanel;
    public Transform declareButton;
    public float boardScale = 0.01f;
    public float playerOffset = 1.5f;
    public float logOffset = 0.6f;
    public float logHeight = 1.4f;
    public float buttonOffset = 0.8f;
    public float buttonHeight = 1.1f;

    void OnEnable()
    {
        Apply();
    }

    void Update()
    {
        Apply();
    }

    void Apply()
    {
        if (!board) return;
        board.localScale = Vector3.one * boardScale;
        board.rotation = Quaternion.identity;
        board.position = Vector3.zero;
        Bounds first = BoundsFor(board);
        board.position = -new Vector3(first.center.x, first.min.y, first.center.z);
        Bounds bounds = BoundsFor(board);
        Vector3 right;
        Vector3 forward;
        float longExtent;
        float shortExtent;
        if (bounds.extents.x >= bounds.extents.z)
        {
            right = Vector3.right;
            forward = Vector3.forward;
            longExtent = bounds.extents.x;
            shortExtent = bounds.extents.z;
        }
        else
        {
            right = Vector3.forward;
            forward = Vector3.right;
            longExtent = bounds.extents.z;
            shortExtent = bounds.extents.x;
        }
        Vector3 center = new Vector3(0, bounds.center.y, 0);
        if (playerMarker)
        {
            Vector3 pos = center - forward * (shortExtent + playerOffset);
            pos.y = 0;
            playerMarker.position = pos;
            playerMarker.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
        if (logBoard)
        {
            Vector3 pos = center + forward * (shortExtent + logOffset);
            pos.y = logHeight;
            logBoard.position = pos;
            logBoard.rotation = Quaternion.LookRotation(-forward, Vector3.up);
        }
        Vector3 button = center + right * (longExtent + buttonOffset);
        button.y = buttonHeight;
        Quaternion facing = Quaternion.LookRotation(-right, Vector3.up);
        if (declarePanel)
        {
            declarePanel.position = button;
            declarePanel.rotation = facing;
        }
        if (declareButton)
        {
            declareButton.position = button;
            declareButton.rotation = facing;
        }
    }

    Bounds BoundsFor(Transform target)
    {
        var renderers = target.GetComponentsInChildren<Renderer>();
        int count = renderers.Length;
        if (count == 0) return new Bounds(target.position, Vector3.zero);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < count; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }
}
