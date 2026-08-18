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
    const int FinalExhibitCount = 20;

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
            throw new InvalidOperationException("VRChatGaussianSplatting is not materialized. Run `task gaussian:renderer` first.");

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
        SetField(optionsType, ref options, "lodUsePackedPositions", true);
        SetField(optionsType, ref options, "importSphericalHarmonics", true);
        SetField(optionsType, ref options, "normalizeSize", true);
        SetEnumField(optionsType, ref options, "defaultSHBand", "SH3");

        int chunkSize = 4096;
        FieldInfo chunkField = importerType.GetField("DefaultChunkSize", BindingFlags.Public | BindingFlags.Static);
        if (chunkField != null) chunkSize = Convert.ToInt32(chunkField.GetValue(null));

        EnsureFolder(PrefabDirectory);
        foreach (EnvironmentEntry environment in registry.environments)
        {
            if (environment == null || string.IsNullOrEmpty(environment.id) || environment.source == null)
                throw new InvalidDataException("Gaussian registry contains an invalid source entry.");

            string sourcePath = Path.Combine(SourceDirectory, environment.id + ".ply").Replace('\\', '/');
            VerifySource(sourcePath, environment.source.size_bytes, environment.source.sha256, environment.id);
            string prefabPath = PrefabDirectory + "/" + environment.id + ".prefab";

            try
            {
                object prefab = importMethod.Invoke(null, new object[] { sourcePath, prefabPath, chunkSize, options });
                if (prefab == null || AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                    throw new InvalidOperationException(environment.id + ": importer did not create the expected prefab: " + prefabPath);
                Debug.Log("Imported Gaussian Splat LOD: " + environment.id + " -> " + prefabPath);
            }
            catch (TargetInvocationException exception)
            {
                throw new InvalidOperationException(environment.id + ": upstream LOD import failed.", exception.InnerException ?? exception);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (registry.environments.Length != FinalExhibitCount)
            Debug.LogWarning("Gaussian LOD import completed for " + registry.environments.Length + "/" + FinalExhibitCount +
                " registered sources. Final exhibition remains blocked until AutoPhotogrammetry supplies the remaining " +
                (FinalExhibitCount - registry.environments.Length) + " source(s).");
    }

    static void VerifySource(string path, long expectedSize, string expectedHash, string id)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(id + ": materialized PLY is missing. Run `task gaussian:sources` first.", path);
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

    static void SetField(Type type, ref object boxed, string name, bool value)
    {
        FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (field == null || field.FieldType != typeof(bool))
            throw new MissingFieldException(type.FullName, name);
        field.SetValue(boxed, value);
    }

    static void SetEnumField(Type type, ref object boxed, string name, string enumName)
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
