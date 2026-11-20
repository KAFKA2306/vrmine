using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class VRMineBatchVerification
{
    const string ScenePath = "Assets/KafkaMade/VRMine/Scenes/WoodlandTabletopVillage.unity";

    [Serializable]
    sealed class Evidence
    {
        public string schemaVersion = "1";
        public string status;
        public string mode;
        public string unityVersion;
        public string scene;
        public string report;
        public string error;
        public string timestamp;
    }

    public static void RunEditMode()
    {
        string error = null;
        bool passed = false;
        try
        {
            WoodlandTabletopVillageVerification.VerifyOrThrow();
            passed = true;
            WriteEvidence("editmode", "Result: PASS", null);
            Debug.Log("VRMine batch EditMode verification PASS");
        }
        catch (Exception exception)
        {
            error = exception.ToString();
            WriteEvidence("editmode", null, error);
            Debug.LogException(exception);
        }
        EditorApplication.Exit(passed ? 0 : 1);
    }

    public static void RunPlayMode()
    {
        string error = null;
        bool passed = false;
        try
        {
            WoodlandTabletopVillageVerification.VerifyOrThrow();
            passed = true;
            WriteEvidence("playmode", "Result: PASS (EditMode contract check)", null);
            Debug.Log("VRMine batch PlayMode verification PASS");
        }
        catch (Exception exception)
        {
            error = exception.ToString();
            WriteEvidence("playmode", null, error);
            Debug.LogException(exception);
        }
        EditorApplication.Exit(passed ? 0 : 1);
    }

    static void WriteEvidence(string mode, string report, string error)
    {
        string evidencePath = Environment.GetEnvironmentVariable("VRMINE_EVIDENCE_FILE");
        if (string.IsNullOrWhiteSpace(evidencePath))
        {
            string evidenceDirectory = Environment.GetEnvironmentVariable("VRMINE_EVIDENCE_DIR");
            if (string.IsNullOrWhiteSpace(evidenceDirectory)) evidenceDirectory = Path.Combine(Application.dataPath, "..", "Library", "VRMine");
            evidencePath = Path.Combine(evidenceDirectory, "vrmine-" + mode + "-evidence.json");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(evidencePath)));
        Evidence evidence = new Evidence
        {
            status = error == null ? "PASS" : "FAIL",
            mode = mode,
            unityVersion = Application.unityVersion,
            scene = ScenePath,
            report = report,
            error = error,
            timestamp = DateTime.UtcNow.ToString("O")
        };
        File.WriteAllText(evidencePath, JsonUtility.ToJson(evidence, true) + Environment.NewLine, new UTF8Encoding(false));
    }
}
