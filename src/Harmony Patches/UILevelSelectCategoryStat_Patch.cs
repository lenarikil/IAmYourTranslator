using System;
using HarmonyLib;
using TMPro;
using IAmYourTranslator.json;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(UILevelSelectCategoryStat), "Initialize")]
    public static class UILevelSelectCategoryStat_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(UILevelSelectCategoryStat __instance)
        {
            try
            {
                if (__instance == null) return;

                var headerField = AccessTools.Field(typeof(UILevelSelectCategoryStat), "header");
                var header = headerField?.GetValue(__instance) as TMP_Text;
                if (header == null) return;

                System.Collections.Generic.Dictionary<string, string> dict = null;
                if (LanguageManager.IsLoaded)
                {
                    dict = LanguageManager.CurrentLanguage.categoryStatHeaders;
                    if (dict == null)
                        dict = LanguageManager.CurrentLanguage.categoryStatHeaders = new System.Collections.Generic.Dictionary<string, string>();
                }

                string original = ResolveOriginalTranslationKey(header.text ?? string.Empty, dict);
                TranslateTextAndSaveIfMissing(header, original, dict, "[UILevelSelectCategoryStat_Patch]");

                var font = TMPFontReplacer.GetCachedFont(Plugin.GlobalFontPath);
                if (font != null)
                    TMPFontReplacer.ApplyFontToTMP(header, font);
            }
            catch (Exception e)
            {
                Logging.Warn($"[UILevelSelectCategoryStat_Patch] Error: {e}");
            }
        }
    }
}
