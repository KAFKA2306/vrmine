using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

public static class GaussianExhibitionBuilder
{
    const string ConfigPath = "config/gaussian-exhibition.json";
    const string GaussianSplatObjectTypeName = "GaussianSplatting.GaussianSplatObject";
    const string GaussianSplatRendererTypeName = "GaussianSplatting.GaussianSplatRenderer";

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
    [Serializable] sealed class PlaylistConfig
    {
        public int schema_version;
        public int expected_entries;
        public string source_registry;
        public string exhibition_manifest;
        public string player_prefab_path;
        public float rate_limit_seconds;
        public string status;
        public PlaylistEntry[] entries;
    }
    [Serializable] sealed class PlaylistEntry
    {
        public int display_index;
        public string source_id;
        public string playback_url;
        public bool requires_untrusted_urls;
        public string status;
    }

    [MenuItem("VRMine/Build Gaussian Splat Exhibition")]
    public static void Build()
    {
        ExhibitionConfig config = LoadJson<ExhibitionConfig>(ConfigPath, "Gaussian exhibition config");
        ValidateConfig(config);
        PlaylistConfig playlist = LoadJson<PlaylistConfig>(config.video_player.playlist_manifest, "Gaussian source-video playlist");
        ValidatePlaylist(config, playlist);

        EnsureProgramAsset<GaussianVideoPlaylist>();
        EnsureProgramAsset<GaussianVideoPlaylistAction>();
        UdonSharpProgramAsset.UdonSharpCheckAbsent();
        UdonSharpProgramAsset.CompileAllCsPrograms(true);

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
        CreateFloorAndShell(config.floor);
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
            instance.SetActive(true);
            CreatePad(exhibit);
            CreateLabel(exhibit);
        }

        EnsureGaussianRuntimeTopology(scene, config.expected_exhibits);
        CreateVideoPlayer(config, playlist, videoPrefab);

        EnsureAssetFolder(Path.GetDirectoryName(config.scene_path)?.Replace('\\', '/'));
        EditorSceneManager.SaveScene(scene, config.scene_path);
        EnsureBuildScene(config.scene_path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Gaussian exhibition scene created: " + config.scene_path);
    }

