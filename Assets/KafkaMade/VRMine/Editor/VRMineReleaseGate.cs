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
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        BoardGameVerification.RunGate();

        StringBuilder report = new StringBuilder();
        int failures = 0;
        report.AppendLine("VRMine Upload Readiness");
        report.AppendLine("Unity: " + Application.unityVersion);
        report.AppendLine("BuildTarget: " + EditorUserBuildSettings.activeBuildTarget);
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
        failures += CheckReport(report, "G1Structure", StructureReport);
        failures += CheckReport(report, "G2RuntimeRules", RuntimeReport);
        failures += CheckReport(report, "G3TwoClientNetwork", NetworkReport);
        report.AppendLine("Result: " + (failures == 0 ? "PASS" : "BLOCKED"));
        report.AppendLine(failures == 0
            ? "The checked project state is eligible for VRChat SDK upload. Keep this report with the uploaded build."
            : "Upload is blocked. Run the missing gates; never interpret LAUNCHED or historical reports as PASS.");
        File.WriteAllText(ReleaseReport, report.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();
        Debug.Log(report.ToString());
    }

    static bool HasObjects(params string[] names)
    {
        for (int i = 0; i < names.Length; i++) if (GameObject.Find(names[i]) == null) return false;
        return true;
    }

    static int CheckReport(StringBuilder report, string name, string path)
    {
        if (!File.Exists(path)) return Check(report, name, false, "missing " + path);
        string content = File.ReadAllText(path);
        bool passed = content.Contains("Result: PASS");
        return Check(report, name, passed, passed ? path : "not PASS: " + path);
    }

    static int Check(StringBuilder report, string name, bool passed, string detail)
    {
        report.AppendLine((passed ? "PASS " : "FAIL ") + name + " " + detail);
        return passed ? 0 : 1;
    }
}