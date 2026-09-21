using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Editor;

public static class MinooRiverWorldBuildMeasurement
{
    const string EvidencePath = "Library/VRMine/minoo-river-build-evidence.json";

    [Serializable]
    sealed class BuildEvidence
    {
        public string scene;
        public string bundlePath;
        public long bundleBytes;
        public string sha256;
        public string sdkVersion;
        public string status;
    }

    public static async void MeasureBatch()
    {
        try
        {
            EditorSceneManager.OpenScene(MinooRiverWorldBuilder.ScenePath, OpenSceneMode.Single);
            VRCSdkControlPanel panel = EditorWindow.GetWindow<VRCSdkControlPanel>();
            var builder = new VRCSdkControlPanelWorldBuilder();
            builder.RegisterBuilder(panel);
            builder.Initialize();
            if (!builder.IsValidBuilder(out string message))
                throw new InvalidOperationException("VRChat world builder is not valid: " + message);
            builder.CreateValidationsGUI(new VisualElement());
            string bundlePath = await builder.Build();
            if (string.IsNullOrWhiteSpace(bundlePath)) throw new InvalidOperationException("VRChat SDK Build() returned an empty bundle path.");
            string absoluteBundlePath = Path.GetFullPath(bundlePath);
            if (!File.Exists(absoluteBundlePath)) throw new FileNotFoundException("VRChat SDK Build() returned a bundle path that does not exist.", absoluteBundlePath);
            long bundleBytes = new FileInfo(absoluteBundlePath).Length;
            if (bundleBytes <= 0) throw new InvalidOperationException("VRChat world bundle is empty.");
            var evidence = new BuildEvidence
            {
                scene = MinooRiverWorldBuilder.ScenePath,
                bundlePath = absoluteBundlePath,
                bundleBytes = bundleBytes,
                sha256 = ComputeSha256(absoluteBundlePath),
                sdkVersion = "3.9.0",
                status = "MEASURED_BUILD_COMPLETE"
            };
            Directory.CreateDirectory(Path.GetDirectoryName(EvidencePath));
            File.WriteAllText(EvidencePath, JsonUtility.ToJson(evidence, true));
            Debug.Log("Minoo River VRChat build evidence: scene=" + evidence.scene + ", bundleBytes=" + evidence.bundleBytes + ", sha256=" + evidence.sha256 + ", status=" + evidence.status);
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    static string ComputeSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using SHA256 sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
}
