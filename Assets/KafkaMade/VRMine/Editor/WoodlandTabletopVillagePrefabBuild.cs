using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Components;

public static class WoodlandTabletopVillagePrefabBuild
{
    const string SpecPath = "config/world-design/generated/woodland-tabletop-village-v0.json";
    const string PrefabFolder = "Assets/KafkaMade/VRMine/Prefabs";
    const string PrefabPath = PrefabFolder + "/WoodlandTabletopVillage.prefab";
    const string RootName = "WoodlandTabletopVillage";

    [Serializable] class Spec { public Blockout blockout; public string[] life_traces; }
    [Serializable] class Blockout { public Anchor[] anchors; }
    [Serializable] class Anchor { public string id; }

    public static void BuildAndVerifyBatch()
    {
        WoodlandTabletopVillageSceneBuilder.BuildBatch();
        WoodlandTabletopVillageLifeTraceBuild.MaterializeAndVerify();
        WoodlandTabletopVillageAudioBuild.MaterializeAndVerifyScene();
        MaterializePrefab();
        VerifyPrefabOrThrow();
        Debug.Log("Woodland Tabletop Village prefab verification PASS");
    }

    static void MaterializePrefab()
    {
        GameObject root = GameObject.Find(RootName);
        Require(root != null, "generated scene root is missing");
        EnsureFolder(PrefabFolder);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
        Require(success && prefab != null, "PrefabUtility failed to save generated world prefab");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static void VerifyPrefabOrThrow()
    {
        Spec spec = LoadSpec();
        Require(File.Exists(PrefabPath), "generated prefab file is missing");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Require(prefab != null, "generated prefab cannot be loaded by AssetDatabase");
        Require(prefab.name == RootName, "generated prefab root identity drifted");

        string[] anchors = spec.blockout.anchors.Select(a => a.id).ToArray();
        Require(anchors.Length == 8 && anchors.Distinct().Count() == 8, "canonical spec must contain eight unique anchors");
        Transform village = Find(prefab.transform, "VillageBlockout");
        Require(village != null, "VillageBlockout is missing from prefab");
        Require(village.childCount == anchors.Length, "prefab anchor count differs from canonical spec");
        foreach (string id in anchors)
            Require(Find(prefab.transform, id) != null, "canonical anchor is missing from prefab: " + id);

        Require(spec.life_traces != null && spec.life_traces.Length >= 3, "canonical spec must contain at least three life traces");
        Transform traces = Find(prefab.transform, "LifeTraces");
        Require(traces != null, "LifeTraces group is missing from prefab");
        foreach (string id in spec.life_traces.Take(3))
            Require(Find(prefab.transform, "LifeTrace_" + id) != null, "canonical life trace is missing from prefab: " + id);

        VRCSceneDescriptor descriptor = prefab.GetComponentInChildren<VRCSceneDescriptor>(true);
        Require(descriptor != null, "VRCSceneDescriptor is missing from prefab");
        Require(descriptor.spawns != null && descriptor.spawns.Length == 1 && descriptor.spawns[0] != null, "prefab must preserve exactly one spawn reference");
        Require(descriptor.ReferenceCamera != null, "prefab must preserve VRChat reference camera");

        WoodlandTabletopVillageAudioBuild.VerifyPrefab(prefab);
    }

    static Spec LoadSpec()
    {
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", SpecPath));
        Require(File.Exists(path), "canonical spec is missing");
        Spec spec = JsonUtility.FromJson<Spec>(File.ReadAllText(path));
        Require(spec != null && spec.blockout != null && spec.blockout.anchors != null, "canonical blockout spec could not be parsed");
        return spec;
    }

    static Transform Find(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = Find(child, name);
            if (found != null) return found;
        }
        return null;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Woodland Tabletop Village prefab verification FAIL: " + message);
    }
}
