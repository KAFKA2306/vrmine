using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PerspectiveCageController : UdonSharpBehaviour
{
    public const int ActionInput = 0;
    public const int ActionSocket = 1;
    public const int ActionHint = 2;
    public const int ActionReset = 3;

    [UdonSynced] public int completionMask;
    [UdonSynced] public int p02Step;
    [UdonSynced] public int p03PlacedMask;
    [UdonSynced] public int p05Step;
    [UdonSynced] public int hintPacked;
    [UdonSynced] public int resetGeneration;
    [UdonSynced] public bool cleared;

    public GameObject[] progressionDoors = new GameObject[4];
    public GameObject clearDoor;
    public GameObject clearPresentation;
    public GameObject[] resultPanels = new GameObject[4];
    public GameObject[] hintPanels = new GameObject[15];
    public GameObject[] wrongFeedbacks = new GameObject[5];
    public GameObject[] markerObjects = new GameObject[4];
    public Transform[] markerHomes = new Transform[4];
    public Transform[] socketTargets = new Transform[4];

    int selectedMarker = -1;

    void Start()
    {
        selectedMarker = -1;
        ApplyPresentation();
    }

    public override void OnDeserialization()
    {
        ApplyPresentation();
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        ApplyPresentation();
    }

    public void HandleInteraction(int puzzleIndex, int action, int value)
    {
        if (action == ActionReset)
        {
            ResetWorld();
            return;
        }
        if (puzzleIndex < 0 || puzzleIndex > 4 || cleared) return;
        if (action == ActionHint)
        {
            RaiseHint(puzzleIndex);
            return;
        }
        if (puzzleIndex > 0 && !IsPuzzleComplete(puzzleIndex - 1)) return;
        if (IsPuzzleComplete(puzzleIndex)) return;

        if (puzzleIndex == 0) HandleP01(value);
        else if (puzzleIndex == 1) HandleP02(value);
        else if (puzzleIndex == 2) HandleP03(action, value);
        else if (puzzleIndex == 3) HandleP04(value);
        else HandleP05(value);
    }

    public bool IsPuzzleComplete(int puzzleIndex)
    {
        return puzzleIndex >= 0 && puzzleIndex < 5 && (completionMask & (1 << puzzleIndex)) != 0;
    }

    public int GetHintLevel(int puzzleIndex)
    {
        if (puzzleIndex < 0 || puzzleIndex > 4) return 0;
        return (hintPacked >> (puzzleIndex * 2)) & 3;
    }

    public int VerifyDeterministicLogic()
    {
        int failures = 0;
        if (ExpectedP02(0) != 1 || ExpectedP02(1) != 3 || ExpectedP02(2) != 0 || ExpectedP02(3) != 2) failures++;
        if (ExpectedP05(0) != 2 || ExpectedP05(1) != 3 || ExpectedP05(2) != 4 || ExpectedP05(3) != 1) failures++;
        for (int i = 0; i < 4; i++) if (ExpectedSocket(i) != i) failures++;
        return failures;
    }

    void HandleP01(int value)
    {
        if (value != 3)
        {
            ShowWrong(0);
            return;
        }
        CompletePuzzle(0);
    }

    void HandleP02(int value)
    {
        if (value != ExpectedP02(p02Step))
        {
            OwnState();
            p02Step = 0;
            SyncState();
            ShowWrong(1);
            return;
        }
        OwnState();
        p02Step++;
        if (p02Step >= 4)
        {
            p02Step = 4;
            completionMask |= 1 << 1;
        }
        SyncState();
    }

    void HandleP03(int action, int value)
    {
        if (value < 0 || value > 3) return;
        if (action == ActionInput)
        {
            if ((p03PlacedMask & (1 << value)) != 0) return;
            selectedMarker = value;
            return;
        }
        if (action != ActionSocket || selectedMarker < 0) return;
        if (value != ExpectedSocket(selectedMarker))
        {
            selectedMarker = -1;
            ShowWrong(2);
            return;
        }
        OwnState();
        p03PlacedMask |= 1 << selectedMarker;
        selectedMarker = -1;
        if ((p03PlacedMask & 15) == 15) completionMask |= 1 << 2;
        SyncState();
    }

    void HandleP04(int value)
    {
        if (value != 4)
        {
            ShowWrong(3);
            return;
        }
        CompletePuzzle(3);
    }

    void HandleP05(int value)
    {
        if (value != ExpectedP05(p05Step))
        {
            OwnState();
            p05Step = 0;
            SyncState();
            ShowWrong(4);
            return;
        }
        OwnState();
        p05Step++;
        if (p05Step >= 4)
        {
            p05Step = 4;
            completionMask |= 1 << 4;
            cleared = true;
        }
        SyncState();
    }

    int ExpectedP02(int step)
    {
        if (step == 0) return 1;
        if (step == 1) return 3;
        if (step == 2) return 0;
        if (step == 3) return 2;
        return -1;
    }

    int ExpectedP05(int step)
    {
        if (step == 0) return 2;
        if (step == 1) return 3;
        if (step == 2) return 4;
        if (step == 3) return 1;
        return -1;
    }

    int ExpectedSocket(int marker)
    {
        if (marker >= 0 && marker < 4) return marker;
        return -1;
    }

    void CompletePuzzle(int puzzleIndex)
    {
        OwnState();
        completionMask |= 1 << puzzleIndex;
        SyncState();
    }

    void RaiseHint(int puzzleIndex)
    {
        int current = GetHintLevel(puzzleIndex);
        if (current >= 3) return;
        OwnState();
        int shift = puzzleIndex * 2;
        hintPacked &= ~(3 << shift);
        hintPacked |= (current + 1) << shift;
        SyncState();
    }

    public void ResetWorld()
    {
        OwnState();
        completionMask = 0;
        p02Step = 0;
        p03PlacedMask = 0;
        p05Step = 0;
        hintPacked = 0;
        cleared = false;
        resetGeneration++;
        selectedMarker = -1;
        SyncState();
        ClearWrongFeedback();
    }

    void OwnState()
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local != null && !Networking.IsOwner(gameObject)) Networking.SetOwner(local, gameObject);
    }

    void SyncState()
    {
        RequestSerialization();
        ApplyPresentation();
    }

    public void ApplyPresentation()
    {
        for (int i = 0; i < progressionDoors.Length && i < 4; i++)
            if (progressionDoors[i] != null) progressionDoors[i].SetActive(!IsPuzzleComplete(i));

        if (clearDoor != null) clearDoor.SetActive(!cleared);
        if (clearPresentation != null) clearPresentation.SetActive(cleared);

        for (int i = 0; i < resultPanels.Length && i < 4; i++)
            if (resultPanels[i] != null) resultPanels[i].SetActive(IsPuzzleComplete(i));

        for (int puzzle = 0; puzzle < 5; puzzle++)
        {
            int level = GetHintLevel(puzzle);
            for (int hint = 0; hint < 3; hint++)
            {
                int index = puzzle * 3 + hint;
                if (index < hintPanels.Length && hintPanels[index] != null) hintPanels[index].SetActive(level > hint);
            }
        }

        for (int marker = 0; marker < 4; marker++)
        {
            if (marker >= markerObjects.Length || markerObjects[marker] == null) continue;
            Transform target = null;
            if ((p03PlacedMask & (1 << marker)) != 0 && marker < socketTargets.Length) target = socketTargets[marker];
            else if (marker < markerHomes.Length) target = markerHomes[marker];
            if (target != null) markerObjects[marker].transform.SetPositionAndRotation(target.position, target.rotation);
        }
    }

    void ShowWrong(int puzzleIndex)
    {
        if (puzzleIndex >= 0 && puzzleIndex < wrongFeedbacks.Length && wrongFeedbacks[puzzleIndex] != null)
            wrongFeedbacks[puzzleIndex].SetActive(true);
        SendCustomEventDelayedSeconds(nameof(ClearWrongFeedback), 1.5f);
    }

    public void ClearWrongFeedback()
    {
        for (int i = 0; i < wrongFeedbacks.Length; i++)
            if (wrongFeedbacks[i] != null) wrongFeedbacks[i].SetActive(false);
    }
}
