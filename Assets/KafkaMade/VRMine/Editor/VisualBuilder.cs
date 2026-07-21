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
        BuildLighting(root.transform, palette);
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
        GameObject spawn = CreateNode(world.transform, "SpawnPoint");
        spawn.transform.localPosition = new Vector3(0f, 1.2f, -3f);
        SerializedObject so = new SerializedObject(descriptor);
        AssignTransformArray(so, "spawns", new[] { spawn.transform });
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void BuildSystems(Transform parent)
    {
        GameObject systems = CreateNode(parent, "Systems");
        GameObject udon = CreateNode(systems.transform, "Udon");
        string[] names = { "GameController", "BoardState", "PlayerClient", "LogStream" };
        for (int i = 0; i < names.Length; i++) CreateNode(udon.transform, names[i]);
    }

    static void BuildGameplay(Transform parent, Palette palette, TextureAssets textures)
    {
        GameObject gameplay = CreateNode(parent, "Gameplay");
        BuildBoardPhysical(gameplay.transform, palette, textures);
    }

    static void BuildBoardPhysical(Transform parent, Palette palette, TextureAssets textures)
    {
        GameObject root = CreateNode(parent, "BoardRoot");
        
        // Table/Frame
        CreatePrimitive(root.transform, PrimitiveType.Cube, "Table", new Vector3(0, 0.75f, 0), new Vector3(2.5f, 0.1f, 2f), Quaternion.identity, palette.boardFrame);
        
        // Board Surface
        GameObject board = CreatePrimitive(root.transform, PrimitiveType.Cube, "BoardQuad", new Vector3(0, 0.81f, 0), new Vector3(1.2f, 0.02f, 1.2f), Quaternion.identity, palette.boardBase);
        ApplyTexture(board, textures.board, Color.white);

        // Hand Area (Local Player)
        GameObject handRoot = CreateNode(root.transform, "HandRoot");
        handRoot.transform.localPosition = new Vector3(0, 0.82f, -0.7f);
        for (int i = 0; i < 15; i++)
        {
            GameObject card = BuildCardObject(handRoot.transform, "HandCard_" + i, new Vector3((i - 7) * 0.12f, 0, 0), palette);
            card.SetActive(false);
        }

        // Trick Area (Center)
        GameObject trickRoot = CreateNode(root.transform, "TrickRoot");
        trickRoot.transform.localPosition = new Vector3(0, 0.82f, 0);
        for (int i = 0; i < 4; i++)
        {
            Vector3 pos = new Vector3((i % 2 - 0.5f) * 0.3f, 0, (i / 2 - 0.5f) * 0.3f);
            GameObject card = BuildCardObject(trickRoot.transform, "TrickCard_" + i, pos, palette);
            card.SetActive(false);
        }

        // Rules Sticky
        GameObject sticky = CreatePrimitive(root.transform, PrimitiveType.Cube, "RuleSticky", new Vector3(0.8f, 0.81f, 0.6f), new Vector3(0.4f, 0.01f, 0.4f), Quaternion.Euler(0, 10, 0), palette.note);
        CreatePhysicalText(sticky.transform, "RuleText", "RULES", 12, new Vector2(400, 400));

        // Score Stand
        GameObject scoreStand = CreatePrimitive(root.transform, PrimitiveType.Cube, "ScoreStand", new Vector3(-0.8f, 0.85f, 0.6f), new Vector3(0.4f, 0.2f, 0.1f), Quaternion.Euler(-20, 0, 0), palette.boardFrame);
        CreatePhysicalText(scoreStand.transform, "ScoreLabel", "SCORE", 12, new Vector2(400, 200));
    }

    static GameObject BuildCardObject(Transform parent, string name, Vector3 pos, Palette palette)
    {
        GameObject card = CreatePrimitive(parent, PrimitiveType.Cube, name, pos, new Vector3(0.1f, 0.005f, 0.15f), Quaternion.identity, Color.white, false);
        GameObject canvas = new GameObject("CardCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.transform.SetParent(card.transform, false);
        canvas.transform.localPosition = new Vector3(0, 0.51f, 0);
        canvas.transform.localRotation = Quaternion.Euler(90, 0, 0);
        canvas.transform.localScale = Vector3.one * 0.0005f;
        
        Canvas c = canvas.GetComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        
        GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bg.transform.SetParent(canvas.transform, false);
        bg.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 300);
        
        GameObject rank = new GameObject("Rank", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        rank.transform.SetParent(canvas.transform, false);
        Text rt = rank.GetComponent<Text>();
        rt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rt.fontSize = 120;
        rt.alignment = TextAnchor.MiddleCenter;
        rt.color = Color.black;
        rank.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 40);

        GameObject suit = new GameObject("Suit", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        suit.transform.SetParent(canvas.transform, false);
        Text st = suit.GetComponent<Text>();
        st.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        st.fontSize = 80;
        st.alignment = TextAnchor.MiddleCenter;
        st.color = Color.black;
        suit.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -60);

        CardView view = card.AddComponent<CardView>();
        view.label = rt;
        view.subLabel = st;
        
        return card;
    }

    static void BuildVisual(Transform parent, Palette palette, TextureAssets textures)
    {
        GameObject floor = CreatePrimitive(parent, PrimitiveType.Plane, "Floor", Vector3.zero, new Vector3(5f, 1f, 5f), Quaternion.identity, palette.floor);
        ApplyTexture(floor, textures.floor, Color.white);
        
        GameObject walls = CreateNode(parent, "Walls");
        for (int i = 0; i < 4; i++)
        {
            Vector3 pos = i < 2 ? new Vector3(0, 4, (i == 0 ? 15 : -15)) : new Vector3((i == 2 ? 15 : -15), 4, 0);
            Vector3 scale = i < 2 ? new Vector3(30, 8, 0.2f) : new Vector3(0.2f, 8, 30);
            GameObject wall = CreatePrimitive(walls.transform, PrimitiveType.Cube, "Wall_" + i, pos, scale, Quaternion.identity, palette.wall);
            ApplyTexture(wall, textures.wall, Color.white);
        }
    }

    static void BuildLighting(Transform parent, Palette palette)
    {
        GameObject lamp = new GameObject("TableLamp");
        lamp.transform.SetParent(parent);
        lamp.transform.localPosition = new Vector3(0, 3, 0);
        Light l = lamp.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.9f, 0.8f);
        l.intensity = 1.5f;
        l.shadows = LightShadows.Soft;
    }

    static void BuildEditorOnly(Transform parent) { CreateNode(parent, "EditorOnly").tag = "EditorOnly"; }

    static GameObject CreateNode(Transform parent, string name)
    {
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
        if (removeCollider && type != PrimitiveType.Plane) { var c = go.GetComponent<Collider>(); if (c != null) UnityEngine.Object.DestroyImmediate(c); }
        Tint(go, color);
        return go;
    }

    static void Tint(GameObject go, Color color)
    {
        Renderer r = go.GetComponent<Renderer>();
        if (r == null) return;
        r.sharedMaterial = new Material(Shader.Find("Standard")) { color = color };
    }

    static void ApplyTexture(GameObject go, Texture2D tex, Color tint)
    {
        if (tex == null) return;
        Renderer r = go.GetComponent<Renderer>();
        if (r == null) return;
        r.sharedMaterial = new Material(Shader.Find("Standard")) { mainTexture = tex, color = tint };
    }

    static void CreatePhysicalText(Transform parent, string name, string val, int size, Vector2 canvasSize)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(Text));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0, 0.51f, 0);
        go.transform.localRotation = Quaternion.Euler(90, 0, 0);
        go.transform.localScale = Vector3.one * 0.001f;
        Canvas c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        Text t = go.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = val;
        t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.black;
    }

    static void AssignTransformArray(SerializedObject so, string prop, Transform[] targets)
    {
        SerializedProperty p = so.FindProperty(prop);
        p.arraySize = targets.Length;
        for (int i = 0; i < targets.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = targets[i];
    }

    static Palette CreatePalette()
    {
        return new Palette { floor = new Color(0.1f, 0.1f, 0.1f), wall = new Color(0.05f, 0.05f, 0.05f), boardBase = new Color(0.15f, 0.15f, 0.15f), boardFrame = new Color(0.3f, 0.2f, 0.1f), note = new Color(0.9f, 0.9f, 0.7f) };
    }

    static void CleanupLights()
    {
        Light[] lights = UnityEngine.Object.FindObjectsOfType<Light>();
        foreach (Light l in lights) UnityEngine.Object.DestroyImmediate(l.gameObject);
    }

    class Palette { public Color floor, wall, boardBase, boardFrame, note; }
}
