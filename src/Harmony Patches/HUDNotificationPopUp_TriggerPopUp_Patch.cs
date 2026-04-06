using System;
using HarmonyLib;
using TMPro;
using IAmYourTranslator.json;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.Harmony_Patches
{
    // Patch translates HUD notification text and applies the global TMP font.
    [HarmonyPatch(typeof(HUDNotificationPopUp), "TriggerPopUp")]
    public static class HUDNotificationPopUp_TriggerPopUp_Patch
    {
        [HarmonyPostfix]
        public static void TriggerPopUpPostfix(HUDNotificationPopUp __instance, string displayText)
        {
            try
            {
                if (__instance == null || string.IsNullOrEmpty(displayText))
                    return;

                if (!LanguageManager.IsLoaded)
                {
                    Logging.Warn("[HUDNotificationPopUp] Language not loaded, skipping translation");
                    return;
                }

                var tmp = Traverse.Create(__instance).Field("text").GetValue<TMP_Text>();
                if (tmp == null)
                    return;

                var dict = LanguageManager.CurrentLanguage.hudNotificationPopups;
                if (dict == null)
                    dict = LanguageManager.CurrentLanguage.hudNotificationPopups = new System.Collections.Generic.Dictionary<string, string>();

                // This TMP component is reused for different popup messages.
                // Clear per-component source cache to avoid "sticking" to the first captured key.
                ClearOriginalTextCache(tmp);
                TranslateTextAndSaveIfMissing(tmp, displayText, dict, "[HUDNotificationPopUp]");

                var tmpFont = TMPFontReplacer.GetCachedFont(Plugin.GlobalFontPath);
                if (tmpFont != null)
                    TMPFontReplacer.ApplyFontToTMP(tmp, tmpFont);
            }
            catch (Exception e)
            {
                Logging.Error($"[HUDNotificationPopUp] Error in TriggerPopUpPostfix: {e}");
            }
        }
    }
}
