using System;
using System.IO;
using System.Reflection;
using UnityEngine;

public static class GaussianExhibitionPresentation
{
    const string ConfigPath = "config/gaussian-exhibition.json";
    const string GaussianSplatObjectTypeName = "GaussianSplatting.GaussianSplatObject";

    [Serializable]
    sealed class ExhibitionConfig
    {
        public float target_extent_m;
        public float presentation_scale_multiplier;
        public LayoutConfig layout;
    }

    [Serializable]
    sealed class LayoutConfig
    {
        public float pad_size_m;
    }

    public static void Apply()
    {
        ExhibitionConfig config = JsonUtility.FromJson<ExhibitionConfig>(File.ReadAllText(ConfigPath));
        if (config == null || !Finite(config.target_extent_m) || config.target_extent_m <= 0f)
            throw new InvalidDataException("Gaussian exhibition target_extent_m must be a positive finite value.");
        if (!Finite(config.presentation_scale_multiplier) || config.presentation_scale_multiplier <= 0f)
            throw new InvalidDataException("Gaussian exhibition presentation_scale_multiplier must be a positive finite value.");
        if (config.layout == null || !Finite(config.layout.pad_size_m) || config.layout.pad_size_m <= 0f)
            throw new InvalidDataException("Gaussian exhibition pad_size_m must be a positive finite value.");

        GameObject root = GameObject.Find("GaussianExhibits");
        if (root == null)
            throw new InvalidOperationException("GaussianExhibits root is missing after scene build.");

        Type splatType = FindType(GaussianSplatObjectTypeName);
        if (splatType == null)
            throw new InvalidOperationException("GaussianSplatObject runtime type is missing.");

        float presentedExtent = config.target_extent_m * config.presentation_scale_multiplier;
        float padSize = Mathf.Max(config.layout.pad_size_m, presentedExtent);
        float outwardOffset = Mathf.Max(0f, (presentedExtent - config.layout.pad_size_m) * 0.5f);
        int applied = 0;

        foreach (Transform exhibit in root.transform)
        {
            if (!exhibit.name.StartsWith("Exhibit_", StringComparison.Ordinal)) continue;

            exhibit.localScale = exhibit.localScale * config.presentation_scale_multiplier;

            Bounds bounds = GetWorldBounds(exhibit.gameObject, splatType);
            exhibit.position += Vector3.up * -bounds.min.y;

            Vector3 exhibitPosition = exhibit.position;
            if (Mathf.Abs(exhibitPosition.z) > 0.0001f)
                exhibitPosition.z += Mathf.Sign(exhibitPosition.z) * outwardOffset;
            exhibit.position = exhibitPosition;

            bounds = GetWorldBounds(exhibit.gameObject, splatType);
            float extent = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (Mathf.Abs(extent - presentedExtent) > 0.01f)
                throw new InvalidOperationException(exhibit.name + ": expected presentation extent " + presentedExtent + " m, got " + extent + " m.");
            if (Mathf.Abs(bounds.min.y) > 0.01f)
                throw new InvalidOperationException(exhibit.name + ": floor alignment drifted after presentation scaling; bottom=" + bounds.min.y + " m.");

            string index = ParseIndex(exhibit.name);
            GameObject pad = GameObject.Find("ExhibitPad_" + index);
            if (pad == null) throw new InvalidOperationException("Missing exhibit pad for " + exhibit.name + ".");
            Vector3 padScale = pad.transform.localScale;
            padScale.x = padSize;
            padScale.z = padSize;
            pad.transform.localScale = padScale;
            pad.transform.position = new Vector3(exhibit.position.x, pad.transform.position.y, exhibit.position.z);

            GameObject label = GameObject.Find("ExhibitLabel_" + index);
            if (label == null) throw new InvalidOperationException("Missing exhibit label for " + exhibit.name + ".");
            Vector3 towardAisle = exhibit.rotation * Vector3.forward;
            label.transform.SetPositionAndRotation(
                new Vector3(bounds.center.x, bounds.max.y + 0.2f, bounds.center.z) + towardAisle * (presentedExtent * 0.5f + 0.25f),
                exhibit.rotation);

            applied++;
        }

        if (applied == 0)
            throw new InvalidOperationException("No Gaussian exhibit roots were found for presentation scaling.");

        Debug.Log("Gaussian exhibition presentation applied: exhibits=" + applied + ", multiplier=" + config.presentation_scale_multiplier + ", targetExtent=" + presentedExtent + " m.");
    }

    static string ParseIndex(string name)
    {
        const string prefix = "Exhibit_";
        if (name.Length < prefix.Length + 2)
            throw new InvalidDataException("Unexpected Gaussian exhibit name: " + name);
        return name.Substring(prefix.Length, 2);
    }

    static Bounds GetWorldBounds(GameObject exhibit, Type splatType)
    {
        Component splat = exhibit.GetComponentInChildren(splatType, true);
        if (splat == null) throw new InvalidOperationException("Gaussian splat component is missing: " + exhibit.name);
        MethodInfo method = splatType.GetMethod("TryGetLocalBounds", BindingFlags.Public | BindingFlags.Instance);
        if (method == null) throw new MissingMethodException(splatType.FullName, "TryGetLocalBounds");
        object[] args = new object[] { new Bounds() };
        bool valid = (bool)method.Invoke(splat, args);
        if (!valid) throw new InvalidOperationException("Gaussian bounds are unavailable: " + exhibit.name);
        return TransformBounds(splat.transform, (Bounds)args[0]);
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

    static Type FindType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName, false);
            if (type != null) return type;
        }
        return null;
    }

    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
