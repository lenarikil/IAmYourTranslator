using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using IAmYourTranslator.json;
using static IAmYourTranslator.CommonFunctions;
using BepInEx;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(HUDTimerIncrease))]
    public static class HUDTimerIncrease_Patch
    {
        // ============================================================
        // CreateTimeListing - processing time bonus description
        // ============================================================
        [HarmonyPostfix]
        [HarmonyPatch("CreateTimeListing")]
        public static void CreateTimeListingPostfix(string description)
        {
            try
            {
                if (string.IsNullOrEmpty(description))
                {
                    Logging.Warn("[HUDTimerIncrease] CreateTimeListing called with empty description");
                    return;
                }

                Logging.Info($"[HUDTimerIncrease] CreateTimeListing called with description: '{description}'");

                // If language is not loaded, just log and return
                if (!LanguageManager.IsLoaded)
                {
                    Logging.Warn("[HUDTimerIncrease] Language not loaded, skipping translation");
                    return;
                }

                // Initialize timerIncrease dict if needed
                var dict = LanguageManager.CurrentLanguage.timerIncrease;
                if (dict == null)
                    dict = LanguageManager.CurrentLanguage.timerIncrease = new System.Collections.Generic.Dictionary<string, string>();

                // Check if translation exists
                if (!dict.TryGetValue(description, out var translation))
                {
                    // Add missing key
                    dict[description] = description;
                    LanguageManager.SaveCurrentLanguage();
                    Logging.Info($"[HUDTimerIncrease] Added missing key: '{description}'");
                }
                else if (!string.IsNullOrEmpty(translation) && translation != description)
                {
                    Logging.Info($"[HUDTimerIncrease] Found translation for: '{description}' -> '{translation}'");
                }
            }
            catch (Exception e)
            {
                Logging.Error($"[HUDTimerIncrease] Error in CreateTimeListing postfix: {e}");
            }
        }

        // ============================================================
        // CreateScoreListing - processing score bonus description
        // ============================================================
        [HarmonyPostfix]
        [HarmonyPatch("CreateScoreListing")]
        public static void CreateScoreListingPostfix(string description)
        {
            try
            {
                if (string.IsNullOrEmpty(description))
                {
                    Logging.Warn("[HUDTimerIncrease] CreateScoreListing called with empty description");
                    return;
                }

                Logging.Info($"[HUDTimerIncrease] CreateScoreListing called with description: '{description}'");

                // If language is not loaded, just log and return
                if (!LanguageManager.IsLoaded)
                {
                    Logging.Warn("[HUDTimerIncrease] Language not loaded, skipping translation");
                    return;
                }

                // Initialize timerIncrease dict if needed
                var dict = LanguageManager.CurrentLanguage.timerIncrease;
                if (dict == null)
                    dict = LanguageManager.CurrentLanguage.timerIncrease = new System.Collections.Generic.Dictionary<string, string>();

                // Check if translation exists
                if (!dict.TryGetValue(description, out var translation))
                {
                    // Add missing key
                    dict[description] = description;
                    LanguageManager.SaveCurrentLanguage();
                    Logging.Info($"[HUDTimerIncrease] Added missing key: '{description}'");
                }
                else if (!string.IsNullOrEmpty(translation) && translation != description)
                {
                    Logging.Info($"[HUDTimerIncrease] Found translation for: '{description}' -> '{translation}'");
                }
            }
            catch (Exception e)
            {
                Logging.Error($"[HUDTimerIncrease] Error in CreateScoreListing postfix: {e}");
            }
        }
    }
}
