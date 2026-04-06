using HarmonyLib;
using TMPro;
using UnityEngine;
using IAmYourTranslator.json;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.HarmonyPatches
{
    [HarmonyPatch(typeof(LevelInformation), "GetDisplayName")]
    public static class LevelNameAndFontPatch
    {
        private const float SaveDebounceSeconds = 1.0f;
        private static bool pendingSave;
        private static float nextSaveTime;
        private static string lastSceneName;
        private static int lastAppliedFontInstanceId = -1;
        private static bool fontAppliedForScene;

        // Use globally cached font from Plugin, fallback - load from file
        private static TMP_FontAsset GetFont()
        {
            // Centralized cached getter (uses Plugin.GlobalTMPFont / Plugin.GlobalFontPath / internal cache)
            return TMPFontReplacer.GetCachedFont(Plugin.GlobalFontPath);
        }

        private static bool ContainsCyrillic(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if ((c >= '\u0400' && c <= '\u052F') || c == '\u2DE0' || c == '\u2DE1')
                    return true;
            }

            return false;
        }

        private static void ScheduleSave()
        {
            pendingSave = true;
            nextSaveTime = Time.realtimeSinceStartup + SaveDebounceSeconds;
        }

        private static void TryFlushPendingSave()
        {
            if (!pendingSave)
                return;

            if (Time.realtimeSinceStartup < nextSaveTime)
                return;

            pendingSave = false;
            LanguageManager.SaveCurrentLanguage();
        }

        private static void MaybeApplyFontToScene(TMP_FontAsset font)
        {
            if (font == null)
                return;

            string sceneName = GetCurrentSceneName();
            if (sceneName != lastSceneName)
            {
                lastSceneName = sceneName;
                fontAppliedForScene = false;
            }

            int fontId = font.GetInstanceID();
            if (fontAppliedForScene && lastAppliedFontInstanceId == fontId)
                return;

            lastAppliedFontInstanceId = fontId;
            fontAppliedForScene = true;
            TMPFontReplacer.ApplyFontToAllTMP(font);
        }

        // Patch after getting level name
        [HarmonyPostfix]
        public static void LevelNamePostfix(LevelInformation __instance, ref string __result)
        {
            try
            {
                if (!LanguageManager.IsLoaded || string.IsNullOrEmpty(__result) || __result == "???")
                    return;

                TryFlushPendingSave();

                string key = __result.Trim();
                if (string.IsNullOrEmpty(key))
                    return;

                var dict = LanguageManager.CurrentLanguage.levelNames;
                if (dict == null)
                    dict = LanguageManager.CurrentLanguage.levelNames = new System.Collections.Generic.Dictionary<string, string>();

                // If no translation exists - add key to JSON
                if (!dict.TryGetValue(key, out string translated))
                {
                    dict[key] = key;
                    ScheduleSave();
                    Logging.Info($"[LevelNamePatch] Added new level translation key: \"{key}\"");
                    return;
                }

                if (!string.IsNullOrEmpty(translated) && translated != key)
                    __result = translated;

                // Apply font once per scene only when Cyrillic is present
                if (ContainsCyrillic(__result))
                {
                    var tmpFont = GetFont();
                    MaybeApplyFontToScene(tmpFont);
                }
            }
            catch (System.Exception e)
            {
                Logging.Warn($"[LevelNamePatch] Error processing level name: {e.Message}");
            }
        }
    }
}
