using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Components;

public static class VRMineReleaseGate
{
    const string ScenePath = "Assets/KafkaMade/VRMine/Scenes/BoardGameShowcase.unity";
    const string StructureReport = "Assets/KafkaMade/VRMine/Verification/LatestBoardGamesVerification.txt";
    const string RuntimeReport = "Assets/KafkaMade/VRMine/Verification/LatestBoardGamesRuntimeVerification.txt";
    const string NetworkReport = "Assets/KafkaMade/VRMine/Verification/LatestVRChatBuildAndTest.txt";
    const string ReleaseReport = "Assets/KafkaMade/VRMine/Verification/LatestUploadReadiness.txt";

    [MenuItem("VRMine/Release/Validate Upload Readiness")]
    public static void ValidateUploadReadiness()
    {
        if (!File.Exists(ScenePath)) BoardGameShowcaseBuilder.Build();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        BoardGameSceneUpgrade.EnsurePlayerControls();
        VRMinePublicNameUpgrade.Apply();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        BoardGameVerification.RunGate();

        DateTime sourceCutoffUtc = LatestRelevantSourceWriteUtc();
        StringBuilder report = new StringBuilder();
        int failures = 0;
        report.AppendLine("VRMine Upload Readiness");
        report.AppendLine("Unity: " + Application.unityVersion);
        report.AppendLine("BuildTarget: " + EditorUserBuildSettings.activeBuildTarget);
        report.AppendLine("EvidenceSourceCutoffUtc: " + sourceCutoffUtc.ToString("O"));
        failures += Check(report, "UnityVersion", Application.unityVersion == "2022.3.22f1", Application.unityVersion);
        failures += Check(report, "WindowsBuildTarget", EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64, EditorUserBuildSettings.activeBuildTarget.ToString());
        failures += Check(report, "WorldSdk3104", File.ReadAllText("Packages/manifest.json").Contains("\"com.vrchat.worlds\": \"3.10.4\""), "Packages/manifest.json");
        failures += Check(report, "SceneDescriptor", Object.FindObjectOfType<VRCSceneDescriptor>(true) != null, ScenePath);
        failures += Check(report, "TrickManager", Object.FindObjectsOfType<GameController>(true).Length == 1, Object.FindObjectsOfType<GameController>(true).Length.ToString());
        failures += Check(report, "OrapaManager", Object.FindObjectsOfType<OrapaMineGame>(true).Length == 1, Object.FindObjectsOfType<OrapaMineGame>(true).Length.ToString());
        failures += Check(report, "ChessManager", Object.FindObjectsOfType<ChessGame>(true).Length == 1, Object.FindObjectsOfType<ChessGame>(true).Length.ToString());
        failures += Check(report, "NetworkProbe", Object.FindObjectsOfType<NetworkVerificationProbe>(true).Length == 1, Object.FindObjectsOfType<NetworkVerificationProbe>(true).Length.ToString());
        failures += Check(report, "TrickSeatLifecycle", Object.FindObjectsOfType<TrickSeatLifecycle>(true).Length == 1, Object.FindObjectsOfType<TrickSeatLifecycle>(true).Length.ToString());
        failures += Check(report, "TrickPlayerCountControls", HasObjects("TrickPlayerCount_3", "TrickPlayerCount_4", "TrickPlayerCount_5"), "3P/4P/5P");
        failures += Check(report, "OrapaPlayerCountControls", HasObjects("OrapaPlayerCount_2", "OrapaPlayerCount_3", "OrapaPlayerCount_4", "OrapaPlayerCount_5"), "2P/3P/4P/5P");
        failures += Check(report, "TrickTableObjects", HasObjects("TrickTableCard_0", "TrickTableCard_1", "TrickTableCard_2", "TrickTableCard_3", "TrickTableCard_4"), "five synchronized card displays");
        BoardGameShowcaseView showcase = Object.FindObjectOfType<BoardGameShowcaseView>(true);
        failures += Check(report, "TrickTableWiring", HasTrickTableWiring(showcase), showcase == null ? "missing view" : "view array");
        failures += Check(report, "PublicGameNames", HasLabel("RULEFORGE") && HasLabel("ECHO MINE"), "RULEFORGE / ECHO MINE");
        failures += CheckReport(report, "G1Structure", StructureReport, sourceCutoffUtc, false);
        failures += CheckReport(report, "G2RuntimeRules", RuntimeReport, sourceCutoffUtc, false);
        failures += CheckReport(report, "G3TwoClientNetwork", NetworkReport, sourceCutoffUtc, true);
        report.AppendLine("Result: " + (failures == 0 ? "PASS" : "BLOCKED"));
        report.AppendLine(failures == 0
            ? "The checked project state is eligible for private VRChat SDK upload. Complete the recorded private-instance smoke test before any public release claim."
            : "Upload is blocked. Run the missing or stale gates; never interpret LAUNCHED or historical reports as PASS.");
        File.WriteAllText(ReleaseReport, report.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();
        Debug.Log(report.ToString());
    }

    static DateTime LatestRelevantSourceWriteUtc()
    {
        DateTime latest = DateTime.MinValue;
        UpdateLatest(ref latest, "Packages/manifest.json");
        UpdateLatest(ref latest, "Packages/vpm-manifest.json");
        UpdateLatest(ref latest, "ProjectSettings/ProjectVersion.txt");
        UpdateLatest(ref latest, ScenePath);
        UpdateLatestInDirectory(ref latest, "Assets/KafkaMade/VRMine/Runtime", "*.cs");
        UpdateLatestInDirectory(ref latest, "Assets/KafkaMade/VRMine/Editor", "*.cs");
        return latest;
    }

    static void UpdateLatestInDirectory(ref DateTime latest, string directory, string pattern)
    {
        if (!Directory.Exists(directory)) return;
        string[] files = Directory.GetFiles(directory, pattern, SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; i++) UpdateLatest(ref latest, files[i]);
    }

    static void UpdateLatest(ref DateTime latest, string path)
    {
        if (!File.Exists(path)) return;
        DateTime value = File.GetLastWriteTimeUtc(path);
        if (value > latest) latest = value;
    }

    static bool HasLabel(string expected)
    {
        UnityEngine.UI.Text[] labels = Object.FindObjectsOfType<UnityEngine.UI.Text>(true);
        for (int i = 0; i < labels.Length; i++) if (labels[i].text == expected) return true;
        return false;
    }

    static bool HasTrickTableWiring(BoardGameShowcaseView view)
    {
        if (view == null || view.trickTableCards == null || view.trickTableCards.Length != NetConst.MaxPlayers) return false;
        for (int i = 0; i < view.trickTableCards.Length; i++) if (view.trickTableCards[i] == null) return false;
        return true;
    }

    static bool HasObjects(params string[] names)
    {
        for (int i = 0; i < names.Length; i++) if (GameObject.Find(names[i]) == null) return false;
        return true;
    }

    static int CheckReport(StringBuilder report, string name, string path, DateTime sourceCutoffUtc, bool requireNetworkFields)
    {
        if (!File.Exists(path)) return Check(report, name, false, "missing " + path);
        string content = File.ReadAllText(path);
        DateTime reportWriteUtc = File.GetLastWriteTimeUtc(path);
        bool passed = content.Contains("Result: PASS");
        bool fresh = reportWriteUtc >= sourceCutoffUtc.AddSeconds(-2);
        bool fields = !requireNetworkFields || content.Contains("RunToken: ")
            && content.Contains("StartedUtc: ")
            && content.Contains("LateJoin: NOT_AUTOMATED");
        string detail = path + " write=" + reportWriteUtc.ToString("O")
            + " pass=" + passed + " fresh=" + fresh + " fields=" + fields;
        return Check(report, name, passed && fresh && fields, detail);
    }

    static int Check(StringBuilder report, string name, bool passed, string detail)
    {
        report.AppendLine((passed ? "PASS " : "FAIL ") + name + " " + detail);
        return passed ? 0 : 1;
    }
}