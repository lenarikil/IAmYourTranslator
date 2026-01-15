using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(HUDObjectiveListing), "Update")]
    public static class HUDObjectiveListing_Font_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(HUDObjectiveListing __instance)
        {
            try
            {
                if (__instance == null) return;

                var textsField = AccessTools.Field(typeof(HUDObjectiveListing), "texts");
                var texts = textsField?.GetValue(__instance) as TMP_Text[];
                if (texts == null || texts.Length == 0) return;

                var font = TMPFontReplacer.GetCachedFont(Plugin.GlobalFontPath);
                if (font == null) return;

                foreach (var tmp in texts)
                {
                    if (tmp == null) continue;
                    // apply font only if different to avoid per-frame work
                    if (!Equals(tmp.font, font))
                        TMPFontReplacer.ApplyFontToTMP(tmp, font);
                }
            }
            catch (Exception e)
            {
                Logging.Warn($"[HUDObjectiveListing_Font_Patch] Error: {e}");
            }
        }
    }
}
