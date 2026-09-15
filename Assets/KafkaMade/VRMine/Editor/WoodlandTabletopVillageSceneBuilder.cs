using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.Core;
using VRC.SDK3.Components;

public static class WoodlandTabletopVillageSceneBuilder
{
    const string SpecPath = "config/world-design/generated/woodland-tabletop-village-v0.json";
    const string ScenePath = "Assets/KafkaMade/VRMine/Scenes/WoodlandTabletopVillage.unity";
    const string MaterialFolder = "Assets/KafkaMade/VRMine/Materials/WoodlandTabletopVillage";

    [Serializable] class WorldSpec { public Spatial spatial_geometry; public RuntimeBudget runtime_budget; public WorldBuild world_build; public Atmosphere atmosphere; public Blockout blockout; }
    [Serializable] class Spatial { public float overall_width_m; public float overall_depth_m; public Zone[] zones; }
    [Serializable] class Zone { public string zone_id; public float diameter_m; public float tabletop_height_m; public float tabletop_thickness_m; }
    [Serializable] class RuntimeBudget { public int realtime_light_count; public float camera_near_clip_m; }
    [Serializable] class WorldBuild { public float ceiling_height_m; public Spawn spawn; public Social social_core; public Retreat retreat; public Hero hero_view; }
    [Serializable] class Spawn { public float[] position_m; public float facing_deg; }
    [Serializable] class Social { public float[] center_m; public int seat_count; public float seat_radius_m; }
    [Serializable] class Retreat { public float[] center_m; }
    [Serializable] class Hero { public float[] position_m; public float[] target_m; }
    [Serializable] class Atmosphere { public Palette palette; }
    [Serializable] class Palette { public string wood_brown; public string forest_green; public string warm_cream; public string accent_terracotta; public string lamp_gold; }
    [Serializable] class Blockout { public Anchor[] anchors; }
    [Serializable] class Anchor { public string id; public float[] position_m; public float[] footprint_m; public float height_m; public float yaw_deg; }

    [MenuItem("VRMine/Worlds/Build Woodland Tabletop Village")]
    public static void Build()
    {
        WorldSpec spec = LoadSpec();
        Validate(spec);
        EnsureFolder("Assets/KafkaMade/VRMine/Scenes");
        EnsureFolder(MaterialFolder);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Transform root = Empty("WoodlandTabletopVillage", null);

        Material wood = Mat("Wood", Html(spec.atmosphere.palette.wood_brown), 0.82f);
        Material forest = Mat("Forest", Html(spec.atmosphere.palette.forest_green), 0.90f);
        Material cream = Mat("Cream", Html(spec.atmosphere.palette.warm_cream), 0.88f);
        Material roof = Mat("Terracotta", Html(spec.atmosphere.palette.accent_terracotta), 0.84f);
        Material lamp = Mat("LampGold", Html(spec.atmosphere.palette.lamp_gold), 0.72f, true);
        Material stone = Mat("Stone", new Color(0.34f, 0.34f, 0.31f), 0.92f);
        Material water = Mat("Water", new Color(0.20f, 0.36f, 0.39f), 0.78f);
        Material wall = Mat("WarmWall", new Color(0.54f, 0.45f, 0.35f), 0.92f);
        Material forestLight = Mat("ForestLight", new Color(0.28f, 0.40f, 0.25f), 0.96f);
        Material moss = Mat("Moss", new Color(0.20f, 0.30f, 0.18f), 0.98f);
        Material path = Mat("Path", new Color(0.38f, 0.29f, 0.19f), 0.98f);
        Material paper = Mat("Paper", new Color(0.78f, 0.68f, 0.48f), 0.98f);

        Room(root, spec, wall, wood);
        Tabletop(root, spec, wood, forest, forestLight, cream, roof, lamp, stone, water, moss, path, paper);
        Seats(root, spec, wood, cream);
        ReadingNook(root, spec, wood, cream, lamp);
        Lighting(root);
        Descriptor(root, spec);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddScene(ScenePath);
        AssetDatabase.SaveAssets();
    }

    public static void BuildBatch()
    {
        Build();
        WoodlandTabletopVillageVerification.VerifyOrThrow();
    }

