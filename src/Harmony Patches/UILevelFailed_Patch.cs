using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using IAmYourTranslator.json;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(UILevelFailed), "Initialize")]
    public static class UILevelFailed_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(UILevelFailed __instance, UILevelFailed.FailCondition condition, Vector3 deadCameraVelocity)
        {
            try
            {
                if (__instance == null) return;

                var field = AccessTools.Field(typeof(UILevelFailed), "headerText");
                var header = field?.GetValue(__instance) as TMP_Text;
                if (header == null) return;

                string original = header.text ?? string.Empty;
                string translated = original;

                if (LanguageManager.IsLoaded)
                {
                    var dict = LanguageManager.CurrentLanguage.levelFailedHeaders;
                    if (dict == null)
                        dict = LanguageManager.CurrentLanguage.levelFailedHeaders = new System.Collections.Generic.Dictionary<string, string>();

                    if (dict.TryGetValue(original, out var val) && !string.IsNullOrEmpty(val) && val != original)
                    {
                        translated = val;
                    }
                    else if (!dict.ContainsKey(original))
                    {
                        dict[original] = original;
                        LanguageManager.SaveCurrentLanguage();
                        Logging.Info($"[UILevelFailed_Patch] Added missing header translation key: '{original}'");
                    }
                }

                header.text = translated;

                var font = TMPFontReplacer.GetCachedFont(Plugin.GlobalFontPath);
                if (font != null)
                    TMPFontReplacer.ApplyFontToTMP(header, font);
            }
            catch (Exception e)
            {
                Logging.Warn($"[UILevelFailed_Patch] Error: {e}");
            }
        }
    }
}
