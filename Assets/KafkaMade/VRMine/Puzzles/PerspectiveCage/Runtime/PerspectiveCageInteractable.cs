using UdonSharp;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PerspectiveCageInteractable : UdonSharpBehaviour
{
    public PerspectiveCageController controller;
    public int puzzleIndex;
    public int action;
    public int value;

    public override void Interact()
    {
        if (controller != null) controller.SubmitInteraction(puzzleIndex, action, value);
    }
}