    static WorldSpec LoadSpec()
    {
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", SpecPath));
        if (!File.Exists(path)) throw new FileNotFoundException("Canonical woodland spec not found", path);
        WorldSpec spec = JsonUtility.FromJson<WorldSpec>(File.ReadAllText(path));
        if (spec == null) throw new InvalidDataException("Could not parse canonical woodland spec");
        return spec;
    }

    static void Validate(WorldSpec s)
    {
        if (s.spatial_geometry == null || s.world_build == null || s.atmosphere == null || s.blockout == null || s.blockout.anchors == null)
            throw new InvalidDataException("Woodland spec lacks physical sections");
        if (s.runtime_budget == null || s.runtime_budget.realtime_light_count != 0)
            throw new InvalidDataException("Woodland runtime budget requires zero realtime lights");
        if (s.blockout.anchors.Length != 8) throw new InvalidDataException("Woodland v0 requires exactly eight blockout anchors");
    }

    static void Room(Transform parent, WorldSpec s, Material wall, Material floor)
    {
        float w = s.spatial_geometry.overall_width_m, d = s.spatial_geometry.overall_depth_m, h = s.world_build.ceiling_height_m, t = 0.08f;
        Transform r = Empty("ObservationRoom", parent);
        Cube("Floor", new Vector3(0, -t / 2, 0), new Vector3(w, t, d), floor, r);
        Cube("Ceiling", new Vector3(0, h + t / 2, 0), new Vector3(w, t, d), wall, r);
        Cube("WallLeft", new Vector3(-w / 2 - t / 2, h / 2, 0), new Vector3(t, h, d), wall, r);
        Cube("WallRight", new Vector3(w / 2 + t / 2, h / 2, 0), new Vector3(t, h, d), wall, r);
        Cube("WallFront", new Vector3(0, h / 2, -d / 2 - t / 2), new Vector3(w, h, t), wall, r);
        Cube("WallBack", new Vector3(0, h / 2, d / 2 + t / 2), new Vector3(w, h, t), wall, r);
    }

    static void Tabletop(Transform parent, WorldSpec s, Material wood, Material forest, Material forestLight, Material cream, Material roof, Material lamp, Material stone, Material water, Material moss, Material path, Material paper)
    {
        Zone z = s.spatial_geometry.zones.FirstOrDefault(x => x.zone_id == "woodland_tabletop");
        if (z == null) throw new InvalidDataException("woodland_tabletop zone is required");
        Transform table = Empty("WoodlandTabletop", parent);
        float centerY = z.tabletop_height_m - z.tabletop_thickness_m / 2;
        Cylinder("DioramaTableTop", new Vector3(0, centerY, 0), z.diameter_m, z.tabletop_thickness_m, wood, table);
        Cylinder("DioramaPedestal", new Vector3(0, centerY * 0.48f, 0), 0.72f, centerY * 0.92f, wood, table);
        Transform polish = Empty("VisualPolish", table);
        GroundDetails(polish, z.tabletop_height_m, stone, moss, path);
        Transform village = Empty("VillageBlockout", table);
        foreach (Anchor a in s.blockout.anchors)
        {
            Vector3 p = Pos(a.position_m);
            switch (a.id)
            {
                case "great_tree": GreatTree(village, a, p, wood, forest, forestLight, moss, stone, lamp); break;
                case "bridge": Bridge(village, a, p, wood, lamp); break;
                case "lodge": House(village, a, p, wood, cream, roof, lamp, stone, moss, paper, true); break;
                case "cottage_a": case "cottage_b": House(village, a, p, wood, cream, roof, lamp, stone, moss, paper, false); break;
                case "market_stall": Market(village, a, p, wood, roof, lamp, stone, paper); break;
                case "reading_bench": MiniBench(village, a, p, wood); break;
                case "stream": Stream(village, a, p, stone, water, moss); break;
                default: Proxy(village, a, p, cream); break;
            }
        }
        PhotoSpots(polish, z.tabletop_height_m, stone, moss, lamp);
    }

