using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using IAmYourTranslator.json;
using IAmYourTranslator;
using static IAmYourTranslator.CommonFunctions;
using BepInEx;

namespace IAmYourTranslator.Harmony_Patches
{
    /// <summary>
    /// Patches HUDTimerIncreaseListing to translate timer increase descriptions.
    /// Handles both time-based and score-based bonus translations with custom formatting.
    /// </summary>
    [HarmonyPatch]
    public static class HUDTimerIncreaseListing_Patch
    {
        /// <summary>
        /// Gets the description text component with fallback logic.
        /// </summary>
        private static TMP_Text GetDescriptionTextComponent(HUDTimerIncreaseListing __instance, TMP_Text[] texts)
        {
            if (texts == null || texts.Length == 0)
                return null;

            // Prefer the serialized field 'descriptionText' on the instance
            var descField = Traverse.Create(__instance).Field("descriptionText").GetValue<TMP_Text>();
            if (descField != null)
                return descField;

            // Fallback to first TMP_Text
            foreach (var t in texts)
            {
                if (t != null)
                    return t;
            }

            return null;
        }

        /// <summary>
        /// Migrates formatted translation keys from previous buggy patch to clean keys.
        /// </summary>
        private static void MigrateFormattedKeys(Dictionary<string, string> dict, string cleanKey)
        {
            if (dict == null || string.IsNullOrEmpty(cleanKey))
                return;

            try
            {
                var keys = new System.Collections.Generic.List<string>(dict.Keys);
                foreach (var k in keys)
                {
                    if (k == cleanKey) continue;
                    // Detect likely formatted keys: contains size tag or plus/time formatting
                    if ((k.Contains("<size=") || k.Contains(" +") || k.Contains("s</size>")) && k.Contains(cleanKey))
                    {
                        var val = dict[k];
                        if (!string.IsNullOrEmpty(val) && val != k)
                        {
                            dict[cleanKey] = val;
                            dict.Remove(k);
                            LanguageManager.SaveCurrentLanguage();
                            Logging.Info($"[HUDTimerIncreaseListing] Migrated translation from formatted key '{k}' to clean key '{cleanKey}'");
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Warn($"[HUDTimerIncreaseListing] Error while migrating formatted keys: {ex}");
            }
        }

        [HarmonyPatch(typeof(HUDTimerIncreaseListing), "InitializeTime")]
        [HarmonyPostfix]
        public static void InitializeTimePostfix(HUDTimerIncreaseListing __instance, double time, string description, int count, Texture iconTexture)
        {
            try
            {
                if (__instance == null)
                    return;
                if (!LanguageManager.IsLoaded || LanguageManager.CurrentLanguage == null)
                    return;

                var go = (__instance as UnityEngine.Component)?.gameObject;
                if (go == null)
                {
                    var traverse = Traverse.Create(__instance);
                    go = traverse.Field("gameObject").GetValue<GameObject>();
                }

                if (go == null)
                {
                    Logging.Warn("[HUDTimerIncreaseListing] Could not get gameObject from instance");
                    return;
                }

                var texts = go.GetComponentsInChildren<TMP_Text>(true);
                if (texts == null || texts.Length == 0)
                {
                    Logging.Warn("[HUDTimerIncreaseListing] No TMP_Text components found");
                    return;
                }

                var dict = LanguageManager.CurrentLanguage.timerIncrease;
                if (dict == null)
                    dict = LanguageManager.CurrentLanguage.timerIncrease = new System.Collections.Generic.Dictionary<string, string>();

                var match = GetDescriptionTextComponent(__instance, texts);
                if (match == null)
                    return;

                string originalDesc = description ?? string.Empty;
                Logging.Info($"[HUDTimerIncreaseListing] Initialize (time) called for: '{originalDesc}'");

                // Migrate formatted keys from buggy previous patch
                MigrateFormattedKeys(dict, originalDesc);

                // Lookup translation and apply custom formatting if found
                if (dict.TryGetValue(originalDesc, out var trans) && !string.IsNullOrEmpty(trans) && trans != originalDesc)
                {
                    string countText = __instance.GetType().GetMethod("GetCountText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(__instance, new object[] { count }) as string;
                    string newText = string.Concat(new string[] {
                        "<size=70%>",
                        trans,
                        " ",
                        countText,
                        "<size=100%> +",
                        time.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                        "<size=70%>s</size>"
                    });
                    match.text = newText;
                    Logging.Info($"[HUDTimerIncreaseListing] Translated (time): '{originalDesc}' -> '{trans}'");
                }
                else if (!dict.ContainsKey(originalDesc))
                {
                    dict[originalDesc] = originalDesc;
                    LanguageManager.SaveCurrentLanguage();
                    Logging.Info($"[HUDTimerIncreaseListing] Added missing key: '{originalDesc}'");
                }

                // Apply global font
                var tmpFont = Plugin.GlobalTMPFont;
                if (tmpFont != null)
                    TMPFontReplacer.ApplyFontToTMP(match, tmpFont);
            }
            catch (Exception e)
            {
                Logging.Error($"[HUDTimerIncreaseListing] Error in InitializeTimePostfix: {e}");
            }
        }

        [HarmonyPatch(typeof(HUDTimerIncreaseListing), "InitializeScore")]
        [HarmonyPostfix]
        public static void InitializeScorePostfix(HUDTimerIncreaseListing __instance, int score, string description, int count, Texture iconTexture)
        {
            try
            {
                if (__instance == null)
                    return;
                if (!LanguageManager.IsLoaded || LanguageManager.CurrentLanguage == null)
                    return;

                var go = (__instance as UnityEngine.Component)?.gameObject;
                if (go == null)
                {
                    var traverse = Traverse.Create(__instance);
                    go = traverse.Field("gameObject").GetValue<GameObject>();
                }

                if (go == null)
                {
                    Logging.Warn("[HUDTimerIncreaseListing] Could not get gameObject from instance (score)");
                    return;
                }

                var texts = go.GetComponentsInChildren<TMP_Text>(true);
                if (texts == null || texts.Length == 0)
                {
                    Logging.Warn("[HUDTimerIncreaseListing] No TMP_Text components found (score)");
                    return;
                }

                var dict = LanguageManager.CurrentLanguage.timerIncrease;
                if (dict == null)
                    dict = LanguageManager.CurrentLanguage.timerIncrease = new System.Collections.Generic.Dictionary<string, string>();

                var match = GetDescriptionTextComponent(__instance, texts);
                if (match == null)
                    return;

                string originalDesc = description ?? string.Empty;
                Logging.Info($"[HUDTimerIncreaseListing] Initialize (score) called for: '{originalDesc}'");

                // Migrate formatted keys from buggy previous patch
                MigrateFormattedKeys(dict, originalDesc);

                // Lookup translation and apply custom formatting if found
                if (dict.TryGetValue(originalDesc, out var trans) && !string.IsNullOrEmpty(trans) && trans != originalDesc)
                {
                    string countText = __instance.GetType().GetMethod("GetCountText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(__instance, new object[] { count }) as string;
                    string newText = string.Concat(new string[] {
                        "<size=70%>",
                        trans,
                        " ",
                        countText,
                        "<size=100%> +",
                        score.ToString(),
                        "<size=70%></size>"
                    });
                    match.text = newText;
                    Logging.Info($"[HUDTimerIncreaseListing] Translated (score): '{originalDesc}' -> '{trans}'");
                }
                else if (!dict.ContainsKey(originalDesc))
                {
                    dict[originalDesc] = originalDesc;
                    LanguageManager.SaveCurrentLanguage();
                    Logging.Info($"[HUDTimerIncreaseListing] Added missing key: '{originalDesc}'");
                }

                // Apply global font
                var tmpFont = Plugin.GlobalTMPFont;
                if (tmpFont != null)
                    TMPFontReplacer.ApplyFontToTMP(match, tmpFont);
            }
            catch (Exception e)
            {
                Logging.Error($"[HUDTimerIncreaseListing] Error in InitializeScorePostfix: {e}");
            }
        }
    }
}
