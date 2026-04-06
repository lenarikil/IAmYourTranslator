using System;
using System.IO;
using HarmonyLib;
using UnityEngine;
using TMPro;
using static IAmYourTranslator.CommonFunctions;
using IAmYourTranslator.json;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(UILevelCompleteScreen), "Start")]
    public static class UILevelCompleteScreen_Patch
    {
        // Set to false to disable widescreen layout adjustments
        private static bool enableWideScreenLayout = true;

        [HarmonyPostfix]
        public static void StartPostfix(UILevelCompleteScreen __instance)
        {
            try
            {
                if (__instance == null) return;

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
                    Logging.Info("[UILevelCompleteScreen] Applied font to all TMP text");
                }

                // Try to replace logo early on Start so it's visible in all screens
                Transform canvasTrans = __instance.transform.Find("Canvas");
                if (canvasTrans == null)
                    canvasTrans = RecursiveFindChild(__instance.transform, "Canvas");

                if (canvasTrans != null)
                {
                    // Attempt to find logo under Level Name Border first
                    Transform levelNameBorder = RecursiveFindChild(canvasTrans, "UI - Level Name Border");
                    Transform logoTransform = null;
                    if (levelNameBorder != null)
                    {
                        logoTransform = levelNameBorder.Find("Logo") ?? RecursiveFindChild(levelNameBorder, "Logo");
                        if (logoTransform == null)
                            logoTransform = levelNameBorder.Find("logo") ?? RecursiveFindChild(levelNameBorder, "logo");
                    }

                    // Fallback: search whole canvas for Logo
                    if (logoTransform == null)
                        logoTransform = RecursiveFindChild(canvasTrans, "Logo") ?? RecursiveFindChild(canvasTrans, "logo");

                    if (logoTransform != null)
                    {
                        GameObject logoObj = logoTransform.gameObject;
                        if (Plugin.EnableTextureReplacementEntry.Value && LanguageManager.CurrentSummary != null)
                        {
                            string logoFile = Path.Combine(LanguageManager.CurrentSummary.Paths.TexturesDir, "UILogoText.png");
                            UITextureReplacer.ApplyTo(logoObj, logoFile, false);
                            Logging.Info($"[UILevelCompleteScreen] Applied UILogoText.png (exists={File.Exists(logoFile)})");
                        }
                        else
                        {
                            Logging.Info($"[UILevelCompleteScreen] Texture replacement disabled or no language loaded");
                        }
                    }
                    else
                    {
                        Logging.Warn("[UILevelCompleteScreen] Logo object not found on Start");
                    }
                }

                // Apply widescreen layout adjustments if enabled
                if (enableWideScreenLayout)
                {
                    WideScreenLevelCompletePatch(__instance);
                }
            }
            catch (Exception e)
            {
                Logging.Warn($"[UILevelCompleteScreen] StartPostfix error: {e}");
            }
        }

        /// <summary>
        /// Adjusts level complete screen layout to use full screen width (widescreen mode).
        /// Stretches Stretch Anchor under Level Name Border and disables side panels.
        /// Also stretches Overview Anchor to full width.
        /// </summary>
        private static void WideScreenLevelCompletePatch(UILevelCompleteScreen __instance)
        {
            try
            {
                // Find Canvas in the Level Complete Screen
                Transform canvasTrans = __instance.transform.Find("Canvas");
                if (canvasTrans == null)
                    canvasTrans = RecursiveFindChild(__instance.transform, "Canvas");

                if (canvasTrans == null)
                {
                    Logging.Warn("[UILevelCompleteScreen] Canvas not found");
                    return;
                }

                // Find "UI - Level Name Border"
                Transform levelNameBorder = RecursiveFindChild(canvasTrans, "UI - Level Name Border");
                if (levelNameBorder == null)
                {
                    Logging.Warn("[UILevelCompleteScreen] UI - Level Name Border not found");
                    return;
                }

                // Stretch Level Name Border to full width
                var levelNameBorderRt = levelNameBorder.GetComponent<RectTransform>();
                if (levelNameBorderRt != null)
                {
                    levelNameBorderRt.anchorMin = new Vector2(0f, levelNameBorderRt.anchorMin.y);
                    levelNameBorderRt.anchorMax = new Vector2(1f, levelNameBorderRt.anchorMax.y);
                    levelNameBorderRt.offsetMin = new Vector2(0f, levelNameBorderRt.offsetMin.y);
                    levelNameBorderRt.offsetMax = new Vector2(0f, levelNameBorderRt.offsetMax.y);
                    levelNameBorderRt.sizeDelta = new Vector2(0f, levelNameBorderRt.sizeDelta.y);
                    Logging.Info("[UILevelCompleteScreen] Expanded UI - Level Name Border to full width");
                }

                // Find Stretch Anchor under Level Name Border
                Transform stretchAnchor = levelNameBorder.Find("Stretch Anchor") ?? RecursiveFindChild(levelNameBorder, "Stretch Anchor");
                if (stretchAnchor == null)
                {
                    Logging.Warn("[UILevelCompleteScreen] Stretch Anchor not found under Level Name Border");
                    return;
                }

                // Disable Left Side and Right Side
                string[] toDisable = new[] { "Left Side", "Right Side" };
                foreach (var name in toDisable)
                {
                    var childTrans = stretchAnchor.Find(name) ?? RecursiveFindChild(stretchAnchor, name);
                    if (childTrans != null)
                    {
                        childTrans.gameObject.SetActive(false);
                        Logging.Info($"[UILevelCompleteScreen] Disabled '{name}' under Stretch Anchor");
                    }
                }

                // Stretch Stretch Anchor to full width
                var stretchRt = stretchAnchor.GetComponent<RectTransform>();
                if (stretchRt != null)
                {
                    stretchRt.anchorMin = new Vector2(0f, stretchRt.anchorMin.y);
                    stretchRt.anchorMax = new Vector2(1f, stretchRt.anchorMax.y);
                    stretchRt.offsetMin = new Vector2(0f, stretchRt.offsetMin.y);
                    stretchRt.offsetMax = new Vector2(0f, stretchRt.offsetMax.y);
                    stretchRt.sizeDelta = new Vector2(0f, stretchRt.sizeDelta.y);
                    Logging.Info("[UILevelCompleteScreen] Expanded Stretch Anchor to full width");
                }

                // WIDESCREEN: Find and expand Overview Anchor to full width
                Transform scaleAnchor = RecursiveFindChild(canvasTrans, "Scale Anchor");
                if (scaleAnchor != null)
                {
                    Transform overviewAnchor = scaleAnchor.Find("Overview Anchor") ?? RecursiveFindChild(scaleAnchor, "Overview Anchor");
                    if (overviewAnchor != null)
                    {
                        var overviewRt = overviewAnchor.GetComponent<RectTransform>();
                        if (overviewRt != null)
                        {
                            StretchRectTransformHorizontal(overviewRt);
                            Logging.Info("[UILevelCompleteScreen] Stretched Overview Anchor to full width (widescreen)");
                        }
                        else
                        {
                            Logging.Warn("[UILevelCompleteScreen] Overview Anchor has no RectTransform");
                        }
                    }
                    else
                    {
                        Logging.Warn("[UILevelCompleteScreen] Overview Anchor not found under Scale Anchor");
                    }
                }
                else
                {
                    Logging.Warn("[UILevelCompleteScreen] Scale Anchor not found");
                }

                Logging.Info("[UILevelCompleteScreen] WideScreen layout adjustments applied");
            }
            catch (Exception e)
            {
                Logging.Error($"[UILevelCompleteScreen] Error in WideScreenLevelCompletePatch: {e}");
            }
        }
    }
}