    static void GroundDetails(Transform parent, float tabletopHeight, Material stone, Material moss, Material path)
    {
        Transform pathRoot = Empty("WornPath", parent);
        for (int i = 0; i < 9; i++)
        {
            float t = i / 8f;
            Vector3 p = new Vector3(Mathf.Sin(t * Mathf.PI * 1.25f) * 0.15f, tabletopHeight + 0.048f, -0.84f + t * 0.98f);
            GameObject step = Cube("PathStone_" + i, p, new Vector3(0.13f, 0.035f, 0.085f), path, pathRoot);
            step.transform.localRotation = Quaternion.Euler(0, (i % 2 == 0 ? -8f : 12f), 0);
        }
        for (int i = 0; i < 4; i++)
        {
            float t = i / 3f;
            Vector3 p = Vector3.Lerp(new Vector3(0.10f, tabletopHeight + 0.05f, -0.10f), new Vector3(0.60f, tabletopHeight + 0.05f, -0.43f), t);
            GameObject step = Cube("MarketBranchStone_" + i, p, new Vector3(0.10f, 0.03f, 0.07f), path, pathRoot);
            step.transform.localRotation = Quaternion.Euler(0, 18f, 0);
        }
        for (int i = 0; i < 4; i++)
        {
            float t = i / 3f;
            Vector3 p = Vector3.Lerp(new Vector3(-0.10f, tabletopHeight + 0.05f, -0.10f), new Vector3(-0.58f, tabletopHeight + 0.05f, -0.39f), t);
            GameObject step = Cube("BenchBranchStone_" + i, p, new Vector3(0.10f, 0.03f, 0.07f), path, pathRoot);
            step.transform.localRotation = Quaternion.Euler(0, -18f, 0);
        }

        Transform mossRoot = Empty("MossGround", parent);
        for (int i = 0; i < 12; i++)
        {
            float angle = Mathf.PI * 2f * i / 12f;
            float radius = 0.86f + (i % 3) * 0.035f;
            Sphere("MossPatch_" + i,
                new Vector3(Mathf.Cos(angle) * radius, tabletopHeight + 0.02f, Mathf.Sin(angle) * radius),
                new Vector3(0.16f, 0.035f, 0.10f), moss, mossRoot);
        }
    }

    static void PhotoSpots(Transform parent, float tabletopHeight, Material stone, Material moss, Material lamp)
    {
        PhotoSpot(parent, "PhotoSpot_HeroTree", new Vector3(0f, tabletopHeight + 0.03f, -0.02f), 0.34f, stone, moss);
        PhotoSpot(parent, "PhotoSpot_Bridge", new Vector3(0f, tabletopHeight + 0.03f, -1.08f), 0.24f, stone, moss);
        PhotoSpot(parent, "PhotoSpot_Market", new Vector3(0.93f, tabletopHeight + 0.03f, -0.48f), 0.20f, stone, moss);
        FireflyCluster(parent, tabletopHeight, lamp);
    }

    static void FireflyCluster(Transform parent, float tabletopHeight, Material lamp)
    {
        Transform fireflies = Empty("Fireflies", parent);
        Vector3[] positions =
        {
            new Vector3(-0.33f, tabletopHeight + 0.42f, 0.05f),
            new Vector3(0.27f, tabletopHeight + 0.52f, 0.12f),
            new Vector3(0.44f, tabletopHeight + 0.30f, -0.25f),
            new Vector3(-0.45f, tabletopHeight + 0.28f, -0.38f),
            new Vector3(0.16f, tabletopHeight + 0.24f, 0.58f),
            new Vector3(-0.18f, tabletopHeight + 0.20f, -0.66f)
        };
        for (int i = 0; i < positions.Length; i++)
            Sphere("Firefly_" + i, positions[i], Vector3.one * 0.025f, lamp, fireflies);
    }

    static void PhotoSpot(Transform parent, string name, Vector3 center, float radius, Material stone, Material moss)
    {
        Transform spot = Empty(name, parent);
        spot.position = center;
        Cylinder("SoftMossRing", Vector3.zero, radius * 1.65f, 0.018f, moss, spot);
        for (int i = 0; i < 4; i++)
        {
            float angle = Mathf.PI * 0.5f * i + Mathf.PI * 0.25f;
            Cylinder("FrameStone_" + i, new Vector3(Mathf.Cos(angle) * radius, 0.025f, Mathf.Sin(angle) * radius), 0.06f, 0.035f, stone, spot);
        }
    }

