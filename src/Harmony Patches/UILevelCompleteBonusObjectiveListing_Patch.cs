using System;
using System.Globalization;
using HarmonyLib;
using TMPro;
using Objectives;
using UnityEngine;
using static IAmYourTranslator.CommonFunctions;
using IAmYourTranslator.json;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(UILevelCompleteBonusObjectiveListing), "Initialize")]
    public static class UILevelCompleteBonusObjectiveListing_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(UILevelCompleteBonusObjectiveListing __instance, LevelObjective objective, UILevelCompleteBonusObjectiveListing.CheckType checkType, UILevelCompleteScreen root, float bestTime)
        {
            try
            {
                if (__instance == null)
                    return;

                // Get the array of TMP_Text from the private field `texts`
                var textsField = AccessTools.Field(typeof(UILevelCompleteBonusObjectiveListing), "texts");
                var texts = textsField?.GetValue(__instance) as TMP_Text[];
                if (texts == null || texts.Length == 0)
                    return;

                // Only apply the centralized font — translation will be performed by a separate patch on GetDescription
                var font = TMPFontReplacer.GetCachedFont(Plugin.GlobalFontPath);
                if (font == null)
                {
                    // do nothing if font is not found
                    return;
                }

                foreach (var tmp in texts)
                {
                    if (tmp == null) continue;
                    TMPFontReplacer.ApplyFontToTMP(tmp, font);
                }
            }
            catch (Exception e)
            {
                Logging.Warn($"[UILevelCompleteBonusObjectiveListing_Patch] Error: {e}");
            }
        }
    }
}
