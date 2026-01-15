using HarmonyLib;
using TMPro;
using UnityEngine;
using System.IO;
using IAmYourTranslator.json;
using BepInEx;
using System.Linq;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.HarmonyPatches
{
    [HarmonyPatch(typeof(LevelInformation), "GetDisplayName")]
    public static class LevelNameAndFontPatch
    {
        // Use globally cached font from Plugin, fallback — load from file
        private static TMP_FontAsset GetFont()
        {
            // Centralized cached getter (uses Plugin.GlobalTMPFont / Plugin.GlobalFontPath / internal cache)
            return TMPFontReplacer.GetCachedFont(Plugin.GlobalFontPath);
        }

        // Patch after getting level name
        [HarmonyPostfix]
        public static void LevelNamePostfix(LevelInformation __instance, ref string __result)
        {
            try
            {
                if (!LanguageManager.IsLoaded || string.IsNullOrEmpty(__result) || __result == "???")
                    return;

                string key = __result.Trim();
                var dict = LanguageManager.CurrentLanguage.levelNames;

                // If no translation exists — add key to JSON
                if (!dict.ContainsKey(key))
                {
                    dict[key] = key;
                    LanguageManager.SaveCurrentLanguage();
                    Logging.Info($"[LevelNamePatch] Added new level translation key: \"{key}\"");
                    return;
                }

                string translated = dict[key];
                if (!string.IsNullOrEmpty(translated) && translated != key)
                    __result = translated;

                // Apply font to text where Cyrillic is present
                var tmpFont = GetFont();
                if (tmpFont != null)
                    TMPFontReplacer.ApplyFontToAllTMP(tmpFont);
            }
            catch (System.Exception e)
            {
                Logging.Warn($"[LevelNamePatch] Error processing level name: {e.Message}");
            }
        }
    }
}