    static void Seats(Transform parent, WorldSpec s, Material wood, Material cream)
    {
        Transform group = Empty("SocialSeating", parent);
        Vector3 c = Pos(s.world_build.social_core.center_m);
        int count = s.world_build.social_core.seat_count;
        float radius = s.world_build.social_core.seat_radius_m;
        for (int i = 0; i < count; i++)
        {
            float a = Mathf.PI * 2 * i / count;
            Transform seat = Empty("SocialSeat_" + (i + 1), group);
            seat.position = new Vector3(c.x + Mathf.Cos(a) * radius, 0, c.z + Mathf.Sin(a) * radius);
            Face(seat, new Vector3(c.x, 0, c.z));
            Cylinder("Base", new Vector3(0, 0.22f, 0), 0.42f, 0.44f, wood, seat);
            Cylinder("Top", new Vector3(0, 0.46f, 0), 0.48f, 0.08f, cream, seat);
        }
    }

    static void ReadingNook(Transform parent, WorldSpec s, Material wood, Material cream, Material lamp)
    {
        Transform r = Empty("ReadingNook", parent);
        r.position = Pos(s.world_build.retreat.center_m);
        r.rotation = Quaternion.Euler(0, 135, 0);
        Cube("ReadingBenchSeat", new Vector3(0, 0.34f, 0), new Vector3(1.05f, 0.12f, 0.42f), wood, r);
        Cube("ReadingBenchBack", new Vector3(0, 0.68f, 0.18f), new Vector3(1.05f, 0.62f, 0.10f), wood, r);
        Cube("ReadingCushion", new Vector3(0, 0.43f, -0.02f), new Vector3(0.92f, 0.07f, 0.34f), cream, r);
        Cylinder("NookSideTable", new Vector3(-0.68f, 0.26f, 0.02f), 0.22f, 0.52f, wood, r);
        Sphere("NookLamp", new Vector3(-0.68f, 0.60f, 0.02f), Vector3.one * 0.10f, lamp, r);
    }

    static Transform AnchorRoot(Transform parent, Anchor a, Vector3 p)
    {
        Transform t = Empty(a.id, parent); t.position = p; t.rotation = Quaternion.Euler(0, a.yaw_deg, 0); return t;
    }

    static void GreatTree(Transform parent, Anchor a, Vector3 p, Material wood, Material forest, Material forestLight, Material moss, Material stone, Material lamp)
    {
        Transform r = AnchorRoot(parent, a, p);
        Sphere("MossBase", Vector3.up * 0.018f, new Vector3(a.footprint_m[0] * 0.92f, 0.05f, a.footprint_m[1] * 0.92f), moss, r);
        Cylinder("Trunk", Vector3.up * a.height_m * 0.31f, a.footprint_m[0] * 0.24f, a.height_m * 0.62f, wood, r);
        for (int i = 0; i < 4; i++)
        {
            float angle = Mathf.PI * 0.5f * i;
            GameObject root = Cube("Root_" + i,
                new Vector3(Mathf.Cos(angle) * a.footprint_m[0] * 0.23f, 0.075f, Mathf.Sin(angle) * a.footprint_m[1] * 0.23f),
                new Vector3(a.footprint_m[0] * 0.48f, 0.08f, 0.12f), wood, r);
            root.transform.localRotation = Quaternion.Euler(0, angle * Mathf.Rad2Deg, 0);
        }
        Sphere("CanopyLow", Vector3.up * a.height_m * 0.67f, new Vector3(a.footprint_m[0] * 0.90f, a.height_m * 0.38f, a.footprint_m[1] * 0.90f), forest, r);
        Sphere("CanopyTop", new Vector3(-0.06f, a.height_m * 0.86f, 0.03f), new Vector3(a.footprint_m[0] * 0.62f, a.height_m * 0.30f, a.footprint_m[1] * 0.62f), forest, r);
        Sphere("CanopySideA", new Vector3(-0.23f, a.height_m * 0.66f, 0.05f), new Vector3(0.31f, 0.22f, 0.30f), forestLight, r);
        Sphere("CanopySideB", new Vector3(0.22f, a.height_m * 0.72f, -0.04f), new Vector3(0.26f, 0.20f, 0.25f), forestLight, r);
        Cube("ShrineBase", new Vector3(0, 0.07f, -a.footprint_m[1] * 0.40f), new Vector3(0.20f, 0.09f, 0.15f), stone, r);
        Sphere("ShrineLantern", new Vector3(0, 0.22f, -a.footprint_m[1] * 0.40f), Vector3.one * 0.07f, lamp, r);
    }

