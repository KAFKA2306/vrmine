using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Components;

public static class GaussianExhibitionVerification
{
    const string ScenePath = "Assets/KafkaMade/VRMine/Scenes/GaussianSplatExhibition.unity";
    const string GaussianSplatObjectTypeName = "GaussianSplatting.GaussianSplatObject";
    const string GaussianSplatRendererTypeName = "GaussianSplatting.GaussianSplatRenderer";

    [MenuItem("VRMine/Verify Gaussian Splat Exhibition")]
    public static void Verify()
    {
        if (!System.IO.File.Exists(ScenePath))
            throw new InvalidOperationException("Canonical Gaussian exhibition scene does not exist. Run the builder after all upstream inputs are ready.");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var errors = new List<string>();

        if (CountSceneComponents<VRCSceneDescriptor>(scene) != 1) errors.Add("VRCSceneDescriptor count must be exactly 1");
        if (CountSceneComponents<GaussianVideoPlaylist>(scene) != 1) errors.Add("GaussianVideoPlaylist count must be exactly 1");
        if (CountSceneComponents<GaussianVideoPlaylistAction>(scene) != 23) errors.Add("playlist action count must be 23 (20 direct + prev/replay/next)");

        GameObject floor = GameObject.Find("WalkableFloor");
        if (floor == null || floor.scene != scene || floor.GetComponent<Collider>() == null) errors.Add("WalkableFloor collider is missing");

        GameObject video = GameObject.Find("SourceVideoPlayer");
        if (video == null || video.scene != scene) errors.Add("SourceVideoPlayer is missing");
        else
        {
            int activeUrlInputs = 0;
            foreach (VRCUrlInputField input in video.GetComponentsInChildren<VRCUrlInputField>(true))
                if (input.gameObject.activeInHierarchy) activeUrlInputs++;
            if (activeUrlInputs != 0) errors.Add("free-form SDK URL input must be disabled so playlist throttle cannot be bypassed");
        }

        Type splatType = FindType(GaussianSplatObjectTypeName);
        Type rendererType = FindType(GaussianSplatRendererTypeName);
        if (splatType == null || rendererType == null)
        {
            errors.Add("pinned VRChatGaussianSplatting runtime types are missing");
        }
        else
        {
            if (CountSceneComponents(splatType, scene) != 20) errors.Add("active GaussianSplatObject count must be exactly 20");
            if (CountSceneComponents(rendererType, scene) != 1) errors.Add("active GaussianSplatRenderer count must be exactly 1");
        }

        int exhibitRoots = 0;
        int missingScripts = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform transform in transforms)
            {
                if (transform.name.StartsWith("Exhibit_", StringComparison.Ordinal)) exhibitRoots++;
                missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
            }
        }
        if (exhibitRoots != 20) errors.Add("exhibit GameObject count must be exactly 20");
        if (missingScripts != 0) errors.Add("missing scripts found: " + missingScripts);

        LightingSettings lightingSettings;
        if (!Lightmapping.TryGetLightingSettings(out lightingSettings) || lightingSettings == null)
        {
            errors.Add("LightingSettings are missing");
        }
        else
        {
            if (!lightingSettings.bakedGI) errors.Add("Baked GI must be enabled");
            if (lightingSettings.realtimeGI) errors.Add("Realtime GI must be disabled");
        }
        if (Lightmapping.lightingDataAsset == null) errors.Add("Lighting Data Asset is missing; run the canonical bake pipeline");
        if (LightmapSettings.lightmaps == null || LightmapSettings.lightmaps.Length == 0) errors.Add("no baked lightmaps are assigned to the scene");

        bool buildSceneEnabled = false;
        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
            if (buildScene.path == ScenePath && buildScene.enabled) buildSceneEnabled = true;
        if (!buildSceneEnabled) errors.Add("canonical scene is not enabled in EditorBuildSettings");

        if (errors.Count > 0)
            throw new InvalidOperationException("Gaussian exhibition verification failed:\n- " + string.Join("\n- ", errors));

        Debug.Log("Gaussian exhibition verification PASS: descriptor=1, exhibits=20, splats=20, renderer=1, video=1, playlist=20, lightingData=present, lightmaps>0, missingScripts=0, buildScene=enabled");
    }

    public static void VerifyBatch()
    {
        Verify();
    }

    static int CountSceneComponents<T>(Scene scene) where T : Component
    {
        int count = 0;
        foreach (T component in Resources.FindObjectsOfTypeAll<T>())
            if (component != null && !EditorUtility.IsPersistent(component) && component.gameObject.scene == scene && component.gameObject.activeInHierarchy) count++;
        return count;
    }

    static int CountSceneComponents(Type componentType, Scene scene)
    {
        int count = 0;
        foreach (UnityEngine.Object value in Resources.FindObjectsOfTypeAll(componentType))
        {
            Component component = value as Component;
            if (component != null && !EditorUtility.IsPersistent(component) && component.gameObject.scene == scene && component.gameObject.activeInHierarchy) count++;
        }
        return count;
    }

    static Type FindType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName, false);
            if (type != null) return type;
        }
        return null;
    }
}
