using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class PrefabBinder
{
    [MenuItem("VRMine/Scaffold/3_Bind References")]
    static void Bind()
    {
        var controllerObject = GameObject.Find("GameController");
        var simulatorObject = GameObject.Find("WaveSimulator");
        var logObject = GameObject.Find("LogBoard");
        var logBoardViewObject = GameObject.Find("LogBoardView");
        var declare = GameObject.Find("DeclareButton")?.GetComponent<Button>();
        var controller = controllerObject ? controllerObject.GetComponent<GameController>() : null;
        var simulator = simulatorObject ? simulatorObject.GetComponent<WaveSimulator>() : null;
        var log = logObject ? logObject.GetComponent<LogBoard>() : null;
        var logBoardView = logBoardViewObject ? logBoardViewObject.GetComponent<LogBoardView>() : null;
        var logStream = Object.FindObjectOfType<LogStream>();
        var board = Object.FindObjectOfType<BoardState>();
        var clients = Object.FindObjectsOfType<PlayerClient>();
        if (controller && clients.Length > 0) controller.mailboxes = clients;
        if (controller && simulator) controller.wave = simulator;
        if (controller && logStream) controller.logStream = logStream;
        if (controller && board) controller.board = board;
        if (logBoardView && logStream) logBoardView.stream = logStream;
        if (log)
        {
            var texts = logObject.GetComponentsInChildren<Text>(true);
            if (texts.Length > 0) log.rows = texts;
        }
        if (logBoardView && log) logBoardView.board = log;
        if (declare && controller)
        {
            declare.onClick.RemoveAllListeners();
            int count = declare.onClick.GetPersistentEventCount();
            while (count > 0)
            {
                count--;
                UnityEditor.Events.UnityEventTools.RemovePersistentListener(declare.onClick, count);
            }
            UnityEditor.Events.UnityEventTools.AddPersistentListener(declare.onClick, () => controller.SendCustomEvent("OnDeclare"));
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
