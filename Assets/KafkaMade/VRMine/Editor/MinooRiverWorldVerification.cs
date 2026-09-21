using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using VRC.Core;
using VRC.SDK3.Components;
using VRC.SDK3.Editor;

public static class MinooRiverWorldVerification
{
    const string GaussianSplatObjectTypeName = "GaussianSplatting.GaussianSplatObject";
    const string GaussianSplatRendererTypeName = "GaussianSplatting.GaussianSplatRenderer";
    const string SourceDirectory = "Library/VRMine/GaussianSources";
    const string PrefabDirectory = "Assets/KafkaMade/VRMine/GaussianSplatting/Prefabs/minoo-river-osaka";

    [Serializable]
    sealed class Config
    {
        public float target_extent_m;
        public float presentation_scale_multiplier;
        public bool show_title_sign;
        public string scene_path;
        public string source_registry;
    }

    [Serializable]
    sealed class Registry
    {
        public EnvironmentEntry[] environments;
    }

    [Serializable]
    sealed class EnvironmentEntry
    {
        public string id;
        public SourceEntry source;
    }

    [Serializable]
    sealed class SourceEntry
    {
        public long size_bytes;
        public string sha256;
    }

    [Serializable]
    sealed class Evidence
    {
        public string activeScene;
        public string sourceId;
        public long sourcePlyBytes;
        public string sourcePlySha256;
        public int gaussianSplatObjects;
        public int renderers;
        public int descriptors;
        public int pipelineManagers;
        public int spawnPoints;
        public int referenceCameras;
        public int missingScripts;
        public int enabledBuildScenes;
        public bool buildSceneEnabled;
        public float measuredExtent;
        public float expectedExtent;
        public bool sceneDirty;
        public string status;
    }

    [Serializable]
    sealed class PerformanceEvidence
    {
        public string activeScene;
        public long sourcePlyBytes;
        public long importedAssetBytes;
        public int importedAssetFiles;
        public long sceneBytes;
        public string status;
    }

