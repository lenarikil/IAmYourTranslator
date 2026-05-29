using System;
using HarmonyLib;
using UnityEngine;
using static IAmYourTranslator.CommonFunctions;
using IAmYourTranslator.json;

namespace IAmYourTranslator.Patches
{
    [HarmonyPatch(typeof(MusicObject), nameof(MusicObject.Play))]
    public static class CreditsAudioPatch
    {
        [HarmonyPrefix]
        static void Prefix(MusicObject __instance)
        {
            try
            {
                AudioSource source = GetAudioSource(__instance);
                if (source?.clip == null)
                    return;

                if (!Plugin.EnableAudioReplacementEntry.Value || LanguageManager.CurrentSummary == null)
                    return;

                string clipName = source.clip.name;
                if (string.IsNullOrEmpty(clipName))
                    return;

                string audioDir = LanguageManager.CurrentSummary.Paths.AudioDir;
                if (!AudioClipReplacer.TryFindReplacementAudioFile(audioDir, clipName, out string replacementPath))
                    return;

                AudioClip newClip = AudioClipReplacer.LoadAudioClip(replacementPath);
                if (newClip == null)
                {
                    Logging.Warn($"[CreditsAudio] Failed to load '{replacementPath}' for '{clipName}'");
                    return;
                }

                IAmYourTranslator.Core.AudioLifecycle.RegisterReplacedAudioSource(source, source.clip);
                source.clip = newClip;
                source.time = 0f;
                Logging.Info($"[CreditsAudio] Replaced '{clipName}' -> '{newClip.name}'");
            }
            catch (Exception e)
            {
                Logging.Error($"[CreditsAudio] Error replacing audio: {e}");
            }
        }

        private static AudioSource GetAudioSource(MusicObject instance)
        {
            if (instance == null)
                return null;

            var field = AccessTools.Field(typeof(MusicObject), "source");
            if (field != null)
            {
                var source = field.GetValue(instance) as AudioSource;
                if (source != null)
                    return source;
            }

            return instance.GetComponentInChildren<AudioSource>();
        }
    }
}
