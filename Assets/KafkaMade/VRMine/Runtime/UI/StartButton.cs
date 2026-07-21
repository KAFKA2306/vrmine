using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public sealed class StartButton : UdonSharpBehaviour
{
    public GameController controller;

    public override void Interact()
    {
        if (controller == null) return;
        
        // Only owner can start/reset for simplicity in this MVP
        if (!Networking.IsOwner(controller.gameObject)) Networking.SetOwner(Networking.LocalPlayer, controller.gameObject);
        
        controller.SendCustomEvent("SetupGame");
    }
}
