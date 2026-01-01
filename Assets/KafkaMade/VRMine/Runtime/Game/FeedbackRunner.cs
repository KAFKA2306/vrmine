using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(menuName = "VRMine/FeedbackReport")]
public class FeedbackReport : ScriptableObject
{
    public string label;
    public int run;
    public float score;
    public string[] lines = new string[0];
}

public class FeedbackRunner : UdonSharpBehaviour
{
    public FeedbackReport report;
    public Renderer[] visuals = new Renderer[0];
    public Collider[] interactors = new Collider[0];
    public float minFootprint = 25f;
    public float minHeight = 3f;
    public FeedbackLogger[] loggers = new FeedbackLogger[0];
    readonly string[] buffer = new string[4];

    void Start()
    {
        Run();
    }

    public void Run()
    {
        float footprint;
        float height;
        bool sizeReady = Size(out footprint, out height);
        int visualReady = CountVisuals(visuals);
        int interactReady = CountInteract(interactors);
        float sizeScore = sizeReady ? 1f : 0f;
        float visualScore = Score(visualReady, visuals.Length);
        float interactScore = Score(interactReady, interactors.Length);
        float total = (sizeScore + visualScore + interactScore) / 3f;
        buffer[0] = "Size:" + FormatSize(sizeReady, footprint, height);
        buffer[1] = "Visual:" + visualReady + "/" + visuals.Length;
        buffer[2] = "Interact:" + interactReady + "/" + interactors.Length;
        buffer[3] = "Score:" + Mathf.RoundToInt(total * 100f);
        report.run++;
        report.score = total;
        report.lines = Copy(buffer);
        int limit = loggers.Length;
        for (int i = 0; i < limit; i++) loggers[i].Render();
    }

    bool Size(out float footprint, out float height)
    {
        bool set = false;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        int total = visuals.Length;
        for (int i = 0; i < total; i++)
        {
            Renderer item = visuals[i];
            if (item == null) continue;
            if (!set)
            {
                bounds = item.bounds;
                set = true;
            }
            else bounds.Encapsulate(item.bounds);
        }
        footprint = 0f;
        height = 0f;
        if (!set) return false;
        footprint = bounds.size.x * bounds.size.z;
        height = bounds.size.y;
        return footprint >= minFootprint && height >= minHeight;
    }

    int CountVisuals(Renderer[] list)
    {
        int ready = 0;
        int total = list.Length;
        for (int i = 0; i < total; i++)
        {
            Renderer item = list[i];
            if (item != null && item.enabled && item.sharedMaterial != null) ready++;
        }
        return ready;
    }

    int CountInteract(Collider[] list)
    {
        int ready = 0;
        int total = list.Length;
        for (int i = 0; i < total; i++)
        {
            Collider item = list[i];
            if (item != null && item.enabled && item.gameObject.activeInHierarchy) ready++;
        }
        return ready;
    }

    float Score(int ready, int total)
    {
        if (total < 1) return 0f;
        return (float)ready / (float)total;
    }

    string FormatSize(bool pass, float footprint, float height)
    {
        return (pass ? "OK" : "NG") + "(" + Mathf.Round(footprint) + "/" + minFootprint + "," + Mathf.Round(height) + "/" + minHeight + ")";
    }

    string[] Copy(string[] source)
    {
        int len = source.Length;
        string[] target = new string[len];
        for (int i = 0; i < len; i++) target[i] = source[i];
        return target;
    }
}

public class FeedbackLogger : UdonSharpBehaviour
{
    public FeedbackReport report;
    public Text screen;

    public void Render()
    {
        string[] lines = report.lines;
        int count = lines.Length;
        string text = "";
        for (int i = 0; i < count; i++)
        {
            if (i > 0) text += "\n";
            text += lines[i];
        }
        screen.text = text;
    }
}
