using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class GaussianVideoPlaylist : UdonSharpBehaviour
{
    public UdonBehaviour syncPlayer;
    public VRCUrl[] urls;
    public string[] titles;
    public bool[] requiresUntrustedUrls;
    public Text statusText;
    public float rateLimitSeconds = 5f;
    public int selectedIndex;

    float nextAllowedRequestTime;

    void Start()
    {
        if (rateLimitSeconds < 5f) rateLimitSeconds = 5f;
        if (urls == null || urls.Length == 0)
        {
            SetStatus("Playlist is empty");
            return;
        }
        if (selectedIndex < 0 || selectedIndex >= urls.Length) selectedIndex = 0;
        SetStatus(CurrentTitle());
    }

    public void Select(int index)
    {
        if (syncPlayer == null || urls == null || index < 0 || index >= urls.Length)
        {
            SetStatus("Playlist configuration error");
            return;
        }

        VRCUrl url = urls[index];
        if (url == null || string.IsNullOrEmpty(url.Get()))
        {
            SetStatus("Playback URL is unavailable");
            return;
        }

        float now = Time.realtimeSinceStartup;
        if (now < nextAllowedRequestTime)
        {
            int seconds = Mathf.CeilToInt(nextAllowedRequestTime - now);
            SetStatus("URL cooldown: " + seconds + "s");
            return;
        }

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        if (localPlayer != null)
        {
            Networking.SetOwner(localPlayer, syncPlayer.gameObject);
        }

        selectedIndex = index;
        nextAllowedRequestTime = now + Mathf.Max(5f, rateLimitSeconds);

        // The SDK sample exposes its synchronized `url` variable. SetProgramVariable
        // triggers its OnVariableChanged handler, which calls PlayURL, then the
        // sample's own serialization/time-sync path keeps the shared player aligned.
        syncPlayer.SetProgramVariable("url", url);
        syncPlayer.RequestSerialization();

        string trust = requiresUntrustedUrls != null &&
            index < requiresUntrustedUrls.Length &&
            requiresUntrustedUrls[index]
            ? " (Allow Untrusted URLs required)"
            : "";
        SetStatus(CurrentTitle() + trust);
    }

    public void Previous()
    {
        if (urls == null || urls.Length == 0) return;
        int index = selectedIndex - 1;
        if (index < 0) index = urls.Length - 1;
        Select(index);
    }

    public void Next()
    {
        if (urls == null || urls.Length == 0) return;
        int index = selectedIndex + 1;
        if (index >= urls.Length) index = 0;
        Select(index);
    }

    public void Replay()
    {
        Select(selectedIndex);
    }

    string CurrentTitle()
    {
        if (titles == null || selectedIndex < 0 || selectedIndex >= titles.Length)
        {
            return "Source video";
        }
        return (selectedIndex + 1).ToString("00") + "  " + titles[selectedIndex];
    }

    void SetStatus(string value)
    {
        if (statusText != null) statusText.text = value;
    }
}
