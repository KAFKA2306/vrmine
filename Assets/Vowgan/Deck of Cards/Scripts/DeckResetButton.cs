
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace Vowgan.DeckOfCards
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class DeckResetButton : UdonSharpBehaviour
    {

        public DeckManager DeckOfCards;
        
        private Animator anim;
        private int hashTrigger;


        private void Start()
        {
            anim = GetComponent<Animator>();
            hashTrigger = Animator.StringToHash("Trigger");
        }

        public override void Interact()
        {
            DeckOfCards.SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(DeckManager._ResetDeck));
            anim.SetTrigger(hashTrigger);
        }
    }
}
