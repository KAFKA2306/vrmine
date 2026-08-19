using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[Serializable]
public sealed class GaussianImportOverride
{
    public string id;
    public GaussianCropOverride crop;
    public GaussianAlignmentOverride alignment;
}

[Serializable]
public sealed class GaussianCropOverride
{
    public bool enabled;
    public GaussianVector3 center;
    public GaussianVector3 size;
    public float padding;
}

[Serializable]
public sealed class GaussianAlignmentOverride
{
    public bool enabled;
    public string mode;
    public GaussianQuaternion rotation;
    public GaussianVector3 pivot;
}

[Serializable]
public sealed class GaussianVector3
{
    public float x;
    public float y;
    public float z;

    public Vector3 ToVector3() => new Vector3(x, y, z);
}

[Serializable]
public sealed class GaussianQuaternion
{
    public float x;
    public float y;
    public float z;
    public float w;

    public Quaternion ToQuaternion() => new Quaternion(x, y, z, w);
}

public static class GaussianImportOverrides
{
    public static void Validate(GaussianImportOverride[] overrides, string[] registeredIds)
    {
        if (overrides == null || overrides.Length == 0) return;
        var registered = new HashSet<string>(registeredIds ?? Array.Empty<string>(), StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (GaussianImportOverride entry in overrides)
        {
            if (entry == null || string.IsNullOrEmpty(entry.id))
                throw new InvalidOperationException("Gaussian import override id is missing.");
            if (!registered.Contains(entry.id))
                throw new InvalidOperationException("Gaussian import override references an unknown id: " + entry.id);
            if (!seen.Add(entry.id))
                throw new InvalidOperationException("Duplicate Gaussian import override id: " + entry.id);
            ValidateCrop(entry.id, entry.crop);
            ValidateAlignment(entry.id, entry.alignment);
        }
    }

    public static string Apply(Type optionsType, object options, GaussianImportOverride[] overrides, string id)
    {
        SetField(optionsType, options, "cropToBounds", false);
        SetField(optionsType, options, "cropBounds", new Bounds(Vector3.zero, Vector3.one));
        SetField(optionsType, options, "cropPadding", 0f);
        SetField(optionsType, options, "applyHorizonAlignment", false);
        SetField(optionsType, options, "horizonRotation", Quaternion.identity);
        SetField(optionsType, options, "horizonPivot", Vector3.zero);

        GaussianImportOverride selected = null;
        if (overrides != null)
        {
            foreach (GaussianImportOverride entry in overrides)
            {
                if (entry != null && string.Equals(entry.id, id, StringComparison.Ordinal))
                {
                    selected = entry;
                    break;
                }
            }
        }
        if (selected == null) return "{}";

        if (selected.crop != null && selected.crop.enabled)
        {
            Vector3 center = selected.crop.center.ToVector3();
            Vector3 size = selected.crop.size.ToVector3();
            SetField(optionsType, options, "cropToBounds", true);
            SetField(optionsType, options, "cropBounds", new Bounds(center, size));
            SetField(optionsType, options, "cropPadding", selected.crop.padding);
        }

        if (selected.alignment != null && selected.alignment.enabled)
        {
            Quaternion rotation = Normalize(selected.alignment.rotation.ToQuaternion());
            Vector3 pivot = selected.alignment.pivot == null ? Vector3.zero : selected.alignment.pivot.ToVector3();
            SetField(optionsType, options, "applyHorizonAlignment", true);
            SetField(optionsType, options, "horizonRotation", rotation);
            SetField(optionsType, options, "horizonPivot", pivot);
        }

        return JsonUtility.ToJson(selected);
    }

    static void ValidateCrop(string id, GaussianCropOverride crop)
    {
        if (crop == null || !crop.enabled) return;
        if (crop.center == null || crop.size == null)
            throw new InvalidOperationException(id + ": enabled crop requires center and size.");
        Vector3 center = crop.center.ToVector3();
        Vector3 size = crop.size.ToVector3();
        if (!Finite(center) || !Finite(size) || size.x <= 0f || size.y <= 0f || size.z <= 0f)
            throw new InvalidOperationException(id + ": crop center/size must be finite and size must be positive.");
        if (!Finite(crop.padding) || crop.padding < 0f)
            throw new InvalidOperationException(id + ": crop padding must be finite and non-negative.");
    }

    static void ValidateAlignment(string id, GaussianAlignmentOverride alignment)
    {
        if (alignment == null || !alignment.enabled) return;
        if (alignment.mode != "horizon" && alignment.mode != "wall")
            throw new InvalidOperationException(id + ": alignment mode must be horizon or wall.");
        if (alignment.rotation == null)
            throw new InvalidOperationException(id + ": enabled alignment requires a rotation quaternion.");
        Quaternion rotation = alignment.rotation.ToQuaternion();
        float magnitudeSquared = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
        if (!Finite(rotation) || magnitudeSquared <= 1e-12f)
            throw new InvalidOperationException(id + ": alignment rotation must be a finite non-zero quaternion.");
        if (alignment.pivot != null && !Finite(alignment.pivot.ToVector3()))
            throw new InvalidOperationException(id + ": alignment pivot must be finite.");
    }

    static Quaternion Normalize(Quaternion value)
    {
        float magnitude = Mathf.Sqrt(value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w);
        return new Quaternion(value.x / magnitude, value.y / magnitude, value.z / magnitude, value.w / magnitude);
    }

    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    static bool Finite(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);

    static void SetField<T>(Type optionsType, object options, string name, T value)
    {
        FieldInfo field = optionsType.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (field == null || field.FieldType != typeof(T))
            throw new MissingFieldException(optionsType.FullName, name);
        field.SetValue(options, value);
    }
}
