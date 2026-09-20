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
    const string TextureFolder = "Assets/KafkaMade/VRMine/Textures/WoodlandTabletopVillage";

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
        Material mossWood = Mat("MossWoodAccent", new Color(0.52f, 0.42f, 0.26f), 0.92f);
        ApplySurface(wood, "WoodTable/WoodTable_Diffuse_2K.jpg", "WoodTable/WoodTable_Normal_2K.jpg", "WoodTable/WoodTable_AO_2K.jpg", new Vector2(0.72f, 0.72f), new Color(0.90f, 0.76f, 0.58f), 0.26f);
        ApplySurface(stone, "MossyRock/MossyRock_Diffuse_2K.jpg", "MossyRock/MossyRock_Normal_2K.jpg", "MossyRock/MossyRock_AO_2K.jpg", new Vector2(0.68f, 0.68f), new Color(0.70f, 0.72f, 0.66f), 0.28f);
        ApplySurface(path, "ForestGround/ForestGround_Diffuse_2K.jpg", "ForestGround/ForestGround_Normal_2K.jpg", "ForestGround/ForestGround_AO_2K.jpg", new Vector2(0.56f, 0.56f), new Color(0.76f, 0.62f, 0.44f), 0.24f);
        ApplySurface(mossWood, "MossWood/MossWood_Diffuse_2K.jpg", "MossWood/MossWood_Normal_2K.jpg", "MossWood/MossWood_AO_2K.jpg", new Vector2(0.72f, 0.72f), new Color(0.78f, 0.68f, 0.50f), 0.30f);
        SoftEmission(wood, 0.26f);
        SoftEmission(forest, 0.50f);
        SoftEmission(forestLight, 0.66f);
        SoftEmission(cream, 0.34f);
        SoftEmission(roof, 0.40f);
        SoftEmission(stone, 0.15f);
        SoftEmission(water, 0.34f);
        SoftEmission(moss, 0.42f);
        SoftEmission(path, 0.12f);
        SoftEmission(paper, 0.28f);
        SoftEmission(mossWood, 0.10f);

        Room(root, spec, wall, wood);
        ForestBoundary(root, spec, wood, forest, forestLight, moss);
        Tabletop(root, spec, wood, mossWood, forest, forestLight, cream, roof, lamp, stone, water, moss, path, paper);
        Seats(root, spec, wood, cream);
        ReadingNook(root, spec, wood, cream, lamp);
        Lighting(root);
        Descriptor(root, spec);
        EditorSceneManager.SaveScene(scene, ScenePath);
        ConfigureBakedLighting();
        Lightmapping.Clear();
        if (!Lightmapping.Bake()) throw new InvalidOperationException("Woodland Tabletop Village lighting bake failed");
        EditorSceneManager.SaveScene(scene);
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
        HideRenderer(Cube("Ceiling", new Vector3(0, h + t / 2, 0), new Vector3(w, t, d), wall, r));
        HideRenderer(Cube("WallLeft", new Vector3(-w / 2 - t / 2, h / 2, 0), new Vector3(t, h, d), wall, r));
        HideRenderer(Cube("WallRight", new Vector3(w / 2 + t / 2, h / 2, 0), new Vector3(t, h, d), wall, r));
        HideRenderer(Cube("WallFront", new Vector3(0, h / 2, -d / 2 - t / 2), new Vector3(w, h, t), wall, r));
        HideRenderer(Cube("WallBack", new Vector3(0, h / 2, d / 2 + t / 2), new Vector3(w, h, t), wall, r));
    }

    static void ForestBoundary(Transform parent, WorldSpec s, Material wood, Material forest, Material forestLight, Material moss)
    {
        float w = s.spatial_geometry.overall_width_m;
        float d = s.spatial_geometry.overall_depth_m;
        Transform boundary = Empty("ForestBoundary", parent);

        int index = 0;
        for (int i = -2; i <= 2; i++)
        {
            BackdropTree(boundary, "BackTree_" + index++, new Vector3(i * 1.18f, 0, d * 0.42f), 1.18f + (Mathf.Abs(i) % 2) * 0.12f, wood, forest, forestLight);
        }
        for (int i = -1; i <= 1; i++)
        {
            BackdropTree(boundary, "LeftTree_" + index++, new Vector3(-w * 0.43f, 0, i * 1.42f), 1.08f + (i == 0 ? 0.12f : 0f), wood, forest, forestLight);
            BackdropTree(boundary, "RightTree_" + index++, new Vector3(w * 0.43f, 0, i * 1.42f), 1.04f + (i == 0 ? 0.10f : 0f), wood, forest, forestLight);
        }

        Transform understory = Empty("Understory", boundary);
        for (int i = 0; i < 14; i++)
        {
            float angle = Mathf.PI * 2f * i / 14f;
            float radius = Mathf.Min(w, d) * 0.43f;
            VisualSphere("Bush_" + i,
                new Vector3(Mathf.Cos(angle) * radius, 0.34f + (i % 3) * 0.05f, Mathf.Sin(angle) * radius),
                new Vector3(0.38f + (i % 2) * 0.08f, 0.28f, 0.34f + (i % 3) * 0.05f),
                i % 3 == 0 ? forestLight : forest, understory);
            VisualSphere("Moss_" + i,
                new Vector3(Mathf.Cos(angle) * (radius - 0.08f), 0.035f, Mathf.Sin(angle) * (radius - 0.08f)),
                new Vector3(0.25f, 0.045f, 0.16f), moss, understory);
        }
    }

    static void BackdropTree(Transform parent, string name, Vector3 position, float scale, Material wood, Material forest, Material forestLight)
    {
        Transform tree = Empty(name, parent);
        tree.localPosition = position;
        VisualCylinder("Trunk", new Vector3(0, scale * 0.44f, 0), scale * 0.18f, scale * 0.88f, wood, tree);
        VisualSphere("CanopyLow", new Vector3(-scale * 0.08f, scale * 0.92f, 0), new Vector3(scale * 0.72f, scale * 0.55f, scale * 0.72f), forest, tree);
        VisualSphere("CanopyHigh", new Vector3(scale * 0.14f, scale * 1.28f, scale * 0.02f), new Vector3(scale * 0.52f, scale * 0.38f, scale * 0.55f), forestLight, tree);
    }

    static void Tabletop(Transform parent, WorldSpec s, Material wood, Material mossWood, Material forest, Material forestLight, Material cream, Material roof, Material lamp, Material stone, Material water, Material moss, Material path, Material paper)
    {
        Zone z = s.spatial_geometry.zones.FirstOrDefault(x => x.zone_id == "woodland_tabletop");
        if (z == null) throw new InvalidDataException("woodland_tabletop zone is required");
        Transform table = Empty("WoodlandTabletop", parent);
        float centerY = z.tabletop_height_m - z.tabletop_thickness_m / 2;
        Cylinder("DioramaTableTop", new Vector3(0, centerY, 0), z.diameter_m, z.tabletop_thickness_m, wood, table);
        Cylinder("DioramaPedestal", new Vector3(0, centerY * 0.48f, 0), 0.72f, centerY * 0.92f, wood, table);
        Transform polish = Empty("VisualPolish", table);
        GroundDetails(polish, z.tabletop_height_m, stone, moss, path);
        WoodlandAccents(polish, z.tabletop_height_m, wood, mossWood, forest, forestLight, cream, roof, stone, moss, lamp, paper, path, water);
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

    static void WoodlandAccents(Transform parent, float tabletopHeight, Material wood, Material mossWood, Material forest, Material forestLight, Material cream, Material roof, Material stone, Material moss, Material lamp, Material paper, Material path, Material water)
    {
        Transform accents = Empty("WoodlandAccents", parent);

        FernCluster(accents, "Fern_LeftRear", new Vector3(-0.96f, tabletopHeight + 0.025f, 0.58f), 0.95f, forestLight);
        FernCluster(accents, "Fern_RightRear", new Vector3(0.94f, tabletopHeight + 0.025f, 0.70f), 0.86f, forest);
        FernCluster(accents, "Fern_LeftFront", new Vector3(-0.98f, tabletopHeight + 0.025f, -0.62f), 0.74f, forest);
        FernCluster(accents, "Fern_RightFront", new Vector3(0.92f, tabletopHeight + 0.025f, -0.76f), 0.68f, forestLight);

        MushroomCluster(accents, "MushroomPatch_A", new Vector3(-0.38f, tabletopHeight + 0.02f, 0.38f), 0.90f, roof, cream);
        MushroomCluster(accents, "MushroomPatch_B", new Vector3(0.46f, tabletopHeight + 0.02f, 0.88f), 0.72f, roof, cream);
        MushroomCluster(accents, "MushroomPatch_C", new Vector3(0.96f, tabletopHeight + 0.02f, -0.18f), 0.60f, roof, cream);

        Transform reeds = Empty("StreamReeds", accents);
        float[] reedX = { -0.62f, -0.26f, 0.28f, 0.62f };
        for (int i = 0; i < reedX.Length; i++)
        {
            Vector3 near = new Vector3(reedX[i], tabletopHeight + 0.025f, 0.59f + (i % 2) * 0.03f);
            Vector3 far = new Vector3(reedX[i] + 0.04f, tabletopHeight + 0.025f, 0.86f - (i % 2) * 0.03f);
            ReedCluster(reeds, "ReedNear_" + i, near, forestLight);
            ReedCluster(reeds, "ReedFar_" + i, far, forest);
        }

        Transform fallenLog = Empty("FallenLog", accents);
        fallenLog.position = new Vector3(-0.64f, tabletopHeight + 0.105f, 0.96f);
         GameObject log = VisualCylinder("Log", Vector3.zero, 0.14f, 0.56f, mossWood, fallenLog);
        log.transform.localRotation = Quaternion.Euler(0, 0, 90);
        VisualSphere("LogMoss", new Vector3(0.18f, 0.08f, 0), new Vector3(0.16f, 0.035f, 0.09f), moss, fallenLog);

        Transform stump = Empty("MossyStump", accents);
        stump.position = new Vector3(-0.92f, tabletopHeight + 0.075f, 0.16f);
         VisualCylinder("StumpWood", Vector3.zero, 0.18f, 0.15f, mossWood, stump);
        VisualSphere("StumpMoss", new Vector3(0, 0.09f, 0), new Vector3(0.10f, 0.025f, 0.10f), moss, stump);

        Transform flowers = Empty("WildflowerPatch", accents);
        FlowerStem(flowers, "Flower_A", new Vector3(0.96f, tabletopHeight + 0.04f, -0.34f), 0.11f, forestLight, lamp);
        FlowerStem(flowers, "Flower_B", new Vector3(1.02f, tabletopHeight + 0.04f, -0.44f), 0.09f, forest, roof);
        FlowerStem(flowers, "Flower_C", new Vector3(0.88f, tabletopHeight + 0.04f, -0.40f), 0.08f, forestLight, cream);

        VillageStoryTraces(accents, tabletopHeight, wood, cream, roof, paper, stone, lamp);
        EnvironmentReadability(accents, tabletopHeight, wood, paper, stone, moss, path, water, forestLight);
        ExhibitionPolish(accents, tabletopHeight, wood, stone, roof, cream, forestLight);
    }

    static void VillageStoryTraces(Transform parent, float tabletopHeight, Material wood, Material cream, Material roof, Material paper, Material stone, Material lamp)
    {
        Transform traces = Empty("VillageStoryTraces", parent);

        Transform book = Empty("OpenBook", traces);
        book.position = new Vector3(-0.70f, tabletopHeight + 0.18f, -0.40f);
        GameObject leftPage = VisualCube("LeftPage", new Vector3(-0.045f, 0.015f, 0), new Vector3(0.085f, 0.018f, 0.12f), paper, book);
        GameObject rightPage = VisualCube("RightPage", new Vector3(0.045f, 0.015f, 0), new Vector3(0.085f, 0.018f, 0.12f), paper, book);
        leftPage.transform.localRotation = Quaternion.Euler(0, 0, -6f);
        rightPage.transform.localRotation = Quaternion.Euler(0, 0, 6f);
        VisualCube("BookSpine", new Vector3(0, 0, 0), new Vector3(0.025f, 0.012f, 0.12f), wood, book);

        Transform mug = Empty("HalfFinishedMug", traces);
        mug.position = new Vector3(-0.42f, tabletopHeight + 0.16f, -0.04f);
        VisualCylinder("Cup", new Vector3(0, 0.035f, 0), 0.07f, 0.07f, cream, mug);
        VisualSphere("Tea", new Vector3(0, 0.073f, 0), new Vector3(0.045f, 0.008f, 0.045f), lamp, mug);

        Transform rack = Empty("DryingRack", traces);
        rack.position = new Vector3(0.96f, tabletopHeight + 0.08f, 0.22f);
        VisualCube("PostLeft", new Vector3(-0.14f, 0.15f, 0), new Vector3(0.025f, 0.30f, 0.025f), wood, rack);
        VisualCube("PostRight", new Vector3(0.14f, 0.15f, 0), new Vector3(0.025f, 0.30f, 0.025f), wood, rack);
        VisualCube("Rail", new Vector3(0, 0.27f, 0), new Vector3(0.31f, 0.025f, 0.025f), wood, rack);
        VisualCube("DryingCloth", new Vector3(0.02f, 0.16f, -0.01f), new Vector3(0.18f, 0.16f, 0.012f), paper, rack);

        Transform birdhouse = Empty("Birdhouse", traces);
        birdhouse.position = new Vector3(0.10f, tabletopHeight + 0.72f, 0.18f);
        VisualCube("Body", new Vector3(0, 0, 0), new Vector3(0.12f, 0.14f, 0.12f), wood, birdhouse);
        GameObject roofA = VisualCube("RoofA", new Vector3(-0.028f, 0.085f, 0), new Vector3(0.09f, 0.025f, 0.15f), roof, birdhouse);
        GameObject roofB = VisualCube("RoofB", new Vector3(0.028f, 0.085f, 0), new Vector3(0.09f, 0.025f, 0.15f), roof, birdhouse);
        roofA.transform.localRotation = Quaternion.Euler(0, 0, 28f);
        roofB.transform.localRotation = Quaternion.Euler(0, 0, -28f);
        VisualSphere("Entrance", new Vector3(0, 0.01f, -0.064f), Vector3.one * 0.022f, lamp, birdhouse);

        Transform toolbox = Empty("SmallToolbox", traces);
        toolbox.position = new Vector3(-0.42f, tabletopHeight + 0.095f, 0.44f);
        VisualCube("Box", new Vector3(0, 0.05f, 0), new Vector3(0.18f, 0.10f, 0.12f), wood, toolbox);
        VisualCube("Lid", new Vector3(0, 0.11f, 0), new Vector3(0.19f, 0.025f, 0.13f), roof, toolbox);
        VisualCube("Handle", new Vector3(0, 0.15f, 0), new Vector3(0.07f, 0.045f, 0.025f), wood, toolbox);

        Transform pebbles = Empty("StreamPebbles", traces);
        float[] pebbleX = { -0.68f, -0.42f, -0.10f, 0.22f, 0.52f, 0.74f };
        for (int i = 0; i < pebbleX.Length; i++)
        {
            float z = 0.59f + (i % 2) * 0.28f;
            VisualSphere("Pebble_" + i, new Vector3(pebbleX[i], tabletopHeight + 0.055f, z), new Vector3(0.075f, 0.045f, 0.055f), stone, pebbles);
        }
    }

    static void EnvironmentReadability(Transform parent, float tabletopHeight, Material wood, Material paper, Material stone, Material moss, Material path, Material water, Material forestLight)
    {
        Transform readability = Empty("EnvironmentReadability", parent);

        Transform sign = Empty("BridgeSignpost", readability);
        sign.position = new Vector3(-0.64f, tabletopHeight + 0.06f, -0.94f);
        VisualCylinder("Post", new Vector3(0, 0.12f, 0), 0.025f, 0.24f, wood, sign);
        VisualCube("Board", new Vector3(0, 0.27f, 0), new Vector3(0.18f, 0.075f, 0.020f), paper, sign);
        GameObject arrow = VisualCube("Arrow", new Vector3(0.02f, 0.27f, -0.014f), new Vector3(0.07f, 0.018f, 0.010f), path, sign);
        arrow.transform.localRotation = Quaternion.Euler(0, 0, -8f);

        Transform boulders = Empty("MossyBoulders", readability);
        Vector3[] boulderPositions =
        {
            new Vector3(-0.92f, tabletopHeight + 0.08f, 0.72f),
            new Vector3(0.88f, tabletopHeight + 0.07f, 0.72f),
            new Vector3(-0.76f, tabletopHeight + 0.07f, 0.88f),
            new Vector3(0.72f, tabletopHeight + 0.06f, 0.57f)
        };
        for (int i = 0; i < boulderPositions.Length; i++)
        {
            float scale = 0.12f + (i % 2) * 0.025f;
            VisualSphere("Boulder_" + i, boulderPositions[i], new Vector3(scale, scale * 0.72f, scale * 0.85f), stone, boulders);
            VisualSphere("BoulderMoss_" + i, boulderPositions[i] + Vector3.up * scale * 0.42f, new Vector3(scale * 0.55f, scale * 0.13f, scale * 0.48f), moss, boulders);
        }

        Transform leaves = Empty("LeafLitter", readability);
        Vector3[] leafPositions =
        {
            new Vector3(-0.34f, tabletopHeight + 0.055f, -0.96f),
            new Vector3(0.18f, tabletopHeight + 0.055f, -0.76f),
            new Vector3(0.42f, tabletopHeight + 0.055f, -0.92f),
            new Vector3(-0.56f, tabletopHeight + 0.055f, -0.62f),
            new Vector3(0.68f, tabletopHeight + 0.055f, -0.58f),
            new Vector3(-0.82f, tabletopHeight + 0.055f, -0.28f)
        };
        for (int i = 0; i < leafPositions.Length; i++)
        {
            GameObject leaf = VisualCube("Leaf_" + i, leafPositions[i], new Vector3(0.10f, 0.012f, 0.045f), i % 2 == 0 ? path : forestLight, leaves);
            leaf.transform.localRotation = Quaternion.Euler(0, -18f + i * 23f, 0);
        }

        Transform ripples = Empty("StreamRipples", readability);
        for (int i = 0; i < 3; i++)
        {
            GameObject ripple = VisualCube("Ripple_" + i, new Vector3(-0.42f + i * 0.42f, tabletopHeight + 0.021f, 0.72f), new Vector3(0.18f, 0.004f, 0.018f), water, ripples);
            ripple.transform.localRotation = Quaternion.Euler(0, i % 2 == 0 ? -8f : 8f, 0);
        }

        Transform pot = Empty("ClayPot", readability);
        pot.position = new Vector3(-0.92f, tabletopHeight + 0.04f, 0.18f);
        VisualCylinder("Pot", new Vector3(0, 0.04f, 0), 0.11f, 0.08f, path, pot);
        VisualSphere("Plant", new Vector3(0, 0.13f, 0), new Vector3(0.08f, 0.10f, 0.08f), forestLight, pot);
    }

    static void ExhibitionPolish(Transform parent, float tabletopHeight, Material wood, Material stone, Material roof, Material cream, Material forestLight)
    {
        Transform polish = Empty("ExhibitionPolish", parent);

        Transform plaza = Empty("PlazaStoneRing", polish);
        for (int i = 0; i < 8; i++)
        {
            float angle = Mathf.PI * 2f * i / 8f;
            VisualCylinder("PlazaStone_" + i,
                new Vector3(Mathf.Cos(angle) * 0.50f, tabletopHeight + 0.055f, Mathf.Sin(angle) * 0.50f),
                0.085f, 0.035f, stone, plaza);
        }

        Transform bunting = Empty("FestivalBunting", polish);
        for (int i = 0; i < 7; i++)
        {
            float t = i / 6f;
            Vector3 position = Vector3.Lerp(
                new Vector3(0.26f, tabletopHeight + 0.49f, -0.64f),
                new Vector3(1.08f, tabletopHeight + 0.49f, -0.64f), t);
            Material flagMaterial = i % 3 == 0 ? roof : (i % 3 == 1 ? cream : forestLight);
            GameObject flag = VisualCube("Pennant_" + i, position, new Vector3(0.065f, 0.075f, 0.012f), flagMaterial, bunting);
            flag.transform.localRotation = Quaternion.Euler(0, 0, i % 2 == 0 ? -10f : 10f);
        }
        VisualCube("BuntingCord", new Vector3(0.67f, tabletopHeight + 0.54f, -0.64f), new Vector3(0.42f, 0.012f, 0.012f), wood, bunting);

        Transform warmPatches = Empty("WarmSunPatches", polish);
        VisualSphere("SunPatch_A", new Vector3(-0.30f, tabletopHeight + 0.041f, -0.16f), new Vector3(0.18f, 0.012f, 0.11f), cream, warmPatches);
        VisualSphere("SunPatch_B", new Vector3(0.33f, tabletopHeight + 0.041f, -0.05f), new Vector3(0.14f, 0.012f, 0.09f), cream, warmPatches);
        VisualSphere("SunPatch_C", new Vector3(0.08f, tabletopHeight + 0.041f, 0.42f), new Vector3(0.12f, 0.012f, 0.08f), cream, warmPatches);
    }

    static void FernCluster(Transform parent, string name, Vector3 position, float scale, Material leaf)
    {
        Transform fern = Empty(name, parent);
        fern.position = position;
        for (int i = 0; i < 3; i++)
        {
            float offset = (i - 1) * 0.035f * scale;
            GameObject blade = VisualCube("Blade_" + i, new Vector3(offset, 0.09f * scale, 0), new Vector3(0.025f * scale, 0.18f * scale, 0.06f * scale), leaf, fern);
            blade.transform.localRotation = Quaternion.Euler(0, (i - 1) * 22f, (i - 1) * -18f);
        }
    }

    static void MushroomCluster(Transform parent, string name, Vector3 position, float scale, Material cap, Material stem)
    {
        Transform patch = Empty(name, parent);
        patch.position = position;
        Mushroom(patch, "Mushroom_0", new Vector3(-0.06f * scale, 0, 0), scale * 0.72f, cap, stem);
        Mushroom(patch, "Mushroom_1", new Vector3(0.08f * scale, 0, 0.04f * scale), scale * 0.46f, cap, stem);
        Mushroom(patch, "Mushroom_2", new Vector3(0.02f * scale, 0, -0.08f * scale), scale * 0.34f, cap, stem);
    }

    static void Mushroom(Transform parent, string name, Vector3 position, float scale, Material cap, Material stem)
    {
        Transform mushroom = Empty(name, parent);
        mushroom.localPosition = position;
        VisualCylinder("Stem", new Vector3(0, scale * 0.08f, 0), scale * 0.10f, scale * 0.16f, stem, mushroom);
        VisualSphere("Cap", new Vector3(0, scale * 0.19f, 0), new Vector3(scale * 0.25f, scale * 0.13f, scale * 0.25f), cap, mushroom);
    }

    static void ReedCluster(Transform parent, string name, Vector3 position, Material leaf)
    {
        Transform reeds = Empty(name, parent);
        reeds.position = position;
        for (int i = 0; i < 3; i++)
        {
            GameObject blade = VisualCube("Blade_" + i, new Vector3((i - 1) * 0.025f, 0.075f, 0), new Vector3(0.018f, 0.15f, 0.035f), leaf, reeds);
            blade.transform.localRotation = Quaternion.Euler(0, (i - 1) * 18f, (i - 1) * -13f);
        }
    }

    static void FlowerStem(Transform parent, string name, Vector3 position, float size, Material stem, Material flower)
    {
        Transform plant = Empty(name, parent);
        plant.position = position;
        VisualCylinder("Stem", new Vector3(0, size * 0.42f, 0), size * 0.035f, size * 0.84f, stem, plant);
        VisualSphere("Bloom", new Vector3(0, size * 0.88f, 0), Vector3.one * size * 0.16f, flower, plant);
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
        Cube("ReadingBenchBackRail", new Vector3(0, 0.62f, 0.18f), new Vector3(1.05f, 0.10f, 0.10f), wood, r);
        for (int i = -1; i <= 1; i++)
            Cube("ReadingBenchBackSlat_" + i, new Vector3(i * 0.34f, 0.77f, 0.18f), new Vector3(0.08f, 0.34f, 0.08f), wood, r);
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
        float windowY = bh * 0.55f;
        float windowW = w * 0.18f;
        float windowH = bh * 0.22f;
        Cube("WindowFrameTop", new Vector3(w * 0.27f, windowY + windowH * 0.60f, -d * 0.53f), new Vector3(windowW + 0.035f, 0.025f, 0.025f), wood, r);
        Cube("WindowFrameBottom", new Vector3(w * 0.27f, windowY - windowH * 0.60f, -d * 0.53f), new Vector3(windowW + 0.035f, 0.025f, 0.025f), wood, r);
        Cube("WindowFrameLeft", new Vector3(w * 0.27f - windowW * 0.60f, windowY, -d * 0.53f), new Vector3(0.025f, windowH + 0.035f, 0.025f), wood, r);
        Cube("WindowFrameRight", new Vector3(w * 0.27f + windowW * 0.60f, windowY, -d * 0.53f), new Vector3(0.025f, windowH + 0.035f, 0.025f), wood, r);
        Cube("WindowShutter", new Vector3(w * 0.08f, bh * 0.55f, -d * 0.525f), new Vector3(w * 0.05f, bh * 0.26f, 0.03f), wood, r);
        Cube("DoorLintel", new Vector3(0, bh * 0.61f, -d * 0.53f), new Vector3(w * 0.24f, 0.03f, 0.025f), wood, r);
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
        Light l = go.AddComponent<Light>(); l.type = LightType.Directional; l.color = new Color(1, 0.84f, 0.68f); l.intensity = 2.90f; l.shadows = LightShadows.Soft; l.lightmapBakeType = LightmapBakeType.Baked;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.58f, 0.67f, 0.63f); RenderSettings.ambientEquatorColor = new Color(0.42f, 0.48f, 0.42f); RenderSettings.ambientGroundColor = new Color(0.24f, 0.20f, 0.14f);
        RenderSettings.ambientIntensity = 2.10f;
        Material sky = SkyMaterial();
        RenderSettings.skybox = sky;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.34f, 0.44f, 0.39f);
        RenderSettings.fogDensity = 0.004f;
        RenderSettings.sun = l;
        CreateLightProbes(parent);
        DynamicGI.UpdateEnvironment();
    }

    static void CreateLightProbes(Transform parent)
    {
        GameObject go = new GameObject("WoodlandLightProbes");
        go.transform.SetParent(parent, false);
        LightProbeGroup group = go.AddComponent<LightProbeGroup>();
        group.probePositions = new[]
        {
            new Vector3(-2.20f, 0.35f, -2.10f), new Vector3(0f, 0.35f, -2.10f), new Vector3(2.20f, 0.35f, -2.10f),
            new Vector3(-2.20f, 1.05f, 0f), new Vector3(0f, 1.05f, 0f), new Vector3(2.20f, 1.05f, 0f),
            new Vector3(-2.20f, 1.95f, 1.90f), new Vector3(0f, 1.95f, 1.90f), new Vector3(2.20f, 1.95f, 1.90f),
        };
    }

    static void ConfigureBakedLighting()
    {
        LightingSettings settings;
        if (!Lightmapping.TryGetLightingSettings(out settings) || settings == null)
        {
            settings = new LightingSettings();
            Lightmapping.lightingSettings = settings;
        }
        settings.autoGenerate = false;
        settings.bakedGI = true;
        settings.realtimeGI = false;
        settings.lightmapResolution = 16f;
        settings.lightmapMaxSize = 1024;
        settings.lightmapPadding = 2;
        settings.indirectResolution = 1f;
        settings.directSampleCount = 32;
        settings.indirectSampleCount = 64;
        settings.environmentSampleCount = 32;
        settings.maxBounces = 2;
        settings.maxBounces = 2;
        settings.prioritizeView = false;
        EditorUtility.SetDirty(settings);
    }

    static Material SkyMaterial()
    {
        string path = MaterialFolder + "/WoodlandSky.mat";
        Material sky = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Skybox/Procedural");
        if (shader == null) throw new InvalidOperationException("Unity built-in Skybox/Procedural shader is required for the woodland backdrop");
        if (sky == null) { sky = new Material(shader); AssetDatabase.CreateAsset(sky, path); }
        else if (sky.shader != shader) sky.shader = shader;
        if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", new Color(0.48f, 0.64f, 0.60f));
        if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", new Color(0.18f, 0.24f, 0.19f));
        if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", 0.72f);
        if (sky.HasProperty("_SunSize")) sky.SetFloat("_SunSize", 0.035f);
        if (sky.HasProperty("_SunSizeConvergence")) sky.SetFloat("_SunSizeConvergence", 4f);
        if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", 0.50f);
        EditorUtility.SetDirty(sky);
        return sky;
    }

    static void Descriptor(Transform parent, WorldSpec s)
    {
        GameObject world = new GameObject("VRCWorld"); world.transform.SetParent(parent, false); VRCSceneDescriptor d = world.AddComponent<VRCSceneDescriptor>(); world.AddComponent<PipelineManager>();
        Transform spawn = Empty("SpawnPoint", parent); spawn.position = Pos(s.world_build.spawn.position_m) + Vector3.up * 0.02f; spawn.rotation = Quaternion.Euler(0, s.world_build.spawn.facing_deg, 0); d.spawns = new[] { spawn };
        GameObject cameraGo = new GameObject("ReferenceCamera"); cameraGo.transform.SetParent(parent, false); Vector3 cameraPosition = Pos(s.world_build.hero_view.position_m); cameraPosition.z += 0.30f; cameraGo.transform.position = cameraPosition; Vector3 cameraTarget = Pos(s.world_build.hero_view.target_m); cameraTarget.y -= 0.22f; cameraGo.transform.LookAt(cameraTarget, Vector3.up);
        Camera c = cameraGo.AddComponent<Camera>(); c.enabled = false; c.clearFlags = CameraClearFlags.Skybox; c.fieldOfView = 36; c.nearClipPlane = Mathf.Max(0.01f, s.runtime_budget.camera_near_clip_m); d.ReferenceCamera = cameraGo;
    }

    static Vector3 Pos(float[] xyz) { if (xyz == null || xyz.Length != 3) throw new InvalidDataException("Expected xyz vector"); return new Vector3(xyz[0], xyz[2], xyz[1]); }
    static Transform Empty(string name, Transform parent) { Transform t = new GameObject(name).transform; if (parent != null) t.SetParent(parent, false); return t; }
    static GameObject Cube(string n, Vector3 p, Vector3 s, Material m, Transform parent) { GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube); Setup(g, n, p, s, m, parent); return g; }
    static GameObject Cylinder(string n, Vector3 p, float dia, float h, Material m, Transform parent) { GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Setup(g, n, p, new Vector3(dia, h / 2, dia), m, parent); return g; }
    static GameObject Sphere(string n, Vector3 p, Vector3 s, Material m, Transform parent) { GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere); Setup(g, n, p, s, m, parent); return g; }
    static GameObject VisualCube(string n, Vector3 p, Vector3 s, Material m, Transform parent) { GameObject g = Cube(n, p, s, m, parent); DisableCollider(g); return g; }
    static GameObject VisualCylinder(string n, Vector3 p, float dia, float h, Material m, Transform parent) { GameObject g = Cylinder(n, p, dia, h, m, parent); DisableCollider(g); return g; }
    static GameObject VisualSphere(string n, Vector3 p, Vector3 s, Material m, Transform parent) { GameObject g = Sphere(n, p, s, m, parent); DisableCollider(g); return g; }
    static void HideRenderer(GameObject g) { Renderer renderer = g.GetComponent<Renderer>(); if (renderer != null) renderer.enabled = false; }
    static void DisableCollider(GameObject g) { Collider collider = g.GetComponent<Collider>(); if (collider != null) collider.enabled = false; }

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
        if (emission && m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * 1.10f); }
        EditorUtility.SetDirty(m); return m;
    }

    static void ApplySurface(Material material, string diffuseRelativePath, string normalRelativePath, string occlusionRelativePath, Vector2 tiling, Color tint, float bumpScale)
    {
        Texture2D diffuse = LoadTexture(diffuseRelativePath, TextureImporterType.Default, true);
        Texture2D normal = LoadTexture(normalRelativePath, TextureImporterType.NormalMap, false);
        Texture2D occlusion = LoadTexture(occlusionRelativePath, TextureImporterType.Default, false);
        if (!material.HasProperty("_MainTex")) throw new InvalidOperationException("Woodland surface shader does not expose _MainTex: " + material.name);
        material.SetTexture("_MainTex", diffuse);
        material.SetTextureScale("_MainTex", tiling);
        material.color = tint;
        if (material.HasProperty("_BumpMap"))
        {
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", bumpScale);
            material.EnableKeyword("_NORMALMAP");
        }
        if (material.HasProperty("_OcclusionMap"))
        {
            material.SetTexture("_OcclusionMap", occlusion);
            material.SetFloat("_OcclusionStrength", 0.72f);
        }
        EditorUtility.SetDirty(material);
    }

    static Texture2D LoadTexture(string relativePath, TextureImporterType type, bool srgb)
    {
        string path = TextureFolder + "/" + relativePath;
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Woodland texture importer is missing: " + path);
        bool changed = importer.textureType != type || importer.sRGBTexture != srgb || importer.wrapMode != TextureWrapMode.Repeat || importer.filterMode != FilterMode.Bilinear || importer.anisoLevel != 2;
        importer.textureType = type;
        importer.sRGBTexture = srgb;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.anisoLevel = 2;
        if (changed) importer.SaveAndReimport();
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null) throw new InvalidOperationException("Woodland texture could not be loaded: " + path);
        return texture;
    }

    static void SoftEmission(Material material, float strength)
    {
        if (material == null || !material.HasProperty("_EmissionColor")) return;
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", material.color * strength);
        EditorUtility.SetDirty(material);
    }

    static Color Html(string value) { Color c; if (!ColorUtility.TryParseHtmlString(value, out c)) throw new InvalidDataException("Invalid palette color: " + value); return c; }
    static void Face(Transform t, Vector3 target) { Vector3 v = target - t.position; v.y = 0; if (v.sqrMagnitude > 0.0001f) t.rotation = Quaternion.LookRotation(v.normalized, Vector3.up); }
    static void AddScene(string scenePath) { EditorBuildSettingsScene[] s = EditorBuildSettings.scenes; if (!s.Any(x => x.path == scenePath)) EditorBuildSettings.scenes = s.Concat(new[] { new EditorBuildSettingsScene(scenePath, true) }).ToArray(); }
    static void EnsureFolder(string folder) { string[] p = folder.Split('/'); string cur = p[0]; for (int i = 1; i < p.Length; i++) { string next = cur + "/" + p[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, p[i]); cur = next; } }
}
