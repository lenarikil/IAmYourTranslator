using System;
using System.IO;
using HarmonyLib;
using UnityEngine;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(UILevelCompleteScreen), "TryDisplayOverview")]
    public static class UILevelCompleteScreen_Patch
    {
        [HarmonyPostfix]
        public static void TryDisplayOverviewPostfix(UILevelCompleteScreen __instance)
        {
            try
            {
                if (__instance == null) return;

                // Get overviewDetails field
                var overviewDetails = Traverse.Create(__instance).Field("overviewDetails").GetValue<UILevelCompleteOverviewDetails>();
                if (overviewDetails == null)
                {
                    Logging.Warn("[UILevelCompleteScreen] overviewDetails is null");
                    return;
                }

                // Find logo through overviewDetails
                Transform logoTransform = overviewDetails.transform.Find("logo");
                if (logoTransform != null)
                {
                    GameObject logoObj = logoTransform.gameObject;
                    string texturesDir = Path.Combine(BepInEx.Paths.ConfigPath, "IAmYourTranslator", "textures");
                    string logoFile = Path.Combine(texturesDir, "UILogoText.png");
                    UITextureReplacer.ApplyTo(logoObj, logoFile, false);
                    Logging.Info("[UILevelCompleteScreen] Applied UILogoText.png to logo object");
                }
                else
                {
                    Logging.Warn("[UILevelCompleteScreen] Logo object not found in overviewDetails");
                }
            }
            catch (Exception e)
            {
                Logging.Warn($"[UILevelCompleteScreen] TryDisplayOverviewPostfix error: {e}");
            }
        }
    }
}