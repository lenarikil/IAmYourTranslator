using System;
using HarmonyLib;
using UnityEngine;
using TMPro;
using static IAmYourTranslator.CommonFunctions;
using IAmYourTranslator.json;
using System.IO;
using BepInEx;
using System.Reflection;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(TutorialDialogueManager))]
    public static class TutorialDialogueManagerPatch
    {
        // ==========================
        // PREFIX: text translation
        // ==========================
        [HarmonyPrefix]
        [HarmonyPatch("TriggerDialogue")]
        public static void TriggerDialogue_Prefix(ref string dialogue, AudioClip clip)
        {
            try
            {
                string clipName = clip != null ? clip.name : "null";
                Logging.Info($"[TutorialDialogueManagerPatch] TriggerDialogue called:");
                Logging.Info($"  Original text: \"{dialogue}\"");
                Logging.Info($"  AudioClip: {clipName}");

                // Use language file for translations
                if (LanguageManager.IsLoaded && LanguageManager.CurrentLanguage?.tutorialPrompts != null)
                {
                    string translated;
                    if (LanguageManager.CurrentLanguage.tutorialPrompts.TryGetValue(clipName, out translated) && !string.IsNullOrEmpty(translated))
                    {
                        dialogue = translated;
                        Logging.Info($"  Translated from file: \"{dialogue}\"");
                    }
                    else
                    {
                        // Add missing key
                        LanguageManager.CurrentLanguage.tutorialPrompts[clipName] = dialogue;
                        LanguageManager.SaveCurrentLanguage();
                        Logging.Warn($"  Added missing tutorial dialogue key: '{clipName}'");
                    }
                }
                else
                {
                    // Original mode - use hardcoded translations for backward compatibility
                    switch (clipName)
                    {
                        case "2 - sprinting":
                            dialogue = "I was running through the forest. I was making my way through the underbrush, as if I were a child again, not thinking about the mess or that later I might have to mend my pants.";
                            break;

                        case "3 - leaping":
                            dialogue = "Leaping over trees.";
                            break;

                        case "4 - best days of the coi":
                            dialogue = "This reminded me of the best days at the ISP. When I felt like everything was going well for me.";
                            break;

                        default:
                            Logging.Warn($"[TutorialDialogueManagerPatch] No translation found for '{clipName}'");
                            break;
                    }
                }

                Logging.Info($"  Final text: \"{dialogue}\"");
            }
            catch (Exception e)
            {
                Logging.Error($"[TutorialDialogueManagerPatch] Error in Prefix: {e}");
            }
        }

        // ==========================
        // POSTFIX: audio replacement + font application
        // ==========================
        [HarmonyPostfix]
        [HarmonyPatch("TriggerDialogue")]
        public static void TriggerDialogue_Postfix(TutorialDialogueManager __instance, AudioClip clip)
        {
            try
            {
                // Apply font to all TMP components in the dialogue manager
                var tmpFont = TMPFontReplacer.GetCachedFont(Plugin.GlobalFontPath);
                if (tmpFont != null)
                {
                    var allTmpTexts = __instance.GetComponentsInChildren<TMP_Text>(true);
                    foreach (var tmp in allTmpTexts)
                    {
                        if (tmp != null)
                        {
                            TMPFontReplacer.ApplyFontToTMP(tmp, tmpFont);
                        }
                    }
                    Logging.Info($"[TutorialDialogueManager] Applied font to {allTmpTexts.Length} TMP components");
                }

                if (clip == null) return;

                var dialogueSourceField = AccessTools.Field(typeof(TutorialDialogueManager), "dialogueSource");
                AudioSource source = (AudioSource)dialogueSourceField.GetValue(__instance);

                var currentDialogueField = AccessTools.Field(typeof(TutorialDialogueManager), "currentDialogue");
                var characterIncreaseTimerField = AccessTools.Field(typeof(TutorialDialogueManager), "characterIncreaseTimer");
                var hideTimerField = AccessTools.Field(typeof(TutorialDialogueManager), "hideTimer");

                string clipName = clip.name;
                string audioFile = null;

                switch (clipName)
                {
                    case "2 - sprinting":
                        if (Plugin.EnableAudioReplacementEntry.Value && LanguageManager.CurrentSummary != null)
                            audioFile = Path.Combine(LanguageManager.CurrentSummary.Paths.AudioDir, "2 - sprinting.ogg");
                        break;

                        // Other cases with audio can be added
                }

                if (!string.IsNullOrEmpty(audioFile))
                {
                    AudioClipReplacer.ReplaceAudioClip(source, audioFile);

                    AudioClip newClip = source.clip;
                    if (newClip != null && newClip.length > 0)
                    {
                        string text = (string)currentDialogueField.GetValue(__instance);
                        int totalChars = Math.Max(text.Length, 1);

                        // Time per character
                        float charTime = newClip.length / totalChars;

                        characterIncreaseTimerField.SetValue(__instance, charTime);
                        // hideTimer = audio length + 0.5 sec reserve
                        hideTimerField.SetValue(__instance, newClip.length + 0.5f);
                    }
                }
            }
            catch (Exception e)
            {
                Logging.Error($"[TutorialDialogueManagerPatch] Error in Postfix: {e}");
            }
        }
    }
}
