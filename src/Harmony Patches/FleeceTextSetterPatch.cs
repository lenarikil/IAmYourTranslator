using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using BepInEx;
using static IAmYourTranslator.CommonFunctions;
using IAmYourTranslator.json;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(FleeceTextSetter))]
    public class FleeceTextSetterPatch
    {
        private static readonly Dictionary<TMP_Text, string> OriginalTextByComponent = new Dictionary<TMP_Text, string>();

        [HarmonyPostfix]
        [HarmonyPatch("SetText")]
        public static void SetTextPostfix(FleeceTextSetter __instance)
        {
            SafeTranslate(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch("ForceText")]
        public static void ForceTextPostfix(FleeceTextSetter __instance)
        {
            SafeTranslate(__instance);
        }

        private static void SafeTranslate(FleeceTextSetter instance)
        {
            try
            {
                TranslateEveryCall(instance);
            }
            catch (Exception e)
            {
                Logging.Error($"Error in SetTextPostfix: {e}");
            }
        }

        public static int RefreshAll(bool skipTranslatorMenu = true)
        {
            int refreshed = 0;
            try
            {
                var allSetters = CommonFunctions.FindObjectsOfTypeCached<FleeceTextSetter>(true);
                if (allSetters == null || allSetters.Length == 0)
                    return 0;

                foreach (var setter in allSetters)
                {
                    if (setter == null)
                        continue;

                    if (skipTranslatorMenu && setter.GetComponentInParent<TranslatorSettingsMenu>(true) != null)
                        continue;

                    SafeTranslate(setter);
                    refreshed++;
                }
            }
            catch (Exception e)
            {
                Logging.Warn($"[FleeceTextSetterPatch] RefreshAll failed: {e.Message}");
            }

            return refreshed;
        }

        private static void CleanupOriginalTextCache()
        {
            var dead = new List<TMP_Text>();
            foreach (var kv in OriginalTextByComponent)
            {
                if (kv.Key == null)
                    dead.Add(kv.Key);
            }

            foreach (var key in dead)
                OriginalTextByComponent.Remove(key);
        }

        private static string ResolveOriginalText(TMP_Text tmp)
        {
            if (tmp == null)
                return null;

            if (OriginalTextByComponent.TryGetValue(tmp, out var original) && !string.IsNullOrEmpty(original))
                return original;

            var current = tmp.text;
            if (string.IsNullOrEmpty(current))
                return null;

            OriginalTextByComponent[tmp] = current;
            return current;
        }

        // EveryCall mode — translate every time
        private static void TranslateEveryCall(FleeceTextSetter instance)
        {
            if (instance == null) return;

            TMP_Text[] texts = GetTextsArray(instance);
            if (texts == null || texts.Length == 0) return;

            CleanupOriginalTextCache();

            // Original mode: restore previously captured source text.
            // Use hardCoded translations from LanguageManager
            if (!LanguageManager.IsLoaded)
            {
                foreach (TMP_Text tmp in texts)
                {
                    if (tmp == null)
                        continue;

                    if (!OriginalTextByComponent.TryGetValue(tmp, out var originalText) || string.IsNullOrEmpty(originalText))
                        continue;

                    if (tmp.text != originalText)
                        tmp.text = originalText;
                    
                    // No font replacement in original mode
                }
                return; // no language loaded
            }

            var hardCodedDict = LanguageManager.CurrentLanguage.hardCoded;
            if (hardCodedDict == null)
            {
                hardCodedDict = LanguageManager.CurrentLanguage.hardCoded = new Dictionary<string, string>();
            }

            bool addedMissingKeys = false;
            foreach (TMP_Text tmp in texts)
            {
                if (tmp == null) continue;

                string originalText = ResolveOriginalText(tmp);
                if (string.IsNullOrEmpty(originalText)) continue;

                string newText = originalText;
                if (hardCodedDict.TryGetValue(originalText, out string translated) && !string.IsNullOrEmpty(translated))
                {
                    newText = translated;
                }
                else if (!hardCodedDict.ContainsKey(originalText))
                {
                    hardCodedDict[originalText] = originalText;
                    addedMissingKeys = true;
                }

                if (newText != originalText)
                {
                    bool changedText = !string.Equals(tmp.text, newText, StringComparison.Ordinal);
                    if (changedText)
                        tmp.text = newText;

                    var tmpFont = TMPFontReplacer.GetCachedFont(Plugin.GlobalFontPath);
                    if (tmpFont != null)
                    {
                        if (!Equals(tmp.font, tmpFont))
                            CommonFunctions.TMPFontReplacer.ApplyFontToTMP(tmp, tmpFont);
                    }
                    else
                    {
                        Logging.Warn("[Translator EveryCall] Failed to load custom font!");
                    }

                    if (changedText)
                        Logging.Info($"[Translator EveryCall] '{instance.name}': '{originalText}' → '{newText}'");
                }
                else
                {
                    if (tmp.text != originalText)
                        tmp.text = originalText;
                }
            }

            if (addedMissingKeys)
                LanguageManager.SaveCurrentLanguage();
        }

        private static TMP_Text[] GetTextsArray(FleeceTextSetter instance)
        {
            var field = AccessTools.Field(typeof(FleeceTextSetter), "texts");
            if (field == null)
            {
                Logging.Error("Could not find field 'texts' in FleeceTextSetter!");
                return null;
            }

            return field.GetValue(instance) as TMP_Text[];
        }
    }
}
