using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WoodlandTabletopVillageLifeTraceBuild
{
    const string SpecPath = "config/world-design/generated/woodland-tabletop-village-v0.json";
    const string MaterialFolder = "Assets/KafkaMade/VRMine/Materials/WoodlandTabletopVillage";
    const int RequiredVisibleLifeTraces = 3;

    [Serializable] class Spec { public string[] life_traces; }

    public static void MaterializeAndVerify()
    {
        Spec spec = LoadSpec();
        Require(spec.life_traces != null && spec.life_traces.Length >= RequiredVisibleLifeTraces,
            "canonical spec must provide at least three life traces");

        string[] required = spec.life_traces.Take(RequiredVisibleLifeTraces).ToArray();
        Require(required.Distinct().Count() == RequiredVisibleLifeTraces, "first three canonical life traces must be unique");

        Transform root = GameObject.Find("WoodlandTabletopVillage")?.transform;
        Require(root != null, "generated world root is missing");
        Transform group = new GameObject("LifeTraces").transform;
        group.SetParent(root, false);

        CreateTrace(required[0], "reading_bench", new Vector3(0f, 0.15f, -0.03f), new Vector3(0.12f, 0.018f, 0.08f), group);
        CreateTrace(required[1], "market_stall", new Vector3(-0.08f, 0.13f, -0.03f), new Vector3(0.055f, 0.07f, 0.055f), group);
        CreateTrace(required[2], "lodge", new Vector3(-0.34f, 0.05f, 0.02f), new Vector3(0.16f, 0.09f, 0.08f), group);
        EditorSceneManager.SaveOpenScenes();

        foreach (string id in required)
        {
            GameObject trace = GameObject.Find("LifeTrace_" + id);
            Require(trace != null && trace.activeInHierarchy, "canonical life trace is missing: " + id);
            Renderer renderer = trace.GetComponent<Renderer>();
            Require(renderer != null && renderer.enabled && renderer.sharedMaterial != null,
                "canonical life trace is not visibly materialized: " + id);
        }

        Debug.Log("Woodland Tabletop Village life-trace verification PASS");
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
        Material traceMaterial = source.sharedMaterial;
        if (id == "open_book") traceMaterial = LoadMaterial("Paper", traceMaterial);
        if (id == "half_finished_mug") traceMaterial = LoadMaterial("LampGold", traceMaterial);
        trace.GetComponent<Renderer>().sharedMaterial = traceMaterial;
        GameObjectUtility.SetStaticEditorFlags(trace,
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

        if (id == "open_book")
        {
            GameObject page = GameObject.CreatePrimitive(PrimitiveType.Cube);
            page.name = "OpenBookPage";
            page.transform.SetParent(trace.transform, false);
            page.transform.localPosition = new Vector3(0.42f, 0.06f, 0);
            page.transform.localRotation = Quaternion.Euler(0, 0, -8);
            page.transform.localScale = new Vector3(0.70f, 0.06f, 0.90f);
            page.GetComponent<Renderer>().sharedMaterial = traceMaterial;
            MarkStatic(page);
        }
        else if (id == "half_finished_mug")
        {
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.name = "MugRim";
            handle.transform.SetParent(trace.transform, false);
            handle.transform.localPosition = new Vector3(0, 0.48f, 0);
            handle.transform.localScale = new Vector3(0.72f, 0.05f, 0.72f);
            handle.GetComponent<Renderer>().sharedMaterial = traceMaterial;
            MarkStatic(handle);
        }
        else if (id == "stacked_firewood")
        {
            GameObject log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            log.name = "FirewoodLog";
            log.transform.SetParent(trace.transform, false);
            log.transform.localPosition = new Vector3(0, 0.30f, 0.32f);
            log.transform.localRotation = Quaternion.Euler(90, 0, 0);
            log.transform.localScale = new Vector3(0.60f, 0.08f, 0.60f);
            log.GetComponent<Renderer>().sharedMaterial = traceMaterial;
            MarkStatic(log);
        }
    }

    static void MarkStatic(GameObject gameObject)
    {
        GameObjectUtility.SetStaticEditorFlags(gameObject,
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
    }

    static Material LoadMaterial(string name, Material fallback)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/" + name + ".mat");
        return material ?? fallback;
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
