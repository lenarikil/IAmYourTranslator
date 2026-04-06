using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using IAmYourTranslator.json;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(UILevelOverviewStartEndTag), "Initialize")]
    public static class UILevelOverviewStartEndTag_Patch
    {
        // track added keys to avoid saving repeatedly
        private static readonly System.Collections.Generic.HashSet<string> _addedStartEndKeys = new System.Collections.Generic.HashSet<string>();

        [HarmonyPostfix]
        public static void InitializePostfix(UILevelOverviewStartEndTag __instance, bool end, bool flip)
        {
            try
            {
                if (__instance == null) return;
                if (!LanguageManager.IsLoaded || LanguageManager.CurrentLanguage == null) return;

                var displayText = Traverse.Create(__instance).Field("displayText").GetValue<TMP_Text>();
                if (displayText == null || string.IsNullOrEmpty(displayText.text)) return;

                var dict = LanguageManager.CurrentLanguage.overviewScreen;
                if (dict == null) dict = LanguageManager.CurrentLanguage.overviewScreen = new System.Collections.Generic.Dictionary<string, string>();

                string originalText = displayText.text;

                if (dict.TryGetValue(originalText, out var trans) && !string.IsNullOrEmpty(trans) && trans != originalText)
                {
                    displayText.text = trans;
                    Logging.Info($"[UILevelOverviewStartEndTag] Applied translation: '{originalText}' -> '{trans}'");
                }
                else if (!dict.ContainsKey(originalText))
                {
                    dict[originalText] = originalText;
                    lock (_addedStartEndKeys)
                    {
                        if (!_addedStartEndKeys.Contains(originalText))
                        {
                            _addedStartEndKeys.Add(originalText);
                            LanguageManager.SaveCurrentLanguage();
                            Logging.Info($"[UILevelOverviewStartEndTag] Added missing key: '{originalText}' (is_end: {end})");
                        }
                    }
                }

                // Apply global TMP font if available
                var tmpFont = Plugin.GlobalTMPFont;
                if (tmpFont != null)
                    CommonFunctions.TMPFontReplacer.ApplyFontToTMP(displayText, tmpFont);
            }
            catch (Exception e)
            {
                Logging.Warn($"[UILevelOverviewStartEndTag] InitializePostfix error: {e}");
            }
        }
    }
}
