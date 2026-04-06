using BepInEx;
using HarmonyLib;
using System.IO;
using UnityEngine;
using static IAmYourTranslator.CommonFunctions;
using IAmYourTranslator.json;

namespace IAmYourTranslator.HarmonyPatches
{
    [HarmonyPatch(typeof(HUDEnemyRadioCamera), nameof(HUDEnemyRadioCamera.SetStatic))]
    public static class HUDEnemyRadioCamera_SetStatic_Patch
    {
        private static readonly System.Reflection.FieldInfo FieldActiveVolume = AccessTools.Field(typeof(HUDEnemyRadioCamera), "activeVolume");
        private static readonly System.Reflection.FieldInfo FieldInactiveVolume = AccessTools.Field(typeof(HUDEnemyRadioCamera), "inactiveVolume");

        private static bool IsExperimentalEnabled()
        {
            return Plugin.EnableExperimentalRadioAudioPatchesEntry != null &&
                   Plugin.EnableExperimentalRadioAudioPatchesEntry.Value;
        }

        [HarmonyPrefix]
        private static bool Prefix(HUDEnemyRadioCamera __instance, bool enableStatic, ref (AudioClip clip, float time, bool wasPlaying) __state)
        {
            if (!IsExperimentalEnabled())
                return true;

            __state = (null, 0f, false);
            if (!enableStatic || __instance == null)
                return true;

            AudioSource source = GetAudioSource(__instance);
            if (source == null)
                return true;

            if (source.isPlaying)
            {
                __state = (source.clip, source.time, true);

                // Apply static visuals manually but skip the internal Stop().
                var activeVolume = FieldActiveVolume?.GetValue(__instance) as Component;
                var inactiveVolume = FieldInactiveVolume?.GetValue(__instance) as Component;
                if (activeVolume != null)
                    activeVolume.gameObject.SetActive(false);
                if (inactiveVolume != null)
                    inactiveVolume.gameObject.SetActive(true);

                return false;
            }

            return true;
        }

        [HarmonyPostfix]
        private static void Postfix(HUDEnemyRadioCamera __instance, bool enableStatic, (AudioClip clip, float time, bool wasPlaying) __state)
        {
            if (!IsExperimentalEnabled())
                return;

            if (!enableStatic || !__state.wasPlaying || __instance == null)
                return;

            AudioSource source = GetAudioSource(__instance);
            if (source == null)
                return;

            // Restore playback if it was stopped by SetStatic.
            if (!source.isPlaying && __state.clip != null)
            {
                source.clip = __state.clip;
                source.time = Mathf.Clamp(__state.time, 0f, __state.clip.length);
                source.Play();
            }
        }

        private static AudioSource GetAudioSource(HUDEnemyRadioCamera camera)
        {
            var field = camera.GetType().GetField("audioSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            AudioSource source = null;
            if (field != null)
                source = field.GetValue(camera) as AudioSource;

            if (source == null)
                source = camera.GetComponentInChildren<AudioSource>();

            return source;
        }
    }

    [HarmonyPatch(typeof(HUDEnemyRadioCamera), nameof(HUDEnemyRadioCamera.StopAudio))]
    public static class HUDEnemyRadioCamera_StopAudio_Patch
    {
        private static bool IsExperimentalEnabled()
        {
            return Plugin.EnableExperimentalRadioAudioPatchesEntry != null &&
                   Plugin.EnableExperimentalRadioAudioPatchesEntry.Value;
        }

        [HarmonyPrefix]
        private static bool Prefix(HUDEnemyRadioCamera __instance)
        {
            if (!IsExperimentalEnabled())
                return true;

            if (__instance == null)
                return true;

            AudioSource source = GetAudioSource(__instance);
            if (source == null)
                return true;

            // If still playing, skip the stop to avoid cutting off dialogue.
            if (source.isPlaying)
                return false;

            return true;
        }

        private static AudioSource GetAudioSource(HUDEnemyRadioCamera camera)
        {
            var field = camera.GetType().GetField("audioSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            AudioSource source = null;
            if (field != null)
                source = field.GetValue(camera) as AudioSource;

            if (source == null)
                source = camera.GetComponentInChildren<AudioSource>();

            return source;
        }
    }

    [HarmonyPatch(typeof(HUDEnemyRadioCamera), nameof(HUDEnemyRadioCamera.PlayAudio))]
    public static class HUDEnemyRadioCamera_PlayAudio_Patch
    {
        [HarmonyPostfix]
        static void Postfix(HUDEnemyRadioCamera __instance, AudioClip clip)
        {
            try
            {
                if (clip == null) return;
                if (__instance == null) return;
                if (!Plugin.EnableAudioReplacementEntry.Value || LanguageManager.CurrentSummary == null)
                    return;

                // --- Directories ---
                string audioDir = LanguageManager.CurrentSummary.Paths.AudioDir;
                Directory.CreateDirectory(audioDir);

                if (!AudioClipReplacer.TryFindReplacementAudioFile(audioDir, clip.name, out string replacementFile))
                {
                    if (Plugin.EnableAudioDebugLogsEntry != null && Plugin.EnableAudioDebugLogsEntry.Value)
                        Logging.Info($"[AudioReplace] No radio replacement found for clip '{clip.name}'");
                    return;
                }

                // --- Get AudioSource ---
                var field = __instance.GetType().GetField("audioSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                AudioSource source = null;
                if (field != null)
                    source = field.GetValue(__instance) as AudioSource;

                if (source == null)
                    source = __instance.GetComponentInChildren<AudioSource>();

                if (source == null)
                {
                    Logging.Warn("[AudioPatch] AudioSource not found in HUDEnemyRadioCamera.");
                    return;
                }

                Plugin.RegisterReplacedAudioSource(source, source.clip);
                Logging.Info($"[AudioReplace] Radio replacement found for '{clip.name}': '{Path.GetFileName(replacementFile)}'");
                AudioClipReplacer.ReplaceAudioClip(source, replacementFile);
            }
            catch (System.Exception ex)
            {
                Logging.Error($"[AudioPatch] Error processing audio: {ex}");
            }
        }
    }
}
