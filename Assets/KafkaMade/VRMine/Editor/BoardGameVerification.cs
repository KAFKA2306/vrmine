using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
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
        PrepareReleaseScene();
        StringBuilder report = new StringBuilder();
        int failures = 0;
        failures += Check(report, "Scene", SceneManager.GetActiveScene().path == ScenePath, SceneManager.GetActiveScene().path);
        failures += Check(report, "SceneDescriptor", Object.FindObjectsOfType<VRCSceneDescriptor>(true).Length == 1, Object.FindObjectsOfType<VRCSceneDescriptor>(true).Length.ToString());
        failures += Check(report, "RuleForge", Object.FindObjectsOfType<GameController>(true).Length == 1, Object.FindObjectsOfType<GameController>(true).Length.ToString());
        failures += Check(report, "EchoMine", Object.FindObjectsOfType<OrapaMineGame>(true).Length == 1, Object.FindObjectsOfType<OrapaMineGame>(true).Length.ToString());
        failures += Check(report, "Chess", Object.FindObjectsOfType<ChessGame>(true).Length == 1, Object.FindObjectsOfType<ChessGame>(true).Length.ToString());
        failures += Check(report, "NetworkProbe", Object.FindObjectsOfType<NetworkVerificationProbe>(true).Length == 1, Object.FindObjectsOfType<NetworkVerificationProbe>(true).Length.ToString());
        failures += Check(report, "TrickSeatLifecycle", Object.FindObjectsOfType<TrickSeatLifecycle>(true).Length == 1, Object.FindObjectsOfType<TrickSeatLifecycle>(true).Length.ToString());

        BoardGameAction[] actions = Object.FindObjectsOfType<BoardGameAction>(true);
        failures += Check(report, "Interactions", actions.Length >= 152, actions.Length.ToString());
        UdonSharpBehaviour[] behaviours = Object.FindObjectsOfType<UdonSharpBehaviour>(true);
        int valid = 0;
        for (int i = 0; i < behaviours.Length; i++)
        {
            VRC.Udon.UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviours[i]);
            if (backing != null && backing.programSource != null) valid++;
        }
        failures += Check(report, "UdonPrograms", valid == behaviours.Length, valid + "/" + behaviours.Length);

        BoardState board = Object.FindObjectOfType<BoardState>(true);
        failures += Check(report, "TrickCapacity", board != null && board.playerHands.Length == 80 && board.ruleHands.Length == 15 && board.scores.Length == 5,
            board == null ? "missing" : board.playerHands.Length + "/" + board.ruleHands.Length + "/" + board.scores.Length);
        VRCSceneDescriptor descriptor = Object.FindObjectOfType<VRCSceneDescriptor>(true);
        failures += Check(report, "Spawn", descriptor != null && descriptor.spawns != null && descriptor.spawns.Length > 0, descriptor == null || descriptor.spawns == null ? "0" : descriptor.spawns.Length.ToString());
        failures += Check(report, "ReferenceCamera", descriptor != null && descriptor.ReferenceCamera != null, descriptor == null || descriptor.ReferenceCamera == null ? "null" : descriptor.ReferenceCamera.name);
        failures += Check(report, "PlayerCountControls", HasObjects(
            "TrickPlayerCount_3", "TrickPlayerCount_4", "TrickPlayerCount_5",
            "OrapaPlayerCount_2", "OrapaPlayerCount_3", "OrapaPlayerCount_4", "OrapaPlayerCount_5"), "7 controls");
        failures += Check(report, "TrickTableObjects", HasObjects(
            "TrickTableCard_0", "TrickTableCard_1", "TrickTableCard_2", "TrickTableCard_3", "TrickTableCard_4"), "5 displays");
        failures += Check(report, "PublicGameNames", HasLabel("RULEFORGE") && HasLabel("ECHO MINE"), "RULEFORGE / ECHO MINE");
        report.AppendLine("Result: " + (failures == 0 ? "PASS" : "FAIL"));
        WriteReport(EditReportPath, report.ToString());
    }

    [MenuItem("VRMine/Verification/Run Board Games Runtime Gate")]
    public static void StartRuntimeGate()
    {
        PrepareReleaseScene();
        SessionState.SetString("VRMine.BoardGamesRuntime", "enter");
        EditorApplication.isPlaying = true;
    }

    [MenuItem("VRMine/Verification/Build And Test Two Clients")]
    public static async void BuildAndTestTwoClients()
    {
        PrepareReleaseScene();
        SetVrcSetting("NumClients", 2);
        SetVrcSetting("ForceNoVR", true);

        NetworkVerificationProbe probe = Object.FindObjectOfType<NetworkVerificationProbe>(true);
        if (probe == null)
        {
            WriteReport(VrcReportPath, "FAIL\nGate: G3 VRChat Build & Test\nReason: NetworkVerificationProbe is missing");
            return;
        }
        int runToken = GenerateRunToken();
        probe.runToken = runToken;
        probe.sequence = 0;
        probe.phase = 0;
        probe.firstPlayerId = 0;
        probe.secondPlayerId = 0;
        probe.publishedOwnerId = 0;
        EditorUtility.SetDirty(probe);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        string clientPath = ResolveVrChatClientPath(GetConfiguredClientPath());
        if (string.IsNullOrEmpty(clientPath) || !File.Exists(clientPath))
        {
            WriteReport(VrcReportPath, "FAIL\nGate: G3 VRChat Build & Test\nRunToken: " + runToken
                + "\nReason: VRChat client executable was not found\nChecked configured path and Steam library locations");
            return;
        }
        SetConfiguredClientPath(clientPath);

        IVRCSdkWorldBuilderApi builder;
        if (!VRCSdkControlPanel.TryGetBuilder(out builder))
        {
            WriteReport(VrcReportPath, "FAIL\nGate: G3 VRChat Build & Test\nRunToken: " + runToken + "\nReason: world builder unavailable");
            return;
        }
        string validation;
        if (!builder.IsValidBuilder(out validation))
        {
            WriteReport(VrcReportPath, "FAIL\nGate: G3 VRChat Build & Test\nRunToken: " + runToken + "\nReason: " + validation);
            return;
        }

        builder.Initialize();
        DateTime started = DateTime.UtcNow;
        WriteReport(VrcReportPath,
            "RUNNING\nGate: G3 VRChat Build & Test\nScene: " + ScenePath
            + "\nClients: 2\nDesktop: true\nClient: " + clientPath
            + "\nRunToken: " + runToken
            + "\nStartedUtc: " + started.ToString("O")
            + "\nPASS is not granted until matching client logs are finalized.");
        try
        {
            await builder.BuildAndTest();
            WriteReport(VrcReportPath,
                "LAUNCHED\nGate: G3 VRChat Build & Test\nScene: " + ScenePath
                + "\nClients: 2\nClient: " + clientPath
                + "\nRunToken: " + runToken
                + "\nStartedUtc: " + started.ToString("O")
                + "\nNext: VRMine/Verification/Finalize Two Client Logs");
        }
        catch (Exception exception)
        {
            WriteReport(VrcReportPath, "FAIL\nGate: G3 VRChat Build & Test\nRunToken: " + runToken + "\nException: " + exception);
        }
    }

    [MenuItem("VRMine/Verification/Finalize Two Client Logs")]
    public static void FinalizeTwoClientLogs()
    {
        string launchReport = File.Exists(VrcReportPath) ? File.ReadAllText(VrcReportPath) : "";
        int expectedRunToken;
        DateTime startedUtc;
        if (!TryReadIntField(launchReport, "RunToken", out expectedRunToken) || expectedRunToken <= 0
            || !TryReadUtcField(launchReport, "StartedUtc", out startedUtc))
        {
            WriteReport(VrcReportPath,
                "FAIL\nGate: G3 VRChat Two-Client Evidence\nReason: current launch report has no valid RunToken and StartedUtc; run Build And Test Two Clients again\nResult: FAIL");
            return;
        }

        string logDirectory = VrChatLogDirectory();
        StringBuilder report = new StringBuilder();
        report.AppendLine("G3 VRChat Two-Client Evidence");
        report.AppendLine("RunToken: " + expectedRunToken);
        report.AppendLine("StartedUtc: " + startedUtc.ToString("O"));
        report.AppendLine("LogDirectory: " + logDirectory);
        if (!Directory.Exists(logDirectory))
        {
            report.AppendLine("Reason: VRChat log directory does not exist");
            report.AppendLine("Result: FAIL");
            WriteReport(VrcReportPath, report.ToString());
            return;
        }

        DateTime cutoff = startedUtc.AddMinutes(-1);
        string[] files = Directory.GetFiles(logDirectory, "output_log_*.txt")
            .Where(path => File.GetLastWriteTimeUtc(path) >= cutoff)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(10)
            .ToArray();
        report.AppendLine("CandidateLogs: " + files.Length);
        for (int i = 0; i < files.Length; i++) report.AppendLine("- " + Path.GetFileName(files[i]));

        Regex markerRegex = new Regex(@"\[VRMINE_G3\] run=(\d+) marker=(\S+) local=(\d+)");
        Regex gameRegex = new Regex(@"\[VRMINE_G3_GAME\] run=(\d+) game=(\S+) local=(\d+) phase=(\d+) value=(\d+)");
        HashSet<int> localPlayers = new HashSet<int>();
        HashSet<string> markers = new HashSet<string>();
        Dictionary<string, HashSet<int>> baseline = NewGameEvidence();
        Dictionary<string, HashSet<int>> handoff = NewGameEvidence();

        for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
        {
            string[] lines;
            try { lines = File.ReadAllLines(files[fileIndex]); }
            catch { continue; }
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                Match marker = markerRegex.Match(lines[lineIndex]);
                if (marker.Success)
                {
                    int token;
                    int localId;
                    if (!int.TryParse(marker.Groups[1].Value, out token) || token != expectedRunToken) continue;
                    markers.Add(marker.Groups[2].Value);
                    if (int.TryParse(marker.Groups[3].Value, out localId) && localId > 0) localPlayers.Add(localId);
                }

                Match game = gameRegex.Match(lines[lineIndex]);
                if (!game.Success) continue;
                int gameToken;
                int localIdValue;
                int phaseValue;
                if (!int.TryParse(game.Groups[1].Value, out gameToken) || gameToken != expectedRunToken
                    || !int.TryParse(game.Groups[3].Value, out localIdValue)
                    || !int.TryParse(game.Groups[4].Value, out phaseValue)
                    || localIdValue <= 0) continue;
                string gameName = game.Groups[2].Value;
                localPlayers.Add(localIdValue);
                Dictionary<string, HashSet<int>> target = phaseValue == 1 ? baseline : phaseValue == 2 ? handoff : null;
                if (target != null && target.ContainsKey(gameName)) target[gameName].Add(localIdValue);
            }
        }

        int failures = 0;
        failures += Check(report, "DistinctClients", localPlayers.Count >= 2, string.Join(",", localPlayers));
        failures += CheckMarker(report, markers, "PUBLISH_BASELINE");
        failures += CheckMarker(report, markers, "OBSERVE_BASELINE");
        failures += CheckMarker(report, markers, "SECOND_CLIENT_SYNC_OBSERVED");
        failures += CheckMarker(report, markers, "OWNERSHIP_TRANSFERRED");
        failures += CheckMarker(report, markers, "REPUBLISH_BY_NEW_OWNER");
        failures += CheckMarker(report, markers, "OBSERVE_REPUBLISH");
        failures += CheckMarker(report, markers, "RESTORED_AFTER_TEST");
        failures += CheckGameEvidence(report, "TRICK", baseline, handoff);
        failures += CheckGameEvidence(report, "ORAPA", baseline, handoff);
        failures += CheckGameEvidence(report, "CHESS", baseline, handoff);
        report.AppendLine("LateJoin: NOT_AUTOMATED; verify manually after private upload");
        report.AppendLine("Result: " + (failures == 0 ? "PASS" : "FAIL"));
        WriteReport(VrcReportPath, report.ToString());
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
        int trickFailures = trick == null ? 1 : trick.VerifyRules();
        int orapaFailures = orapa == null ? 1 : orapa.VerifySimulation();
        int chessFailures = chess == null ? 1 : chess.VerifyRules();
        StringBuilder report = new StringBuilder();
        report.AppendLine("Board Games Runtime Verification");
        report.AppendLine((trickFailures == 0 ? "PASS " : "FAIL ") + "RuleForgeRules failures=" + trickFailures);
        report.AppendLine((orapaFailures == 0 ? "PASS " : "FAIL ") + "EchoMineSimulation failures=" + orapaFailures);
        report.AppendLine((chessFailures == 0 ? "PASS " : "FAIL ") + "ChessRules failures=" + chessFailures);
        bool passed = trickFailures == 0 && orapaFailures == 0 && chessFailures == 0;
        report.AppendLine("Result: " + (passed ? "PASS" : "FAIL"));
        WriteReport(RuntimeReportPath, report.ToString());
        SessionState.SetString("VRMine.BoardGamesRuntime", "");
        EditorApplication.isPlaying = false;
    }

    static void PrepareReleaseScene()
    {
        if (!File.Exists(ScenePath)) BoardGameShowcaseBuilder.Build();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        BoardGameSceneUpgrade.EnsurePlayerControls();
        VRMinePublicNameUpgrade.Apply();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    static bool HasObjects(params string[] names)
    {
        for (int i = 0; i < names.Length; i++) if (GameObject.Find(names[i]) == null) return false;
        return true;
    }

    static bool HasLabel(string expected)
    {
        UnityEngine.UI.Text[] labels = Object.FindObjectsOfType<UnityEngine.UI.Text>(true);
        for (int i = 0; i < labels.Length; i++) if (labels[i].text == expected) return true;
        return false;
    }

    static int GenerateRunToken()
    {
        long mixed = DateTime.UtcNow.Ticks ^ Environment.TickCount;
        int token = (int)(mixed & 0x7fffffff);
        return token == 0 ? 1 : token;
    }

    static bool TryReadIntField(string text, string field, out int value)
    {
        Match match = Regex.Match(text, "^" + Regex.Escape(field) + @":\s*(\d+)\s*$", RegexOptions.Multiline);
        return match.Success && int.TryParse(match.Groups[1].Value, out value);
    }

    static bool TryReadUtcField(string text, string field, out DateTime value)
    {
        Match match = Regex.Match(text, "^" + Regex.Escape(field) + @":\s*(\S+)\s*$", RegexOptions.Multiline);
        return match.Success && DateTime.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value);
    }

    static Dictionary<string, HashSet<int>> NewGameEvidence()
    {
        return new Dictionary<string, HashSet<int>>
        {
            { "TRICK", new HashSet<int>() },
            { "ORAPA", new HashSet<int>() },
            { "CHESS", new HashSet<int>() }
        };
    }

    static int CheckGameEvidence(StringBuilder report, string game, Dictionary<string, HashSet<int>> baseline, Dictionary<string, HashSet<int>> handoff)
    {
        int failures = 0;
        failures += Check(report, game + "BaselineReplication", baseline[game].Count >= 2, string.Join(",", baseline[game]));
        failures += Check(report, game + "HandoffReplication", handoff[game].Count >= 2, string.Join(",", handoff[game]));
        return failures;
    }

    static int CheckMarker(StringBuilder report, HashSet<string> markers, string marker)
    {
        return Check(report, marker, markers.Contains(marker), markers.Contains(marker) ? "observed" : "missing");
    }

    static int Check(StringBuilder report, string name, bool passed, string detail)
    {
        report.AppendLine((passed ? "PASS " : "FAIL ") + name + " " + detail);
        return passed ? 0 : 1;
    }

    static void WriteReport(string path, string content)
    {
        File.WriteAllText(path, content, Encoding.UTF8);
        AssetDatabase.Refresh();
        Debug.Log(content);
    }

    static void SetVrcSetting(string name, object value)
    {
        Type settingsType = FindType("VRCSettings");
        if (settingsType == null) return;
        PropertyInfo property = settingsType.GetProperty(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.CanWrite) property.SetValue(null, value);
    }

    static string GetConfiguredClientPath()
    {
        Type panelType = FindType("VRCSdkControlPanel");
        if (panelType == null) return "";
        FieldInfo field = panelType.GetField("clientInstallPath", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        return field == null ? "" : field.GetValue(null) as string;
    }

    static void SetConfiguredClientPath(string path)
    {
        Type panelType = FindType("VRCSdkControlPanel");
        if (panelType == null) return;
        FieldInfo field = panelType.GetField("clientInstallPath", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (field != null) field.SetValue(null, path);
    }

    static string ResolveVrChatClientPath(string configured)
    {
        List<string> candidates = new List<string>();
        if (!string.IsNullOrEmpty(configured)) candidates.Add(configured);
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        candidates.Add(Path.Combine(programFilesX86, "Steam", "steamapps", "common", "VRChat", "launch.exe"));
        candidates.Add(Path.Combine(programFilesX86, "Steam", "steamapps", "common", "VRChat", "VRChat.exe"));
        candidates.Add(@"D:\SteamLibrary\steamapps\common\VRChat\launch.exe");
        candidates.Add(@"D:\SteamLibrary\steamapps\common\VRChat\VRChat.exe");
        candidates.Add(@"C:\SteamLibrary\steamapps\common\VRChat\launch.exe");
        candidates.Add(@"C:\SteamLibrary\steamapps\common\VRChat\VRChat.exe");

        string vdf = Path.Combine(programFilesX86, "Steam", "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdf))
        {
            MatchCollection matches = Regex.Matches(File.ReadAllText(vdf), "\\\"path\\\"\\s+\\\"([^\\\"]+)\\\"");
            for (int i = 0; i < matches.Count; i++)
            {
                string root = matches[i].Groups[1].Value.Replace("\\\\", "\\");
                candidates.Add(Path.Combine(root, "steamapps", "common", "VRChat", "launch.exe"));
                candidates.Add(Path.Combine(root, "steamapps", "common", "VRChat", "VRChat.exe"));
            }
        }
        for (int i = 0; i < candidates.Count; i++) if (File.Exists(candidates[i])) return candidates[i];
        return "";
    }

    static string VrChatLogDirectory()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        DirectoryInfo parent = Directory.GetParent(local);
        string profile = parent == null ? local : parent.FullName;
        return Path.Combine(profile, "LocalLow", "VRChat", "VRChat");
    }

    static Type FindType(string name)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
        {
            Type[] types;
            try { types = assemblies[assemblyIndex].GetTypes(); }
            catch (ReflectionTypeLoadException exception) { types = exception.Types; }
            for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
                if (types[typeIndex] != null && types[typeIndex].Name == name) return types[typeIndex];
        }
        return null;
    }
}
