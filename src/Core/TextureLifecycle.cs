using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using IAmYourTranslator.json;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.Core
{
    internal static class TextureLifecycle
    {
        private static readonly (string path, bool invertAlpha)[] LogoPaths = new[]
        {
            ("Canvas/Logo", false),
            ("Canvas/Corner Anchor/Logo", false),
            ("Canvas/UI - Level Name Border/Logo", false),
            ("[LEVEL DEPENDENCIES]/Credit Anchor/Credits UI/Fade In/Logo", true)
        };

        public static void RefreshTexturesInCurrentScene()
        {
            try
            {
                foreach (var (path, invertAlpha) in LogoPaths)
                {
                    TryApplyTextureTo(path, invertAlpha);
                }

                TryApplyTextureToCommonLogoNodes();

                if (!LanguageManager.IsLoaded || !IsTextureReplacementEnabled())
                    UITextureReplacer.RestoreAll();
            }
            catch (Exception e)
            {
                Logging.Warn($"[Plugin] RefreshTexturesInCurrentScene error: {e.Message}");
            }
        }

        private static void TryApplyTextureToCommonLogoNodes()
        {
            try
            {
                var roots = SceneManager.GetActiveScene().GetRootGameObjects();
                if (roots == null || roots.Length == 0)
                    return;

                bool isEndCreditsScene = string.Equals(GetCurrentSceneName(), "#027_Special_EndCredits", StringComparison.Ordinal);

                // Optimization: Search for Image components instead of all Transforms (much faster on large hierarchies)
                // This reduces search from 5000+ Transforms to ~100-200 Image components
                foreach (var root in roots)
                {
                    if (root == null)
                        continue;

                    // Search for Image/RawImage components first - these are what hold textures
                    var images = root.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                    foreach (var img in images)
                    {
                        if (img == null)
                            continue;

                        string name = img.gameObject.name;
                        if (string.Equals(name, "Logo", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(name, "Title Image", StringComparison.OrdinalIgnoreCase))
                        {
                            bool invertAlpha = isEndCreditsScene && IsEndCreditsFadeInLogoNode(img.transform);
                            TryApplyTextureToByTarget(img.gameObject, invertAlpha);
                        }
                    }

                    // Also check RawImage components
                    var rawImages = root.GetComponentsInChildren<UnityEngine.UI.RawImage>(true);
                    foreach (var rawImg in rawImages)
                    {
                        if (rawImg == null)
                            continue;

                        string name = rawImg.gameObject.name;
                        if (string.Equals(name, "Logo", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(name, "Title Image", StringComparison.OrdinalIgnoreCase))
                        {
                            bool invertAlpha = isEndCreditsScene && IsEndCreditsFadeInLogoNode(rawImg.transform);
                            TryApplyTextureToByTarget(rawImg.gameObject, invertAlpha);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logging.Warn($"Failed to refresh common logo nodes: {e.Message}");
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

        private static void TryApplyTextureToByTarget(GameObject target, bool invertAlpha)
        {
            if (target == null)
                return;

            if (LanguageManager.CurrentSummary == null || !IsTextureReplacementEnabled())
            {
                UITextureReplacer.ApplyTo(target, null, invertAlpha);
                return;
            }

            string textureFile = Path.Combine(LanguageManager.CurrentSummary.Paths.TexturesDir, "UILogoText.png");
            UITextureReplacer.ApplyTo(target, File.Exists(textureFile) ? textureFile : null, invertAlpha);
        }

        private static bool IsTextureReplacementEnabled()
        {
            return Plugin.EnableTextureReplacementEntry != null && Plugin.EnableTextureReplacementEntry.Value;
        }

        private static void TryApplyTextureTo(string objectPath, bool invertAlpha = false)
        {
            GameObject target = GetObject(objectPath);
            if (target == null)
                return;

            TryApplyTextureToByTarget(target, invertAlpha);
        }
    }
}
