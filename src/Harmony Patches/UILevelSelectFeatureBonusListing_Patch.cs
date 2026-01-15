using System;
using System.Globalization;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IAmYourTranslator.json;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(UILevelSelectFeatureBonusListing), "Initialize")]
    public static class UILevelSelectFeatureBonusListing_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(UILevelSelectFeatureBonusListing __instance, UILevelSelectFeatureBonusListing.State state, string objectiveDescription, float time)
        {
            try
            {
                if (__instance == null) return;

                var textField = AccessTools.Field(typeof(UILevelSelectFeatureBonusListing), "text");
                var tmp = textField?.GetValue(__instance) as TMP_Text;
                if (tmp == null) return;

                // Get the current text (after the original initialization)
                string current = tmp.text ?? string.Empty;

                // If the text contains added time in format " <size=85%>(...)", separate it
                string baseText = current;
                const string timeTag = " <size=85%>(";
                int idx = current.IndexOf(timeTag, StringComparison.Ordinal);
                if (idx >= 0)
                    baseText = current.Substring(0, idx);

                string translated = baseText;
                if (LanguageManager.IsLoaded)
                {
                    var dict = LanguageManager.CurrentLanguage.bonusObjectives;
                    if (dict == null)
                        dict = LanguageManager.CurrentLanguage.bonusObjectives = new System.Collections.Generic.Dictionary<string, string>();

                    if (dict.TryGetValue(baseText, out var val) && !string.IsNullOrEmpty(val) && val != baseText)
                    {
                        translated = val;
                    }
                    else if (!dict.ContainsKey(baseText))
                    {
                        dict[baseText] = baseText; // add placeholder
                        LanguageManager.SaveCurrentLanguage();
                        Logging.Info($"[UILevelSelectFeatureBonusListing_Patch] Added missing translation key: '{baseText}'");
                    }
                }

                // Recreate the final text: translation + time (if any)
                string finalText = translated;
                if (state == UILevelSelectFeatureBonusListing.State.Full && time < 999f)
                {
                    finalText = finalText + " <size=85%>(" + time.ToString("F2", CultureInfo.InvariantCulture) + ")";
                }

                tmp.text = finalText;

                // Apply centralized TMP font if it exists
                var font = TMPFontReplacer.GetCachedFont(Plugin.GlobalFontPath);
                if (font != null)
                {
                    TMPFontReplacer.ApplyFontToTMP(tmp, font);
                }
            }
            catch (Exception e)
            {
                Logging.Warn($"[UILevelSelectFeatureBonusListing_Patch] Error: {e}");
            }
        }
    }
}
