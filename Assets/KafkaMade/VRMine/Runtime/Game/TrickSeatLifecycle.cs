using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TrickSeatLifecycle : UdonSharpBehaviour
{
    public GameController game;

    void Start()
    {
        EnsureReference();
    }

    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        EnsureReference();
        VRCPlayerApi local = Networking.LocalPlayer;
        if (game == null || game.board == null || player == null || local == null || !local.isMaster) return;

        bool changed = false;
        for (int seat = 0; seat < game.board.occupiedPlayerIds.Length; seat++)
        {
            if (game.board.occupiedPlayerIds[seat] != player.playerId) continue;
            game.board.occupiedPlayerIds[seat] = 0;
            changed = true;
        }
        if (!changed) return;

        if (!Networking.IsOwner(game.gameObject)) Networking.SetOwner(local, game.gameObject);
        if (!Networking.IsOwner(game.board.gameObject)) Networking.SetOwner(local, game.board.gameObject);
        game.board.RequestSerialization();
        game.RequestSerialization();
    }

    void EnsureReference()
    {
        if (game != null) return;
        GameObject target = GameObject.Find("TrickMeisterGame");
        if (target != null) game = target.GetComponent<GameController>();
    }
}