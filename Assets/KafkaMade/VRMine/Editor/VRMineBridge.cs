using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.Udon;

public static class VRMineBridge
{
    const string ScenePath = "Assets/KafkaMade/VRMine/Scenes/MVP.unity";
    const string PrefabDir = "Assets/KafkaMade/VRMine/Prefabs";

    [MenuItem("VRMine/wire_scene")]
    public static void WireScene()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) return;
        EnsureScene(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("VRMine/validate_scene")]
    public static void ValidateScene()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) return;
        GameObject controller = GameObject.Find("GameController");
        GameObject boardRoot = GameObject.Find("BoardRoot");
        GameObject uiRoot = GameObject.Find("UIRoot");
        GameObject tableRoot = GameObject.Find("TableRoot");
        GameObject runtimeRoot = GameObject.Find("RuntimeRoot");
        GameObject logBoard = GameObject.Find("LogBoard");
        bool ok = controller != null && boardRoot != null && uiRoot != null && tableRoot != null && runtimeRoot != null && logBoard != null;
        Debug.Log("VRMine validate_scene " + (ok ? "OK" : "NG"));
        EditorSceneManager.MarkSceneDirty(scene);
    }

    static void EnsureScene(Scene scene)
    {
        GameObject vrMine = FindOrCreateRoot("VRMine");
        GameObject environment = FindOrCreateRoot("Environment");
        GameObject tableRoot = FindOrCreateRoot("TableRoot");
        GameObject boardRoot = FindOrCreateRoot("BoardRoot");
        GameObject uiRoot = FindOrCreateRoot("UIRoot");
        GameObject runtimeRoot = FindOrCreateRoot("RuntimeRoot");
        GameObject lightingRoot = FindOrCreateRoot("LightingRoot");
        GameObject audioRoot = FindOrCreateRoot("AudioRoot");
        EnsureSceneDescriptor(vrMine);
        EnsureEnvironment(environment);
        EnsureTable(tableRoot);
        EnsureRuntime(runtimeRoot, boardRoot, uiRoot);
        EnsureUi(uiRoot);
        EnsureLighting(lightingRoot);
        EnsureAudio(audioRoot);
        EnsurePrefabs();
        EnsureRootParenting(vrMine, environment, tableRoot, boardRoot, uiRoot, runtimeRoot, lightingRoot, audioRoot);
    }

    static GameObject FindOrCreateRoot(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go != null) return go;
        go = new GameObject(name);
        return go;
    }

    static void EnsureRootParenting(params GameObject[] roots)
    {
        if (roots.Length < 8) return;
        GameObject vrMine = roots[0];
        GameObject environment = roots[1];
        GameObject tableRoot = roots[2];
        GameObject boardRoot = roots[3];
        GameObject uiRoot = roots[4];
        GameObject runtimeRoot = roots[5];
        GameObject lightingRoot = roots[6];
        GameObject audioRoot = roots[7];
        if (vrMine != null)
        {
            vrMine.transform.SetParent(null);
            vrMine.transform.localPosition = Vector3.zero;
            vrMine.transform.localRotation = Quaternion.identity;
            vrMine.transform.localScale = Vector3.one;
        }
        if (environment != null) environment.transform.SetParent(vrMine != null ? vrMine.transform : null, false);
        if (tableRoot != null) tableRoot.transform.SetParent(vrMine != null ? vrMine.transform : null, false);
        if (boardRoot != null) boardRoot.transform.SetParent(tableRoot != null ? tableRoot.transform : (vrMine != null ? vrMine.transform : null), false);
        if (uiRoot != null) uiRoot.transform.SetParent(vrMine != null ? vrMine.transform : null, false);
        if (runtimeRoot != null) runtimeRoot.transform.SetParent(vrMine != null ? vrMine.transform : null, false);
        if (lightingRoot != null) lightingRoot.transform.SetParent(vrMine != null ? vrMine.transform : null, false);
        if (audioRoot != null) audioRoot.transform.SetParent(vrMine != null ? vrMine.transform : null, false);
        if (boardRoot != null) boardRoot.transform.localPosition = Vector3.zero;
    }

    static void EnsureSceneDescriptor(GameObject vrMine)
    {
        VRCSceneDescriptor[] descriptors = Object.FindObjectsOfType<VRCSceneDescriptor>();
        for (int i = 0; i < descriptors.Length; i++)
        {
            VRCSceneDescriptor item = descriptors[i];
            if (item == null) continue;
            if (item.gameObject == vrMine) continue;
            Object.DestroyImmediate(item);
        }
        VRCSceneDescriptor descriptor = vrMine.GetComponent<VRCSceneDescriptor>();
        if (descriptor == null) descriptor = vrMine.AddComponent<VRCSceneDescriptor>();
        Transform spawn = FindOrCreateChild(vrMine.transform, "SpawnPoint").transform;
        spawn.localPosition = new Vector3(0f, 1.1f, -2.5f);
        spawn.localRotation = Quaternion.identity;
        descriptor.spawns = new[] { spawn };
        descriptor.spawnRadius = 0f;
        descriptor.spawnOrder = VRCSceneDescriptor.SpawnOrder.Sequential;
        descriptor.spawnOrientation = VRCSceneDescriptor.SpawnOrientation.Default;
        descriptor.RespawnHeightY = -100f;
        descriptor.ForbidUserPortals = false;
    }

    static void EnsureEnvironment(GameObject environment)
    {
        GameObject room = FindOrCreateChild(environment.transform, "Room");
        GameObject deskProps = FindOrCreateChild(environment.transform, "DeskProps");
        GameObject monitorProps = FindOrCreateChild(environment.transform, "MonitorProps");
        GameObject window = FindOrCreateChild(environment.transform, "Window");
        GameObject rainPanel = FindOrCreateChild(environment.transform, "RainPanel");
        GameObject wallNotes = FindOrCreateChild(environment.transform, "WallNotes");
        CreateRoomBackdrop(room.transform);
        CreateDeskProps(deskProps.transform);
        CreateMonitorProps(monitorProps.transform);
        CreateWindowScene(window.transform);
        CreateRainPanel(rainPanel.transform);
        CreateWallNotes(wallNotes.transform);
    }

    static void EnsureTable(GameObject tableRoot)
    {
        GameObject table = FindOrCreateChild(tableRoot.transform, "RoundTable");
        GameObject board = FindOrCreateChild(tableRoot.transform, "BoardQuad");
        GameObject hand = FindOrCreateChild(tableRoot.transform, "PlayerHandAnchor");
        GameObject rule = FindOrCreateChild(tableRoot.transform, "RuleAreaAnchor");
        GameObject trick = FindOrCreateChild(tableRoot.transform, "TrickAreaAnchor");
        GameObject discard = FindOrCreateChild(tableRoot.transform, "DiscardAreaAnchor");
        table.transform.localPosition = new Vector3(0f, 0.75f, 0f);
        table.transform.localScale = new Vector3(1.3f, 0.12f, 1.3f);
        board.transform.localPosition = new Vector3(0f, 0.86f, 0f);
        board.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
        hand.transform.localPosition = new Vector3(0f, 0.92f, -0.82f);
        rule.transform.localPosition = new Vector3(0f, 1.12f, 0.78f);
        trick.transform.localPosition = new Vector3(0.52f, 0.90f, 0.0f);
        discard.transform.localPosition = new Vector3(-0.52f, 0.90f, 0.0f);
        SetupPrimitive(table, PrimitiveType.Cylinder);
        SetupPrimitive(board, PrimitiveType.Quad);
        board.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        TintPrimitive(table, new Color(0.08f, 0.10f, 0.14f, 1f));
        TintPrimitive(board, new Color(0.10f, 0.12f, 0.17f, 1f));
        GameObject ring = FindOrCreateChild(tableRoot.transform, "TableRing");
        ring.transform.localPosition = new Vector3(0f, 0.88f, 0f);
        ring.transform.localScale = new Vector3(1.42f, 0.03f, 1.42f);
        SetupPrimitive(ring, PrimitiveType.Cylinder);
        TintPrimitive(ring, new Color(0.16f, 0.20f, 0.28f, 1f));
    }

    static void EnsureRuntime(GameObject runtimeRoot, GameObject boardRoot, GameObject uiRoot)
    {
        GameObject controller = FindOrCreateRuntimeObject(runtimeRoot.transform, "GameController");
        GameObject client = FindOrCreateRuntimeObject(runtimeRoot.transform, "PlayerClient");
        GameObject boardStateObject = FindOrCreateRuntimeObject(runtimeRoot.transform, "BoardState");
        LogStream[] existingLogStreams = Object.FindObjectsOfType<LogStream>(true);
        for (int i = 0; i < existingLogStreams.Length; i++)
        {
            LogStream existing = existingLogStreams[i];
            if (existing == null) continue;
            Object.DestroyImmediate(existing.gameObject);
        }
        GameObject logStreamObject = new GameObject("LogStream");
        logStreamObject.transform.SetParent(runtimeRoot.transform, false);
        GameObject logBoardObject = FindOrCreateRuntimeObject(runtimeRoot.transform, "LogBoard");
        GameObject logBoardViewObject = FindOrCreateRuntimeObject(runtimeRoot.transform, "LogBoardView");
        GameObject wave = FindOrCreateRuntimeObject(runtimeRoot.transform, "WaveSimulator");
        BoardState boardState = EnsureComponent<BoardState>(boardStateObject);
        LogStream logStream = EnsureComponent<LogStream>(logStreamObject);
        LogBoard logBoard = EnsureComponent<LogBoard>(logBoardObject);
        LogBoardView logBoardView = EnsureComponent<LogBoardView>(logBoardViewObject);
        GameController controllerBehaviour = EnsureComponent<GameController>(controller);
        PlayerClient playerClient = EnsureComponent<PlayerClient>(client);
        WaveSimulator waveSimulator = EnsureComponent<WaveSimulator>(wave);
        RemoveUdonBehaviourIfAny(logBoardObject);
        RemoveUdonBehaviourIfAny(logBoardViewObject);
        BoardView boardView = EnsureComponent<BoardView>(EnsureChildComponent(boardRoot.transform, "BoardView"));
        GameObject boardCells = FindOrCreateChild(boardRoot.transform, "CellMarkers");
        GameObject boardBlocks = FindOrCreateChild(boardRoot.transform, "BlockMarkers");
        GameObject boardTricks = FindOrCreateChild(boardRoot.transform, "TrickMarkers");
        RemoveUdonBehaviourIfAny(boardView.gameObject);
        boardView.state = boardState;
        boardView.controller = controllerBehaviour;
        GameObject boardQuadObject = GameObject.Find("BoardQuad");
        if (boardQuadObject != null) boardView.boardRenderer = boardQuadObject.GetComponent<Renderer>();
        boardView.statusText = CreateBoardStatusText(boardRoot.transform);
        boardView.cellMarkers = CreateCellMarkers(boardCells.transform, NetConst.GridWidth * NetConst.GridHeight, new Color(0.16f, 0.22f, 0.30f, 1f), 0.095f);
        boardView.blockMarkers = CreateCellMarkers(boardBlocks.transform, boardState.blocks.Length, new Color(0.24f, 0.28f, 0.36f, 1f), 0.11f);
        boardView.trickMarkers = CreateTrickMarkers(boardTricks.transform, 4, new Color(0.55f, 0.92f, 1f, 1f));
        controllerBehaviour.board = boardState;
        controllerBehaviour.logStream = logStream;
        controllerBehaviour.wave = waveSimulator;
        controllerBehaviour.mailboxes = new[] { playerClient };
        playerClient.controller = controllerBehaviour;
        logBoardView.board = logBoard;
        logBoardView.stream = logStream;
        boardView.cellRoot = boardRoot.transform;
        logBoard.rows = CreateLogRows(logBoardObject.transform, uiRoot);
        logBoardView.titleText = FindText(uiRoot, "LogTitle");
        logBoardView.footerText = FindText(uiRoot, "LogFooter");
        AttachPanels(uiRoot, boardState, controllerBehaviour, logStream);
    }

    static void EnsureUi(GameObject uiRoot)
    {
        SetupWorldCanvas(uiRoot, Vector3.zero, new Vector3(0.01f, 0.01f, 0.01f));
        GameObject canvasLog = FindOrCreateSceneObject(uiRoot.transform, "Canvas_Log");
        SetupWorldCanvas(canvasLog, new Vector3(105f, 112f, 62f), new Vector3(0.008f, 0.008f, 0.008f));
        Text logTitle = CreateText(canvasLog.transform, "LogTitle", "VRMine Log", 22, TextAnchor.UpperLeft, new Vector2(420f, 44f));
        logTitle.color = new Color(0.84f, 0.90f, 0.98f, 1f);
        GameObject logFooter = FindOrCreateChild(canvasLog.transform, "LogFooter");
        SetupLabel(logFooter, "EVENTS 0 / 0", 14, TextAnchor.LowerLeft, new Vector2(420f, 24f));
        GameObject phasePanel = BuildPanel(uiRoot.transform, "PhasePanel", new Vector3(-95f, 105f, 55f), new Vector2(0.36f, 0.22f), "PHASE", "WAITING");
        GameObject scorePanel = BuildPanel(uiRoot.transform, "ScorePanel", new Vector3(-95f, 78f, 55f), new Vector2(0.36f, 0.28f), "SCORE", "0 : 0");
        GameObject rulePanel = BuildPanel(uiRoot.transform, "RulePanel", new Vector3(0f, 108f, 82f), new Vector2(0.42f, 0.28f), "CURRENT RULE", "HIDDEN");
        GameObject warningPanel = BuildPanel(uiRoot.transform, "WarningPanel", new Vector3(0.0f, 82f, -82f), new Vector2(0.44f, 0.18f), "WARNING", "NO WARNINGS");
        GameObject monitorPanel = BuildPanel(uiRoot.transform, "KafkaMonitorPanel", new Vector3(102f, 95f, 12f), new Vector2(0.42f, 0.28f), "LOG", "EVENTS 0 / 0");
        EnsureComponent<PhaseView>(phasePanel);
        EnsureComponent<ScorePanelView>(scorePanel);
        EnsureComponent<RuleView>(rulePanel);
        EnsureComponent<WarningPanelView>(warningPanel);
        EnsureComponent<LogBoardView>(monitorPanel);
    }

    static void EnsureLighting(GameObject lightingRoot)
    {
        GameObject tableLight = FindOrCreateChild(lightingRoot.transform, "TableLight");
        GameObject rimLight = FindOrCreateChild(lightingRoot.transform, "RimLight");
        GameObject ambientSphere = FindOrCreateChild(lightingRoot.transform, "AmbientSphere");
        Light light = EnsureComponent<Light>(tableLight);
        light.type = LightType.Point;
        light.color = new Color(0.74f, 0.86f, 1f, 1f);
        light.range = 8f;
        light.intensity = 1.3f;
        tableLight.transform.localPosition = new Vector3(0f, 1.75f, 0f);
        Light rim = EnsureComponent<Light>(rimLight);
        rim.type = LightType.Spot;
        rim.color = new Color(0.86f, 0.72f, 1f, 1f);
        rim.range = 12f;
        rim.spotAngle = 52f;
        rim.intensity = 0.55f;
        rim.transform.localPosition = new Vector3(1.8f, 3.2f, -2.4f);
        rim.transform.localRotation = Quaternion.Euler(62f, -24f, 0f);
        Light ambient = EnsureComponent<Light>(ambientSphere);
        ambient.type = LightType.Point;
        ambient.color = new Color(0.22f, 0.30f, 0.42f, 1f);
        ambient.range = 14f;
        ambient.intensity = 0.35f;
        ambientSphere.transform.localPosition = new Vector3(-1.8f, 2.3f, 2.2f);
    }

    static void EnsureAudio(GameObject audioRoot)
    {
        FindOrCreateChild(audioRoot.transform, "AmbientNoise");
        FindOrCreateChild(audioRoot.transform, "RainLoop");
        FindOrCreateChild(audioRoot.transform, "FanLoop");
    }

    static void EnsurePrefabs()
    {
        Directory.CreateDirectory(PrefabDir);
        SaveSimplePrefab("BoardCellMarker.prefab", PrimitiveType.Cube, new Color(0.45f, 0.85f, 1f, 1f));
        SaveSimplePrefab("BlockMarker.prefab", PrimitiveType.Cube, new Color(0.18f, 0.22f, 0.28f, 1f));
        SaveCardPrefab("CardView.prefab");
        SavePanelPrefab("RuleCardView.prefab", "CURRENT RULE");
        SavePanelPrefab("PhasePanel.prefab", "PHASE");
        SavePanelPrefab("ScorePanel.prefab", "SCORE");
        SavePanelPrefab("WarningPanel.prefab", "WARNING");
        SaveSimplePrefab("TableLight.prefab", PrimitiveType.Sphere, new Color(0.75f, 0.88f, 1f, 1f), true);
        SavePanelPrefab("KafkaMonitorPanel.prefab", "LOG");
    }

    static void SaveSimplePrefab(string name, PrimitiveType type, Color tint, bool light = false)
    {
        string path = Path.Combine(PrefabDir, name).Replace("\\", "/");
        GameObject root = GameObject.CreatePrimitive(type);
        root.name = Path.GetFileNameWithoutExtension(name);
        TintPrimitive(root, tint);
        if (light) EnsureComponent<Light>(root).type = LightType.Point;
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    static void SaveCardPrefab(string name)
    {
        string path = Path.Combine(PrefabDir, name).Replace("\\", "/");
        GameObject root = new GameObject(Path.GetFileNameWithoutExtension(name), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CardView));
        SetupPanelRect(root, new Vector2(220f, 120f));
        root.GetComponent<Image>().color = new Color(0.09f, 0.12f, 0.18f, 0.86f);
        Text label = CreateText(root.transform, "Label", "CARD", 22, TextAnchor.MiddleCenter, new Vector2(200f, 48f));
        Text sub = CreateText(root.transform, "SubLabel", "PLAY", 14, TextAnchor.LowerCenter, new Vector2(200f, 32f));
        CardView view = root.GetComponent<CardView>();
        view.label = label;
        view.subLabel = sub;
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    static void SavePanelPrefab(string name, string title)
    {
        string path = Path.Combine(PrefabDir, name).Replace("\\", "/");
        GameObject root = BuildPanel(null, Path.GetFileNameWithoutExtension(name), Vector3.zero, new Vector2(0.4f, 0.2f), title, title);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    static GameObject BuildPanel(Transform parent, string name, Vector3 localPosition, Vector2 size, string title, string body)
    {
        GameObject root = GameObject.Find(name);
        if (root == null) root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        EnsureComponent<RectTransform>(root);
        EnsureComponent<CanvasRenderer>(root);
        EnsureComponent<Image>(root);
        if (parent != null) root.transform.SetParent(parent, false);
        SetupPanelRect(root, size);
        root.transform.localPosition = localPosition;
        root.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.12f, 0.88f);
        Text titleText = CreateText(root.transform, "Title", title, 18, TextAnchor.UpperLeft, new Vector2(size.x * 1000f - 20f, 26f));
        titleText.transform.localPosition = new Vector3(0f, 20f, 0f);
        Text bodyText = CreateText(root.transform, "Body", body, 16, TextAnchor.MiddleLeft, new Vector2(size.x * 1000f - 20f, 30f));
        bodyText.transform.localPosition = new Vector3(0f, -5f, 0f);
        if (name == "PhasePanel")
        {
            PhaseView view = root.GetComponent<PhaseView>();
            if (view == null) view = root.AddComponent<PhaseView>();
            RemoveUdonBehaviourIfAny(root);
            view.phaseText = titleText;
            view.bodyText = bodyText;
        }
        if (name == "ScorePanel")
        {
            ScorePanelView view = root.GetComponent<ScorePanelView>();
            if (view == null) view = root.AddComponent<ScorePanelView>();
            RemoveUdonBehaviourIfAny(root);
            view.phaseText = titleText;
            view.bodyText = bodyText;
        }
        if (name == "RulePanel")
        {
            RuleView view = root.GetComponent<RuleView>();
            if (view == null) view = root.AddComponent<RuleView>();
            RemoveUdonBehaviourIfAny(root);
            view.titleText = titleText;
            view.bodyText = bodyText;
        }
        if (name == "WarningPanel")
        {
            WarningPanelView view = root.GetComponent<WarningPanelView>();
            if (view == null) view = root.AddComponent<WarningPanelView>();
            RemoveUdonBehaviourIfAny(root);
            view.titleText = titleText;
            view.bodyText = bodyText;
        }
        if (name == "KafkaMonitorPanel")
        {
            LogBoardView view = root.GetComponent<LogBoardView>();
            if (view == null) view = root.AddComponent<LogBoardView>();
            RemoveUdonBehaviourIfAny(root);
            view.titleText = titleText;
            view.footerText = bodyText;
        }
        return root;
    }

    static void AttachPanels(GameObject uiRoot, BoardState boardState, GameController controller, LogStream logStream)
    {
        PhaseView phaseView = Object.FindObjectOfType<PhaseView>();
        ScorePanelView scoreView = Object.FindObjectOfType<ScorePanelView>();
        RuleView ruleView = Object.FindObjectOfType<RuleView>();
        WarningPanelView warningView = Object.FindObjectOfType<WarningPanelView>();
        LogBoardView logBoardView = Object.FindObjectOfType<LogBoardView>();
        if (phaseView != null) phaseView.state = boardState;
        if (scoreView != null) scoreView.state = boardState;
        if (ruleView != null) ruleView.state = boardState;
        if (warningView != null)
        {
            warningView.state = boardState;
            warningView.logStream = logStream;
        }
        if (logBoardView != null) logBoardView.stream = logStream;
    }

    static GameObject FindOrCreateChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) return child.gameObject;
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    static GameObject FindOrCreateSceneObject(Transform parent, string name)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null)
        {
            existing.transform.SetParent(parent, false);
            return existing;
        }
        return FindOrCreateChild(parent, name);
    }

    static GameObject FindOrCreateRuntimeObject(Transform parent, string name)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null)
        {
            existing.transform.SetParent(parent, false);
            return existing;
        }
        return FindOrCreateChild(parent, name);
    }

    static GameObject EnsureChildComponent(Transform parent, string name)
    {
        return FindOrCreateChild(parent, name);
    }

    static T EnsureComponent<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        if (component == null) component = go.AddComponent<T>();
        return component;
    }

    static void SetupWorldCanvas(GameObject go, Vector3 pos, Vector3 scale)
    {
        RectTransform rect = EnsureComponent<RectTransform>(go);
        Canvas canvas = EnsureComponent<Canvas>(go);
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;
        EnsureComponent<CanvasScaler>(go);
        EnsureComponent<GraphicRaycaster>(go);
        rect.sizeDelta = new Vector2(420f, 240f);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
    }

    static void SetupPanelRect(GameObject go, Vector2 size)
    {
        RectTransform rect = EnsureComponent<RectTransform>(go);
        rect.sizeDelta = new Vector2(size.x * 1000f, size.y * 1000f);
    }

    static Text CreateText(Transform parent, string name, string text, int fontSize, TextAnchor anchor, Vector2 size)
    {
        GameObject go = FindOrCreateText(parent, name);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(10f, 10f);
        rect.offsetMax = new Vector2(-10f, -10f);
        rect.sizeDelta = size;
        Text label = go.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = anchor;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.color = new Color(0.90f, 0.94f, 1f, 1f);
        return label;
    }

    static GameObject FindOrCreateText(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
        {
            EnsureComponent<RectTransform>(child.gameObject);
            EnsureComponent<CanvasRenderer>(child.gameObject);
            EnsureComponent<Text>(child.gameObject);
            return child.gameObject;
        }
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        return go;
    }

    static void SetupLabel(GameObject go, string text, int fontSize, TextAnchor anchor, Vector2 size)
    {
        RectTransform rect = EnsureComponent<RectTransform>(go);
        rect.sizeDelta = size;
        Text label = EnsureComponent<Text>(go);
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = anchor;
        label.color = new Color(0.90f, 0.94f, 1f, 1f);
    }

    static Text FindText(GameObject root, string name)
    {
        if (root == null) return null;
        Transform child = FindChildRecursive(root.transform, name);
        if (child == null) return null;
        return child.GetComponent<Text>();
    }

    static Transform FindChildRecursive(Transform root, string name)
    {
        if (root.name == name) return root;
        int count = root.childCount;
        for (int i = 0; i < count; i++)
        {
            Transform child = root.GetChild(i);
            Transform found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }

    static Text CreateBoardStatusText(Transform parent)
    {
        GameObject canvas = new GameObject("BoardStatusCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.transform.SetParent(parent, false);
        SetupWorldCanvas(canvas, new Vector3(0f, 118f, 0f), new Vector3(0.01f, 0.01f, 0.01f));
        return CreateText(canvas.transform, "StatusText", "BOARD", 16, TextAnchor.UpperLeft, new Vector2(320f, 30f));
    }

    static Renderer[] CreateCellMarkers(Transform parent, int count, Color tint, float spacing)
    {
        Renderer[] markers = new Renderer[count];
        int width = NetConst.GridWidth;
        int height = NetConst.GridHeight;
        float startX = -(width - 1) * spacing * 0.5f;
        float startZ = -(height - 1) * spacing * 0.5f;
        for (int i = 0; i < count; i++)
        {
            GameObject cell = FindOrCreateChild(parent, "Cell_" + i);
            if (cell.GetComponent<Collider>() != null) Object.DestroyImmediate(cell.GetComponent<Collider>());
            cell.transform.localPosition = new Vector3(startX + (i % width) * spacing, 0.01f, startZ + (i / width) * spacing);
            cell.transform.localScale = new Vector3(0.06f, 0.01f, 0.06f);
            MeshFilter filter = EnsureComponent<MeshFilter>(cell);
            if (filter.sharedMesh == null) filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            Renderer renderer = EnsureComponent<MeshRenderer>(cell);
            TintPrimitive(cell, tint);
            markers[i] = renderer;
        }
        return markers;
    }

    static Renderer[] CreateTrickMarkers(Transform parent, int count, Color tint)
    {
        Renderer[] markers = new Renderer[count];
        for (int i = 0; i < count; i++)
        {
            GameObject trick = FindOrCreateChild(parent, "Trick_" + i);
            if (trick.GetComponent<Collider>() != null) Object.DestroyImmediate(trick.GetComponent<Collider>());
            trick.transform.localPosition = new Vector3((i - 1.5f) * 0.08f, 0.02f, 0f);
            trick.transform.localScale = new Vector3(0.08f, 0.02f, 0.12f);
            MeshFilter filter = EnsureComponent<MeshFilter>(trick);
            if (filter.sharedMesh == null) filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            Renderer renderer = EnsureComponent<MeshRenderer>(trick);
            TintPrimitive(trick, tint);
            trick.SetActive(false);
            markers[i] = renderer;
        }
        return markers;
    }

    static Text[] CreateLogRows(Transform parent, GameObject uiRoot)
    {
        Text[] rows = new Text[6];
        Text first = FindText(uiRoot, "LogTitle");
        rows[0] = first;
        for (int i = 1; i < rows.Length; i++)
        {
            GameObject row = CreateText(parent, "Row_" + i, "", 14, TextAnchor.UpperLeft, new Vector2(420f, 20f)).gameObject;
            row.transform.localPosition = new Vector3(0f, -22f * i, 0f);
            rows[i] = row.GetComponent<Text>();
        }
        return rows;
    }

    static void CreateRoomBackdrop(Transform parent)
    {
        GameObject floor = FindOrCreateChild(parent, "Floor");
        floor.transform.localPosition = new Vector3(0f, -0.02f, 0f);
        floor.transform.localScale = new Vector3(9f, 0.08f, 9f);
        SetupPrimitive(floor, PrimitiveType.Cube);
        TintPrimitive(floor, new Color(0.05f, 0.06f, 0.08f, 1f));
        GameObject backWall = FindOrCreateChild(parent, "BackWall");
        backWall.transform.localPosition = new Vector3(0f, 2.0f, 4.4f);
        backWall.transform.localScale = new Vector3(9f, 4f, 0.08f);
        SetupPrimitive(backWall, PrimitiveType.Cube);
        TintPrimitive(backWall, new Color(0.06f, 0.07f, 0.10f, 1f));
        GameObject leftWall = FindOrCreateChild(parent, "LeftWall");
        leftWall.transform.localPosition = new Vector3(-4.4f, 2.0f, 0f);
        leftWall.transform.localScale = new Vector3(0.08f, 4f, 9f);
        SetupPrimitive(leftWall, PrimitiveType.Cube);
        TintPrimitive(leftWall, new Color(0.05f, 0.06f, 0.09f, 1f));
        GameObject rightWall = FindOrCreateChild(parent, "RightWall");
        rightWall.transform.localPosition = new Vector3(4.4f, 2.0f, 0f);
        rightWall.transform.localScale = new Vector3(0.08f, 4f, 9f);
        SetupPrimitive(rightWall, PrimitiveType.Cube);
        TintPrimitive(rightWall, new Color(0.05f, 0.06f, 0.09f, 1f));
        GameObject ceiling = FindOrCreateChild(parent, "Ceiling");
        ceiling.transform.localPosition = new Vector3(0f, 4.1f, 0f);
        ceiling.transform.localScale = new Vector3(9f, 0.06f, 9f);
        SetupPrimitive(ceiling, PrimitiveType.Cube);
        TintPrimitive(ceiling, new Color(0.04f, 0.05f, 0.07f, 1f));
    }

    static void CreateDeskProps(Transform parent)
    {
        GameObject desk = FindOrCreateChild(parent, "Desk");
        desk.transform.localPosition = new Vector3(-1.8f, 0.55f, -2.2f);
        desk.transform.localScale = new Vector3(2.0f, 0.12f, 0.9f);
        SetupPrimitive(desk, PrimitiveType.Cube);
        TintPrimitive(desk, new Color(0.09f, 0.08f, 0.10f, 1f));
        GameObject mug = FindOrCreateChild(parent, "Mug");
        mug.transform.localPosition = new Vector3(-1.05f, 0.72f, -1.95f);
        mug.transform.localScale = new Vector3(0.12f, 0.14f, 0.12f);
        SetupPrimitive(mug, PrimitiveType.Cylinder);
        TintPrimitive(mug, new Color(0.84f, 0.80f, 0.90f, 1f));
        GameObject keyboard = FindOrCreateChild(parent, "Keyboard");
        keyboard.transform.localPosition = new Vector3(-1.95f, 0.68f, -2.05f);
        keyboard.transform.localScale = new Vector3(0.56f, 0.05f, 0.18f);
        SetupPrimitive(keyboard, PrimitiveType.Cube);
        TintPrimitive(keyboard, new Color(0.15f, 0.17f, 0.22f, 1f));
        GameObject lamp = FindOrCreateChild(parent, "DeskLamp");
        lamp.transform.localPosition = new Vector3(-2.55f, 1.05f, -2.45f);
        lamp.transform.localScale = new Vector3(0.16f, 0.50f, 0.16f);
        SetupPrimitive(lamp, PrimitiveType.Cylinder);
        TintPrimitive(lamp, new Color(0.72f, 0.84f, 1f, 1f), true);
    }

    static void CreateMonitorProps(Transform parent)
    {
        GameObject monitor = FindOrCreateChild(parent, "Monitor");
        monitor.transform.localPosition = new Vector3(2.0f, 1.35f, -2.65f);
        monitor.transform.localScale = new Vector3(1.15f, 0.72f, 0.10f);
        SetupPrimitive(monitor, PrimitiveType.Cube);
        TintPrimitive(monitor, new Color(0.04f, 0.05f, 0.08f, 1f));
        GameObject monitorGlow = FindOrCreateChild(parent, "MonitorGlow");
        monitorGlow.transform.localPosition = new Vector3(2.0f, 1.35f, -2.58f);
        monitorGlow.transform.localScale = new Vector3(1.0f, 0.52f, 0.02f);
        SetupPrimitive(monitorGlow, PrimitiveType.Quad);
        TintPrimitive(monitorGlow, new Color(0.26f, 0.32f, 0.48f, 0.96f));
        GameObject sticky = FindOrCreateChild(parent, "StickyNote");
        sticky.transform.localPosition = new Vector3(2.55f, 1.12f, -2.58f);
        sticky.transform.localScale = new Vector3(0.22f, 0.22f, 0.02f);
        SetupPrimitive(sticky, PrimitiveType.Quad);
        TintPrimitive(sticky, new Color(0.70f, 0.64f, 0.92f, 0.96f));
    }

    static void CreateWindowScene(Transform parent)
    {
        GameObject frame = FindOrCreateChild(parent, "WindowFrame");
        frame.transform.localPosition = new Vector3(3.15f, 2.1f, 4.35f);
        frame.transform.localScale = new Vector3(1.9f, 1.4f, 0.06f);
        SetupPrimitive(frame, PrimitiveType.Cube);
        TintPrimitive(frame, new Color(0.08f, 0.10f, 0.14f, 1f));
        GameObject glass = FindOrCreateChild(parent, "WindowGlass");
        glass.transform.localPosition = new Vector3(3.15f, 2.1f, 4.32f);
        glass.transform.localScale = new Vector3(1.6f, 1.1f, 0.02f);
        SetupPrimitive(glass, PrimitiveType.Quad);
        TintPrimitive(glass, new Color(0.18f, 0.26f, 0.38f, 0.72f));
    }

    static void CreateRainPanel(Transform parent)
    {
        GameObject rain = FindOrCreateChild(parent, "RainPanelPlane");
        rain.transform.localPosition = new Vector3(-2.2f, 2.2f, 4.25f);
        rain.transform.localScale = new Vector3(2.4f, 1.8f, 0.02f);
        SetupPrimitive(rain, PrimitiveType.Quad);
        TintPrimitive(rain, new Color(0.10f, 0.16f, 0.24f, 0.60f));
    }

    static void CreateWallNotes(Transform parent)
    {
        GameObject note1 = FindOrCreateChild(parent, "Note_A");
        note1.transform.localPosition = new Vector3(-3.4f, 2.4f, 4.33f);
        note1.transform.localScale = new Vector3(0.34f, 0.20f, 0.02f);
        SetupPrimitive(note1, PrimitiveType.Quad);
        TintPrimitive(note1, new Color(0.92f, 0.82f, 0.55f, 0.94f));
        GameObject note2 = FindOrCreateChild(parent, "Note_B");
        note2.transform.localPosition = new Vector3(-2.8f, 1.85f, 4.33f);
        note2.transform.localScale = new Vector3(0.28f, 0.16f, 0.02f);
        SetupPrimitive(note2, PrimitiveType.Quad);
        TintPrimitive(note2, new Color(0.72f, 0.86f, 0.95f, 0.94f));
    }

    static void TintPrimitive(GameObject go, Color color, bool pointLight = false)
    {
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.color = color;
            renderer.sharedMaterial = material;
        }
        if (pointLight)
        {
            Light light = go.GetComponent<Light>();
            if (light == null) light = go.AddComponent<Light>();
            light.color = color;
            light.intensity = 1f;
            light.type = LightType.Point;
        }
    }

    static void SetupPrimitive(GameObject go, PrimitiveType type)
    {
        MeshFilter filter = EnsureComponent<MeshFilter>(go);
        Renderer renderer = EnsureComponent<MeshRenderer>(go);
        filter.sharedMesh = BuiltinMesh(type);
        if (type == PrimitiveType.Cylinder || type == PrimitiveType.Sphere || type == PrimitiveType.Cube || type == PrimitiveType.Quad)
        {
            if (go.GetComponent<Collider>() != null) Object.DestroyImmediate(go.GetComponent<Collider>());
        }
        renderer.enabled = true;
    }

    static void RemoveUdonBehaviourIfAny(GameObject go)
    {
        UdonBehaviour udon = go.GetComponent<UdonBehaviour>();
        if (udon != null) Object.DestroyImmediate(udon, true);
    }

    static Mesh BuiltinMesh(PrimitiveType type)
    {
        if (type == PrimitiveType.Cylinder) return Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        if (type == PrimitiveType.Sphere) return Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        if (type == PrimitiveType.Quad) return Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        return Resources.GetBuiltinResource<Mesh>("Cube.fbx");
    }
}
