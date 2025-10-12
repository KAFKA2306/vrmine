using UdonSharp;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PlayerClient : UdonSharpBehaviour
{
    [UdonSynced] public int ownerPlayerId;
    [UdonSynced] public int requestSequence;
    [UdonSynced] public byte requestType;
    [UdonSynced] public byte entryId;
    [UdonSynced] public byte[] declaration = new byte[NetConst.GridWidth * NetConst.GridHeight];
    public GameController controller;

    void Start()
    {
        if (Networking.IsOwner(gameObject))
        {
            ownerPlayerId = Networking.LocalPlayer.playerId;
            RequestSerialization();
        }
    }

    public void SubmitWave(byte id)
    {
        entryId = id;
        Touch(1);
    }

    public void SubmitDeclaration(byte[] data)
    {
        int size = declaration.Length;
        int count = data.Length;
        if (count < size) size = count;
        for (int i = 0; i < size; i++) declaration[i] = data[i];
        Touch(2);
    }

    void Touch(byte type)
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        ownerPlayerId = Networking.LocalPlayer.playerId;
        requestType = type;
        requestSequence++;
        RequestSerialization();
        controller.SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(GameController.Pull));
    }
}