    static void House(Transform parent, Anchor a, Vector3 p, Material wood, Material cream, Material roof, Material lamp, Material stone, Material moss, Material paper, bool lodge)
    {
        Transform r = AnchorRoot(parent, a, p); float w = a.footprint_m[0], d = a.footprint_m[1], bh = a.height_m * 0.58f;
        Cube("Body", Vector3.up * bh / 2, new Vector3(w, bh, d), cream, r);
        GameObject ra = Cube("RoofA", new Vector3(-w * 0.18f, bh + a.height_m * 0.10f, 0), new Vector3(w * 0.62f, a.height_m * 0.12f, d * 1.14f), roof, r);
        GameObject rb = Cube("RoofB", new Vector3(w * 0.18f, bh + a.height_m * 0.10f, 0), new Vector3(w * 0.62f, a.height_m * 0.12f, d * 1.14f), roof, r);
        ra.transform.localRotation = Quaternion.Euler(0, 0, 28); rb.transform.localRotation = Quaternion.Euler(0, 0, -28);
        Cube("Door", new Vector3(0, bh * 0.32f, -d * 0.51f), new Vector3(w * 0.20f, bh * 0.52f, 0.025f), wood, r);
        Cube("WarmWindow", new Vector3(w * 0.27f, bh * 0.55f, -d * 0.515f), new Vector3(w * 0.18f, bh * 0.22f, 0.02f), lamp, r);
        Cube("WindowShutter", new Vector3(w * 0.08f, bh * 0.55f, -d * 0.525f), new Vector3(w * 0.05f, bh * 0.26f, 0.03f), wood, r);
        Cube("Doorstep", new Vector3(0, 0.035f, -d * 0.56f), new Vector3(w * 0.30f, 0.06f, 0.11f), stone, r);
        Cube("FlowerBox", new Vector3(-w * 0.24f, bh * 0.35f, -d * 0.54f), new Vector3(w * 0.22f, 0.05f, 0.07f), wood, r);
        Sphere("Planter", new Vector3(-w * 0.24f, bh * 0.43f, -d * 0.55f), new Vector3(w * 0.14f, 0.07f, 0.06f), moss, r);
        Sphere("DoorLantern", new Vector3(-w * 0.16f, bh * 0.62f, -d * 0.55f), Vector3.one * Mathf.Max(0.035f, w * 0.07f), lamp, r);
        Cube("DeliveryCrate", new Vector3(w * 0.35f, 0.10f, -d * 0.45f), new Vector3(w * 0.18f, 0.16f, d * 0.16f), wood, r);
        if (lodge)
        {
            Cylinder("Chimney", new Vector3(w * 0.28f, a.height_m * 0.78f, 0), w * 0.09f, a.height_m * 0.36f, wood, r);
            GameObject firewood = Cylinder("StackedFirewood", new Vector3(-w * 0.36f, 0.12f, d * 0.38f), w * 0.14f, w * 0.44f, wood, r);
            firewood.transform.localRotation = Quaternion.Euler(0, 0, 90);
        }
        Cube("VillageSign", new Vector3(0, bh * 0.83f, -d * 0.56f), new Vector3(w * 0.34f, 0.08f, 0.025f), paper, r);
    }

    static void Bridge(Transform parent, Anchor a, Vector3 p, Material wood, Material lamp)
    {
        Transform r = AnchorRoot(parent, a, p);
        Cube("Deck", Vector3.up * a.height_m * 0.40f, new Vector3(a.footprint_m[0], a.height_m * 0.20f, a.footprint_m[1]), wood, r);
        Cube("RailLeft", new Vector3(-a.footprint_m[0] * 0.42f, a.height_m * 0.85f, 0), new Vector3(0.025f, a.height_m, a.footprint_m[1]), wood, r);
        Cube("RailRight", new Vector3(a.footprint_m[0] * 0.42f, a.height_m * 0.85f, 0), new Vector3(0.025f, a.height_m, a.footprint_m[1]), wood, r);
        for (int i = -1; i <= 1; i++)
            Cube("DeckPlank_" + i, new Vector3(0, a.height_m * 0.54f, i * a.footprint_m[1] * 0.28f), new Vector3(a.footprint_m[0] * 0.88f, 0.025f, 0.025f), wood, r);
        for (int side = -1; side <= 1; side += 2)
        {
            Cylinder("LanternPost_" + side, new Vector3(side * a.footprint_m[0] * 0.36f, a.height_m * 0.90f, 0), 0.025f, a.height_m * 0.80f, wood, r);
            Sphere("BridgeLantern_" + side, new Vector3(side * a.footprint_m[0] * 0.36f, a.height_m * 1.30f, 0), Vector3.one * 0.06f, lamp, r);
        }
    }

