using UnityEditor;

public static class MinooRiverWorldPipeline
{
    [MenuItem("VRMine/Prepare Minoo River Osaka World")]
    public static void Prepare()
    {
        GaussianSplatBatchImporter.ImportConfigured(
            MinooRiverWorldBuilder.RegistryPath,
            MinooRiverWorldBuilder.ConfigPath);
        MinooRiverWorldBuilder.Build();
        UnityEngine.Debug.Log("Minoo River Osaka preparation completed.");
    }

    public static void PrepareBatch() => Prepare();

    public static void BuildAndVerifyBatch()
    {
        Prepare();
        MinooRiverWorldVerification.Verify();
    }
}
