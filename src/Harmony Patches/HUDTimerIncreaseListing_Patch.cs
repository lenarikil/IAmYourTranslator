using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using IAmYourTranslator.json;
using IAmYourTranslator;
using static IAmYourTranslator.CommonFunctions;
using BepInEx;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch]
    public static class HUDTimerIncreaseListing_Patch
    {
        [HarmonyPatch(typeof(HUDTimerIncreaseListing), "InitializeTime")]
        [HarmonyPostfix]
        public static void InitializeTimePostfix(HUDTimerIncreaseListing __instance, double time, string description, int count, Texture iconTexture)
        {
            try
            {
                if (__instance == null) return;

                var go = ( __instance as UnityEngine.Component )?.gameObject;
                if (go == null)
                {
                    // try using reflection to get gameobject via Traverse
                    var traverse = HarmonyLib.Traverse.Create(__instance);
                    var gameObject = traverse.Field("gameObject").GetValue<GameObject>();
                    go = gameObject;
                }

                if (go == null)
                {
                    Logging.Warn("[HUDTimerIncreaseListing] Could not get gameObject from instance");
                    return;
                }

                // Find TMP_Text components and locate the one matching the description
                var texts = go.GetComponentsInChildren<TMP_Text>(true);
                if (texts == null || texts.Length == 0)
                {
                    Logging.Warn("[HUDTimerIncreaseListing] No TMP_Text components found");
                    return;
                }

                // If no language loaded, just register the first found text and return
                var dict = LanguageManager.CurrentLanguage.timerIncrease;
                if (dict == null)
                    dict = LanguageManager.CurrentLanguage.timerIncrease = new System.Collections.Generic.Dictionary<string, string>();
                // Prefer the serialized field 'descriptionText' on the instance
                var descField = HarmonyLib.Traverse.Create(__instance).Field("descriptionText").GetValue<TMP_Text>();
                TMP_Text match = descField;
                if (match == null)
                {
                    // fallback to first TMP_Text
                    foreach (var t in texts)
                    {
                        if (t != null)
                        {
                            match = t;
                            break;
                        }
                    }
                }

                if (match == null) return;

                string originalDesc = description ?? string.Empty;

                // Debug/log original description
                Logging.Info($"[HUDTimerIncreaseListing] Initialize called for description: '{originalDesc}'");

                // If we have bad keys in the JSON that include formatting (from previous buggy patch),
                // try to transfer their translation to the clean 'originalDesc' key.
                try
                {
                    var keys = new System.Collections.Generic.List<string>(dict.Keys);
                    foreach (var k in keys)
                    {
                        if (k == originalDesc) continue;
                        // detect likely formatted keys: contains size tag or plus/time formatting
                        if ((k.Contains("<size=") || k.Contains(" +") || k.Contains("s</size>")) && k.Contains(originalDesc))
                        {
                            var val = dict[k];
                            if (!string.IsNullOrEmpty(val) && val != k)
                            {
                                dict[originalDesc] = val;
                                dict.Remove(k);
                                LanguageManager.SaveCurrentLanguage();
                                Logging.Info($"[HUDTimerIncreaseListing] Migrated translation from formatted key '{k}' to clean key '{originalDesc}'");
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logging.Warn($"[HUDTimerIncreaseListing] Error while migrating formatted keys: {ex}");
                }

                // Lookup translation using the original (raw) description string
                if (dict.TryGetValue(originalDesc, out var trans) && !string.IsNullOrEmpty(trans) && trans != originalDesc)
                {
                    // Rebuild the full text same as original InitializeTime/InitializeScore formatting
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
                    Logging.Info($"[HUDTimerIncreaseListing] Applied translation for description: '{originalDesc}' -> '{trans}'");
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
                if (__instance == null) return;

                var go = (__instance as UnityEngine.Component)?.gameObject;
                if (go == null)
                {
                    var traverse = HarmonyLib.Traverse.Create(__instance);
                    var gameObject = traverse.Field("gameObject").GetValue<GameObject>();
                    go = gameObject;
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

                var descField = HarmonyLib.Traverse.Create(__instance).Field("descriptionText").GetValue<TMP_Text>();
                TMP_Text match = descField ?? texts[0];
                if (match == null) return;

                string originalDesc = description ?? string.Empty;

                // Debug/log original description
                Logging.Info($"[HUDTimerIncreaseListing] InitializeScore called for description: '{originalDesc}'");

                // Attempt to migrate formatted keys (from buggy previous patch) to clean key
                try
                {
                    var keys = new System.Collections.Generic.List<string>(dict.Keys);
                    foreach (var k in keys)
                    {
                        if (k == originalDesc) continue;
                        if ((k.Contains("<size=") || k.Contains(" +") || k.Contains("s</size>")) && k.Contains(originalDesc))
                        {
                            var val = dict[k];
                            if (!string.IsNullOrEmpty(val) && val != k)
                            {
                                dict[originalDesc] = val;
                                dict.Remove(k);
                                LanguageManager.SaveCurrentLanguage();
                                Logging.Info($"[HUDTimerIncreaseListing] Migrated translation from formatted key '{k}' to clean key '{originalDesc}' (score)");
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logging.Warn($"[HUDTimerIncreaseListing] Error while migrating formatted keys (score): {ex}");
                }

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
                    Logging.Info($"[HUDTimerIncreaseListing] Applied translation for description (score): '{originalDesc}' -> '{trans}'");
                }
                else if (!dict.ContainsKey(originalDesc))
                {
                    dict[originalDesc] = originalDesc;
                    LanguageManager.SaveCurrentLanguage();
                    Logging.Info($"[HUDTimerIncreaseListing] Added missing key (score): '{originalDesc}'");
                }

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
