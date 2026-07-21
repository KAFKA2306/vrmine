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

public static class BoardGameShowcaseBuilder
{
    const string ScenePath = "Assets/KafkaMade/VRMine/Scenes/BoardGameShowcase.unity";

    [MenuItem("VRMine/Build Board Game Showcase")]
    public static void Build()
    {
        EnsureProgramAsset<BoardState>();
        EnsureProgramAsset<GameController>();
        EnsureProgramAsset<OrapaMineGame>();
        EnsureProgramAsset<ChessGame>();
        EnsureProgramAsset<BoardGameAction>();
        EnsureProgramAsset<BoardGameShowcaseView>();
        UdonSharpProgramAsset.UdonSharpCheckAbsent();
        UdonSharpProgramAsset.CompileAllCsPrograms(true);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Material dark = MaterialAsset("Dark", new Color(0.035f, 0.055f, 0.085f));
        Material blue = MaterialAsset("Blue", new Color(0.08f, 0.30f, 0.55f));
        Material gold = MaterialAsset("Gold", new Color(0.75f, 0.48f, 0.08f));
        Material white = MaterialAsset("White", new Color(0.82f, 0.86f, 0.91f));
        Material black = MaterialAsset("Black", new Color(0.06f, 0.07f, 0.09f));
        Primitive("Floor", new Vector3(0f, -0.15f, 1f), new Vector3(18f, 0.2f, 12f), dark);
        DirectionalLight();
        CreateDescriptor();
        GameObject logic = new GameObject("GameSystems");
        BoardState board = AddUdon<BoardState>(new GameObject("TrickState"));
        board.transform.SetParent(logic.transform);
        ResetBoardArrays(board);
        GameController trick = AddUdon<GameController>(new GameObject("TrickMeisterGame"));
        trick.transform.SetParent(logic.transform);
        trick.board = board;
        OrapaMineGame orapa = AddUdon<OrapaMineGame>(new GameObject("OrapaMineGame"));
        orapa.transform.SetParent(logic.transform);
        ChessGame chess = AddUdon<ChessGame>(new GameObject("ChessGame"));
        chess.transform.SetParent(logic.transform);
        BoardGameShowcaseView view = AddUdon<BoardGameShowcaseView>(new GameObject("ShowcaseView"));
        view.transform.SetParent(logic.transform);
        view.trickGame = trick;
        view.orapaGame = orapa;
        view.chessGame = chess;
        BuildTrick(new Vector3(-5.5f, 0f, 1f), trick, view, blue, gold, white);
        BuildOrapa(new Vector3(0f, 0f, 1f), orapa, view, blue, gold, white);
        BuildChess(new Vector3(5.5f, 0f, 1f), chess, view, white, black, gold);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
    }

    static void BuildTrick(Vector3 center, GameController game, BoardGameShowcaseView view, Material blue, Material gold, Material white)
    {
        Primitive("TrickMeisterTable", center, new Vector3(4.7f, 0.25f, 5.2f), blue);
        Text title = Label("トリックマイスター", center + new Vector3(0f, 0.3f, 2.15f), 0.13f, Color.white);
        title.alignment = TextAnchor.MiddleCenter;
        view.trickStatus = Label("TrickStatus", center + new Vector3(0f, 1.8f, 2.45f), 0.065f, Color.white);
        view.trickStatus.alignment = TextAnchor.UpperCenter;
        view.trickCards = new Text[16];
        for (int i = 0; i < 16; i++)
        {
            Vector3 position = center + new Vector3(-1.75f + (i % 8) * 0.5f, 0.24f, -1.55f + (i / 8) * 0.7f);
            BoardGameAction action = Action("TrickCard_" + i, position, new Vector3(0.42f, 0.12f, 0.6f), white, 0, 0, i, game, null, null);
            view.trickCards[i] = action.GetComponentInChildren<Text>();
        }
        view.ruleCards = new Text[3];
        for (int i = 0; i < 3; i++)
        {
            BoardGameAction action = Action("RuleCard_" + i, center + new Vector3(-0.9f + i * 0.9f, 0.24f, 0.15f), new Vector3(0.7f, 0.12f, 0.8f), gold, 0, 1, i, game, null, null);
            view.ruleCards[i] = action.GetComponentInChildren<Text>();
        }
        Action("Confirm", center + new Vector3(0f, 0.24f, 1.05f), new Vector3(1.2f, 0.16f, 0.45f), gold, 0, 2, 0, game, null, null).GetComponentInChildren<Text>().text = "CONFIRM";
        Action("ResetTrick", center + new Vector3(1.55f, 0.24f, 1.05f), new Vector3(0.9f, 0.16f, 0.45f), gold, 0, 4, 0, game, null, null).GetComponentInChildren<Text>().text = "RESET";
        for (int i = 0; i < 5; i++) Action("TrickSeat_" + i, center + new Vector3(-1.6f + i * 0.8f, 0.24f, 1.55f), new Vector3(0.65f, 0.16f, 0.38f), gold, 0, 3, i, game, null, null).GetComponentInChildren<Text>().text = "SEAT " + (i + 1);
    }

