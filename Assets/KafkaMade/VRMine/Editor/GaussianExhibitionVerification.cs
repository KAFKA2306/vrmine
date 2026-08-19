using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using VRC.Core;
using VRC.SDK3.Components;
using VRC.SDK3.Editor;

public static class GaussianExhibitionVerification
{
    const string ScenePath = "Assets/KafkaMade/VRMine/Scenes/GaussianSplatExhibition.unity";
    const string GaussianSplatObjectTypeName = "GaussianSplatting.GaussianSplatObject";
    const string GaussianSplatRendererTypeName = "GaussianSplatting.GaussianSplatRenderer";

    [Serializable]
    sealed class RegisteredMeasurement
    {
        public string id;
        public string prefabPath;
        public Vector3 localMin;
        public Vector3 localMax;
        public Vector3 worldMin;
        public Vector3 worldMax;
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale;
        public float extent;
        public float floorBottom;
    }

    [Serializable]
    sealed class RegisteredEvidence
    {
        public string activeScene;
        public int registered;
        public int gaussianSplatObjects;
        public int prefabs;
        public int exhibits;
        public int pads;
        public int labels;
        public int renderers;
        public int descriptors;
        public int pipelineManagers;
        public int spawnPoints;
        public int referenceCameras;
        public int enabledBuildScenes;
        public bool canonicalBuildSceneOnly;
        public int missingScripts;
        public bool sceneDirty;
        public RegisteredMeasurement[] measurements;
    }

    [Serializable]
    sealed class PerformanceEvidence
    {
        public string activeScene;
        public int registered;
        public int exhibits;
        public int renderers;
        public int videoPlayers;
        public int sourcePlyFiles;
        public long sourcePlyBytes;
        public int importedAssetFiles;
        public long importedAssetBytes;
        public long sceneBytes;
        public string status;
    }

    [MenuItem("VRMine/Verify Registered Gaussian Exhibition")]
    public static void VerifyRegisteredBatch()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Type splatType = FindType(GaussianSplatObjectTypeName);
        Type rendererType = FindType(GaussianSplatRendererTypeName);
        if (splatType == null || rendererType == null) throw new InvalidOperationException("Gaussian runtime types are missing");

        var evidence = new RegisteredEvidence
        {
            activeScene = scene.path,
            registered = CountRegisteredSources(),
            gaussianSplatObjects = CountSceneComponents(splatType, scene),
            prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/KafkaMade/VRMine/GaussianSplatting/Prefabs" }).Length,
            exhibits = CountNamed(scene, "Exhibit_"),
            pads = CountNamed(scene, "ExhibitPad_"),
            labels = CountNamed(scene, "ExhibitLabel_"),
            renderers = CountSceneComponents(rendererType, scene),
            descriptors = CountSceneComponents<VRCSceneDescriptor>(scene),
            pipelineManagers = CountSceneComponents<PipelineManager>(scene),
            spawnPoints = CountNamed(scene, "SpawnPoint"),
            referenceCameras = CountNamed(scene, "ReferenceCamera"),
            enabledBuildScenes = CountEnabledBuildScenes(),
            canonicalBuildSceneOnly = HasOnlyCanonicalBuildScene(),
            missingScripts = CountMissingScripts(scene),
            sceneDirty = scene.isDirty,
            measurements = MeasureSplats(scene)
        };

        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        evidence.sceneDirty = scene.isDirty;

        if (evidence.descriptors != 1 || evidence.pipelineManagers != 1)
            throw new InvalidOperationException("World bootstrap counts are invalid: descriptors=" + evidence.descriptors + ", pipelineManagers=" + evidence.pipelineManagers);

