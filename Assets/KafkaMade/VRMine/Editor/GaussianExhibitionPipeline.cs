using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GaussianExhibitionPipeline
{
    [MenuItem("VRMine/Prepare Gaussian Exhibition For SDK")]
    public static void BuildAndVerify()
    {
        Debug.Log("VRMine 3DGS pipeline: importing registered Gaussian Splats...");
        GaussianSplatBatchImporter.ImportRegistered();

        Debug.Log("VRMine 3DGS pipeline: building canonical exhibition scene...");
        GaussianExhibitionBuilder.Build();

        Debug.Log("VRMine 3DGS pipeline: preparing volumetric light probes and baked GI settings...");
        PrepareLightProbeVolume();
        ConfigureBakedLighting();
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("VRMine 3DGS pipeline: baking static shell lighting...");
        if (!Lightmapping.Bake())
            throw new InvalidOperationException("Unity failed to complete the synchronous baked-lighting job.");
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("VRMine 3DGS pipeline: verifying upload-readiness scene contract...");
        GaussianExhibitionVerification.Verify();

        Debug.Log("VRMine 3DGS pipeline PASS. Repository-side preparation is complete; continue with the canonical VRChat SDK Build & Test / Publish flow.");
    }

    // Unity CLI entry point:
    // Unity.exe -batchmode -quit -projectPath <repo> -executeMethod GaussianExhibitionPipeline.BuildAndVerifyBatch
    public static void BuildAndVerifyBatch()
    {
        BuildAndVerify();
    }

    static void PrepareLightProbeVolume()
    {
        LightProbeGroup group = UnityEngine.Object.FindObjectOfType<LightProbeGroup>();
        if (group == null) throw new InvalidOperationException("Gaussian exhibition LightProbeGroup is missing.");

        group.probePositions = new[]
        {
            new Vector3(-9f, 0.5f, -3f), new Vector3(-3f, 0.5f, -3f), new Vector3(3f, 0.5f, -3f), new Vector3(9f, 0.5f, -3f),
            new Vector3(-9f, 0.5f,  3f), new Vector3(-3f, 0.5f,  3f), new Vector3(3f, 0.5f,  3f), new Vector3(9f, 0.5f,  3f),
            new Vector3(-9f, 2.5f, -3f), new Vector3(-3f, 2.5f, -3f), new Vector3(3f, 2.5f, -3f), new Vector3(9f, 2.5f, -3f),
            new Vector3(-9f, 2.5f,  3f), new Vector3(-3f, 2.5f,  3f), new Vector3(3f, 2.5f,  3f), new Vector3(9f, 2.5f,  3f),
        };
        EditorUtility.SetDirty(group);
    }

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
