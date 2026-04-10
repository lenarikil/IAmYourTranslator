using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using TMPro;
using IAmYourTranslator.json;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(HUDObjectiveListing), "Update")]
    public static class HUDObjectiveListing_Translate_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(HUDObjectiveListing __instance)
        {
            try
            {
                if (__instance == null) return;
                if (!LanguageManager.IsLoaded) return;

                // Get the objective
                var objectiveField = AccessTools.Field(typeof(HUDObjectiveListing), "objective");
                var objective = objectiveField?.GetValue(__instance) as Objectives.LevelObjective;
                if (objective == null) return;

                // Get texts array
                var textsField = AccessTools.Field(typeof(HUDObjectiveListing), "texts");
                var texts = textsField?.GetValue(__instance) as TMP_Text[];
                if (texts == null || texts.Length == 0) return;

                // Select dictionary depending on objective type: bonus or main
                var dict = objective.IsBonus()
                    ? LanguageManager.CurrentLanguage.bonusObjectives
                    : LanguageManager.CurrentLanguage.mainObjectives;

                if (dict == null)
                {
                    if (objective.IsBonus())
                        dict = LanguageManager.CurrentLanguage.bonusObjectives = new Dictionary<string, string>();
                    else
                        dict = LanguageManager.CurrentLanguage.mainObjectives = new Dictionary<string, string>();
                }

                // Process each text element
                foreach (var tmp in texts)
                {
                    if (tmp == null || string.IsNullOrEmpty(tmp.text)) continue;

                    string current = tmp.text;
                    
                    // Skip texts containing [WEAPON] placeholder - they will be handled later with actual weapon name
                    if (current.Contains("[WEAPON]"))
                    {
                        continue;
                    }
                    
                    // Handle trailing counter patterns, e.g. "Kill Enemies: [0/9]", "Collect Items (0/5)", "Do X: 0/3"
                    var counterRegex = new Regex("^(.*?)(\\s*[:：]?\\s*(?:\\[[0-9]+\\/[0-9]+\\]|\\([0-9]+\\/[0-9]+\\)|[0-9]+\\/[0-9]+))$", RegexOptions.Compiled);
                    var m = counterRegex.Match(current);
                    string baseText = current;
                    string suffix = null;
                    if (m.Success)
                    {
                        baseText = m.Groups[1].Value.TrimEnd();
                        suffix = m.Groups[2].Value; // includes separator and counter
                    }
                    
                    string original = ResolveOriginalTranslationKey(baseText, dict);

                    // Try to get translation
                    if (dict.TryGetValue(original, out var translated) && !string.IsNullOrEmpty(translated) && translated != original)
                    {
                        tmp.text = translated + (suffix ?? "");
                    }
                    else if (!dict.ContainsKey(original))
                    {
                        // Register missing key (base text without counter)
                        dict[original] = original;
                        LanguageManager.SaveCurrentLanguage();
                        Logging.Info($"[HUDObjectiveListing_Translate_Patch] Added missing translation key ({(objective.IsBonus()?"bonus":"main")}): '{original}'");
                    }
                }
            }
            catch (Exception e)
            {
                Logging.Warn($"[HUDObjectiveListing_Translate_Patch] Error: {e}");
            }
        }
    }
}