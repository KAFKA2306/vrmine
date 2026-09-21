using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.Core;
using VRC.SDK3.Components;

public static class MinooRiverWorldBuilder
{
    public const string ConfigPath = "config/gaussian-worlds/minoo-river-osaka.json";
    public const string ScenePath = "Assets/KafkaMade/VRMine/Scenes/MinooRiverOsaka.unity";
    public const string RegistryPath = "config/gaussian-worlds/minoo-river-osaka-sources.json";
    const string PrefabDirectory = "Assets/KafkaMade/VRMine/GaussianSplatting/Prefabs";
    const string GaussianSplatObjectTypeName = "GaussianSplatting.GaussianSplatObject";
    const string GaussianSplatRendererTypeName = "GaussianSplatting.GaussianSplatRenderer";

    [Serializable]
    sealed class WorldConfig
    {
        public int schema_version;
        public string world_id;
        public string world_mode;
        public string scene_path;
        public string source_registry;
        public string canonical_platform;
        public string renderer;
        public float target_extent_m;
        public float presentation_scale_multiplier;
        public bool show_title_sign;
        public LayoutConfig layout;
        public MaterialConfig materials;
        public CameraConfig reference_camera;
        public SpawnConfig spawn;
    }

    [Serializable]
    sealed class LayoutConfig
    {
        public float floor_size_x_m;
        public float floor_size_z_m;
        public float boundary_height_m;
        public float boundary_thickness_m;
    }

    [Serializable]
    sealed class MaterialConfig
    {
        public string ground;
        public string water;
    }

    [Serializable]
    sealed class CameraConfig
    {
        public float field_of_view;
        public float x;
        public float y;
        public float z;
        public float pitch;
        public float yaw;
    }

    [Serializable]
    sealed class SpawnConfig
    {
        public float x;
        public float y;
        public float z;
        public float yaw;
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
        public int display_index;
    }

