using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Components;

public static class GaussianExhibitionBuilder
{
    const string ConfigPath = "config/gaussian-exhibition.json";

    [Serializable] sealed class ExhibitionConfig
    {
        public int schema_version;
        public string scene_path;
        public int expected_exhibits;
        public string canonical_platform;
        public string source_registry;
        public string renderer;
        public float target_extent_m;
        public FloorConfig floor;
        public SpawnConfig spawn;
        public CameraConfig reference_camera;
        public VideoPlayerConfig video_player;
        public ExhibitConfig[] exhibits;
    }

    [Serializable] sealed class FloorConfig { public float[] position; public float[] scale; }
    [Serializable] sealed class SpawnConfig { public float[] position; }
    [Serializable] sealed class CameraConfig
    {
        public float[] position;
        public float[] rotation_euler_degrees;
        public float field_of_view;
    }
    [Serializable] sealed class VideoPlayerConfig
    {
        public float[] position;
        public float[] rotation_euler_degrees;
        public string status;
        public string prefab_path;
        public string playlist_manifest;
    }
    [Serializable] sealed class ExhibitConfig
    {
        public int display_index;
        public string source_id;
        public string prefab_path;
        public string label;
        public string status;
        public float[] position;
        public float[] rotation_euler_degrees;
        public float[] scale;
    }

    [MenuItem("VRMine/Build Gaussian Splat Exhibition")]
    public static void Build()
    {
        ExhibitionConfig config = LoadConfig();
        ValidateConfig(config);

        var prefabs = new Dictionary<int, GameObject>();
        var missing = new List<string>();
        foreach (ExhibitConfig exhibit in config.exhibits)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(exhibit.prefab_path);
            if (prefab == null) missing.Add(exhibit.prefab_path);
            else prefabs.Add(exhibit.display_index, prefab);
        }

        GameObject videoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(config.video_player.prefab_path);
        if (videoPrefab == null) missing.Add(config.video_player.prefab_path);
        if (missing.Count > 0)
            throw new InvalidOperationException("Gaussian exhibition assets are incomplete:\n- " + string.Join("\n- ", missing));

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateFloor(config.floor);
        CreateBakedDirectionalLight();
        CreateLightProbes();
        CreateDescriptor(config.spawn, config.reference_camera);

