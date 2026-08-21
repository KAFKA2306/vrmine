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

    [Serializable]
    class PuzzleSpec { public string id; public string title_ja; public string[] hints; }
    [Serializable]
    class IntroRule { public string description_ja; }
    [Serializable]
    class PerspectiveCageSpec { public PuzzleSpec[] puzzles; public IntroRule intro_rule; }

    [MenuItem("VRMine/Perspective Cage/Build Canonical Scene")]
    public static void Build()
    {
        PerspectiveCageSpec spec = LoadSpec();
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

        BuildIntro(world.transform, spec, shell, clue);
        BuildP01(world.transform, controller, ZoneCenter(1), spec.puzzles[0], clue, accent, active);
        BuildP02(world.transform, controller, ZoneCenter(2), spec.puzzles[1], clue, accent, active);
        BuildP03(world.transform, controller, ZoneCenter(3), spec.puzzles[2], clue, accent, active);
        BuildP04(world.transform, controller, ZoneCenter(4), spec.puzzles[3], clue, accent, active);
        BuildP05(world.transform, controller, ZoneCenter(5), spec.puzzles[4], clue, accent, active);
        BuildClearArea(world.transform, controller, ZoneCenter(6), active);

        for (int i = 0; i < 4; i++)
        {
            float z = (i + 1.5f) * ZoneSpacing;
            controller.progressionDoors[i] = Primitive("ProgressionDoor_P0" + (i + 1), new Vector3(0f, 1.5f, z), new Vector3(4.5f, 3f, 0.25f), blocker, world.transform);
        }
        controller.clearDoor = Primitive("ClearDoor", new Vector3(0f, 1.5f, 5.5f * ZoneSpacing), new Vector3(4.5f, 3f, 0.25f), blocker, world.transform);

        controller.ApplyPresentation();
        EditorSceneManager.SaveScene(scene, ScenePath);
        RegisterBuildScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Perspective Cage canonical scene built: " + ScenePath);
    }

    static PerspectiveCageSpec LoadSpec()
    {
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), SpecPath);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Perspective Cage spec not found", fullPath);
        PerspectiveCageSpec spec = JsonUtility.FromJson<PerspectiveCageSpec>(File.ReadAllText(fullPath));
        if (spec == null || spec.puzzles == null || spec.puzzles.Length != 5) throw new InvalidDataException("Perspective Cage spec must contain exactly five puzzles");
        for (int i = 0; i < 5; i++) if (spec.puzzles[i].hints == null || spec.puzzles[i].hints.Length != 3) throw new InvalidDataException("Each puzzle must contain exactly three hints");
        return spec;
    }

    static void BuildIntro(Transform parent, PerspectiveCageSpec spec, Material shell, Material clue)
    {
        Vector3 center = ZoneCenter(0);
        WallLabel("Title", "視点の檻\nCAGE OF PERSPECTIVE", center + new Vector3(0f, 2.6f, 3.8f), 0.075f, Color.white, parent);
        string rule = spec.intro_rule == null ? "3 → 1 → 4 → 2" : spec.intro_rule.description_ja;
        WallLabel("IntroRule", "入口の規則\n" + rule, center + new Vector3(0f, 1.5f, 3.75f), 0.045f, Color.white, parent);
        Primitive("IntroRuleFrame", center + new Vector3(0f, 1.8f, 3.9f), new Vector3(7f, 2.5f, 0.12f), shell, parent);
        string[] symbols = { "TRIANGLE", "CIRCLE", "SQUARE", "DIAMOND", "CROSS" };
        for (int i = 0; i < symbols.Length; i++) WallLabel("IntroSymbol" + i, symbols[i], center + new Vector3(-3.2f + i * 1.6f, 0.7f, 3.72f), 0.025f, Color.white, parent);
    }

    static void BuildP01(Transform parent, PerspectiveCageController controller, Vector3 center, PuzzleSpec spec, Material clue, Material accent, Material active)
    {
        AddRoomHeading(parent, center, spec);
        Vector3 observer = center + new Vector3(0f, 1.6f, -3.6f);
        Primitive("P01ObservationSpot", center + new Vector3(0f, 0.04f, -3.6f), new Vector3(1.2f, 0.08f, 1.2f), accent, parent);
        WallLabel("P01ObservationLabel", "VIEW", center + new Vector3(0f, 0.12f, -3.6f), 0.025f, Color.white, parent, Quaternion.Euler(90f, 0f, 0f));

        Vector3[] virtualVertices = {
            center + new Vector3(0f, 2.9f, 2.2f),
            center + new Vector3(1.25f, 1.8f, 2.2f),
            center + new Vector3(0f, 0.7f, 2.2f),
            center + new Vector3(-1.25f, 1.8f, 2.2f)
        };
        float[] depthFactors = { 0.72f, 0.90f, 1.08f, 1.28f };
        for (int i = 0; i < 4; i++)
        {
            Vector3 a = observer + (virtualVertices[i] - observer) * depthFactors[i];
            Vector3 b = observer + (virtualVertices[(i + 1) % 4] - observer) * depthFactors[i];
            Bar("P01Fragment" + i, a, b, 0.12f, clue, parent);
        }

        string[] names = { "TRIANGLE", "CIRCLE", "SQUARE", "DIAMOND" };
        for (int i = 0; i < 4; i++) InteractionButton("P01Choice" + i, names[i], center + new Vector3(-3f + i * 2f, 0.3f, 4f), 0, PerspectiveCageController.ActionInput, i, controller, active, parent);
        BuildRoomFeedback(parent, controller, center, 0, spec, active);
    }

    static void BuildP02(Transform parent, PerspectiveCageController controller, Vector3 center, PuzzleSpec spec, Material clue, Material accent, Material active)
    {
        AddRoomHeading(parent, center, spec);
        float[] heights = { 1.6f, 0.7f, 2.05f, 1.15f };
        string[] labels = { "A", "B", "C", "D" };
        for (int i = 0; i < 4; i++)
        {
            float x = -3f + i * 2f;
            Primitive("P02Object" + labels[i], center + new Vector3(x, heights[i] * 0.5f, 0f), new Vector3(0.8f, heights[i], 0.8f), clue, parent);
            WallLabel("P02Label" + labels[i], labels[i], center + new Vector3(x, heights[i] + 0.3f, -0.45f), 0.035f, Color.white, parent);
            InteractionButton("P02Button" + labels[i], labels[i], center + new Vector3(x, 0.3f, 3.7f), 1, PerspectiveCageController.ActionInput, i, controller, active, parent);
        }
        for (int i = 0; i < 4; i++)
        {
            float h = 0.35f + i * 0.28f;
            Primitive("P02ReferenceStep" + (i + 1), center + new Vector3(-3f + i * 0.75f, h * 0.5f, -3.7f), new Vector3(0.65f, h, 1.1f), accent, parent);
            WallLabel("P02Ticks" + (i + 1), new string('|', i + 1), center + new Vector3(-3f + i * 0.75f, h + 0.2f, -3.2f), 0.022f, Color.white, parent);
        }
        BuildRoomFeedback(parent, controller, center, 1, spec, active);
    }

    static void BuildP03(Transform parent, PerspectiveCageController controller, Vector3 center, PuzzleSpec spec, Material clue, Material accent, Material active)
    {
        AddRoomHeading(parent, center, spec);
        string[] markerNames = { "TRIANGLE", "CIRCLE", "SQUARE", "DIAMOND" };
        string[] socketNames = { "WEST", "NORTH", "EAST", "SOUTH" };
        Vector3[] homes = {
            center + new Vector3(-3f, 1f, -1.5f), center + new Vector3(-1f, 1f, -1.5f),
            center + new Vector3(1f, 1f, -1.5f), center + new Vector3(3f, 1f, -1.5f)
        };
        Vector3[] targets = {
            center + new Vector3(-3f, 1f, 2f), center + new Vector3(-1f, 1f, 2f),
            center + new Vector3(1f, 1f, 2f), center + new Vector3(3f, 1f, 2f)
        };
        for (int i = 0; i < 4; i++)
        {
            GameObject home = new GameObject("P03Home" + i);
            home.transform.SetParent(parent);
            home.transform.position = homes[i];
            controller.markerHomes[i] = home.transform;

            GameObject target = new GameObject("P03SocketTarget" + i);
            target.transform.SetParent(parent);
            target.transform.position = targets[i];
            controller.socketTargets[i] = target.transform;

            GameObject marker = CreateMarker("P03Marker" + i, homes[i], i, clue, accent, parent);
            controller.markerObjects[i] = marker;
            InteractionButton("P03Select" + i, markerNames[i], homes[i] + new Vector3(0f, -0.7f, -0.8f), 2, PerspectiveCageController.ActionInput, i, controller, active, parent);
            CreateSocketVisual("P03Socket" + i, targets[i], i, clue, parent);
            InteractionButton("P03SocketButton" + i, socketNames[i], targets[i] + new Vector3(0f, -0.7f, 0.8f), 2, PerspectiveCageController.ActionSocket, i, controller, active, parent);
        }
        BuildRoomFeedback(parent, controller, center, 2, spec, active);
    }

    static void BuildP04(Transform parent, PerspectiveCageController controller, Vector3 center, PuzzleSpec spec, Material clue, Material accent, Material active)
    {
        AddRoomHeading(parent, center, spec);
        WallLabel("P04Reference", "REFERENCE\nTRIANGLE  CIRCLE  SQUARE  DIAMOND  CROSS", center + new Vector3(0f, 2.5f, 2.5f), 0.036f, Color.white, parent);
        Primitive("P04ReferencePanel", center + new Vector3(0f, 2.4f, 2.7f), new Vector3(8f, 1.5f, 0.12f), clue, parent);
        WallLabel("P04Current", "THIS ROOM\nTRIANGLE  CIRCLE  SQUARE  DIAMOND", center + new Vector3(0f, 1.2f, 2.45f), 0.036f, Color.white, parent);
        Primitive("P04CurrentPanel", center + new Vector3(0f, 1.2f, 2.65f), new Vector3(7f, 1.2f, 0.12f), accent, parent);
        string[] symbols = { "TRIANGLE", "CIRCLE", "SQUARE", "DIAMOND", "CROSS" };
        for (int i = 0; i < 5; i++) InteractionButton("P04Choice" + i, symbols[i], center + new Vector3(-3.6f + i * 1.8f, 0.3f, -2.5f), 3, PerspectiveCageController.ActionInput, i, controller, active, parent);
        BuildRoomFeedback(parent, controller, center, 3, spec, active);
    }

    static void BuildP05(Transform parent, PerspectiveCageController controller, Vector3 center, PuzzleSpec spec, Material clue, Material accent, Material active)
    {
        AddRoomHeading(parent, center, spec);
        WallLabel("P05Results", "RESULTS\n1 DIAMOND   2 CIRCLE   3 SQUARE   4 CROSS", center + new Vector3(0f, 2.5f, 2.8f), 0.04f, Color.white, parent);
        Primitive("P05ResultsPanel", center + new Vector3(0f, 2.4f, 3f), new Vector3(8f, 1.6f, 0.12f), clue, parent);
        WallLabel("P05Rule", "READ: 3 → 1 → 4 → 2", center + new Vector3(0f, 1.25f, 2.75f), 0.045f, Color.white, parent);
        Primitive("P05RulePanel", center + new Vector3(0f, 1.2f, 2.95f), new Vector3(5f, 1f, 0.12f), accent, parent);
        string[] symbols = { "TRIANGLE", "CIRCLE", "SQUARE", "DIAMOND", "CROSS" };
        for (int i = 0; i < 5; i++) InteractionButton("P05Input" + i, symbols[i], center + new Vector3(-3.6f + i * 1.8f, 0.3f, -2.5f), 4, PerspectiveCageController.ActionInput, i, controller, active, parent);
        BuildRoomFeedback(parent, controller, center, 4, spec, active);
    }

    static void BuildRoomFeedback(Transform parent, PerspectiveCageController controller, Vector3 center, int puzzle, PuzzleSpec spec, Material active)
    {
        InteractionButton("HintButton_P0" + (puzzle + 1), "HINT", center + new Vector3(4f, 0.3f, -4.2f), puzzle, PerspectiveCageController.ActionHint, 0, controller, active, parent);
        for (int hint = 0; hint < 3; hint++)
        {
            GameObject panel = WallLabel("Hint_P0" + (puzzle + 1) + "_" + (hint + 1), "HINT " + (hint + 1) + "\n" + spec.hints[hint], center + new Vector3(0f, 3.1f - hint * 0.75f, -4.75f), 0.028f, Color.white, parent);
            panel.SetActive(false);
            controller.hintPanels[puzzle * 3 + hint] = panel;
        }
        GameObject wrong = WallLabel("Wrong_P0" + (puzzle + 1), "NOT YET", center + new Vector3(0f, 1f, -4.75f), 0.04f, Color.white, parent);
        wrong.SetActive(false);
        controller.wrongFeedbacks[puzzle] = wrong;
        if (puzzle < 4)
        {
            string[] outputs = { "DIAMOND", "CIRCLE", "SQUARE", "CROSS" };
            GameObject result = WallLabel("Result_P0" + (puzzle + 1), "RESULT: " + outputs[puzzle], center + new Vector3(0f, 0.45f, 4.75f), 0.035f, Color.white, parent);
            result.SetActive(false);
            controller.resultPanels[puzzle] = result;
        }
    }

    static void BuildClearArea(Transform parent, PerspectiveCageController controller, Vector3 center, Material active)
    {
        GameObject clear = WallLabel("ClearPresentation", "CLEAR\n視点を変えると、意味も変わる。", center + new Vector3(0f, 2.2f, 2.8f), 0.07f, Color.white, parent);
        clear.SetActive(false);
        controller.clearPresentation = clear;
        InteractionButton("ResetStation", "RESET WORLD", center + new Vector3(0f, 0.3f, -2.5f), -1, PerspectiveCageController.ActionReset, 0, controller, active, parent);
    }

    static void AddRoomHeading(Transform parent, Vector3 center, PuzzleSpec spec)
    {
        WallLabel("Heading_" + spec.id, spec.id.ToUpperInvariant() + "  " + spec.title_ja, center + new Vector3(0f, 3.8f, -4.8f), 0.045f, Color.white, parent);
    }

    static PerspectiveCageInteractable InteractionButton(string name, string label, Vector3 position, int puzzle, int action, int value, PerspectiveCageController controller, Material material, Transform parent)
    {
        GameObject button = Primitive(name, position, new Vector3(1.45f, 0.32f, 0.8f), material, parent);
        PerspectiveCageInteractable interactable = AddUdon<PerspectiveCageInteractable>(button);
        interactable.controller = controller;
        interactable.puzzleIndex = puzzle;
        interactable.action = action;
        interactable.value = value;
        WallLabel(name + "Label", label, position + new Vector3(0f, 0.23f, -0.42f), 0.022f, Color.white, button.transform);
        return interactable;
    }

    static GameObject CreateMarker(string name, Vector3 position, int kind, Material clue, Material accent, Transform parent)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent);
        root.transform.position = position;
        if (kind == 0)
        {
            LocalBar(root.transform, new Vector3(-0.35f, -0.25f, 0f), new Vector3(0f, 0.35f, 0f), clue);
            LocalBar(root.transform, new Vector3(0f, 0.35f, 0f), new Vector3(0.35f, -0.25f, 0f), clue);
            LocalBar(root.transform, new Vector3(0.35f, -0.25f, 0f), new Vector3(-0.35f, -0.25f, 0f), clue);
        }
        else if (kind == 1)
        {
            GameObject sphere = LocalPrimitive("Circle", PrimitiveType.Sphere, root.transform, Vector3.zero, new Vector3(0.7f, 0.7f, 0.18f), clue);
            sphere.transform.localScale = new Vector3(0.7f, 0.7f, 0.18f);
        }
        else
        {
            GameObject cube = LocalPrimitive(kind == 2 ? "Square" : "Diamond", PrimitiveType.Cube, root.transform, Vector3.zero, new Vector3(0.7f, 0.7f, 0.18f), clue);
            if (kind == 3) cube.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }
        LocalPrimitive("OrientationNotch", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.48f, 0f), new Vector3(0.16f, 0.16f, 0.22f), accent);
        return root;
    }

    static void CreateSocketVisual(string name, Vector3 position, int kind, Material material, Transform parent)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent);
        root.transform.position = position;
        GameObject outline = LocalPrimitive("SocketOutline", PrimitiveType.Cube, root.transform, Vector3.zero, new Vector3(1f, 1f, 0.08f), material);
        outline.transform.localRotation = kind == 3 ? Quaternion.Euler(0f, 0f, 45f) : Quaternion.identity;
        LocalPrimitive("SocketNotch", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.18f, 0.18f, 0.12f), material);
    }

    static void LocalBar(Transform parent, Vector3 a, Vector3 b, Material material)
    {
        Vector3 delta = b - a;
        GameObject bar = LocalPrimitive("Edge", PrimitiveType.Cube, parent, (a + b) * 0.5f, new Vector3(delta.magnitude, 0.09f, 0.12f), material);
        bar.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    static GameObject LocalPrimitive(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject gameObject = GameObject.CreatePrimitive(type);
        gameObject.name = name;
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = localPosition;
        gameObject.transform.localScale = localScale;
        gameObject.GetComponent<Renderer>().sharedMaterial = material;
        return gameObject;
    }

    static void Bar(string name, Vector3 a, Vector3 b, float thickness, Material material, Transform parent)
    {
        Vector3 delta = b - a;
        GameObject bar = Primitive(name, (a + b) * 0.5f, new Vector3(delta.magnitude, thickness, thickness), material, parent);
        bar.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    static void BuildZoneShell(Transform parent, int zone, Material material)
    {
        Vector3 center = ZoneCenter(zone);
        Primitive("Floor_" + zone, center + new Vector3(0f, -0.12f, 0f), new Vector3(10f, 0.24f, ZoneSpacing), material, parent);
        Primitive("WallLeft_" + zone, center + new Vector3(-5f, 2.5f, 0f), new Vector3(0.2f, 5f, ZoneSpacing), material, parent);
        Primitive("WallRight_" + zone, center + new Vector3(5f, 2.5f, 0f), new Vector3(0.2f, 5f, ZoneSpacing), material, parent);
    }

    static Vector3 ZoneCenter(int zone)
    {
        return new Vector3(0f, 0f, zone * ZoneSpacing);
    }

    static GameObject Primitive(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
    {
        GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gameObject.name = name;
        gameObject.transform.SetParent(parent);
        gameObject.transform.position = position;
        gameObject.transform.localScale = scale;
        gameObject.GetComponent<Renderer>().sharedMaterial = material;
        return gameObject;
    }

    static GameObject WallLabel(string name, string text, Vector3 position, float scale, Color color, Transform parent, Quaternion? rotation = null)
    {
        GameObject canvasObject = new GameObject(name + "Canvas");
        canvasObject.transform.SetParent(parent);
        canvasObject.transform.position = position;
        canvasObject.transform.rotation = rotation ?? Quaternion.identity;
        canvasObject.transform.localScale = Vector3.one * scale * 0.002f;
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(800f, 240f);
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
        label.color = color;
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
        GameObject spawn = new GameObject("SpawnPoint");
        spawn.transform.SetParent(parent);
        spawn.transform.SetPositionAndRotation(new Vector3(0f, 0.1f, -4.5f), Quaternion.identity);
        descriptor.spawns = new[] { spawn.transform };
        GameObject cameraObject = new GameObject("ReferenceCamera");
        cameraObject.transform.SetParent(parent);
        cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 7f, -8f), Quaternion.Euler(32f, 0f, 0f));
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