    [MenuItem("VRMine/Build Minoo River Osaka World")]
    public static void Build()
    {
        WorldConfig config = LoadJson<WorldConfig>(ConfigPath, "Minoo River world config");
        ValidateConfig(config);
        Registry registry = LoadJson<Registry>(config.source_registry, "Minoo River source registry");
        ValidateRegistry(config, registry);

        string prefabPath = PrefabDirectory + "/" + registry.environments[0].id + ".prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            throw new InvalidOperationException("Minoo River Gaussian prefab is missing. Run the Minoo local preparation task first: " + prefabPath);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Material groundMaterial = LoadMaterial(config.materials.ground, "ground");
        Material waterMaterial = LoadMaterial(config.materials.water, "water");
        CreateWorldShell(config, groundMaterial, waterMaterial);
        Light sun = CreateBakedDirectionalLight();
        ConfigureRiverBackdrop(sun);
        CreateDescriptor(config);

        GameObject root = new GameObject("MinooRiverGaussianSplat");
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "MinooRiverSplat";
        instance.transform.SetParent(root.transform);
        // Preserve the prefab's verified normalization scale when overriding the
        // scene presentation scale. Prefab instances replace, rather than
        // multiply, the asset root scale when m_LocalScale is overridden.
        instance.transform.localScale = prefab.transform.localScale * config.presentation_scale_multiplier;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        AlignSplatToFloor(instance);
        instance.SetActive(true);
        if (config.show_title_sign) CreateTitleSign(config);

        EnsureGaussianRuntimeTopology(scene);
        EnsureAssetFolder(Path.GetDirectoryName(config.scene_path)?.Replace('\\', '/'));
        EditorSceneManager.SaveScene(scene, config.scene_path);
        GaussianExhibitionBuilder.ConfigureBakedLighting();
        if (!Lightmapping.Bake())
            throw new InvalidOperationException("Unity failed to complete the Minoo River baked-lighting job.");
        EditorSceneManager.SaveScene(scene);
        EnsureBuildScene(config.scene_path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(config.scene_path);

        Debug.Log("Minoo River Osaka world ready: scene=" + config.scene_path + ", source=" + registry.environments[0].id + ", scale=" + config.presentation_scale_multiplier.ToString("F3"));
    }

    static Light CreateBakedDirectionalLight()
    {
        GameObject lightObject = new GameObject("Baked Directional Light");
        lightObject.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        light.lightmapBakeType = LightmapBakeType.Baked;
        return light;
    }

    static void ConfigureRiverBackdrop(Light sun)
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.54f, 0.68f, 0.78f);
        RenderSettings.ambientEquatorColor = new Color(0.68f, 0.72f, 0.68f);
        RenderSettings.ambientGroundColor = new Color(0.22f, 0.25f, 0.22f);
        RenderSettings.ambientIntensity = 1.15f;
        RenderSettings.skybox = RiverSkyMaterial();
        RenderSettings.fog = false;
        RenderSettings.sun = sun;
        DynamicGI.UpdateEnvironment();
    }

    static void ValidateConfig(WorldConfig config)
    {
        if (config == null || config.schema_version != 1)
            throw new InvalidDataException("Unsupported Minoo River world config schema.");
        if (config.world_id != "minoo-river-osaka" || config.world_mode != "single-world")
            throw new InvalidDataException("Minoo River world config has an unexpected identity or mode.");
        if (config.scene_path != ScenePath)
            throw new InvalidDataException("Minoo River scene path is not canonical: " + config.scene_path);
        if (config.canonical_platform != "windows")
            throw new InvalidDataException("Minoo River world currently targets Windows only.");
        if (string.IsNullOrEmpty(config.source_registry) || string.IsNullOrEmpty(config.renderer))
            throw new InvalidDataException("Minoo River source registry or renderer is missing.");
        if (!FinitePositive(config.target_extent_m) || !FinitePositive(config.presentation_scale_multiplier))
            throw new InvalidDataException("Minoo River extent and presentation scale must be positive finite values.");
        if (config.layout == null || !FinitePositive(config.layout.floor_size_x_m) || !FinitePositive(config.layout.floor_size_z_m) ||
            !FinitePositive(config.layout.boundary_height_m) || !FinitePositive(config.layout.boundary_thickness_m))
            throw new InvalidDataException("Minoo River layout is invalid.");
        if (config.materials == null || string.IsNullOrEmpty(config.materials.ground) || string.IsNullOrEmpty(config.materials.water))
            throw new InvalidDataException("Minoo River materials are incomplete.");
        if (config.reference_camera == null || !FinitePositive(config.reference_camera.field_of_view))
            throw new InvalidDataException("Minoo River reference camera is invalid.");
        if (config.spawn == null)
            throw new InvalidDataException("Minoo River spawn configuration is missing.");
    }

    static void ValidateRegistry(WorldConfig config, Registry registry)
    {
        if (registry == null || registry.environments == null || registry.environments.Length != 1)
            throw new InvalidDataException("Minoo River world requires exactly one registered Gaussian source.");
        EnvironmentEntry entry = registry.environments[0];
        if (entry == null || entry.id != config.world_id || entry.display_index != 1)
            throw new InvalidDataException("Minoo River source registry identity is invalid.");
    }

    static void CreateWorldShell(WorldConfig config, Material groundMaterial, Material waterMaterial)
    {
        GameObject floor = CreatePrimitive("WalkableFloor", new Vector3(0f, -0.1f, 0f),
            new Vector3(config.layout.floor_size_x_m, 0.2f, config.layout.floor_size_z_m), true, groundMaterial);
        floor.GetComponent<Renderer>().enabled = false;

        float edgeX = config.layout.floor_size_x_m * 0.5f;
        float edgeZ = config.layout.floor_size_z_m * 0.5f;
        float height = config.layout.boundary_height_m;
        float thickness = config.layout.boundary_thickness_m;
        CreateBoundary("Boundary_Left", new Vector3(-edgeX, height * 0.5f, 0f), new Vector3(thickness, height, config.layout.floor_size_z_m));
        CreateBoundary("Boundary_Right", new Vector3(edgeX, height * 0.5f, 0f), new Vector3(thickness, height, config.layout.floor_size_z_m));
        CreateBoundary("Boundary_Back", new Vector3(0f, height * 0.5f, -edgeZ), new Vector3(config.layout.floor_size_x_m, height, thickness));
        CreateBoundary("Boundary_Front", new Vector3(0f, height * 0.5f, edgeZ), new Vector3(config.layout.floor_size_x_m, height, thickness));

        GameObject waterMarker = CreatePrimitive("RiverWaterMarker", new Vector3(0f, -0.19f, 0f),
            new Vector3(config.layout.floor_size_x_m * 0.7f, 0.02f, config.layout.floor_size_z_m * 0.2f), true, waterMaterial);
        waterMarker.GetComponent<Renderer>().enabled = false;
        waterMarker.GetComponent<Collider>().enabled = false;
    }

    static GameObject CreateBoundary(string name, Vector3 position, Vector3 scale)
    {
        GameObject boundary = CreatePrimitive(name, position, scale, true, null);
        Renderer renderer = boundary.GetComponent<Renderer>();
        if (renderer != null) renderer.enabled = false;
        return boundary;
    }

    static GameObject CreatePrimitive(string name, Vector3 position, Vector3 scale, bool isStatic, Material material)
    {
        GameObject value = GameObject.CreatePrimitive(PrimitiveType.Cube);
        value.name = name;
        value.transform.position = position;
        value.transform.localScale = scale;
        value.isStatic = isStatic;
        if (material != null) value.GetComponent<Renderer>().sharedMaterial = material;
        return value;
    }

    static void CreateDescriptor(WorldConfig config)
    {
        GameObject descriptorObject = new GameObject("VRCSceneDescriptor");
        VRCSceneDescriptor descriptor = descriptorObject.AddComponent<VRCSceneDescriptor>();
        descriptorObject.AddComponent<PipelineManager>();

        GameObject spawn = new GameObject("SpawnPoint");
        spawn.transform.position = new Vector3(config.spawn.x, config.spawn.y, config.spawn.z);
        spawn.transform.rotation = Quaternion.Euler(0f, config.spawn.yaw, 0f);
        descriptor.spawns = new[] { spawn.transform };
        SerializedObject serializedDescriptor = new SerializedObject(descriptor);
        SerializedProperty spawnRadius = serializedDescriptor.FindProperty("SpawnRadius");
        if (spawnRadius != null) spawnRadius.floatValue = 0f;
        SerializedProperty respawnHeight = serializedDescriptor.FindProperty("RespawnHeightY");
        if (respawnHeight != null) respawnHeight.floatValue = -5f;
        serializedDescriptor.ApplyModifiedPropertiesWithoutUndo();

        GameObject cameraObject = new GameObject("ReferenceCamera");
        cameraObject.transform.SetPositionAndRotation(
            new Vector3(config.reference_camera.x, config.reference_camera.y, config.reference_camera.z),
            Quaternion.Euler(config.reference_camera.pitch, config.reference_camera.yaw, 0f));
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = config.reference_camera.field_of_view;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 500f;
        descriptor.ReferenceCamera = cameraObject;
    }

    static void CreateTitleSign(WorldConfig config)
    {
        GameObject sign = CreatePrimitive("WorldTitleSign", new Vector3(0f, 2.2f, -13.5f), new Vector3(5f, 0.08f, 1.2f), false, null);
        sign.GetComponent<Renderer>().enabled = false;
        GameObject canvasObject = new GameObject("WorldTitleCanvas");
        canvasObject.transform.SetParent(sign.transform, false);
        canvasObject.transform.localPosition = new Vector3(0f, 0f, -0.55f);
        canvasObject.transform.localRotation = Quaternion.identity;
        canvasObject.transform.localScale = Vector3.one * 0.002f;
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1200f, 220f);
        GameObject textObject = new GameObject("WorldTitle");
        textObject.transform.SetParent(canvasObject.transform, false);
        UnityEngine.UI.Text text = textObject.AddComponent<UnityEngine.UI.Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 44;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = "箕面川 / Minoo River Osaka\n3DGS field capture";
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static void AlignSplatToFloor(GameObject instance)
    {
        Type splatType = FindType(GaussianSplatObjectTypeName);
        Component splat = instance.GetComponentInChildren(splatType, true);
        if (splat == null) throw new InvalidOperationException("GaussianSplatObject is missing from the Minoo prefab.");
        MethodInfo boundsMethod = splatType.GetMethod("TryGetLocalBounds", BindingFlags.Public | BindingFlags.Instance);
        if (boundsMethod == null) throw new MissingMethodException(GaussianSplatObjectTypeName, "TryGetLocalBounds");
        object[] args = new object[] { new Bounds() };
        bool valid = (bool)boundsMethod.Invoke(splat, args);
        if (!valid) throw new InvalidOperationException("Minoo Gaussian bounds are unavailable.");
        Bounds world = TransformBounds(splat.transform, (Bounds)args[0]);
        instance.transform.position += Vector3.up * -world.min.y;
    }

    static void EnsureGaussianRuntimeTopology(Scene scene)
    {
        Type splatType = FindType(GaussianSplatObjectTypeName);
        Type rendererType = FindType(GaussianSplatRendererTypeName);
        if (splatType == null || rendererType == null)
            throw new InvalidOperationException("Pinned VRChatGaussianSplatting renderer is not materialized. Run `task gaussian:renderer` first.");
        if (CountActiveSceneComponents(splatType, scene) != 1)
            throw new InvalidOperationException("Minoo River world requires exactly one active GaussianSplatObject.");
        MethodInfo ensureRenderer = rendererType.GetMethod("EnsureSceneRendererExists", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Scene) }, null);
        if (ensureRenderer == null) throw new MissingMethodException(GaussianSplatRendererTypeName, "EnsureSceneRendererExists(Scene)");
        ensureRenderer.Invoke(null, new object[] { scene });
        if (CountActiveSceneComponents(rendererType, scene) != 1)
            throw new InvalidOperationException("Minoo River world requires exactly one active GaussianSplatRenderer.");
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

    static Material LoadMaterial(string path, string role)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) throw new InvalidOperationException("Minoo River " + role + " material is missing: " + path);
        return material;
    }

    static Material RiverSkyMaterial()
    {
        const string path = "Assets/KafkaMade/VRMine/Materials/MinooRiverSky.mat";
        Material sky = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Skybox/Procedural");
        if (shader == null) throw new InvalidOperationException("Unity built-in Skybox/Procedural shader is required for the Minoo River backdrop.");
        if (sky == null)
        {
            sky = new Material(shader);
            AssetDatabase.CreateAsset(sky, path);
        }
        else if (sky.shader != shader) sky.shader = shader;
        if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", new Color(0.48f, 0.66f, 0.78f));
        if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", new Color(0.20f, 0.25f, 0.23f));
        if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", 0.65f);
        if (sky.HasProperty("_SunSize")) sky.SetFloat("_SunSize", 0.035f);
        if (sky.HasProperty("_SunSizeConvergence")) sky.SetFloat("_SunSizeConvergence", 4f);
        if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", 0.85f);
        EditorUtility.SetDirty(sky);
        return sky;
    }

    static T LoadJson<T>(string path, string label) where T : class
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) throw new FileNotFoundException(label + " is missing.", path);
        T value = JsonUtility.FromJson<T>(File.ReadAllText(path));
        if (value == null) throw new InvalidDataException(label + " could not be parsed.");
        return value;
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

    static Type FindType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName, false);
            if (type != null) return type;
        }
        return null;
    }

    static bool FinitePositive(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
}
