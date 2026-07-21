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
    const int MinimumGeneratedActions = 152;
    static bool upgradeInProgress;

    public static bool IsUpgradeInProgress
    {
        get { return upgradeInProgress; }
    }

    [InitializeOnLoadMethod]
    static void Schedule()
    {
        if (Application.isBatchMode) return;
        EditorApplication.delayCall += EnsurePlayerControls;
    }

    [MenuItem("VRMine/Upgrade Player Count Controls")]
    public static void EnsurePlayerControls()
    {
        if (upgradeInProgress || Application.isPlayingOrWillChangePlaymode || !File.Exists(ScenePath)) return;

        upgradeInProgress = true;
        Scene scene = default(Scene);
        bool openedHere = false;
        try
        {
            scene = SceneManager.GetSceneByPath(ScenePath);
            openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            if (CountComponents<BoardGameAction>(scene) < MinimumGeneratedActions)
            {
                if (openedHere && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                BoardGameShowcaseBuilder.Build();
                scene = SceneManager.GetSceneByPath(ScenePath);
                openedHere = false;
            }

            GameController trick = FindComponent<GameController>(scene);
            OrapaMineGame orapa = FindComponent<OrapaMineGame>(scene);
            BoardGameShowcaseView view = FindComponent<BoardGameShowcaseView>(scene);
            if (trick == null || orapa == null || view == null) return;

            bool programAssetsChanged = EnsureProgramAsset<BoardGameAction>() | EnsureProgramAsset<TrickSeatLifecycle>();
            if (programAssetsChanged)
            {
                UdonSharpProgramAsset.UdonSharpCheckAbsent();
                UdonSharpProgramAsset.CompileAllCsPrograms(true);
                AssetDatabase.SaveAssets();
            }

            bool changed = false;
            GameObject root = FindGameObject(scene, "ReleaseControls");
            if (root == null)
            {
                root = new GameObject("ReleaseControls");
                SceneManager.MoveGameObjectToScene(root, scene);
                changed = true;
            }

            Material gold = LoadOrCreateMaterial("Gold", new Color(0.75f, 0.48f, 0.08f), ref changed);
            Material white = LoadOrCreateMaterial("White", new Color(0.82f, 0.86f, 0.91f), ref changed);

            for (int count = 3; count <= 5; count++)
            {
                float x = -6.35f + (count - 3) * 0.85f;
                changed |= EnsureControl(scene, root.transform, "TrickPlayerCount_" + count,
                    new Vector3(x, 0.24f, -0.95f), gold, 0, 5, count, trick, null, count + "P");
            }
            for (int count = 2; count <= 5; count++)
            {
                float x = -1.25f + (count - 2) * 0.82f;
                changed |= EnsureControl(scene, root.transform, "OrapaPlayerCount_" + count,
                    new Vector3(x, 0.24f, -1.95f), gold, 1, 8, count, null, orapa, count + "P");
            }

            Text[] desiredTableCards = new Text[NetConst.MaxPlayers];
            for (int slot = 0; slot < NetConst.MaxPlayers; slot++)
            {
                bool displayChanged;
                float x = -6.9f + slot * 0.7f;
                desiredTableCards[slot] = EnsureDisplay(scene, root.transform, "TrickTableCard_" + slot,
                    new Vector3(x, 0.25f, 0.25f), white, out displayChanged);
                changed |= displayChanged;
            }
            if (!ReferencesMatch(view.trickTableCards, desiredTableCards))
            {
                view.trickTableCards = desiredTableCards;
                EditorUtility.SetDirty(view);
                changed = true;
            }

            GameObject lifecycleObject = FindGameObject(scene, "TrickSeatLifecycle");
            TrickSeatLifecycle lifecycle;
            if (lifecycleObject == null)
            {
                lifecycleObject = new GameObject("TrickSeatLifecycle");
                lifecycleObject.transform.SetParent(root.transform);
                lifecycle = (TrickSeatLifecycle)(Component)lifecycleObject.AddUdonSharpComponent(typeof(TrickSeatLifecycle));
                changed = true;
            }
            else
            {
                lifecycle = lifecycleObject.GetComponent<TrickSeatLifecycle>();
                if (lifecycle == null)
                {
                    lifecycle = (TrickSeatLifecycle)(Component)lifecycleObject.AddUdonSharpComponent(typeof(TrickSeatLifecycle));
                    changed = true;
                }
            }
            if (lifecycle.game != trick)
            {
                lifecycle.game = trick;
                EditorUtility.SetDirty(lifecycle);
                changed = true;
            }

            if (!changed) return;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[VRMine] Updated player-count controls, trick table, and seat lifecycle in " + ScenePath);
        }
        finally
        {
            if (openedHere && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            upgradeInProgress = false;
        }
    }

    static bool EnsureControl(Scene scene, Transform parent, string name, Vector3 position, Material material,
        int game, int action, int value, GameController trick, OrapaMineGame orapa, string label)
    {
        bool changed = false;
        GameObject button = FindGameObject(scene, name);
        if (button == null)
        {
            button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = name;
            button.transform.SetParent(parent);
            button.transform.position = position;
            button.transform.localScale = new Vector3(0.65f, 0.16f, 0.34f);
            button.GetComponent<Renderer>().sharedMaterial = material;
            changed = true;
        }

        BoardGameAction behaviour = button.GetComponent<BoardGameAction>();
        if (behaviour == null)
        {
            behaviour = (BoardGameAction)(Component)button.AddUdonSharpComponent(typeof(BoardGameAction));
            changed = true;
        }
        if (behaviour.game != game || behaviour.action != action || behaviour.value != value
            || behaviour.trickGame != trick || behaviour.orapaGame != orapa)
        {
            behaviour.game = game;
            behaviour.action = action;
            behaviour.value = value;
            behaviour.trickGame = trick;
            behaviour.orapaGame = orapa;
            EditorUtility.SetDirty(behaviour);
            changed = true;
        }

        Text text = button.GetComponentInChildren<Text>(true);
        if (text == null)
        {
            CreateLabel(button.transform, name + "LabelCanvas", label);
            changed = true;
        }
        else if (text.text != label)
        {
            text.text = label;
            EditorUtility.SetDirty(text);
            changed = true;
        }
        return changed;
    }

    static Text EnsureDisplay(Scene scene, Transform parent, string name, Vector3 position, Material material, out bool changed)
    {
        changed = false;
        GameObject display = FindGameObject(scene, name);
        if (display == null)
        {
            display = GameObject.CreatePrimitive(PrimitiveType.Cube);
            display.name = name;
            display.transform.SetParent(parent);
            display.transform.position = position;
            display.transform.localScale = new Vector3(0.58f, 0.08f, 0.78f);
            display.GetComponent<Renderer>().sharedMaterial = material;
            changed = true;
        }

        Text existing = display.GetComponentInChildren<Text>(true);
        if (existing != null) return existing;
        changed = true;
        return CreateLabel(display.transform, name + "LabelCanvas", "");
    }

    static Text CreateLabel(Transform parent, string name, string label)
    {
        GameObject canvasObject = new GameObject(name);
        canvasObject.transform.SetParent(parent);
        canvasObject.transform.localPosition = new Vector3(0f, 0.65f, 0f);
        canvasObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        canvasObject.transform.localScale = Vector3.one * 0.0007f;
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(600f, 220f);

        GameObject textObject = new GameObject(label == "" ? "Value" : label);
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
        return text;
    }

    static Material LoadOrCreateMaterial(string name, Color color, ref bool changed)
    {
        const string folder = "Assets/KafkaMade/VRMine/Materials";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets/KafkaMade/VRMine", "Materials");
            changed = true;
        }
        string path = folder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Standard"));
        material.color = color;
        AssetDatabase.CreateAsset(material, path);
        changed = true;
        return material;
    }

    static bool ReferencesMatch(Text[] current, Text[] desired)
    {
        if (current == null || current.Length != desired.Length) return false;
        for (int i = 0; i < desired.Length; i++) if (current[i] != desired[i]) return false;
        return true;
    }

    static int CountComponents<T>(Scene scene) where T : Component
    {
        int count = 0;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++) count += roots[i].GetComponentsInChildren<T>(true).Length;
        return count;
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

    static bool EnsureProgramAsset<T>() where T : UdonSharpBehaviour
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
        if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path) != null) return false;
        UdonSharpProgramAsset program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
        program.sourceCsScript = source;
        AssetDatabase.CreateAsset(program, path);
        return true;
    }
}

public class BoardGameSceneUpgradePostprocessor : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        if (BoardGameSceneUpgrade.IsUpgradeInProgress) return;
        for (int i = 0; i < importedAssets.Length; i++)
        {
            if (importedAssets[i] != "Assets/KafkaMade/VRMine/Scenes/BoardGameShowcase.unity") continue;
            EditorApplication.delayCall += BoardGameSceneUpgrade.EnsurePlayerControls;
            return;
        }
    }
}
