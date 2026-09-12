using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WoodlandTabletopVillageLifeTraceBuild
{
    const string SpecPath = "config/world-design/generated/woodland-tabletop-village-v0.json";
    const int RequiredVisibleLifeTraces = 3;

    [Serializable] class Spec { public string[] life_traces; }

    public static void BuildAndVerifyBatch()
    {
        WoodlandTabletopVillageSceneBuilder.Build();
        Spec spec = LoadSpec();
        Require(spec.life_traces != null && spec.life_traces.Length >= RequiredVisibleLifeTraces,
            "canonical spec must provide at least three life traces");

        Materialize(spec.life_traces.Take(RequiredVisibleLifeTraces).ToArray());
        EditorSceneManager.SaveOpenScenes();

        WoodlandTabletopVillageVerification.VerifyOrThrow();
        VerifyOrThrow(spec);
        Debug.Log("Woodland Tabletop Village life-trace verification PASS");
    }

    static void Materialize(string[] ids)
    {
        Transform root = GameObject.Find("WoodlandTabletopVillage")?.transform;
        Require(root != null, "generated world root is missing");

        Transform group = new GameObject("LifeTraces").transform;
        group.SetParent(root, false);

        CreateTrace(ids[0], "reading_bench", new Vector3(0f, 0.15f, -0.03f), new Vector3(0.12f, 0.018f, 0.08f), group);
        CreateTrace(ids[1], "market_stall", new Vector3(-0.08f, 0.13f, -0.03f), new Vector3(0.055f, 0.07f, 0.055f), group);
        CreateTrace(ids[2], "lodge", new Vector3(-0.34f, 0.05f, 0.02f), new Vector3(0.16f, 0.09f, 0.08f), group);
    }

    static void CreateTrace(string id, string anchorName, Vector3 localOffset, Vector3 scale, Transform group)
    {
        Require(!string.IsNullOrWhiteSpace(id), "life trace id is empty");
        Transform anchor = GameObject.Find(anchorName)?.transform;
        Require(anchor != null, "required life-trace anchor is missing: " + anchorName);

        GameObject trace = GameObject.CreatePrimitive(PrimitiveType.Cube);
        trace.name = "LifeTrace_" + id;
        trace.transform.SetParent(group, false);
        trace.transform.position = anchor.TransformPoint(localOffset);
        trace.transform.rotation = anchor.rotation;
        trace.transform.localScale = scale;

        Renderer source = anchor.GetComponentInChildren<Renderer>();
        Require(source != null && source.sharedMaterial != null, "life-trace anchor lacks a material: " + anchorName);
        trace.GetComponent<Renderer>().sharedMaterial = source.sharedMaterial;
        GameObjectUtility.SetStaticEditorFlags(trace,
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
    }

    static void VerifyOrThrow(Spec spec)
    {
        string[] required = spec.life_traces.Take(RequiredVisibleLifeTraces).ToArray();
        Require(required.Distinct().Count() == RequiredVisibleLifeTraces, "first three canonical life traces must be unique");
        Require(GameObject.Find("LifeTraces") != null, "LifeTraces group is missing");

        foreach (string id in required)
        {
            GameObject trace = GameObject.Find("LifeTrace_" + id);
            Require(trace != null && trace.activeInHierarchy, "canonical life trace is missing: " + id);
            Renderer renderer = trace.GetComponent<Renderer>();
            Require(renderer != null && renderer.enabled && renderer.sharedMaterial != null,
                "canonical life trace is not visibly materialized: " + id);
        }
    }

    static Spec LoadSpec()
    {
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", SpecPath));
        Require(File.Exists(path), "canonical spec is missing");
        Spec spec = JsonUtility.FromJson<Spec>(File.ReadAllText(path));
        Require(spec != null, "canonical spec could not be parsed");
        return spec;
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Woodland Tabletop Village life-trace verification FAIL: " + message);
    }
}
