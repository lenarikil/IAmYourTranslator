using System;
using System.IO;
using System.Collections;
using HarmonyLib;
using AudioTextSynchronizer;
using AudioTextSynchronizer.Core;
using IAmYourTranslator.json;
using UnityEngine;
using BepInEx;
using static IAmYourTranslator.CommonFunctions;
using System.Collections.Generic;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(TextSynchronizer))]
    public static class TextSynchronizerPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch("set_Timings")]
        public static void OnTimingsSet_Prefix(TextSynchronizer __instance, ref PhraseAsset value)
        {
            try
            {
                if (value == null || value.Timings == null)
                    return;

                string phraseName = value.name;
                var translationsDict = LanguageManager.GetTranslations();

                if (translationsDict.TryGetValue(phraseName, out var translations))
                {
                    // translations exist → just apply them
                    for (int i = 0; i < value.Timings.Count; i++)
                    {
                        if (i < translations.Count)
                            value.Timings[i].Text = translations[i];
                    }
                }
                else
                {
                    // === no translation → add original to CurrentLanguage.timings ===
                    List<string> originalTimings = value.Timings.ConvertAll(t => t.Text);
                    LanguageManager.CurrentLanguage.timings[phraseName] = originalTimings;

                    // save to JSON
                    LanguageManager.SaveCurrentLanguage();

                    Logging.Warn($"[TextSynchronizerPatch] Added missing translation for '{phraseName}'");
                }
            }
            catch (Exception e)
            {
                Logging.Error($"[TextSynchronizerPatch] Prefix error: {e}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("set_Timings")]
        public static void OnTimingsSet_Postfix(TextSynchronizer __instance, PhraseAsset value)
        {
            if (__instance == null || value == null)
                return;

            __instance.StartCoroutine(DelayedAudioProcess(__instance, value));
        }

        private static IEnumerator DelayedAudioProcess(TextSynchronizer instance, PhraseAsset value)
        {
            // wait one frame so the game can assign Source.clip
            yield return null;

            if (instance?.Source == null)
            {
                Logging.Warn($"[TextSynchronizerPatch] No AudioSource for '{value?.name}'");
                yield break;
            }

            string phraseName = value?.name ?? "Unknown";
            string basePath = Path.Combine(Paths.ConfigPath, "IAmYourTranslator", "audio");
            if (!Directory.Exists(basePath))
                Directory.CreateDirectory(basePath);

            string safeName = SanitizeFileName(phraseName);
            string wavPath = Path.Combine(basePath, safeName + ".wav");

            AudioSource mainSource = instance.Source;
            AudioClip originalClip = mainSource.clip;

            mainSource.volume = 0f; // mute the original during processing

            // export the original if custom file doesn't exist
            if (!File.Exists(wavPath))
            {
                if (originalClip != null)
                {
                    AudioClipReplacer.ExportAudioClipToWav(originalClip, wavPath);
                    mainSource.volume = 1f; // restore volume
                    Logging.Info($"[TextSynchronizerPatch] Exported original audio for '{phraseName}'");
                }
                else
                {
                    Logging.Info($"[TextSynchronizerPatch] No clip to export for '{phraseName}' (source.clip == null)");
                }
                yield break;
            }

            // load custom clip
            AudioClip newClip = AudioClipReplacer.LoadAudioClip(wavPath);
            if (newClip == null)
            {
                Logging.Warn($"[TextSynchronizerPatch] Failed to load custom clip for '{phraseName}'");
                yield break;
            }

            Logging.Info($"[TextSynchronizerPatch] Applying replacement clip for '{phraseName}' ({newClip.name})");

            // if Source.clip is not null — stop it
            if (mainSource.isPlaying)
                mainSource.Stop();

            // replace clip with new one
            mainSource.clip = newClip;
            mainSource.time = 0f; // start from the beginning
            mainSource.volume = 1f;

            // play the new clip
            mainSource.Play();
            Logging.Info($"[TextSynchronizerPatch] Replacement clip started for '{phraseName}'");

            // wait until audio ends or Source is stopped (for example, skip cutscene)
            while (mainSource.isPlaying && mainSource.clip == newClip)
                yield return null;

            Logging.Info($"[TextSynchronizerPatch] Replacement clip finished for '{phraseName}'");
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