    [MenuItem("VRMine/Verify Minoo River Osaka World")]
    public static void Verify()
    {
        Config config = LoadJson<Config>(MinooRiverWorldBuilder.ConfigPath, "Minoo River world config");
        Registry registry = LoadJson<Registry>(config.source_registry, "Minoo River source registry");
        if (registry.environments == null || registry.environments.Length != 1)
            throw new InvalidDataException("Minoo River verification requires exactly one source.");
        EnvironmentEntry source = registry.environments[0];
        Scene scene = EditorSceneManager.OpenScene(config.scene_path, OpenSceneMode.Single);
        Type splatType = FindType(GaussianSplatObjectTypeName);
        Type rendererType = FindType(GaussianSplatRendererTypeName);
        if (splatType == null || rendererType == null)
            throw new InvalidOperationException("Pinned Gaussian runtime types are missing.");

        string sourcePath = Path.Combine(SourceDirectory, source.id + ".ply").Replace('\\', '/');
        VerifySource(sourcePath, source.source);
        float measuredExtent = MeasureExtent(splatType, scene);
        float expectedExtent = config.target_extent_m * config.presentation_scale_multiplier;
        var evidence = new Evidence
        {
            activeScene = scene.path,
            sourceId = source.id,
            sourcePlyBytes = new FileInfo(sourcePath).Length,
            sourcePlySha256 = ComputeSha256(sourcePath),
            gaussianSplatObjects = CountSceneComponents(splatType, scene),
            renderers = CountSceneComponents(rendererType, scene),
            descriptors = CountSceneComponents<VRCSceneDescriptor>(scene),
            pipelineManagers = CountSceneComponents<PipelineManager>(scene),
            spawnPoints = CountNamed(scene, "SpawnPoint"),
            referenceCameras = CountNamed(scene, "ReferenceCamera"),
            missingScripts = CountMissingScripts(scene),
            enabledBuildScenes = CountEnabledBuildScenes(),
            buildSceneEnabled = IsBuildSceneEnabled(scene.path),
            measuredExtent = measuredExtent,
            expectedExtent = expectedExtent,
            sceneDirty = scene.isDirty,
            status = "PASS"
        };

        var errors = new List<string>();
        if (evidence.gaussianSplatObjects != 1) errors.Add("active GaussianSplatObject count must be 1");
        if (evidence.renderers != 1) errors.Add("active GaussianSplatRenderer count must be 1");
        if (evidence.descriptors != 1) errors.Add("VRCSceneDescriptor count must be 1");
        if (evidence.pipelineManagers != 1) errors.Add("PipelineManager count must be 1");
        if (evidence.spawnPoints != 1) errors.Add("SpawnPoint count must be 1");
        if (evidence.referenceCameras != 1) errors.Add("ReferenceCamera count must be 1");
        if (evidence.missingScripts != 0) errors.Add("missing scripts found: " + evidence.missingScripts);
        if (!evidence.buildSceneEnabled) errors.Add("Minoo River scene is not enabled in EditorBuildSettings");
        if (Mathf.Abs(measuredExtent - expectedExtent) > 0.1f)
            errors.Add("measured Gaussian extent " + measuredExtent.ToString("F3") + " differs from expected " + expectedExtent.ToString("F3"));
        GameObject floor = GameObject.Find("WalkableFloor");
        if (floor == null || floor.GetComponent<Collider>() == null) errors.Add("WalkableFloor collider is missing");
        if (floor != null && floor.GetComponent<Renderer>() != null && floor.GetComponent<Renderer>().enabled)
            errors.Add("WalkableFloor renderer must be disabled so it cannot occlude the Gaussian capture");
        GameObject water = GameObject.Find("RiverWaterMarker");
        if (water == null || water.GetComponent<Collider>() == null) errors.Add("RiverWaterMarker is missing");
        if (water != null && water.GetComponent<Renderer>() != null && water.GetComponent<Renderer>().enabled)
            errors.Add("RiverWaterMarker renderer must be disabled; it is a non-visual guide only");
        if (!config.show_title_sign && GameObject.Find("WorldTitleSign") != null)
            errors.Add("WorldTitleSign must be absent when show_title_sign is false");
        GameObject cameraObject = GameObject.Find("ReferenceCamera");
        Camera referenceCamera = cameraObject == null ? null : cameraObject.GetComponent<Camera>();
        if (referenceCamera == null || referenceCamera.clearFlags != CameraClearFlags.Skybox)
            errors.Add("ReferenceCamera must use the authored river skybox");
        if (RenderSettings.skybox == null) errors.Add("Minoo River skybox is missing");
        if (errors.Count > 0)
        {
            evidence.status = "FAIL";
            WriteEvidence("Library/VRMine/minoo-river-u2-evidence.json", evidence);
            throw new InvalidOperationException("Minoo River world verification failed:\n- " + string.Join("\n- ", errors));
        }

        EditorSceneManager.SaveScene(scene);
        evidence.sceneDirty = scene.isDirty;
        WriteEvidence("Library/VRMine/minoo-river-u2-evidence.json", evidence);
        Debug.Log("Minoo River world verification PASS: scene=" + scene.path + ", source=" + source.id + ", extent=" + measuredExtent.ToString("F3") + ", renderer=1, descriptor=1, missingScripts=0");
    }

    public static void VerifyBatch() => Verify();

    public static void VerifySdkWorldBuilderBatch()
    {
        Config config = LoadJson<Config>(MinooRiverWorldBuilder.ConfigPath, "Minoo River world config");
        EditorSceneManager.OpenScene(config.scene_path, OpenSceneMode.Single);
        VRCSdkControlPanel panel = EditorWindow.GetWindow<VRCSdkControlPanel>();
        var builder = new VRCSdkControlPanelWorldBuilder();
        builder.RegisterBuilder(panel);
        builder.Initialize();
        if (!builder.IsValidBuilder(out string message)) throw new InvalidOperationException(message);
        builder.CreateValidationsGUI(new VisualElement());
        Debug.Log("Minoo River SDK world builder validation completed without exception: scene=" + config.scene_path + ", sdk=3.9.0");
    }

    public static void VerifyPerformanceBatch()
    {
        Config config = LoadJson<Config>(MinooRiverWorldBuilder.ConfigPath, "Minoo River world config");
        EditorSceneManager.OpenScene(config.scene_path, OpenSceneMode.Single);
        string sourcePath = Path.Combine(SourceDirectory, "minoo-river-osaka.ply");
        string[] importedFiles = Directory.Exists(PrefabDirectory) ? Directory.GetFiles(PrefabDirectory, "*", SearchOption.AllDirectories) : Array.Empty<string>();
        var evidence = new PerformanceEvidence
        {
            activeScene = config.scene_path,
            sourcePlyBytes = File.Exists(sourcePath) ? new FileInfo(sourcePath).Length : 0,
            importedAssetFiles = CountNonMetaFiles(importedFiles),
            importedAssetBytes = SumNonMetaFiles(importedFiles),
            sceneBytes = File.Exists(config.scene_path) ? new FileInfo(config.scene_path).Length : 0,
            status = File.Exists(sourcePath) && importedFiles.Length > 0 ? "MEASURED_LOCAL_WORLD" : "BLOCKED_LOCAL_WORLD"
        };
        WriteEvidence("Library/VRMine/minoo-river-performance-evidence.json", evidence);
        if (evidence.status != "MEASURED_LOCAL_WORLD") throw new InvalidOperationException("Minoo River performance evidence is incomplete.");
        Debug.Log("Minoo River performance evidence: sourcePlyBytes=" + evidence.sourcePlyBytes + ", importedAssetBytes=" + evidence.importedAssetBytes + ", sceneBytes=" + evidence.sceneBytes + ", status=" + evidence.status);
    }

