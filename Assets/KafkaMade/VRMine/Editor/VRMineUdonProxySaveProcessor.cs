using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class VRMineUdonProxySaveProcessor : AssetModificationProcessor
{
    const string ScenePath = "Assets/KafkaMade/VRMine/Scenes/BoardGameShowcase.unity";
    static bool applying;

    static string[] OnWillSaveAssets(string[] paths)
    {
        if (applying) return paths;
        bool savesReleaseScene = false;
        for (int i = 0; i < paths.Length; i++)
        {
            if (paths[i] != ScenePath) continue;
            savesReleaseScene = true;
            break;
        }
        if (!savesReleaseScene) return paths;

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) return paths;

        applying = true;
        try
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                UdonSharpBehaviour[] proxies = roots[rootIndex].GetComponentsInChildren<UdonSharpBehaviour>(true);
                for (int proxyIndex = 0; proxyIndex < proxies.Length; proxyIndex++)
                {
                    UdonSharpBehaviour proxy = proxies[proxyIndex];
                    if (proxy == null) continue;
                    proxy.ApplyProxyModifications();
                }
            }
        }
        finally
        {
            applying = false;
        }
        return paths;
    }
}
