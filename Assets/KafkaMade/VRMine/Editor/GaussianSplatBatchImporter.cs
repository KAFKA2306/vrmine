using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

public static class GaussianSplatBatchImporter
{
    const string RegistryPath = "config/gaussian-splats.json";
    const string ExhibitionConfigPath = "config/gaussian-exhibition.json";
    const string SourceDirectory = "Library/VRMine/GaussianSources";
    const string PrefabDirectory = "Assets/KafkaMade/VRMine/GaussianSplatting/Prefabs";
    const string ProvenanceDirectory = "Library/VRMine/GaussianImportProvenance";
    const int ProvenanceSchemaVersion = 4;

    [Serializable] sealed class Registry
    {
        public string source_repository;
        public string source_commit;
        public Renderers renderers;
        public EnvironmentEntry[] environments;
    }

    [Serializable] sealed class Renderers { public string unity_vrchat; }
    [Serializable] sealed class EnvironmentEntry { public string id; public SourceEntry source; }
    [Serializable] sealed class SourceEntry { public string path; public long size_bytes; public string sha256; }
    [Serializable] sealed class ExhibitionConfig { public string renderer; public float target_extent_m; }

    [Serializable]
    sealed class ImportProvenance
    {
        public int schema_version;
        public string source_repository;
        public string source_commit;
        public string source_path;
        public long source_size_bytes;
        public string source_sha256;
        public string renderer;
        public string import_method;
        public int chunk_size;
        public string import_options_json;
    }

