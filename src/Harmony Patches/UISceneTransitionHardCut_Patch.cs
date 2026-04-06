using System;
using System.IO;
using HarmonyLib;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using IAmYourTranslator.json;
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
                // Apply font first
                var tmpFont = TMPFontReplacer.GetCachedFont(Plugin.GlobalFontPath);
                if (tmpFont != null)
                {
                    var allTmpTexts = __instance.GetComponentsInChildren<TMP_Text>(true);
                    foreach (var tmp in allTmpTexts)
                    {
                        if (tmp != null)
                        {
                            TMPFontReplacer.ApplyFontToTMP(tmp, tmpFont);
                        }
                    }
                    Logging.Info("[UISceneTransitionHardCut] Applied font to all TMP text");
                }

                // Get the logo Image component from the instance
                var logoField = HarmonyLib.Traverse.Create(__instance).Field("logo").GetValue<Image>();
                if (logoField == null)
                {
                    Logging.Warn("[UISceneTransitionHardCut] Logo Image field not found");
                    return;
                }

                // Try to replace with the custom logo texture (same as main menu)
                if (Plugin.EnableTextureReplacementEntry.Value && LanguageManager.CurrentSummary != null)
                {
                    string logoFile = Path.Combine(LanguageManager.CurrentSummary.Paths.TexturesDir, "UILogoText.png");
                    string activeSceneName = SceneManager.GetActiveScene().name;
                    bool invertAlpha =
                        string.Equals(activeSceneName, "#027_Special_EndCredits", StringComparison.Ordinal) ||
                        IsEndCreditsFadeInLogoNode(logoField.transform);
                    // Use UITextureReplacer to apply the custom logo
                    UITextureReplacer.ApplyTo(logoField.gameObject, logoFile, invertAlpha);
                    Logging.Info($"[UISceneTransitionHardCut] Applied custom logo texture (scene='{activeSceneName}', invertAlpha={invertAlpha}, exists={File.Exists(logoFile)})");
                }
                else
                {
                    Logging.Info($"[UISceneTransitionHardCut] Texture replacement disabled or no language loaded");
                }
            }
            catch (Exception e)
            {
                Logging.Error($"[UISceneTransitionHardCut] Error in Start postfix: {e}");
            }
        }

        private static bool IsEndCreditsFadeInLogoNode(Transform tr)
        {
            if (tr == null || !string.Equals(tr.name, "Logo", StringComparison.OrdinalIgnoreCase))
                return false;

            Transform fadeIn = tr.parent;
            if (fadeIn == null || !string.Equals(fadeIn.name, "Fade In", StringComparison.OrdinalIgnoreCase))
                return false;

            Transform creditsUi = fadeIn.parent;
            if (creditsUi == null || !string.Equals(creditsUi.name, "Credits UI", StringComparison.OrdinalIgnoreCase))
                return false;

            Transform creditAnchor = creditsUi.parent;
            if (creditAnchor == null || !string.Equals(creditAnchor.name, "Credit Anchor", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }
    }
}