        string evidencePath = "Library/VRMine/gaussian-u2-evidence.json";
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(evidencePath));
        System.IO.File.WriteAllText(evidencePath, JsonUtility.ToJson(evidence, true));
        foreach (RegisteredMeasurement measurement in evidence.measurements)
            Debug.Log("Gaussian U2 measurement: id=" + measurement.id + ", extent=" + measurement.extent.ToString("F6") + ", floorBottom=" + measurement.floorBottom.ToString("F6") + ", position=" + measurement.position);
        Debug.Log("Gaussian U2 evidence: scene=" + evidence.activeScene + ", registered=" + evidence.registered + ", splats=" + evidence.gaussianSplatObjects + ", prefabs=" + evidence.prefabs + ", exhibits=" + evidence.exhibits + ", pads=" + evidence.pads + ", labels=" + evidence.labels + ", renderer=" + evidence.renderers + ", descriptor=" + evidence.descriptors + ", pipelineManager=" + evidence.pipelineManagers + ", spawn=" + evidence.spawnPoints + ", referenceCamera=" + evidence.referenceCameras + ", missingScripts=" + evidence.missingScripts + ", dirty=" + evidence.sceneDirty + ", path=" + evidencePath);
    }

    static int CountRegisteredSources()
    {
        string json = System.IO.File.ReadAllText("config/gaussian-splats.json");
        int count = 0;
        int cursor = 0;
        while ((cursor = json.IndexOf("\"id\"", cursor, StringComparison.Ordinal)) >= 0) { count++; cursor += 4; }
        return count;
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

    static RegisteredMeasurement[] MeasureSplats(Scene scene)
    {
        Type splatType = FindType(GaussianSplatObjectTypeName);
        var result = new List<RegisteredMeasurement>();
        foreach (UnityEngine.Object value in Resources.FindObjectsOfTypeAll(splatType))
        {
            Component component = value as Component;
            if (component == null || EditorUtility.IsPersistent(component) || component.gameObject.scene != scene || !component.gameObject.activeInHierarchy) continue;
            object[] args = new object[] { new Bounds() };
            bool valid = (bool)splatType.GetMethod("TryGetLocalBounds").Invoke(component, args);
            if (!valid) throw new InvalidOperationException("Gaussian bounds are unavailable: " + component.name);
            Bounds local = (Bounds)args[0];
            Bounds world = TransformBounds(component.transform, local);
            result.Add(new RegisteredMeasurement
            {
                id = component.GetComponent(splatType).name,
                prefabPath = PrefabUtility.GetCorrespondingObjectFromSource(component.gameObject) == null ? "" : AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(component.gameObject)),
                localMin = local.min,
                localMax = local.max,
                worldMin = world.min,
                worldMax = world.max,
                position = component.transform.position,
                rotation = component.transform.eulerAngles,
                scale = component.transform.lossyScale,
                extent = Mathf.Max(world.size.x, Mathf.Max(world.size.y, world.size.z)),
                floorBottom = world.min.y
            });
        }
        return result.ToArray();
    }

    static Bounds TransformBounds(Transform transform, Bounds bounds)
    {
        Vector3 center = transform.TransformPoint(bounds.center);
        Vector3 extents = bounds.extents;
        var result = new Bounds(center, Vector3.zero);
        for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                    result.Encapsulate(transform.TransformPoint(bounds.center + Vector3.Scale(extents, new Vector3(x, y, z))));
        return result;
    }

    [MenuItem("VRMine/Verify Gaussian Splat Exhibition")]
    public static void Verify()
    {
        if (!System.IO.File.Exists(ScenePath))
            throw new InvalidOperationException("Canonical Gaussian exhibition scene does not exist. Run the builder after all upstream inputs are ready.");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var errors = new List<string>();

        if (CountSceneComponents<VRCSceneDescriptor>(scene) != 1) errors.Add("VRCSceneDescriptor count must be exactly 1");
        if (CountSceneComponents<PipelineManager>(scene) != 1) errors.Add("PipelineManager count must be exactly 1");
        if (CountSceneComponents<GaussianVideoPlaylist>(scene) != 1) errors.Add("GaussianVideoPlaylist count must be exactly 1");
        if (CountSceneComponents<GaussianVideoPlaylistAction>(scene) != 23) errors.Add("playlist action count must be 23 (20 direct + prev/replay/next)");

        GameObject floor = GameObject.Find("WalkableFloor");
        if (floor == null || floor.scene != scene || floor.GetComponent<Collider>() == null) errors.Add("WalkableFloor collider is missing");

        GameObject video = GameObject.Find("SourceVideoPlayer");
        if (video == null || video.scene != scene) errors.Add("SourceVideoPlayer is missing");
        else
        {
            int activeUrlInputs = 0;
            foreach (VRCUrlInputField input in video.GetComponentsInChildren<VRCUrlInputField>(true))
                if (input.gameObject.activeInHierarchy) activeUrlInputs++;
            if (activeUrlInputs != 0) errors.Add("free-form SDK URL input must be disabled so playlist throttle cannot be bypassed");
        }

        Type splatType = FindType(GaussianSplatObjectTypeName);
        Type rendererType = FindType(GaussianSplatRendererTypeName);
        if (splatType == null || rendererType == null)
        {
            errors.Add("pinned VRChatGaussianSplatting runtime types are missing");
        }
        else
        {
            if (CountSceneComponents(splatType, scene) != 20) errors.Add("active GaussianSplatObject count must be exactly 20");
            if (CountSceneComponents(rendererType, scene) != 1) errors.Add("active GaussianSplatRenderer count must be exactly 1");
        }

        int exhibitRoots = 0;
        int missingScripts = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform transform in transforms)
            {
                if (transform.name.StartsWith("Exhibit_", StringComparison.Ordinal)) exhibitRoots++;
                missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
            }
        }
        if (exhibitRoots != 20) errors.Add("exhibit GameObject count must be exactly 20");
        if (missingScripts != 0) errors.Add("missing scripts found: " + missingScripts);

        LightingSettings lightingSettings;
        if (!Lightmapping.TryGetLightingSettings(out lightingSettings) || lightingSettings == null)
        {
            errors.Add("LightingSettings are missing");
        }
        else
        {
            if (!lightingSettings.bakedGI) errors.Add("Baked GI must be enabled");
            if (lightingSettings.realtimeGI) errors.Add("Realtime GI must be disabled");
        }
        if (Lightmapping.lightingDataAsset == null) errors.Add("Lighting Data Asset is missing; run the canonical bake pipeline");
        if (LightmapSettings.lightmaps == null || LightmapSettings.lightmaps.Length == 0) errors.Add("no baked lightmaps are assigned to the scene");

        if (!HasOnlyCanonicalBuildScene()) errors.Add("EditorBuildSettings must contain exactly one enabled canonical scene");

        if (errors.Count > 0)
            throw new InvalidOperationException("Gaussian exhibition verification failed:\n- " + string.Join("\n- ", errors));

        Debug.Log("Gaussian exhibition verification PASS: descriptor=1, exhibits=20, splats=20, renderer=1, video=1, playlist=20, lightingData=present, lightmaps>0, missingScripts=0, buildScenes=1, canonicalBuildSceneOnly=true");
    }

    public static void VerifyBatch()
    {
        Verify();
    }

    public static void VerifySdkWorldBuilderBatch()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        VRCSdkControlPanel panel = EditorWindow.GetWindow<VRCSdkControlPanel>();
        var builder = new VRCSdkControlPanelWorldBuilder();
        builder.RegisterBuilder(panel);
        builder.Initialize();
        if (!builder.IsValidBuilder(out string message)) throw new InvalidOperationException(message);
        builder.CreateValidationsGUI(new VisualElement());
        Debug.Log("Gaussian SDK world builder validation completed without exception: scene=" + ScenePath + ", sdk=3.9.0");
    }

    public static void VerifyPerformanceBatch()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        string sourceDirectory = "Library/VRMine/GaussianSources";
        string prefabDirectory = "Assets/KafkaMade/VRMine/GaussianSplatting/Prefabs";
        string[] sourceFiles = System.IO.Directory.GetFiles(sourceDirectory, "*.ply", System.IO.SearchOption.TopDirectoryOnly);
        string[] importedFiles = System.IO.Directory.GetFiles(prefabDirectory, "*", System.IO.SearchOption.AllDirectories);
        var evidence = new PerformanceEvidence
        {
            activeScene = scene.path,
            registered = CountRegisteredSources(),
            exhibits = CountNamed(scene, "Exhibit_"),
            renderers = CountSceneComponents(FindType(GaussianSplatRendererTypeName), scene),
            videoPlayers = GameObject.Find("SourceVideoPlayer") == null ? 0 : 1,
            sourcePlyFiles = sourceFiles.Length,
            sourcePlyBytes = SumFiles(sourceFiles),
            importedAssetFiles = CountNonMetaFiles(importedFiles),
            importedAssetBytes = SumNonMetaFiles(importedFiles),
            sceneBytes = new System.IO.FileInfo(ScenePath).Length,
            status = CountRegisteredSources() == 20 ? "MEASURED_FINAL_COUNT" : "BLOCKED_FINAL_COUNT"
        };
        string evidencePath = "Library/VRMine/gaussian-performance-evidence.json";
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(evidencePath));
        System.IO.File.WriteAllText(evidencePath, JsonUtility.ToJson(evidence, true));
        Debug.Log("Gaussian performance evidence: registered=" + evidence.registered + ", exhibits=" + evidence.exhibits + ", renderers=" + evidence.renderers + ", videoPlayers=" + evidence.videoPlayers + ", sourcePlyBytes=" + evidence.sourcePlyBytes + ", importedAssetBytes=" + evidence.importedAssetBytes + ", sceneBytes=" + evidence.sceneBytes + ", status=" + evidence.status + ", path=" + evidencePath);
    }

    static int CountSceneComponents<T>(Scene scene) where T : Component
    {
        int count = 0;
        foreach (T component in Resources.FindObjectsOfTypeAll<T>())
            if (component != null && !EditorUtility.IsPersistent(component) && component.gameObject.scene == scene && component.gameObject.activeInHierarchy) count++;
        return count;
    }

    static long SumFiles(string[] paths)
    {
        long total = 0;
        foreach (string path in paths) total += new System.IO.FileInfo(path).Length;
        return total;
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
        foreach (string path in paths) if (!path.EndsWith(".meta", StringComparison.Ordinal)) total += new System.IO.FileInfo(path).Length;
        return total;
    }

    static int CountEnabledBuildScenes()
    {
        int count = 0;
        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
            if (buildScene.enabled) count++;
        return count;
    }

    static bool HasOnlyCanonicalBuildScene()
    {
        return CountEnabledBuildScenes() == 1 && EditorBuildSettings.scenes.Length == 1 && EditorBuildSettings.scenes[0].enabled && EditorBuildSettings.scenes[0].path == ScenePath;
    }

    static int CountSceneComponents(Type componentType, Scene scene)
    {
        int count = 0;
        foreach (UnityEngine.Object value in Resources.FindObjectsOfTypeAll(componentType))
        {
            Component component = value as Component;
            if (component != null && !EditorUtility.IsPersistent(component) && component.gameObject.scene == scene && component.gameObject.activeInHierarchy) count++;
        }
        return count;
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
