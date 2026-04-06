using System;
using System.Collections;
using HarmonyLib;
using TMPro;
using UnityEngine;
using IAmYourTranslator.json;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.Harmony_Patches
{
    // Translates level failed header text and applies widescreen layout.
    [HarmonyPatch(typeof(UILevelFailed), "Initialize")]
    public static class UILevelFailed_Patch
    {
        // Set to false to disable widescreen layout adjustments
        private static bool enableWideScreenLayout = true;

        [HarmonyPostfix]
        public static void Postfix(UILevelFailed __instance, UILevelFailed.FailCondition condition, Vector3 deadCameraVelocity)
        {
            try
            {
                if (__instance == null) return;

                var field = AccessTools.Field(typeof(UILevelFailed), "headerText");
                var header = field?.GetValue(__instance) as TMP_Text;
                if (header == null) return;

                string original = header.text ?? string.Empty;
                string translated = original;

                if (LanguageManager.IsLoaded)
                {
                    var dict = LanguageManager.CurrentLanguage.levelFailedHeaders;
                    if (dict == null)
                        dict = LanguageManager.CurrentLanguage.levelFailedHeaders = new System.Collections.Generic.Dictionary<string, string>();

                    if (dict.TryGetValue(original, out var val) && !string.IsNullOrEmpty(val) && val != original)
                    {
                        translated = val;
                    }
                    else if (!dict.ContainsKey(original))
                    {
                        dict[original] = original;
                        LanguageManager.SaveCurrentLanguage();
                        Logging.Info($"[UILevelFailed_Patch] Added missing header translation key: '{original}'");
                    }
                }

                header.text = translated;

                var font = TMPFontReplacer.GetCachedFont(Plugin.GlobalFontPath);
                if (font != null)
                    TMPFontReplacer.ApplyFontToTMP(header, font);

                // Apply widescreen layout adjustments (delayed) if enabled
                if (enableWideScreenLayout)
                {
                    __instance.StartCoroutine(ApplyWidescreenNextFrame(__instance));
                }
            }
            catch (Exception e)
            {
                Logging.Warn($"[UILevelFailed_Patch] Error: {e}");
            }
        }

        private static IEnumerator ApplyWidescreenNextFrame(UILevelFailed instance)
        {
            // Wait one frame to ensure UI is built
            yield return null;

            if (instance == null) yield break;

            WideScreenLevelFailedPatch(instance);

            // Re-apply after a short delay in case animator/layout overrides first pass
            yield return new WaitForSecondsRealtime(0.1f);

            if (instance == null) yield break;

            WideScreenLevelFailedPatch(instance);
        }

        /// <summary>
        /// Adjusts level failed screen layout to use full screen width (widescreen mode).
        /// Stretches Level Name Border + Stretch Anchor and disables side panels.
        /// Also stretches Scale Anchor to full width.
        /// </summary>
        private static void WideScreenLevelFailedPatch(UILevelFailed __instance)
        {
            try
            {
                // Find Canvas (UILevelFailed can be attached to the Canvas object itself)
                Transform canvasTrans = null;
                if (__instance.transform != null)
                {
                    if (__instance.transform.name == "Canvas")
                        canvasTrans = __instance.transform;
                    else
                        canvasTrans = __instance.transform.Find("Canvas") ?? RecursiveFindChild(__instance.transform, "Canvas");
                }

                if (canvasTrans == null)
                {
                    var canvasComp = __instance.GetComponentInChildren<Canvas>(true);
                    if (canvasComp != null)
                        canvasTrans = canvasComp.transform;
                }

                if (canvasTrans == null)
                {
                    Logging.Warn("[UILevelFailed] Canvas not found");
                    return;
                }

                // Make canvas fill the screen
                var canvasRt = canvasTrans.GetComponent<RectTransform>();
                if (canvasRt != null)
                {
                    canvasRt.anchorMin = new Vector2(0f, 0f);
                    canvasRt.anchorMax = new Vector2(1f, 1f);
                    canvasRt.offsetMin = Vector2.zero;
                    canvasRt.offsetMax = Vector2.zero;
                }

                // Stretch Scale Anchor horizontally (if present)
                Transform scaleAnchor = canvasTrans.Find("Scale Anchor") ?? RecursiveFindChild(canvasTrans, "Scale Anchor");
                if (scaleAnchor != null)
                {
                    var scaleRt = scaleAnchor.GetComponent<RectTransform>();
                    if (scaleRt != null)
                    {
                        StretchRectTransformHorizontal(scaleRt);
                        Logging.Info("[UILevelFailed] Stretched Scale Anchor to full width");
                    }
                }

                // Find "UI - Level Name Border"
                Transform levelNameBorder = RecursiveFindChild(canvasTrans, "UI - Level Name Border");
                if (levelNameBorder != null)
                {
                    // Stretch Level Name Border to full width
                    var borderRt = levelNameBorder.GetComponent<RectTransform>();
                    if (borderRt != null)
                    {
                        borderRt.anchorMin = new Vector2(0f, borderRt.anchorMin.y);
                        borderRt.anchorMax = new Vector2(1f, borderRt.anchorMax.y);
                        borderRt.offsetMin = new Vector2(0f, borderRt.offsetMin.y);
                        borderRt.offsetMax = new Vector2(0f, borderRt.offsetMax.y);
                        borderRt.sizeDelta = new Vector2(0f, borderRt.sizeDelta.y);
                        Logging.Info("[UILevelFailed] Expanded UI - Level Name Border to full width");
                    }

                    // Find Stretch Anchor under Level Name Border
                    Transform stretchAnchor = levelNameBorder.Find("Stretch Anchor") ?? RecursiveFindChild(levelNameBorder, "Stretch Anchor");
                    if (stretchAnchor != null)
                    {
                        // Disable Left Side and Right Side (side panels)
                        var left = stretchAnchor.Find("Left Side") ?? RecursiveFindChild(stretchAnchor, "Left Side");
                        var right = stretchAnchor.Find("Right Side") ?? RecursiveFindChild(stretchAnchor, "Right Side");
                        DisableGameObjectPanels(left != null ? left.gameObject : null, right != null ? right.gameObject : null);

                        // Stretch Stretch Anchor to full width
                        var stretchRt = stretchAnchor.GetComponent<RectTransform>();
                        if (stretchRt != null)
                        {
                            stretchRt.anchorMin = new Vector2(0f, stretchRt.anchorMin.y);
                            stretchRt.anchorMax = new Vector2(1f, stretchRt.anchorMax.y);
                            stretchRt.offsetMin = new Vector2(0f, stretchRt.offsetMin.y);
                            stretchRt.offsetMax = new Vector2(0f, stretchRt.offsetMax.y);
                            stretchRt.sizeDelta = new Vector2(0f, stretchRt.sizeDelta.y);
                            Logging.Info("[UILevelFailed] Expanded Stretch Anchor to full width");
                        }
                    }
                    else
                    {
                        Logging.Warn("[UILevelFailed] Stretch Anchor not found under Level Name Border");
                    }
                }
                else
                {
                    Logging.Warn("[UILevelFailed] UI - Level Name Border not found");
                }

                Logging.Info("[UILevelFailed] WideScreen layout adjustments applied");
            }
            catch (Exception e)
            {
                Logging.Error($"[UILevelFailed] Error in WideScreenLevelFailedPatch: {e}");
            }
        }
    }
}
