using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
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

                string original = tmp.text ?? string.Empty;
                string translated = original;

                if (LanguageManager.IsLoaded)
                {
                    var dict = LanguageManager.CurrentLanguage.categorySlideTexts;
                    if (dict == null)
                        dict = LanguageManager.CurrentLanguage.categorySlideTexts = new Dictionary<string, string>();

                    if (dict.TryGetValue(original, out var val) && !string.IsNullOrEmpty(val) && val != original)
                    {
                        translated = val;
                    }
                    else if (!dict.ContainsKey(original))
                    {
                        dict[original] = original;
                        LanguageManager.SaveCurrentLanguage();
                        Logging.Info($"[UILevelSelectionRoot_Patch] Added missing categorySlideTexts key: '{original}'");
                    }
                }

                tmp.text = translated;

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
