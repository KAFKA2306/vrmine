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
using VRC.Core;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

public static class GaussianExhibitionBuilder
{
    const string ConfigPath = "config/gaussian-exhibition.json";
    const string GaussianSplatObjectTypeName = "GaussianSplatting.GaussianSplatObject";
    const string GaussianSplatRendererTypeName = "GaussianSplatting.GaussianSplatRenderer";
    const string PrefabDirectory = "Assets/KafkaMade/VRMine/GaussianSplatting/Prefabs";

    [Serializable] sealed class ExhibitionConfig
    {
        public int schema_version;
        public string scene_path;
        public string canonical_platform;
        public string source_registry;
        public string renderer;
        public float target_extent_m;
        public LayoutConfig layout;
        public CameraConfig reference_camera;
        public VideoPlayerConfig video_player;
    }

    [Serializable] sealed class LayoutConfig
    {
        public float center_spacing_m;
        public float aisle_width_m;
        public float margin_m;
        public float pad_size_m;
        public float wall_height_m;
    }

    [Serializable] sealed class CameraConfig { public float field_of_view; }
    [Serializable] sealed class VideoPlayerConfig { public string prefab_path; }
    [Serializable] sealed class Registry { public EnvironmentEntry[] environments; }

    [Serializable] sealed class EnvironmentEntry
    {
        public string id;
        public int display_index;
        public PlaybackEntry playback;
    }

    [Serializable] sealed class PlaybackEntry
    {
        public string url;
        public bool requires_untrusted_urls;
        public string status;
    }

    sealed class Exhibit
    {
        public int index;
        public string id;
        public string title;
        public string prefabPath;
        public Vector3 position;
        public Quaternion rotation;
    }

    [MenuItem("VRMine/Build Gaussian Splat Exhibition")]
    public static void Build() => BuildFromRegistry();

    static void BuildFromRegistry()
    {
        ExhibitionConfig config = LoadJson<ExhibitionConfig>(ConfigPath, "Gaussian exhibition config");
        ValidateConfig(config);
        Registry registry = LoadJson<Registry>(config.source_registry, "Gaussian source registry");
        ValidateRegistry(registry);

        List<Exhibit> exhibits = BuildExhibitLayout(config, registry);
        var prefabs = new Dictionary<string, GameObject>();
        var missing = new List<string>();
        foreach (Exhibit exhibit in exhibits)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(exhibit.prefabPath);
            if (prefab == null) missing.Add(exhibit.id + " -> " + exhibit.prefabPath);
            else prefabs.Add(exhibit.id, prefab);
        }
        if (missing.Count > 0)
            throw new InvalidOperationException("Gaussian exhibition prefabs are incomplete. Run the registered-source importer first:\n- " + string.Join("\n- ", missing));

        Vector2 floorSize = ComputeFloorSize(config, exhibits.Count);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateFloorAndShell(config, floorSize);
        CreateBakedDirectionalLight();
        CreateLightProbes(floorSize);
        CreateDescriptor(config, floorSize);

