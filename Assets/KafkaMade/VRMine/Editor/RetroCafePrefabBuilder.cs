using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RetroCafePrefabBuilder
{
    const string UnityVersion = "2022.3.22f1";
    const string RootAssetPath = "Assets/KafkaMade/VRMine/RetroCafe";
    const string ModelsAssetPath = RootAssetPath + "/Models";
    const string PrefabsAssetPath = RootAssetPath + "/Prefabs";
    const string EvidencePath = "Library/VRMine/retro-cafe-u2.json";

    static readonly string[] Names =
    {
        "pendant-light", "table-lamp", "wall-light", "round-table", "stool",
        "side-table", "cup", "saucer", "tray", "vase"
    };

    [Serializable]
    sealed class Manifest
    {
        public ModelRecord[] models;
    }

    [Serializable]
    sealed class ModelRecord
    {
        public string name;
        public float[] dimensions_m;
        public int triangles;
    }

    [Serializable]
    sealed class ModelEvidence
    {
        public string name;
        public string modelAssetPath;
        public string prefabAssetPath;
        public int meshCount;
        public int triangleCount;
        public int materialCount;
        public Vector3 boundsMeters;
    }

    [Serializable]
    sealed class Evidence
    {
        public string status;
        public string unityVersion;
        public string sourceDirectory;
        public int prefabCount;
        public ModelEvidence[] models;
    }

    public static void BuildAndVerifyBatch()
    {
        if (Application.unityVersion != UnityVersion)
            throw new InvalidOperationException("Retro Cafe U2 requires Unity " + UnityVersion + ", actual=" + Application.unityVersion);

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string sourceDirectory = Environment.GetEnvironmentVariable("VRMINE_CAFE_SOURCE_DIR");
        if (string.IsNullOrWhiteSpace(sourceDirectory))
            sourceDirectory = Path.Combine(projectRoot, ".artifacts", "retro-cafe");
        else if (!Path.IsPathRooted(sourceDirectory))
            sourceDirectory = Path.GetFullPath(Path.Combine(projectRoot, sourceDirectory));

        string manifestPath = Path.Combine(sourceDirectory, "manifest.json");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("Retro Cafe manifest is missing.", manifestPath);

        Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
        if (manifest == null || manifest.models == null)
            throw new InvalidOperationException("Retro Cafe manifest could not be parsed.");

        var records = manifest.models.ToDictionary(record => record.name, StringComparer.Ordinal);
        if (records.Count != Names.Length || Names.Any(name => !records.ContainsKey(name)))
            throw new InvalidOperationException("Retro Cafe manifest must contain exactly the canonical ten models.");

        EnsureAssetFolder(RootAssetPath);
        EnsureAssetFolder(ModelsAssetPath);
        EnsureAssetFolder(PrefabsAssetPath);

        var evidenceModels = new List<ModelEvidence>();
        foreach (string name in Names)
        {
            ModelRecord record = records[name];
            if (record.dimensions_m == null || record.dimensions_m.Length != 3)
                throw new InvalidOperationException("Invalid dimensions_m for " + name);

            string sourceFbx = Path.Combine(sourceDirectory, name + ".fbx");
            if (!File.Exists(sourceFbx))
                throw new FileNotFoundException("Retro Cafe FBX is missing.", sourceFbx);

            string modelAssetPath = ModelsAssetPath + "/" + name + ".fbx";
            string prefabAssetPath = PrefabsAssetPath + "/" + name + ".prefab";
            CopyIntoAssets(projectRoot, sourceFbx, modelAssetPath);

            AssetDatabase.ImportAsset(modelAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelAssetPath);
            if (model == null)
                throw new InvalidOperationException("Unity did not import a GameObject from " + modelAssetPath);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            if (instance == null)
                throw new InvalidOperationException("Could not instantiate imported model " + modelAssetPath);

            instance.name = name;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, prefabAssetPath);
            UnityEngine.Object.DestroyImmediate(instance);
            if (saved == null)
                throw new InvalidOperationException("Could not save prefab " + prefabAssetPath);

            evidenceModels.Add(VerifyPrefab(name, modelAssetPath, prefabAssetPath, record));
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string evidenceFullPath = Path.Combine(projectRoot, EvidencePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(evidenceFullPath));
        var evidence = new Evidence
        {
            status = "PASS",
            unityVersion = Application.unityVersion,
            sourceDirectory = sourceDirectory,
            prefabCount = evidenceModels.Count,
            models = evidenceModels.ToArray()
        };
        File.WriteAllText(evidenceFullPath, JsonUtility.ToJson(evidence, true) + Environment.NewLine);
        Debug.Log("Retro Cafe Unity prefab verification PASS: prefabs=" + evidence.prefabCount + ", evidence=" + EvidencePath);
    }

    static ModelEvidence VerifyPrefab(string name, string modelAssetPath, string prefabAssetPath, ModelRecord record)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabAssetPath);
        try
        {
            if (root.transform.localPosition != Vector3.zero ||
                root.transform.localRotation != Quaternion.identity ||
                root.transform.localScale != Vector3.one)
                throw new InvalidOperationException("Prefab root transform drift: " + name);

            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            SkinnedMeshRenderer[] skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Mesh[] meshes = filters.Select(filter => filter.sharedMesh)
                .Concat(skinned.Select(renderer => renderer.sharedMesh))
                .Where(mesh => mesh != null)
                .Distinct()
                .ToArray();
            if (meshes.Length == 0)
                throw new InvalidOperationException("Prefab contains no meshes: " + name);

            int triangles = meshes.Sum(mesh => mesh.triangles.Length / 3);
            if (triangles != record.triangles)
                throw new InvalidOperationException("Triangle count drift for " + name + ": expected=" + record.triangles + ", actual=" + triangles);

            Material[] materials = root.GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null)
                .Distinct()
                .ToArray();
            if (materials.Length == 0)
                throw new InvalidOperationException("Prefab contains no materials: " + name);

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException("Prefab contains no renderers: " + name);

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1))
                bounds.Encapsulate(renderer.bounds);

            Vector3 expected = new Vector3(record.dimensions_m[0], record.dimensions_m[2], record.dimensions_m[1]);
            float tolerance = Mathf.Max(0.002f, expected.magnitude * 0.005f);
            if (!Close(bounds.size.x, expected.x, tolerance) ||
                !Close(bounds.size.y, expected.y, tolerance) ||
                !Close(bounds.size.z, expected.z, tolerance))
                throw new InvalidOperationException(
                    "Unity bounds drift for " + name + ": expected=" + expected + ", actual=" + bounds.size + ", tolerance=" + tolerance);

            return new ModelEvidence
            {
                name = name,
                modelAssetPath = modelAssetPath,
                prefabAssetPath = prefabAssetPath,
                meshCount = meshes.Length,
                triangleCount = triangles,
                materialCount = materials.Length,
                boundsMeters = bounds.size
            };
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static bool Close(float actual, float expected, float tolerance)
    {
        return Mathf.Abs(actual - expected) <= tolerance;
    }

    static void CopyIntoAssets(string projectRoot, string sourcePath, string assetPath)
    {
        string destination = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        File.Copy(sourcePath, destination, true);
    }

    static void EnsureAssetFolder(string assetPath)
    {
        string[] parts = assetPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
