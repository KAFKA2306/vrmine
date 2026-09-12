using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Components;

public static class WoodlandTabletopVillageVerification
{
    const string SpecPath = "config/world-design/generated/woodland-tabletop-village-v0.json";
    const string ScenePath = "Assets/KafkaMade/VRMine/Scenes/WoodlandTabletopVillage.unity";

    [Serializable] class Spec { public Spatial spatial_geometry; public RuntimeBudget runtime_budget; public WorldBuild world_build; public Blockout blockout; }
    [Serializable] class Spatial { public Zone[] zones; }
    [Serializable] class Zone { public string zone_id; public float diameter_m; }
    [Serializable] class RuntimeBudget { public int realtime_light_count; public float camera_near_clip_m; }
    [Serializable] class WorldBuild { public Spawn spawn; }
    [Serializable] class Spawn { public float[] position_m; }
    [Serializable] class Blockout { public Anchor[] anchors; }
    [Serializable] class Anchor { public string id; }

    [MenuItem("VRMine/Worlds/Verify Woodland Tabletop Village")]
    public static void VerifyMenu()
    {
        VerifyOrThrow();
        Debug.Log("Woodland Tabletop Village verification PASS");
    }

    public static void VerifyBatch()
    {
        VerifyOrThrow();
        Debug.Log("Woodland Tabletop Village verification PASS");
    }

    public static void VerifyOrThrow()
    {
        Spec spec = LoadSpec();
        Require(File.Exists(ScenePath), "generated scene is missing");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Require(GameObject.Find("WoodlandTabletopVillage") != null, "root object is missing");
        Require(GameObject.Find("WoodlandTabletop") != null, "tabletop is missing");
        Require(GameObject.Find("ReadingNook") != null, "reading nook is missing");

        string[] anchors = spec.blockout.anchors.Select(a => a.id).ToArray();
        Require(anchors.Length == 8 && anchors.Distinct().Count() == 8, "canonical spec must contain eight unique anchors");
        foreach (string id in anchors) Require(GameObject.Find(id) != null, "required tabletop anchor is missing: " + id);

        GameObject tabletop = GameObject.Find("DioramaTableTop");
        Require(tabletop != null, "diorama tabletop geometry is missing");
        Zone tabletopZone = spec.spatial_geometry.zones.FirstOrDefault(z => z.zone_id == "woodland_tabletop");
        Require(tabletopZone != null, "canonical woodland_tabletop zone is missing");
        Require(Mathf.Abs(tabletop.transform.lossyScale.x - tabletopZone.diameter_m) < 0.01f, "tabletop diameter drifted from canonical spec");

        VRCSceneDescriptor descriptor = UnityEngine.Object.FindObjectOfType<VRCSceneDescriptor>();
        Require(descriptor != null, "VRCSceneDescriptor is missing");
        Require(descriptor.spawns != null && descriptor.spawns.Length == 1 && descriptor.spawns[0] != null, "exactly one spawn is required");
        Require(descriptor.ReferenceCamera != null, "VRChat reference camera is missing");

        Vector3 expectedSpawn = SpecToUnity(spec.world_build.spawn.position_m) + Vector3.up * 0.02f;
        Require(Vector3.Distance(descriptor.spawns[0].position, expectedSpawn) < 0.02f, "spawn drifted from canonical spec");
        Camera referenceCamera = descriptor.ReferenceCamera.GetComponent<Camera>();
        Require(referenceCamera != null, "reference camera component is missing");
        Require(Mathf.Abs(referenceCamera.nearClipPlane - spec.runtime_budget.camera_near_clip_m) < 0.001f, "reference camera near clip drifted from canonical spec");

        Light[] lights = UnityEngine.Object.FindObjectsOfType<Light>();
        int realtime = lights.Count(l => l.lightmapBakeType != LightmapBakeType.Baked);
        Require(realtime == spec.runtime_budget.realtime_light_count, "realtime light count drifted from canonical runtime budget");
        Require(lights.Any(l => l.name == "BakedSun" && l.lightmapBakeType == LightmapBakeType.Baked), "baked key light is missing");

        Renderer[] renderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
        Require(renderers.Length >= 30, "blockout is unexpectedly sparse: " + renderers.Length + " renderers");
        Require(renderers.All(r => r.sharedMaterial != null), "every visible primitive must have a material");
        Require(renderers.All(r => (GameObjectUtility.GetStaticEditorFlags(r.gameObject) & StaticEditorFlags.ContributeGI) != 0), "all generated renderers must contribute GI");
        Require(UnityEngine.Object.FindObjectsOfType<Collider>().Length >= 20, "expected tabletop and room collision geometry");
        Require(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == ScenePath), "generated scene is not enabled in EditorBuildSettings");

        GameObject village = GameObject.Find("VillageBlockout");
        Require(village != null && village.transform.childCount == anchors.Length, "village blockout child count differs from canonical anchors");
    }

    static Spec LoadSpec()
    {
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", SpecPath));
        Require(File.Exists(path), "canonical spec is missing");
        Spec spec = JsonUtility.FromJson<Spec>(File.ReadAllText(path));
        Require(spec != null && spec.spatial_geometry != null && spec.runtime_budget != null && spec.world_build != null && spec.blockout != null && spec.blockout.anchors != null, "canonical spec could not be parsed");
        return spec;
    }

    static Vector3 SpecToUnity(float[] xyz)
    {
        Require(xyz != null && xyz.Length == 3, "canonical position must be xyz");
        return new Vector3(xyz[0], xyz[2], xyz[1]);
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Woodland Tabletop Village verification FAIL: " + message);
    }
}