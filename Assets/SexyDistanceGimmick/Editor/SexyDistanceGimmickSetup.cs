#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.IO;

public class SexyDistanceGimmickSetup : EditorWindow
{
    [MenuItem("Tools/SexyDistanceGimmick/Setup All")]
    static void SetupAll()
    {
        CreateDirectories();
        CreateAnimatorController();
        CreatePrefab();
        CreateSampleScene();
        AssetDatabase.Refresh();
        Debug.Log("SexyDistanceGimmick: Setup Complete");
    }

    [MenuItem("Tools/SexyDistanceGimmick/Create Animator")]
    static void CreateAnimatorController()
    {
        string path = "Assets/SexyDistanceGimmick/Animations/Ghost.controller";
        
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        
        controller.AddParameter("Active", AnimatorControllerParameterType.Bool);
        controller.AddParameter("LeanAmount", AnimatorControllerParameterType.Float);

        var rootStateMachine = controller.layers[0].stateMachine;
        
        var idle = rootStateMachine.AddState("Idle", new Vector3(250, 50, 0));
        var active = rootStateMachine.AddState("Active", new Vector3(250, 150, 0));
        
        var toActive = idle.AddTransition(active);
        toActive.AddCondition(AnimatorConditionMode.If, 0, "Active");
        toActive.duration = 0.25f;
        
        var toIdle = active.AddTransition(idle);
        toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Active");
        toIdle.duration = 0.5f;

        var blendTree = new BlendTree();
        blendTree.name = "LeanBlend";
        blendTree.blendParameter = "LeanAmount";
        blendTree.blendType = BlendTreeType.Simple1D;
        
        AssetDatabase.AddObjectToAsset(blendTree, controller);
        active.motion = blendTree;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        
        Debug.Log("Created: " + path);
    }

    [MenuItem("Tools/SexyDistanceGimmick/Create Prefab")]
    static void CreatePrefab()
    {
        var root = new GameObject("SexyDistanceGimmick");
        
        var ghostRoot = CreateChild(root, "GhostRoot");
        CreateChild(ghostRoot, "GhostVisual");
        CreateChild(ghostRoot, "GhostTouch_Chest");
        CreateChild(ghostRoot, "GhostTouch_Neck");
        CreateChild(ghostRoot, "GhostTouch_Ear_L");
        CreateChild(ghostRoot, "GhostTouch_Ear_R");
        CreateChild(ghostRoot, "GhostTouch_Waist");
        CreateChild(ghostRoot, "GhostTouch_Thigh");
        
        var whisper = CreateChild(root, "Audio_Whisper");
        whisper.AddComponent<AudioSource>();
        
        var touch = CreateChild(root, "Audio_Touch");
        touch.AddComponent<AudioSource>();

        string prefabPath = "Assets/SexyDistanceGimmick/Prefabs/SexyDistanceGimmick.prefab";
        EnsureDirectory(Path.GetDirectoryName(prefabPath));
        
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        DestroyImmediate(root);
        
        Debug.Log("Created: " + prefabPath);
    }

    [MenuItem("Tools/SexyDistanceGimmick/Create Sample Scene")]
    static void CreateSampleScene()
    {
        string scenePath = "Assets/SexyDistanceGimmick/Scenes/SampleScene.unity";
        EnsureDirectory(Path.GetDirectoryName(scenePath));
        
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,
            UnityEditor.SceneManagement.NewSceneMode.Single);
        
        string prefabPath = "Assets/SexyDistanceGimmick/Prefabs/SexyDistanceGimmick.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab != null)
        {
            PrefabUtility.InstantiatePrefab(prefab);
        }

        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.localScale = new Vector3(5, 1, 5);

        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scenePath);
        
        Debug.Log("Created: " + scenePath);
    }

    static void CreateDirectories()
    {
        EnsureDirectory("Assets/SexyDistanceGimmick/Prefabs");
        EnsureDirectory("Assets/SexyDistanceGimmick/Animations");
        EnsureDirectory("Assets/SexyDistanceGimmick/Audio");
        EnsureDirectory("Assets/SexyDistanceGimmick/Scenes");
        EnsureDirectory("Assets/SexyDistanceGimmick/UdonSharp");
    }

    static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    static GameObject CreateChild(GameObject parent, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent.transform);
        child.transform.localPosition = Vector3.zero;
        return child;
    }
}
#endif
