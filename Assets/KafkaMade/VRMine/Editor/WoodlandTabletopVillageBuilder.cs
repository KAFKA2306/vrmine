using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Components;

public static class WoodlandTabletopVillageBuilder
{
    const string SpecPath = "config/world-design/generated/woodland-tabletop-village-v0.json";
    const string ScenePath = "Assets/KafkaMade/VRMine/Scenes/WoodlandTabletopVillage.unity";
    const string MaterialFolder = "Assets/KafkaMade/VRMine/Materials/WoodlandTabletopVillage";

    [Serializable] class WorldSpec
    {
        public SpatialGeometry spatial_geometry;
        public SocialClusters social_clusters;
        public RuntimeBudget runtime_budget;
        public WorldBuild world_build;
        public Atmosphere atmosphere;
        public Blockout blockout;
    }

    [Serializable] class SpatialGeometry
    {
        public float overall_width_m;
        public float overall_depth_m;
        public Zone[] zones;
    }

    [Serializable] class Zone
    {
        public string zone_id;
        public float diameter_m;
        public float tabletop_height_m;
        public float tabletop_thickness_m;
    }

    [Serializable] class SocialClusters { public PrimaryCore primary_core; }
    [Serializable] class PrimaryCore { public float seat_spacing_m; public int seat_count; }
    [Serializable] class RuntimeBudget { public int realtime_light_count; public float camera_near_clip_m; }

    [Serializable] class WorldBuild
    {
        public float ceiling_height_m;
        public Spawn spawn;
        public SocialCore social_core;
        public Retreat retreat;
        public HeroView hero_view;
    }

    [Serializable] class Spawn { public float[] position_m; public float facing_deg; public float buffer_radius_m; }
    [Serializable] class SocialCore { public float[] center_m; public int seat_count; public float seat_radius_m; }
    [Serializable] class Retreat { public float[] center_m; public int seat_count; }
    [Serializable] class HeroView { public float[] position_m; public float[] target_m; }

    [Serializable] class Atmosphere { public Palette palette; }
    [Serializable] class Palette
    {
        public string wood_brown;
        public string forest_green;
        public string warm_cream;
        public string accent_terracotta;
        public string lamp_gold;
    }

    [Serializable] class Blockout { public Anchor[] anchors; }
    [Serializable] class Anchor
    {
        public string id;
        public float[] position_m;
        public float[] footprint_m;
        public float height_m;
        public float yaw_deg;
        public string role;
    }

    [MenuItem("VRMine/Worlds/Build Woodland Tabletop Village")]
    public static void Build()
    {
        WorldSpec spec = LoadSpec();
        ValidateSpec(spec);
        EnsureFolder("Assets/KafkaMade/VRMine/Scenes");
        EnsureFolder(MaterialFolder);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject root = new GameObject("WoodlandTabletopVillage");

        Material wood = MaterialAsset("Wood", ParseColor(spec.atmosphere.palette.wood_brown), 0.82f);
        Material forest = MaterialAsset("Forest", ParseColor(spec.atmosphere.palette.forest_green), 0.9f);
        Material cream = MaterialAsset("Cream", ParseColor(spec.atmosphere.palette.warm_cream), 0.88f);
        Material terracotta = MaterialAsset("Terracotta", ParseColor(spec.atmosphere.palette.accent_terracotta), 0.84f);
        Material lamp = MaterialAsset("LampGold", ParseColor(spec.atmosphere.palette.lamp_gold), 0.72f, true);
        Material stone = MaterialAsset("Stone", new Color(0.34f, 0.34f, 0.31f), 0.92f);
        Material water = MaterialAsset("Water", new Color(0.20f, 0.36f, 0.39f), 0.78f);
        Material wall = MaterialAsset("WarmWall", new Color(0.46f, 0.39f, 0.30f), 0.92f);

        BuildObservationRoom(root.transform, spec, wall, wood);
        BuildTabletop(root.transform, spec, wood, forest, cream, terracotta, lamp, stone, water);
        BuildSocialSeating(root.transform, spec, wood, cream);
        BuildRetreat(root.transform, spec, wood, cream);
        BuildLighting(root.transform, spec);
        CreateDescriptor(root.transform, spec);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("Woodland Tabletop Village built from canonical spec: " + SpecPath);
    }

