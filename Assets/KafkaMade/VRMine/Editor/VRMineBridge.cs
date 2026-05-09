using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Components;

public static class VRMineBridge
{
    const string ScenePath = "Assets/KafkaMade/VRMine/Scenes/MVP.unity";

    [MenuItem("VRMine/wire_scene")]
    public static void WireScene()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) return;
        
        // 1. Physical Build
        VisualBuilder.BuildVisuals();

        // 2. Logic Wiring
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
        GameObject client = FindOrCreateObject(runtimeRoot.transform, "PlayerClient");
        GameObject state = FindOrCreateObject(runtimeRoot.transform, "BoardState");
        GameObject logStream = FindOrCreateObject(runtimeRoot.transform, "LogStream");

        // Components
        BoardState boardState = EnsureComponent<BoardState>(state);
        LogStream stream = EnsureComponent<LogStream>(logStream);
        GameController ctrl = EnsureComponent<GameController>(controller);
        PlayerClient pc = EnsureComponent<PlayerClient>(client);
        BoardView bView = EnsureComponent<BoardView>(boardRoot);

        // Link Visuals to Behavior
        GameObject boardQuad = GameObject.Find("BoardQuad");
        if (boardQuad != null) bView.boardRenderer = boardQuad.GetComponent<Renderer>();

        // Wire
        ctrl.board = boardState;
        ctrl.logStream = stream;
        ctrl.mailboxes = new[] { pc };
        pc.controller = ctrl;
        bView.state = boardState;
        bView.controller = ctrl;

        // Wire Declare Button
        GameObject buttonCap = GameObject.Find("ButtonCap");
        if (buttonCap != null)
        {
            DeclareButton declareButton = EnsureComponent<DeclareButton>(buttonCap);
            declareButton.controller = ctrl;
        }

        // Wire Physical Rule Display
        GameObject ruleTextObj = GameObject.Find("RuleText");
        if (ruleTextObj != null)
        {
            RuleView ruleView = EnsureComponent<RuleView>(ruleTextObj);
            ruleView.state = boardState;
            ruleView.ruleText = ruleTextObj.GetComponent<Text>();
        }

        // Wire Physical Score Display
        GameObject scoreLabelObj = GameObject.Find("ScoreLabel");
        if (scoreLabelObj != null)
        {
            ScorePanelView scoreView = EnsureComponent<ScorePanelView>(scoreLabelObj);
            scoreView.state = boardState;
            scoreView.scoreText = scoreLabelObj.GetComponent<Text>();
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
        return comp ?? go.AddComponent<T>();
    }

    static void EnsureSceneDescriptor()
    {
        VRCSceneDescriptor desc = Object.FindObjectOfType<VRCSceneDescriptor>();
        if (desc == null) desc = new GameObject("VRCSceneDescriptor").AddComponent<VRCSceneDescriptor>();
        GameObject spawn = GameObject.Find("SpawnPoint") ?? new GameObject("SpawnPoint");
        spawn.transform.position = new Vector3(0, 1.1f, -2f);
        desc.spawns = new[] { spawn.transform };
    }
}
