using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

public static class BatchImportUdonSharp
{
    static ListRequest list;

    [InitializeOnLoadMethod]
    static void AutoImport()
    {
        if (!Application.isBatchMode) return;
        if (list != null) return;
        list = Client.List(true, true);
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (!list.IsCompleted) return;
        EditorApplication.update -= Tick;
        if (list.Status == StatusCode.Success)
        {
            foreach (var package in list.Result)
            {
                if (package.name != "com.vrchat.worlds") continue;
                var samples = package.samples;
                if (samples == null) break;
                foreach (var sample in samples)
                {
                    if (!sample.displayName.Contains("UdonSharp")) continue;
                    if (sample.isImported) continue;
                    sample.Import();
                }
                break;
            }
        }
        EditorApplication.Exit(0);
    }
}
