using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using IAmYourTranslator.json;
using static IAmYourTranslator.CommonFunctions;
using BepInEx;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(UILevelCompletePopUpListing))]
    public static class UILevelCompletePopUpListing_Patch
    {
        [HarmonyPatch("Initialize")]
        [HarmonyPostfix]
        public static void InitializePostfix(UILevelCompletePopUpListing __instance, string description, string amount, Texture iconTexture)
        {
            try
            {
                if (__instance == null || string.IsNullOrEmpty(description))
                    return;
                if (!LanguageManager.IsLoaded || LanguageManager.CurrentLanguage == null)
                    return;

                // Get the timerIncrease dictionary (using the same as HUDTimerIncrease)
                var dict = LanguageManager.CurrentLanguage.timerIncrease;
                if (dict == null)
                    dict = LanguageManager.CurrentLanguage.timerIncrease = new System.Collections.Generic.Dictionary<string, string>();

                // Get the descriptionText field
                var descField = HarmonyLib.Traverse.Create(__instance).Field("descriptionText").GetValue<TMP_Text>();
                if (descField == null)
                    return;

                string translatedDesc = description;

                // Check for translation availability
                if (dict.TryGetValue(description, out var trans) && !string.IsNullOrEmpty(trans) && trans != description)
                {
                    translatedDesc = trans;
                    Logging.Info($"[UILevelCompletePopUpListing] Applied translation: '{description}' -> '{trans}'");
                }
                else if (!dict.ContainsKey(description))
                {
                    // Add missing key
                    dict[description] = description;
                    LanguageManager.SaveCurrentLanguage();
                    Logging.Info($"[UILevelCompletePopUpListing] Added missing key: '{description}'");
                }

                // Set the translated description
                descField.text = translatedDesc;

                // Apply global font
                var tmpFont = Plugin.GlobalTMPFont;
                if (tmpFont != null)
                    TMPFontReplacer.ApplyFontToTMP(descField, tmpFont);
            }
            catch (Exception e)
            {
                Logging.Error($"[UILevelCompletePopUpListing] Error in InitializePostfix: {e}");
            }
        }
    }
}
