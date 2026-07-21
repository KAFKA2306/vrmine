using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public sealed class PhysicalToken : UdonSharpBehaviour
{
    public GameController controller;
    public int tokenValue = 1;

    public Vector3 snapScale = new Vector3(0.05f, 0.01f, 0.05f);

    public override void OnPickup()
    {
        if (controller != null) Networking.SetOwner(Networking.LocalPlayer, controller.gameObject);
    }

    public override void OnDrop()
    {
        Vector3 pos = transform.position;
        pos.x = Mathf.Round(pos.x / snapScale.x) * snapScale.x;
        pos.y = Mathf.Round(pos.y / snapScale.y) * snapScale.y;
        pos.z = Mathf.Round(pos.z / snapScale.z) * snapScale.z;
        transform.position = pos;
    }

    public override void OnPickupUseDown()
    {
        if (controller == null) return;
        // Use the token for something, e.g., adding to score
        Debug.Log("Token Used");
    }
}
