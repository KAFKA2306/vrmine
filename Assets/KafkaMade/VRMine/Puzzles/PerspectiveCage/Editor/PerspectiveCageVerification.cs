using System;
using System.IO;
using System.Text;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRC.SDK3.Components;
using Object = UnityEngine.Object;

public static class PerspectiveCageVerification
{
    const string ReportPath = "Library/VRMine/PerspectiveCageVerification.txt";

    [MenuItem("VRMine/Perspective Cage/Build And Verify")]
    public static void BuildAndVerify()
    {
        PerspectiveCageBuilder.Build();
        PerspectiveCageExperienceBuilder.Apply();
        PerspectiveCageBuilder.Build();
        PerspectiveCageExperienceBuilder.Apply();
        RunGate();
    }

    [MenuItem("VRMine/Perspective Cage/Verify Canonical Scene")]
    public static void RunGate()
    {
        int failures = Verify(out string report);
        Debug.Log(report);
        if (failures != 0) Debug.LogError("Perspective Cage verification failed with " + failures + " failure(s)");
    }

    public static void BuildAndVerifyBatch()
    {
        try
        {
            PerspectiveCageBuilder.Build();
            PerspectiveCageExperienceBuilder.Apply();
            PerspectiveCageBuilder.Build();
            PerspectiveCageExperienceBuilder.Apply();
            int failures = Verify(out string report);
            if (failures == 0)
            {
                Debug.Log("Perspective Cage verification PASS\n" + report);
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError("Perspective Cage verification FAIL\n" + report);
                EditorApplication.Exit(1);
            }
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    static int Verify(out string reportText)
    {
        EditorSceneManager.OpenScene(PerspectiveCageBuilder.ScenePath, OpenSceneMode.Single);
        Scene scene = SceneManager.GetActiveScene();
        StringBuilder report = new StringBuilder();
        report.AppendLine("Perspective Cage Unity Verification");
        report.AppendLine("Scene: " + scene.path);
        int failures = 0;

        failures += Check(report, "CanonicalScene", scene.path == PerspectiveCageBuilder.ScenePath, scene.path);
        VRCSceneDescriptor[] descriptors = Object.FindObjectsOfType<VRCSceneDescriptor>(true);
        failures += Check(report, "SceneDescriptor", descriptors.Length == 1, descriptors.Length.ToString());
        if (descriptors.Length == 1)
        {
            failures += Check(report, "Spawn", descriptors[0].spawns != null && descriptors[0].spawns.Length == 1 && descriptors[0].spawns[0] != null, descriptors[0].spawns == null ? "null" : descriptors[0].spawns.Length.ToString());
            failures += Check(report, "ReferenceCamera", descriptors[0].ReferenceCamera != null, descriptors[0].ReferenceCamera == null ? "null" : descriptors[0].ReferenceCamera.name);
        }

        PerspectiveCageController[] controllers = Object.FindObjectsOfType<PerspectiveCageController>(true);
        failures += Check(report, "ControllerCount", controllers.Length == 1, controllers.Length.ToString());
        PerspectiveCageInteractable[] interactions = Object.FindObjectsOfType<PerspectiveCageInteractable>(true);
        failures += Check(report, "InteractionCount", interactions.Length == 32, interactions.Length.ToString());
        for (int i = 0; i < interactions.Length; i++)
        {
            failures += Check(report, "InteractionController[" + i + "]", interactions[i].controller != null, interactions[i].name);
            failures += Check(report, "InteractionCollider[" + i + "]", interactions[i].GetComponent<Collider>() != null, interactions[i].name);
        }

        if (controllers.Length == 1)
        {
            PerspectiveCageController controller = controllers[0];
            failures += Check(report, "ProgressionDoors", Full(controller.progressionDoors, 4), Count(controller.progressionDoors) + "/4");
            failures += Check(report, "ClearDoor", controller.clearDoor != null, controller.clearDoor == null ? "null" : controller.clearDoor.name);
            failures += Check(report, "ClearPresentation", controller.clearPresentation != null, controller.clearPresentation == null ? "null" : controller.clearPresentation.name);
            failures += Check(report, "ResultPanels", Full(controller.resultPanels, 4), Count(controller.resultPanels) + "/4");
            failures += Check(report, "HintPanels", Full(controller.hintPanels, 15), Count(controller.hintPanels) + "/15");
            failures += Check(report, "WrongFeedbacks", Full(controller.wrongFeedbacks, 5), Count(controller.wrongFeedbacks) + "/5");
            failures += Check(report, "Markers", Full(controller.markerObjects, 4), Count(controller.markerObjects) + "/4");
            failures += Check(report, "MarkerHomes", Full(controller.markerHomes, 4), Count(controller.markerHomes) + "/4");
            failures += Check(report, "SocketTargets", Full(controller.socketTargets, 4), Count(controller.socketTargets) + "/4");
            failures += Check(report, "DeterministicPuzzleLogic", controller.VerifyDeterministicLogic() == 0, controller.VerifyDeterministicLogic().ToString());
            bool initial = controller.completionMask == 0 && controller.p02Step == 0 && controller.p03PlacedMask == 0 && controller.p05Step == 0 && controller.hintPacked == 0 && !controller.cleared;
            failures += Check(report, "InitialState", initial, controller.completionMask + "/" + controller.p02Step + "/" + controller.p03PlacedMask + "/" + controller.p05Step + "/" + controller.hintPacked + "/" + controller.cleared);
        }

        UdonSharpBehaviour[] behaviours = Object.FindObjectsOfType<UdonSharpBehaviour>(true);
        int validPrograms = 0;
        for (int i = 0; i < behaviours.Length; i++)
        {
            VRC.Udon.UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviours[i]);
            if (backing != null && backing.programSource != null) validPrograms++;
        }
        failures += Check(report, "UdonPrograms", behaviours.Length > 0 && validPrograms == behaviours.Length, validPrograms + "/" + behaviours.Length);

        for (int zone = 0; zone < 7; zone++) failures += Check(report, "Floor_" + zone, GameObject.Find("Floor_" + zone) != null, GameObject.Find("Floor_" + zone) == null ? "missing" : "present");

        GameObject experienceRoot = GameObject.Find("PerspectiveCageExperience");
        failures += Check(report, "ExperienceRoot", experienceRoot != null, experienceRoot == null ? "missing" : "present");
        Text quickStart = FindSceneText("QuickStartText");
        failures += Check(report, "QuickStart", quickStart != null && quickStart.text.Contains("OBSERVE") && quickStart.text.Contains("観察"), quickStart == null ? "missing" : "bilingual");
        Text introRule = FindSceneText("IntroRuleText");
        failures += Check(report, "BilingualIntroRule", introRule != null && introRule.text.Contains("ENTRANCE RULE") && introRule.text.Contains("入口の規則"), introRule == null ? "missing" : "bilingual");
        Text p01Guide = FindSceneText("P01ViewpointGuideText");
        failures += Check(report, "P01ViewpointGuide", p01Guide != null && p01Guide.text.Contains("断片") && p01Guide.text.Contains("FRAGMENTS"), p01Guide == null ? "missing" : "bilingual");
        int bilingualHints = 0;
        for (int puzzle = 1; puzzle <= 5; puzzle++)
        {
            for (int hint = 1; hint <= 3; hint++)
            {
                Text label = FindSceneText("Hint_P0" + puzzle + "_" + hint + "Text");
                if (label != null && label.text.Contains("HINT") && label.text.Contains("ヒント")) bilingualHints++;
            }
        }
        failures += Check(report, "BilingualHints", bilingualHints == 15, bilingualHints + "/15");

        failures += Check(report, "MissingScripts", CountMissingScripts(scene) == 0, CountMissingScripts(scene).ToString());
        failures += Check(report, "BuildSettings", IsRegisteredBuildScene(), PerspectiveCageBuilder.ScenePath);

        report.AppendLine("Result: " + (failures == 0 ? "PASS" : "FAIL"));
        string folder = Path.GetDirectoryName(ReportPath);
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        File.WriteAllText(ReportPath, report.ToString(), Encoding.UTF8);
        reportText = report.ToString();
        return failures;
    }

    static Text FindSceneText(string name)
    {
        Text[] labels = Resources.FindObjectsOfTypeAll<Text>();
        for (int i = 0; i < labels.Length; i++)
        {
            Text label = labels[i];
            if (label != null && label.gameObject.name == name && label.gameObject.scene.path == PerspectiveCageBuilder.ScenePath) return label;
        }
        return null;
    }

    static int CountMissingScripts(Scene scene)
    {
        int count = 0;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++) count += CountMissingScriptsRecursive(roots[i].transform);
        return count;
    }

