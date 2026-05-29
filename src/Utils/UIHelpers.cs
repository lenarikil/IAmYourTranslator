using System;
using UnityEngine;
using TMPro;
using IAmYourTranslator.Fonts;

namespace IAmYourTranslator.Utils
{
    public static class UIHelpers
    {
        public static void ApplyFontToAllChildrenTMP(Component target, TMP_FontAsset font, string logPrefix = "")
        {
            if (target == null || font == null)
            {
                if (font == null)
                    Logging.Warn($"{logPrefix} Font is null, cannot apply");
                return;
            }

            try
            {
                var allTMPs = target.GetComponentsInChildren<TMP_Text>(true);
                if (allTMPs == null || allTMPs.Length == 0)
                    return;

                int appliedCount = 0;
                foreach (var tmp in allTMPs)
                {
                    if (tmp != null && tmp.font != font)
                    {
                        FontManager.ApplyFontToTMP(tmp, font);
                        appliedCount++;
                    }
                }

                Logging.Info($"{logPrefix} Applied global font to {appliedCount} TMP_Text children");
            }
            catch (Exception e)
            {
                Logging.Error($"{logPrefix} Error in ApplyFontToAllChildrenTMP: {e}");
            }
        }

        public static void DisableGameObjectPanels(params GameObject[] panels)
        {
            if (panels == null || panels.Length == 0)
                return;

            foreach (var panel in panels)
            {
                if (panel != null)
                {
                    panel.SetActive(false);
                    Logging.Info($"[UIHelpers] Disabled panel: {panel.name}");
                }
            }
        }

        public static void StretchRectTransformHorizontal(RectTransform rt)
        {
            if (rt == null)
                return;

            try
            {
                rt.anchorMin = new Vector2(0f, rt.anchorMin.y);
                rt.anchorMax = new Vector2(1f, rt.anchorMax.y);
                rt.offsetMin = new Vector2(0f, rt.offsetMin.y);
                rt.offsetMax = new Vector2(0f, rt.offsetMax.y);
                rt.sizeDelta = new Vector2(0f, rt.sizeDelta.y);
                Logging.Info($"[UIHelpers] Stretched RectTransform horizontally: {rt.name}");
            }
            catch (Exception e)
            {
                Logging.Error($"[UIHelpers] Error stretching RectTransform: {e}");
            }
        }

        public static T FindComponentWithFallback<T>(Component startComponent, params string[] fallbackPaths) where T : Component
        {
            if (startComponent == null || fallbackPaths == null || fallbackPaths.Length == 0)
                return null;

            foreach (var path in fallbackPaths)
            {
                if (string.IsNullOrEmpty(path))
                    continue;

                try
                {
                    Transform found = SceneHelpers.RecursiveFindChild(startComponent.transform, path);
                    if (found != null)
                    {
                        var component = found.GetComponent<T>();
                        if (component != null)
                        {
                            Logging.Info($"[UIHelpers] Found {typeof(T).Name} at path: {path}");
                            return component;
                        }
                    }
                }
                catch (Exception e)
                {
                    Logging.Warn($"[UIHelpers] Error searching path '{path}': {e.Message}");
                }
            }

            Logging.Warn($"[UIHelpers] Could not find {typeof(T).Name} in any fallback paths");
            return null;
        }
    }
}
