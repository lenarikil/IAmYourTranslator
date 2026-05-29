using System.IO;
using UnityEngine;
using IAmYourTranslator.json;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.Utils
{
    public static class UILayoutHelpers
    {
        /// <summary>
        /// Applies custom logo texture to a "Logo" child under the given parent.
        /// </summary>
        public static void TryApplyLogoTexture(Transform parent, bool invertAlpha = false)
        {
            if (parent == null) return;

            Transform logo = RecursiveFindChild(parent, "Logo");
            if (logo == null) return;
            if (!Plugin.EnableTextureReplacementEntry.Value || LanguageManager.CurrentSummary == null) return;

            string logoFile = Path.Combine(LanguageManager.CurrentSummary.Paths.TexturesDir, "UILogoText.png");
            if (File.Exists(logoFile))
                UITextureReplacer.ApplyTo(logo.gameObject, logoFile, invertAlpha);
        }

        /// <summary>
        /// Stretches a RectTransform horizontally to full screen width while preserving Y-axis values.
        /// Delegates to CommonFunctions.StretchRectTransformHorizontal.
        /// </summary>
        public static void StretchHorizontal(RectTransform rt)
        {
            StretchRectTransformHorizontal(rt);
        }

        /// <summary>
        /// Anchors a RectTransform to the left edge (anchorMin/Max X = 0), preserving vertical offsets.
        /// </summary>
        public static void AnchorToLeft(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(0f, rt.anchorMin.y);
            rt.anchorMax = new Vector2(0f, rt.anchorMax.y);
            rt.pivot = new Vector2(0f, rt.pivot.y);
        }

        /// <summary>
        /// Anchors a RectTransform to the right edge (anchorMin/Max X = 1), preserving vertical offsets.
        /// </summary>
        public static void AnchorToRight(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(1f, rt.anchorMin.y);
            rt.anchorMax = new Vector2(1f, rt.anchorMax.y);
            rt.pivot = new Vector2(1f, rt.pivot.y);
        }
    }
}
