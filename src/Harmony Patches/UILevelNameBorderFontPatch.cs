using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using static IAmYourTranslator.CommonFunctions;
using BepInEx;

namespace IAmYourTranslator.HarmonyPatches
{
    [HarmonyPatch(typeof(UILevelNameBorder), "Initialize")]
    public static class UILevelNameBorderFontPatch
    {
        [HarmonyPostfix]
        public static void Postfix(UILevelNameBorder __instance)
        {
            try
            {
                // Use globally cached font or load as fallback
                var tmpFont = Plugin.GlobalTMPFont;
                if (tmpFont == null)
                {
                    Logging.Warn("[UILevelNameBorderFontPatch] Failed to load font, skipping replacement.");
                    return;
                }

                // Get references to text elements
                TMP_Text levelNumber = Traverse.Create(__instance).Field("levelNumber").GetValue<TMP_Text>();
                TMP_Text levelName = Traverse.Create(__instance).Field("levelName").GetValue<TMP_Text>();

                // Safely replace fonts
                TMPFontReplacer.ApplyFontToTMP(levelNumber, tmpFont);
                TMPFontReplacer.ApplyFontToTMP(levelName, tmpFont);
            }
            catch (Exception e)
            {
                Logging.Warn($"[UILevelNameBorderFontPatch] Error when replacing font: {e.Message}");
            }
        }
    }
}
