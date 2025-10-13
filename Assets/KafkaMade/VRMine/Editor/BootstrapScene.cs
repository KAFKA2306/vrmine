using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class BootstrapScene
{
    [MenuItem("VRMine/Scaffold/2_Bootstrap MVP Scene")]
    static void Boot()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("BoardRoot");
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "BoardQuad";
        quad.transform.SetParent(root.transform);
        quad.transform.localScale = new Vector3(10, 8, 1);
        var canvas = new GameObject("Canvas_Log", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvasComp = canvas.GetComponent<Canvas>();
        canvasComp.renderMode = RenderMode.WorldSpace;
        canvas.transform.position = new Vector3(0, 5.5f, 0);
        canvas.transform.localScale = Vector3.one * 0.01f;
        var panel = new GameObject("Panel", typeof(Image));
        panel.transform.SetParent(canvas.transform);
        var textObj = new GameObject("LogText", typeof(Text));
        textObj.transform.SetParent(panel.transform);
        var text = textObj.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = "VRMine Log";
        text.alignment = TextAnchor.UpperLeft;
        text.resizeTextForBestFit = true;
        var button = new GameObject("DeclareButton", typeof(Image), typeof(Button));
        button.transform.SetParent(canvas.transform);
        var buttonTextObj = new GameObject("Text", typeof(Text));
        buttonTextObj.transform.SetParent(button.transform);
        var buttonText = buttonTextObj.GetComponent<Text>();
        buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        buttonText.text = "完全一致を宣言";
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.resizeTextForBestFit = true;
        var markers = new GameObject("EntryMarkers");
        markers.transform.SetParent(root.transform);
        float halfWidth = 5f;
        float halfHeight = 4f;
        for (int i = 0; i < 10; i++)
        {
            float x = -4.5f + i;
            MakeMarker(markers, "Top_" + (i + 1), new Vector3(x, halfHeight + 0.25f, 0));
            MakeMarker(markers, "Bottom_" + (i + 1), new Vector3(x, -halfHeight - 0.25f, 0));
        }
        string labels = "ABCDEFGH";
        for (int j = 0; j < 8; j++)
        {
            float y = -3.5f + j;
            MakeMarker(markers, "Left_" + labels[j], new Vector3(-halfWidth - 0.25f, y, 0));
            MakeMarker(markers, "Right_" + labels[j], new Vector3(halfWidth + 0.25f, y, 0));
        }
        var controller = new GameObject("GameController");
        var client = new GameObject("PlayerClient");
        var simulator = new GameObject("WaveSimulator");
        var logBoard = new GameObject("LogBoard");
        controller.transform.SetParent(root.transform);
        client.transform.SetParent(root.transform);
        simulator.transform.SetParent(root.transform);
        logBoard.transform.SetParent(canvas.transform);
        Directory.CreateDirectory("Assets/KafkaMade/VRMine/Prefabs");
        PrefabUtility.SaveAsPrefabAsset(root, "Assets/KafkaMade/VRMine/Prefabs/BoardRoot.prefab");
        PrefabUtility.SaveAsPrefabAsset(canvas, "Assets/KafkaMade/VRMine/Prefabs/LogCanvas.prefab");
        EditorSceneManager.SaveScene(scene, "Assets/KafkaMade/VRMine/Scenes/MVP.unity");
        AssetDatabase.SaveAssets();
    }

    static void MakeMarker(GameObject parent, string name, Vector3 pos)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent.transform);
        g.transform.localPosition = pos;
        g.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
    }
}
