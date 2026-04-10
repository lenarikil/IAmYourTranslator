using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using Objectives;
using IAmYourTranslator.json;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(LevelObjective), "GetDescription")]
    public static class LevelObjective_GetDescription_Patch
    {
        // Postfix translates the GetDescription result for objectives (handles counters like "Kill Enemies: [0/9]")
        public static void Postfix(LevelObjective __instance, bool includeCount, ref string __result)
        {
            try
            {
                if (__instance == null) return;
            if (string.IsNullOrEmpty(__result)) return;
            if (!LanguageManager.IsLoaded) return;

                // Select dictionary depending on objective type: bonus or main
                var dict = __instance.IsBonus()
                    ? LanguageManager.CurrentLanguage.bonusObjectives
                    : LanguageManager.CurrentLanguage.mainObjectives;

                if (dict == null)
                {
                    if (__instance.IsBonus())
                        dict = LanguageManager.CurrentLanguage.bonusObjectives = new Dictionary<string, string>();
                    else
                        dict = LanguageManager.CurrentLanguage.mainObjectives = new Dictionary<string, string>();
                }

                var original = __result;

                // Skip objectives containing [WEAPON] placeholder - they will be handled later with actual weapon name
                if (original.Contains("[WEAPON]"))
                {
                    return;
                }

                // Try exact match first
                if (dict.TryGetValue(original, out var val) && !string.IsNullOrEmpty(val) && val != original)
                {
                    __result = val;
                    return;
                }

                // Handle trailing counter patterns, e.g. "Kill Enemies: [0/9]", "Collect Items (0/5)", "Do X: 0/3"
                var counterRegex = new Regex("^(.*?)(\\s*[:：]?\\s*(?:\\[[0-9]+\\/[0-9]+\\]|\\([0-9]+\\/[0-9]+\\)|[0-9]+\\/[0-9]+))$", RegexOptions.Compiled);
                var m = counterRegex.Match(original);
                if (m.Success)
                {
                    var baseText = m.Groups[1].Value.TrimEnd();
                    var suffix = m.Groups[2].Value; // includes separator and counter

                    if (dict.TryGetValue(baseText, out val) && !string.IsNullOrEmpty(val) && val != baseText)
                    {
                        __result = val + suffix;
                        return;
                    }
                    else if (!dict.ContainsKey(baseText))
                    {
                        // register base text for future translation (do not change current display)
                        dict[baseText] = baseText;
                        LanguageManager.SaveCurrentLanguage();
                        Logging.Info($"[LevelObjective_GetDescription_Patch] Added missing translation key ({(__instance.IsBonus()?"bonus":"main")}): '{baseText}'");
                        return;
                    }
                }

                // Fallback: register full original string if missing
                if (!dict.ContainsKey(original))
                {
                    dict[original] = original;
                    LanguageManager.SaveCurrentLanguage();
                    Logging.Info($"[LevelObjective_GetDescription_Patch] Added missing translation key ({(__instance.IsBonus()?"bonus":"main")}): '{original}'");
                }
            }
            catch (Exception e)
            {
                Logging.Warn($"[LevelObjective_GetDescription_Patch] Error: {e}");
            }
        }
    }
}
