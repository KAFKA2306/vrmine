using System.IO;
using System.Text;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Components;
using Vowgan;
using Vowgan.DeckOfCards;

public static class VRMineVerification
{
    const string ReportRelativePath = "KafkaMade/VRMine/Verification/LatestVerification.txt";
    const string RuntimeReportRelativePath = "KafkaMade/VRMine/Verification/LatestRuntimeVerification.txt";
    const string InventoryReportRelativePath = "KafkaMade/VRMine/Verification/LatestInventoryVerification.txt";
    const string ChessReportRelativePath = "KafkaMade/VRMine/Verification/LatestChessRuntimeVerification.txt";
    const string DeckReportRelativePath = "KafkaMade/VRMine/Verification/LatestDeckRuntimeVerification.txt";
    static double runtimeStartedAt;
    static Vector3[] runtimeStartPositions;
    static Vector3[] chessStartPositions;
    static bool chessSnapPassed;

    [InitializeOnLoadMethod]
    static void Initialize()
    {
        EditorApplication.update -= RunRuntimeGate;
        EditorApplication.update += RunRuntimeGate;
    }

    static void RunRuntimeGate()
    {
        string phase = SessionState.GetString("VRMine.RuntimePhase", "");
        if (phase == "chess-return" || phase == "deck-return")
        {
            if (EditorApplication.isPlaying) return;
            EditorSceneManager.OpenScene(SessionState.GetString("VRMine.ReturnScene", "Assets/trickstar.unity"), OpenSceneMode.Single);
            SessionState.SetString("VRMine.RuntimePhase", "");
            return;
        }
        if (phase == "" || !EditorApplication.isPlaying) return;
        if (phase.StartsWith("chess"))
        {
            RunChessRuntimeGate(phase);
            return;
        }
        if (phase.StartsWith("deck"))
        {
            RunDeckRuntimeGate(phase);
            return;
        }
        if (phase == "enter")
        {
            runtimeStartPositions = new Vector3[5];
            for (int i = 0; i < runtimeStartPositions.Length; i++) runtimeStartPositions[i] = GameObject.Find("Card_" + i).transform.position;
            runtimeStartedAt = EditorApplication.timeSinceStartup;
            SessionState.SetString("VRMine.RuntimePhase", "stability");
            return;
        }
        if (EditorApplication.timeSinceStartup - runtimeStartedAt < 10d) return;
        int stable = 0;
        StringBuilder positions = new StringBuilder();
        for (int i = 0; i < runtimeStartPositions.Length; i++)
        {
            GameObject card = GameObject.Find("Card_" + i);
            Rigidbody body = card.GetComponent<Rigidbody>();
            if (Vector3.Distance(runtimeStartPositions[i], card.transform.position) < 0.01f && body.velocity.sqrMagnitude < 0.0001f) stable++;
            positions.AppendLine("Card_" + i + " " + runtimeStartPositions[i] + " -> " + card.transform.position + " velocity=" + body.velocity);
        }
        PhysicalToken movedToken = GameObject.Find("Card_1").GetComponent<PhysicalToken>();
        Vector3 before = movedToken.transform.position;
        movedToken.transform.position += new Vector3(0.123f, 0.017f, 0.117f);
        movedToken.OnDrop();
        Vector3 after = movedToken.transform.position;
        bool moved = Vector3.Distance(before, after) > 0.1f;
        bool snapped = Mathf.Abs(after.x / movedToken.snapScale.x - Mathf.Round(after.x / movedToken.snapScale.x)) < 0.001f
            && Mathf.Abs(after.y / movedToken.snapScale.y - Mathf.Round(after.y / movedToken.snapScale.y)) < 0.001f
            && Mathf.Abs(after.z / movedToken.snapScale.z - Mathf.Round(after.z / movedToken.snapScale.z)) < 0.001f;
        StringBuilder report = new StringBuilder();
        report.AppendLine("VRMine Runtime Verification");
        report.AppendLine("Scene: " + SceneManager.GetActiveScene().path);
        report.AppendLine("Editor: " + Application.unityVersion);
        report.AppendLine((stable == runtimeStartPositions.Length ? "PASS " : "FAIL ") + "PhysicsStable10Seconds count=" + stable + "/" + runtimeStartPositions.Length);
        report.Append(positions);
        report.AppendLine((moved ? "PASS " : "FAIL ") + "CardMovement " + before + " -> " + after);
        report.AppendLine((snapped ? "PASS " : "FAIL ") + "CardSnap " + after);
        report.AppendLine("Result: " + (stable == runtimeStartPositions.Length && moved && snapped ? "PASS" : "FAIL"));
        File.WriteAllText(Path.Combine(Application.dataPath, RuntimeReportRelativePath), report.ToString(), Encoding.UTF8);
        Debug.Log(report.ToString());
        SessionState.SetString("VRMine.RuntimePhase", "");
        EditorApplication.isPlaying = false;
    }

