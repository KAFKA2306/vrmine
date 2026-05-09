using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace BoardGameLab.Runtime.Net
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class BGL_SyncManager : UdonSharpBehaviour
    {
        [UdonSynced, FieldChangeCallback("Score")]
        private int _score;

        public BGL_SyncVisual VisualTarget;

        public int Score
        {
            get => _score;
            set
            {
                _score = value;
                if (VisualTarget != null) VisualTarget._OnScoreChanged(_score);
            }
        }

        public void _RequestPulse()
        {
            if (!Networking.IsOwner(gameObject)) return;

            Score++;
            RequestSerialization();
            
            if (VisualTarget != null) VisualTarget._OnPulseEffect();
        }
    }
}
