using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace BoardGameLab.Runtime.Net
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class BGL_BugDismissProxy : UdonSharpBehaviour
    {
        public BGL_SyncVisual Visual;

        public override void Interact()
        {
            if (Visual != null) Visual._DismissBug();
        }
    }
}