    static void BuildOrapa(Vector3 center, OrapaMineGame game, BoardGameShowcaseView view, Material blue, Material gold, Material white)
    {
        Primitive("OrapaMineTable", center, new Vector3(4.7f, 0.25f, 5.2f), blue);
        Text title = Label("オラパ・マイン", center + new Vector3(0f, 0.3f, 2.15f), 0.13f, Color.white);
        title.alignment = TextAnchor.MiddleCenter;
        view.orapaStatus = Label("OrapaStatus", center + new Vector3(0f, 1.8f, 2.45f), 0.055f, Color.white);
        view.orapaStatus.alignment = TextAnchor.UpperCenter;
        float cell = 0.34f;
        Vector3 origin = center + new Vector3(-1.53f, 0.24f, -1.35f);
        for (int y = 0; y < 8; y++) for (int x = 0; x < 10; x++) Primitive("Mine_" + x + "_" + y, origin + new Vector3(x * cell, 0f, y * cell), new Vector3(cell * 0.92f, 0.05f, cell * 0.92f), (x + y & 1) == 0 ? white : blue);
        for (int i = 0; i < 10; i++)
        {
            Action("Wave_" + i, origin + new Vector3(i * cell, 0.08f, -0.32f), new Vector3(0.27f, 0.12f, 0.27f), gold, 1, 0, i, null, game, null).GetComponentInChildren<Text>().text = i.ToString();
            Action("Wave_" + (18 + 9 - i), origin + new Vector3(i * cell, 0.08f, 8 * cell + 0.02f), new Vector3(0.27f, 0.12f, 0.27f), gold, 1, 0, 18 + 9 - i, null, game, null).GetComponentInChildren<Text>().text = (18 + 9 - i).ToString();
        }
        for (int i = 0; i < 8; i++)
        {
            Action("Wave_" + (28 + 7 - i), origin + new Vector3(-0.32f, 0.08f, i * cell), new Vector3(0.27f, 0.12f, 0.27f), gold, 1, 0, 28 + 7 - i, null, game, null).GetComponentInChildren<Text>().text = (28 + 7 - i).ToString();
            Action("Wave_" + (10 + i), origin + new Vector3(10 * cell + 0.02f, 0.08f, i * cell), new Vector3(0.27f, 0.12f, 0.27f), gold, 1, 0, 10 + i, null, game, null).GetComponentInChildren<Text>().text = (10 + i).ToString();
        }
        for (int i = 0; i < 5; i++) Action("Gem_" + i, center + new Vector3(-1.6f + i * 0.8f, 0.24f, 1.45f), new Vector3(0.65f, 0.16f, 0.38f), gold, 1, 4, i, null, game, null).GetComponentInChildren<Text>().text = "GEM " + (i + 1);
        Action("GuessLeft", center + new Vector3(-1.4f, 0.24f, 1.9f), new Vector3(0.45f, 0.16f, 0.38f), gold, 1, 5, -1, null, game, null).GetComponentInChildren<Text>().text = "X-";
        Action("GuessRight", center + new Vector3(-0.9f, 0.24f, 1.9f), new Vector3(0.45f, 0.16f, 0.38f), gold, 1, 5, 1, null, game, null).GetComponentInChildren<Text>().text = "X+";
        Action("GuessDown", center + new Vector3(-0.4f, 0.24f, 1.9f), new Vector3(0.45f, 0.16f, 0.38f), gold, 1, 6, -1, null, game, null).GetComponentInChildren<Text>().text = "Y-";
        Action("GuessUp", center + new Vector3(0.1f, 0.24f, 1.9f), new Vector3(0.45f, 0.16f, 0.38f), gold, 1, 6, 1, null, game, null).GetComponentInChildren<Text>().text = "Y+";
        Action("GuessRotate", center + new Vector3(0.7f, 0.24f, 1.9f), new Vector3(0.65f, 0.16f, 0.38f), gold, 1, 7, 0, null, game, null).GetComponentInChildren<Text>().text = "ROTATE";
        Action("GuessSubmit", center + new Vector3(1.55f, 0.24f, 1.9f), new Vector3(0.85f, 0.16f, 0.38f), gold, 1, 3, 0, null, game, null).GetComponentInChildren<Text>().text = "SUBMIT";
    }