    static void RunChessRuntimeGate(string phase)
    {
        if (phase == "chess-enter")
        {
            ResetChessGame reset = Object.FindObjectOfType<ResetChessGame>(true);
            reset.Initialize();
            chessStartPositions = new Vector3[reset.Pieces.Length];
            for (int i = 0; i < reset.Pieces.Length; i++) chessStartPositions[i] = reset.Pieces[i].transform.position;
            SnapOnDrop snap = reset.Pieces[0].GetComponent<SnapOnDrop>();
            snap.transform.position += new Vector3(0.063f, 0f, 0.047f);
            snap.OnDrop();
            Vector3 local = snap.transform.localPosition;
            chessSnapPassed = Mathf.Abs(local.x / snap.SnapPositionScale.x - Mathf.Round(local.x / snap.SnapPositionScale.x)) < 0.001f
                && Mathf.Abs(local.z / snap.SnapPositionScale.z - Mathf.Round(local.z / snap.SnapPositionScale.z)) < 0.001f;
            reset.Interact();
            runtimeStartedAt = EditorApplication.timeSinceStartup;
            SessionState.SetString("VRMine.RuntimePhase", "chess-reset");
            return;
        }
        if (EditorApplication.timeSinceStartup - runtimeStartedAt < 2d) return;
        ResetChessGame chess = Object.FindObjectOfType<ResetChessGame>(true);
        int resetCount = 0;
        for (int i = 0; i < chess.Pieces.Length; i++)
            if (Vector3.Distance(chessStartPositions[i], chess.Pieces[i].transform.position) < 0.01f) resetCount++;
        bool passed = chess.Pieces.Length == 32 && chessSnapPassed && resetCount == chess.Pieces.Length;
        StringBuilder report = new StringBuilder();
        report.AppendLine("Chess Runtime Verification");
        report.AppendLine("Scene: " + SceneManager.GetActiveScene().path);
        report.AppendLine((chess.Pieces.Length == 32 ? "PASS " : "FAIL ") + "Pieces count=" + chess.Pieces.Length);
        report.AppendLine((chessSnapPassed ? "PASS " : "FAIL ") + "GridSnap");
        report.AppendLine((resetCount == chess.Pieces.Length ? "PASS " : "FAIL ") + "Reset count=" + resetCount + "/" + chess.Pieces.Length);
        report.AppendLine("Result: " + (passed ? "PASS" : "FAIL"));
        File.WriteAllText(Path.Combine(Application.dataPath, ChessReportRelativePath), report.ToString(), Encoding.UTF8);
        Debug.Log(report.ToString());
        SessionState.SetString("VRMine.RuntimePhase", "chess-return");
        EditorApplication.isPlaying = false;
    }

    static void RunDeckRuntimeGate(string phase)
    {
        if (phase == "deck-enter")
        {
            runtimeStartedAt = EditorApplication.timeSinceStartup;
            SessionState.SetString("VRMine.RuntimePhase", "deck-ready");
            return;
        }
        if (EditorApplication.timeSinceStartup - runtimeStartedAt < 2d) return;
        DeckManager[] decks = Object.FindObjectsOfType<DeckManager>(true);
        int passedDecks = 0;
        StringBuilder report = new StringBuilder();
        report.AppendLine("Deck Runtime Verification");
        report.AppendLine("Scene: " + SceneManager.GetActiveScene().path);
        for (int i = 0; i < decks.Length; i++)
        {
            decks[i].Initialize();
            decks[i]._ResetDeck();
            int initial = decks[i].CardCount;
            decks[i].NextCard();
            int drawn = decks[i].CardCount;
            decks[i]._ResetDeck();
            int reset = decks[i].CardCount;
            bool passed = initial == decks[i].Pool.Pool.Length - 1 && drawn == initial - 1 && reset == decks[i].Pool.Pool.Length - 1;
            if (passed) passedDecks++;
            report.AppendLine((passed ? "PASS " : "FAIL ") + decks[i].name + " initial=" + initial + " drawn=" + drawn + " reset=" + reset);
        }
        bool allPassed = decks.Length > 0 && passedDecks == decks.Length;
        report.AppendLine("Result: " + (allPassed ? "PASS" : "FAIL") + " decks=" + passedDecks + "/" + decks.Length);
        File.WriteAllText(Path.Combine(Application.dataPath, DeckReportRelativePath), report.ToString(), Encoding.UTF8);
        Debug.Log(report.ToString());
        SessionState.SetString("VRMine.RuntimePhase", "deck-return");
        EditorApplication.isPlaying = false;
    }

