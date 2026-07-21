using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class NetworkVerificationProbe : UdonSharpBehaviour
{
    public GameController trickGame;
    public OrapaMineGame orapaGame;
    public ChessGame chessGame;

    [UdonSynced] public int sequence;
    [UdonSynced] public byte phase;
    [UdonSynced] public int firstPlayerId;
    [UdonSynced] public int secondPlayerId;
    [UdonSynced] public int publishedOwnerId;
    bool transferRequested;
    int checkCount;

    void Start()
    {
        EnsureReferences();
        SendCustomEventDelayedSeconds(nameof(BeginProbe), 2f);
        if (phase > 0) LogMarker("RESTORE_OR_LATE_JOIN");
        LogMarker("READY");
    }

    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        if (Networking.IsOwner(gameObject)) SendCustomEventDelayedSeconds(nameof(BeginProbe), 1f);
    }

    public void BeginProbe()
    {
        EnsureReferences();
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
        PublishGameSentinels(700);
        RequestSerialization();
        LogMarker("PUBLISH_BASELINE");
        checkCount = 0;
        SendCustomEventDelayedSeconds(nameof(CheckGameState), 1f);
        SendCustomEventDelayedSeconds(nameof(TransferProbeOwnership), 5f);
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
        TransferGameOwnership(nextOwner);
        LogMarker("TRANSFER_REQUEST");
        Networking.SetOwner(nextOwner, gameObject);
    }

    public override void OnDeserialization()
    {
        EnsureReferences();
        LogMarker(phase == 1 ? "OBSERVE_BASELINE" : "OBSERVE_REPUBLISH");
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local != null && local.playerId == secondPlayerId && phase >= 1) LogMarker("RESTORE_OR_LATE_JOIN");
        checkCount = 0;
        SendCustomEventDelayedSeconds(nameof(CheckGameState), 0.5f);
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        EnsureReferences();
        LogMarker("OWNERSHIP_TRANSFERRED");
        if (player == null || !player.isLocal || phase != 1) return;
        sequence++;
        phase = 2;
        publishedOwnerId = player.playerId;
        PublishGameSentinels(710);
        RequestSerialization();
        LogMarker("REPUBLISH_BY_NEW_OWNER");
        checkCount = 0;
        SendCustomEventDelayedSeconds(nameof(CheckGameState), 1f);
        SendCustomEventDelayedSeconds(nameof(RestoreGames), 5f);
    }

    public void CheckGameState()
    {
        EnsureReferences();
        int expectedBase = phase == 1 ? 700 : phase == 2 ? 710 : -1;
        if (expectedBase < 0) return;
        if (trickGame != null && trickGame.turnIndex == expectedBase + 1 && trickGame.board != null && trickGame.board.phase == 1)
            LogGameMarker("TRICK", expectedBase + 1);
        if (orapaGame != null && orapaGame.puzzleSeed == (uint)(expectedBase + 2) && orapaGame.currentSeat == 1)
            LogGameMarker("ORAPA", expectedBase + 2);
        if (chessGame != null && chessGame.fullmoveNumber == expectedBase + 3 && chessGame.sideToMove == 8)
            LogGameMarker("CHESS", expectedBase + 3);
        checkCount++;
        if (checkCount < 8) SendCustomEventDelayedSeconds(nameof(CheckGameState), 0.75f);
    }

    public void RestoreGames()
    {
        EnsureReferences();
        if (!Networking.IsOwner(gameObject) || phase != 2) return;
        if (trickGame != null) trickGame.SetupGame();
        if (orapaGame != null) orapaGame.ResetGame();
        if (chessGame != null) chessGame.ResetGame();
        phase = 3;
        sequence++;
        RequestSerialization();
        LogMarker("RESTORED_AFTER_TEST");
    }

    void EnsureReferences()
    {
        GameObject target;
        if (trickGame == null)
        {
            target = GameObject.Find("TrickMeisterGame");
            if (target != null) trickGame = target.GetComponent<GameController>();
        }
        if (orapaGame == null)
        {
            target = GameObject.Find("OrapaMineGame");
            if (target != null) orapaGame = target.GetComponent<OrapaMineGame>();
        }
        if (chessGame == null)
        {
            target = GameObject.Find("ChessGame");
            if (target != null) chessGame = target.GetComponent<ChessGame>();
        }
    }

    void PublishGameSentinels(int baseValue)
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null) return;
        if (trickGame != null)
        {
            Networking.SetOwner(local, trickGame.gameObject);
            if (trickGame.board != null) Networking.SetOwner(local, trickGame.board.gameObject);
            trickGame.turnIndex = baseValue + 1;
            trickGame.RequestSerialization();
            if (trickGame.board != null)
            {
                trickGame.board.phase = 1;
                trickGame.board.RequestSerialization();
            }
        }
        if (orapaGame != null)
        {
            Networking.SetOwner(local, orapaGame.gameObject);
            orapaGame.puzzleSeed = (uint)(baseValue + 2);
            orapaGame.currentSeat = 1;
            orapaGame.RequestSerialization();
        }
        if (chessGame != null)
        {
            Networking.SetOwner(local, chessGame.gameObject);
            chessGame.fullmoveNumber = (ushort)(baseValue + 3);
            chessGame.sideToMove = 8;
            chessGame.RequestSerialization();
        }
    }

    void TransferGameOwnership(VRCPlayerApi player)
    {
        if (trickGame != null)
        {
            Networking.SetOwner(player, trickGame.gameObject);
            if (trickGame.board != null) Networking.SetOwner(player, trickGame.board.gameObject);
        }
        if (orapaGame != null) Networking.SetOwner(player, orapaGame.gameObject);
        if (chessGame != null) Networking.SetOwner(player, chessGame.gameObject);
    }

    void LogGameMarker(string gameName, int value)
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        int localId = local == null ? 0 : local.playerId;
        Debug.Log("[VRMINE_G3_GAME] game=" + gameName + " local=" + localId + " phase=" + phase + " value=" + value);
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