    static void Market(Transform parent, Anchor a, Vector3 p, Material wood, Material canopy, Material lamp, Material stone, Material paper)
    {
        Transform r = AnchorRoot(parent, a, p); float w = a.footprint_m[0], d = a.footprint_m[1];
        Cube("Counter", new Vector3(0, a.height_m * 0.34f, 0), new Vector3(w, a.height_m * 0.12f, d), wood, r);
        for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
            Cube("Post_" + x + "_" + z, new Vector3(x * w * 0.43f, a.height_m * 0.57f, z * d * 0.42f), new Vector3(0.025f, a.height_m * 0.76f, 0.025f), wood, r);
        Cube("Canopy", Vector3.up * a.height_m * 0.91f, new Vector3(w * 1.08f, a.height_m * 0.08f, d * 1.10f), canopy, r);
        Sphere("Lantern", new Vector3(0, a.height_m * 0.76f, -d * 0.46f), Vector3.one * a.height_m * 0.12f, lamp, r);
        Cube("MarketSign", new Vector3(0, a.height_m * 0.68f, -d * 0.52f), new Vector3(w * 0.52f, a.height_m * 0.18f, 0.025f), paper, r);
        Cube("Crate", new Vector3(-w * 0.27f, a.height_m * 0.17f, -d * 0.57f), new Vector3(w * 0.26f, a.height_m * 0.25f, d * 0.28f), wood, r);
        Cylinder("MarketStone", new Vector3(w * 0.29f, 0.035f, d * 0.52f), 0.07f, 0.06f, stone, r);
    }

    static void MiniBench(Transform parent, Anchor a, Vector3 p, Material wood)
    {
        Transform r = AnchorRoot(parent, a, p);
        Cube("Seat", Vector3.up * a.height_m * 0.38f, new Vector3(a.footprint_m[0], a.height_m * 0.18f, a.footprint_m[1]), wood, r);
        Cube("Back", new Vector3(0, a.height_m * 0.72f, a.footprint_m[1] * 0.38f), new Vector3(a.footprint_m[0], a.height_m * 0.54f, 0.025f), wood, r);
    }

    static void Stream(Transform parent, Anchor a, Vector3 p, Material stone, Material water, Material moss)
    {
        Transform r = AnchorRoot(parent, a, p);
        Cube("Water", Vector3.up * 0.006f, new Vector3(a.footprint_m[0], 0.012f, a.footprint_m[1]), water, r);
        Cube("BankNear", new Vector3(0, 0.015f, -a.footprint_m[1] * 0.58f), new Vector3(a.footprint_m[0], 0.03f, 0.035f), stone, r);
        Cube("BankFar", new Vector3(0, 0.015f, a.footprint_m[1] * 0.58f), new Vector3(a.footprint_m[0], 0.03f, 0.035f), stone, r);
        Sphere("StreamMoss", new Vector3(-a.footprint_m[0] * 0.25f, 0.035f, 0), new Vector3(0.22f, 0.04f, 0.08f), moss, r);
    }

    static void Proxy(Transform parent, Anchor a, Vector3 p, Material m)
    {
        Transform r = AnchorRoot(parent, a, p); Cube("Proxy", Vector3.up * a.height_m / 2, new Vector3(a.footprint_m[0], a.height_m, a.footprint_m[1]), m, r);
    }

