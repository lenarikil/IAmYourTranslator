using System;
using HarmonyLib;
using UnityEngine;
using BepInEx;
using System.IO;
using static IAmYourTranslator.CommonFunctions;
using IAmYourTranslator.json;

namespace IAmYourTranslator.HarmonyPatches
{
    [HarmonyPatch]
    public static class AudioSourceLoggerPatch
    {
        private static bool IsDebugLoggingEnabled()
        {
            return Plugin.EnableAudioDebugLogsEntry != null && Plugin.EnableAudioDebugLogsEntry.Value;
        }

        private static bool IsExperimentalRadioAudioEnabled()
        {
            return Plugin.EnableExperimentalRadioAudioPatchesEntry != null && Plugin.EnableExperimentalRadioAudioPatchesEntry.Value;
        }

        // === AudioSource.Play() ===
        [HarmonyPostfix]
        [HarmonyPatch(typeof(AudioSource), nameof(AudioSource.Play), new Type[] { })]
        static void Postfix_Play(AudioSource __instance)
        {
            try
            {
                if (!IsDebugLoggingEnabled())
                    return;
                if (__instance == null)
                    return;
                if (__instance.clip == null)
                    return;

                var go = __instance.gameObject;
                string hierarchy = GetHierarchy(go, 3);
                string clipName = __instance.clip ? __instance.clip.name : "null";

                Logging.Info($"[AudioLog] AudioSource.Play() called | Clip='{clipName}' | Object: {hierarchy}");
            }
            catch (Exception e)
            {
                Logging.Error($"[AudioLog] Error while logging AudioSource.Play(): {e}");
            }
        }

        // === AudioSource.Play() === // A more complex method of sound replacement if other methods couldn't be found. Used at your own risk.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(AudioSource), nameof(AudioSource.Play), new Type[] { })]
        static void Prefix_Play(AudioSource __instance)
        {
            try
            {
                if (!IsExperimentalRadioAudioEnabled())
                    return;
                if (__instance == null)
                    return;
                if (__instance.clip == null)
                    return;

                var go = __instance.gameObject;
                string hierarchy = GetHierarchy(go, 3);

                if (hierarchy == "Helmet Camera(Clone)")
                {
                    string dumpPath = Path.Combine(Paths.ConfigPath, "AudioDumps");
                    //AudioClipRawExtractor.TryDumpRawAudioData(__instance.clip, dumpPath);
                }
                if (hierarchy == "Head Anchor → Enemy Radio Camera Pivot → Helmet Camera Bone → Helmet Camera(Clone)")
                {
                    string dumpPath = Path.Combine(Paths.ConfigPath, "AudioDumps");
                    //AudioClipRawExtractor.TryDumpRawAudioData(__instance.clip, dumpPath);
                }
                
                if (__instance.clip.name == "14 - same team" || __instance.clip.name == "iaybendcreditstest")
                {
                    if (Plugin.EnableAudioReplacementEntry.Value && LanguageManager.CurrentSummary != null)
                    {
                        string audioFile = Path.Combine(LanguageManager.CurrentSummary.Paths.AudioDir, __instance.clip.name + ".wav");
                        AudioClip newClip = AudioClipReplacer.LoadAudioClip(audioFile);
                        if (newClip != null)
                        {
                            Plugin.RegisterReplacedAudioSource(__instance, __instance.clip);
                            __instance.clip = newClip;
                            __instance.time = 0f;
                            if (IsDebugLoggingEnabled())
                                Logging.Info($"[AudioLog] AudioSource clip replaced with '{newClip.name}'");
                        }
                        else
                        {
                            if (IsDebugLoggingEnabled())
                                Logging.Warn($"[AudioLog] Failed to load '{audioFile}' for AudioSource clip replacement");
                        }
                    }
                }
                //Logging.Info($"[AudioLog] AudioSource.Play() called | Clip='{clipName}' | Object: {hierarchy}");
            }
            catch (Exception e)
            {
                Logging.Error($"[AudioLog] Error while logging AudioSource.Play(): {e}");
            }
        }
        

