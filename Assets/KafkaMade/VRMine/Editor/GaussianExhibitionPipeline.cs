using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GaussianExhibitionPipeline
{
    static string PrepareOnOpenMarker => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library", "VRMine", "prepare-gaussian-on-open");

    [InitializeOnLoadMethod]
    static void QueuePreparedOpen()
    {
        if (!File.Exists(PrepareOnOpenMarker)) return;
        EditorApplication.delayCall += PrepareRequestedLocalScene;
    }

    static void PrepareRequestedLocalScene()
    {
        if (!File.Exists(PrepareOnOpenMarker)) return;
        try
        {
            PrepareLocal();
            File.Delete(PrepareOnOpenMarker);
            Debug.Log("VRMine 3DGS preparation marker consumed after successful scene preparation.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "VRMine Gaussian preparation failed",
                exception.Message + "\n\nThe preparation marker was preserved. Fix the reported input and reopen the project (or use VRMine/Prepare Registered Gaussian Exhibition) to retry.",
                "OK");
        }
    }

    [MenuItem("VRMine/Prepare Registered Gaussian Exhibition")]
    public static void PrepareLocal()
    {
        Debug.Log("VRMine 3DGS local pipeline: importing registered Gaussian Splats...");
        GaussianSplatBatchImporter.ImportRegistered();

        Debug.Log("VRMine 3DGS local pipeline: building canonical scene from all registered sources...");
        GaussianExhibitionBuilder.BuildLocalPreview();
        GaussianExhibitionPresentation.Apply();
        ConfigureBakedLighting();
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("VRMine 3DGS local scene ready. Registered PLYs, configured presentation scale, floor/world shell, spawn, labels, lighting configuration and one Gaussian renderer are wired in GaussianSplatExhibition.unity.");
    }

    [MenuItem("VRMine/Prepare Final Gaussian Exhibition For SDK")]
    public static void BuildAndVerify()
    {
        Debug.Log("VRMine 3DGS final pipeline: importing registered Gaussian Splats...");
        GaussianSplatBatchImporter.ImportRegistered();

        Debug.Log("VRMine 3DGS final pipeline: requiring final product count and playlist...");
        GaussianExhibitionBuilder.BuildFinal();
        GaussianExhibitionPresentation.Apply();
        ConfigureBakedLighting();
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("VRMine 3DGS final pipeline: baking static shell lighting...");
        if (!Lightmapping.Bake())
            throw new InvalidOperationException("Unity failed to complete the synchronous baked-lighting job.");
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("VRMine 3DGS final pipeline: verifying upload-readiness scene contract...");
        GaussianExhibitionVerification.Verify();

        Debug.Log("VRMine 3DGS final pipeline PASS. Repository-side preparation is complete; continue with the canonical VRChat SDK Build & Test / Publish flow.");
    }

    // Unity command-line entry point:
    // Unity.exe -batchmode -quit -projectPath <repo> -executeMethod GaussianExhibitionPipeline.BuildAndVerifyBatch
    public static void BuildAndVerifyBatch() => BuildAndVerify();

    // Optional local batch entry point when only currently registered sources are needed.
    public static void PrepareLocalBatch() => PrepareLocal();

    static void ConfigureBakedLighting()
    {
        LightingSettings settings;
        if (!Lightmapping.TryGetLightingSettings(out settings) || settings == null)
        {
            settings = new LightingSettings();
            Lightmapping.lightingSettings = settings;
        }
        settings.autoGenerate = false;
        settings.bakedGI = true;
        settings.realtimeGI = false;
        EditorUtility.SetDirty(settings);
    }
}