    static int CountMissingScriptsRecursive(Transform transform)
    {
        int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
        for (int i = 0; i < transform.childCount; i++) count += CountMissingScriptsRecursive(transform.GetChild(i));
        return count;
    }

    static bool IsRegisteredBuildScene()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        for (int i = 0; i < scenes.Length; i++) if (scenes[i].path == PerspectiveCageBuilder.ScenePath && scenes[i].enabled) return true;
        return false;
    }

    static bool Full(GameObject[] values, int expected)
    {
        if (values == null || values.Length != expected) return false;
        for (int i = 0; i < values.Length; i++) if (values[i] == null) return false;
        return true;
    }

    static bool Full(Transform[] values, int expected)
    {
        if (values == null || values.Length != expected) return false;
        for (int i = 0; i < values.Length; i++) if (values[i] == null) return false;
        return true;
    }

    static int Count(GameObject[] values)
    {
        if (values == null) return 0;
        int count = 0;
        for (int i = 0; i < values.Length; i++) if (values[i] != null) count++;
        return count;
    }

    static int Count(Transform[] values)
    {
        if (values == null) return 0;
        int count = 0;
        for (int i = 0; i < values.Length; i++) if (values[i] != null) count++;
        return count;
    }

    static int Check(StringBuilder report, string name, bool passed, string detail)
    {
        report.AppendLine((passed ? "PASS " : "FAIL ") + name + " " + detail);
        return passed ? 0 : 1;
    }
}
