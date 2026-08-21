using System;
using System.IO;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRC.SDK3.Components;

public static class PerspectiveCageBuilder
{
    public const string ScenePath = "Assets/KafkaMade/VRMine/Puzzles/PerspectiveCage/Scenes/PerspectiveCage.unity";
    const string SpecPath = "config/perspective-cage.json";
    const float ZoneSpacing = 12f;

    [Serializable] class PuzzleSpec { public string id; public string title_ja; public string[] hints; }
    [Serializable] class IntroRule { public string description_ja; }
    [Serializable] class WorldSpec { public PuzzleSpec[] puzzles; public IntroRule intro_rule; }

    [MenuItem("VRMine/Perspective Cage/Build Canonical Scene")]
    public static void Build()
    {
        WorldSpec spec = LoadSpec();
        EnsureFolder("Assets/KafkaMade/VRMine/Puzzles/PerspectiveCage/Scenes");
        EnsureFolder("Assets/KafkaMade/VRMine/Puzzles/PerspectiveCage/Materials");
        EnsureProgramAsset<PerspectiveCageController>();
        EnsureProgramAsset<PerspectiveCageInteractable>();
        UdonSharpProgramAsset.UdonSharpCheckAbsent();
        UdonSharpProgramAsset.CompileAllCsPrograms(true);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Material shell = MaterialAsset("Shell", new Color(0.07f, 0.09f, 0.12f));
        Material clue = MaterialAsset("Clue", new Color(0.72f, 0.76f, 0.80f));
        Material accent = MaterialAsset("Accent", new Color(0.18f, 0.45f, 0.66f));
        Material active = MaterialAsset("Active", new Color(0.72f, 0.46f, 0.12f));
        Material blocker = MaterialAsset("Blocker", new Color(0.28f, 0.10f, 0.10f));

        GameObject world = new GameObject("PerspectiveCageWorld");
        BuildLighting(world.transform);
        CreateDescriptor(world.transform);
        for (int zone = 0; zone < 7; zone++) BuildZoneShell(world.transform, zone, shell);

        PerspectiveCageController controller = AddUdon<PerspectiveCageController>(new GameObject("PerspectiveCageController"));
        controller.transform.SetParent(world.transform);
        controller.progressionDoors = new GameObject[4];
        controller.resultPanels = new GameObject[4];
        controller.hintPanels = new GameObject[15];
        controller.wrongFeedbacks = new GameObject[5];
        controller.markerObjects = new GameObject[4];
        controller.markerHomes = new Transform[4];
        controller.socketTargets = new Transform[4];

        BuildIntro(world.transform, spec, shell);
        BuildP01(world.transform, controller, ZoneCenter(1), spec.puzzles[0], clue, accent, active);
        BuildP02(world.transform, controller, ZoneCenter(2), spec.puzzles[1], clue, accent, active);
        BuildP03(world.transform, controller, ZoneCenter(3), spec.puzzles[2], clue, accent, active);
        BuildP04(world.transform, controller, ZoneCenter(4), spec.puzzles[3], clue, accent, active);
        BuildP05(world.transform, controller, ZoneCenter(5), spec.puzzles[4], clue, accent, active);
        BuildClearArea(world.transform, controller, ZoneCenter(6), active);

        for (int i = 0; i < 4; i++)
        {
            float z = (i + 1.5f) * ZoneSpacing;
            controller.progressionDoors[i] = Cube("ProgressionDoor_P0" + (i + 1), new Vector3(0f, 1.5f, z), new Vector3(4.5f, 3f, 0.25f), blocker, world.transform);
        }
        controller.clearDoor = Cube("ClearDoor", new Vector3(0f, 1.5f, 5.5f * ZoneSpacing), new Vector3(4.5f, 3f, 0.25f), blocker, world.transform);

        controller.ApplyPresentation();
        EditorSceneManager.SaveScene(scene, ScenePath);
        RegisterBuildScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Perspective Cage canonical scene built: " + ScenePath);
    }

