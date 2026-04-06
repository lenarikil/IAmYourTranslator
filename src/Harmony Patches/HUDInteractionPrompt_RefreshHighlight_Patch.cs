using System;
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
        [HarmonyPostfix]
        public static void Postfix(HUDInteractionPrompt __instance, PlayerInteractable interactable)
        {
            try
            {
                if (__instance == null || interactable == null)
                    return;

                var field = AccessTools.Field(typeof(HUDInteractionPrompt), "promptText");
                var tmp = field?.GetValue(__instance) as TMP_Text;
                if (tmp == null || string.IsNullOrEmpty(tmp.text))
                    return;

                if (LanguageManager.IsLoaded)
                {
                    var dict = LanguageManager.CurrentLanguage.interactionPrompts;
                    if (dict == null)
                        dict = LanguageManager.CurrentLanguage.interactionPrompts = new System.Collections.Generic.Dictionary<string, string>();

                    string source = tmp.text;
                    // This prompt TMP is reused for different interactions; reset cache each refresh.
                    ClearOriginalTextCache(tmp);
                    TranslateTextAndSaveIfMissing(tmp, source, dict, "[HUDInteractionPrompt]");
                }

                var font = TMPFontReplacer.GetCachedFont(Plugin.GlobalFontPath);
                if (font != null)
                    TMPFontReplacer.ApplyFontToTMP(tmp, font);
            }
            catch (Exception e)
            {
                Logging.Error($"[HUDInteractionPrompt] Error in RefreshHighlight postfix: {e}");
            }
        }
    }
}
