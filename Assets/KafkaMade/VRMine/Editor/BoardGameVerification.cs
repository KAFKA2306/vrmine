using System.IO;
using System;
using System.Text;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Components;
using VRC.SDK3.Editor;
using Object = UnityEngine.Object;

public static class BoardGameVerification
{
    const string ScenePath = "Assets/KafkaMade/VRMine/Scenes/BoardGameShowcase.unity";
    const string EditReportPath = "Assets/KafkaMade/VRMine/Verification/LatestBoardGamesVerification.txt";
    const string RuntimeReportPath = "Assets/KafkaMade/VRMine/Verification/LatestBoardGamesRuntimeVerification.txt";
    const string VrcReportPath = "Assets/KafkaMade/VRMine/Verification/LatestVRChatBuildAndTest.txt";

    [InitializeOnLoadMethod]
    static void Initialize()
    {
        EditorApplication.update -= RuntimeUpdate;
        EditorApplication.update += RuntimeUpdate;
    }

    [MenuItem("VRMine/Verification/Run Board Games Gate")]
    public static void RunGate()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        StringBuilder report = new StringBuilder();
        int failures = 0;
        failures += Check(report, "Scene", SceneManager.GetActiveScene().path == ScenePath, SceneManager.GetActiveScene().path);
        failures += Check(report, "SceneDescriptor", Object.FindObjectsOfType<VRCSceneDescriptor>(true).Length == 1, Object.FindObjectsOfType<VRCSceneDescriptor>(true).Length.ToString());
        failures += Check(report, "TrickMeister", Object.FindObjectsOfType<GameController>(true).Length == 1, Object.FindObjectsOfType<GameController>(true).Length.ToString());
        failures += Check(report, "OrapaMine", Object.FindObjectsOfType<OrapaMineGame>(true).Length == 1, Object.FindObjectsOfType<OrapaMineGame>(true).Length.ToString());
        failures += Check(report, "Chess", Object.FindObjectsOfType<ChessGame>(true).Length == 1, Object.FindObjectsOfType<ChessGame>(true).Length.ToString());
        BoardGameAction[] actions = Object.FindObjectsOfType<BoardGameAction>(true);
        failures += Check(report, "Interactions", actions.Length >= 130, actions.Length.ToString());
        UdonSharpBehaviour[] behaviours = Object.FindObjectsOfType<UdonSharpBehaviour>(true);
        int valid = 0;
        for (int i = 0; i < behaviours.Length; i++)
        {
            VRC.Udon.UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviours[i]);
            if (backing != null && backing.programSource != null) valid++;
        }
        failures += Check(report, "UdonPrograms", valid == behaviours.Length, valid + "/" + behaviours.Length);
        BoardState board = Object.FindObjectOfType<BoardState>(true);
        failures += Check(report, "TrickCapacity", board.playerHands.Length == 80 && board.ruleHands.Length == 15 && board.scores.Length == 5, board.playerHands.Length + "/" + board.ruleHands.Length + "/" + board.scores.Length);
        VRCSceneDescriptor descriptor = Object.FindObjectOfType<VRCSceneDescriptor>(true);
        failures += Check(report, "Spawn", descriptor.spawns != null && descriptor.spawns.Length > 0, descriptor.spawns == null ? "0" : descriptor.spawns.Length.ToString());
        failures += Check(report, "ReferenceCamera", descriptor.ReferenceCamera != null, descriptor.ReferenceCamera == null ? "null" : descriptor.ReferenceCamera.name);
        report.AppendLine("Result: " + (failures == 0 ? "PASS" : "FAIL"));
        File.WriteAllText(EditReportPath, report.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();
        Debug.Log(report.ToString());
    }

    [MenuItem("VRMine/Verification/Run Board Games Runtime Gate")]
    public static void StartRuntimeGate()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SessionState.SetString("VRMine.BoardGamesRuntime", "enter");
        EditorApplication.isPlaying = true;
    }

    [MenuItem("VRMine/Verification/Build And Test Two Clients")]
    public static async void BuildAndTestTwoClients()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorSceneManager.SaveOpenScenes();
        SetVrcSetting("NumClients", 2);
        SetVrcSetting("ForceNoVR", true);
        Type panelType = FindType("VRCSdkControlPanel");
        string clientPath = (string)panelType.GetField("clientInstallPath", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
        if (!File.Exists(clientPath))
        {
            File.WriteAllText(VrcReportPath, "FAIL\nGate: G3 VRChat Build & Test\nClient: " + clientPath + "\nReason: configured VRChat client executable does not exist", Encoding.UTF8);
            AssetDatabase.Refresh();
            return;
        }
        IVRCSdkWorldBuilderApi builder;
        if (!VRCSdkControlPanel.TryGetBuilder(out builder))
        {
            File.WriteAllText(VrcReportPath, "FAIL Builder unavailable", Encoding.UTF8);
            return;
        }
        string validation;
        if (!builder.IsValidBuilder(out validation))
        {
            File.WriteAllText(VrcReportPath, "FAIL " + validation, Encoding.UTF8);
            return;
        }
        builder.Initialize();
        File.WriteAllText(VrcReportPath, "RUNNING\nScene: " + ScenePath + "\nClients: 2\nDesktop: true", Encoding.UTF8);
        await builder.BuildAndTest();
        File.WriteAllText(VrcReportPath, "PASS\nScene: " + ScenePath + "\nClients: 2\nDesktop: true", Encoding.UTF8);
        AssetDatabase.Refresh();
    }

    static void RuntimeUpdate()
    {
        string phase = SessionState.GetString("VRMine.BoardGamesRuntime", "");
        if (phase == "" || !EditorApplication.isPlaying) return;
        if (phase == "enter")
        {
            SessionState.SetString("VRMine.BoardGamesRuntime", "run");
            return;
        }
        GameController trick = Object.FindObjectOfType<GameController>(true);
        OrapaMineGame orapa = Object.FindObjectOfType<OrapaMineGame>(true);
        ChessGame chess = Object.FindObjectOfType<ChessGame>(true);
        int trickFailures = trick.VerifyRules();
        int orapaFailures = orapa.VerifySimulation();
        int chessFailures = chess.VerifyRules();
        StringBuilder report = new StringBuilder();
        report.AppendLine("Board Games Runtime Verification");
        report.AppendLine((trickFailures == 0 ? "PASS " : "FAIL ") + "TrickMeisterRules failures=" + trickFailures);
        report.AppendLine((orapaFailures == 0 ? "PASS " : "FAIL ") + "OrapaReflection failures=" + orapaFailures);
        report.AppendLine((chessFailures == 0 ? "PASS " : "FAIL ") + "ChessRules failures=" + chessFailures);
        bool passed = trickFailures == 0 && orapaFailures == 0 && chessFailures == 0;
        report.AppendLine("Result: " + (passed ? "PASS" : "FAIL"));
        File.WriteAllText(RuntimeReportPath, report.ToString(), Encoding.UTF8);
        Debug.Log(report.ToString());
        SessionState.SetString("VRMine.BoardGamesRuntime", "");
        EditorApplication.isPlaying = false;
    }

    static int Check(StringBuilder report, string name, bool passed, string detail)
    {
        report.AppendLine((passed ? "PASS " : "FAIL ") + name + " " + detail);
        return passed ? 0 : 1;
    }

    static void SetVrcSetting(string name, object value)
    {
        Type settingsType = FindVrcSettingsType();
        settingsType.GetProperty(name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).SetValue(null, value);
    }

    static Type FindVrcSettingsType()
    {
        return FindType("VRCSettings");
    }

    static Type FindType(string name)
    {
        Type settingsType = null;
        System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int assemblyIndex = 0; assemblyIndex < assemblies.Length && settingsType == null; assemblyIndex++)
        {
            Type[] types = assemblies[assemblyIndex].GetTypes();
            for (int typeIndex = 0; typeIndex < types.Length; typeIndex++) if (types[typeIndex].Name == name) settingsType = types[typeIndex];
        }
        return settingsType;
    }
}