    static WorldSpec LoadSpec()
    {
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), SpecPath);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Perspective Cage spec not found", fullPath);
        WorldSpec spec = JsonUtility.FromJson<WorldSpec>(File.ReadAllText(fullPath));
        if (spec == null || spec.puzzles == null || spec.puzzles.Length != 5) throw new InvalidDataException("Perspective Cage spec must contain exactly five puzzles");
        for (int i = 0; i < 5; i++) if (spec.puzzles[i].hints == null || spec.puzzles[i].hints.Length != 3) throw new InvalidDataException("Each puzzle must contain exactly three hints");
        return spec;
    }

    static void BuildIntro(Transform parent, WorldSpec spec, Material shell)
    {
        Vector3 center = ZoneCenter(0);
        Label("Title", "視点の檻\nCAGE OF PERSPECTIVE", center + new Vector3(0f, 2.7f, 3.7f), 0.075f, parent);
        Label("IntroRule", "入口の規則\n" + spec.intro_rule.description_ja, center + new Vector3(0f, 1.5f, 3.68f), 0.042f, parent);
        Cube("IntroRuleFrame", center + new Vector3(0f, 1.9f, 3.9f), new Vector3(8f, 3f, 0.12f), shell, parent);
        string[] symbols = { "TRIANGLE", "CIRCLE", "SQUARE", "DIAMOND", "CROSS" };
        for (int i = 0; i < 5; i++) Label("IntroSymbol" + i, symbols[i], center + new Vector3(-3.2f + i * 1.6f, 0.65f, 3.65f), 0.022f, parent);
    }

    static void BuildP01(Transform parent, PerspectiveCageController controller, Vector3 center, PuzzleSpec spec, Material clue, Material accent, Material active)
    {
        Heading(parent, center, spec);
        Vector3 observer = center + new Vector3(0f, 1.6f, -3.6f);
        Cube("P01ObservationSpot", center + new Vector3(0f, 0.04f, -3.6f), new Vector3(1.2f, 0.08f, 1.2f), accent, parent);
        Label("P01ObservationLabel", "VIEW", center + new Vector3(0f, 0.12f, -3.6f), 0.025f, parent, Quaternion.Euler(90f, 0f, 0f));
        Vector3[] target = {
            center + new Vector3(0f, 2.9f, 2.2f), center + new Vector3(1.25f, 1.8f, 2.2f),
            center + new Vector3(0f, 0.7f, 2.2f), center + new Vector3(-1.25f, 1.8f, 2.2f)
        };
        float[] depth = { 0.72f, 0.90f, 1.08f, 1.28f };
        for (int i = 0; i < 4; i++)
        {
            Vector3 a = observer + (target[i] - observer) * depth[i];
            Vector3 b = observer + (target[(i + 1) % 4] - observer) * depth[i];
            WorldBar("P01Fragment" + i, a, b, clue, parent);
        }
        string[] choices = { "TRIANGLE", "CIRCLE", "SQUARE", "DIAMOND" };
        for (int i = 0; i < 4; i++) Button("P01Choice" + i, choices[i], center + new Vector3(-3f + i * 2f, 0.3f, 4f), 0, PerspectiveCageController.ActionInput, i, controller, active, parent);
        Feedback(parent, controller, center, 0, spec, active);
    }

    static void BuildP02(Transform parent, PerspectiveCageController controller, Vector3 center, PuzzleSpec spec, Material clue, Material accent, Material active)
    {
        Heading(parent, center, spec);
        float[] heights = { 1.6f, 0.7f, 2.05f, 1.15f };
        string[] names = { "A", "B", "C", "D" };
        for (int i = 0; i < 4; i++)
        {
            float x = -3f + i * 2f;
            Cube("P02Object" + names[i], center + new Vector3(x, heights[i] * 0.5f, 0f), new Vector3(0.8f, heights[i], 0.8f), clue, parent);
            Label("P02ObjectLabel" + i, names[i], center + new Vector3(x, heights[i] + 0.3f, -0.45f), 0.034f, parent);
            Button("P02Button" + names[i], names[i], center + new Vector3(x, 0.3f, 3.7f), 1, PerspectiveCageController.ActionInput, i, controller, active, parent);
        }
        for (int i = 0; i < 4; i++)
        {
            float h = 0.35f + i * 0.28f;
            Cube("P02ReferenceStep" + (i + 1), center + new Vector3(-3f + i * 0.75f, h * 0.5f, -3.7f), new Vector3(0.65f, h, 1.1f), accent, parent);
            Label("P02Ticks" + i, new string('|', i + 1), center + new Vector3(-3f + i * 0.75f, h + 0.18f, -3.2f), 0.022f, parent);
        }
        Feedback(parent, controller, center, 1, spec, active);
    }

    static void BuildP03(Transform parent, PerspectiveCageController controller, Vector3 center, PuzzleSpec spec, Material clue, Material accent, Material active)
    {
        Heading(parent, center, spec);
        string[] markerNames = { "TRIANGLE", "CIRCLE", "SQUARE", "DIAMOND" };
        string[] socketNames = { "WEST", "NORTH", "EAST", "SOUTH" };
        Vector3[] homes = {
            center + new Vector3(-3f, 1.3f, -1.8f), center + new Vector3(-1f, 1.3f, -1.8f),
            center + new Vector3(1f, 1.3f, -1.8f), center + new Vector3(3f, 1.3f, -1.8f)
        };
        Vector3[] targets = {
            center + new Vector3(-3f, 1.3f, 2.0f), center + new Vector3(0f, 1.3f, 3.1f),
            center + new Vector3(3f, 1.3f, 2.0f), center + new Vector3(0f, 1.3f, 0.9f)
        };
        for (int i = 0; i < 4; i++)
        {
            GameObject home = Anchor("P03Home" + i, homes[i], parent);
            GameObject socketTarget = Anchor("P03SocketTarget" + i, targets[i], parent);
            controller.markerHomes[i] = home.transform;
            controller.socketTargets[i] = socketTarget.transform;
            controller.markerObjects[i] = Icon("P03Marker" + i, homes[i], i, clue, accent, parent);
            Icon("P03Socket" + i, targets[i], i, clue, accent, parent);
            Button("P03Select" + i, markerNames[i], homes[i] + new Vector3(0f, -0.9f, -0.5f), 2, PerspectiveCageController.ActionInput, i, controller, active, parent);
            Vector3 buttonPos = targets[i] + new Vector3(0f, -0.9f, 0.55f);
            Button("P03SocketButton" + i, socketNames[i], buttonPos, 2, PerspectiveCageController.ActionSocket, i, controller, active, parent);
        }
        Label("P03Cue", "MATCH SHAPE + NOTCH DIRECTION", center + new Vector3(0f, 3.1f, 0f), 0.03f, parent);
        Feedback(parent, controller, center, 2, spec, active);
    }

    static void BuildP04(Transform parent, PerspectiveCageController controller, Vector3 center, PuzzleSpec spec, Material clue, Material accent, Material active)
    {
        Heading(parent, center, spec);
        Cube("P04ReferencePanel", center + new Vector3(0f, 2.5f, 2.8f), new Vector3(8f, 1.5f, 0.12f), clue, parent);
        Label("P04Reference", "REFERENCE\nTRIANGLE  CIRCLE  SQUARE  DIAMOND  CROSS", center + new Vector3(0f, 2.5f, 2.65f), 0.034f, parent);
        Cube("P04CurrentPanel", center + new Vector3(0f, 1.2f, 2.8f), new Vector3(7f, 1.1f, 0.12f), accent, parent);
        Label("P04Current", "THIS ROOM\nTRIANGLE  CIRCLE  SQUARE  DIAMOND", center + new Vector3(0f, 1.2f, 2.65f), 0.034f, parent);
        string[] symbols = { "TRIANGLE", "CIRCLE", "SQUARE", "DIAMOND", "CROSS" };
        for (int i = 0; i < 5; i++) Button("P04Choice" + i, symbols[i], center + new Vector3(-3.6f + i * 1.8f, 0.3f, -2.5f), 3, PerspectiveCageController.ActionInput, i, controller, active, parent);
        Feedback(parent, controller, center, 3, spec, active);
    }

    static void BuildP05(Transform parent, PerspectiveCageController controller, Vector3 center, PuzzleSpec spec, Material clue, Material accent, Material active)
    {
        Heading(parent, center, spec);
        Cube("P05ResultsPanel", center + new Vector3(0f, 2.5f, 3f), new Vector3(8f, 1.6f, 0.12f), clue, parent);
        Label("P05Results", "RESULTS\n1 DIAMOND   2 CIRCLE   3 SQUARE   4 CROSS", center + new Vector3(0f, 2.5f, 2.85f), 0.038f, parent);
        Cube("P05RulePanel", center + new Vector3(0f, 1.2f, 3f), new Vector3(5f, 1f, 0.12f), accent, parent);
        Label("P05Rule", "READ: 3 → 1 → 4 → 2", center + new Vector3(0f, 1.2f, 2.85f), 0.042f, parent);
        string[] symbols = { "TRIANGLE", "CIRCLE", "SQUARE", "DIAMOND", "CROSS" };
        for (int i = 0; i < 5; i++) Button("P05Input" + i, symbols[i], center + new Vector3(-3.6f + i * 1.8f, 0.3f, -2.5f), 4, PerspectiveCageController.ActionInput, i, controller, active, parent);
        Feedback(parent, controller, center, 4, spec, active);
    }

    static void Feedback(Transform parent, PerspectiveCageController controller, Vector3 center, int puzzle, PuzzleSpec spec, Material active)
    {
        Button("HintButton_P0" + (puzzle + 1), "HINT", center + new Vector3(4f, 0.3f, -4.2f), puzzle, PerspectiveCageController.ActionHint, 0, controller, active, parent);
        for (int hint = 0; hint < 3; hint++)
        {
            GameObject panel = Label("Hint_P0" + (puzzle + 1) + "_" + (hint + 1), "HINT " + (hint + 1) + "\n" + spec.hints[hint], center + new Vector3(0f, 3.1f - hint * 0.75f, -4.75f), 0.027f, parent);
            panel.SetActive(false);
            controller.hintPanels[puzzle * 3 + hint] = panel;
        }
        GameObject wrong = Label("Wrong_P0" + (puzzle + 1), "NOT YET", center + new Vector3(0f, 0.9f, -4.75f), 0.038f, parent);
        wrong.SetActive(false);
        controller.wrongFeedbacks[puzzle] = wrong;
        if (puzzle < 4)
        {
            string[] outputs = { "DIAMOND", "CIRCLE", "SQUARE", "CROSS" };
            GameObject result = Label("Result_P0" + (puzzle + 1), "RESULT: " + outputs[puzzle], center + new Vector3(0f, 0.45f, 4.75f), 0.034f, parent);
            result.SetActive(false);
            controller.resultPanels[puzzle] = result;
        }
    }

    static void BuildClearArea(Transform parent, PerspectiveCageController controller, Vector3 center, Material active)
    {
        GameObject clear = Label("ClearPresentation", "CLEAR\n視点を変えると、意味も変わる。", center + new Vector3(0f, 2.2f, 2.8f), 0.065f, parent);
        clear.SetActive(false);
        controller.clearPresentation = clear;
        Button("ResetStation", "RESET WORLD", center + new Vector3(0f, 0.3f, -2.5f), -1, PerspectiveCageController.ActionReset, 0, controller, active, parent);
    }

    static PerspectiveCageInteractable Button(string name, string text, Vector3 position, int puzzle, int action, int value, PerspectiveCageController controller, Material material, Transform parent)
    {
        GameObject button = Cube(name, position, new Vector3(1.45f, 0.32f, 0.8f), material, parent);
        PerspectiveCageInteractable interactable = AddUdon<PerspectiveCageInteractable>(button);
        interactable.controller = controller;
        interactable.puzzleIndex = puzzle;
        interactable.action = action;
        interactable.value = value;
        Label(name + "Label", text, position + new Vector3(0f, 0.3f, -0.43f), 0.021f, parent);
        return interactable;
    }

    static GameObject Icon(string name, Vector3 position, int kind, Material body, Material notch, Transform parent)
    {
        GameObject root = Anchor(name, position, parent);
        if (kind == 0)
        {
            LocalBar(root.transform, new Vector3(-0.38f, -0.28f, 0f), new Vector3(0f, 0.38f, 0f), body);
            LocalBar(root.transform, new Vector3(0f, 0.38f, 0f), new Vector3(0.38f, -0.28f, 0f), body);
            LocalBar(root.transform, new Vector3(0.38f, -0.28f, 0f), new Vector3(-0.38f, -0.28f, 0f), body);
        }
        else if (kind == 1) LocalPrimitive("Circle", PrimitiveType.Sphere, root.transform, Vector3.zero, new Vector3(0.72f, 0.72f, 0.18f), body, Quaternion.identity);
        else LocalPrimitive(kind == 2 ? "Square" : "Diamond", PrimitiveType.Cube, root.transform, Vector3.zero, new Vector3(0.72f, 0.72f, 0.18f), body, kind == 3 ? Quaternion.Euler(0f, 0f, 45f) : Quaternion.identity);

        Vector3[] notches = { new Vector3(0f, 0.52f, 0f), new Vector3(0.52f, 0f, 0f), new Vector3(0f, -0.52f, 0f), new Vector3(-0.52f, 0f, 0f) };
        LocalPrimitive("OrientationNotch", PrimitiveType.Cube, root.transform, notches[kind], new Vector3(0.16f, 0.16f, 0.22f), notch, Quaternion.identity);
        return root;
    }

    static void LocalBar(Transform parent, Vector3 a, Vector3 b, Material material)
    {
        Vector3 delta = b - a;
        LocalPrimitive("Edge", PrimitiveType.Cube, parent, (a + b) * 0.5f, new Vector3(delta.magnitude, 0.09f, 0.12f), material, Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg));
    }

    static GameObject LocalPrimitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material, Quaternion rotation)
    {
        GameObject gameObject = GameObject.CreatePrimitive(type);
        gameObject.name = name;
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = position;
        gameObject.transform.localScale = scale;
        gameObject.transform.localRotation = rotation;
        gameObject.GetComponent<Renderer>().sharedMaterial = material;
        return gameObject;
    }

    static void WorldBar(string name, Vector3 a, Vector3 b, Material material, Transform parent)
    {
        Vector3 delta = b - a;
        GameObject bar = Cube(name, (a + b) * 0.5f, new Vector3(delta.magnitude, 0.12f, 0.12f), material, parent);
        bar.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    static void Heading(Transform parent, Vector3 center, PuzzleSpec spec)
    {
        Label("Heading_" + spec.id, spec.id.ToUpperInvariant() + "  " + spec.title_ja, center + new Vector3(0f, 3.8f, -4.8f), 0.044f, parent);
    }

    static void BuildZoneShell(Transform parent, int zone, Material material)
    {
        Vector3 center = ZoneCenter(zone);
        Cube("Floor_" + zone, center + new Vector3(0f, -0.12f, 0f), new Vector3(10f, 0.24f, ZoneSpacing), material, parent);
        Cube("WallLeft_" + zone, center + new Vector3(-5f, 2.5f, 0f), new Vector3(0.2f, 5f, ZoneSpacing), material, parent);
        Cube("WallRight_" + zone, center + new Vector3(5f, 2.5f, 0f), new Vector3(0.2f, 5f, ZoneSpacing), material, parent);
    }

    static Vector3 ZoneCenter(int zone) { return new Vector3(0f, 0f, zone * ZoneSpacing); }

    static GameObject Anchor(string name, Vector3 position, Transform parent)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent);
        gameObject.transform.position = position;
        return gameObject;
    }

    static GameObject Cube(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
    {
        GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gameObject.name = name;
        gameObject.transform.SetParent(parent);
        gameObject.transform.position = position;
        gameObject.transform.localScale = scale;
        gameObject.GetComponent<Renderer>().sharedMaterial = material;
        return gameObject;
    }

    static GameObject Label(string name, string text, Vector3 position, float scale, Transform parent, Quaternion? rotation = null)
    {
        GameObject canvasObject = new GameObject(name + "Canvas");
        canvasObject.transform.SetParent(parent);
        canvasObject.transform.position = position;
        canvasObject.transform.rotation = rotation ?? Quaternion.identity;
        canvasObject.transform.localScale = Vector3.one * scale * 0.002f;
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(850f, 250f);
        GameObject textObject = new GameObject(name + "Text");
        textObject.transform.SetParent(canvasObject.transform, false);
        Text label = textObject.AddComponent<Text>();
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        label.text = text;
        label.fontSize = 58;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        return canvasObject;
    }

    static void BuildLighting(Transform parent)
    {
        GameObject lightObject = new GameObject("Directional Light");
        lightObject.transform.SetParent(parent);
        lightObject.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.05f;
    }

    static void CreateDescriptor(Transform parent)
    {
        GameObject descriptorObject = new GameObject("VRCSceneDescriptor");
        descriptorObject.transform.SetParent(parent);
        VRCSceneDescriptor descriptor = descriptorObject.AddComponent<VRCSceneDescriptor>();
        GameObject spawn = Anchor("SpawnPoint", new Vector3(0f, 0.1f, -4.5f), parent);
        descriptor.spawns = new[] { spawn.transform };
        GameObject cameraObject = Anchor("ReferenceCamera", new Vector3(0f, 7f, -8f), parent);
        cameraObject.transform.rotation = Quaternion.Euler(32f, 0f, 0f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.fieldOfView = 60f;
        descriptor.ReferenceCamera = cameraObject;
    }

    static Material MaterialAsset(string name, Color color)
    {
        string folder = "Assets/KafkaMade/VRMine/Puzzles/PerspectiveCage/Materials";
        EnsureFolder(folder);
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
            if (candidate != null && candidate.GetClass() == typeof(T)) script = candidate;
        }
        if (script == null) throw new InvalidOperationException("MonoScript not found for " + typeof(T).Name);
        string path = Path.ChangeExtension(AssetDatabase.GetAssetPath(script), ".asset");
        if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path) != null) return;
        UdonSharpProgramAsset program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
        program.sourceCsScript = script;
        AssetDatabase.CreateAsset(program, path);
    }

    static void EnsureFolder(string folder)
    {
        if (Directory.Exists(folder)) return;
        Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
    }

    static void RegisterBuildScene()
    {
        EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
        for (int i = 0; i < current.Length; i++) if (current[i].path == ScenePath) return;
        EditorBuildSettingsScene[] updated = new EditorBuildSettingsScene[current.Length + 1];
        for (int i = 0; i < current.Length; i++) updated[i] = current[i];
        updated[current.Length] = new EditorBuildSettingsScene(ScenePath, true);
        EditorBuildSettings.scenes = updated;
    }
}
