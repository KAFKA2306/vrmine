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
        EditorApplication.delayCall += PrepareRequestedScene;
    }

    static void PrepareRequestedScene()
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
                exception.Message + "\n\nThe preparation marker was preserved. Fix the reported input and reopen the project (or use VRMine/Prepare Gaussian Exhibition) to retry.",
                "OK");
        }
    }

    [MenuItem("VRMine/Prepare Gaussian Exhibition")]
    public static void Prepare()
    {
        Debug.Log("VRMine 3DGS pipeline: importing every registered Gaussian Splat...");
        GaussianSplatBatchImporter.ImportRegistered();

        Debug.Log("VRMine 3DGS pipeline: building canonical scene from the current registry...");
        GaussianExhibitionBuilder.Build();
        GaussianExhibitionPresentation.Apply();
        ConfigureBakedLighting();
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("VRMine 3DGS scene ready. Registered PLYs, presentation scale, floor/world shell, spawn, labels, playback controls, lighting configuration and one Gaussian renderer are wired in GaussianSplatExhibition.unity.");
    }

    public static void PrepareLocal() => Prepare();

    [MenuItem("VRMine/Build And Verify Gaussian Exhibition For SDK")]
    public static void BuildAndVerify()
    {
        Prepare();

        Debug.Log("VRMine 3DGS pipeline: baking static shell lighting...");
        if (!Lightmapping.Bake())
            throw new InvalidOperationException("Unity failed to complete the synchronous baked-lighting job.");
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("VRMine 3DGS pipeline: verifying upload-readiness scene contract against the current registry...");
        GaussianExhibitionVerification.Verify();

        Debug.Log("VRMine 3DGS pipeline PASS. Repository-side preparation is complete; continue with the canonical VRChat SDK Build & Test / Publish flow.");
    }

    // Unity command-line entry point:
    // Unity.exe -batchmode -quit -projectPath <repo> -executeMethod GaussianExhibitionPipeline.BuildAndVerifyBatch
    public static void BuildAndVerifyBatch() => BuildAndVerify();

    public static void PrepareBatch() => Prepare();

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
