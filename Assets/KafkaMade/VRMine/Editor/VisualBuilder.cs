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
        public Texture2D floor;
        public Texture2D wall;
        public Texture2D board;
        public Texture2D suits;
        public Texture2D note;
        public Texture2D reference;
        public Texture2D props;
    }

    static TextureAssets GetTextures()
    {
        return new TextureAssets
        {
            floor = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/KafkaMade/VRMine/Textures/VRMine_Floor.png"),
            wall = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/KafkaMade/VRMine/Textures/VRMine_Wall.png"),
            board = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/KafkaMade/VRMine/Textures/VRMine_Board.png"),
            suits = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/KafkaMade/VRMine/Textures/VRMine_Suits.png"),
            note = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/KafkaMade/VRMine/Textures/VRMine_Note.png"),
            reference = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/KafkaMade/VRMine/Textures/VRMine_Rule_Reference.png"),
            props = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/KafkaMade/VRMine/Textures/VRMine_Props.png")
        };
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
        TextureAssets textures = GetTextures();
        BuildVrcWorld(root.transform, palette);
        BuildSystems(root.transform);
        BuildGameplay(root.transform, palette, textures);
        BuildVisual(root.transform, palette, textures);
        BuildAudio(root.transform);
        BuildLighting(root.transform, palette);
        BuildDebug(root.transform);
        BuildEditorOnly(root.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root;
    }

    static void BuildVrcWorld(Transform parent, Palette palette)
    {
        GameObject world = CreateNode(parent, "VRCWorld");
        VRCSceneDescriptor descriptor = world.GetComponent<VRCSceneDescriptor>();
        if (descriptor == null) descriptor = world.AddComponent<VRCSceneDescriptor>();

        GameObject spawnRoot = CreateNode(world.transform, "SpawnRoot");
        GameObject spawn = CreateNode(spawnRoot.transform, "Spawn_0");
        spawn.transform.localPosition = new Vector3(0f, 1.2f, -3f);
        
        SerializedObject so = new SerializedObject(descriptor);
        AssignTransformArray(so, "spawns", new[] { spawn.transform });
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void BuildSystems(Transform parent)
    {
        GameObject systems = CreateNode(parent, "Systems");
        GameObject udon = CreateNode(systems.transform, "Udon");
        string[] names = { "GameController", "BoardState", "PlayerClient", "WaveSimulator", "LogStream" };
        for (int i = 0; i < names.Length; i++) CreateNode(udon.transform, names[i]);
    }

    static void BuildGameplay(Transform parent, Palette palette, TextureAssets textures)
    {
        GameObject gameplay = CreateNode(parent, "Gameplay");
        BuildBoardPhysical(gameplay.transform, palette, textures);
        BuildPhysicalInteraction(gameplay.transform, palette, textures);
    }

    static void BuildBoardPhysical(Transform parent, Palette palette, TextureAssets textures)
    {
        GameObject root = CreateNode(parent, "BoardRoot");
        root.transform.localPosition = Vector3.zero;

        // Physical Frame (Raised edge)
        CreatePrimitive(root.transform, PrimitiveType.Cube, "Frame_N", new Vector3(0f, 0.1f, 4.2f), new Vector3(10.6f, 0.2f, 0.4f), Quaternion.identity, palette.boardFrame, false);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "Frame_S", new Vector3(0f, 0.1f, -4.2f), new Vector3(10.6f, 0.2f, 0.4f), Quaternion.identity, palette.boardFrame, false);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "Frame_E", new Vector3(5.1f, 0.1f, 0f), new Vector3(0.4f, 0.2f, 8.8f), Quaternion.identity, palette.boardFrame, false);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "Frame_W", new Vector3(-5.1f, 0.1f, 0f), new Vector3(0.4f, 0.2f, 8.8f), Quaternion.identity, palette.boardFrame, false);

        // Recessed Board Base
        GameObject board = CreatePrimitive(root.transform, PrimitiveType.Cube, "BoardBase", new Vector3(0f, 0.04f, 0f), new Vector3(10f, 0.08f, 8f), Quaternion.identity, palette.boardBase, false);
        ApplyTexture(board, textures.board, Color.white);

        GameObject cells = CreateNode(root.transform, "Cells");
        float startX = -4.5f;
        float startZ = -3.5f;
        for (int z = 0; z < 8; z++)
        {
            for (int x = 0; x < 10; x++)
            {
                int index = z * 10 + x;
                Vector3 pos = new Vector3(startX + x, 0.09f, startZ + z);
                GameObject cell = CreatePrimitive(cells.transform, PrimitiveType.Cube, "Cell_" + index, pos, new Vector3(0.85f, 0.02f, 0.85f), Quaternion.identity, palette.cellBase, false);
                cell.transform.localPosition += Vector3.up * (UnityEngine.Random.value * 0.005f);
            }
        }
    }

    static void BuildPhysicalInteraction(Transform parent, Palette palette, TextureAssets textures)
    {
        GameObject root = CreateNode(parent, "InteractionRoot");
        
        // Rule Sticky Note (on the table)
        GameObject sticky = CreatePrimitive(root.transform, PrimitiveType.Cube, "RuleSticky", new Vector3(2.5f, 0.11f, 2.2f), new Vector3(0.6f, 0.02f, 0.6f), Quaternion.Euler(0f, 12f, 0f), palette.note, false);
        ApplyTexture(sticky, textures.note, Color.white);
        GameObject ruleText = CreatePhysicalText(sticky.transform, "RuleText", "CURRENT RULE\n[GATES]", 14, new Vector2(500f, 500f));
        ruleText.transform.localPosition = new Vector3(0f, 0.6f, 0f);

        // Score Stand (Wooden)
        GameObject scoreStand = CreateNode(root.transform, "ScoreStand");
        scoreStand.transform.localPosition = new Vector3(-2.8f, 0.1f, 2.5f);
        scoreStand.transform.localRotation = Quaternion.Euler(0f, -15f, 0f);
        CreatePrimitive(scoreStand.transform, PrimitiveType.Cube, "Base", Vector3.zero, new Vector3(1.2f, 0.1f, 0.4f), Quaternion.identity, palette.boardFrame, false);
        GameObject scorePanel = CreatePrimitive(scoreStand.transform, PrimitiveType.Cube, "Panel", new Vector3(0f, 0.4f, 0.1f), new Vector3(1.1f, 0.7f, 0.05f), Quaternion.Euler(-20f, 0f, 0f), palette.boardBase, false);
        CreatePhysicalText(scorePanel.transform, "ScoreLabel", "SCORE\n0 : 0", 20, new Vector2(1000f, 600f)).transform.localPosition = new Vector3(0f, 0.1f, 0.6f);

        // Declare Button (Mechanical)
        GameObject buttonCase = CreatePrimitive(root.transform, PrimitiveType.Cube, "DeclareButtonCase", new Vector3(3.5f, 0.1f, -1.5f), new Vector3(0.8f, 0.2f, 0.8f), Quaternion.identity, palette.boardFrame, false);
        GameObject buttonCap = CreatePrimitive(buttonCase.transform, PrimitiveType.Cylinder, "ButtonCap", new Vector3(0f, 0.6f, 0f), new Vector3(0.7f, 0.15f, 0.7f), Quaternion.identity, palette.declare, false);
        CreatePhysicalText(buttonCap.transform, "Label", "DECLARE", 12, new Vector2(400f, 400f)).transform.localPosition = new Vector3(0f, 1.1f, 0f);
    }

    static void BuildVisual(Transform parent, Palette palette, TextureAssets textures)
    {
        GameObject root = CreateNode(parent, "Visual");
        BuildEnvironmentPhysical(root.transform, palette, textures);
        BuildKafkaGuidePhysical(root.transform, palette, textures);
    }

    static void BuildEnvironmentPhysical(Transform parent, Palette palette, TextureAssets textures)
    {
        GameObject floor = CreatePrimitive(parent, PrimitiveType.Plane, "Floor", Vector3.zero, new Vector3(5f, 1f, 5f), Quaternion.identity, palette.floor, false);
        ApplyTexture(floor, textures.floor, Color.white);

        GameObject walls = CreateNode(parent, "Walls");
        Vector3[] pos = { new Vector3(0, 4, 15), new Vector3(0, 4, -15), new Vector3(15, 4, 0), new Vector3(-15, 4, 0) };
        Vector3[] scale = { new Vector3(30, 8, 1), new Vector3(30, 8, 1), new Vector3(1, 8, 30), new Vector3(1, 8, 30) };
        for (int i = 0; i < 4; i++)
        {
            GameObject wall = CreatePrimitive(walls.transform, PrimitiveType.Cube, "Wall_" + i, pos[i], scale[i], Quaternion.identity, palette.wall, false);
            ApplyTexture(wall, textures.wall, new Color(0.6f, 0.6f, 0.7f, 1f));
        }

        GameObject poster = CreatePrimitive(parent, PrimitiveType.Quad, "RulePoster", new Vector3(4f, 2.5f, 14.8f), new Vector3(2f, 2.8f, 1f), Quaternion.identity, Color.white, true);
        ApplyTexture(poster, textures.reference, Color.white);
    }

    static void BuildKafkaGuidePhysical(Transform parent, Palette palette, TextureAssets textures)
    {
        GameObject root = CreateNode(parent, "KafkaGuide");
        root.transform.localPosition = new Vector3(-3f, 1.2f, 2f);
        GameObject body = CreatePrimitive(root.transform, PrimitiveType.Sphere, "Core", Vector3.zero, new Vector3(0.5f, 0.5f, 0.5f), Quaternion.identity, palette.guideBody, true);
        GameObject ring = CreatePrimitive(body.transform, PrimitiveType.Cylinder, "BrassRing", Vector3.zero, new Vector3(1.2f, 0.05f, 1.2f), Quaternion.Euler(45f, 45f, 0f), palette.guidePin, true);
        GameObject head = CreatePrimitive(root.transform, PrimitiveType.Sphere, "Soul", new Vector3(0f, 0.7f, 0f), new Vector3(0.3f, 0.3f, 0.3f), Quaternion.identity, palette.guideHead, true);
        Light glow = head.AddComponent<Light>();
        glow.color = palette.guideHead;
        glow.intensity = 0.5f;
        glow.range = 2f;
    }

    static void BuildAudio(Transform parent)
    {
        GameObject root = CreateNode(parent, "Audio");
        CreateAudioSource(root.transform, "Ambience", Vector3.zero, true, 0.5f);
    }

    static void BuildLighting(Transform parent, Palette palette)
    {
        GameObject root = CreateNode(parent, "Lighting");
        GameObject lamp = CreateNode(root.transform, "TableLamp");
        lamp.transform.localPosition = new Vector3(0f, 3.5f, 0f);
        Light l = lamp.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.85f, 0.7f);
        l.intensity = 1.2f;
        l.range = 10f;
        l.shadows = LightShadows.Soft;

        GameObject ambient = CreateNode(root.transform, "AmbientBlue");
        Light a = ambient.AddComponent<Light>();
        a.type = LightType.Directional;
        a.color = new Color(0.1f, 0.15f, 0.3f);
        a.intensity = 0.2f;
        ambient.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    static void BuildDebug(Transform parent) { CreateNode(parent, "Debug"); }
    static void BuildEditorOnly(Transform parent) { CreateNode(parent, "EditorOnly").tag = "EditorOnly"; }

    static GameObject CreateNode(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null) return t.gameObject;
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    static GameObject CreatePrimitive(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Quaternion rot, Color color, bool removeCollider = true)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.transform.localRotation = rot;
        if (removeCollider)
        {
            Collider c = go.GetComponent<Collider>();
            if (c != null) UnityEngine.Object.DestroyImmediate(c);
        }
        Tint(go, color);
        return go;
    }

    static void Tint(GameObject go, Color color)
    {
        Renderer r = go.GetComponent<Renderer>();
        if (r == null) return;
        string key = ColorUtility.ToHtmlStringRGBA(color);
        if (!Materials.TryGetValue(key, out Material m))
        {
            m = new Material(Shader.Find("Standard"));
            m.color = color;
            m.SetFloat("_Glossiness", 0.2f);
            Materials[key] = m;
        }
        r.sharedMaterial = m;
    }

    static void ApplyTexture(GameObject go, Texture2D tex, Color tint)
    {
        if (tex == null) return;
        Renderer r = go.GetComponent<Renderer>();
        if (r == null) return;
        Material m = new Material(Shader.Find("Standard"));
        m.mainTexture = tex;
        m.color = tint;
        m.SetFloat("_Glossiness", 0.1f);
        r.sharedMaterial = m;
    }

    static void ApplyTexture(GameObject go, Texture2D tex, Vector2 offset = default, Vector2 scale = default)
    {
        if (tex == null) return;
        if (scale == default) scale = Vector2.one;
        Renderer r = go.GetComponent<Renderer>();
        if (r == null) return;
        Material m = new Material(Shader.Find("Standard"));
        m.mainTexture = tex;
        m.mainTextureOffset = offset;
        m.mainTextureScale = scale;
        r.sharedMaterial = m;
    }

    static GameObject CreatePhysicalText(Transform parent, string name, string val, int size, Vector2 canvasSize)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(Text));
        go.transform.SetParent(parent, false);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = Vector3.one * 0.001f;
        Canvas c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = canvasSize;
        Text t = go.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = val;
        t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(0.1f, 0.1f, 0.12f, 1f);
        return go;
    }

    static void CreateAudioSource(Transform parent, string name, Vector3 pos, bool loop, float blend)
    {
        GameObject go = CreateNode(parent, name);
        go.transform.localPosition = pos;
        AudioSource s = go.AddComponent<AudioSource>();
        s.loop = loop;
        s.spatialBlend = blend;
    }

    static void CleanupLights()
    {
        foreach (var l in UnityEngine.Object.FindObjectsOfType<Light>())
        {
            if (l.type == LightType.Directional && l.intensity > 0.5f) UnityEngine.Object.DestroyImmediate(l.gameObject);
        }
    }

    static void AssignTransformArray(SerializedObject so, string prop, Transform[] targets)
    {
        SerializedProperty p = so.FindProperty(prop);
        p.arraySize = targets.Length;
        for (int i = 0; i < targets.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = targets[i];
    }

    static Palette CreatePalette()
    {
        return new Palette
        {
            floor = new Color(0.1f, 0.12f, 0.15f),
            wall = new Color(0.08f, 0.09f, 0.12f),
            boardBase = new Color(0.12f, 0.15f, 0.2f),
            boardFrame = new Color(0.25f, 0.2f, 0.15f),
            cellBase = new Color(0.2f, 0.25f, 0.3f),
            note = new Color(0.9f, 0.85f, 0.6f),
            declare = new Color(0.7f, 0.2f, 0.2f),
            guideBody = new Color(0.4f, 0.35f, 0.2f),
            guideHead = new Color(0.6f, 0.9f, 1f),
            guidePin = new Color(0.5f, 0.45f, 0.3f)
        };
    }

    class Palette
    {
        public Color floor, wall, boardBase, boardFrame, cellBase, note, declare, guideBody, guideHead, guidePin;
    }
}
