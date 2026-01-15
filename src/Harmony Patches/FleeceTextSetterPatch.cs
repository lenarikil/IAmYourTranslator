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

        // EveryCall mode — translate every time
        private static void TranslateEveryCall(FleeceTextSetter instance)
        {
            if (instance == null) return;

            TMP_Text[] texts = GetTextsArray(instance);
            if (texts == null || texts.Length == 0) return;

            // Use hardCoded translations from LanguageManager
            if (!LanguageManager.IsLoaded)
            {
                return; // no language loaded
            }

            var hardCodedDict = LanguageManager.CurrentLanguage.hardCoded;
            if (hardCodedDict == null)
            {
                hardCodedDict = LanguageManager.CurrentLanguage.hardCoded = new Dictionary<string, string>();
            }

            foreach (TMP_Text tmp in texts)
            {
                if (tmp == null) continue;

                string originalText = tmp.text;
                if (string.IsNullOrEmpty(originalText)) continue;

                // Try to get translation, if not found use original
                string newText = hardCodedDict.TryGetValue(originalText, out string translated) ? translated : originalText;

                if (newText != originalText)
                {
                    tmp.text = newText;
                    var tmpFont = TMPFontReplacer.GetCachedFont(Plugin.GlobalFontPath);
                    if (tmpFont != null)
                    {
                        CommonFunctions.TMPFontReplacer.ApplyFontToTMP(tmp, tmpFont);
                    }
                    else
                    {
                        Logging.Warn("[Translator EveryCall] Failed to load custom font!");
                    }
                    Logging.Info($"[Translator EveryCall] '{instance.name}': '{originalText}' → '{newText}'");
                }
                else
                {
                    // Add missing key if not exists
                    if (!hardCodedDict.ContainsKey(originalText))
                    {
                        hardCodedDict[originalText] = originalText;
                        LanguageManager.SaveCurrentLanguage();
                        Logging.Info($"[Translator EveryCall] Added missing hardCoded key: '{originalText}'");
                    }
                }
            }
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
