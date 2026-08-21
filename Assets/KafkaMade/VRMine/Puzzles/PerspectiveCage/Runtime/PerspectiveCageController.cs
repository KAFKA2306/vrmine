using UdonSharp;
using UnityEngine;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PerspectiveCageController : UdonSharpBehaviour
{
    public const int ActionInput = 0;
    public const int ActionSocket = 1;
    public const int ActionHint = 2;
    public const int ActionReset = 3;

    // Immutable world configuration, serialized by PerspectiveCageBuilder from config/perspective-cage.json.
    public int p01Solution;
    public int[] p02Solution = new int[4];
    public int[] p03SocketByMarker = new int[4];
    public int p04Solution;
    public int[] p05Solution = new int[4];

    // Canonical public instance state. Only the current owner mutates these fields.
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

    // Local-only interaction cursor. A selection is not puzzle truth until the owner accepts a socket placement.
    int selectedMarker = -1;
    int observedResetGeneration = -1;

    void Start()
    {
        selectedMarker = -1;
        observedResetGeneration = resetGeneration;
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

    public void SubmitInteraction(int puzzleIndex, int action, int value)
    {
        if (puzzleIndex == 2 && action == ActionInput)
        {
            if (value < 0 || value > 3) return;
            if (!IsPuzzleComplete(1) || IsPuzzleComplete(2)) return;
            if ((p03PlacedMask & (1 << value)) != 0) return;
            selectedMarker = value;
            return;
        }

        int marker = -1;
        if (puzzleIndex == 2 && action == ActionSocket)
        {
            marker = selectedMarker;
            selectedMarker = -1;
            if (marker < 0 || marker > 3) return;
        }

        SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(ApplyInteraction), puzzleIndex, action, value, marker);
    }

    [NetworkCallable(maxEventsPerSecond: 20)]
    public void ApplyInteraction(int puzzleIndex, int action, int value, int marker)
    {
        if (!Networking.IsOwner(gameObject)) return;
        if (action == ActionReset)
        {
            ResetWorldOwner();
            return;
        }
        if (puzzleIndex < 0 || puzzleIndex > 4 || cleared) return;
        if (puzzleIndex > 0 && !IsPuzzleComplete(puzzleIndex - 1)) return;
        if (IsPuzzleComplete(puzzleIndex)) return;
        if (action == ActionHint)
        {
            RaiseHint(puzzleIndex);
            return;
        }
        if (puzzleIndex == 2)
        {
            if (action != ActionSocket) return;
        }
        else if (action != ActionInput) return;

        if (puzzleIndex == 0) HandleP01(value);
        else if (puzzleIndex == 1) HandleP02(value);
        else if (puzzleIndex == 2) HandleP03(value, marker);
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
        if (p01Solution < 0 || p01Solution > 4) failures++;
        if (p04Solution < 0 || p04Solution > 4) failures++;
        if (!ValidFour(p02Solution, 0, 3, true)) failures++;
        if (!ValidFour(p03SocketByMarker, 0, 3, true)) failures++;
        if (!ValidFour(p05Solution, 0, 4, false)) failures++;
        return failures;
    }

    bool ValidFour(int[] values, int min, int max, bool requireUnique)
    {
        if (values == null || values.Length != 4) return false;
        int seen = 0;
        for (int i = 0; i < 4; i++)
        {
            int value = values[i];
            if (value < min || value > max) return false;
            if (requireUnique)
            {
                int bit = 1 << value;
                if ((seen & bit) != 0) return false;
                seen |= bit;
            }
        }
        return true;
    }

    void HandleP01(int value)
    {
        if (value != p01Solution)
        {
            BroadcastWrong(0);
            return;
        }
        CompletePuzzle(0);
    }

    void HandleP02(int value)
    {
        if (p02Step < 0 || p02Step >= p02Solution.Length || value != p02Solution[p02Step])
        {
            p02Step = 0;
            SyncState();
            BroadcastWrong(1);
            return;
        }
        p02Step++;
        if (p02Step >= p02Solution.Length)
        {
            p02Step = p02Solution.Length;
            completionMask |= 1 << 1;
        }
        SyncState();
    }

    void HandleP03(int value, int marker)
    {
        if (value < 0 || value > 3 || marker < 0 || marker > 3) return;
        if ((p03PlacedMask & (1 << marker)) != 0) return;
        if (p03SocketByMarker == null || p03SocketByMarker.Length != 4 || value != p03SocketByMarker[marker])
        {
            BroadcastWrong(2);
            return;
        }
        p03PlacedMask |= 1 << marker;
        if ((p03PlacedMask & 15) == 15) completionMask |= 1 << 2;
        SyncState();
    }

    void HandleP04(int value)
    {
        if (value != p04Solution)
        {
            BroadcastWrong(3);
            return;
        }
        CompletePuzzle(3);
    }

    void HandleP05(int value)
    {
        if (p05Step < 0 || p05Step >= p05Solution.Length || value != p05Solution[p05Step])
        {
            p05Step = 0;
            SyncState();
            BroadcastWrong(4);
            return;
        }
        p05Step++;
        if (p05Step >= p05Solution.Length)
        {
            p05Step = p05Solution.Length;
            completionMask |= 1 << 4;
            cleared = true;
        }
        SyncState();
    }

    void CompletePuzzle(int puzzleIndex)
    {
        completionMask |= 1 << puzzleIndex;
        SyncState();
    }

    void RaiseHint(int puzzleIndex)
    {
        int current = GetHintLevel(puzzleIndex);
        if (current >= 3) return;
        int shift = puzzleIndex * 2;
        hintPacked &= ~(3 << shift);
        hintPacked |= (current + 1) << shift;
        SyncState();
    }

    void ResetWorldOwner()
    {
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

    void SyncState()
    {
        RequestSerialization();
        ApplyPresentation();
    }

    public void ApplyPresentation()
    {
        if (observedResetGeneration != resetGeneration)
        {
            observedResetGeneration = resetGeneration;
            selectedMarker = -1;
        }
        else if (selectedMarker >= 0 && (p03PlacedMask & (1 << selectedMarker)) != 0)
        {
            selectedMarker = -1;
        }

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
            if ((p03PlacedMask & (1 << marker)) != 0 && p03SocketByMarker != null && p03SocketByMarker.Length == 4)
            {
                int socket = p03SocketByMarker[marker];
                if (socket >= 0 && socket < socketTargets.Length) target = socketTargets[socket];
            }
            else if (marker < markerHomes.Length)
            {
                target = markerHomes[marker];
            }
            if (target != null)
            {
                markerObjects[marker].transform.position = target.position;
                markerObjects[marker].transform.rotation = target.rotation;
            }
        }
    }

    void BroadcastWrong(int puzzleIndex)
    {
        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ShowWrongNetwork), puzzleIndex);
    }

    [NetworkCallable(maxEventsPerSecond: 20)]
    public void ShowWrongNetwork(int puzzleIndex)
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