    static void Lighting(Transform parent)
    {
        GameObject go = new GameObject("BakedSun"); go.transform.SetParent(parent, false); go.transform.rotation = Quaternion.Euler(38, -32, 0);
        Light l = go.AddComponent<Light>(); l.type = LightType.Directional; l.color = new Color(1, 0.78f, 0.58f); l.intensity = 1.0f; l.shadows = LightShadows.Soft; l.lightmapBakeType = LightmapBakeType.Baked;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.28f, 0.34f, 0.30f); RenderSettings.ambientEquatorColor = new Color(0.16f, 0.19f, 0.16f); RenderSettings.ambientGroundColor = new Color(0.08f, 0.07f, 0.055f);
        RenderSettings.ambientIntensity = 1.0f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.08f, 0.13f, 0.11f);
        RenderSettings.fogDensity = 0.022f;
    }

    static void Descriptor(Transform parent, WorldSpec s)
    {
        GameObject world = new GameObject("VRCWorld"); world.transform.SetParent(parent, false); VRCSceneDescriptor d = world.AddComponent<VRCSceneDescriptor>(); world.AddComponent<PipelineManager>();
        Transform spawn = Empty("SpawnPoint", parent); spawn.position = Pos(s.world_build.spawn.position_m) + Vector3.up * 0.02f; spawn.rotation = Quaternion.Euler(0, s.world_build.spawn.facing_deg, 0); d.spawns = new[] { spawn };
        GameObject cameraGo = new GameObject("ReferenceCamera"); cameraGo.transform.SetParent(parent, false); cameraGo.transform.position = Pos(s.world_build.hero_view.position_m); Face(cameraGo.transform, Pos(s.world_build.hero_view.target_m));
        Camera c = cameraGo.AddComponent<Camera>(); c.enabled = false; c.fieldOfView = 58; c.nearClipPlane = Mathf.Max(0.01f, s.runtime_budget.camera_near_clip_m); d.ReferenceCamera = cameraGo;
    }

    static Vector3 Pos(float[] xyz) { if (xyz == null || xyz.Length != 3) throw new InvalidDataException("Expected xyz vector"); return new Vector3(xyz[0], xyz[2], xyz[1]); }
    static Transform Empty(string name, Transform parent) { Transform t = new GameObject(name).transform; if (parent != null) t.SetParent(parent, false); return t; }
    static GameObject Cube(string n, Vector3 p, Vector3 s, Material m, Transform parent) { GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube); Setup(g, n, p, s, m, parent); return g; }
    static GameObject Cylinder(string n, Vector3 p, float dia, float h, Material m, Transform parent) { GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Setup(g, n, p, new Vector3(dia, h / 2, dia), m, parent); return g; }
    static GameObject Sphere(string n, Vector3 p, Vector3 s, Material m, Transform parent) { GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere); Setup(g, n, p, s, m, parent); return g; }

    static void Setup(GameObject g, string n, Vector3 p, Vector3 s, Material m, Transform parent)
    {
        g.name = n; g.transform.SetParent(parent, false); g.transform.localPosition = p; g.transform.localScale = s; g.GetComponent<Renderer>().sharedMaterial = m;
        GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
    }

    static Material Mat(string name, Color color, float roughness, bool emission = false)
    {
        string path = MaterialFolder + "/" + name + ".mat"; Material m = AssetDatabase.LoadAssetAtPath<Material>(path); Shader shader = Shader.Find("VRChat/Mobile/Standard Lite") ?? Shader.Find("Standard");
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); } else if (m.shader != shader) m.shader = shader;
        m.color = color; if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 1 - roughness);
        if (emission && m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * 1.85f); }
        EditorUtility.SetDirty(m); return m;
    }

    static Color Html(string value) { Color c; if (!ColorUtility.TryParseHtmlString(value, out c)) throw new InvalidDataException("Invalid palette color: " + value); return c; }
    static void Face(Transform t, Vector3 target) { Vector3 v = target - t.position; v.y = 0; if (v.sqrMagnitude > 0.0001f) t.rotation = Quaternion.LookRotation(v.normalized, Vector3.up); }
    static void AddScene(string scenePath) { EditorBuildSettingsScene[] s = EditorBuildSettings.scenes; if (!s.Any(x => x.path == scenePath)) EditorBuildSettings.scenes = s.Concat(new[] { new EditorBuildSettingsScene(scenePath, true) }).ToArray(); }
    static void EnsureFolder(string folder) { string[] p = folder.Split('/'); string cur = p[0]; for (int i = 1; i < p.Length; i++) { string next = cur + "/" + p[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, p[i]); cur = next; } }
}
