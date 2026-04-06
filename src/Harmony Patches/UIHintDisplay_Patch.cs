using System;
using System.IO;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IAmYourTranslator.json;
using BepInEx;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.Harmony_Patches
{
    /// <summary>
    /// Patches UIHintDisplay to translate all hints using the "Hints" dictionary.
    /// Handles both UnityEngine.UI.Text and TextMeshProUGUI components.
    /// </summary>
    [HarmonyPatch]
    public static class UIHintDisplay_Patch
    {
        // Patch Start of UIHintDisplay to translate hints on creation
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIHintDisplay), "Start")]
        public static void StartPostfix(UIHintDisplay __instance)
        {
            try
            {
                if (__instance == null || LanguageManager.CurrentLanguage == null)
                    return;

                // Look for Text components and translate them
                var texts = __instance.GetComponentsInChildren<Text>(true);
                foreach (var t in texts)
                {
                    if (t == null) continue;
                    TranslateTextAndSaveIfMissing(t, t.text, LanguageManager.CurrentLanguage.Hints, "[UIHintDisplay]");
                }

                // Look for TMP_Text components and translate them
                var tmps = __instance.GetComponentsInChildren<TMP_Text>(true);
                foreach (var tmp in tmps)
                {
                    if (tmp == null) continue;
                    TranslateTextAndSaveIfMissing(tmp, tmp.text, LanguageManager.CurrentLanguage.Hints, "[UIHintDisplay]");
                }

                // Apply global font to all TMP_Text components
                ApplyFontToAllChildrenTMP(__instance, Plugin.GlobalTMPFont, "[UIHintDisplay]");
            }
            catch (Exception e)
            {
                Logging.Error($"[UIHintDisplay] Error in StartPostfix: {e}");
            }
        }

        // Postfix for RefreshHint so each new hint is translated when it appears
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIHintDisplay), "RefreshHint")]
        public static void RefreshHintPostfix(UIHintDisplay __instance)
        {
            try
            {
                if (__instance == null || LanguageManager.CurrentLanguage == null)
                    return;

                var display = Traverse.Create(__instance).Field("displayText").GetValue<TMP_Text>();
                if (display == null)
                    return;

                string original = display.text;
                if (string.IsNullOrEmpty(original))
                    return;

                // Translate the current hint using the common method
                TranslateTextAndSaveIfMissing(display, original, LanguageManager.CurrentLanguage.Hints, "[UIHintDisplay.RefreshHint]");

                // Apply global font to the hint display
                var tmpFont = Plugin.GlobalTMPFont;
                if (tmpFont != null)
                    TMPFontReplacer.ApplyFontToTMP(display, tmpFont);
            }
            catch (Exception e)
            {
                Logging.Error($"[UIHintDisplay] Error in RefreshHintPostfix: {e}");
            }
        }
    }
}
