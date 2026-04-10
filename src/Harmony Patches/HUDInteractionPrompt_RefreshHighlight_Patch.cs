using System;
using System.Reflection;
using HarmonyLib;
using TMPro;
using IAmYourTranslator.json;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.Harmony_Patches
{
    // Translates HUD interaction prompts and applies the global TMP font.
    [HarmonyPatch(typeof(HUDInteractionPrompt), "RefreshHighlight")]
    public static class HUDInteractionPrompt_RefreshHighlight_Patch
    {
        private static FieldInfo promptTextField;
        private static MethodInfo getPromptDescriptionMethod;

        [HarmonyPostfix]
        public static void Postfix(HUDInteractionPrompt __instance, PlayerInteractable interactable)
        {
            try
            {
                if (__instance == null || interactable == null)
                    return;

                // Get promptText field via cached reflection
                if (promptTextField == null)
                    promptTextField = AccessTools.Field(typeof(HUDInteractionPrompt), "promptText");
                
                var tmp = promptTextField?.GetValue(__instance) as TMP_Text;
                if (tmp == null)
                    return;

                // Get original description from interactable
                string originalText = GetOriginalPromptDescription(interactable);
                if (string.IsNullOrEmpty(originalText))
                {
                    // Fallback to current text
                    originalText = tmp.text;
                    if (string.IsNullOrEmpty(originalText))
                        return;
                }

                if (LanguageManager.IsLoaded)
                {
                    var dict = LanguageManager.CurrentLanguage.interactionPrompts;
                    if (dict == null)
                        dict = LanguageManager.CurrentLanguage.interactionPrompts = new System.Collections.Generic.Dictionary<string, string>();

                    // This prompt TMP is reused for different interactions; reset cache each refresh.
                    ClearOriginalTextCache(tmp);
                    
                    // Handle weapon placeholders if present
                    if (originalText.Contains("[WEAPON]"))
                    {
                        // Replace weapon placeholder with translated weapon name if possible
                        // For now, just translate the base text
                        TranslateTextAndSaveIfMissing(tmp, originalText, dict, "[HUDInteractionPrompt]");
                    }
                    else
                    {
                        TranslateTextAndSaveIfMissing(tmp, originalText, dict, "[HUDInteractionPrompt]");
                    }
                }

                // Apply global font
                var font = TMPFontReplacer.GetCachedFont(Plugin.GlobalFontPath);
                if (font != null)
                    TMPFontReplacer.ApplyFontToTMP(tmp, font);
            }
            catch (Exception e)
            {
                Logging.Error($"[HUDInteractionPrompt] Error in RefreshHighlight postfix: {e}");
            }
        }

        private static string GetOriginalPromptDescription(PlayerInteractable interactable)
        {
            try
            {
                // Use reflection to call GetPromptDescription method
                if (getPromptDescriptionMethod == null)
                    getPromptDescriptionMethod = AccessTools.Method(interactable.GetType(), "GetPromptDescription");
                
                if (getPromptDescriptionMethod != null)
                {
                    var result = getPromptDescriptionMethod.Invoke(interactable, null) as string;
                    if (!string.IsNullOrEmpty(result))
                        return result;
                }
            }
            catch (Exception e)
            {
                Logging.Warn($"[HUDInteractionPrompt] Failed to get original prompt description: {e.Message}");
            }
            
            return null;
        }
    }
}
