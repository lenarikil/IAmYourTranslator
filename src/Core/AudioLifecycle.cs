using System.Collections.Generic;
using UnityEngine;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.Core
{
    internal static class AudioLifecycle
    {
        private static readonly Dictionary<int, AudioClip> OriginalClipBySourceId = new Dictionary<int, AudioClip>();

        public static void RegisterReplacedAudioSource(AudioSource source, AudioClip originalClip)
        {
            if (source == null || originalClip == null)
                return;

            int id = source.GetInstanceID();
            if (!OriginalClipBySourceId.ContainsKey(id))
                OriginalClipBySourceId[id] = originalClip;
        }

        public static void RestoreReplacedAudioSources()
        {
            try
            {
                if (OriginalClipBySourceId.Count == 0)
                    return;

                var allSources = FindObjectsOfTypeCached<AudioSource>(true);
                if (allSources == null || allSources.Length == 0)
                    return;

                int restored = 0;
                var liveIds = new HashSet<int>();
                foreach (var source in allSources)
                {
                    if (source == null)
                        continue;

                    int id = source.GetInstanceID();
                    liveIds.Add(id);
                    if (!OriginalClipBySourceId.TryGetValue(id, out var originalClip) || originalClip == null)
                        continue;

                    if (source.clip == originalClip)
                        continue;

                    bool wasPlaying = source.isPlaying;
                    float previousTime = source.time;
                    source.clip = originalClip;
                    if (wasPlaying)
                    {
                        float clamped = Mathf.Clamp(previousTime, 0f, Mathf.Max(0f, originalClip.length - 0.01f));
                        source.time = clamped;
                        source.Play();
                    }
                    restored++;
                }

                var staleKeys = new List<int>();
                foreach (var id in OriginalClipBySourceId.Keys)
                {
                    if (!liveIds.Contains(id))
                        staleKeys.Add(id);
                }
                foreach (var id in staleKeys)
                    OriginalClipBySourceId.Remove(id);

                if (restored > 0)
                    Logging.Info($"[Plugin] Restored original audio clip on {restored} active AudioSource components.");
            }
            catch (System.Exception e)
            {
                Logging.Warn($"Failed to restore replaced audio sources: {e.Message}");
            }
        }
    }
}
