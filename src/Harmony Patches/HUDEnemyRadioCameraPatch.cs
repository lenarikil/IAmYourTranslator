using BepInEx;
using HarmonyLib;
using System.IO;
using UnityEngine;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.HarmonyPatches
{
    [HarmonyPatch(typeof(HUDEnemyRadioCamera), nameof(HUDEnemyRadioCamera.PlayAudio))]
    public static class HUDEnemyRadioCamera_PlayAudio_Patch
    {
        [HarmonyPostfix]
        static void Postfix(HUDEnemyRadioCamera __instance, AudioClip clip)
        {
            try
            {
                if (clip == null) return;

                // --- Directories ---
                string audioDir = Path.Combine(Paths.ConfigPath, "IAmYourTranslator", "Audio");
                string dumpDir = Path.Combine(Paths.ConfigPath, "IAmYourTranslator", "AudioDumps");
                Directory.CreateDirectory(audioDir);
                Directory.CreateDirectory(dumpDir);

                string safeName = SanitizeFileName(clip.name);
                string replacementFile = Path.Combine(audioDir, $"{safeName}.ogg");
                string dumpFile = Path.Combine(dumpDir, $"{safeName}.ogg");

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

                // --- If replacement exists — use it ---
                if (File.Exists(replacementFile))
                {
                    Logging.Info($"[AudioReplace] Replacement found for '{safeName}', replacing...");
                    AudioClipReplacer.ReplaceAudioClip(source, replacementFile);
                    return; // replacement found — no dump
                }

                // --- No replacement — check if dump already exists ---
                if (File.Exists(dumpFile))
                {
                    Logging.Warn($"[AudioDump] ⚠️ File already exists, skipped: {dumpFile}");
                    return;
                }

                // --- Initialize AudioCapture for dumping ---
                var capture = source.gameObject.GetComponent<IAmYourTranslator.AudioCapture>();
                if (capture == null)
                    capture = source.gameObject.AddComponent<IAmYourTranslator.AudioCapture>();

                capture.StartCapture(dumpFile, quality: 10);
                Logging.Info($"[AudioDump] 🎙️ Started dumping '{clip.name}' → {dumpFile}");
            }
            catch (System.Exception ex)
            {
                Logging.Error($"[AudioPatch] Error processing audio: {ex}");
            }
        }

        static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