        GameObject root = new GameObject("GaussianExhibits");
        foreach (ExhibitConfig exhibit in config.exhibits)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[exhibit.display_index]);
            instance.name = "Exhibit_" + exhibit.display_index.ToString("00") + "_" + exhibit.source_id;
            instance.transform.SetParent(root.transform);
            instance.transform.SetPositionAndRotation(V3(exhibit.position), Quaternion.Euler(V3(exhibit.rotation_euler_degrees)));
            instance.transform.localScale = V3(exhibit.scale);
            CreatePad(exhibit);
            CreateLabel(exhibit);
        }

        GameObject videoPlayer = (GameObject)PrefabUtility.InstantiatePrefab(videoPrefab);
        videoPlayer.name = "SourceVideoPlayer";
        videoPlayer.transform.SetPositionAndRotation(
            V3(config.video_player.position),
            Quaternion.Euler(V3(config.video_player.rotation_euler_degrees)));

        EnsureAssetFolder(Path.GetDirectoryName(config.scene_path)?.Replace('\\', '/'));
        EditorSceneManager.SaveScene(scene, config.scene_path);
        EnsureBuildScene(config.scene_path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Gaussian exhibition scene created: " + config.scene_path);
    }

    static ExhibitionConfig LoadConfig()
    {
        if (!File.Exists(ConfigPath)) throw new FileNotFoundException("Gaussian exhibition config is missing.", ConfigPath);
        ExhibitionConfig config = JsonUtility.FromJson<ExhibitionConfig>(File.ReadAllText(ConfigPath));
        if (config == null) throw new InvalidDataException("Gaussian exhibition config could not be parsed.");
        return config;
    }

    static void ValidateConfig(ExhibitionConfig config)
    {
        if (config.schema_version != 1) throw new InvalidDataException("Unsupported Gaussian exhibition schema.");
        if (config.expected_exhibits != 20 || config.exhibits == null || config.exhibits.Length != 20)
            throw new InvalidDataException("Gaussian exhibition must contain exactly 20 exhibit slots.");
        if (config.scene_path != "Assets/KafkaMade/VRMine/Scenes/GaussianSplatExhibition.unity")
            throw new InvalidDataException("Unexpected canonical Gaussian exhibition scene path.");
        if (config.canonical_platform != "windows") throw new InvalidDataException("Canonical first target must be Windows.");
        if (Mathf.Abs(config.target_extent_m - 1f) > 0.001f)
            throw new InvalidDataException("Gaussian exhibits must target an approximately 1 m normalized extent.");

        var blocked = new List<string>();
        var indexes = new HashSet<int>();
        var sourceIds = new HashSet<string>();
        foreach (ExhibitConfig exhibit in config.exhibits)
        {
            if (!indexes.Add(exhibit.display_index))
                throw new InvalidDataException("Duplicate exhibit display index: " + exhibit.display_index);
            ValidateVector(exhibit.position, "exhibit position");
            ValidateVector(exhibit.rotation_euler_degrees, "exhibit rotation");
            ValidateVector(exhibit.scale, "exhibit scale");

            if (exhibit.status != "source_registered" || string.IsNullOrEmpty(exhibit.source_id) || string.IsNullOrEmpty(exhibit.prefab_path))
            {
                blocked.Add(exhibit.display_index.ToString("00"));
                continue;
            }
            if (!sourceIds.Add(exhibit.source_id))
                throw new InvalidDataException("Duplicate exhibit source id: " + exhibit.source_id);
        }

        if (blocked.Count > 0)
            throw new InvalidOperationException("Gaussian exhibition source inputs are incomplete. Blocked slots: " + string.Join(", ", blocked));

        if (config.video_player == null || config.video_player.status != "ready" ||
            string.IsNullOrEmpty(config.video_player.prefab_path) ||
            string.IsNullOrEmpty(config.video_player.playlist_manifest) ||
            !File.Exists(config.video_player.playlist_manifest))
            throw new InvalidOperationException("The real 20-entry video player/playlist is not ready.");

        ValidateVector(config.floor.position, "floor position");
        ValidateVector(config.floor.scale, "floor scale");
        ValidateVector(config.spawn.position, "spawn position");
        ValidateVector(config.reference_camera.position, "reference camera position");
        ValidateVector(config.reference_camera.rotation_euler_degrees, "reference camera rotation");
        ValidateVector(config.video_player.position, "video player position");
        ValidateVector(config.video_player.rotation_euler_degrees, "video player rotation");
    }

    static void ValidateVector(float[] values, string name)
    {
        if (values == null || values.Length != 3) throw new InvalidDataException(name + " must contain exactly three values.");
        foreach (float value in values)
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException(name + " contains a non-finite value.");
    }

    static void CreateFloor(FloorConfig config)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "WalkableFloor";
        floor.transform.position = V3(config.position);
        floor.transform.localScale = V3(config.scale);
        floor.isStatic = true;
    }

    static void CreatePad(ExhibitConfig exhibit)
    {
        GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = "ExhibitPad_" + exhibit.display_index.ToString("00");
        Vector3 p = V3(exhibit.position);
        pad.transform.position = new Vector3(p.x, 0.05f, p.z);
        pad.transform.localScale = new Vector3(1.4f, 0.1f, 1.4f);
        pad.isStatic = true;
    }

    static void CreateLabel(ExhibitConfig exhibit)
    {
        Type tmpType = Type.GetType("TMPro.TextMeshPro, Unity.TextMeshPro");
        if (tmpType == null) throw new InvalidOperationException("TextMesh Pro is required for Gaussian exhibition labels.");

        Quaternion rotation = Quaternion.Euler(V3(exhibit.rotation_euler_degrees));
        GameObject label = new GameObject("ExhibitLabel_" + exhibit.display_index.ToString("00"));
        label.transform.SetPositionAndRotation(
            V3(exhibit.position) + Vector3.up * 1.2f + rotation * Vector3.forward * 0.75f,
            rotation);
        label.transform.localScale = Vector3.one * 0.1f;

        Component text = label.AddComponent(tmpType);
        tmpType.GetProperty("text")?.SetValue(text, exhibit.display_index.ToString("00") + "  " + exhibit.label);
        tmpType.GetProperty("fontSize")?.SetValue(text, 4f);
    }

    static void CreateBakedDirectionalLight()
    {
        GameObject lightObject = new GameObject("Baked Directional Light");
        lightObject.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        light.lightmapBakeType = LightmapBakeType.Baked;
    }

    static void CreateLightProbes()
    {
        GameObject probes = new GameObject("Light Probes");
        LightProbeGroup group = probes.AddComponent<LightProbeGroup>();
        group.probePositions = new[]
        {
            new Vector3(-9f, 1.6f, 0f), new Vector3(-3f, 1.6f, 0f),
            new Vector3(3f, 1.6f, 0f), new Vector3(9f, 1.6f, 0f),
            new Vector3(0f, 1.6f, -3f), new Vector3(0f, 1.6f, 3f),
        };
    }

    static void CreateDescriptor(SpawnConfig spawnConfig, CameraConfig cameraConfig)
    {
        GameObject descriptorObject = new GameObject("VRCSceneDescriptor");
        VRCSceneDescriptor descriptor = descriptorObject.AddComponent<VRCSceneDescriptor>();

        GameObject spawn = new GameObject("SpawnPoint");
        spawn.transform.position = V3(spawnConfig.position);
        descriptor.spawns = new[] { spawn.transform };

        GameObject cameraObject = new GameObject("ReferenceCamera");
        cameraObject.transform.SetPositionAndRotation(V3(cameraConfig.position), Quaternion.Euler(V3(cameraConfig.rotation_euler_degrees)));
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.fieldOfView = cameraConfig.field_of_view;
        descriptor.ReferenceCamera = cameraObject;
    }

    static void EnsureAssetFolder(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath) || AssetDatabase.IsValidFolder(assetPath)) return;
        string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        EnsureAssetFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(assetPath));
    }

    static void EnsureBuildScene(string scenePath)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path != scenePath) continue;
            scenes[i] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = scenes.ToArray();
            return;
        }
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    static Vector3 V3(float[] values) => new Vector3(values[0], values[1], values[2]);
}