    // Batch entry point for Unity -executeMethod.
    public static void BuildBatch()
    {
        Build();
        WoodlandTabletopVillageVerification.VerifyOrThrow();
    }

    static WorldSpec LoadSpec()
    {
        string absolute = Path.GetFullPath(Path.Combine(Application.dataPath, "..", SpecPath));
        if (!File.Exists(absolute)) throw new FileNotFoundException("Canonical woodland spec not found", absolute);
        WorldSpec spec = JsonUtility.FromJson<WorldSpec>(File.ReadAllText(absolute));
        if (spec == null) throw new InvalidDataException("Failed to parse canonical woodland spec: " + absolute);
        return spec;
    }

    static void ValidateSpec(WorldSpec spec)
    {
        if (spec.spatial_geometry == null || spec.world_build == null || spec.blockout == null || spec.blockout.anchors == null)
            throw new InvalidDataException("Woodland spec is missing required physical sections");
        if (spec.runtime_budget == null || spec.runtime_budget.realtime_light_count != 0)
            throw new InvalidDataException("Woodland v0 requires zero realtime lights");
        if (spec.blockout.anchors.Length < 8)
            throw new InvalidDataException("Woodland v0 requires all eight tabletop anchors");
    }

    static void BuildObservationRoom(Transform parent, WorldSpec spec, Material wall, Material wood)
    {
        float width = spec.spatial_geometry.overall_width_m;
        float depth = spec.spatial_geometry.overall_depth_m;
        float height = spec.world_build.ceiling_height_m;
        const float thickness = 0.08f;
        Transform room = new GameObject("ObservationRoom").transform;
        room.SetParent(parent, false);

        Cube("Floor", new Vector3(0f, -thickness * 0.5f, 0f), new Vector3(width, thickness, depth), wood, room);
        Cube("Ceiling", new Vector3(0f, height + thickness * 0.5f, 0f), new Vector3(width, thickness, depth), wall, room);
        Cube("WallLeft", new Vector3(-width * 0.5f - thickness * 0.5f, height * 0.5f, 0f), new Vector3(thickness, height, depth), wall, room);
        Cube("WallRight", new Vector3(width * 0.5f + thickness * 0.5f, height * 0.5f, 0f), new Vector3(thickness, height, depth), wall, room);
        Cube("WallFront", new Vector3(0f, height * 0.5f, -depth * 0.5f - thickness * 0.5f), new Vector3(width, height, thickness), wall, room);
        Cube("WallBack", new Vector3(0f, height * 0.5f, depth * 0.5f + thickness * 0.5f), new Vector3(width, height, thickness), wall, room);
    }

    static void BuildTabletop(Transform parent, WorldSpec spec, Material wood, Material forest, Material cream, Material terracotta, Material lamp, Material stone, Material water)
    {
        Zone tableZone = spec.spatial_geometry.zones.FirstOrDefault(z => z.zone_id == "woodland_tabletop");
        if (tableZone == null) throw new InvalidDataException("woodland_tabletop zone is required");

        Transform table = new GameObject("WoodlandTabletop").transform;
        table.SetParent(parent, false);
        float centerY = tableZone.tabletop_height_m - tableZone.tabletop_thickness_m * 0.5f;
        Cylinder("DioramaTableTop", new Vector3(0f, centerY, 0f), tableZone.diameter_m, tableZone.tabletop_thickness_m, wood, table);
        Cylinder("DioramaPedestal", new Vector3(0f, centerY * 0.48f, 0f), 0.72f, centerY * 0.92f, wood, table);

        Transform village = new GameObject("VillageBlockout").transform;
        village.SetParent(table, false);
        foreach (Anchor anchor in spec.blockout.anchors)
        {
            Vector3 position = SpecToUnity(anchor.position_m);
            switch (anchor.id)
            {
                case "great_tree": BuildGreatTree(village, anchor, position, wood, forest); break;
                case "bridge": BuildBridge(village, anchor, position, wood, stone); break;
                case "lodge": BuildHouse(village, anchor, position, wood, cream, terracotta, lamp, true); break;
                case "cottage_a":
                case "cottage_b": BuildHouse(village, anchor, position, wood, cream, terracotta, lamp, false); break;
                case "market_stall": BuildMarket(village, anchor, position, wood, terracotta, lamp); break;
                case "reading_bench": BuildMiniBench(village, anchor, position, wood); break;
                case "stream": BuildStream(village, anchor, position, stone, water); break;
                default: BuildAnchorProxy(village, anchor, position, cream); break;
            }
        }
    }

