using HarmonyLib;
using UnityEngine;
using System.IO;
using Sounds;
using BepInEx;
using static IAmYourTranslator.CommonFunctions;

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

                string clipName = source.clip.name;
                if (string.IsNullOrEmpty(clipName))
                    return;

                // The folder where replaced audio is stored
                string audioFolder = Path.Combine(Paths.ConfigPath, "IAmYourTranslator", "audio");
                string wavPath = Path.Combine(audioFolder, SanitizeFileName(clipName) + ".wav");

                if (!File.Exists(wavPath))
                {
                    //Logging.Warn($"[SoundManagerPatch] No replacement for '{clipName}'");
                    return; // We don't replace anything if the file doesn't exist
                }


                // Loading new AudioClip
                AudioClip newClip = AudioClipReplacer.LoadAudioClip(wavPath);
                if (newClip == null)
                {
                    Logging.Warn($"[SoundManagerPatch] Failed to load '{wavPath}' for '{clipName}'");
                    return;
                }

                // Replacing clip before Play()
                source.clip = newClip;
                source.time = 0f;
                Logging.Info($"[SoundManagerPatch] Replaced SoundObject: {clipName} -> {newClip.name}");
            }
            catch (System.Exception ex)
            {
                Logging.Error($"[SoundManagerPatch] Error replacing SoundObject: {ex}");
            }
        }

        // Utility for safe names
        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
