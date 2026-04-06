using HarmonyLib;
using UnityEngine;
using System.IO;
using Sounds;
using BepInEx;
using static IAmYourTranslator.CommonFunctions;
using IAmYourTranslator.json;

namespace IAmYourTranslator.HarmonyPatches
{
    [HarmonyPatch(typeof(SoundManager))]
    [HarmonyPatch("CreateSound")]
    public static class SoundManagerCreateSoundPatch
    {
        // Postfix with ref to catch SoundObject after Instantiate
        [HarmonyPostfix]
        public static void Postfix(ref SoundObject __result)
        {
            try
            {
                if (__result == null)
                    return;

                AudioSource source = __result.GetComponent<AudioSource>();
                if (source == null || source.clip == null)
                    return;

                if (!Plugin.EnableAudioReplacementEntry.Value || LanguageManager.CurrentSummary == null)
                    return;

                string clipName = source.clip.name;
                if (string.IsNullOrEmpty(clipName))
                    return;

                // The folder where replaced audio is stored
                string audioFolder = LanguageManager.CurrentSummary.Paths.AudioDir;
                if (!AudioClipReplacer.TryFindReplacementAudioFile(audioFolder, clipName, out string replacementPath))
                    return;


                // Loading new AudioClip
                AudioClip newClip = AudioClipReplacer.LoadAudioClip(replacementPath);
                if (newClip == null)
                {
                    Logging.Warn($"[SoundManagerPatch] Failed to load '{replacementPath}' for '{clipName}'");
                    return;
                }

                // Replacing clip before Play()
                Plugin.RegisterReplacedAudioSource(source, source.clip);
                source.clip = newClip;
                source.time = 0f;
                Logging.Info($"[SoundManagerPatch] Replaced SoundObject: {clipName} -> {newClip.name} ({Path.GetFileName(replacementPath)})");
            }
            catch (System.Exception ex)
            {
                Logging.Error($"[SoundManagerPatch] Error replacing SoundObject: {ex}");
            }
        }
    }
}
