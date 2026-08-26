using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

public static class GlbConsumerVerification
{
    const string ImportedAssetPath = "Assets/KafkaMade/VRMine/Verification/consumer-proof.glb";
    const string EvidencePath = "Library/VRMine/glb-consumer-evidence.json";

    [Serializable]
    sealed class Evidence
    {
        public string sourcePath;
        public string sourceSha256;
        public long sourceBytes;
        public string importedAssetPath;
        public int meshCount;
        public int vertexCount;
        public int triangleCount;
        public int materialCount;
        public Vector3 boundsMin;
        public Vector3 boundsMax;
        public string unityVersion;
        public string status;
    }

    public static void VerifyBatch()
    {
        string sourcePath = Environment.GetEnvironmentVariable("VRMINE_GLB_PATH");
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            throw new InvalidOperationException("VRMINE_GLB_PATH must point to an existing GLB file.");

        string expectedSha = Environment.GetEnvironmentVariable("VRMINE_GLB_SHA256");
        string actualSha = Sha256(sourcePath);
        if (!string.IsNullOrEmpty(expectedSha) && !string.Equals(expectedSha, actualSha, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("GLB SHA-256 mismatch: expected=" + expectedSha + ", actual=" + actualSha);

        string directory = Path.GetDirectoryName(ImportedAssetPath);
        Directory.CreateDirectory(directory);
        File.Copy(sourcePath, ImportedAssetPath, true);
        AssetDatabase.ImportAsset(ImportedAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        GameObject imported = AssetDatabase.LoadAssetAtPath<GameObject>(ImportedAssetPath);
        if (imported == null)
            throw new InvalidOperationException("UnityGLTF did not produce a GameObject main asset for " + ImportedAssetPath);

        MeshFilter[] filters = imported.GetComponentsInChildren<MeshFilter>(true);
        SkinnedMeshRenderer[] skinned = imported.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        Mesh[] meshes = filters.Select(value => value.sharedMesh)
            .Concat(skinned.Select(value => value.sharedMesh))
            .Where(value => value != null)
            .Distinct()
            .ToArray();
        if (meshes.Length == 0) throw new InvalidOperationException("Imported GLB contains no Unity meshes.");

        int vertices = meshes.Sum(mesh => mesh.vertexCount);
        int triangles = meshes.Sum(mesh => mesh.triangles.Length / 3);
        int materials = imported.GetComponentsInChildren<Renderer>(true)
            .SelectMany(renderer => renderer.sharedMaterials)
            .Where(material => material != null)
            .Distinct()
            .Count();

        bool hasBounds = false;
        Bounds bounds = new Bounds();
        foreach (MeshFilter filter in filters)
        {
            if (filter.sharedMesh == null) continue;
            Bounds world = TransformBounds(filter.transform, filter.sharedMesh.bounds);
            if (!hasBounds) { bounds = world; hasBounds = true; }
            else bounds.Encapsulate(world);
        }
        foreach (SkinnedMeshRenderer renderer in skinned)
        {
            if (renderer.sharedMesh == null) continue;
            Bounds world = TransformBounds(renderer.transform, renderer.sharedMesh.bounds);
            if (!hasBounds) { bounds = world; hasBounds = true; }
            else bounds.Encapsulate(world);
        }
        if (!hasBounds) throw new InvalidOperationException("Imported GLB bounds are unavailable.");

        int expectedVertices = ReadExpectedInt("VRMINE_GLB_VERTEX_COUNT");
        int expectedTriangles = ReadExpectedInt("VRMINE_GLB_TRIANGLE_COUNT");
        if (expectedVertices >= 0 && vertices != expectedVertices)
            throw new InvalidOperationException("Unity vertex count drift: expected=" + expectedVertices + ", actual=" + vertices);
        if (expectedTriangles >= 0 && triangles != expectedTriangles)
            throw new InvalidOperationException("Unity triangle count drift: expected=" + expectedTriangles + ", actual=" + triangles);

        var evidence = new Evidence
        {
            sourcePath = sourcePath,
            sourceSha256 = actualSha,
            sourceBytes = new FileInfo(sourcePath).Length,
            importedAssetPath = ImportedAssetPath,
            meshCount = meshes.Length,
            vertexCount = vertices,
            triangleCount = triangles,
            materialCount = materials,
            boundsMin = bounds.min,
            boundsMax = bounds.max,
            unityVersion = Application.unityVersion,
            status = "PASS"
        };
        Directory.CreateDirectory(Path.GetDirectoryName(EvidencePath));
        File.WriteAllText(EvidencePath, JsonUtility.ToJson(evidence, true));
        Debug.Log("GLB consumer verification PASS: sha256=" + actualSha + ", meshes=" + meshes.Length + ", vertices=" + vertices + ", triangles=" + triangles + ", materials=" + materials + ", bounds=" + bounds + ", evidence=" + EvidencePath);
    }

    static int ReadExpectedInt(string name)
    {
        string value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(value)) return -1;
        int parsed;
        if (!int.TryParse(value, out parsed) || parsed < 0) throw new InvalidOperationException(name + " must be a non-negative integer.");
        return parsed;
    }

    static string Sha256(string path)
    {
        using (FileStream stream = File.OpenRead(path))
        using (SHA256 hash = SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    static Bounds TransformBounds(Transform transform, Bounds local)
    {
        Vector3 center = transform.TransformPoint(local.center);
        Vector3 extents = local.extents;
        var result = new Bounds(center, Vector3.zero);
        for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                    result.Encapsulate(transform.TransformPoint(local.center + Vector3.Scale(extents, new Vector3(x, y, z))));
        return result;
    }
}