    static T LoadJson<T>(string path, string label) where T : class
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) throw new FileNotFoundException(label + " is missing.", path);
        T value = JsonUtility.FromJson<T>(File.ReadAllText(path));
        if (value == null) throw new InvalidDataException(label + " could not be parsed.");
        return value;
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

    static void ValidatePlaylist(ExhibitionConfig exhibition, PlaylistConfig playlist)
    {
        if (playlist.schema_version != 1 || playlist.expected_entries != 20 || playlist.entries == null || playlist.entries.Length != 20)
            throw new InvalidDataException("Source-video playlist must contain exactly 20 entries.");
        if (playlist.status != "ready") throw new InvalidOperationException("Source-video playlist is still blocked upstream.");
        if (playlist.player_prefab_path != exhibition.video_player.prefab_path)
            throw new InvalidDataException("Playlist and exhibition disagree on the canonical video-player prefab.");
        if (playlist.rate_limit_seconds < 5f)
            throw new InvalidDataException("Video URL rate limit must be at least 5 seconds.");

        for (int i = 0; i < playlist.entries.Length; i++)
        {
            PlaylistEntry entry = playlist.entries[i];
            ExhibitConfig exhibit = exhibition.exhibits[i];
            if (entry.display_index != i + 1 || exhibit.display_index != entry.display_index || entry.source_id != exhibit.source_id)
                throw new InvalidDataException("Playlist/exhibition mismatch at display slot " + (i + 1));
            if (string.IsNullOrEmpty(entry.source_id) || string.IsNullOrEmpty(entry.playback_url) || !entry.playback_url.StartsWith("https://", StringComparison.Ordinal))
                throw new InvalidDataException("Playlist entry is not playback-ready: " + (i + 1));
            if (entry.status != "ready_allowlisted" && entry.status != "ready_untrusted")
                throw new InvalidDataException("Playlist entry has an invalid ready status: " + entry.source_id);
            if ((entry.status == "ready_untrusted") != entry.requires_untrusted_urls)
                throw new InvalidDataException("Playlist URL trust state mismatch: " + entry.source_id);
        }
    }

    static void ValidateVector(float[] values, string name)
    {
        if (values == null || values.Length != 3) throw new InvalidDataException(name + " must contain exactly three values.");
        foreach (float value in values)
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException(name + " contains a non-finite value.");
    }

    static void CreateFloorAndShell(FloorConfig config)
    {
        Vector3 center = V3(config.position);
        Vector3 size = V3(config.scale);
        CreatePrimitive("WalkableFloor", center, size, true);

        float wallHeight = 3f;
        float wallThickness = 0.2f;
        float wallY = center.y + wallHeight * 0.5f;
        CreatePrimitive("Wall_Left", new Vector3(center.x - size.x * 0.5f, wallY, center.z), new Vector3(wallThickness, wallHeight, size.z), true);
        CreatePrimitive("Wall_Right", new Vector3(center.x + size.x * 0.5f, wallY, center.z), new Vector3(wallThickness, wallHeight, size.z), true);
        CreatePrimitive("Wall_Back", new Vector3(center.x, wallY, center.z - size.z * 0.5f), new Vector3(size.x, wallHeight, wallThickness), true);
        CreatePrimitive("Wall_Front", new Vector3(center.x, wallY, center.z + size.z * 0.5f), new Vector3(size.x, wallHeight, wallThickness), true);
    }

    static GameObject CreatePrimitive(string name, Vector3 position, Vector3 scale, bool isStatic)
    {
        GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gameObject.name = name;
        gameObject.transform.position = position;
        gameObject.transform.localScale = scale;
        gameObject.isStatic = isStatic;
        return gameObject;
    }

    static void CreatePad(ExhibitConfig exhibit)
    {
        Vector3 p = V3(exhibit.position);
        CreatePrimitive(
            "ExhibitPad_" + exhibit.display_index.ToString("00"),
            new Vector3(p.x, 0.05f, p.z),
            new Vector3(1.4f, 0.1f, 1.4f),
            true);
    }

    static void CreateLabel(ExhibitConfig exhibit)
    {
        Type tmpType = FindType("TMPro.TextMeshPro");
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
        spawn.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        descriptor.spawns = new[] { spawn.transform };

        SerializedObject serializedDescriptor = new SerializedObject(descriptor);
        SerializedProperty spawnRadius = serializedDescriptor.FindProperty("SpawnRadius");
        if (spawnRadius != null) spawnRadius.floatValue = 0f;
        SerializedProperty respawnHeight = serializedDescriptor.FindProperty("RespawnHeightY");
        if (respawnHeight != null) respawnHeight.floatValue = -5f;
        serializedDescriptor.ApplyModifiedPropertiesWithoutUndo();

        GameObject cameraObject = new GameObject("ReferenceCamera");
        cameraObject.transform.SetPositionAndRotation(V3(cameraConfig.position), Quaternion.Euler(V3(cameraConfig.rotation_euler_degrees)));
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.fieldOfView = cameraConfig.field_of_view;
        descriptor.ReferenceCamera = cameraObject;
    }

    static void EnsureGaussianRuntimeTopology(Scene scene, int expectedExhibits)
    {
        Type splatType = FindType(GaussianSplatObjectTypeName);
        Type rendererType = FindType(GaussianSplatRendererTypeName);
        if (splatType == null || rendererType == null)
            throw new InvalidOperationException("Pinned VRChatGaussianSplatting renderer is not materialized. Resolve #89 before building the scene.");

        int splatCount = CountActiveSceneComponents(splatType, scene);
        if (splatCount != expectedExhibits)
            throw new InvalidOperationException("Expected exactly " + expectedExhibits + " active GaussianSplatObject components, found " + splatCount + ".");

        MethodInfo ensureRenderer = rendererType.GetMethod(
            "EnsureSceneRendererExists",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { typeof(Scene) },
            null);
        if (ensureRenderer == null)
            throw new MissingMethodException(GaussianSplatRendererTypeName, "EnsureSceneRendererExists(Scene)");
        ensureRenderer.Invoke(null, new object[] { scene });

        int rendererCount = CountActiveSceneComponents(rendererType, scene);
        if (rendererCount != 1)
            throw new InvalidOperationException("Gaussian exhibition requires exactly one active GaussianSplatRenderer, found " + rendererCount + ".");
    }

    static int CountActiveSceneComponents(Type componentType, Scene scene)
    {
        int count = 0;
        UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll(componentType);
        foreach (UnityEngine.Object value in objects)
        {
            Component component = value as Component;
            if (component == null || EditorUtility.IsPersistent(component) || component.gameObject.scene != scene || !component.gameObject.activeInHierarchy)
                continue;
            count++;
        }
        return count;
    }

    static void CreateVideoPlayer(ExhibitionConfig config, PlaylistConfig playlist, GameObject videoPrefab)
    {
        GameObject videoPlayer = (GameObject)PrefabUtility.InstantiatePrefab(videoPrefab);
        videoPlayer.name = "SourceVideoPlayer";
        videoPlayer.transform.SetPositionAndRotation(
            V3(config.video_player.position),
            Quaternion.Euler(V3(config.video_player.rotation_euler_degrees)));

        UdonBehaviour syncPlayer = FindSyncPlayer(videoPlayer);
        foreach (VRCUrlInputField input in videoPlayer.GetComponentsInChildren<VRCUrlInputField>(true))
            input.gameObject.SetActive(false);

        GameObject controlsRoot = new GameObject("Canonical20VideoPlaylist");
        controlsRoot.transform.SetPositionAndRotation(videoPlayer.transform.position, videoPlayer.transform.rotation);

        GaussianVideoPlaylist controller = AddUdon<GaussianVideoPlaylist>(new GameObject("GaussianVideoPlaylist"));
        controller.transform.SetParent(controlsRoot.transform, false);
        controller.syncPlayer = syncPlayer;
        controller.rateLimitSeconds = Mathf.Max(5f, playlist.rate_limit_seconds);
        controller.urls = new VRCUrl[playlist.entries.Length];
        controller.titles = new string[playlist.entries.Length];
        controller.requiresUntrustedUrls = new bool[playlist.entries.Length];

        for (int i = 0; i < playlist.entries.Length; i++)
        {
            PlaylistEntry entry = playlist.entries[i];
            controller.urls[i] = new VRCUrl(entry.playback_url);
            controller.titles[i] = config.exhibits[i].label;
            controller.requiresUntrustedUrls[i] = entry.requires_untrusted_urls;
        }

        controller.statusText = CreateWorldText(
            controlsRoot.transform,
            "VideoStatus",
            new Vector3(0f, 1.25f, 0.45f),
            new Vector2(900f, 120f),
            34,
            "Source video playlist");

        for (int i = 0; i < playlist.entries.Length; i++)
        {
            int column = i % 5;
            int row = i / 5;
            float x = -1.2f + column * 0.6f;
            float y = 0.75f - row * 0.42f;
            CreatePlaylistAction(
                controlsRoot.transform,
                "SelectVideo_" + (i + 1).ToString("00"),
                new Vector3(x, y, 0.45f),
                new Vector3(0.5f, 0.12f, 0.18f),
                controller,
                0,
                i,
                (i + 1).ToString("00"));
        }

        CreatePlaylistAction(controlsRoot.transform, "PreviousVideo", new Vector3(-0.75f, -1.15f, 0.45f), new Vector3(0.65f, 0.12f, 0.18f), controller, 1, 0, "PREV");
        CreatePlaylistAction(controlsRoot.transform, "ReplayVideo", new Vector3(0f, -1.15f, 0.45f), new Vector3(0.65f, 0.12f, 0.18f), controller, 3, 0, "REPLAY");
        CreatePlaylistAction(controlsRoot.transform, "NextVideo", new Vector3(0.75f, -1.15f, 0.45f), new Vector3(0.65f, 0.12f, 0.18f), controller, 2, 0, "NEXT");

        EditorUtility.SetDirty(controller);
    }

    static UdonBehaviour FindSyncPlayer(GameObject videoPlayer)
    {
        UdonBehaviour[] behaviours = videoPlayer.GetComponentsInChildren<UdonBehaviour>(true);
        foreach (UdonBehaviour behaviour in behaviours)
        {
            if (behaviour.programSource != null && behaviour.programSource.name.IndexOf("UdonSyncPlayer", StringComparison.OrdinalIgnoreCase) >= 0)
                return behaviour;
        }
        throw new InvalidOperationException("Canonical SDK video prefab does not contain the expected UdonSyncPlayer behaviour.");
    }

    static void CreatePlaylistAction(Transform parent, string name, Vector3 localPosition, Vector3 localScale, GaussianVideoPlaylist controller, int action, int index, string text)
    {
        GameObject button = GameObject.CreatePrimitive(PrimitiveType.Cube);
        button.name = name;
        button.transform.SetParent(parent, false);
        button.transform.localPosition = localPosition;
        button.transform.localScale = localScale;

        GaussianVideoPlaylistAction behaviour = AddUdon<GaussianVideoPlaylistAction>(button);
        behaviour.playlist = controller;
        behaviour.action = action;
        behaviour.index = index;
        EditorUtility.SetDirty(behaviour);

        CreateWorldText(button.transform, name + "Label", new Vector3(0f, 0f, -0.6f), new Vector2(220f, 80f), 30, text);
    }

    static Text CreateWorldText(Transform parent, string name, Vector3 localPosition, Vector2 size, int fontSize, string value)
    {
        GameObject canvasObject = new GameObject(name + "Canvas");
        canvasObject.transform.SetParent(parent, false);
        canvasObject.transform.localPosition = localPosition;
        canvasObject.transform.localRotation = Quaternion.identity;
        canvasObject.transform.localScale = Vector3.one * 0.002f;
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = size;

        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(canvasObject.transform, false);
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = value;
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return text;
    }

    static T AddUdon<T>(GameObject gameObject) where T : UdonSharpBehaviour
    {
        return (T)(Component)gameObject.AddUdonSharpComponent(typeof(T));
    }

    static void EnsureProgramAsset<T>() where T : UdonSharpBehaviour
    {
        string[] scripts = AssetDatabase.FindAssets(typeof(T).Name + " t:MonoScript");
        MonoScript script = null;
        for (int i = 0; i < scripts.Length; i++)
        {
            MonoScript candidate = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(scripts[i]));
            if (candidate != null && candidate.GetClass() == typeof(T)) script = candidate;
        }
        if (script == null) throw new InvalidOperationException("UdonSharp script is missing: " + typeof(T).Name);
        string path = Path.ChangeExtension(AssetDatabase.GetAssetPath(script), ".asset");
        if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path) != null) return;
        UdonSharpProgramAsset program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
        program.sourceCsScript = script;
        AssetDatabase.CreateAsset(program, path);
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
