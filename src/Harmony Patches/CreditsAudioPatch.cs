using System;
using HarmonyLib;
using UnityEngine;
using static IAmYourTranslator.CommonFunctions;
using IAmYourTranslator.json;

namespace IAmYourTranslator.HarmonyPatches
{
    [HarmonyPatch(typeof(MusicObject), nameof(MusicObject.Play))]
    public static class CreditsAudioPatch
    {
        [HarmonyPrefix]
        static void Prefix(MusicObject __instance)
        {
            try
            {
                if (__instance?.source?.clip == null)
                    return;

                if (!Plugin.EnableAudioReplacementEntry.Value || LanguageManager.CurrentSummary == null)
                    return;

                string clipName = __instance.source.clip.name;
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

                Plugin.RegisterReplacedAudioSource(__instance.source, __instance.source.clip);
                __instance.source.clip = newClip;
                __instance.source.time = 0f;
                Logging.Info($"[CreditsAudio] Replaced '{clipName}' -> '{newClip.name}'");
            }
            catch (Exception e)
            {
                Logging.Error($"[CreditsAudio] Error replacing audio: {e}");
            }
        }
    }
}