    static float MeasureExtent(Type splatType, Scene scene)
    {
        float extent = 0f;
        foreach (UnityEngine.Object value in Resources.FindObjectsOfTypeAll(splatType))
        {
            Component component = value as Component;
            if (component == null || EditorUtility.IsPersistent(component) || component.gameObject.scene != scene || !component.gameObject.activeInHierarchy) continue;
            MethodInfo method = splatType.GetMethod("TryGetLocalBounds", BindingFlags.Public | BindingFlags.Instance);
            if (method == null) throw new MissingMethodException(splatType.FullName, "TryGetLocalBounds");
            object[] args = new object[] { new Bounds() };
            if (!(bool)method.Invoke(component, args)) throw new InvalidOperationException("Gaussian bounds are unavailable.");
            Bounds bounds = TransformBounds(component.transform, (Bounds)args[0]);
            extent = Mathf.Max(extent, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
        }
        return extent;
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

    static void VerifySource(string path, SourceEntry source)
    {
        if (source == null || !File.Exists(path)) throw new FileNotFoundException("Minoo source PLY is missing.", path);
        FileInfo info = new FileInfo(path);
        if (info.Length != source.size_bytes) throw new InvalidDataException("Minoo source PLY size mismatch.");
        string hash = ComputeSha256(path);
        if (!string.Equals(hash, source.sha256, StringComparison.Ordinal)) throw new InvalidDataException("Minoo source PLY SHA-256 mismatch.");
    }

    static string ComputeSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using SHA256 sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    static int CountNamed(Scene scene, string prefix)
    {
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                if (transform.name.StartsWith(prefix, StringComparison.Ordinal)) count++;
        return count;
    }

    static int CountMissingScripts(Scene scene)
    {
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                count += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
        return count;
    }

    static int CountSceneComponents<T>(Scene scene) where T : Component
    {
        int count = 0;
        foreach (T component in Resources.FindObjectsOfTypeAll<T>())
            if (component != null && !EditorUtility.IsPersistent(component) && component.gameObject.scene == scene && component.gameObject.activeInHierarchy) count++;
        return count;
    }

    static int CountSceneComponents(Type componentType, Scene scene)
    {
        if (componentType == null) return 0;
        int count = 0;
        foreach (UnityEngine.Object value in Resources.FindObjectsOfTypeAll(componentType))
        {
            Component component = value as Component;
            if (component != null && !EditorUtility.IsPersistent(component) && component.gameObject.scene == scene && component.gameObject.activeInHierarchy) count++;
        }
        return count;
    }

    static bool IsBuildSceneEnabled(string scenePath)
    {
        int matches = 0;
        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
            if (buildScene.enabled && buildScene.path == scenePath) matches++;
        return matches == 1;
    }

    static int CountEnabledBuildScenes()
    {
        int count = 0;
        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
            if (buildScene.enabled) count++;
        return count;
    }

    static int CountNonMetaFiles(string[] paths)
    {
        int count = 0;
        foreach (string path in paths) if (!path.EndsWith(".meta", StringComparison.Ordinal)) count++;
        return count;
    }

    static long SumNonMetaFiles(string[] paths)
    {
        long total = 0;
        foreach (string path in paths) if (!path.EndsWith(".meta", StringComparison.Ordinal)) total += new FileInfo(path).Length;
        return total;
    }

    static void WriteEvidence<T>(string path, T evidence)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonUtility.ToJson(evidence, true));
    }

    static T LoadJson<T>(string path, string label) where T : class
    {
        if (!File.Exists(path)) throw new FileNotFoundException(label + " is missing.", path);
        T value = JsonUtility.FromJson<T>(File.ReadAllText(path));
        if (value == null) throw new InvalidDataException(label + " could not be parsed.");
        return value;
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
}
