using System;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;
using IAmYourTranslator.json;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(UILevelCompleteOverviewCameraOption), "Initialize")]
    public static class UILevelCompleteOverviewCameraOption_Patch
    {
        // track added keys to avoid saving repeatedly
        private static readonly System.Collections.Generic.HashSet<string> _addedKillKeys = new System.Collections.Generic.HashSet<string>();

        [HarmonyPostfix]
        public static void InitializePostfix(UILevelCompleteOverviewCameraOption __instance)
        {
            try
            {
                if (__instance == null) return;
                if (!LanguageManager.IsLoaded || LanguageManager.CurrentLanguage == null) return;

                var displayStrings = Traverse.Create(__instance).Field("displayStrings").GetValue<string[]>();
                if (displayStrings == null || displayStrings.Length == 0) return;

                var timerDict = LanguageManager.CurrentLanguage.timerIncrease;
                if (timerDict == null) timerDict = LanguageManager.CurrentLanguage.timerIncrease = new System.Collections.Generic.Dictionary<string, string>();

                var overviewDict = LanguageManager.CurrentLanguage.overviewScreen;
                if (overviewDict == null) overviewDict = LanguageManager.CurrentLanguage.overviewScreen = new System.Collections.Generic.Dictionary<string, string>();

                // Translate displayStrings[0] - "Overview" text
                if (!string.IsNullOrEmpty(displayStrings[0]))
                {
                    string overviewText = displayStrings[0];
                    Logging.Info($"[UILevelCompleteOverviewCameraOption] displayStrings[0] = '{overviewText}'");
                    
                    if (overviewDict.TryGetValue(overviewText, out var trans) && !string.IsNullOrEmpty(trans) && trans != overviewText)
                    {
                        displayStrings[0] = trans;
                        Logging.Info($"[UILevelCompleteOverviewCameraOption] Applied overview translation: '{overviewText}' -> '{trans}'");
                    }
                    else if (!overviewDict.ContainsKey(overviewText))
                    {
                        overviewDict[overviewText] = overviewText;
                        lock (_addedKillKeys)
                        {
                            if (!_addedKillKeys.Contains($"overview:{overviewText}"))
                            {
                                _addedKillKeys.Add($"overview:{overviewText}");
                                LanguageManager.SaveCurrentLanguage();
                                Logging.Info($"[UILevelCompleteOverviewCameraOption] Added missing overview key: '{overviewText}'");
                            }
                        }
                    }
                    else
                    {
                        Logging.Info($"[UILevelCompleteOverviewCameraOption] Overview key exists but has same value (no translation yet)");
                    }
                }

                // pattern: "#<num> - <key>"
                var rx = new Regex("^#(\\d+)\\s*-\\s*(.+)$");
                for (int i = 1; i < displayStrings.Length; i++)
                {
                    if (string.IsNullOrEmpty(displayStrings[i])) continue;
                    var m = rx.Match(displayStrings[i]);
                    if (!m.Success) continue;

                    string idx = m.Groups[1].Value;
                    string keyPart = m.Groups[2].Value.Trim();

                    if (string.IsNullOrEmpty(keyPart)) continue;

                    if (timerDict.TryGetValue(keyPart, out var trans) && !string.IsNullOrEmpty(trans) && trans != keyPart)
                    {
                        displayStrings[i] = $"#{idx} - {trans}";
                        Logging.Info($"[UILevelCompleteOverviewCameraOption] Applied translation for kill: '{keyPart}' -> '{trans}'");
                    }
                    else if (!timerDict.ContainsKey(keyPart))
                    {
                        timerDict[keyPart] = keyPart;
                        lock (_addedKillKeys)
                        {
                            if (!_addedKillKeys.Contains(keyPart))
                            {
                                _addedKillKeys.Add(keyPart);
                                LanguageManager.SaveCurrentLanguage();
                                Logging.Info($"[UILevelCompleteOverviewCameraOption] Added missing kill key: '{keyPart}'");
                            }
                        }
                    }
                }

                // Force UI refresh after translations applied
                try
                {
                    __instance.RefreshSetting();
                }
                catch (Exception e)
                {
                    Logging.Warn($"[UILevelCompleteOverviewCameraOption] RefreshSetting error: {e}");
                }
            }
            catch (Exception e)
            {
                Logging.Warn($"[UILevelCompleteOverviewCameraOption] InitializePostfix error: {e}");
            }
        }
    }
}
