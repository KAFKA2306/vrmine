using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public sealed class SeatButton : UdonSharpBehaviour
{
    public GameController controller;
    public int seatIndex;

    public override void Interact()
    {
        if (controller == null) return;
        controller.JoinGame(seatIndex);
    }
}
