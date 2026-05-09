using UnityEditor;
using UnityEngine;

public static class VisualBuilder
{
    [MenuItem("VRMine/build_visuals")]
    public static void BuildVisuals()
    {
        GameObject root = GameObject.Find("VRMineVisualRoot");
        if (root != null) Object.DestroyImmediate(root);
        root = new GameObject("VRMineVisualRoot");

        Palette palette = GetPalette();
        TextureAssets textures = GetTextures();

        // 1. Room
        BuildRoom(root.transform, palette, textures);

        // 2. Board Game (The core)
        BuildBoardRoot(root.transform, palette, textures);
    }

    struct Palette
    {
        public Color floor;
        public Color wall;
        public Color boardFrame;
        public Color boardSurface;
        public Color card;
        public Color note;
    }

    static Palette GetPalette()
    {
        return new Palette
        {
            floor = new Color(0.12f, 0.14f, 0.18f),
            wall = new Color(0.08f, 0.10f, 0.14f),
            boardFrame = new Color(0.35f, 0.25f, 0.20f),
            boardSurface = new Color(0.15f, 0.18f, 0.22f),
            card = Color.white,
            note = new Color(0.92f, 0.88f, 1.0f)
        };
    }

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

    static void BuildRoom(Transform parent, Palette palette, TextureAssets textures)
    {
        GameObject room = new GameObject("Environment");
        room.transform.SetParent(parent);

        GameObject floor = CreatePrimitive(room.transform, PrimitiveType.Plane, "Floor", Vector3.zero, new Vector3(2f, 1f, 2f), Quaternion.identity, palette.floor);
        ApplyTexture(floor, textures.floor);

        GameObject wall = CreatePrimitive(room.transform, PrimitiveType.Cube, "NorthWall", new Vector3(0f, 4f, 10f), new Vector3(20f, 8f, 0.2f), Quaternion.identity, palette.wall);
        ApplyTexture(wall, textures.wall);

        GameObject poster = CreatePrimitive(wall.transform, PrimitiveType.Quad, "RulePoster", new Vector3(0f, -0.2f, -0.11f), new Vector3(1.2f, 1.6f, 1f), Quaternion.identity, Color.white);
        ApplyTexture(poster, textures.reference);
    }

    static void BuildBoardRoot(Transform parent, Palette palette, TextureAssets textures)
    {
        GameObject boardRoot = new GameObject("BoardRoot");
        boardRoot.transform.SetParent(parent);
        boardRoot.transform.localPosition = new Vector3(0f, 0.8f, 0f);

        // Frame
        GameObject frame = CreatePrimitive(boardRoot.transform, PrimitiveType.Cube, "BoardFrame", Vector3.zero, new Vector3(1.1f, 0.05f, 0.9f), Quaternion.identity, palette.boardFrame);
        ApplyTexture(frame, textures.props);

        // Surface
        GameObject surface = CreatePrimitive(frame.transform, PrimitiveType.Cube, "BoardQuad", new Vector3(0f, 0.02f, 0f), new Vector3(0.92f, 0.1f, 0.92f), Quaternion.identity, palette.boardSurface);
        ApplyTexture(surface, textures.board);

        // Cards
        for (int i = 0; i < 5; i++)
        {
            float angle = -20f + i * 10f;
            Vector3 pos = new Vector3(-0.3f + i * 0.15f, 0.04f, -0.35f);
            GameObject card = CreatePrimitive(boardRoot.transform, PrimitiveType.Cube, "Card_" + i, pos, new Vector3(0.12f, 0.005f, 0.18f), Quaternion.Euler(0, angle, 0), palette.card);
            ApplyTexture(card, textures.suits);
        }

        // Physical Note for Rule UI
        GameObject note = CreatePrimitive(boardRoot.transform, PrimitiveType.Cube, "StickyNote", new Vector3(0.45f, 0.03f, 0.35f), new Vector3(0.15f, 0.01f, 0.15f), Quaternion.Euler(0, 5f, 0), palette.note);
        ApplyTexture(note, textures.note);

        LinkUI("RuleView", note.transform, new Vector3(0, 0.51f, 0));
        LinkUI("PhaseView", boardRoot.transform, new Vector3(-0.45f, 0.03f, 0.35f));
    }

    static void LinkUI(string name, Transform newParent, Vector3 pos)
    {
        GameObject panel = GameObject.Find(name);
        if (panel != null)
        {
            panel.transform.SetParent(newParent);
            panel.transform.localPosition = pos;
            panel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            panel.transform.localScale = new Vector3(0.003f, 0.003f, 1f);
        }
    }

    static GameObject CreatePrimitive(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Quaternion rot, Color color)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(parent);
        obj.transform.localPosition = pos;
        obj.transform.localScale = scale;
        obj.transform.localRotation = rot;
        obj.GetComponent<Renderer>().material.color = color;
        return obj;
    }

    static void ApplyTexture(GameObject obj, Texture2D tex)
    {
        if (tex == null) return;
        Renderer r = obj.GetComponent<Renderer>();
        r.material = new Material(Shader.Find("Standard"));
        r.material.mainTexture = tex;
    }
}
