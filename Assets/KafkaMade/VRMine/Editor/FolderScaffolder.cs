using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class FolderScaffolder
{
    [MenuItem("VRMine/Scaffold/1_Move Assets")]
    static void Move()
    {
        Ensure("Assets/KafkaMade/VRMine/Editor");
        Ensure("Assets/KafkaMade/VRMine/Runtime/Net");
        Ensure("Assets/KafkaMade/VRMine/Runtime/Data");
        Ensure("Assets/KafkaMade/VRMine/Runtime/Sim");
        Ensure("Assets/KafkaMade/VRMine/Runtime/Game");
        Ensure("Assets/KafkaMade/VRMine/Runtime/UI");
        Ensure("Assets/KafkaMade/VRMine/Prefabs");
        Ensure("Assets/KafkaMade/VRMine/Scenes");
        var m = new Dictionary<string, string>
        {
            {"Assets/KafkaMade/VRMine/Udon/NetConst.cs", "Assets/KafkaMade/VRMine/Runtime/Net/NetConst.cs"},
            {"Assets/KafkaMade/VRMine/Udon/NetConst.asset", "Assets/KafkaMade/VRMine/Runtime/Net/NetConst.asset"},
            {"Assets/KafkaMade/VRMine/Udon/BoardState.cs", "Assets/KafkaMade/VRMine/Runtime/Data/BoardState.cs"},
            {"Assets/KafkaMade/VRMine/Udon/BoardState.asset", "Assets/KafkaMade/VRMine/Runtime/Data/BoardState.asset"},
            {"Assets/KafkaMade/VRMine/Udon/LogStream.cs", "Assets/KafkaMade/VRMine/Runtime/Data/LogStream.cs"},
            {"Assets/KafkaMade/VRMine/Udon/LogStream.asset", "Assets/KafkaMade/VRMine/Runtime/Data/LogStream.asset"},
            {"Assets/KafkaMade/VRMine/Udon/WaveSimulator.cs", "Assets/KafkaMade/VRMine/Runtime/Sim/WaveSimulator.cs"},
            {"Assets/KafkaMade/VRMine/Udon/WaveSimulator.asset", "Assets/KafkaMade/VRMine/Runtime/Sim/WaveSimulator.asset"},
            {"Assets/KafkaMade/VRMine/Udon/GameController.cs", "Assets/KafkaMade/VRMine/Runtime/Game/GameController.cs"},
            {"Assets/KafkaMade/VRMine/Udon/GameController.asset", "Assets/KafkaMade/VRMine/Runtime/Game/GameController.asset"},
            {"Assets/KafkaMade/VRMine/Udon/PlayerClient.cs", "Assets/KafkaMade/VRMine/Runtime/Game/PlayerClient.cs"},
            {"Assets/KafkaMade/VRMine/Udon/PlayerClient.asset", "Assets/KafkaMade/VRMine/Runtime/Game/PlayerClient.asset"},
            {"Assets/KafkaMade/VRMine/Udon/LogBoard.cs", "Assets/KafkaMade/VRMine/Runtime/UI/LogBoard.cs"},
            {"Assets/KafkaMade/VRMine/Udon/LogBoard.asset", "Assets/KafkaMade/VRMine/Runtime/UI/LogBoard.asset"}
        };
        foreach (var kv in m) if (Exists(kv.Key)) AssetDatabase.MoveAsset(kv.Key, kv.Value);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static void Ensure(string p)
    {
        if (AssetDatabase.IsValidFolder(p)) return;
        var parent = p.Substring(0, p.LastIndexOf('/'));
        var name = p.Substring(p.LastIndexOf('/') + 1);
        AssetDatabase.CreateFolder(parent, name);
    }

    static bool Exists(string p)
    {
        return AssetDatabase.LoadAssetAtPath<Object>(p) != null;
    }
}