        // === AudioSource.Play(ulong delay) ===
        [HarmonyPostfix]
        [HarmonyPatch(typeof(AudioSource), nameof(AudioSource.Play), new Type[] { typeof(ulong) })]
        static void Postfix_PlayWithDelay(AudioSource __instance)
        {
            try
            {
                if (!IsDebugLoggingEnabled())
                    return;
                if (__instance == null)
                    return;

                var go = __instance.gameObject;
                string hierarchy = GetHierarchy(go, 3);
                string clipName = __instance.clip ? __instance.clip.name : "null";

                Logging.Info($"[AudioLog] AudioSource.Play(ulong delay) | Clip='{clipName}' | Object: {hierarchy}");
            }
            catch (Exception e)
            {
                Logging.Error($"[AudioLog] Error while logging Play(ulong delay): {e}");
            }
        }

        // === AudioSource.PlayOneShot(AudioClip) ===
        [HarmonyPostfix]
        [HarmonyPatch(typeof(AudioSource), nameof(AudioSource.PlayOneShot), new Type[] { typeof(AudioClip) })]
        static void Postfix_PlayOneShot(AudioSource __instance, AudioClip clip)
        {
            try
            {
                if (!IsDebugLoggingEnabled())
                    return;
                if (__instance == null)
                    return;

                var go = __instance.gameObject;
                string hierarchy = GetHierarchy(go, 3);
                string clipName = clip ? clip.name : "null";

                Logging.Info($"[AudioLog] AudioSource.PlayOneShot(AudioClip) | Clip='{clipName}' | Object: {hierarchy}");
            }
            catch (Exception e)
            {
                Logging.Error($"[AudioLog] Error while logging PlayOneShot(AudioClip): {e}");
            }
        }

        // === AudioSource.PlayOneShot(AudioClip, float volumeScale) ===
        [HarmonyPostfix]
        [HarmonyPatch(typeof(AudioSource), nameof(AudioSource.PlayOneShot), new Type[] { typeof(AudioClip), typeof(float) })]
        static void Postfix_PlayOneShotVolume(AudioSource __instance, AudioClip clip, float volumeScale)
        {
            try
            {
                if (!IsDebugLoggingEnabled())
                    return;
                if (__instance == null)
                    return;

                var go = __instance.gameObject;
                string hierarchy = GetHierarchy(go, 3);
                string clipName = clip ? clip.name : "null";

                Logging.Info($"[AudioLog] AudioSource.PlayOneShot(AudioClip, float={volumeScale}) | Clip='{clipName}' | Object: {hierarchy}");
            }
            catch (Exception e)
            {
                Logging.Error($"[AudioLog] Error while logging PlayOneShot(AudioClip, float): {e}");
            }
        }

        // === Additionally: PlayClipAtPoint (if needed) ===
        [HarmonyPostfix]
        [HarmonyPatch(typeof(AudioSource), nameof(AudioSource.PlayClipAtPoint), new Type[] { typeof(AudioClip), typeof(Vector3) })]
        static void Postfix_PlayClipAtPoint(AudioClip clip, Vector3 position)
        {
            try
            {
                if (!IsDebugLoggingEnabled())
                    return;
                string clipName = clip ? clip.name : "null";
                Logging.Info($"[AudioLog] AudioSource.PlayClipAtPoint() | Clip='{clipName}' | Pos={position}");
            }
            catch (Exception e)
            {
                Logging.Error($"[AudioLog] Error while logging PlayClipAtPoint(): {e}");
            }
        }

        // === Helper method ===
        private static string GetHierarchy(GameObject go, int depth)
        {
            if (go == null)
                return "null";

            string result = go.name;
            Transform current = go.transform;
            int count = 0;

            while (current.parent != null && count < depth)
            {
                current = current.parent;
                result = current.name + " → " + result;
                count++;
            }

            return result;
        }
    }
}