    [MenuItem("VRMine/Import Registered Gaussian Splats")]
    public static void ImportRegistered()
    {
        Registry registry = JsonUtility.FromJson<Registry>(File.ReadAllText(RegistryPath));
        if (registry == null || registry.environments == null || registry.environments.Length == 0)
            throw new InvalidDataException("Gaussian source registry is empty or invalid.");
        if (string.IsNullOrEmpty(registry.source_repository) || string.IsNullOrEmpty(registry.source_commit))
            throw new InvalidDataException("Gaussian source registry provenance is missing.");
        if (registry.renderers == null || string.IsNullOrEmpty(registry.renderers.unity_vrchat))
            throw new InvalidDataException("Gaussian source registry Unity renderer pin is missing.");

        ExhibitionConfig exhibition = JsonUtility.FromJson<ExhibitionConfig>(File.ReadAllText(ExhibitionConfigPath));
        if (exhibition == null || string.IsNullOrEmpty(exhibition.renderer))
            throw new InvalidDataException("Gaussian exhibition renderer configuration is missing.");
        if (!string.Equals(exhibition.renderer, registry.renderers.unity_vrchat, StringComparison.Ordinal))
            throw new InvalidDataException("Gaussian exhibition renderer does not match the canonical source registry renderer.");
        if (float.IsNaN(exhibition.target_extent_m) || float.IsInfinity(exhibition.target_extent_m) || exhibition.target_extent_m <= 0f)
            throw new InvalidDataException("Gaussian exhibition target_extent_m must be a positive finite value.");

        Type importerType = FindType("GaussianSplatting.GaussianSplatLODImporter");
        if (importerType == null)
            throw new InvalidOperationException("VRChatGaussianSplatting is not materialized. Run `task gaussian:prepare` before opening Unity.");

        MethodInfo importMethod = null;
        foreach (MethodInfo method in importerType.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (method.Name == "ImportLODToPrefab" && parameters.Length == 4 &&
                parameters[0].ParameterType == typeof(string) && parameters[1].ParameterType == typeof(string) &&
                parameters[2].ParameterType == typeof(int))
            {
                importMethod = method;
                break;
            }
        }
        if (importMethod == null)
            throw new MissingMethodException("Pinned GaussianSplatLODImporter.ImportLODToPrefab overload was not found.");

        Type optionsType = importMethod.GetParameters()[3].ParameterType;
        object options = Activator.CreateInstance(optionsType);
        SetBoolField(optionsType, options, "lodUsePackedPositions", true);
        SetBoolField(optionsType, options, "importSphericalHarmonics", true);
        SetBoolField(optionsType, options, "normalizeSize", true);
        SetFloatField(optionsType, options, "normalizeTargetSize", exhibition.target_extent_m);
        SetEnumField(optionsType, options, "defaultSHBand", "SH3");
        SetBoolField(optionsType, options, "lodComputeSplats", false);
        SetIntField(optionsType, options, "lodResamplePercent", 0);
        SetIntField(optionsType, options, "lodReusePercent", 0);
        SetBoolField(optionsType, options, "compressColorAlphaToBC7", false);
        SetEnumField(optionsType, options, "shCompression", "None");

        FieldInfo chunkField = importerType.GetField("DefaultChunkSize", BindingFlags.Public | BindingFlags.Static);
        if (chunkField == null)
            throw new MissingFieldException(importerType.FullName, "DefaultChunkSize");
        int chunkSize = Convert.ToInt32(chunkField.GetValue(null));
        if (chunkSize <= 0)
            throw new InvalidDataException("Pinned GaussianSplatLODImporter.DefaultChunkSize must be positive.");

        string importOptionsJson = JsonUtility.ToJson(options);
        EnsureFolder(PrefabDirectory);
        Directory.CreateDirectory(ProvenanceDirectory);

        int imported = 0;
        int reused = 0;
        foreach (EnvironmentEntry environment in registry.environments)
        {
            if (environment == null || string.IsNullOrEmpty(environment.id) || environment.source == null ||
                string.IsNullOrEmpty(environment.source.path) || string.IsNullOrEmpty(environment.source.sha256))
                throw new InvalidDataException("Gaussian registry contains an invalid source entry.");

            string sourcePath = Path.Combine(SourceDirectory, environment.id + ".ply").Replace('\\', '/');
            VerifySource(sourcePath, environment.source.size_bytes, environment.source.sha256, environment.id);

            string prefabPath = PrefabDirectory + "/" + environment.id + ".prefab";
            string provenancePath = Path.Combine(ProvenanceDirectory, environment.id + ".json");
            ImportProvenance expectedProvenance = new ImportProvenance
            {
                schema_version = ProvenanceSchemaVersion,
                source_repository = registry.source_repository,
                source_commit = registry.source_commit,
                source_path = environment.source.path,
                source_size_bytes = environment.source.size_bytes,
                source_sha256 = environment.source.sha256,
                renderer = exhibition.renderer,
                import_method = importerType.FullName + ".ImportLODToPrefab",
                chunk_size = chunkSize,
                import_options_json = importOptionsJson
            };

            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existingPrefab != null && ProvenanceMatches(provenancePath, expectedProvenance))
            {
                reused++;
                Debug.Log("Reusing verified Gaussian Splat prefab: " + prefabPath);
                continue;
            }

            if (existingPrefab != null)
                Debug.Log("Reimporting Gaussian Splat prefab because import provenance changed or is missing: " + prefabPath);
            InvalidateProvenance(provenancePath);

            try
            {
                object prefab = importMethod.Invoke(null, new object[] { sourcePath, prefabPath, chunkSize, options });
                if (prefab == null || AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                    throw new InvalidOperationException(environment.id + ": importer did not create the expected prefab: " + prefabPath);

                NormalizePrefabPresentation(prefabPath, exhibition.target_extent_m);
                WriteProvenance(provenancePath, expectedProvenance);
                imported++;
                Debug.Log("Imported Gaussian Splat LOD at target extent " + exhibition.target_extent_m + " m: " + environment.id + " -> " + prefabPath);
            }
            catch (TargetInvocationException exception)
            {
                InvalidateProvenance(provenancePath);
                throw new InvalidOperationException(environment.id + ": upstream LOD import failed.", exception.InnerException ?? exception);
            }
            catch
            {
                InvalidateProvenance(provenancePath);
                throw;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Gaussian LOD prefabs ready: count=" + registry.environments.Length + ", imported=" + imported + ", reused=" + reused + ".");
    }

    static bool ProvenanceMatches(string path, ImportProvenance expected)
    {
        if (!File.Exists(path)) return false;
        try
        {
            ImportProvenance actual = JsonUtility.FromJson<ImportProvenance>(File.ReadAllText(path));
            return actual != null &&
                actual.schema_version == expected.schema_version &&
                string.Equals(actual.source_repository, expected.source_repository, StringComparison.Ordinal) &&
                string.Equals(actual.source_commit, expected.source_commit, StringComparison.Ordinal) &&
                string.Equals(actual.source_path, expected.source_path, StringComparison.Ordinal) &&
                actual.source_size_bytes == expected.source_size_bytes &&
                string.Equals(actual.source_sha256, expected.source_sha256, StringComparison.Ordinal) &&
                string.Equals(actual.renderer, expected.renderer, StringComparison.Ordinal) &&
                string.Equals(actual.import_method, expected.import_method, StringComparison.Ordinal) &&
                actual.chunk_size == expected.chunk_size &&
                string.Equals(actual.import_options_json, expected.import_options_json, StringComparison.Ordinal);
        }
        catch (Exception)
        {
            return false;
        }
    }

    static void WriteProvenance(string path, ImportProvenance provenance)
    {
        string temporary = path + ".partial";
        File.WriteAllText(temporary, JsonUtility.ToJson(provenance, true));
        if (File.Exists(path)) File.Delete(path);
        File.Move(temporary, path);
    }

    static void InvalidateProvenance(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        string temporary = path + ".partial";
        if (File.Exists(temporary)) File.Delete(temporary);
    }

    static void VerifySource(string path, long expectedSize, string expectedHash, string id)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(id + ": materialized PLY is missing. Run `task gaussian:prepare` first.", path);
        FileInfo info = new FileInfo(path);
        if (info.Length != expectedSize)
            throw new InvalidDataException(id + ": PLY byte-size mismatch. Expected " + expectedSize + ", got " + info.Length + ".");

        using (FileStream stream = File.OpenRead(path))
        using (SHA256 sha = SHA256.Create())
        {
            string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            if (!string.Equals(actual, expectedHash, StringComparison.Ordinal))
                throw new InvalidDataException(id + ": PLY SHA-256 mismatch. Expected " + expectedHash + ", got " + actual + ".");
        }
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

    static void SetBoolField(Type type, object boxed, string name, bool value)
    {
        FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (field == null || field.FieldType != typeof(bool))
            throw new MissingFieldException(type.FullName, name);
        field.SetValue(boxed, value);
    }

    static void SetIntField(Type type, object boxed, string name, int value)
    {
        FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (field == null || field.FieldType != typeof(int))
            throw new MissingFieldException(type.FullName, name);
        field.SetValue(boxed, value);
    }

    static void SetFloatField(Type type, object boxed, string name, float value)
    {
        FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (field == null || field.FieldType != typeof(float))
            throw new MissingFieldException(type.FullName, name);
        field.SetValue(boxed, value);
    }

    static void SetEnumField(Type type, object boxed, string name, string enumName)
    {
        FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (field == null || !field.FieldType.IsEnum)
            throw new MissingFieldException(type.FullName, name);
        field.SetValue(boxed, Enum.Parse(field.FieldType, enumName));
    }

    static void NormalizePrefabPresentation(string prefabPath, float targetExtent)
    {
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Type splatType = FindType("GaussianSplatting.GaussianSplatObject");
        Component splat = root.GetComponentInChildren(splatType, true);
        object[] args = new object[] { new Bounds() };
        bool valid = (bool)splatType.GetMethod("TryGetLocalBounds").Invoke(splat, args);
        if (!valid) throw new InvalidDataException("Gaussian bounds are unavailable: " + prefabPath);
        Bounds bounds = TransformBounds(splat.transform, (Bounds)args[0]);
        float extent = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        root.transform.localScale = Vector3.one * (targetExtent / Mathf.Max(0.000001f, extent));
        EditorUtility.SetDirty(root);
    }

    static Bounds TransformBounds(Transform transform, Bounds bounds)
    {
        var result = new Bounds(transform.TransformPoint(bounds.center), Vector3.zero);
        for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                    result.Encapsulate(transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z))));
        return result;
    }

    static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath)) return;
        string parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(assetPath));
    }
}
