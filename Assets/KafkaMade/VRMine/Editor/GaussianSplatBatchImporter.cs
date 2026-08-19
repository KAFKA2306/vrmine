using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

public static class GaussianSplatBatchImporter
{
    const string RegistryPath = "config/gaussian-splats.json";
    const string SourceDirectory = "Library/VRMine/GaussianSources";
    const string PrefabDirectory = "Assets/KafkaMade/VRMine/GaussianSplatting/Prefabs";
    const float TargetExtentMeters = 1f;

    [Serializable] sealed class Registry { public EnvironmentEntry[] environments; }
    [Serializable] sealed class EnvironmentEntry { public string id; public SourceEntry source; }
    [Serializable] sealed class SourceEntry { public string path; public long size_bytes; public string sha256; }

    [MenuItem("VRMine/Import Registered Gaussian Splats")]
    public static void ImportRegistered()
    {
        Registry registry = JsonUtility.FromJson<Registry>(File.ReadAllText(RegistryPath));
        if (registry == null || registry.environments == null || registry.environments.Length == 0)
            throw new InvalidDataException("Gaussian source registry is empty or invalid.");

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
        SetFloatField(optionsType, options, "normalizeTargetSize", TargetExtentMeters);
        SetEnumField(optionsType, options, "defaultSHBand", "SH3");

        int chunkSize = 4096;
        FieldInfo chunkField = importerType.GetField("DefaultChunkSize", BindingFlags.Public | BindingFlags.Static);
        if (chunkField != null) chunkSize = Convert.ToInt32(chunkField.GetValue(null));

        EnsureFolder(PrefabDirectory);
        int imported = 0;
        int reused = 0;
        foreach (EnvironmentEntry environment in registry.environments)
        {
            if (environment == null || string.IsNullOrEmpty(environment.id) || environment.source == null)
                throw new InvalidDataException("Gaussian registry contains an invalid source entry.");

            string sourcePath = Path.Combine(SourceDirectory, environment.id + ".ply").Replace('\\', '/');
            VerifySource(sourcePath, environment.source.size_bytes, environment.source.sha256, environment.id);
            string prefabPath = PrefabDirectory + "/" + environment.id + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                reused++;
                Debug.Log("Reusing imported Gaussian Splat prefab: " + prefabPath);
                continue;
            }

            try
            {
                object prefab = importMethod.Invoke(null, new object[] { sourcePath, prefabPath, chunkSize, options });
                if (prefab == null || AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                    throw new InvalidOperationException(environment.id + ": importer did not create the expected prefab: " + prefabPath);
                imported++;
                Debug.Log("Imported Gaussian Splat LOD at target extent " + TargetExtentMeters + " m: " + environment.id + " -> " + prefabPath);
            }
            catch (TargetInvocationException exception)
            {
                throw new InvalidOperationException(environment.id + ": upstream LOD import failed.", exception.InnerException ?? exception);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Gaussian LOD prefabs ready: count=" + registry.environments.Length + ", imported=" + imported + ", reused=" + reused + ".");
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

    static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath)) return;
        string parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(assetPath));
    }
}
