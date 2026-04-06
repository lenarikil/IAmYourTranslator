using System;
using System.IO;
using HarmonyLib;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using IAmYourTranslator.json;
using BepInEx;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(UIPauseMenu))]
    public static class UIPauseMenu_LogoPatch
    {
        // Set to false to disable widescreen layout adjustments
        private static bool enableWideScreenLayout = true;

        [HarmonyPostfix]
        [HarmonyPatch("Start")]
        public static void StartPostfix(UIPauseMenu __instance)
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
                    Logging.Info("[UIPauseMenu] Applied font to all TMP text");
                }

                // Try direct path first: Canvas/Corner Anchor/Logo
                Transform logoTrans = __instance.transform.Find("Canvas/Corner Anchor/Logo");

                // Fallback: recursive search for a child named "Logo"
                if (logoTrans == null)
                    logoTrans = RecursiveFindChild(__instance.transform, "Logo");

                if (logoTrans == null)
                {
                    Logging.Warn("[UIPauseMenu] Logo GameObject not found under pause menu");
                    return;
                }

                GameObject logoObj = logoTrans.gameObject;

                if (Plugin.EnableTextureReplacementEntry.Value && LanguageManager.CurrentSummary != null)
                {
                    string logoFile = Path.Combine(LanguageManager.CurrentSummary.Paths.TexturesDir, "UILogoText.png");
                    UITextureReplacer.ApplyTo(logoObj, logoFile, false);
                    Logging.Info($"[UIPauseMenu] Applied custom logo texture (exists={File.Exists(logoFile)})");
                }
                else
                {
                    Logging.Info("[UIPauseMenu] Texture replacement disabled or no language loaded");
                }

                // Apply widescreen layout adjustments if enabled
                if (enableWideScreenLayout)
                {
                    WideScreenPausePatch(__instance);
                }
            }
            catch (Exception e)
            {
                Logging.Error($"[UIPauseMenu] Error in Start postfix: {e}");
            }
        }

        /// <summary>
        /// Adjusts pause menu layout to use full screen width (widescreen mode).
        /// Stretches containers but preserves original offsets where applicable.
        /// </summary>
        private static void WideScreenPausePatch(UIPauseMenu __instance)
        {
            try
            {
                Transform canvasTrans = __instance.transform.Find("Canvas");
                if (canvasTrans == null)
                    canvasTrans = RecursiveFindChild(__instance.transform, "Canvas");

                if (canvasTrans == null)
                    return;

                // Make canvas fill the screen
                var canvasRt = canvasTrans.GetComponent<RectTransform>();
                if (canvasRt != null)
                {
                    canvasRt.anchorMin = new Vector2(0f, 0f);
                    canvasRt.anchorMax = new Vector2(1f, 1f);
                    canvasRt.offsetMin = Vector2.zero;
                    canvasRt.offsetMax = Vector2.zero;
                }

                // Stretch Anchor adjustments
                Transform stretch = RecursiveFindChild(canvasTrans, "Stretch Anchor");
                if (stretch != null)
                {
                    // Make Stretch Anchor occupy full screen to remove side gaps
                    var stretchRt = stretch.GetComponent<RectTransform>();
                    if (stretchRt != null)
                    {
                        stretchRt.anchorMin = new Vector2(0f, 0f);
                        stretchRt.anchorMax = new Vector2(1f, 1f);
                        stretchRt.offsetMin = Vector2.zero;
                        stretchRt.offsetMax = Vector2.zero;
                        stretchRt.sizeDelta = Vector2.zero;
                        stretchRt.pivot = new Vector2(0.5f, 0.5f);
                        Logging.Info("[UIPauseMenu] Stretched 'Stretch Anchor' to full screen");
                    }
                }

                // Name Anchor adjustments (re-anchor to left, preserve existing offsets)
                Transform nameAnchor = RecursiveFindChild(canvasTrans, "Name Anchor");
                if (nameAnchor != null)
                {
                    var rt = nameAnchor.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchorMin = new Vector2(0f, rt.anchorMin.y);
                        rt.anchorMax = new Vector2(0f, rt.anchorMax.y);
                        rt.pivot = new Vector2(0f, rt.pivot.y);
                        // Preserve original anchoredPosition/offsets to keep original padding
                        Logging.Info("[UIPauseMenu] Re-anchored Name Anchor to left (preserved offsets)");
                    }
                }

                // Corner Anchor adjustments: expand horizontally for Logo (left) and Button List (right)
                Transform corner = RecursiveFindChild(canvasTrans, "Corner Anchor");
                if (corner != null)
                {
                    // Ensure corner container stretches horizontally across screen
                    var cornerRt = corner.GetComponent<RectTransform>();
                    if (cornerRt != null)
                    {
                        cornerRt.anchorMin = new Vector2(0f, cornerRt.anchorMin.y);
                        cornerRt.anchorMax = new Vector2(1f, cornerRt.anchorMax.y);
                        cornerRt.offsetMin = new Vector2(0f, cornerRt.offsetMin.y);
                        cornerRt.offsetMax = new Vector2(0f, cornerRt.offsetMax.y);
                        cornerRt.sizeDelta = new Vector2(0f, cornerRt.sizeDelta.y);
                        Logging.Info("[UIPauseMenu] Expanded 'Corner Anchor' horizontally");
                    }

                    // Anchor Logo to left (preserve vertical offset)
                    var logoT = corner.Find("Logo") ?? RecursiveFindChild(corner, "Logo");
                    if (logoT != null)
                    {
                        var rt = logoT.GetComponent<RectTransform>();
                        if (rt != null)
                        {
                            rt.anchorMin = new Vector2(0f, rt.anchorMin.y);
                            rt.anchorMax = new Vector2(0f, rt.anchorMax.y);
                            rt.pivot = new Vector2(0f, rt.pivot.y);
                            // Preserve original horizontal offset
                            Logging.Info("[UIPauseMenu] Anchored Logo to left in Corner Anchor (preserved offsets)");
                        }
                    }

                    // Anchor Button List to right (preserve vertical offset)
                    var buttonListT = corner.Find("Button List") ?? RecursiveFindChild(corner, "Button List");
                    if (buttonListT != null)
                    {
                        var rt = buttonListT.GetComponent<RectTransform>();
                        if (rt != null)
                        {
                            rt.anchorMin = new Vector2(1f, rt.anchorMin.y);
                            rt.anchorMax = new Vector2(1f, rt.anchorMax.y);
                            rt.pivot = new Vector2(1f, rt.pivot.y);
                            // Preserve original horizontal offset
                            Logging.Info("[UIPauseMenu] Anchored Button List to right in Corner Anchor (preserved offsets)");
                        }
                    }
                }

                Logging.Info("[UIPauseMenu] WideScreen layout adjustments applied");
            }
            catch (Exception e)
            {
                Logging.Error($"[UIPauseMenu] Error in WideScreenPausePatch: {e}");
            }
        }
    }
}
