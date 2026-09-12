using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class WoodlandTabletopVillageAudioBuild
{
    const string SpecPath = "config/world-design/generated/woodland-tabletop-village-v0.json";
    const string RootName = "WoodlandTabletopVillage";
    const string AudioRootName = "EnvironmentAudio";
    const string AudioFolder = "Assets/KafkaMade/VRMine/Audio/WoodlandTabletopVillage";

    [Serializable] class Spec
    {
        public Spatial spatial_geometry;
        public RetreatProtocol retreat_protocol;
        public Soundscape soundscape;
    }

    [Serializable] class Spatial
    {
        public float overall_width_m;
        public float overall_depth_m;
        public float corridor_clearance_m;
    }

    [Serializable] class RetreatProtocol
    {
        public float acoustic_attenuation_db;
    }

    [Serializable] class Soundscape
    {
        public bool music_required;
        public string[] layers;
        public string rule;
    }

    public static void MaterializeAndVerifyScene()
    {
        Spec spec = LoadSpec();
        ValidateCanonical(spec);

        GameObject root = GameObject.Find(RootName);
        Require(root != null, "generated scene root is missing");

        Transform existing = Find(root.transform, AudioRootName);
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

        Transform audioRoot = new GameObject(AudioRootName).transform;
        audioRoot.SetParent(root.transform, false);

        EnsureFolder(AudioFolder);
        float volumeCap = VolumeCap(spec.retreat_protocol.acoustic_attenuation_db);
        float sourceVolume = volumeCap * 0.8f;
        float minDistance = Mathf.Max(0.1f, spec.spatial_geometry.corridor_clearance_m * 0.5f);
        float maxDistance = Mathf.Max(spec.spatial_geometry.overall_width_m, spec.spatial_geometry.overall_depth_m);

        for (int i = 0; i < spec.soundscape.layers.Length; i++)
        {
            string token = spec.soundscape.layers[i];
            string assetPath = AudioFolder + "/" + token + ".wav";
            WriteAmbientWav(assetPath, token, i);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            Require(clip != null, "generated audio clip could not be imported: " + token);

            GameObject go = new GameObject("Audio_" + token);
            go.transform.SetParent(audioRoot, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = sourceVolume;
            source.loop = true;
            source.playOnAwake = true;
            source.spatialBlend = 1.0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
        }

        AssetDatabase.SaveAssets();
        VerifyScene(spec);
        VerifyNegativeVolumeFixture(spec);
    }

    public static void VerifyPrefab(GameObject prefab)
    {
        Require(prefab != null, "prefab is missing for audio verification");
        Spec spec = LoadSpec();
        ValidateCanonical(spec);
        VerifySources(prefab.GetComponentsInChildren<AudioSource>(true), spec, "prefab");
    }

    static void VerifyScene(Spec spec)
    {
        GameObject root = GameObject.Find(RootName);
        Require(root != null, "scene root is missing during audio verification");
        VerifySources(root.GetComponentsInChildren<AudioSource>(true), spec, "scene");
    }

    static void VerifySources(AudioSource[] sources, Spec spec, string scope)
    {
        string[] layers = spec.soundscape.layers;
        Require(sources != null && sources.Length >= 1, scope + " must contain environment AudioSource components");
        Require(sources.Length == layers.Length, scope + " AudioSource count must equal canonical soundscape layer count");

        float volumeCap = VolumeCap(spec.retreat_protocol.acoustic_attenuation_db);
        float expectedMinDistance = Mathf.Max(0.1f, spec.spatial_geometry.corridor_clearance_m * 0.5f);
        float expectedMaxDistance = Mathf.Max(spec.spatial_geometry.overall_width_m, spec.spatial_geometry.overall_depth_m);

        foreach (string token in layers)
        {
            AudioSource source = sources.SingleOrDefault(s => s.name == "Audio_" + token);
            Require(source != null, scope + " is missing canonical soundscape layer: " + token);
            Require(source.clip != null, scope + " layer has no clip/reference: " + token);
            Require(source.loop, scope + " layer must loop its sparse ambient clip: " + token);
            Require(source.playOnAwake, scope + " layer must be present at blockout runtime: " + token);
            Require(Mathf.Abs(source.spatialBlend - 1.0f) < 0.001f, scope + " layer must be fully spatialized: " + token);
            Require(source.rolloffMode == AudioRolloffMode.Linear, scope + " layer must use deterministic linear attenuation: " + token);
            Require(Mathf.Abs(source.minDistance - expectedMinDistance) < 0.001f, scope + " minDistance drifted from corridor clearance: " + token);
            Require(Mathf.Abs(source.maxDistance - expectedMaxDistance) < 0.001f, scope + " maxDistance drifted from room bounds: " + token);
            Require(source.volume > 0f && source.volume <= volumeCap + 0.0001f, scope + " layer exceeds voice-chat coexistence cap derived from acoustic attenuation: " + token);
        }
    }

    static void VerifyNegativeVolumeFixture(Spec spec)
    {
        AudioSource source = UnityEngine.Object.FindObjectsOfType<AudioSource>().FirstOrDefault();
        Require(source != null, "negative audio fixture needs one AudioSource");
        float original = source.volume;
        source.volume = Mathf.Min(1.0f, VolumeCap(spec.retreat_protocol.acoustic_attenuation_db) + 0.25f);
        bool failed = false;
        try { VerifyScene(spec); }
        catch (InvalidOperationException) { failed = true; }
        finally { source.volume = original; }
        Require(failed, "over-volume negative fixture must fail audio verification");
        VerifyScene(spec);
    }

    static float VolumeCap(float attenuationDb)
    {
        Require(attenuationDb < 0f, "acoustic attenuation must be negative dB");
        return Mathf.Pow(10f, attenuationDb / 20f);
    }

    static void ValidateCanonical(Spec spec)
    {
        Require(spec != null && spec.spatial_geometry != null && spec.retreat_protocol != null && spec.soundscape != null, "canonical audio dependencies are missing");
        Require(spec.soundscape.layers != null && spec.soundscape.layers.Length > 0, "canonical soundscape layers are empty");
        Require(spec.soundscape.layers.All(x => !string.IsNullOrWhiteSpace(x)), "canonical soundscape contains an empty layer token");
        Require(spec.soundscape.layers.Distinct().Count() == spec.soundscape.layers.Length, "canonical soundscape layer tokens must be unique");
        Require(!spec.soundscape.music_required, "WORLD_BLOCKOUT audio contract does not require music");
        Require(!string.IsNullOrWhiteSpace(spec.soundscape.rule), "canonical soundscape silence rule is missing");
        Require(spec.spatial_geometry.overall_width_m > 0f && spec.spatial_geometry.overall_depth_m > 0f && spec.spatial_geometry.corridor_clearance_m > 0f, "canonical spatial values required for audio attenuation are invalid");
    }

    static Spec LoadSpec()
    {
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", SpecPath));
        Require(File.Exists(path), "canonical spec is missing");
        return JsonUtility.FromJson<Spec>(File.ReadAllText(path));
    }

    static void WriteAmbientWav(string assetPath, string token, int index)
    {
        string absolute = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        Directory.CreateDirectory(Path.GetDirectoryName(absolute));
        const int sampleRate = 22050;
        const int seconds = 2;
        int samples = sampleRate * seconds;
        short[] pcm = new short[samples];
        int seed = token.Aggregate(17, (acc, c) => acc * 31 + c) ^ (index * 7919);
        System.Random rng = new System.Random(seed);
        double phase = 0.0;
        double frequency = 80.0 + index * 37.0;
        for (int i = 0; i < samples; i++)
        {
            phase += 2.0 * Math.PI * frequency / sampleRate;
            double sparse = (i % (sampleRate / 2) < sampleRate / 18) ? Math.Sin(phase) * 0.22 : 0.0;
            double noise = (rng.NextDouble() * 2.0 - 1.0) * 0.035;
            pcm[i] = (short)Mathf.Clamp((float)((sparse + noise) * short.MaxValue), short.MinValue, short.MaxValue);
        }

        using (FileStream fs = new FileStream(absolute, FileMode.Create, FileAccess.Write))
        using (BinaryWriter bw = new BinaryWriter(fs))
        {
            int dataSize = pcm.Length * 2;
            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + dataSize);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1);
            bw.Write((short)1);
            bw.Write(sampleRate);
            bw.Write(sampleRate * 2);
            bw.Write((short)2);
            bw.Write((short)16);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(dataSize);
            foreach (short sample in pcm) bw.Write(sample);
        }
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
        if (!condition) throw new InvalidOperationException("Woodland Tabletop Village audio verification FAIL: " + message);
    }
}
