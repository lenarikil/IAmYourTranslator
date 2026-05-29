using System;
using HarmonyLib;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using IAmYourTranslator.json;
using IAmYourTranslator.Utils;
using BepInEx;
using static IAmYourTranslator.CommonFunctions;
using static IAmYourTranslator.Utils.UILayoutHelpers;

namespace IAmYourTranslator.Patches
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
                ApplyFontToAllChildrenTMP(__instance, TMPFontReplacer.GetCachedFont(Plugin.GlobalFontPath), "[UIPauseMenu]");

                TryApplyLogoTexture(__instance.transform);

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
                    AnchorToLeft(rt);
                    Logging.Info("[UIPauseMenu] Re-anchored Name Anchor to left (preserved offsets)");
                }

                // Corner Anchor adjustments: expand horizontally for Logo (left) and Button List (right)
                Transform corner = RecursiveFindChild(canvasTrans, "Corner Anchor");
                if (corner != null)
                {
                    // Ensure corner container stretches horizontally across screen
                    var cornerRt = corner.GetComponent<RectTransform>();
                    StretchHorizontal(cornerRt);
                    Logging.Info("[UIPauseMenu] Expanded 'Corner Anchor' horizontally");

                    // Anchor Logo to left (preserve vertical offset)
                    var logoT = corner.Find("Logo") ?? RecursiveFindChild(corner, "Logo");
                    if (logoT != null)
                    {
                        var rt = logoT.GetComponent<RectTransform>();
                        AnchorToLeft(rt);
                        Logging.Info("[UIPauseMenu] Anchored Logo to left in Corner Anchor (preserved offsets)");
                    }

                    // Anchor Button List to right (preserve vertical offset)
                    var buttonListT = corner.Find("Button List") ?? RecursiveFindChild(corner, "Button List");
                    if (buttonListT != null)
                    {
                        var rt = buttonListT.GetComponent<RectTransform>();
                        AnchorToRight(rt);
                        Logging.Info("[UIPauseMenu] Anchored Button List to right in Corner Anchor (preserved offsets)");
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
