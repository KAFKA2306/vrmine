using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace BoardGameLab.Runtime.Net
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class BGL_SyncProxy : UdonSharpBehaviour
    {
        public BGL_SyncManager Manager;
        public BGL_SyncVisual Visual;

        public override void Interact()
        {
            if (Manager != null)
            {
                Manager.SendCustomNetworkEvent(NetworkEventTarget.Owner, "_RequestPulse");
            }
        }
    }
}
