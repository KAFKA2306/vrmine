using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PerspectiveCageExperienceBuilder
{
    const string SpecPath = "config/perspective-cage.json";
    const float ZoneSpacing = 12f;

    [Serializable] class WorldSpec
    {
        public string title_ja;
        public string title_en;
        public string summary_ja;
        public string summary_en;
    }

    [Serializable] class IntroRule
    {
        public string description_ja;
        public string description_en;
        public string quick_start_ja;
        public string quick_start_en;
    }

    [Serializable] class SolutionSpec
    {
        public string output;
    }

    [Serializable] class PuzzleSpec
    {
        public string id;
        public string title_ja;
        public string title_en;
        public string goal;
        public string goal_en;
        public string[] hints;
        public string[] hints_en;
        public string wrong_feedback_ja;
        public string wrong_feedback_en;
        public string success_feedback_ja;
        public string success_feedback_en;
        public SolutionSpec solution;
    }

    [Serializable] class ExperienceSpec
    {
        public WorldSpec world;
        public IntroRule intro_rule;
        public PuzzleSpec[] puzzles;
    }

    [MenuItem("VRMine/Perspective Cage/Apply Product Experience")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != PerspectiveCageBuilder.ScenePath)
            throw new InvalidOperationException("Perspective Cage canonical scene must be active before applying experience presentation.");

        ExperienceSpec spec = LoadSpec();
        GameObject world = GameObject.Find("PerspectiveCageWorld");
        if (world == null) throw new InvalidOperationException("PerspectiveCageWorld root is missing.");

        Transform oldRoot = world.transform.Find("PerspectiveCageExperience");
        if (oldRoot != null) UnityEngine.Object.DestroyImmediate(oldRoot.gameObject);

        GameObject experience = new GameObject("PerspectiveCageExperience");
        experience.transform.SetParent(world.transform);

        ApplyIntro(experience.transform, spec);
        ApplyPuzzlePresentation(experience.transform, spec);
        ApplyPersistentControls();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, PerspectiveCageBuilder.ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Perspective Cage product experience applied: bilingual onboarding, hints, feedback, and progress cues.");
    }

    static ExperienceSpec LoadSpec()
    {
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), SpecPath);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Perspective Cage spec not found", fullPath);
        ExperienceSpec spec = JsonUtility.FromJson<ExperienceSpec>(File.ReadAllText(fullPath));
        if (spec == null || spec.world == null || spec.intro_rule == null || spec.puzzles == null || spec.puzzles.Length != 5)
            throw new InvalidDataException("Perspective Cage experience copy is incomplete.");
        for (int i = 0; i < spec.puzzles.Length; i++)
        {
            PuzzleSpec puzzle = spec.puzzles[i];
            if (puzzle == null || puzzle.hints == null || puzzle.hints_en == null || puzzle.hints.Length != 3 || puzzle.hints_en.Length != 3)
                throw new InvalidDataException("Each Perspective Cage puzzle requires three Japanese and three English hints.");
        }
        return spec;
    }

    static void ApplyIntro(Transform parent, ExperienceSpec spec)
    {
        SetText("TitleText", spec.world.title_ja + "\n" + spec.world.title_en);
        SetText("IntroRuleText", "入口の規則 / ENTRANCE RULE\n" + spec.intro_rule.description_ja + "\n" + spec.intro_rule.description_en);

        string quickStart = "15–30 MIN · 1–4 PLAYERS · VRCHAT PC\n"
            + spec.intro_rule.quick_start_ja + "\n"
            + spec.intro_rule.quick_start_en + "\n"
            + "HINT / ヒント: each room · RESET / 最初から: after clear";
        Label("QuickStart", quickStart, new Vector3(0f, 2.15f, -2.4f), 0.031f, parent);
        Label("StartDirection", "START / 開始  →  ROOM 1", new Vector3(0f, 1.0f, 5.1f), 0.034f, parent);
    }

    static void ApplyPuzzlePresentation(Transform parent, ExperienceSpec spec)
    {
        for (int puzzleIndex = 0; puzzleIndex < spec.puzzles.Length; puzzleIndex++)
        {
            PuzzleSpec puzzle = spec.puzzles[puzzleIndex];
            int room = puzzleIndex + 1;
            SetText("Heading_" + puzzle.id + "Text", puzzle.id.ToUpperInvariant() + "  " + puzzle.title_ja + " / " + puzzle.title_en);

            for (int hint = 0; hint < 3; hint++)
            {
                SetText(
                    "Hint_P0" + room + "_" + (hint + 1) + "Text",
                    "HINT " + (hint + 1) + " / ヒント " + (hint + 1) + "\n" + puzzle.hints[hint] + "\n" + puzzle.hints_en[hint]);
            }

            SetText("HintButton_P0" + room + "LabelText", "HINT\nヒント");
            SetText("Wrong_P0" + room + "Text", puzzle.wrong_feedback_ja + "\n" + puzzle.wrong_feedback_en);

            if (puzzleIndex < 4)
            {
                string result = puzzle.solution == null ? "" : (puzzle.solution.output ?? "").ToUpperInvariant();
                SetText(
                    "Result_P0" + room + "Text",
                    puzzle.success_feedback_ja + "\n" + puzzle.success_feedback_en + "\nRESULT / 結果: " + result);
            }

            Vector3 center = new Vector3(0f, 0f, room * ZoneSpacing);
            Label(
                "RoomGoal_P0" + room,
                puzzle.goal + "\n" + puzzle.goal_en,
                center + new Vector3(0f, 3.15f, -3.75f),
                0.023f,
                parent);
        }

        SetText("P01ObservationLabelText", "VIEWPOINT\n観測位置");
        SetText("P03CueText", "MATCH SHAPE + NOTCH DIRECTION\n形 + 切欠きの向きを合わせる");
        SetText("P04ReferenceText", "REFERENCE / 比較基準\nTRIANGLE  CIRCLE  SQUARE  DIAMOND  CROSS");
        SetText("P04CurrentText", "THIS ROOM / この部屋\nTRIANGLE  CIRCLE  SQUARE  DIAMOND");
        SetText("P05RuleText", "READ / 読む順: 3 → 1 → 4 → 2");
        SetText("ClearPresentationText", spec.puzzles[4].success_feedback_ja + "\n" + spec.puzzles[4].success_feedback_en);

        Label(
            "P01ViewpointGuide",
            "MOVE YOUR VIEW UNTIL THE FRAGMENTS OVERLAP\n断片が一つに重なる位置・高さを探す",
            new Vector3(0f, 2.55f, ZoneSpacing - 3.7f),
            0.026f,
            parent);
    }

    static void ApplyPersistentControls()
    {
        SetText("ResetStationLabelText", "RESET WORLD\n最初から");
    }

    static void SetText(string textObjectName, string value)
    {
        Text[] labels = Resources.FindObjectsOfTypeAll<Text>();
        for (int i = 0; i < labels.Length; i++)
        {
            Text label = labels[i];
            if (label == null || label.gameObject.name != textObjectName) continue;
            if (label.gameObject.scene.path != PerspectiveCageBuilder.ScenePath) continue;
            label.text = value;
            EditorUtility.SetDirty(label);
            return;
        }
        throw new InvalidOperationException("Perspective Cage label not found: " + textObjectName);
    }

    static GameObject Label(string name, string text, Vector3 position, float scale, Transform parent)
    {
        GameObject canvasObject = new GameObject(name + "Canvas");
        canvasObject.transform.SetParent(parent);
        canvasObject.transform.position = position;
        canvasObject.transform.localScale = Vector3.one * scale * 0.002f;
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1050f, 340f);

        GameObject textObject = new GameObject(name + "Text");
        textObject.transform.SetParent(canvasObject.transform, false);
        Text label = textObject.AddComponent<Text>();
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        label.text = text;
        label.fontSize = 52;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        return canvasObject;
    }
}
