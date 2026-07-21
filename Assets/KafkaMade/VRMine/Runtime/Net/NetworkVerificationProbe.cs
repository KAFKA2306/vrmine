using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class NetworkVerificationProbe : UdonSharpBehaviour
{
    [UdonSynced] public int sequence;
    [UdonSynced] public byte phase;
    [UdonSynced] public int firstPlayerId;
    [UdonSynced] public int secondPlayerId;
    [UdonSynced] public int publishedOwnerId;
    bool transferRequested;

    void Start()
    {
        SendCustomEventDelayedSeconds(nameof(BeginProbe), 2f);
        LogMarker("READY");
    }

    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        if (Networking.IsOwner(gameObject)) SendCustomEventDelayedSeconds(nameof(BeginProbe), 1f);
    }

    public void BeginProbe()
    {
        if (!Networking.IsOwner(gameObject) || phase != 0) return;
        int playerCount = VRCPlayerApi.GetPlayerCount();
        if (playerCount < 2)
        {
            LogMarker("WAIT_SECOND_CLIENT");
            SendCustomEventDelayedSeconds(nameof(BeginProbe), 2f);
            return;
        }

        VRCPlayerApi[] players = new VRCPlayerApi[80];
        VRCPlayerApi.GetPlayers(players);
        VRCPlayerApi owner = Networking.GetOwner(gameObject);
        if (owner == null) return;
        firstPlayerId = owner.playerId;
        secondPlayerId = 0;
        for (int i = 0; i < players.Length; i++)
        {
            VRCPlayerApi player = players[i];
            if (player == null || player.playerId == firstPlayerId) continue;
            secondPlayerId = player.playerId;
            break;
        }
        if (secondPlayerId == 0)
        {
            SendCustomEventDelayedSeconds(nameof(BeginProbe), 2f);
            return;
        }

        sequence++;
        phase = 1;
        publishedOwnerId = firstPlayerId;
        RequestSerialization();
        LogMarker("PUBLISH_BASELINE");
        SendCustomEventDelayedSeconds(nameof(TransferProbeOwnership), 4f);
    }

    public void TransferProbeOwnership()
    {
        if (!Networking.IsOwner(gameObject) || transferRequested || phase != 1) return;
        VRCPlayerApi nextOwner = VRCPlayerApi.GetPlayerById(secondPlayerId);
        if (nextOwner == null)
        {
            LogMarker("TRANSFER_TARGET_MISSING");
            return;
        }
        transferRequested = true;
        LogMarker("TRANSFER_REQUEST");
        Networking.SetOwner(nextOwner, gameObject);
    }

    public override void OnDeserialization()
    {
        LogMarker(phase == 1 ? "OBSERVE_BASELINE" : "OBSERVE_REPUBLISH");
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local != null && local.playerId == secondPlayerId && phase >= 1) LogMarker("RESTORE_OR_LATE_JOIN");
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        LogMarker("OWNERSHIP_TRANSFERRED");
        if (player == null || !player.isLocal || phase != 1) return;
        sequence++;
        phase = 2;
        publishedOwnerId = player.playerId;
        RequestSerialization();
        LogMarker("REPUBLISH_BY_NEW_OWNER");
    }

    void LogMarker(string marker)
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        VRCPlayerApi owner = Networking.GetOwner(gameObject);
        int localId = local == null ? 0 : local.playerId;
        int ownerId = owner == null ? 0 : owner.playerId;
        Debug.Log("[VRMINE_G3] marker=" + marker
            + " local=" + localId
            + " owner=" + ownerId
            + " sequence=" + sequence
            + " phase=" + phase
            + " first=" + firstPlayerId
            + " second=" + secondPlayerId);
    }
}