using System.IO;
using System;
using System.Security.Cryptography;
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
        StringBuilder report = new StringBuilder();
        int trickFailures = trick.VerifyRules();
        int flowFailures = VerifyIntegratedStichFlow(trick, report);
        int replayFailures = VerifyDeterministicReplay(trick, report);
        int orapaFailures = orapa.VerifySimulation();
        int chessFailures = chess.VerifyRules();
        report.Insert(0, "Board Games Runtime Verification\n");
        report.AppendLine((trickFailures == 0 ? "PASS " : "FAIL ") + "TrickMeisterRules failures=" + trickFailures);
        report.AppendLine((flowFailures == 0 ? "PASS " : "FAIL ") + "StichMeisterFlow failures=" + flowFailures);
        report.AppendLine((replayFailures == 0 ? "PASS " : "FAIL ") + "StichMeisterReplay failures=" + replayFailures);
        report.AppendLine((orapaFailures == 0 ? "PASS " : "FAIL ") + "OrapaReflection failures=" + orapaFailures);
        report.AppendLine((chessFailures == 0 ? "PASS " : "FAIL ") + "ChessRules failures=" + chessFailures);
        bool passed = trickFailures == 0 && flowFailures == 0 && replayFailures == 0 && orapaFailures == 0 && chessFailures == 0;
        report.AppendLine("Result: " + (passed ? "PASS" : "FAIL"));
        File.WriteAllText(RuntimeReportPath, report.ToString(), Encoding.UTF8);
        Debug.Log(report.ToString());
        SessionState.SetString("VRMine.BoardGamesRuntime", "");
        EditorApplication.isPlaying = false;
    }

    static int VerifyIntegratedStichFlow(GameController trick, StringBuilder report)
    {
        int failures = 0;
        System.Reflection.MethodInfo activateRules = typeof(GameController).GetMethod("ActivateRules", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        System.Reflection.FieldInfo resetArmedAt = typeof(BoardGameAction).GetField("resetArmedAt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        BoardGameAction resetAction = null;
        BoardGameAction[] actions = Object.FindObjectsOfType<BoardGameAction>(true);
        for (int i = 0; i < actions.Length; i++)
        {
            if (actions[i].game == 0 && actions[i].action >= 4 && actions[i].trickGame == trick)
            {
                resetAction = actions[i];
                break;
            }
        }
        if (activateRules == null || resetArmedAt == null || resetAction == null)
        {
            failures += Check(report, "StichFlowFixture", false, "missing canonical ActivateRules/reset action path");
            return failures;
        }

        for (int playerCount = 3; playerCount <= NetConst.MaxPlayers; playerCount++)
        {
            bool passed = RunIntegratedStichFlow(trick, resetAction, resetArmedAt, activateRules, playerCount);
            failures += Check(report, "StichFlow" + playerCount + "P", passed, passed ? "start->complete->confirmed-reset->second-match" : "flow did not reach canonical second match");
        }
        return failures;
    }

    static bool RunIntegratedStichFlow(GameController trick, BoardGameAction resetAction, System.Reflection.FieldInfo resetArmedAt, System.Reflection.MethodInfo activateRules, int playerCount)
    {
        trick.ConfigurePlayers(playerCount);
        trick.boardSeed = (uint)(6200 + playerCount);
        for (int i = 0; i < trick.board.occupiedPlayerIds.Length; i++) trick.board.occupiedPlayerIds[i] = 0;
        for (int seat = 0; seat < playerCount; seat++) trick.board.occupiedPlayerIds[seat] = 62000 + playerCount * 10 + seat;
        trick.SetupGame();
        if (trick.board.phase != BoardState.PhaseRuleSelect || trick.board.roundIndex != 0) return false;

        int guard = 0;
        while (trick.board.phase != BoardState.PhaseComplete && guard++ < 1000)
        {
            if (trick.board.phase == BoardState.PhaseRuleSelect)
            {
                SetSafeFlowRules(trick, playerCount);
                activateRules.Invoke(trick, null);
                if (trick.board.phase != BoardState.PhasePlayCard) return false;
                continue;
            }
            if (trick.board.phase != BoardState.PhasePlayCard) return false;

            int playerSeat = trick.board.currentPlayerSeat;
            int offset = playerSeat * NetConst.MaxHandSize;
            int beforeTurn = trick.turnIndex;
            for (int handIndex = 0; handIndex < NetConst.MaxHandSize && trick.turnIndex == beforeTurn; handIndex++)
            {
                if (trick.board.playerHands[offset + handIndex] == 0) continue;
                trick.TryPlayCard(playerSeat, handIndex);
            }
            if (trick.turnIndex == beforeTurn) return false;
        }

        if (trick.board.phase != BoardState.PhaseComplete || trick.board.roundIndex != playerCount) return false;
        int[] occupied = new int[playerCount];
        for (int seat = 0; seat < playerCount; seat++) occupied[seat] = trick.board.occupiedPlayerIds[seat];

        resetAction.Interact();
        if (trick.board.phase != BoardState.PhaseComplete) return false;
        resetArmedAt.SetValue(resetAction, Time.time - 1f);
        resetAction.Interact();

        if (trick.board.phase != BoardState.PhaseRuleSelect || trick.board.roundIndex != 0 || trick.turnIndex != 0 || trick.winnerPlayerId != 0) return false;
        for (int seat = 0; seat < playerCount; seat++)
        {
            if (trick.board.occupiedPlayerIds[seat] != occupied[seat] || trick.board.scores[seat] != 0) return false;
            for (int other = seat + 1; other < playerCount; other++) if (trick.board.occupiedPlayerIds[seat] == trick.board.occupiedPlayerIds[other]) return false;
        }
        return trick.board.trickIndex == 0 && trick.board.trickCardCount == 0 && trick.board.prepareStep == 0;
    }

    static void SetSafeFlowRules(GameController trick, int playerCount)
    {
        byte[] safeRules = { 1, 14, 26, 41 };
        for (int i = 0; i < trick.board.selectedRuleBySeat.Length; i++) trick.board.selectedRuleBySeat[i] = 0;
        int ruleIndex = 0;
        int skippedSeat = playerCount == 5 ? (trick.board.dealerSeat + 1) % playerCount : -1;
        for (int seat = 0; seat < playerCount; seat++)
        {
            if (seat == skippedSeat) continue;
            trick.board.selectedRuleBySeat[seat] = safeRules[ruleIndex++];
        }
        if (playerCount == 3 && trick.board.ruleDeckCursor < trick.board.ruleDeck.Length) trick.board.ruleDeck[trick.board.ruleDeckCursor] = safeRules[3];
    }

    static int VerifyDeterministicReplay(GameController trick, StringBuilder report)
    {
        int failures = 0;
        const uint seed = 2306u;
        for (int playerCount = 3; playerCount <= NetConst.MaxPlayers; playerCount++)
        {
            string first = ReplayHash(trick, playerCount, seed);
            string second = ReplayHash(trick, playerCount, seed);
            string changedSeed = ReplayHash(trick, playerCount, seed + 1u);
            failures += Check(report, "StichReplay" + playerCount + "P", first == second, first + " / " + second);
            failures += Check(report, "StichReplay" + playerCount + "PSeedSensitivity", first != changedSeed, first + " / " + changedSeed);
        }
        return failures;
    }

    static string ReplayHash(GameController trick, int playerCount, uint seed)
    {
        trick.ConfigurePlayers(playerCount);
        trick.boardSeed = seed;
        trick.SetupGame();

        System.Reflection.MethodInfo beginPlay = typeof(GameController).GetMethod("BeginPlay", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (beginPlay == null) throw new InvalidOperationException("GameController.BeginPlay is unavailable");
        beginPlay.Invoke(trick, null);

        int playerSeat = trick.board.currentPlayerSeat;
        int offset = playerSeat * NetConst.MaxHandSize;
        int handIndex = -1;
        for (int i = 0; i < NetConst.MaxHandSize; i++)
        {
            if (trick.board.playerHands[offset + i] == 0) continue;
            handIndex = i;
            break;
        }
        if (handIndex < 0) throw new InvalidOperationException("Replay has no playable opening card");
        trick.TryPlayCard(playerSeat, handIndex);
        if (trick.turnIndex != 1) throw new InvalidOperationException("Replay opening action did not advance turnIndex");

        string state = EditorJsonUtility.ToJson(trick.board, false)
            + "|" + trick.boardSeed
            + "|" + trick.boardHash
            + "|" + trick.turnIndex
            + "|" + trick.winnerPlayerId
            + "|" + trick.declarationResult;
        using (SHA256 sha = SHA256.Create())
        {
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(state));
            StringBuilder hex = new StringBuilder(digest.Length * 2);
            for (int i = 0; i < digest.Length; i++) hex.Append(digest[i].ToString("x2"));
            return hex.ToString();
        }
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
