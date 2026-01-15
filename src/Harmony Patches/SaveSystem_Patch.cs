using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using BepInEx;
using IAmYourTranslator.json;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(SaveSystem))]
    public static class SaveSystem_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch("DisplaySavingPopUp")]
        public static void DisplaySavingPopUpPostfix(SaveSystem __instance)
        {
            try
            {
                var subText = Traverse.Create(__instance).Field("subText").GetValue<TMP_Text>();
                var mainText = Traverse.Create(__instance).Field("mainText").GetValue<TMP_Text>();

                // If the language is not loaded — just log and exit
                if (!LanguageManager.IsLoaded)
                {
                    if (mainText != null)
                        Logging.Info($"[SaveSystem] DisplaySavingPopUp -> mainText.text = '{mainText.text}' (no language loaded)");
                    if (subText != null)
                        Logging.Info($"[SaveSystem] DisplaySavingPopUp -> subText.text = '{subText.text}' (no language loaded)");
                    return;
                }

                // Ensure that the saveSystem dictionary is initialized
                var dict = LanguageManager.CurrentLanguage.saveSystem;
                if (dict == null)
                    dict = LanguageManager.CurrentLanguage.saveSystem = new System.Collections.Generic.Dictionary<string, string>();

                // Process mainText
                if (mainText != null)
                {
                    string original = mainText.text ?? string.Empty;
                    if (dict.TryGetValue(original, out var val) && !string.IsNullOrEmpty(val) && val != original)
                    {
                        mainText.text = val;
                        Logging.Info($"[SaveSystem] Applied translation to mainText: '{original}' -> '{val}'");
                    }
                    else if (!dict.ContainsKey(original))
                    {
                        dict[original] = original;
                        LanguageManager.SaveCurrentLanguage();
                        Logging.Info($"[SaveSystem] Added missing saveSystem key for mainText: '{original}'");
                    }

                    // Apply global font if available
                    var tmpFont = Plugin.GlobalTMPFont;
                    if (tmpFont != null)
                        TMPFontReplacer.ApplyFontToTMP(mainText, tmpFont);
                }

                // Process subText
                if (subText != null)
                {
                    string original = subText.text ?? string.Empty;
                    if (dict.TryGetValue(original, out var val) && !string.IsNullOrEmpty(val) && val != original)
                    {
                        subText.text = val;
                        Logging.Info($"[SaveSystem] Applied translation to subText: '{original}' -> '{val}'");
                    }
                    else if (!dict.ContainsKey(original))
                    {
                        dict[original] = original;
                        LanguageManager.SaveCurrentLanguage();
                        Logging.Info($"[SaveSystem] Added missing saveSystem key for subText: '{original}'");
                    }

                    var tmpFont = Plugin.GlobalTMPFont;
                    if (tmpFont != null)
                        TMPFontReplacer.ApplyFontToTMP(subText, tmpFont);
                }
            }
            catch (Exception e)
            {
                Logging.Error($"[SaveSystem] Error in DisplaySavingPopUpPostfix: {e}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("DisplaySaveWarning")]
        public static void DisplaySaveWarningPostfix(SaveSystem __instance)
        {
            try
            {
                var subText = Traverse.Create(__instance).Field("subText").GetValue<TMP_Text>();
                if (subText != null)
                    Logging.Info($"[SaveSystem] DisplaySaveWarning -> subText.text = '{subText.text}'");
                else
                    Logging.Warn("[SaveSystem] DisplaySaveWarning -> subText field is null");
            }
            catch (Exception e)
            {
                Logging.Error($"[SaveSystem] Error in DisplaySaveWarningPostfix: {e}");
            }
        }
    }
}
