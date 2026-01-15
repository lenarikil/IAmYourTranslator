using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using IAmYourTranslator.json;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch]
    public static class UILevelCompleteOverviewIncreaseListing_Patch
    {
        // Postfix for Initialize(LevelCombatTimerIncreaseInformation) — translates the description and adds a key to JSON
        [HarmonyPatch(typeof(UILevelCompleteOverviewIncreaseListing), "Initialize")]
        [HarmonyPostfix]
        public static void InitializePostfix(object __instance, object increase)
        {
            try
            {
                if (__instance == null || increase == null) return;

                var go = (__instance as UnityEngine.Component)?.gameObject;
                if (go == null)
                {
                    var traverse = HarmonyLib.Traverse.Create(__instance);
                    var gameObject = traverse.Field("gameObject").GetValue<GameObject>();
                    go = gameObject;
                }
                if (go == null)
                {
                    Logging.Warn("[UILevelCompleteOverviewIncreaseListing] Could not get gameObject from instance");
                    return;
                }

                var texts = go.GetComponentsInChildren<TMP_Text>(true);
                if (texts == null || texts.Length == 0)
                {
                    Logging.Warn("[UILevelCompleteOverviewIncreaseListing] No TMP_Text components found");
                    return;
                }

                var dict = LanguageManager.CurrentLanguage.timerIncrease;
                if (dict == null) dict = LanguageManager.CurrentLanguage.timerIncrease = new System.Collections.Generic.Dictionary<string, string>();

                // Try to obtain a dedicated descriptionText field on the listing, fallback to first TMP_Text
                var descField = HarmonyLib.Traverse.Create(__instance).Field("descriptionText").GetValue<TMP_Text>();
                TMP_Text match = descField ?? texts[0];
                if (match == null) return;

                // Call GetDescription on the increase object (try overloads)
                string originalDesc = string.Empty;
                try
                {
                    var t = increase.GetType();
                    var mBool = t.GetMethod("GetDescription", new Type[] { typeof(bool) });
                    if (mBool != null)
                    {
                        originalDesc = mBool.Invoke(increase, new object[] { false }) as string ?? string.Empty;
                    }
                    else
                    {
                        var mNo = t.GetMethod("GetDescription", Type.EmptyTypes);
                        if (mNo != null)
                            originalDesc = mNo.Invoke(increase, null) as string ?? string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    Logging.Warn($"[UILevelCompleteOverviewIncreaseListing] Error calling GetDescription: {ex}");
                }

                Logging.Info($"[UILevelCompleteOverviewIncreaseListing] Initialize called for description: '{originalDesc}'");

                if (!string.IsNullOrEmpty(originalDesc))
                {
                    if (dict.TryGetValue(originalDesc, out var trans) && !string.IsNullOrEmpty(trans) && trans != originalDesc)
                    {
                        match.text = trans;
                        Logging.Info($"[UILevelCompleteOverviewIncreaseListing] Applied translation: '{originalDesc}' -> '{trans}'");
                    }
                    else if (!dict.ContainsKey(originalDesc))
                    {
                        dict[originalDesc] = originalDesc;
                        LanguageManager.SaveCurrentLanguage();
                        Logging.Info($"[UILevelCompleteOverviewIncreaseListing] Added missing key: '{originalDesc}'");
                    }
                }

                // Apply global TMP font if available
                var tmpFont = Plugin.GlobalTMPFont;
                if (tmpFont != null)
                    TMPFontReplacer.ApplyFontToTMP(match, tmpFont);
            }
            catch (Exception e)
            {
                Logging.Error($"[UILevelCompleteOverviewIncreaseListing] InitializePostfix error: {e}");
            }
        }
    }
}
