using System;
using System.IO;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using BepInEx;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(UISceneTransitionHardCut))]
    public static class UISceneTransitionHardCut_Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch("Start")]
        public static void StartPostfix(UISceneTransitionHardCut __instance)
        {
            try
            {
                // Get the logo Image component from the instance
                var logoField = HarmonyLib.Traverse.Create(__instance).Field("logo").GetValue<Image>();
                if (logoField == null)
                {
                    Logging.Warn("[UISceneTransitionHardCut] Logo Image field not found");
                    return;
                }

                // Try to replace with the custom logo texture (same as main menu)
                string texturesDir = Path.Combine(Paths.ConfigPath, "IAmYourTranslator", "textures");
                string logoFile = Path.Combine(texturesDir, "UILogoText.png");

                // Use UITextureReplacer to apply the custom logo
                UITextureReplacer.ApplyTo(logoField.gameObject, logoFile, false);
                Logging.Info("[UISceneTransitionHardCut] Applied custom logo texture");
            }
            catch (Exception e)
            {
                Logging.Error($"[UISceneTransitionHardCut] Error in Start postfix: {e}");
            }
        }
    }
}