        GameObject root = new GameObject("GaussianExhibits");
        foreach (Exhibit exhibit in exhibits)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[exhibit.id]);
            instance.name = "Exhibit_" + exhibit.index.ToString("00") + "_" + exhibit.id;
            instance.transform.SetParent(root.transform);
            instance.transform.SetPositionAndRotation(exhibit.position, exhibit.rotation);
            AlignExhibitToFloor(instance);
            instance.SetActive(true);
            CreatePad(config, exhibit);
            CreateLabel(exhibit);
        }

        EnsureGaussianRuntimeTopology(scene, exhibits.Count);
        CreateVideoArea(config, registry, floorSize);

        EnsureAssetFolder(Path.GetDirectoryName(config.scene_path)?.Replace('\\', '/'));
        EditorSceneManager.SaveScene(scene, config.scene_path);
        EnsureBuildScene(config.scene_path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(config.scene_path);

        Debug.Log("Gaussian exhibition scene ready: registered=" + exhibits.Count + ", scene=" + config.scene_path);
    }

    static List<Exhibit> BuildExhibitLayout(ExhibitionConfig config, Registry registry)
    {
        int count = registry.environments.Length;
        int firstRowCount = (count + 1) / 2;
        int secondRowCount = count - firstRowCount;
        var result = new List<Exhibit>(count);
        for (int i = 0; i < count; i++)
        {
            EnvironmentEntry source = registry.environments[i];
            bool secondRow = i >= firstRowCount;
            int rowIndex = secondRow ? i - firstRowCount : i;
            int rowCount = secondRow ? secondRowCount : firstRowCount;
            float x = (rowIndex - (rowCount - 1) * 0.5f) * config.layout.center_spacing_m;
            float z = (config.layout.aisle_width_m * 0.5f + config.layout.pad_size_m * 0.5f) * (secondRow ? 1f : -1f);
            result.Add(new Exhibit
            {
                index = source.display_index,
                id = source.id,
                title = source.id,
                prefabPath = PrefabDirectory + "/" + source.id + ".prefab",
                position = new Vector3(x, config.target_extent_m * 0.5f, z),
                rotation = Quaternion.Euler(0f, secondRow ? 180f : 0f, 0f),
            });
        }
        return result;
    }

    static Vector2 ComputeFloorSize(ExhibitionConfig config, int count)
    {
        int maxRowCount = (count + 1) / 2;
        float width = Mathf.Max(6f, Mathf.Max(0, maxRowCount - 1) * config.layout.center_spacing_m + config.layout.pad_size_m + config.layout.margin_m * 2f);
        float depth = Mathf.Max(8f, config.layout.aisle_width_m + config.layout.pad_size_m * 2f + config.layout.margin_m * 2f);
        return new Vector2(width, depth);
    }

    static void AlignExhibitToFloor(GameObject instance)
    {
        Type splatType = FindType(GaussianSplatObjectTypeName);
        Component splat = instance.GetComponentInChildren(splatType, true);
        if (splat == null) throw new InvalidOperationException("GaussianSplatObject is missing: " + instance.name);
        object[] args = new object[] { new Bounds() };
        MethodInfo boundsMethod = splatType.GetMethod("TryGetLocalBounds");
        if (boundsMethod == null) throw new MissingMethodException(GaussianSplatObjectTypeName, "TryGetLocalBounds");
        bool valid = (bool)boundsMethod.Invoke(splat, args);
        if (!valid) throw new InvalidOperationException("Gaussian bounds are unavailable: " + instance.name);
        Bounds world = TransformBounds(splat.transform, (Bounds)args[0]);
        instance.transform.position += Vector3.up * -world.min.y;
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

    static T LoadJson<T>(string path, string label) where T : class
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) throw new FileNotFoundException(label + " is missing.", path);
        T value = JsonUtility.FromJson<T>(File.ReadAllText(path));
        if (value == null) throw new InvalidDataException(label + " could not be parsed.");
        return value;
    }

    static void ValidateConfig(ExhibitionConfig config)
    {
        if (config.schema_version != 3) throw new InvalidDataException("Unsupported Gaussian exhibition schema.");
        if (config.scene_path != "Assets/KafkaMade/VRMine/Scenes/GaussianSplatExhibition.unity")
            throw new InvalidDataException("Unexpected canonical Gaussian exhibition scene path.");
        if (config.canonical_platform != "windows") throw new InvalidDataException("Canonical first target must be Windows.");
        if (Mathf.Abs(config.target_extent_m - 1f) > 0.001f) throw new InvalidDataException("Gaussian exhibits must target an approximately 1 m normalized extent.");
        if (config.layout == null || config.layout.center_spacing_m <= 0 || config.layout.aisle_width_m <= 0 ||
            config.layout.margin_m < 0 || config.layout.pad_size_m <= 0 || config.layout.wall_height_m <= 0)
            throw new InvalidDataException("Gaussian exhibition layout values are invalid.");
        if (config.reference_camera == null || config.reference_camera.field_of_view <= 0)
            throw new InvalidDataException("Reference camera configuration is invalid.");
        if (config.video_player == null || string.IsNullOrEmpty(config.video_player.prefab_path))
            throw new InvalidDataException("Video player configuration is incomplete.");
    }

    static void ValidateRegistry(Registry registry)
    {
        if (registry.environments == null || registry.environments.Length == 0)
            throw new InvalidDataException("Gaussian source registry must contain at least one registered source.");
        var ids = new HashSet<string>();
        for (int i = 0; i < registry.environments.Length; i++)
        {
            EnvironmentEntry entry = registry.environments[i];
            if (entry == null || string.IsNullOrEmpty(entry.id) || !ids.Add(entry.id))
                throw new InvalidDataException("Gaussian source registry contains a missing or duplicate id at index " + i + ".");
            if (entry.display_index != i + 1)
                throw new InvalidDataException("Gaussian source registry display_index must be contiguous at " + entry.id + ".");
            if (entry.playback == null || string.IsNullOrEmpty(entry.playback.url) || !entry.playback.url.StartsWith("https://", StringComparison.Ordinal))
                throw new InvalidDataException("Registered source is missing an HTTPS playback URL: " + entry.id);
            if (entry.playback.status != "ready_untrusted" && entry.playback.status != "ready_allowlisted")
                throw new InvalidDataException("Registered source playback is not ready: " + entry.id);
            if ((entry.playback.status == "ready_untrusted") != entry.playback.requires_untrusted_urls)
                throw new InvalidDataException("Registered source playback trust state is inconsistent: " + entry.id);
        }
    }

    static void CreateFloorAndShell(ExhibitionConfig config, Vector2 floorSize)
    {
        Vector3 center = new Vector3(0f, -0.1f, 0f);
        Vector3 size = new Vector3(floorSize.x, 0.2f, floorSize.y);
        CreatePrimitive("WalkableFloor", center, size, true);
        float h = config.layout.wall_height_m;
        float t = 0.2f;
        float y = h * 0.5f;
        CreatePrimitive("Wall_Left", new Vector3(-floorSize.x * 0.5f, y, 0f), new Vector3(t, h, floorSize.y), true);
        CreatePrimitive("Wall_Right", new Vector3(floorSize.x * 0.5f, y, 0f), new Vector3(t, h, floorSize.y), true);
        CreatePrimitive("Wall_Back", new Vector3(0f, y, -floorSize.y * 0.5f), new Vector3(floorSize.x, h, t), true);
        CreatePrimitive("Wall_Front", new Vector3(0f, y, floorSize.y * 0.5f), new Vector3(floorSize.x, h, t), true);
    }

    static GameObject CreatePrimitive(string name, Vector3 position, Vector3 scale, bool isStatic)
    {
        GameObject value = GameObject.CreatePrimitive(PrimitiveType.Cube);
        value.name = name;
        value.transform.position = position;
        value.transform.localScale = scale;
        value.isStatic = isStatic;
        return value;
    }

    static void CreatePad(ExhibitionConfig config, Exhibit exhibit)
    {
        CreatePrimitive("ExhibitPad_" + exhibit.index.ToString("00"), new Vector3(exhibit.position.x, 0.05f, exhibit.position.z),
            new Vector3(config.layout.pad_size_m, 0.1f, config.layout.pad_size_m), true);
    }

    static void CreateLabel(Exhibit exhibit)
    {
        Type tmpType = FindType("TMPro.TextMeshPro");
        if (tmpType == null) throw new InvalidOperationException("TextMesh Pro is required for Gaussian exhibition labels.");
        GameObject label = new GameObject("ExhibitLabel_" + exhibit.index.ToString("00"));
        label.transform.SetPositionAndRotation(exhibit.position + Vector3.up * 1.2f + exhibit.rotation * Vector3.forward * 0.75f, exhibit.rotation);
        label.transform.localScale = Vector3.one * 0.1f;
        Component text = label.AddComponent(tmpType);
        tmpType.GetProperty("text")?.SetValue(text, exhibit.index.ToString("00") + "  " + exhibit.title);
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

    static void CreateLightProbes(Vector2 floorSize)
    {
        GameObject probes = new GameObject("Light Probes");
        LightProbeGroup group = probes.AddComponent<LightProbeGroup>();
        float x = Mathf.Max(1f, floorSize.x * 0.35f);
        float z = Mathf.Max(1f, floorSize.y * 0.3f);
        group.probePositions = new[]
        {
            new Vector3(-x, 0.5f, -z), new Vector3(x, 0.5f, -z), new Vector3(-x, 0.5f, z), new Vector3(x, 0.5f, z),
            new Vector3(-x, 2.5f, -z), new Vector3(x, 2.5f, -z), new Vector3(-x, 2.5f, z), new Vector3(x, 2.5f, z),
        };
    }

    static void CreateDescriptor(ExhibitionConfig config, Vector2 floorSize)
    {
        GameObject descriptorObject = new GameObject("VRCSceneDescriptor");
        VRCSceneDescriptor descriptor = descriptorObject.AddComponent<VRCSceneDescriptor>();
        descriptorObject.AddComponent<PipelineManager>();
        GameObject spawn = new GameObject("SpawnPoint");
        spawn.transform.position = new Vector3(0f, 0.1f, floorSize.y * 0.5f - 1f);
        spawn.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        descriptor.spawns = new[] { spawn.transform };
        SerializedObject serializedDescriptor = new SerializedObject(descriptor);
        SerializedProperty spawnRadius = serializedDescriptor.FindProperty("SpawnRadius");
        if (spawnRadius != null) spawnRadius.floatValue = 0f;
        SerializedProperty respawnHeight = serializedDescriptor.FindProperty("RespawnHeightY");
        if (respawnHeight != null) respawnHeight.floatValue = -5f;
        serializedDescriptor.ApplyModifiedPropertiesWithoutUndo();

        GameObject cameraObject = new GameObject("ReferenceCamera");
        cameraObject.transform.SetPositionAndRotation(new Vector3(0f, Mathf.Max(7f, floorSize.x * 0.35f), -floorSize.y * 0.55f), Quaternion.Euler(48f, 0f, 0f));
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.fieldOfView = config.reference_camera.field_of_view;
        descriptor.ReferenceCamera = cameraObject;
    }

    static void CreateVideoArea(ExhibitionConfig config, Registry registry, Vector2 floorSize)
    {
        Vector3 position = new Vector3(floorSize.x * 0.5f - 1f, 1.6f, 0f);
        GameObject videoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(config.video_player.prefab_path);
        if (videoPrefab == null) throw new InvalidOperationException("Canonical SDK video player prefab is missing: " + config.video_player.prefab_path);
        EnsureProgramAsset<GaussianVideoPlaylist>();
        EnsureProgramAsset<GaussianVideoPlaylistAction>();
        UdonSharpProgramAsset.UdonSharpCheckAbsent();
        UdonSharpProgramAsset.CompileAllCsPrograms(true);
        CreateVideoPlayer(registry, videoPrefab, position);
    }

    static void EnsureGaussianRuntimeTopology(Scene scene, int expectedExhibits)
    {
        Type splatType = FindType(GaussianSplatObjectTypeName);
        Type rendererType = FindType(GaussianSplatRendererTypeName);
        if (splatType == null || rendererType == null)
            throw new InvalidOperationException("Pinned VRChatGaussianSplatting renderer is not materialized. Run `task gaussian:prepare` before opening Unity.");
        int splatCount = CountActiveSceneComponents(splatType, scene);
        if (splatCount != expectedExhibits) throw new InvalidOperationException("Expected " + expectedExhibits + " active GaussianSplatObject components, found " + splatCount + ".");
        MethodInfo ensureRenderer = rendererType.GetMethod("EnsureSceneRendererExists", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Scene) }, null);
        if (ensureRenderer == null) throw new MissingMethodException(GaussianSplatRendererTypeName, "EnsureSceneRendererExists(Scene)");
        ensureRenderer.Invoke(null, new object[] { scene });
        int rendererCount = CountActiveSceneComponents(rendererType, scene);
        if (rendererCount != 1) throw new InvalidOperationException("Gaussian exhibition requires exactly one active GaussianSplatRenderer, found " + rendererCount + ".");
    }

    static int CountActiveSceneComponents(Type componentType, Scene scene)
    {
        int count = 0;
        foreach (UnityEngine.Object value in Resources.FindObjectsOfTypeAll(componentType))
        {
            Component component = value as Component;
            if (component != null && !EditorUtility.IsPersistent(component) && component.gameObject.scene == scene && component.gameObject.activeInHierarchy) count++;
        }
        return count;
    }

    static void CreateVideoPlayer(Registry registry, GameObject videoPrefab, Vector3 position)
    {
        GameObject videoPlayer = (GameObject)PrefabUtility.InstantiatePrefab(videoPrefab);
        videoPlayer.name = "SourceVideoPlayer";
        videoPlayer.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, -90f, 0f));
        UdonBehaviour syncPlayer = FindSyncPlayer(videoPlayer);
        foreach (VRCUrlInputField input in videoPlayer.GetComponentsInChildren<VRCUrlInputField>(true)) input.gameObject.SetActive(false);
        GameObject controlsRoot = new GameObject("CanonicalVideoPlaylist");
        controlsRoot.transform.SetPositionAndRotation(videoPlayer.transform.position, videoPlayer.transform.rotation);
        GaussianVideoPlaylist controller = AddUdon<GaussianVideoPlaylist>(new GameObject("GaussianVideoPlaylist"));
        controller.transform.SetParent(controlsRoot.transform, false);
        controller.syncPlayer = syncPlayer;
        controller.rateLimitSeconds = Mathf.Max(5f, controller.rateLimitSeconds);
        controller.urls = new VRCUrl[registry.environments.Length];
        controller.titles = new string[registry.environments.Length];
        controller.requiresUntrustedUrls = new bool[registry.environments.Length];
        for (int i = 0; i < registry.environments.Length; i++)
        {
            EnvironmentEntry entry = registry.environments[i];
            controller.urls[i] = new VRCUrl(entry.playback.url);
            controller.titles[i] = entry.id;
            controller.requiresUntrustedUrls[i] = entry.playback.requires_untrusted_urls;
        }
        controller.statusText = CreateWorldText(controlsRoot.transform, "VideoStatus", new Vector3(0f, 1.25f, 0.45f), new Vector2(900f, 120f), 34, "Source video playlist");
        int columns = Mathf.Min(5, Mathf.Max(1, registry.environments.Length));
        for (int i = 0; i < registry.environments.Length; i++)
        {
            int column = i % columns;
            int row = i / columns;
            float x = (column - (columns - 1) * 0.5f) * 0.6f;
            float y = 0.75f - row * 0.42f;
            CreatePlaylistAction(controlsRoot.transform, "SelectVideo_" + (i + 1).ToString("00"), new Vector3(x, y, 0.45f), new Vector3(0.5f, 0.12f, 0.18f), controller, 0, i, (i + 1).ToString("00"));
        }
        float controlsY = 0.75f - Mathf.CeilToInt((float)registry.environments.Length / columns) * 0.42f - 0.35f;
        CreatePlaylistAction(controlsRoot.transform, "PreviousVideo", new Vector3(-0.75f, controlsY, 0.45f), new Vector3(0.65f, 0.12f, 0.18f), controller, 1, 0, "PREV");
        CreatePlaylistAction(controlsRoot.transform, "ReplayVideo", new Vector3(0f, controlsY, 0.45f), new Vector3(0.65f, 0.12f, 0.18f), controller, 3, 0, "REPLAY");
        CreatePlaylistAction(controlsRoot.transform, "NextVideo", new Vector3(0.75f, controlsY, 0.45f), new Vector3(0.65f, 0.12f, 0.18f), controller, 2, 0, "NEXT");
        EditorUtility.SetDirty(controller);
    }

    static UdonBehaviour FindSyncPlayer(GameObject videoPlayer)
    {
        foreach (UdonBehaviour behaviour in videoPlayer.GetComponentsInChildren<UdonBehaviour>(true))
            if (behaviour.programSource != null && behaviour.programSource.name.IndexOf("UdonSyncPlayer", StringComparison.OrdinalIgnoreCase) >= 0) return behaviour;
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

    static T AddUdon<T>(GameObject gameObject) where T : UdonSharpBehaviour => (T)(Component)gameObject.AddUdonSharpComponent(typeof(T));

    static void EnsureProgramAsset<T>() where T : UdonSharpBehaviour
    {
        string[] scripts = AssetDatabase.FindAssets(typeof(T).Name + " t:MonoScript");
        MonoScript script = null;
        foreach (string guid in scripts)
        {
            MonoScript candidate = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
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
}
