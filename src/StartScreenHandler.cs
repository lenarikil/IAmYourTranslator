using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using static IAmYourTranslator.CommonFunctions;
using System.IO;
using BepInEx;
using IAmYourTranslator.json;

namespace IAmYourTranslator
{
    public class StartScreenHandler
    {
        public static void HandleStartScreen(MonoBehaviour coroutineHost = null)
        {
            Logging.Info("Handling Start Screen scene");

            // Apply font first
            var font = Plugin.GlobalTMPFont ?? CommonFunctions.TMPFontReplacer.GetCachedFont(Plugin.GlobalFontPath);
            if (font != null)
            {
                CommonFunctions.TMPFontReplacer.ApplyFontToAllTMP(font);
                Logging.Info("[StartScreenHandler] Applied global font to all TMP text");
            }
            else
            {
                Logging.Warn("[StartScreenHandler] GlobalTMPFont is null, restoring original fonts");
                CommonFunctions.TMPFontReplacer.RestoreOriginalFonts();
            }

            // Apply textures with delay to ensure UI is ready
            var host = coroutineHost ?? Plugin.GetOrRecoverInstance();
            if (host != null)
            {
                host.StartCoroutine(ApplyTitleImageDelayed());
                Logging.Info("[StartScreenHandler] Started ApplyTitleImageDelayed coroutine");
            }
            else
            {
                // Fallback: apply immediately without coroutine
                Logging.Warn("[StartScreenHandler] Coroutine host is null, applying texture immediately");
                ApplyTitleImageImmediate();
            }

            Logging.Info("Start Screen handling completed");
        }

        private static void ApplyTitleImageImmediate()
        {
            string TextureFile = null;
            if (LanguageManager.CurrentSummary != null && Plugin.EnableTextureReplacementEntry.Value)
            {
                TextureFile = Path.Combine(LanguageManager.CurrentSummary.Paths.TexturesDir, "UILogoText.png");
                Logging.Info($"[StartScreenHandler] Looking for texture at: {TextureFile}");
                Logging.Info($"[StartScreenHandler] Texture file exists: {File.Exists(TextureFile)}");
            }
            else
            {
                Logging.Info($"[StartScreenHandler] Texture replacement disabled or no language loaded");
            }

            GameObject TitleImage = GameObject.Find("Start Screen UI/Title Anchor/Title Image");
            if (TitleImage == null)
            {
                Logging.Warn("[StartScreenHandler] TitleImage not found!");
                return;
            }

            UITextureReplacer.ApplyTo(TitleImage, TextureFile);
            Logging.Info($"[StartScreenHandler] Applied texture immediately");
        }

        private static IEnumerator ApplyTitleImageDelayed()
        {
            yield return new WaitForSecondsRealtime(0.5f);

            string TextureFile = null;
            if (LanguageManager.CurrentSummary != null && Plugin.EnableTextureReplacementEntry.Value)
            {
                TextureFile = Path.Combine(LanguageManager.CurrentSummary.Paths.TexturesDir, "UILogoText.png");
                Logging.Info($"[StartScreenHandler] Looking for texture at: {TextureFile}");
                Logging.Info($"[StartScreenHandler] Texture file exists: {File.Exists(TextureFile)}");
            }
            else
            {
                Logging.Info($"[StartScreenHandler] Texture replacement disabled or no language loaded. CurrentSummary={LanguageManager.CurrentSummary != null}, EnableTextureReplacement={Plugin.EnableTextureReplacementEntry?.Value}");
            }

            GameObject TitleImage = GameObject.Find("Start Screen UI/Title Anchor/Title Image");
            if (TitleImage == null)
            {
                Logging.Warn("[StartScreenHandler] TitleImage not found! Trying alternative search...");
                // Try to find by recursive search
                var roots = SceneManager.GetActiveScene().GetRootGameObjects();
                foreach (var root in roots)
                {
                    var found = RecursiveFindChild(root.transform, "Title Image");
                    if (found != null)
                    {
                        TitleImage = found.gameObject;
                        Logging.Info($"[StartScreenHandler] Found TitleImage via recursive search: {found.name}");
                        break;
                    }
                }
            }

            if (TitleImage != null)
            {
                UITextureReplacer.ApplyTo(TitleImage, TextureFile);
                Logging.Info($"[StartScreenHandler] Applied texture to TitleImage (textureFile={(string.IsNullOrEmpty(TextureFile) ? "null (restore original)" : TextureFile)})");
            }
            else
            {
                Logging.Warn("[StartScreenHandler] TitleImage still not found after recursive search");
            }
        }

        private static Transform RecursiveFindChild(Transform parent, string childName)
        {
            if (parent == null) return null;
            
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                    return child;

                Transform result = RecursiveFindChild(child, childName);
                if (result != null)
                    return result;
            }
            return null;
        }

        public static void PatchMainMenuButtons()
        {
            try
            {
                Logging.Info("Patching main menu buttons");

                // Find the button by name
                GameObject buttonStart = GameObject.Find("Button Start");
                
                if (buttonStart != null)
                {
                    Logging.Info("Found Button Start object");

                    // Disable the FleeceTextSetter script
                    FleeceTextSetter textSetter = buttonStart.GetComponent<FleeceTextSetter>();
                    if (textSetter != null)
                    {
                        Logging.Info("Disabling FleeceTextSetter component");
                        textSetter.enabled = false; // Disable the script
                    }
                    else
                    {
                        Logging.Warn("FleeceTextSetter component not found");
                    }

                    // Now change the text directly
                    TextMeshProUGUI tmpText = buttonStart.GetComponentInChildren<TextMeshProUGUI>();
                    if (tmpText != null)
                    {
                        tmpText.text = "Start Game";
                        Logging.Info("Text changed successfully");
                    }
                    else
                    {
                        Logging.Warn("TextMeshProUGUI component not found");
                    }
                }
                else
                {
                    Logging.Warn("Button Start object not found");
                }
            }
            catch (System.Exception e)
            {
                Logging.Error($"Error in PatchMainMenuButtons: {e.Message}");
            }
        }
    }
}
