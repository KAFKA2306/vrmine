using System.IO;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.Udon;

public static class VRMineBridge
{
    const string ScenePath = "Assets/trickstar.unity";

    [MenuItem("VRMine/wire_scene")]
    public static void BuildVisuals()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) return;
        EnsureProgramAsset<BoardState>();
        EnsureProgramAsset<LogStream>();
        EnsureProgramAsset<GameController>();
        EnsureProgramAsset<PlayerClient>();
        EnsureProgramAsset<BoardView>();
        EnsureProgramAsset<CardView>();
        EnsureProgramAsset<PhysicalToken>();
        EnsureProgramAsset<RuleView>();
        EnsureProgramAsset<ScorePanelView>();
        UdonSharpProgramAsset.UdonSharpCheckAbsent();
        UdonSharpProgramAsset.CompileAllCsPrograms(true);
        
        // 1. Logic Wiring
        EnsureScene(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    static void EnsureScene(Scene scene)
    {
        GameObject boardRoot = GameObject.Find("BoardRoot");
        GameObject runtimeRoot = FindOrCreateRoot("RuntimeRoot");
        
        // Logic Objects
        GameObject controller = FindOrCreateObject(runtimeRoot.transform, "GameController");
        GameObject state = FindOrCreateObject(runtimeRoot.transform, "BoardState");
        GameObject logStream = FindOrCreateObject(runtimeRoot.transform, "LogStream");
        GameObject pc = FindOrCreateObject(runtimeRoot.transform, "PlayerClient");

        // Components
        BoardState boardState = EnsureComponent<BoardState>(state);
        LogStream stream = EnsureComponent<LogStream>(logStream);
        GameController ctrl = EnsureComponent<GameController>(controller);
        PlayerClient pClient = EnsureComponent<PlayerClient>(pc);
        BoardView bView = EnsureComponent<BoardView>(boardRoot);

        // Link Visuals to Behavior
        GameObject boardQuad = GameObject.Find("BoardQuad");
        if (boardQuad != null) bView.boardRenderer = boardQuad.GetComponent<Renderer>();

        // Wire
        ctrl.board = boardState;
        ctrl.logStream = stream;
        ctrl.mailboxes = new[] { pClient };
        pClient.controller = ctrl;
        bView.state = boardState;
        bView.controller = ctrl;

        CardView[] physicalCards = new CardView[5];
        for (int i = 0; i < physicalCards.Length; i++)
        {
            GameObject card = GameObject.Find("Card_" + i);
            EnsureComponent<BoxCollider>(card).size = new Vector3(0.2f, 0.2f, 0.2f);
            Rigidbody body = EnsureComponent<Rigidbody>(card);
            body.useGravity = false;
            body.isKinematic = true;
            EnsureComponent<VRCPickup>(card);
            EnsureComponent<VRCObjectSync>(card);
            CardView cardView = EnsureComponent<CardView>(card);
            cardView.controller = ctrl;
            cardView.cardIndex = i;
            PhysicalToken token = EnsureComponent<PhysicalToken>(card);
            token.controller = ctrl;
            physicalCards[i] = cardView;
        }
        bView.handCards = physicalCards;
        
        // Find Cards
        GameObject handRoot = GameObject.Find("HandRoot");
        if (handRoot != null)
        {
            CardView[] cards = handRoot.GetComponentsInChildren<CardView>(true);
            bView.handCards = cards;
            // CardView no longer needs controller reference
        }

        GameObject trickRoot = GameObject.Find("TrickRoot");
        if (trickRoot != null)
        {
            CardView[] cards = trickRoot.GetComponentsInChildren<CardView>(true);
            bView.trickCards = cards;
            // CardView no longer needs controller reference
        }

        // Wire Physical Rule Display
        GameObject ruleTextObj = GameObject.Find("RuleText");
        if (ruleTextObj != null)
        {
            RuleView ruleView = EnsureComponent<RuleView>(ruleTextObj);
            ruleView.state = boardState;
            ruleView.bodyText = ruleTextObj.GetComponent<Text>();
            bView.ruleView = ruleView;
        }

        // Wire Physical Score Display
        GameObject scoreLabelObj = GameObject.Find("ScoreLabel");
        if (scoreLabelObj != null)
        {
            ScorePanelView scoreView = EnsureComponent<ScorePanelView>(scoreLabelObj);
            scoreView.state = boardState;
            scoreView.scoreText = scoreLabelObj.GetComponent<Text>();
            bView.scoreView = scoreView;
        }

        EnsureSceneDescriptor();
    }

    static GameObject FindOrCreateRoot(string name)
    {
        GameObject go = GameObject.Find(name);
        return go ?? new GameObject(name);
    }

    static GameObject FindOrCreateObject(Transform parent, string name)
    {
        GameObject go = GameObject.Find(name);
        if (go != null) { go.transform.SetParent(parent, false); return go; }
        go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    static T EnsureComponent<T>(GameObject go) where T : Component
    {
        T comp = go.GetComponent<T>();
        if (comp != null && comp is UdonSharpBehaviour proxy)
        {
            UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy);
            if (backing == null || backing.programSource == null)
            {
                if (backing != null) Object.DestroyImmediate(backing);
                Object.DestroyImmediate(comp);
                comp = null;
            }
        }
        if (typeof(UdonSharpBehaviour).IsAssignableFrom(typeof(T)))
        {
            UdonBehaviour[] behaviours = go.GetComponents<UdonBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i].programSource == null) Object.DestroyImmediate(behaviours[i]);
            }
        }
        if (comp == null)
        {
            if (typeof(UdonSharpBehaviour).IsAssignableFrom(typeof(T))) comp = (T)(Component)go.AddUdonSharpComponent(typeof(T));
            else comp = go.AddComponent<T>();
        }
        return comp;
    }

    static void EnsureProgramAsset<T>() where T : UdonSharpBehaviour
    {
        string[] scripts = AssetDatabase.FindAssets(typeof(T).Name + " t:MonoScript");
        MonoScript script = null;
        for (int i = 0; i < scripts.Length; i++)
        {
            MonoScript candidate = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(scripts[i]));
            if (candidate.GetClass() == typeof(T)) script = candidate;
        }
        string scriptPath = AssetDatabase.GetAssetPath(script);
        string assetPath = Path.ChangeExtension(scriptPath, ".asset");
        if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(assetPath) != null) return;
        UdonSharpProgramAsset program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
        program.sourceCsScript = script;
        AssetDatabase.CreateAsset(program, assetPath);
    }

    static void EnsureSceneDescriptor()
    {
        VRCSceneDescriptor desc = Object.FindObjectOfType<VRCSceneDescriptor>();
        if (desc == null) desc = new GameObject("VRCSceneDescriptor").AddComponent<VRCSceneDescriptor>();
        GameObject spawn = GameObject.Find("SpawnPoint") ?? new GameObject("SpawnPoint");
        spawn.transform.position = new Vector3(0, 1.1f, -2f);
        desc.spawns = new[] { spawn.transform };
        GameObject referenceCamera = GameObject.Find("ReferenceCamera") ?? new GameObject("ReferenceCamera");
        referenceCamera.transform.position = new Vector3(0, 1.6f, -2f);
        referenceCamera.transform.rotation = Quaternion.Euler(20f, 0, 0);
        Camera camera = EnsureComponent<Camera>(referenceCamera);
        camera.fieldOfView = 40f;
        camera.enabled = false;
        desc.ReferenceCamera = referenceCamera;
    }
}
