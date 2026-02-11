using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

public class ImportUdonSharpSample
{
    static ListRequest listRequest;

    [MenuItem("Tools/Import UdonSharp Sample")]
    static void Import()
    {
        if (listRequest != null && !listRequest.IsCompleted) return;
        listRequest = Client.List(true);
        EditorApplication.update += OnListRequest;
    }

    static void OnListRequest()
    {
        if (!listRequest.IsCompleted) return;
        EditorApplication.update -= OnListRequest;
        if (listRequest.Status == StatusCode.Success)
        {
            foreach (var package in listRequest.Result)
            {
                if (package.name != "com.vrchat.worlds") continue;
                var samples = Sample.FindByPackage(package.name, package.version);
                foreach (var sample in samples)
                {
                    if (!sample.displayName.Contains("UdonSharp")) continue;
                    if (sample.isImported) continue;
                    sample.Import(false);
                }
            }
        }
        listRequest = null;
    }
}
