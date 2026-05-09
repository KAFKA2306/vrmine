using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using UdonSharp;
using TMPro;
using BoardGameLab.Runtime.Net;

namespace BoardGameLab.Editor
{
    public class BoardGameLabSetup : EditorWindow
    {
        [MenuItem("Tools/BoardGameLab/Setup Level0 SyncButton")]
        public static void SetupLevel0()
        {
            string root = "Assets/BoardGameLab";
            string matPath = $"{root}/Materials";
            string prefabPath = $"{root}/Prefabs/BGL_Level0_Game.prefab";
            string scenePath = $"{root}/Scenes/BGL_Level0_CollectivePulse.unity";

            EnsureProgramAsset($"{root}/Runtime/Net/BGL_SyncManager.cs");
            EnsureProgramAsset($"{root}/Runtime/Net/BGL_SyncVisual.cs");
            EnsureProgramAsset($"{root}/Runtime/Net/BGL_BugDismissProxy.cs");
            EnsureProgramAsset($"{root}/Runtime/Net/BGL_SyncProxy.cs");

            // 1. Materials
            Material coreMat = GetOrCreateMaterial(matPath, "BGL_Mat_Core", Color.cyan, true);
            Material bugMat = GetOrCreateMaterial(matPath, "BGL_Mat_Bug", new Color(0.3f, 0, 0));
            Material tableMat = GetOrCreateMaterial(matPath, "BGL_Table", new Color(0.1f, 0.1f, 0.1f));

            // 2. Hierarchy
            GameObject tempRoot = new GameObject("BGL_Level0_Game_Root");

            // Manager
            GameObject managerObj = new GameObject("BGL_GameManager");
            managerObj.transform.SetParent(tempRoot.transform);
            var manager = managerObj.AddComponent<BGL_SyncManager>();

            // Visual Core
            GameObject coreObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            coreObj.name = "BGL_PulseCore";
            coreObj.transform.SetParent(tempRoot.transform);
            coreObj.transform.localPosition = new Vector3(0, 2f, 0);
            coreObj.GetComponent<MeshRenderer>().sharedMaterial = coreMat;
            DestroyImmediate(coreObj.GetComponent<SphereCollider>());

            // UI
            GameObject scoreObj = new GameObject("BGL_ScoreText");
            scoreObj.transform.SetParent(tempRoot.transform);
            scoreObj.transform.localPosition = new Vector3(0, 3.5f, 0);
            var sText = scoreObj.AddComponent<TextMeshPro>();
            sText.alignment = TextAlignmentOptions.Center; sText.fontSize = 40;

            GameObject mileObj = new GameObject("BGL_MilestoneText");
            mileObj.transform.SetParent(tempRoot.transform);
            mileObj.transform.localPosition = new Vector3(0, 3f, 0);
            var mText = mileObj.AddComponent<TextMeshPro>();
            mText.alignment = TextAlignmentOptions.Center; mText.fontSize = 30;

            var visual = coreObj.AddComponent<BGL_SyncVisual>();

            // --- BUG WINDOW ---
            GameObject bugWindow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bugWindow.name = "BGL_BugWindow";
            bugWindow.transform.SetParent(tempRoot.transform);
            bugWindow.transform.localPosition = new Vector3(0, 2.5f, -1.5f);
            bugWindow.transform.localScale = new Vector3(2.5f, 1.5f, 1);
            bugWindow.GetComponent<MeshRenderer>().sharedMaterial = bugMat;
            
            // Add interaction to dismiss bug
            bugWindow.AddComponent<BGL_BugDismissProxy>().Visual = visual;

            GameObject bugTextObj = new GameObject("BGL_BugText");
            bugTextObj.transform.SetParent(bugWindow.transform);
            bugTextObj.transform.localPosition = new Vector3(0, 0, -0.01f);
            var bugReportText = bugTextObj.AddComponent<TextMeshPro>();
            bugReportText.alignment = TextAlignmentOptions.Center;
            bugReportText.fontSize = 20; bugReportText.color = Color.white;

            // Proxy (Interactable)
            GameObject proxyObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            proxyObj.name = "BGL_InputProxy";
            proxyObj.transform.SetParent(tempRoot.transform);
            proxyObj.transform.localPosition = new Vector3(0, 1.1f, 0);
            proxyObj.transform.localScale = new Vector3(0.5f, 0.1f, 0.5f);
            var proxy = proxyObj.AddComponent<BGL_SyncProxy>();

            // Wiring
            SerializedObject soVis = new SerializedObject(visual);
            soVis.FindProperty("Manager").objectReferenceValue = manager;
            soVis.FindProperty("PulseCore").objectReferenceValue = coreObj.transform;
            soVis.FindProperty("CoreRenderer").objectReferenceValue = coreObj.GetComponent<MeshRenderer>();
            soVis.FindProperty("ScoreText").objectReferenceValue = sText;
            soVis.FindProperty("MilestoneText").objectReferenceValue = mText;
            soVis.FindProperty("BugWindow").objectReferenceValue = bugWindow;
            soVis.FindProperty("BugReportText").objectReferenceValue = bugReportText;
            soVis.ApplyModifiedProperties();

            SerializedObject soProxy = new SerializedObject(proxy);
            soProxy.FindProperty("Manager").objectReferenceValue = manager;
            soProxy.FindProperty("Visual").objectReferenceValue = visual;
            soProxy.ApplyModifiedProperties();

            SerializedObject soMan = new SerializedObject(manager);
            soMan.FindProperty("VisualTarget").objectReferenceValue = visual;
            soMan.ApplyModifiedProperties();

            // Prefab
            PrefabUtility.SaveAsPrefabAsset(tempRoot, prefabPath);
            DestroyImmediate(tempRoot);

            // Scene
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            GameObject.CreatePrimitive(PrimitiveType.Plane).transform.localScale = new Vector3(5, 1, 5);
            GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.transform.position = new Vector3(0, 0.5f, 0); table.transform.localScale = new Vector3(2, 1, 2);
            table.GetComponent<MeshRenderer>().sharedMaterial = tableMat;
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));

            EditorSceneManager.SaveScene(newScene, scenePath);
            Debug.Log($"[BoardGameLab] Level 0 Game + Bug Window Setup Complete: {scenePath}");
        }

        private static Material GetOrCreateMaterial(string path, string name, Color color, bool emissive = false)
        {
            string fullPath = $"{path}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(fullPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                mat.color = color;
                if (emissive) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", color); }
                AssetDatabase.CreateAsset(mat, fullPath);
            }
            return mat;
        }

        private static void EnsureProgramAsset(string scriptPath)
        {
            string assetPath = scriptPath.Replace(".cs", ".asset");
            if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(assetPath) != null)
                return;

            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            UdonSharpProgramAsset programAsset = CreateInstance<UdonSharpProgramAsset>();
            programAsset.sourceCsScript = script;
            AssetDatabase.CreateAsset(programAsset, assetPath);
        }
    }
}
