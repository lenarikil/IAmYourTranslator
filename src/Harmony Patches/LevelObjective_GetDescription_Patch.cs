using System;
using System.Collections.Generic;
using HarmonyLib;
using Objectives;
using IAmYourTranslator.json;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(LevelObjective), "GetDescription")]
    public static class LevelObjective_GetDescription_Patch
    {
        // Postfix translates the GetDescription result for bonus objectives
        public static void Postfix(LevelObjective __instance, bool includeCount, ref string __result)
        {
            try
            {
                if (__instance == null) return;
                if (!__instance.IsBonus()) return; // translate only bonuses
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
                if (dict.TryGetValue(original, out var val))
                {
                    if (!string.IsNullOrEmpty(val) && val != original)
                        __result = val;
                }
                else
                {
                    // Add key for future translation, without changing display now
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