    static void BuildChess(Vector3 center, ChessGame game, BoardGameShowcaseView view, Material white, Material black, Material gold)
    {
        Primitive("ChessTable", center, new Vector3(4.7f, 0.25f, 5.2f), black);
        Text title = Label("チェス", center + new Vector3(0f, 0.3f, 2.15f), 0.13f, Color.white);
        title.alignment = TextAnchor.MiddleCenter;
        view.chessStatus = Label("ChessStatus", center + new Vector3(0f, 1.8f, 2.45f), 0.075f, Color.white);
        view.chessStatus.alignment = TextAnchor.UpperCenter;
        view.chessPieces = new Text[64];
        float cell = 0.48f;
        Vector3 origin = center + new Vector3(-1.68f, 0.24f, -1.7f);
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                int square = y * 8 + x;
                BoardGameAction action = Action("Chess_" + square, origin + new Vector3(x * cell, 0f, y * cell), new Vector3(cell, 0.1f, cell), (x + y & 1) == 0 ? white : black, 2, 0, square, null, null, game);
                view.chessPieces[square] = action.GetComponentInChildren<Text>();
            }
        Action("WhiteSeat", center + new Vector3(-1f, 0.24f, 1.65f), new Vector3(0.85f, 0.16f, 0.4f), gold, 2, 1, 0, null, null, game).GetComponentInChildren<Text>().text = "WHITE";
        Action("BlackSeat", center + new Vector3(0f, 0.24f, 1.65f), new Vector3(0.85f, 0.16f, 0.4f), gold, 2, 1, 1, null, null, game).GetComponentInChildren<Text>().text = "BLACK";
        Action("ResetChess", center + new Vector3(1f, 0.24f, 1.65f), new Vector3(0.85f, 0.16f, 0.4f), gold, 2, 2, 0, null, null, game).GetComponentInChildren<Text>().text = "RESET";
    }

    static BoardGameAction Action(string name, Vector3 position, Vector3 scale, Material material, int game, int action, int value, GameController trick, OrapaMineGame orapa, ChessGame chess)
    {
        GameObject button = Primitive(name, position, scale, material);
        BoardGameAction behaviour = AddUdon<BoardGameAction>(button);
        behaviour.game = game;
        behaviour.action = action;
        behaviour.value = value;
        behaviour.trickGame = trick;
        behaviour.orapaGame = orapa;
        behaviour.chessGame = chess;
        Text text = Label(name + "Label", position + new Vector3(0f, scale.y * 0.7f, 0f), 0.06f, Color.white);
        text.canvas.transform.SetParent(button.transform);
        text.canvas.transform.localPosition = new Vector3(0f, 0.65f, 0f);
        text.canvas.transform.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z) * 0.0007f;
        return behaviour;
    }

    static GameObject Primitive(string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gameObject.name = name;
        gameObject.transform.position = position;
        gameObject.transform.localScale = scale;
        gameObject.GetComponent<Renderer>().sharedMaterial = material;
        return gameObject;
    }

    static Text Label(string text, Vector3 position, float size, Color color)
    {
        GameObject canvasObject = new GameObject(text + "Canvas");
        canvasObject.transform.position = position;
        canvasObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        canvasObject.transform.localScale = Vector3.one * size * 0.002f;
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(600f, 220f);
        GameObject textObject = new GameObject(text);
        textObject.transform.SetParent(canvasObject.transform, false);
        Text label = textObject.AddComponent<Text>();
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        label.text = text;
        label.fontSize = 64;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.color = color;
        label.alignment = TextAnchor.MiddleCenter;
        return label;
    }

    static void DirectionalLight()
    {
        GameObject lightObject = new GameObject("Directional Light");
        lightObject.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
    }

    static void CreateDescriptor()
    {
        GameObject descriptorObject = new GameObject("VRCSceneDescriptor");
        VRCSceneDescriptor descriptor = descriptorObject.AddComponent<VRCSceneDescriptor>();
        GameObject spawn = new GameObject("SpawnPoint");
        spawn.transform.position = new Vector3(0f, 0.1f, -5f);
        descriptor.spawns = new[] { spawn.transform };
        GameObject cameraObject = new GameObject("ReferenceCamera");
        cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 10f, -9f), Quaternion.Euler(48f, 0f, 0f));
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.fieldOfView = 55f;
        descriptor.ReferenceCamera = cameraObject;
    }

    static Material MaterialAsset(string name, Color color)
    {
        string folder = "Assets/KafkaMade/VRMine/Materials";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/KafkaMade/VRMine", "Materials");
        string path = folder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    static T AddUdon<T>(GameObject gameObject) where T : UdonSharpBehaviour
    {
        return (T)(Component)gameObject.AddUdonSharpComponent(typeof(T));
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
        string path = Path.ChangeExtension(AssetDatabase.GetAssetPath(script), ".asset");
        if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path) != null) return;
        UdonSharpProgramAsset program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
        program.sourceCsScript = script;
        AssetDatabase.CreateAsset(program, path);
    }

    static void ResetBoardArrays(BoardState board)
    {
        board.playerHands = new byte[NetConst.MaxPlayers * NetConst.MaxHandSize];
        board.trickCards = new byte[NetConst.MaxPlayers];
        board.trickSeats = new byte[NetConst.MaxPlayers];
        board.occupiedPlayerIds = new int[NetConst.MaxPlayers];
        board.ruleHands = new byte[NetConst.MaxPlayers * 3];
        board.selectedRuleBySeat = new byte[NetConst.MaxPlayers];
        board.markedCards = new byte[NetConst.MaxPlayers * NetConst.MaxHandSize];
        board.reservedCards = new byte[NetConst.MaxPlayers];
        board.scores = new int[NetConst.MaxPlayers];
        board.takenTricks = new byte[NetConst.MaxPlayers];
    }
}
