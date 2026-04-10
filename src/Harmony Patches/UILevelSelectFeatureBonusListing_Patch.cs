using System;
using System.Globalization;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
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

                Dictionary<string, string> dict = null;
                if (LanguageManager.IsLoaded)
                {
                    dict = LanguageManager.CurrentLanguage.bonusObjectives;
                    if (dict == null)
                        dict = LanguageManager.CurrentLanguage.bonusObjectives = new Dictionary<string, string>();
                }

                string sourceText = ResolveOriginalTranslationKey(baseText, dict);
                
                // Skip texts containing [WEAPON] placeholder - they will be handled later with actual weapon name
                if (sourceText.Contains("[WEAPON]"))
                {
                    return;
                }
                
                string translated = sourceText;

                if (dict != null)
                {
                    if (dict.TryGetValue(sourceText, out var val) && !string.IsNullOrEmpty(val) && val != sourceText)
                    {
                        translated = val;
                    }
                    else if (!dict.ContainsKey(sourceText))
                    {
                        dict[sourceText] = sourceText; // add placeholder
                        LanguageManager.SaveCurrentLanguage();
                        Logging.Info($"[UILevelSelectFeatureBonusListing_Patch] Added missing translation key: '{sourceText}'");
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
