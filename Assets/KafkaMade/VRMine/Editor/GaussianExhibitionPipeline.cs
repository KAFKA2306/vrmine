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

        Debug.Log("VRMine 3DGS pipeline: preparing volumetric light probes...");
        PrepareLightProbeVolume();
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("VRMine 3DGS pipeline: baking static shell lighting...");
        Lightmapping.Bake();
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("VRMine 3DGS pipeline: verifying upload-readiness scene contract...");
        GaussianExhibitionVerification.Verify();

        Debug.Log("VRMine 3DGS pipeline PASS. Repository-side preparation is complete; continue with the canonical VRChat SDK Build & Test / Publish flow.");
    }

    // Unity CLI entry point:
    // Unity.exe -batchmode -quit -projectPath <repo> -executeMethod GaussianExhibitionPipeline.BuildAndVerify
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
}
