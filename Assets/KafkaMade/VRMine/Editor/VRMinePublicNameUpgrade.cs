using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class VRMinePublicNameUpgrade
{
    const string ScenePath = "Assets/KafkaMade/VRMine/Scenes/BoardGameShowcase.unity";

    [InitializeOnLoadMethod]
    static void Schedule()
    {
        if (Application.isBatchMode) return;
        EditorApplication.delayCall += Apply;
    }

    [MenuItem("VRMine/Apply Public Game Names")]
    public static void Apply()
    {
        if (Application.isPlayingOrWillChangePlaymode || !File.Exists(ScenePath)) return;
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        bool changed = false;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            Text[] labels = roots[rootIndex].GetComponentsInChildren<Text>(true);
            for (int labelIndex = 0; labelIndex < labels.Length; labelIndex++)
            {
                Text label = labels[labelIndex];
                if (label.text == "TRICK MEISTER")
                {
                    label.text = "RULEFORGE";
                    changed = true;
                }
                else if (label.text == "ORAPA MINE - AUTO PUZZLE")
                {
                    label.text = "ECHO MINE";
                    changed = true;
                }
            }
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
        if (openedHere) EditorSceneManager.CloseScene(scene, true);
    }
}