using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VRMineBatchVerification
{
    const string ScenePath = "Assets/KafkaMade/VRMine/Scenes/BoardGameShowcase.unity";
    const string RuntimePhaseKey = "VRMine.BoardGamesRuntime";
    const string PlayModeStartKey = "VRMine.BoardGamesBatchStart";
    const string EditReportPath = "Assets/KafkaMade/VRMine/Verification/LatestBoardGamesVerification.txt";
    const string RuntimeReportPath = "Assets/KafkaMade/VRMine/Verification/LatestBoardGamesRuntimeVerification.txt";
    static double playModeStartedAt;

    [InitializeOnLoadMethod]
    static void Initialize()
    {
        EditorApplication.update -= FinishPlayMode;
        EditorApplication.update += FinishPlayMode;
    }

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
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            BoardGameVerification.RunGate();
            string report = ReadReport(EditReportPath);
            passed = report.Contains("Result: PASS", StringComparison.Ordinal);
            if (!passed) error = "BoardGameVerification.RunGate did not produce Result: PASS";
            WriteEvidence("editmode", report, error);
            Debug.Log("VRMine batch EditMode verification " + (passed ? "PASS" : "FAIL"));
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
        try
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SessionState.SetString(RuntimePhaseKey, "enter");
            playModeStartedAt = EditorApplication.timeSinceStartup;
            SessionState.SetString(PlayModeStartKey, playModeStartedAt.ToString(System.Globalization.CultureInfo.InvariantCulture));
            EditorApplication.update -= FinishPlayMode;
            EditorApplication.update += FinishPlayMode;
            EditorApplication.isPlaying = true;
        }
        catch (Exception exception)
        {
            SessionState.SetString(RuntimePhaseKey, "");
            SessionState.SetString(PlayModeStartKey, "");
            WriteEvidence("playmode", null, exception.ToString());
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    static void FinishPlayMode()
    {
        string startText = SessionState.GetString(PlayModeStartKey, "");
        if (string.IsNullOrEmpty(startText)) return;
        double start = playModeStartedAt;
        if (double.TryParse(startText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double persistedStart)) start = persistedStart;
        if (EditorApplication.timeSinceStartup - start > 120d)
        {
            EditorApplication.update -= FinishPlayMode;
            SessionState.SetString(RuntimePhaseKey, "");
            SessionState.SetString(PlayModeStartKey, "");
            EditorApplication.isPlaying = false;
            WriteEvidence("playmode", null, "VRMine PlayMode verification timed out after 120 seconds");
            EditorApplication.Exit(1);
            return;
        }

        if (EditorApplication.isPlaying) return;
        if (SessionState.GetString(RuntimePhaseKey, "") != "") return;

        EditorApplication.update -= FinishPlayMode;
        SessionState.SetString(PlayModeStartKey, "");
        string report = ReadReport(RuntimeReportPath);
        bool passed = report.Contains("Result: PASS", StringComparison.Ordinal);
        string error = passed ? null : "BoardGameVerification runtime gate did not produce Result: PASS";
        WriteEvidence("playmode", report, error);
        Debug.Log("VRMine batch PlayMode verification " + (passed ? "PASS" : "FAIL"));
        EditorApplication.Exit(passed ? 0 : 1);
    }

    static string ReadReport(string relativePath)
    {
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
        return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;
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
