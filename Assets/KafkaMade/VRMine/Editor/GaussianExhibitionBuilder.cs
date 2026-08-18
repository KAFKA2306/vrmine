using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRC.SDK3.Components;

public static class GaussianExhibitionBuilder
{
    const string ConfigPath = "config/gaussian-exhibition.json";

    [Serializable]
    sealed class ExhibitionConfig
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

    [Serializable]
    sealed class FloorConfig
    {
        public float[] position;
        public float[] scale;
    }

    [Serializable]
    sealed class SpawnConfig
    {
        public float[] position;
    }

    [Serializable]
    sealed class CameraConfig
    {
        public float[] position;
        public float[] rotation_euler_degrees;
        public float field_of_view;
    }

    [Serializable]
    sealed class VideoPlayerConfig
    {
        public float[] position;
        public string status;
        public string prefab_path;
        public string playlist_manifest;
    }

    [Serializable]
    sealed class ExhibitConfig
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

    [MenuItem("VRMine/Build Gaussian Exhibition")]
    public static void Build()
    {
        ExhibitionConfig config = LoadConfig();
        ValidateConfig(config);

        var prefabs = new Dictionary<int, GameObject>();
        var missingPrefabs = new List<string>();
        for (int i = 0; i < config.exhibits.Length; i++)
        {
            ExhibitConfig exhibit = config.exhibits[i];
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(exhibit.prefab_path);
            if (prefab == null)
            {
                missingPrefabs.Add(exhibit.prefab_path);
                continue;
            }
            prefabs.Add(exhibit.display_index, prefab);
        }

        GameObject videoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(config.video_player.prefab_path);
        if (videoPrefab == null)
        {
            missingPrefabs.Add(config.video_player.prefab_path);
        }

        if (missingPrefabs.Count > 0)
        {
            throw new InvalidOperationException(
                "Gaussian exhibition assets are incomplete. Missing prefabs:\n- " +
                string.Join("\n- ", missingPrefabs));
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateFloor(config.floor);
        CreateDirectionalLight();
        CreateDescriptor(config.spawn, config.reference_camera);

        GameObject exhibitsRoot = new GameObject("GaussianExhibits");
        for (int i = 0; i < config.exhibits.Length; i++)
        {
            ExhibitConfig exhibit = config.exhibits[i];
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[exhibit.display_index]);
            instance.name = "Exhibit_" + exhibit.display_index.ToString("00") + "_" + exhibit.source_id;
            instance.transform.SetParent(exhibitsRoot.transform);
            instance.transform.SetPositionAndRotation(
                Vector3Value(exhibit.position),
                Quaternion.Euler(Vector3Value(exhibit.rotation_euler_degrees)));
            instance.transform.localScale = Vector3Value(exhibit.scale);

            CreatePad(exhibit);
            CreateLabel(exhibit);
        }

        GameObject videoPlayer = (GameObject)PrefabUtility.InstantiatePrefab(videoPrefab);
        videoPlayer.name = "SourceVideoPlayer";
        videoPlayer.transform.position = Vector3Value(config.video_player.position);

        string directory = Path.GetDirectoryName(config.scene_path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        EditorSceneManager.SaveScene(scene, config.scene_path);
        EnsureBuildScene(config.scene_path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "Gaussian exhibition scene created: " + config.scene_path +
            " (20 exhibits, target extent " + config.target_extent_m + " m)");
    }

    static ExhibitionConfig LoadConfig()
    {
        if (!File.Exists(ConfigPath))
        {
            throw new FileNotFoundException("Gaussian exhibition config is missing.", ConfigPath);
        }

        ExhibitionConfig config = JsonUtility.FromJson<ExhibitionConfig>(File.ReadAllText(ConfigPath));
        if (config == null)
        {
            throw new InvalidDataException("Gaussian exhibition config could not be parsed.");
        }
        return config;
    }

    static void ValidateConfig(ExhibitionConfig config)
    {
        if (config.schema_version != 1)
        {
            throw new InvalidDataException("Unsupported Gaussian exhibition schema: " + config.schema_version);
        }
        if (config.expected_exhibits != 20 || config.exhibits == null || config.exhibits.Length != 20)
        {
            throw new InvalidDataException("Gaussian exhibition must contain exactly 20 exhibit slots.");
        }
        if (!string.Equals(config.canonical_platform, "windows", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Canonical first target must be Windows.");
        }
        if (Mathf.Abs(config.target_extent_m - 1f) > 0.001f)
        {
            throw new InvalidDataException("Gaussian exhibits must target an approximately 1 m normalized extent.");
        }

        var blocked = new List<string>();
        var indexes = new HashSet<int>();
        var sourceIds = new HashSet<string>();
        for (int i = 0; i < config.exhibits.Length; i++)
        {
            ExhibitConfig exhibit = config.exhibits[i];
            if (!indexes.Add(exhibit.display_index))
            {
                throw new InvalidDataException("Duplicate exhibit display index: " + exhibit.display_index);
            }
            if (!string.Equals(exhibit.status, "source_registered", StringComparison.Ordinal) ||
                string.IsNullOrEmpty(exhibit.source_id) ||
                string.IsNullOrEmpty(exhibit.prefab_path))
            {
                blocked.Add(exhibit.display_index.ToString("00"));
                continue;
            }
            if (!sourceIds.Add(exhibit.source_id))
            {
                throw new InvalidDataException("Duplicate exhibit source id: " + exhibit.source_id);
            }
            ValidateVector(exhibit.position, "exhibit position");
            ValidateVector(exhibit.rotation_euler_degrees, "exhibit rotation");
            ValidateVector(exhibit.scale, "exhibit scale");
        }

        if (blocked.Count > 0)
        {
            throw new InvalidOperationException(
                "Gaussian exhibition source inputs are incomplete. Blocked slots: " +
                string.Join(", ", blocked));
        }

        if (config.video_player == null ||
            !string.Equals(config.video_player.status, "ready", StringComparison.Ordinal) ||
            string.IsNullOrEmpty(config.video_player.prefab_path) ||
            string.IsNullOrEmpty(config.video_player.playlist_manifest))
        {
            throw new InvalidOperationException(
                "Gaussian exhibition video player is not ready. Complete the 20-entry playlist before building the upload scene.");
        }

        ValidateVector(config.floor.position, "floor position");
        ValidateVector(config.floor.scale, "floor scale");
        ValidateVector(config.spawn.position, "spawn position");
        ValidateVector(config.reference_camera.position, "reference camera position");
        ValidateVector(config.reference_camera.rotation_euler_degrees, "reference camera rotation");
        ValidateVector(config.video_player.position, "video player position");
    }

    static void ValidateVector(float[] values, string name)
    {
        if (values == null || values.Length != 3)
        {
            throw new InvalidDataException(name + " must contain exactly three values.");
        }
        for (int i = 0; i < values.Length; i++)
        {
            if (float.IsNaN(values[i]) || float.IsInfinity(values[i]))
            {
                throw new InvalidDataException(name + " contains a non-finite value.");
            }
        }
    }

    static void CreateFloor(FloorConfig config)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "WalkableFloor";
        floor.transform.position = Vector3Value(config.position);
        floor.transform.localScale = Vector3Value(config.scale);
    }

    static void CreatePad(ExhibitConfig exhibit)
    {
        GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = "ExhibitPad_" + exhibit.display_index.ToString("00");
        Vector3 position = Vector3Value(exhibit.position);
        pad.transform.position = new Vector3(position.x, 0.05f, position.z);
        pad.transform.localScale = new Vector3(1.4f, 0.1f, 1.4f);
    }

    static void CreateLabel(ExhibitConfig exhibit)
    {
        GameObject canvasObject = new GameObject("ExhibitLabel_" + exhibit.display_index.ToString("00"));
        Vector3 position = Vector3Value(exhibit.position);
        canvasObject.transform.position = position + new Vector3(0f, 1.2f, 0.75f);
        canvasObject.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        canvasObject.transform.localScale = Vector3.one * 0.002f;

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(600f, 180f);

        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(canvasObject.transform, false);
        Text text = textObject.AddComponent<Text>();
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 52;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = exhibit.display_index.ToString("00") + "  " + exhibit.label;
    }

    static void CreateDirectionalLight()
    {
        GameObject lightObject = new GameObject("Directional Light");
        lightObject.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.0f;
    }

    static void CreateDescriptor(SpawnConfig spawnConfig, CameraConfig cameraConfig)
    {
        GameObject descriptorObject = new GameObject("VRCSceneDescriptor");
        VRCSceneDescriptor descriptor = descriptorObject.AddComponent<VRCSceneDescriptor>();

        GameObject spawn = new GameObject("SpawnPoint");
        spawn.transform.position = Vector3Value(spawnConfig.position);
        descriptor.spawns = new[] { spawn.transform };

        GameObject cameraObject = new GameObject("ReferenceCamera");
        cameraObject.transform.SetPositionAndRotation(
            Vector3Value(cameraConfig.position),
            Quaternion.Euler(Vector3Value(cameraConfig.rotation_euler_degrees)));
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.fieldOfView = cameraConfig.field_of_view;
        descriptor.ReferenceCamera = cameraObject;
    }

    static void EnsureBuildScene(string scenePath)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool found = false;
        for (int i = 0; i < scenes.Count; i++)
        {
            if (!string.Equals(scenes[i].path, scenePath, StringComparison.Ordinal))
            {
                continue;
            }
            scenes[i] = new EditorBuildSettingsScene(scenePath, true);
            found = true;
            break;
        }

        if (!found)
        {
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    static Vector3 Vector3Value(float[] values)
    {
        return new Vector3(values[0], values[1], values[2]);
    }
}
