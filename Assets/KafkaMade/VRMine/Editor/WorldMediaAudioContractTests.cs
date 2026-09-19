#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KafkaMade.VRMine.Editor
{
    public static class WorldMediaAudioContractTests
    {
        [Serializable] private sealed class Contract { public Player player; public AudioLink audiolink; public Platforms platforms; }
        [Serializable] private sealed class Player { public string abstraction; public string production_provider; public bool single_playback_authority; }
        [Serializable] private sealed class AudioLink { public string binding; public bool silent_fallback; }
        [Serializable] private sealed class Platforms { public Platform pc; public Platform android; }
        [Serializable] private sealed class Platform { public int latency_target_ms; }

        [MenuItem("VRMine/Verify/World Media Audio Contract")]
        public static void Verify()
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "config/world-media-audio.json");
            if (!File.Exists(path)) throw new InvalidOperationException("world media/audio contract is missing");
            var c = JsonUtility.FromJson<Contract>(File.ReadAllText(path));
            if (c?.player == null || string.IsNullOrEmpty(c.player.abstraction) || string.IsNullOrEmpty(c.player.production_provider))
                throw new InvalidOperationException("player abstraction/provider is missing");
            if (!c.player.single_playback_authority) throw new InvalidOperationException("playback authority is not singular");
            if (c.audiolink == null || c.audiolink.binding != "runtime_discovery" || c.audiolink.silent_fallback)
                throw new InvalidOperationException("AudioLink rebind policy is invalid");
            if (c.platforms?.pc == null || c.platforms.android == null || c.platforms.pc.latency_target_ms == c.platforms.android.latency_target_ms)
                throw new InvalidOperationException("PC/Android latency contracts must be explicit and distinct");
            Debug.Log("PASS: VRMine world media/audio editor contract");
        }
    }
}
#endif
