using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class DeclareButton : UdonSharpBehaviour
{
    public GameController controller;

    public override void Interact()
    {
        if (controller != null)
        {
            controller.OnDeclare();
        }
    }
}
