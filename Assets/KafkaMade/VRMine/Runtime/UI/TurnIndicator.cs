using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public sealed class TurnIndicator : UdonSharpBehaviour
{
    public BoardState state;
    public Transform[] seatMarkers;
    public GameObject indicator;

    void Update()
    {
        if (state == null || indicator == null || seatMarkers == null) return;
        
        int current = state.currentPlayerSeat;
        if (current >= 0 && current < seatMarkers.Length && seatMarkers[current] != null)
        {
            indicator.transform.position = Vector3.Lerp(indicator.transform.position, seatMarkers[current].position, Time.deltaTime * 5f);
            indicator.transform.rotation = Quaternion.Lerp(indicator.transform.rotation, seatMarkers[current].rotation, Time.deltaTime * 5f);
        }
    }
}
