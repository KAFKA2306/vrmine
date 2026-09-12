using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Components;

public static class WoodlandTabletopVillageVerification
{
    const string ScenePath = "Assets/KafkaMade/VRMine/Scenes/WoodlandTabletopVillage.unity";
    static readonly string[] RequiredAnchors =
    {
        "great_tree", "bridge", "lodge", "cottage_a", "cottage_b", "market_stall", "reading_bench", "stream"
    };

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
        if (!System.IO.File.Exists(ScenePath))
            throw new InvalidOperationException("Generated woodland scene is missing. Run VRMine/Worlds/Build Woodland Tabletop Village first.");

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject root = GameObject.Find("WoodlandTabletopVillage");
        Require(root != null, "root object is missing");
        Require(GameObject.Find("WoodlandTabletop") != null, "tabletop is missing");
        Require(GameObject.Find("DioramaTableTop") != null, "2.8 m diorama table is missing");
        Require(GameObject.Find("ReadingNook") != null, "reading nook is missing");

        foreach (string anchor in RequiredAnchors)
            Require(GameObject.Find(anchor) != null, "required tabletop anchor is missing: " + anchor);

        VRCSceneDescriptor descriptor = UnityEngine.Object.FindObjectOfType<VRCSceneDescriptor>();
        Require(descriptor != null, "VRCSceneDescriptor is missing");
        Require(descriptor.spawns != null && descriptor.spawns.Length == 1 && descriptor.spawns[0] != null, "exactly one spawn is required");
        Require(descriptor.ReferenceCamera != null, "VRChat reference camera is missing");

        Transform spawn = descriptor.spawns[0];
        Require(Vector3.Distance(spawn.position, new Vector3(0f, 0.02f, -2.75f)) < 0.02f, "spawn drifted from canonical spec");
        Require(Mathf.Abs(descriptor.ReferenceCamera.GetComponent<Camera>().nearClipPlane - 0.01f) < 0.001f, "reference camera near clip must remain 0.01 m");

        Light[] lights = UnityEngine.Object.FindObjectsOfType<Light>();
        int realtime = lights.Count(l => l.lightmapBakeType != LightmapBakeType.Baked);
        Require(realtime == 0, "runtime budget requires zero realtime or mixed lights, found " + realtime);
        Require(lights.Any(l => l.name == "BakedSun" && l.lightmapBakeType == LightmapBakeType.Baked), "baked key light is missing");

        Renderer[] renderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
        Require(renderers.Length >= 30, "blockout is unexpectedly sparse: " + renderers.Length + " renderers");
        Require(renderers.All(r => r.sharedMaterial != null), "every visible primitive must have a material");
        Require(renderers.All(r => (GameObjectUtility.GetStaticEditorFlags(r.gameObject) & StaticEditorFlags.ContributeGI) != 0), "all generated renderers must contribute GI");

        Collider[] colliders = UnityEngine.Object.FindObjectsOfType<Collider>();
        Require(colliders.Length >= 20, "expected tabletop and room collision geometry");

        bool buildEnabled = EditorBuildSettings.scenes.Any(s => s.enabled && s.path == ScenePath);
        Require(buildEnabled, "generated scene is not enabled in EditorBuildSettings");

        GameObject village = GameObject.Find("VillageBlockout");
        Require(village != null && village.transform.childCount == RequiredAnchors.Length, "village blockout must contain exactly eight canonical anchors");
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Woodland Tabletop Village verification FAIL: " + message);
    }
}