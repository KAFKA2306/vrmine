using UnityEditor;
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
}
