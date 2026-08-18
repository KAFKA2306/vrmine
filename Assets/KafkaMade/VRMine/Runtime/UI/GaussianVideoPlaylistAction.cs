using UdonSharp;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class GaussianVideoPlaylistAction : UdonSharpBehaviour
{
    public GaussianVideoPlaylist playlist;
    public int action;
    public int index;

    public override void Interact()
    {
        if (playlist == null) return;
        if (action == 0) playlist.Select(index);
        else if (action == 1) playlist.Previous();
        else if (action == 2) playlist.Next();
        else playlist.Replay();
    }
}
