using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class PrefabBinder
{
    [MenuItem("VRMine/Scaffold/3_Bind References")]
    static void Bind()
    {
        var controllerObject = GameObject.Find("GameController");
        var simulatorObject = GameObject.Find("WaveSimulator");
        var logObject = GameObject.Find("LogBoard");
        var declare = GameObject.Find("DeclareButton")?.GetComponent<Button>();
        var controller = controllerObject ? controllerObject.GetComponent<GameController>() : null;
        var simulator = simulatorObject ? simulatorObject.GetComponent<WaveSimulator>() : null;
        var log = logObject ? logObject.GetComponent<LogBoard>() : null;
        var logStream = Object.FindObjectOfType<LogStream>();
        var board = Object.FindObjectOfType<BoardState>();
        var clients = Object.FindObjectsOfType<PlayerClient>();
        if (controller && clients.Length > 0) controller.mailboxes = clients;
        if (controller && simulator) controller.wave = simulator;
        if (controller && logStream) controller.logStream = logStream;
        if (controller && board) controller.board = board;
        if (log)
        {
            var texts = logObject.GetComponentsInChildren<Text>(true);
            if (texts.Length > 0) log.rows = texts;
        }
        if (logStream && log) logStream.view = log;
        var behaviour = controllerObject ? controllerObject.GetComponent<VRC.Udon.UdonBehaviour>() : null;
        if (declare && behaviour)
        {
            declare.onClick.RemoveAllListeners();
            declare.onClick.AddListener(new UnityAction(() => behaviour.SendCustomEvent("OnDeclare")));
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
