using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using IAmYourTranslator.json;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(UILevelSelectionRoot), "SelectCategory")]
    public static class UILevelSelectionRoot_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(UILevelSelectionRoot __instance, LevelCollection levelCollection)
        {
            try
            {
                if (__instance == null) return;

                var field = AccessTools.Field(typeof(UILevelSelectionRoot), "categoryNameText");
                var tmp = field?.GetValue(__instance) as TMP_Text;
                if (tmp == null) return;

                Dictionary<string, string> dict = null;
                if (LanguageManager.IsLoaded)
                {
                    dict = LanguageManager.CurrentLanguage.categorySlideTexts;
                    if (dict == null)
                        dict = LanguageManager.CurrentLanguage.categorySlideTexts = new Dictionary<string, string>();
                }

                string original = ResolveOriginalTranslationKey(tmp.text ?? string.Empty, dict);
                TranslateTextAndSaveIfMissing(tmp, original, dict, "[UILevelSelectionRoot_Patch]");

                var font = TMPFontReplacer.GetCachedFont(Plugin.GlobalFontPath);
                if (font != null)
                    TMPFontReplacer.ApplyFontToTMP(tmp, font);
            }
            catch (Exception e)
            {
                Logging.Warn($"[UILevelSelectionRoot_Patch] Error: {e}");
            }
        }
    }
}
