
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.Components;

namespace Vowgan
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class ResetChessGame : UdonSharpBehaviour
    {
        public VRCObjectSync[] Pieces;
        private Vector3[] positions;
        private Quaternion[] rotations;

        private void Start()
        {
            Initialize();
        }

        public void Initialize()
        {
            positions = new Vector3[Pieces.Length];
            rotations = new Quaternion[Pieces.Length];
            for (int i = 0; i < Pieces.Length; i++)
            {
                positions[i] = Pieces[i].transform.position;
                rotations[i] = Pieces[i].transform.rotation;
            }
        }

        public override void Interact()
        {
            for (int i = 0; i < Pieces.Length; i++)
            {
                Networking.SetOwner(Networking.LocalPlayer, Pieces[i].gameObject);
                Pieces[i].transform.SetPositionAndRotation(positions[i], rotations[i]);
                Pieces[i].FlagDiscontinuity();
            }
        }
    }
}
