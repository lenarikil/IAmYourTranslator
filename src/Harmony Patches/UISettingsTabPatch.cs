using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using IAmYourTranslator.json;
using static IAmYourTranslator.CommonFunctions;
using BepInEx;

namespace IAmYourTranslator.HarmonyPatches
{
    [HarmonyPatch]
    public static class UISettingsTabPatch
    {
        // ----------------------------
        // Initialize — translate menu name and apply font
        // ----------------------------
        [HarmonyPatch(typeof(UISettingsTab), "Initialize")]
        [HarmonyPostfix]
        public static void InitializePostfix(UISettingsTab __instance, UISettingsSubMenu subMenu)
        {
            try
            {
                if (__instance == null || subMenu == null) return;

                // Get the original menu name
                string originalName = subMenu.GetMenuName() ?? "";
                string translatedText = originalName;

                // Check if the key exists in the settings dictionary
                if (!LanguageManager.CurrentLanguage.settings.TryGetValue(originalName, out var foundTranslation))
                {
                    // If the key doesn't exist, add it to the dictionary with the original value
                    LanguageManager.CurrentLanguage.settings[originalName] = originalName;
                    LanguageManager.SaveCurrentLanguage();
                    Logging.Info($"[UISettingsTab] Added new key to settings: '{originalName}'");
                }
                else
                {
                    // Use the found translation if it's not empty
                    if (!string.IsNullOrEmpty(foundTranslation) && foundTranslation != originalName)
                    {
                        translatedText = foundTranslation;
                        Logging.Info($"[UISettingsTab] Applied translation: '{originalName}' -> '{translatedText}'");
                    }
                }

                // Apply translation through Traverse
                var nameTextField = HarmonyLib.Traverse.Create(__instance).Field("nameText").GetValue<TMP_Text>();
                if (nameTextField != null)
                {
                    nameTextField.text = translatedText;

                    // Also apply global font if it's loaded
                    var tmpFont = Plugin.GlobalTMPFont;
                    if (tmpFont != null)
                        TMPFontReplacer.ApplyFontToTMP(nameTextField, tmpFont);
                }
            }
            catch (Exception e)
            {
                Logging.Error($"[UISettingsTab] Error in InitializePostfix: {e}");
            }
        }
    }
}