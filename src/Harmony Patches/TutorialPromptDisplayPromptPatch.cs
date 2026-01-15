using System;
using HarmonyLib;
using UnityEngine;
using TMPro;
using IAmYourTranslator.json;
using BepInEx;
using System.IO;
using static IAmYourTranslator.CommonFunctions;
using Fleece; // Import Fleece for access to Jumper


namespace IAmYourTranslator.HarmonyPatches
{
    [HarmonyPatch(typeof(TutorialPromptStorer), "DisplayPrompt")]
    public static class TutorialPromptPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(TutorialPromptStorer __instance)
        {
            try
            {
                if (__instance == null)
                    return true;

                // Get the private field 'prompt'
                var promptField = AccessTools.Field(typeof(TutorialPromptStorer), "prompt");
                var jumper = promptField?.GetValue(__instance) as Fleece.Jumper;
                if (jumper == null)
                {
                    Logging.Warn("[TutorialPrompt] prompt == null");
                    return true;
                }

                // Get the text
                string originalText = jumper.passage?.parsedText;
                if (string.IsNullOrEmpty(originalText))
                {
                    Logging.Warn("[TutorialPrompt] parsedText == null or empty");
                    return true;
                }

                Logging.Info($"[TutorialPrompt] Original text: {originalText}");

                // Get inputName
                var inputNameField = AccessTools.Field(typeof(TutorialPromptStorer), "inputName");
                string inputName = inputNameField?.GetValue(__instance) as string;
                string inputKeyNameForAction = GameManager.instance.inputManager.GetInputKeyNameForAction(inputName, false);

                float size = 100f;
                if (GameManager.instance.inputManager.ShowGamepadPrompts() &&
                    GameManager.instance.saveManager.CurrentSettings.resolutionHeight < 1080)
                {
                    size = 150f;
                }

                // Get the translation
                string translatedText;
                bool keyExisted = LanguageManager.CurrentLanguage.tutorialPrompts.TryGetValue(originalText, out translatedText);
                if (!keyExisted)
                {
                    LanguageManager.CurrentLanguage.tutorialPrompts[originalText] = originalText;
                    translatedText = originalText;
                    LanguageManager.SaveCurrentLanguage();
                }

                translatedText = translatedText.Replace("[KEY]", $"<size={size}%>{inputKeyNameForAction}<size=100%>");

                // === New block: font replacement ===
                var promptHUD = GameManager.instance.player.GetHUD().GetTutorialPrompt();
                if (promptHUD == null)
                {
                    Logging.Warn("[TutorialPrompt] HUD prompt object not found!");
                    return true;
                }

                // Get TMP component on HUD
                var tmp = promptHUD.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp != null)
                {
                        var tmpFont = TMPFontReplacer.GetCachedFont(Plugin.GlobalFontPath);
                    if (tmpFont != null)
                    {
                        TMPFontReplacer.ApplyFontToTMP(tmp, tmpFont);
                    }
                    else
                    {
                        Logging.Warn("[TutorialPrompt] Failed to load custom font!");
                    }
                }
                else
                {
                    Logging.Warn("[TutorialPrompt] TMP component not found!");
                }

                // Display the translated text
                promptHUD.DisplayPrompt(translatedText);

                return false;
            }
            catch (Exception e)
            {
                Logging.Error($"[TutorialPrompt] Error in patch: {e}");
                return true;
            }
        }
    }
}
