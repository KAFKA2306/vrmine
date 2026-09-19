using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

public class OwnerSyncedToggle : UdonSharpBehaviour
{
    [UdonSynced] private bool enabledState;

    public override void Interact()
    {
        if (!Networking.IsOwner(gameObject))
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        enabledState = !enabledState;
        RequestSerialization();
        ApplyState();
    }

    public override void OnDeserialization()
    {
        ApplyState();
    }

    private void ApplyState()
    {
        gameObject.SetActive(enabledState);
    }
}
