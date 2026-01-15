using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using static IAmYourTranslator.CommonFunctions;
using IAmYourTranslator.json;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(HUDAmmoIndicator), "Start")]
    public static class HUDAmmoIndicator_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(HUDAmmoIndicator __instance)
        {
            try
            {
                if (__instance == null) return;

                var field = AccessTools.Field(typeof(HUDAmmoIndicator), "noUsesText");
                var tmp = field?.GetValue(__instance) as TMP_Text;
                if (tmp == null) return;

                // Replace text with translation from language file
                if (LanguageManager.IsLoaded)
                {
                    var dict = LanguageManager.CurrentLanguage.hardCoded;
                    if (dict == null)
                        dict = LanguageManager.CurrentLanguage.hardCoded = new System.Collections.Generic.Dictionary<string, string>();

                    string translatedText;
                    bool keyExisted = dict.TryGetValue("noAmmo", out translatedText);
                    if (!keyExisted)
                    {
                        dict["noAmmo"] = "No ammo";
                        translatedText = "No ammo";
                        LanguageManager.SaveCurrentLanguage();
                        Logging.Info($"[HUDAmmoIndicator_Patch] Added missing hardCoded key: 'noAmmo'");
                    }
                    tmp.text = translatedText;
                }
                else
                {
                    tmp.text = "No ammo";
                }

                // Apply centralized TMP font
                var font = TMPFontReplacer.GetCachedFont(Plugin.GlobalFontPath);
                if (font != null)
                    TMPFontReplacer.ApplyFontToTMP(tmp, font);
            }
            catch (Exception e)
            {
                Logging.Warn($"[HUDAmmoIndicator_Patch] Error: {e}");
            }
        }
    }
}
