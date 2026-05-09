using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRC.SDK3.Components;

public static class VisualBuilder
{
    sealed class TextureAssets
    {
        public Texture2D icons;
        public Texture2D guide;
        public Texture2D gems;
        public Texture2D actions;
    }

    const string ScenePath = "Assets/KafkaMade/VRMine/Scenes/MVP.unity";
    const string RootName = "VRMineVisualRoot";
    private static readonly Dictionary<string, Material> Materials = new();

    [MenuItem("VRMine/build_visuals")]
    public static void BuildVisuals()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) return;

        CleanupLights();

        GameObject root = GameObject.Find(RootName);
        if (root != null) UnityEngine.Object.DestroyImmediate(root);

        root = new GameObject(RootName);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        Palette palette = CreatePalette();
        BuildVrcWorld(root.transform, palette);
        BuildSystems(root.transform);
        BuildGameplay(root.transform, palette);
        BuildVisual(root.transform, palette);
        BuildUi(root.transform, palette);
        BuildAudio(root.transform);
        BuildLighting(root.transform, palette);
        BuildDebug(root.transform);
        BuildEditorOnly(root.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root;
    }

    [MenuItem("VRMine/validate_visuals")]
    public static void ValidateVisuals()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) return;

        bool ok =
            GameObject.Find(RootName) != null &&
            GameObject.Find("VRCWorld") != null &&
            GameObject.Find("Gameplay") != null &&
            GameObject.Find("Visual") != null &&
            GameObject.Find("UI") != null &&
            GameObject.Find("Lighting") != null;

        Debug.Log("VRMine validate_visuals " + (ok ? "OK" : "NG"));
    }

    static void BuildVrcWorld(Transform parent, Palette palette)
    {
        GameObject world = CreateNode(parent, "VRCWorld");
        world.transform.localPosition = Vector3.zero;
        world.transform.localRotation = Quaternion.identity;
        world.transform.localScale = Vector3.one;

        VRCSceneDescriptor descriptor = world.GetComponent<VRCSceneDescriptor>();
        if (descriptor == null) descriptor = world.AddComponent<VRCSceneDescriptor>();

        GameObject referenceCamera = CreateNode(world.transform, "ReferenceCamera");
        referenceCamera.transform.localPosition = new Vector3(0f, 1.7f, -5.5f);
        referenceCamera.transform.localRotation = Quaternion.identity;
        Camera camera = referenceCamera.GetComponent<Camera>();
        if (camera == null) camera = referenceCamera.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = palette.floor;
        camera.nearClipPlane = 0.03f;
        camera.farClipPlane = 200f;

        GameObject spawnRoot = CreateNode(world.transform, "SpawnRoot");
        Vector3[] spawns =
        {
            new Vector3(0f, 1.2f, -6f),
            new Vector3(-2f, 1.2f, -6f),
            new Vector3(2f, 1.2f, -6f),
            new Vector3(0f, 1.2f, -8f)
        };
        for (int i = 0; i < spawns.Length; i++)
        {
            GameObject spawn = CreateNode(spawnRoot.transform, "Spawn_" + i);
            spawn.transform.localPosition = spawns[i];
            spawn.transform.localRotation = Quaternion.identity;
        }

        SerializedObject so = new SerializedObject(descriptor);
        AssignTransformArray(so, "spawns", new[]
        {
            spawnRoot.transform.Find("Spawn_0"),
            spawnRoot.transform.Find("Spawn_1"),
            spawnRoot.transform.Find("Spawn_2"),
            spawnRoot.transform.Find("Spawn_3")
        });
        SetProperty(so, "spawnRadius", 0.5f);
        SetProperty(so, "spawnOrder", 1);
        SetProperty(so, "spawnOrientation", 0);
        SetProperty(so, "RespawnHeightY", -25f);
        SetProperty(so, "ObjectBehaviourAtRespawnHeight", 0);
        SetProperty(so, "ReferenceCamera", camera);
        SetProperty(so, "ForbidUserPortals", false);
        so.ApplyModifiedPropertiesWithoutUndo();

        AddOptionalComponent(world, "VRC.SDK3.Components.VRCPipelineManager", "VRCPipelineManager");
    }

    static void BuildSystems(Transform parent)
    {
        GameObject systems = CreateNode(parent, "Systems");
        GameObject udon = CreateNode(systems.transform, "Udon");
        string[] names = { "GameController", "BoardState", "PlayerClient", "WaveSimulator", "LogStream" };
        for (int i = 0; i < names.Length; i++) CreateNode(udon.transform, names[i]);
    }

    static void BuildGameplay(Transform parent, Palette palette)
    {
        GameObject gameplay = CreateNode(parent, "Gameplay");
        BuildBoardRoot(gameplay.transform, palette);
        BuildCardRoot(gameplay.transform, palette);
        BuildBlockRoot(gameplay.transform, palette);
        BuildPickupRoot(gameplay.transform, palette);
        BuildNetworkRoot(gameplay.transform);
        BuildInteractionRoot(gameplay.transform, palette);
    }

    static void BuildBoardRoot(Transform parent, Palette palette)
    {
        GameObject root = CreateNode(parent, "BoardRoot");
        root.transform.localPosition = Vector3.zero;
        CreatePrimitive(root.transform, PrimitiveType.Cube, "Board", new Vector3(0f, 0.025f, 0f), new Vector3(10.2f, 0.05f, 8.2f), Quaternion.identity, palette.boardBase, false, "BoardBase");
        GameObject cells = CreateNode(root.transform, "Cells");
        float startX = -4.5f;
        float startZ = -3.5f;
        for (int z = 0; z < 8; z++)
        {
            for (int x = 0; x < 10; x++)
            {
                int index = z * 10 + x;
                Vector3 position = new Vector3(startX + x, 0.05f, startZ + z);
                Color color = CellColor(index, x, z, palette);
                CreatePrimitive(cells.transform, PrimitiveType.Cube, "Cell_" + index, position, new Vector3(0.9f, 0.05f, 0.9f), Quaternion.identity, color, false, CellMaterialKey(index, x, z));
            }
        }
        GameObject highlights = CreateNode(root.transform, "CellHighlights");
        CreatePrimitive(highlights.transform, PrimitiveType.Sphere, "Highlight_A", new Vector3(-4.1f, 0.18f, -3.1f), new Vector3(0.12f, 0.12f, 0.12f), Quaternion.identity, palette.highlight, true, "CellHighlight");
        CreatePrimitive(highlights.transform, PrimitiveType.Sphere, "Highlight_B", new Vector3(4.1f, 0.18f, -3.1f), new Vector3(0.12f, 0.12f, 0.12f), Quaternion.identity, palette.highlight, true, "CellHighlight");
        CreatePrimitive(highlights.transform, PrimitiveType.Sphere, "Highlight_C", new Vector3(-4.1f, 0.18f, 3.1f), new Vector3(0.12f, 0.12f, 0.12f), Quaternion.identity, palette.highlight, true, "CellHighlight");
        CreatePrimitive(highlights.transform, PrimitiveType.Sphere, "Highlight_D", new Vector3(4.1f, 0.18f, 3.1f), new Vector3(0.12f, 0.12f, 0.12f), Quaternion.identity, palette.highlight, true, "CellHighlight");
    }

    static void BuildCardRoot(Transform parent, Palette palette, TextureAssets textures)
    {
        GameObject root = CreateNode(parent, "CardRoot");
        Vector3[] positions =
        {
            new Vector3(-1.4f, 1.05f, 1.3f),
            new Vector3(-0.7f, 1.08f, 1.4f),
            new Vector3(0f, 1.1f, 1.45f),
            new Vector3(0.7f, 1.08f, 1.4f),
            new Vector3(1.4f, 1.05f, 1.3f)
        };
        for (int i = 0; i < positions.Length; i++)
        {
            Quaternion rotation = Quaternion.Euler(0f, -20f + i * 10f, 0f);
            GameObject card = CreatePrimitive(root.transform, PrimitiveType.Cube, "Card_" + i, positions[i], new Vector3(0.75f, 0.04f, 1.1f), rotation, palette.card, false, "Card");
            CreatePrimitive(card.transform, PrimitiveType.Cube, "CardStripe", new Vector3(0f, 0.02f, -0.48f), new Vector3(0.65f, 0.01f, 0.04f), Quaternion.identity, palette.cardStripe, false, "CardStripe");
        }
    }

    static void BuildBlockRoot(Transform parent, Palette palette, TextureAssets textures)
    {
        GameObject root = CreateNode(parent, "BlockRoot");
        byte[] colors = { NetConst.ColorRed, NetConst.ColorBlue, NetConst.ColorYellow, 8, 8 };
        Vector3[] positions =
        {
            new Vector3(-3.2f, 0.6f, 1.8f),
            new Vector3(-2.7f, 0.8f, 1.6f),
            new Vector3(-2.2f, 1.0f, 1.8f),
            new Vector3(-1.7f, 0.7f, 1.5f),
            new Vector3(-1.2f, 0.9f, 1.7f)
        };
        for (int i = 0; i < positions.Length; i++)
        {
            Color color = ColorForBlock(colors[i], palette);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Block_" + i, positions[i], new Vector3(0.34f, 0.34f, 0.34f), Quaternion.Euler(0f, 15f * i, 0f), color, false, "Block_" + i);
        }
    }

    static void BuildPickupRoot(Transform parent, Palette palette)
    {
        GameObject root = CreateNode(parent, "PickupRoot");
        Vector3[] positions =
        {
            new Vector3(-3f, 1f, 2f),
            new Vector3(-2f, 1f, 2f),
            new Vector3(-1f, 1f, 2f)
        };
        Color[] colors = { palette.pickupRed, palette.pickupBlue, palette.pickupYellow };
        for (int i = 0; i < positions.Length; i++)
        {
            GameObject pickup = CreatePrimitive(root.transform, PrimitiveType.Cube, "Pickup_" + i, positions[i], new Vector3(0.32f, 0.32f, 0.32f), Quaternion.identity, colors[i], false, "Pickup_" + i);
            Rigidbody body = AddOrGet<Rigidbody>(pickup);
            body.useGravity = true;
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            AddOptionalComponent(pickup, "VRC.SDK3.Components.VRCObjectSync", "VRCObjectSync");
            AddOptionalComponent(pickup, "VRC.SDK3.Components.VRCPickup", "VRCPickup", "VRC_Pickup");
        }
    }

    static void BuildNetworkRoot(Transform parent)
    {
        GameObject root = CreateNode(parent, "NetworkRoot");
        string[] names = { "GameStateSync", "ScoreSync", "TimerSync", "OwnershipManager" };
        for (int i = 0; i < names.Length; i++) CreateNode(root.transform, names[i]);
    }

    static void BuildInteractionRoot(Transform parent, Palette palette, TextureAssets textures)
    {
        GameObject root = CreateNode(parent, "InteractionRoot");
        GameObject declare = CreatePrimitive(root.transform, PrimitiveType.Cube, "DeclareButton", new Vector3(2.6f, 0.9f, 2.4f), new Vector3(0.6f, 0.16f, 0.28f), Quaternion.Euler(0f, -20f, 0f), palette.declare, false, "DeclareButton");
        GameObject label = CreateBillboardText(declare.transform, "Label", "DECLARE", 22, new Vector2(300f, 64f));
        label.transform.localPosition = new Vector3(0f, 0.16f, 0f);
    }

    static void BuildVisual(Transform parent, Palette palette)
    {
        GameObject root = CreateNode(parent, "Visual");
        BuildFloor(root.transform, palette);
        BuildWalls(root.transform, palette);
        BuildBoardFrame(root.transform, palette);
        BuildCellHighlights(root.transform, palette);
        BuildKafkaGuide(root.transform, palette);
        BuildSimpleDecorations(root.transform, palette);
        BuildFx(root.transform, palette);
        BuildSkyboxAnchor(root.transform);
    }

    static void BuildFloor(Transform parent, Palette palette)
    {
        CreatePrimitive(parent, PrimitiveType.Plane, "Floor", new Vector3(0f, 0f, 0f), new Vector3(2f, 1f, 2f), Quaternion.identity, palette.floor, false, "Floor");
    }

    static void BuildWalls(Transform parent, Palette palette)
    {
        CreatePrimitive(parent, PrimitiveType.Cube, "NorthWall", new Vector3(0f, 2f, 10f), new Vector3(20f, 4f, 0.2f), Quaternion.identity, palette.wall, false, "Wall");
        CreatePrimitive(parent, PrimitiveType.Cube, "SouthWall", new Vector3(0f, 2f, -10f), new Vector3(20f, 4f, 0.2f), Quaternion.identity, palette.wall, false, "Wall");
        CreatePrimitive(parent, PrimitiveType.Cube, "EastWall", new Vector3(10f, 2f, 0f), new Vector3(20f, 4f, 0.2f), Quaternion.Euler(0f, 90f, 0f), palette.wall, false, "Wall");
        CreatePrimitive(parent, PrimitiveType.Cube, "WestWall", new Vector3(-10f, 2f, 0f), new Vector3(20f, 4f, 0.2f), Quaternion.Euler(0f, 90f, 0f), palette.wall, false, "Wall");
    }

    static void BuildBoardFrame(Transform parent, Palette palette)
    {
        CreatePrimitive(parent, PrimitiveType.Cube, "BoardFrame", new Vector3(0f, 0.06f, 0f), new Vector3(10.8f, 0.08f, 8.8f), Quaternion.identity, palette.boardFrame, false, "BoardFrame");
    }

    static void BuildCellHighlights(Transform parent, Palette palette)
    {
        GameObject root = CreateNode(parent, "CellHighlights");
        CreatePrimitive(root.transform, PrimitiveType.Sphere, "CellGlow_0", new Vector3(-1.6f, 0.12f, -1.6f), new Vector3(0.14f, 0.14f, 0.14f), Quaternion.identity, palette.highlight, true, "CellHighlight");
        CreatePrimitive(root.transform, PrimitiveType.Sphere, "CellGlow_1", new Vector3(1.6f, 0.12f, -1.6f), new Vector3(0.14f, 0.14f, 0.14f), Quaternion.identity, palette.highlight, true, "CellHighlight");
        CreatePrimitive(root.transform, PrimitiveType.Sphere, "CellGlow_2", new Vector3(-1.6f, 0.12f, 1.6f), new Vector3(0.14f, 0.14f, 0.14f), Quaternion.identity, palette.highlight, true, "CellHighlight");
        CreatePrimitive(root.transform, PrimitiveType.Sphere, "CellGlow_3", new Vector3(1.6f, 0.12f, 1.6f), new Vector3(0.14f, 0.14f, 0.14f), Quaternion.identity, palette.highlight, true, "CellHighlight");
    }

    static void BuildKafkaGuide(Transform parent, Palette palette)
    {
        GameObject root = CreateNode(parent, "KafkaGuide");
        root.transform.localPosition = new Vector3(-4f, 0f, -3f);
        root.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
        CreatePrimitive(root.transform, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.74f, 0f), new Vector3(0.45f, 0.8f, 0.3f), Quaternion.identity, palette.guideBody, true, "GuideBody");
        CreatePrimitive(root.transform, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.38f, 0f), new Vector3(0.42f, 0.42f, 0.42f), Quaternion.identity, palette.guideHead, true, "GuideHead");
        CreatePrimitive(root.transform, PrimitiveType.Capsule, "HairLeft", new Vector3(-0.18f, 1.42f, 0f), new Vector3(0.12f, 0.7f, 0.12f), Quaternion.Euler(0f, 0f, -14f), palette.guideHairLeft, true, "GuideHairLeft");
        CreatePrimitive(root.transform, PrimitiveType.Capsule, "HairRight", new Vector3(0.18f, 1.42f, 0f), new Vector3(0.12f, 0.7f, 0.12f), Quaternion.Euler(0f, 0f, 14f), palette.guideHairRight, true, "GuideHairRight");

        GameObject eyes = CreateNode(root.transform, "Eyes");
        CreatePrimitive(eyes.transform, PrimitiveType.Sphere, "Eye_L", new Vector3(-0.08f, 1.37f, 0.18f), new Vector3(0.04f, 0.04f, 0.04f), Quaternion.identity, palette.guideEye, true, "GuideEye");
        CreatePrimitive(eyes.transform, PrimitiveType.Sphere, "Eye_R", new Vector3(0.08f, 1.37f, 0.18f), new Vector3(0.04f, 0.04f, 0.04f), Quaternion.identity, palette.guideEye, true, "GuideEye");

        GameObject pin = CreateNode(root.transform, "Hairpin");
        CreatePrimitive(pin.transform, PrimitiveType.Cube, "Pin_A", new Vector3(0.18f, 1.72f, 0.08f), new Vector3(0.05f, 0.05f, 0.18f), Quaternion.Euler(0f, 10f, 28f), palette.guidePin, true, "GuidePin");
        CreatePrimitive(pin.transform, PrimitiveType.Cube, "Pin_B", new Vector3(0.12f, 1.79f, 0.08f), new Vector3(0.05f, 0.05f, 0.18f), Quaternion.Euler(0f, -20f, -8f), palette.guidePin, true, "GuidePin");
        CreatePrimitive(pin.transform, PrimitiveType.Cube, "Pin_C", new Vector3(0.24f, 1.79f, 0.08f), new Vector3(0.05f, 0.05f, 0.18f), Quaternion.Euler(0f, 26f, -8f), palette.guidePin, true, "GuidePin");
    }

    static void BuildSimpleDecorations(Transform parent, Palette palette)
    {
        GameObject root = CreateNode(parent, "SimpleDecorations");
        CreatePrimitive(root.transform, PrimitiveType.Cube, "DeskLampBase", new Vector3(2.8f, 0.62f, -2.3f), new Vector3(0.22f, 0.04f, 0.22f), Quaternion.identity, palette.decorAccent, false, "DecorAccent");
        CreatePrimitive(root.transform, PrimitiveType.Cylinder, "DeskLampStem", new Vector3(2.8f, 1.02f, -2.3f), new Vector3(0.08f, 0.45f, 0.08f), Quaternion.identity, palette.decorAccent, true, "DecorAccent");
        CreatePrimitive(root.transform, PrimitiveType.Cube, "StickyNote", new Vector3(3.2f, 1.22f, -2.28f), new Vector3(0.28f, 0.18f, 0.02f), Quaternion.identity, palette.note, false, "Note");
        CreatePrimitive(root.transform, PrimitiveType.Cube, "Mug", new Vector3(-1.8f, 0.68f, -2.1f), new Vector3(0.16f, 0.18f, 0.16f), Quaternion.identity, palette.mug, false, "Mug");
    }

    static void BuildFx(Transform parent, Palette palette)
    {
        GameObject root = CreateNode(parent, "FX");
        CreatePrimitive(root.transform, PrimitiveType.Sphere, "GlowOrb", new Vector3(0f, 2.8f, -0.8f), new Vector3(0.18f, 0.18f, 0.18f), Quaternion.identity, palette.glow, true, "GlowOrb");
        CreatePrimitive(root.transform, PrimitiveType.Cube, "NeonBar", new Vector3(0f, 2.1f, 2.6f), new Vector3(2.2f, 0.12f, 0.08f), Quaternion.identity, palette.neon, true, "NeonBar");
    }

    static void BuildSkyboxAnchor(Transform parent)
    {
        CreateNode(parent, "SkyboxAnchor");
    }

    static void BuildUi(Transform parent, Palette palette)
    {
        GameObject root = CreateNode(parent, "UI");
        GameObject worldCanvas = new GameObject("WorldCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasRenderer), typeof(Image));
        worldCanvas.transform.SetParent(root.transform, false);
        worldCanvas.transform.localPosition = Vector3.zero;
        worldCanvas.transform.localRotation = Quaternion.identity;
        worldCanvas.transform.localScale = Vector3.one * 0.01f;
        Canvas canvas = worldCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 5;
        RectTransform rect = worldCanvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1200f, 300f);
        worldCanvas.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.05f, 0.2f);
        RemoveCollider(worldCanvas);
        CreateText(worldCanvas.transform, "WorldTitle", "KAFKA BLUE LAB", 30, TextAnchor.MiddleCenter, new Vector2(800f, 60f), new Vector2(0f, -20f), new Color(0.78f, 0.88f, 1f, 1f));
        BuildWorldPanel(root.transform, "RulePanel", new Vector3(4f, 1.8f, 0f), Quaternion.Euler(0f, -60f, 0f), new Vector2(2.2f, 1.2f), palette.rulePanel, "CURRENT RULE", "HIDDEN");
        BuildLogPanel(root.transform, "LogPanel", new Vector3(-4f, 1.8f, 0f), Quaternion.Euler(0f, 60f, 0f), new Vector2(2.2f, 1.2f), palette.logPanel);
        BuildWorldPanel(root.transform, "WarningPanel", new Vector3(0f, 1.6f, -1.8f), Quaternion.identity, new Vector2(2.0f, 0.7f), palette.warningPanel, "WARNING", "NO WARNINGS");
        BuildWorldPanel(root.transform, "ScorePanel", new Vector3(0f, 2.6f, -1.8f), Quaternion.identity, new Vector2(2.0f, 0.8f), palette.scorePanel, "SCORE", "0 : 0");
        BuildWorldPanel(root.transform, "TimerPanel", new Vector3(0f, 3.3f, -1.8f), Quaternion.identity, new Vector2(1.4f, 0.55f), palette.timerPanel, "TIMER", "00:00");
    }

    static void BuildWorldPanel(Transform parent, string name, Vector3 position, Quaternion rotation, Vector2 size, Color panelColor, string title, string body)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);
        panel.transform.localPosition = position;
        panel.transform.localRotation = rotation;
        panel.transform.localScale = Vector3.one * 0.01f;

        Canvas canvas = panel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(size.x * 100f, size.y * 100f);

        Image image = panel.GetComponent<Image>();
        image.color = panelColor;
        RemoveCollider(panel);

        CreateText(panel.transform, "Title", title, 24, TextAnchor.UpperLeft, new Vector2(size.x * 100f - 20f, 34f), new Vector2(10f, -8f), new Color(0.98f, 0.94f, 0.60f, 1f));
        CreateText(panel.transform, "Body", body, 18, TextAnchor.UpperLeft, new Vector2(size.x * 100f - 20f, 60f), new Vector2(10f, -54f), new Color(0.90f, 0.94f, 0.98f, 1f));
    }

    static void BuildLogPanel(Transform parent, string name, Vector3 position, Quaternion rotation, Vector2 size, Color panelColor)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);
        panel.transform.localPosition = position;
        panel.transform.localRotation = rotation;
        panel.transform.localScale = Vector3.one * 0.01f;

        Canvas canvas = panel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(size.x * 100f, size.y * 100f);

        Image image = panel.GetComponent<Image>();
        image.color = panelColor;
        RemoveCollider(panel);

        CreateText(panel.transform, "Title", "LOG", 24, TextAnchor.UpperLeft, new Vector2(size.x * 100f - 20f, 34f), new Vector2(10f, -8f), new Color(0.55f, 0.92f, 1f, 1f));
        string[] rows =
        {
            "PROGRAM asset is not valid",
            "SanitizeProxyBehaviours",
            "NullReferenceException",
            "has not been fully setup",
            "Force-enabling Fog",
            "VRMine validate_scene OK"
        };
        for (int i = 0; i < rows.Length; i++)
        {
            CreateText(panel.transform, "Row_" + i, rows[i], 16, TextAnchor.UpperLeft, new Vector2(size.x * 100f - 20f, 22f), new Vector2(10f, -48f - (i * 20f)), i == 5 ? new Color(0.55f, 0.92f, 1f, 1f) : new Color(0.90f, 0.94f, 0.98f, 1f));
        }
    }

    static void BuildAudio(Transform parent)
    {
        GameObject root = CreateNode(parent, "Audio");
        CreateAudioSource(root.transform, "BGMSource", new Vector3(0f, 1.6f, 0f), true, 0.8f);
        CreateAudioSource(root.transform, "UISource", new Vector3(0f, 1.6f, -1f), false, 0.8f);
        CreateAudioSource(root.transform, "AmbientSource", new Vector3(0f, 2.4f, -4f), true, 0.8f);
        CreateAudioSource(root.transform, "PickupSource", new Vector3(0f, 1.2f, 2f), false, 0.8f);
    }

    static void CreateAudioSource(Transform parent, string name, Vector3 position, bool loop, float blend)
    {
        GameObject go = CreateNode(parent, name);
        go.transform.localPosition = position;
        AudioSource source = go.GetComponent<AudioSource>();
        if (source == null) source = go.AddComponent<AudioSource>();
        source.loop = loop;
        source.playOnAwake = false;
        source.spatialBlend = blend;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = 1f;
        source.maxDistance = 20f;
    }

    static void BuildLighting(Transform parent, Palette palette)
    {
        GameObject root = CreateNode(parent, "Lighting");
        GameObject directional = CreateNode(root.transform, "DirectionalLight");
        Light dir = AddOrGet<Light>(directional);
        dir.type = LightType.Directional;
        dir.color = new Color(0.74f, 0.84f, 1f, 1f);
        dir.intensity = 1.15f;
        directional.transform.localRotation = Quaternion.Euler(50f, -30f, 0f);

        GameObject fill = CreateNode(root.transform, "FillLight");
        Light fillLight = AddOrGet<Light>(fill);
        fillLight.type = LightType.Point;
        fillLight.color = new Color(0.56f, 0.70f, 1f, 1f);
        fillLight.intensity = 0.45f;
        fillLight.range = 18f;
        fill.transform.localPosition = new Vector3(0f, 4f, -4f);

        GameObject probe = CreateNode(root.transform, "ReflectionProbe");
        ReflectionProbe reflectionProbe = AddOrGet<ReflectionProbe>(probe);
        reflectionProbe.size = new Vector3(20f, 8f, 20f);
        reflectionProbe.transform.localPosition = new Vector3(0f, 2f, 0f);
        reflectionProbe.intensity = 1f;
    }

    static void BuildDebug(Transform parent)
    {
        GameObject root = CreateNode(parent, "Debug");
        GameObject clientSim = CreateNode(root.transform, "ClientSimAnchor");
        clientSim.tag = "EditorOnly";
        GameObject gizmoRoot = CreateNode(root.transform, "GizmoRoot");
        gizmoRoot.tag = "EditorOnly";
        GameObject validationRoot = CreateNode(root.transform, "ValidationRoot");
        validationRoot.tag = "EditorOnly";
    }

    static void BuildEditorOnly(Transform parent)
    {
        GameObject root = CreateNode(parent, "EditorOnly");
        root.tag = "EditorOnly";
    }

    static GameObject CreateNode(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing.gameObject;
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    static GameObject CreatePrimitive(Transform parent, PrimitiveType type, string name, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Color color, bool removeCollider, string materialKey = null)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        go.transform.localRotation = localRotation;
        if (removeCollider || (name != "Floor" && !name.EndsWith("Wall"))) RemoveCollider(go);
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null) Tint(go, color);
        return go;
    }

    static void Tint(GameObject go, Color color)
    {
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;

        string key = ColorUtility.ToHtmlStringRGBA(color);
        if (!Materials.TryGetValue(key, out Material material))
        {
            Shader shader = Shader.Find("VRChat/Mobile/Unlit");
            if (shader == null) shader = Shader.Find("Standard");
            material = new Material(shader);
            material.color = color;
            Materials[key] = material;
        }
        renderer.sharedMaterial = material;
    }

    static void RemoveCollider(GameObject go)
    {
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
    }

    static Text CreateText(Transform parent, string name, string value, int size, TextAnchor anchor, Vector2 panelSize, Vector2 localPosition, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.up;
        rect.anchorMax = Vector2.up;
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = panelSize;
        rect.anchoredPosition = localPosition;
        Text text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = size;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = color;
        return text;
    }

    static GameObject CreateBillboardText(Transform parent, string name, string value, int size, Vector2 panelSize)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * 0.01f;
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = panelSize;
        RemoveCollider(go);
        Text text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.98f, 0.94f, 0.60f, 1f);
        return go;
    }

    static void AssignTransformArray(SerializedObject so, string propertyName, Transform[] transforms)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null || !property.isArray) return;
        property.arraySize = transforms.Length;
        for (int i = 0; i < transforms.Length; i++)
        {
            SerializedProperty element = property.GetArrayElementAtIndex(i);
            if (element != null) element.objectReferenceValue = transforms[i];
        }
    }

    static void SetProperty(SerializedObject so, string name, float value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null) property.floatValue = value;
    }

    static void SetProperty(SerializedObject so, string name, int value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null) property.intValue = value;
    }

    static void SetProperty(SerializedObject so, string name, bool value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null) property.boolValue = value;
    }

    static void SetProperty(SerializedObject so, string name, UnityEngine.Object value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null) property.objectReferenceValue = value;
    }

    static T AddOrGet<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        if (component == null) component = go.AddComponent<T>();
        return component;
    }

    static Component AddOptionalComponent(GameObject go, params string[] typeNames)
    {
        Type type = FindType(typeNames);
        if (type == null) return null;
        Component existing = go.GetComponent(type);
        if (existing != null) return existing;
        return go.AddComponent(type);
    }

    static Type FindType(params string[] names)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            for (int j = 0; j < assemblies.Length; j++)
            {
                Type type = assemblies[j].GetType(name);
                if (type != null) return type;
            }
        }
        return null;
    }

    static Color CellColor(int index, int x, int z, Palette palette)
    {
        if (x == 5 && z == 4) return palette.cellSelected;
        if ((x + z) % 4 == 0) return palette.cellGoal;
        if (index == 6 || index == 18) return palette.cellBlocked;
        return palette.cellBase;
    }

    static Color ColorForBlock(byte value, Palette palette)
    {
        if (value == NetConst.ColorRed) return palette.pickupRed;
        if (value == NetConst.ColorBlue) return palette.pickupBlue;
        if (value == NetConst.ColorYellow) return palette.pickupYellow;
        return palette.blockNeutral;
    }

    static string CellMaterialKey(int index, int x, int z)
    {
        if (x == 5 && z == 4) return "CellSelected";
        if ((x + z) % 4 == 0) return "CellGoal";
        if (index == 6 || index == 18) return "CellBlocked";
        return "CellBase";
    }

    static void CleanupLights()
    {
        Light[] lights = UnityEngine.Object.FindObjectsOfType<Light>();
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null) continue;
            if (light.name == "DirectionalLight" || light.name == "FillLight")
            {
                UnityEngine.Object.DestroyImmediate(light.gameObject);
            }
        }
    }

    static Palette CreatePalette()
    {
        return new Palette
        {
            floor = new Color(0.05f, 0.07f, 0.10f, 1f),
            wall = new Color(0.04f, 0.06f, 0.09f, 1f),
            boardBase = new Color(0.07f, 0.09f, 0.13f, 1f),
            boardFrame = new Color(0.08f, 0.10f, 0.16f, 1f),
            cellBase = new Color(0.16f, 0.20f, 0.24f, 1f),
            cellSelected = new Color(0.26f, 0.64f, 0.84f, 1f),
            cellBlocked = new Color(0.30f, 0.26f, 0.36f, 1f),
            cellGoal = new Color(0.78f, 0.70f, 0.36f, 1f),
            highlight = new Color(0.45f, 0.92f, 1f, 1f),
            card = new Color(0.10f, 0.13f, 0.19f, 1f),
            cardStripe = new Color(0.34f, 0.42f, 0.60f, 1f),
            pickupRed = new Color(0.74f, 0.30f, 0.38f, 1f),
            pickupBlue = new Color(0.28f, 0.52f, 0.82f, 1f),
            pickupYellow = new Color(0.72f, 0.64f, 0.28f, 1f),
            blockNeutral = new Color(0.20f, 0.22f, 0.28f, 1f),
            guideBody = new Color(0.10f, 0.13f, 0.18f, 1f),
            guideHead = new Color(0.88f, 0.82f, 0.84f, 1f),
            guideHairLeft = new Color(0.60f, 0.78f, 1f, 1f),
            guideHairRight = new Color(0.72f, 0.64f, 0.92f, 1f),
            guideEye = new Color(0.22f, 0.24f, 0.46f, 1f),
            guidePin = new Color(0.82f, 0.86f, 0.90f, 1f),
            decorAccent = new Color(0.72f, 0.84f, 1f, 1f),
            note = new Color(0.72f, 0.64f, 0.92f, 1f),
            mug = new Color(0.84f, 0.80f, 0.90f, 1f),
            glow = new Color(0.36f, 0.56f, 0.84f, 0.55f),
            neon = new Color(0.28f, 0.36f, 0.58f, 0.72f),
            declare = new Color(0.48f, 0.64f, 0.84f, 1f),
            rulePanel = new Color(0.06f, 0.08f, 0.12f, 0.90f),
            logPanel = new Color(0.07f, 0.10f, 0.15f, 0.94f),
            warningPanel = new Color(0.17f, 0.08f, 0.13f, 0.94f),
            scorePanel = new Color(0.10f, 0.08f, 0.16f, 0.92f),
            timerPanel = new Color(0.08f, 0.09f, 0.14f, 0.92f)
        };
    }

    sealed class Palette
    {
        public Color floor;
        public Color wall;
        public Color boardBase;
        public Color boardFrame;
        public Color cellBase;
        public Color cellSelected;
        public Color cellBlocked;
        public Color cellGoal;
        public Color highlight;
        public Color card;
        public Color cardStripe;
        public Color pickupRed;
        public Color pickupBlue;
        public Color pickupYellow;
        public Color blockNeutral;
        public Color guideBody;
        public Color guideHead;
        public Color guideHairLeft;
        public Color guideHairRight;
        public Color guideEye;
        public Color guidePin;
        public Color decorAccent;
        public Color note;
        public Color mug;
        public Color glow;
        public Color neon;
        public Color declare;
        public Color rulePanel;
        public Color logPanel;
        public Color warningPanel;
        public Color scorePanel;
        public Color timerPanel;
    }
}