    static void BuildSocialSeating(Transform parent, WorldSpec spec, Material wood, Material cream)
    {
        Transform seating = new GameObject("SocialSeating").transform;
        seating.SetParent(parent, false);
        int count = spec.world_build.social_core.seat_count;
        float radius = spec.world_build.social_core.seat_radius_m;
        Vector3 center = SpecToUnity(spec.world_build.social_core.center_m);
        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.PI * 2f * i / count;
            Vector3 pos = new Vector3(center.x + Mathf.Cos(angle) * radius, 0f, center.z + Mathf.Sin(angle) * radius);
            GameObject stool = Cylinder("SocialSeat_" + (i + 1), pos + Vector3.up * 0.22f, 0.42f, 0.44f, wood, seating);
            Cylinder("SocialSeatTop_" + (i + 1), pos + Vector3.up * 0.46f, 0.48f, 0.08f, cream, stool.transform);
            FaceTowards(stool.transform, new Vector3(center.x, pos.y, center.z));
        }
    }

    static void BuildRetreat(Transform parent, WorldSpec spec, Material wood, Material cream)
    {
        Vector3 center = SpecToUnity(spec.world_build.retreat.center_m);
        Transform retreat = new GameObject("ReadingNook").transform;
        retreat.SetParent(parent, false);
        Cube("ReadingBenchSeat", center + Vector3.up * 0.34f, new Vector3(1.05f, 0.12f, 0.42f), wood, retreat);
        Cube("ReadingBenchBack", center + new Vector3(0f, 0.68f, 0.18f), new Vector3(1.05f, 0.62f, 0.10f), wood, retreat);
        Cube("ReadingCushion", center + Vector3.up * 0.43f, new Vector3(0.92f, 0.07f, 0.34f), cream, retreat);
        retreat.rotation = Quaternion.Euler(0f, 135f, 0f);
    }

    static void BuildGreatTree(Transform parent, Anchor anchor, Vector3 position, Material wood, Material forest)
    {
        Transform root = AnchorRoot(parent, anchor, position);
        float h = anchor.height_m;
        Cylinder("Trunk", Vector3.up * h * 0.31f, anchor.footprint_m[0] * 0.24f, h * 0.62f, wood, root);
        Sphere("CanopyLow", Vector3.up * h * 0.67f, new Vector3(anchor.footprint_m[0], h * 0.42f, anchor.footprint_m[1]) * 0.90f, forest, root);
        Sphere("CanopyTop", new Vector3(-0.06f, h * 0.86f, 0.03f), new Vector3(anchor.footprint_m[0], h * 0.34f, anchor.footprint_m[1]) * 0.62f, forest, root);
    }

    static void BuildHouse(Transform parent, Anchor anchor, Vector3 position, Material wood, Material cream, Material roof, Material lamp, bool lodge)
    {
        Transform root = AnchorRoot(parent, anchor, position);
        float width = anchor.footprint_m[0];
        float depth = anchor.footprint_m[1];
        float bodyH = anchor.height_m * 0.58f;
        Cube("Body", Vector3.up * bodyH * 0.5f, new Vector3(width, bodyH, depth), cream, root);
        GameObject roofA = Cube("RoofA", new Vector3(-width * 0.18f, bodyH + anchor.height_m * 0.10f, 0f), new Vector3(width * 0.62f, anchor.height_m * 0.12f, depth * 1.14f), roof, root);
        roofA.transform.localRotation = Quaternion.Euler(0f, 0f, 28f);
        GameObject roofB = Cube("RoofB", new Vector3(width * 0.18f, bodyH + anchor.height_m * 0.10f, 0f), new Vector3(width * 0.62f, anchor.height_m * 0.12f, depth * 1.14f), roof, root);
        roofB.transform.localRotation = Quaternion.Euler(0f, 0f, -28f);
        Cube("Door", new Vector3(0f, bodyH * 0.32f, -depth * 0.51f), new Vector3(width * 0.20f, bodyH * 0.52f, 0.025f), wood, root);
        Cube("WarmWindow", new Vector3(width * 0.27f, bodyH * 0.55f, -depth * 0.515f), new Vector3(width * 0.18f, bodyH * 0.22f, 0.02f), lamp, root);
        if (lodge) Cylinder("Chimney", new Vector3(width * 0.28f, anchor.height_m * 0.78f, 0f), width * 0.09f, anchor.height_m * 0.36f, wood, root);
    }

    static void BuildBridge(Transform parent, Anchor anchor, Vector3 position, Material wood, Material stone)
    {
        Transform root = AnchorRoot(parent, anchor, position);
        Cube("Deck", Vector3.up * anchor.height_m * 0.40f, new Vector3(anchor.footprint_m[0], anchor.height_m * 0.20f, anchor.footprint_m[1]), wood, root);
        Cube("RailLeft", new Vector3(-anchor.footprint_m[0] * 0.42f, anchor.height_m * 0.85f, 0f), new Vector3(0.025f, anchor.height_m, anchor.footprint_m[1]), wood, root);
        Cube("RailRight", new Vector3(anchor.footprint_m[0] * 0.42f, anchor.height_m * 0.85f, 0f), new Vector3(0.025f, anchor.height_m, anchor.footprint_m[1]), wood, root);
    }

    static void BuildMarket(Transform parent, Anchor anchor, Vector3 position, Material wood, Material canopy, Material lamp)
    {
        Transform root = AnchorRoot(parent, anchor, position);
        float w = anchor.footprint_m[0];
        float d = anchor.footprint_m[1];
        Cube("Counter", new Vector3(0f, anchor.height_m * 0.34f, 0f), new Vector3(w, anchor.height_m * 0.12f, d), wood, root);
        for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
                Cube("Post", new Vector3(x * w * 0.43f, anchor.height_m * 0.57f, z * d * 0.42f), new Vector3(0.025f, anchor.height_m * 0.76f, 0.025f), wood, root);
        Cube("Canopy", Vector3.up * anchor.height_m * 0.91f, new Vector3(w * 1.08f, anchor.height_m * 0.08f, d * 1.10f), canopy, root);
        Sphere("Lantern", new Vector3(0f, anchor.height_m * 0.76f, -d * 0.46f), Vector3.one * anchor.height_m * 0.12f, lamp, root);
    }

    static void BuildMiniBench(Transform parent, Anchor anchor, Vector3 position, Material wood)
    {
        Transform root = AnchorRoot(parent, anchor, position);
        Cube("Seat", Vector3.up * anchor.height_m * 0.38f, new Vector3(anchor.footprint_m[0], anchor.height_m * 0.18f, anchor.footprint_m[1]), wood, root);
        Cube("Back", new Vector3(0f, anchor.height_m * 0.72f, anchor.footprint_m[1] * 0.38f), new Vector3(anchor.footprint_m[0], anchor.height_m * 0.54f, 0.025f), wood, root);
    }

    static void BuildStream(Transform parent, Anchor anchor, Vector3 position, Material stone, Material water)
    {
        Transform root = AnchorRoot(parent, anchor, position);
        Cube("Water", Vector3.up * 0.006f, new Vector3(anchor.footprint_m[0], 0.012f, anchor.footprint_m[1]), water, root);
        Cube("BankNear", new Vector3(0f, 0.015f, -anchor.footprint_m[1] * 0.58f), new Vector3(anchor.footprint_m[0], 0.03f, 0.035f), stone, root);
        Cube("BankFar", new Vector3(0f, 0.015f, anchor.footprint_m[1] * 0.58f), new Vector3(anchor.footprint_m[0], 0.03f, 0.035f), stone, root);
    }

    static void BuildAnchorProxy(Transform parent, Anchor anchor, Vector3 position, Material material)
    {
        Transform root = AnchorRoot(parent, anchor, position);
        Cube("Proxy", Vector3.up * anchor.height_m * 0.5f, new Vector3(anchor.footprint_m[0], anchor.height_m, anchor.footprint_m[1]), material, root);
    }

    static Transform AnchorRoot(Transform parent, Anchor anchor, Vector3 worldPosition)
    {
        GameObject root = new GameObject(anchor.id);
        root.transform.SetParent(parent, false);
        root.transform.position = worldPosition;
        root.transform.rotation = Quaternion.Euler(0f, anchor.yaw_deg, 0f);
        root.tag = "Untagged";
        return root.transform;
    }

    static void BuildLighting(Transform parent, WorldSpec spec)
    {
        GameObject lightObject = new GameObject("BakedSun");
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.rotation = Quaternion.Euler(38f, -32f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.78f, 0.58f);
        light.intensity = 0.72f;
        light.shadows = LightShadows.Soft;
        light.lightmapBakeType = LightmapBakeType.Baked;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.20f, 0.23f, 0.20f);
        RenderSettings.ambientEquatorColor = new Color(0.12f, 0.13f, 0.12f);
        RenderSettings.ambientGroundColor = new Color(0.055f, 0.050f, 0.045f);
    }

    static void CreateDescriptor(Transform parent, WorldSpec spec)
    {
        GameObject descriptorObject = new GameObject("VRCWorld");
        descriptorObject.transform.SetParent(parent, false);
        VRCSceneDescriptor descriptor = descriptorObject.AddComponent<VRCSceneDescriptor>();

        GameObject spawn = new GameObject("SpawnPoint");
        spawn.transform.SetParent(parent, false);
        spawn.transform.position = SpecToUnity(spec.world_build.spawn.position_m) + Vector3.up * 0.02f;
        spawn.transform.rotation = Quaternion.Euler(0f, spec.world_build.spawn.facing_deg, 0f);
        descriptor.spawns = new[] { spawn.transform };

        GameObject cameraObject = new GameObject("ReferenceCamera");
        cameraObject.transform.SetParent(parent, false);
        cameraObject.transform.position = SpecToUnity(spec.world_build.hero_view.position_m);
        FaceTowards(cameraObject.transform, SpecToUnity(spec.world_build.hero_view.target_m));
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.fieldOfView = 58f;
        camera.nearClipPlane = Mathf.Max(0.01f, spec.runtime_budget.camera_near_clip_m);
        descriptor.ReferenceCamera = cameraObject;
    }

    static Vector3 SpecToUnity(float[] xyz)
    {
        if (xyz == null || xyz.Length != 3) throw new InvalidDataException("Expected a three-component spec position");
        return new Vector3(xyz[0], xyz[2], xyz[1]);
    }

    static GameObject Cube(string name, Vector3 position, Vector3 size, Material material, Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        SetupPrimitive(go, name, position, size, material, parent);
        return go;
    }

    static GameObject Cylinder(string name, Vector3 position, float diameter, float height, Material material, Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        SetupPrimitive(go, name, position, new Vector3(diameter, height * 0.5f, diameter), material, parent);
        return go;
    }

    static GameObject Sphere(string name, Vector3 position, Vector3 size, Material material, Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        SetupPrimitive(go, name, position, size, material, parent);
        return go;
    }

    static void SetupPrimitive(GameObject go, string name, Vector3 position, Vector3 scale, Material material, Transform parent)
    {
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
    }

    static Material MaterialAsset(string name, Color color, float roughness, bool emission = false)
    {
        EnsureFolder(MaterialFolder);
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("VRChat/Mobile/Standard Lite");
        if (shader == null) shader = Shader.Find("Standard");
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }
        material.color = color;
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 1f - roughness);
        if (emission && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.25f);
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    static Color ParseColor(string html)
    {
        Color color;
        if (!ColorUtility.TryParseHtmlString(html, out color)) throw new InvalidDataException("Invalid HTML color in woodland spec: " + html);
        return color;
    }

    static void FaceTowards(Transform transform, Vector3 target)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    static void AddSceneToBuildSettings(string scenePath)
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        if (scenes.Any(s => s.path == scenePath)) return;
        EditorBuildSettings.scenes = scenes.Concat(new[] { new EditorBuildSettingsScene(scenePath, true) }).ToArray();
    }

    static void EnsureFolder(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}