    [MenuItem("VRMine/Verification/Run Runtime Gate")]
    public static void StartRuntimeGate()
    {
        SessionState.SetString("VRMine.RuntimePhase", "enter");
        EditorApplication.isPlaying = true;
    }

    [MenuItem("VRMine/Verification/Run Chess Runtime Gate")]
    public static void StartChessRuntimeGate()
    {
        StartSceneRuntimeGate("Assets/Vowgan/Snappable Chess Set/Chess Set.unity", "chess-enter");
    }

    [MenuItem("VRMine/Verification/Prepare Chess Scene")]
    public static void PrepareChessScene()
    {
        string activeScenePath = SceneManager.GetActiveScene().path;
        Scene scene = EditorSceneManager.OpenScene("Assets/Vowgan/Snappable Chess Set/Chess Set.unity", OpenSceneMode.Single);
        GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(GameObject.Find("Chess Set"));
        if (root != null) PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        VRCSceneDescriptor descriptor = Object.FindObjectOfType<VRCSceneDescriptor>();
        GameObject cameraObject = GameObject.Find("ReferenceCamera") ?? new GameObject("ReferenceCamera");
        Camera camera = cameraObject.GetComponent<Camera>();
        if (camera == null) camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 3f, -4f), Quaternion.Euler(30f, 0f, 0f));
        descriptor.ReferenceCamera = cameraObject;
        EditorUtility.SetDirty(descriptor);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
    }

    [MenuItem("VRMine/Verification/Run Deck Runtime Gate")]
    public static void StartDeckRuntimeGate()
    {
        StartSceneRuntimeGate("Assets/Vowgan/Deck of Cards/Demo/Deck Demo.unity", "deck-enter");
    }

    [MenuItem("VRMine/Verification/Prepare Deck Scene")]
    public static void PrepareDeckScene()
    {
        string activeScenePath = SceneManager.GetActiveScene().path;
        Scene scene = EditorSceneManager.OpenScene("Assets/Vowgan/Deck of Cards/Demo/Deck Demo.unity", OpenSceneMode.Single);
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            if (PrefabUtility.IsPartOfPrefabInstance(roots[i]))
                PrefabUtility.UnpackPrefabInstance(roots[i], PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        VRCSceneDescriptor descriptor = Object.FindObjectOfType<VRCSceneDescriptor>();
        GameObject cameraObject = descriptor.ReferenceCamera;
        if (cameraObject == null)
        {
            cameraObject = new GameObject("ReferenceCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 3f, -4f), Quaternion.Euler(30f, 0f, 0f));
            descriptor.ReferenceCamera = cameraObject;
        }
        EditorUtility.SetDirty(descriptor);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
    }

    static void StartSceneRuntimeGate(string scenePath, string phase)
    {
        SessionState.SetString("VRMine.ReturnScene", SceneManager.GetActiveScene().path);
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        SessionState.SetString("VRMine.RuntimePhase", phase);
        EditorApplication.isPlaying = true;
    }

    [MenuItem("VRMine/Verification/Run Gate")]
    public static void RunGate()
    {
        Scene scene = SceneManager.GetActiveScene();
        StringBuilder report = new StringBuilder();
        int failures = 0;

        report.AppendLine("VRMine Verification Gate");
        report.AppendLine("Scene: " + scene.path);
        report.AppendLine("Editor: " + Application.unityVersion);

        VRCSceneDescriptor[] descriptors = Object.FindObjectsOfType<VRCSceneDescriptor>(true);
        failures += Check(report, "SceneDescriptor", descriptors.Length == 1, "count=" + descriptors.Length);
        if (descriptors.Length == 1)
        {
            failures += Check(report, "SpawnPoints", descriptors[0].spawns != null && descriptors[0].spawns.Length > 0, "count=" + (descriptors[0].spawns == null ? 0 : descriptors[0].spawns.Length));
            failures += Check(report, "ReferenceCamera", descriptors[0].ReferenceCamera != null, descriptors[0].ReferenceCamera == null ? "null" : descriptors[0].ReferenceCamera.name);
        }

        BoardState[] boards = Object.FindObjectsOfType<BoardState>(true);
        GameController[] controllers = Object.FindObjectsOfType<GameController>(true);
        failures += Check(report, "BoardState", boards.Length == 1, "count=" + boards.Length);
        failures += Check(report, "GameController", controllers.Length == 1, "count=" + controllers.Length);

        if (boards.Length == 1 && controllers.Length == 1)
        {
            failures += Check(report, "ControllerBoardReference", controllers[0].board == boards[0], controllers[0].board == null ? "null" : controllers[0].board.name);
            failures += VerifyDeterministicBake(report, boards[0]);
        }

        PhysicalToken[] tokens = Object.FindObjectsOfType<PhysicalToken>(true);
        failures += Check(report, "PhysicalTokens", tokens.Length == 5, "count=" + tokens.Length);
        int movableCards = 0;
        int nonOverlappingColliders = 0;
        for (int i = 0; i < tokens.Length; i++)
        {
            GameObject card = tokens[i].gameObject;
            Rigidbody body = card.GetComponent<Rigidbody>();
            if (body != null && body.isKinematic && card.GetComponent<VRCPickup>() != null && card.GetComponent<VRCObjectSync>() != null) movableCards++;
            BoxCollider collider = card.GetComponent<BoxCollider>();
            if (collider != null && collider.size == new Vector3(0.2f, 0.2f, 0.2f)) nonOverlappingColliders++;
        }
        failures += Check(report, "MovableCards", movableCards == 5, "count=" + movableCards);
        failures += Check(report, "CardColliders", nonOverlappingColliders == 5, "count=" + nonOverlappingColliders);
        UdonSharpBehaviour[] behaviours = Object.FindObjectsOfType<UdonSharpBehaviour>(true);
        int validPrograms = 0;
        for (int i = 0; i < behaviours.Length; i++)
        {
            VRC.Udon.UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviours[i]);
            if (backing != null && backing.programSource != null) validPrograms++;
        }
        failures += Check(report, "UdonPrograms", validPrograms == behaviours.Length, "count=" + validPrograms + "/" + behaviours.Length);

        report.AppendLine("Result: " + (failures == 0 ? "PASS" : "FAIL"));
        string reportPath = Path.Combine(Application.dataPath, ReportRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        File.WriteAllText(reportPath, report.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();
        Debug.Log(report.ToString());
    }

    [MenuItem("VRMine/Verification/Run Inventory Gate")]
    public static void RunInventoryGate()
    {
        string[] scenePaths =
        {
            "Assets/trickstar.unity",
            "Assets/KafkaMade/VRMine/Scenes/BoardGameLab.unity",
            "Assets/KafkaMade/VRMine/Scenes/MVP.unity",
            "Assets/KafkaMade/VRMine/Scenes/VRMine.unity",
            "Assets/Vowgan/Deck of Cards/Demo/Deck Demo.unity",
            "Assets/Vowgan/Snappable Chess Set/Chess Set.unity"
        };
        string activeScenePath = SceneManager.GetActiveScene().path;
        StringBuilder report = new StringBuilder();
        report.AppendLine("VRMine Inventory Verification");
        report.AppendLine("Editor: " + Application.unityVersion);
        int failures = 0;
        for (int i = 0; i < scenePaths.Length; i++)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePaths[i], OpenSceneMode.Single);
            VRCSceneDescriptor[] descriptors = Object.FindObjectsOfType<VRCSceneDescriptor>(true);
            UdonSharpBehaviour[] behaviours = Object.FindObjectsOfType<UdonSharpBehaviour>(true);
            VRCPickup[] pickups = Object.FindObjectsOfType<VRCPickup>(true);
            int missingScripts = 0;
            int validPrograms = 0;
            int invalidPickups = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Transform[] transforms = roots[rootIndex].GetComponentsInChildren<Transform>(true);
                for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
                {
                    int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transforms[transformIndex].gameObject);
                    missingScripts += missing;
                    if (missing > 0) report.AppendLine("  MissingScriptObject=" + GetPath(transforms[transformIndex]));
                }
            }
            for (int behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
            {
                VRC.Udon.UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviours[behaviourIndex]);
                if (backing != null && backing.programSource != null) validPrograms++;
            }
            for (int pickupIndex = 0; pickupIndex < pickups.Length; pickupIndex++)
            {
                GameObject pickup = pickups[pickupIndex].gameObject;
                if (pickup.GetComponent<Rigidbody>() == null || pickup.GetComponent<Collider>() == null || pickup.GetComponent<VRCObjectSync>() == null) invalidPickups++;
            }
            int spawns = descriptors.Length == 1 && descriptors[0].spawns != null ? descriptors[0].spawns.Length : 0;
            bool passed = descriptors.Length == 1 && spawns > 0 && missingScripts == 0 && validPrograms == behaviours.Length && invalidPickups == 0;
            if (!passed) failures++;
            report.AppendLine((passed ? "PASS " : "FAIL ") + scenePaths[i]);
            report.AppendLine("  SceneDescriptor=" + descriptors.Length + " Spawns=" + spawns + " MissingScripts=" + missingScripts);
            report.AppendLine("  UdonPrograms=" + validPrograms + "/" + behaviours.Length + " Pickups=" + pickups.Length + " InvalidPickups=" + invalidPickups);
        }
        EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
        report.AppendLine("Result: " + (failures == 0 ? "PASS" : "FAIL") + " failures=" + failures + "/" + scenePaths.Length);
        string reportPath = Path.Combine(Application.dataPath, InventoryReportRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        File.WriteAllText(reportPath, report.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();
        Debug.Log(report.ToString());
    }

    public static void RepairLegacySceneSerialization()
    {
        string[] scenePaths =
        {
            "Assets/KafkaMade/VRMine/Scenes/BoardGameLab.unity",
            "Assets/KafkaMade/VRMine/Scenes/VRMine.unity"
        };
        for (int i = 0; i < scenePaths.Length; i++)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePaths[i], OpenSceneMode.Single);
            GameObject logBoard = GameObject.Find("LogBoard");
            LogBoard[] logBoardComponents = logBoard.GetComponents<LogBoard>();
            for (int componentIndex = 1; componentIndex < logBoardComponents.Length; componentIndex++)
                Object.DestroyImmediate(logBoardComponents[componentIndex], true);
            VRC.Udon.UdonBehaviour[] udonBehaviours = logBoard.GetComponents<VRC.Udon.UdonBehaviour>();
            for (int componentIndex = 0; componentIndex < udonBehaviours.Length; componentIndex++)
                Object.DestroyImmediate(udonBehaviours[componentIndex], true);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
    }

    static string GetPath(Transform target)
    {
        string path = target.name;
        while (target.parent != null)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }
        return path;
    }

    static int VerifyDeterministicBake(StringBuilder report, BoardState board)
    {
        byte[] originalCells = new byte[board.cells.Length];
        for (int i = 0; i < originalCells.Length; i++) originalCells[i] = board.cells[i];
        uint firstHash = board.Bake(123456789u);
        byte[] firstCells = new byte[board.cells.Length];
        for (int i = 0; i < firstCells.Length; i++) firstCells[i] = board.cells[i];
        uint secondHash = board.Bake(123456789u);
        bool sameHash = firstHash == secondHash;
        bool sameCells = board.Matches(firstCells);
        for (int i = 0; i < originalCells.Length; i++) board.cells[i] = originalCells[i];
        int failures = 0;
        failures += Check(report, "BoardBakeHash", sameHash, firstHash + " / " + secondHash);
        failures += Check(report, "BoardBakeCells", sameCells, "length=" + firstCells.Length);
        return failures;
    }

    static int Check(StringBuilder report, string name, bool passed, string detail)
    {
        report.AppendLine((passed ? "PASS " : "FAIL ") + name + " " + detail);
        return passed ? 0 : 1;
    }
}
