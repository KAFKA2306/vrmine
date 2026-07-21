using System.IO;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class BoardGameSceneUpgrade
{
    const string ScenePath = "Assets/KafkaMade/VRMine/Scenes/BoardGameShowcase.unity";

    [InitializeOnLoadMethod]
    static void Schedule()
    {
        if (Application.isBatchMode) return;
        EditorApplication.delayCall += EnsurePlayerControls;
    }

    [MenuItem("VRMine/Upgrade Player Count Controls")]
    public static void EnsurePlayerControls()
    {
        if (Application.isPlayingOrWillChangePlaymode || !File.Exists(ScenePath)) return;

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        GameController trick = FindComponent<GameController>(scene);
        OrapaMineGame orapa = FindComponent<OrapaMineGame>(scene);
        if (trick == null || orapa == null)
        {
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            return;
        }

        EnsureProgramAsset<BoardGameAction>();
        EnsureProgramAsset<TrickSeatLifecycle>();
        UdonSharpProgramAsset.CompileAllCsPrograms(true);

        GameObject root = FindGameObject(scene, "ReleaseControls");
        if (root == null)
        {
            root = new GameObject("ReleaseControls");
            SceneManager.MoveGameObjectToScene(root, scene);
        }

        Material gold = AssetDatabase.LoadAssetAtPath<Material>("Assets/KafkaMade/VRMine/Materials/Gold.mat");
        if (gold == null)
        {
            gold = new Material(Shader.Find("Standard"));
            gold.color = new Color(0.75f, 0.48f, 0.08f);
        }

        for (int count = 3; count <= 5; count++)
        {
            float x = -6.35f + (count - 3) * 0.85f;
            AddControl(scene, root.transform, "TrickPlayerCount_" + count, new Vector3(x, 0.24f, -0.95f), gold, 0, 5, count, trick, null, count + "P");
        }
        for (int count = 2; count <= 5; count++)
        {
            float x = -1.25f + (count - 2) * 0.82f;
            AddControl(scene, root.transform, "OrapaPlayerCount_" + count, new Vector3(x, 0.24f, -1.95f), gold, 1, 8, count, null, orapa, count + "P");
        }

        if (FindGameObject(scene, "TrickSeatLifecycle") == null)
        {
            GameObject lifecycleObject = new GameObject("TrickSeatLifecycle");
            lifecycleObject.transform.SetParent(root.transform);
            TrickSeatLifecycle lifecycle = (TrickSeatLifecycle)(Component)lifecycleObject.AddUdonSharpComponent(typeof(TrickSeatLifecycle));
            lifecycle.game = trick;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        if (openedHere) EditorSceneManager.CloseScene(scene, true);
        Debug.Log("[VRMine] Ensured player-count controls and seat lifecycle in " + ScenePath);
    }

    static void AddControl(Scene scene, Transform parent, string name, Vector3 position, Material material, int game, int action, int value, GameController trick, OrapaMineGame orapa, string label)
    {
        if (FindGameObject(scene, name) != null) return;
        GameObject button = GameObject.CreatePrimitive(PrimitiveType.Cube);
        button.name = name;
        button.transform.SetParent(parent);
        button.transform.position = position;
        button.transform.localScale = new Vector3(0.65f, 0.16f, 0.34f);
        button.GetComponent<Renderer>().sharedMaterial = material;

        BoardGameAction behaviour = (BoardGameAction)(Component)button.AddUdonSharpComponent(typeof(BoardGameAction));
        behaviour.game = game;
        behaviour.action = action;
        behaviour.value = value;
        behaviour.trickGame = trick;
        behaviour.orapaGame = orapa;

        GameObject canvasObject = new GameObject(name + "LabelCanvas");
        canvasObject.transform.SetParent(button.transform);
        canvasObject.transform.localPosition = new Vector3(0f, 0.65f, 0f);
        canvasObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        canvasObject.transform.localScale = Vector3.one * 0.0007f;
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(600f, 220f);

        GameObject textObject = new GameObject(label);
        textObject.transform.SetParent(canvasObject.transform, false);
        Text text = textObject.AddComponent<Text>();
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        text.text = label;
        text.fontSize = 64;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
    }

    static T FindComponent<T>(Scene scene) where T : Component
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T component = roots[i].GetComponentInChildren<T>(true);
            if (component != null) return component;
        }
        return null;
    }

    static GameObject FindGameObject(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++) if (transforms[j].name == name) return transforms[j].gameObject;
        }
        return null;
    }

    static void EnsureProgramAsset<T>() where T : UdonSharpBehaviour
    {
        string[] scripts = AssetDatabase.FindAssets(typeof(T).Name + " t:MonoScript");
        MonoScript source = null;
        for (int i = 0; i < scripts.Length; i++)
        {
            MonoScript candidate = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(scripts[i]));
            if (candidate != null && candidate.GetClass() == typeof(T)) source = candidate;
        }
        if (source == null) throw new FileNotFoundException("Missing UdonSharp script for " + typeof(T).Name);
        string path = Path.ChangeExtension(AssetDatabase.GetAssetPath(source), ".asset");
        if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path) != null) return;
        UdonSharpProgramAsset program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
        program.sourceCsScript = source;
        AssetDatabase.CreateAsset(program, path);
    }
}

public class BoardGameSceneUpgradePostprocessor : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        for (int i = 0; i < importedAssets.Length; i++)
        {
            if (importedAssets[i] != "Assets/KafkaMade/VRMine/Scenes/BoardGameShowcase.unity") continue;
            EditorApplication.delayCall += BoardGameSceneUpgrade.EnsurePlayerControls;
            return;
        }
    }
}