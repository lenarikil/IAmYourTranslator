using HarmonyLib;
using TMPro;
using System.Collections.Generic;
using static IAmYourTranslator.CommonFunctions;
using IAmYourTranslator.json;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(UILevelSelectButton), "RefreshInformation")]
    public static class UILevelSelectButton_FontPatch
    {
    // fallback path is taken from Plugin.GlobalFontPath when needed
        private static readonly System.Reflection.FieldInfo nameTextField = AccessTools.Field(typeof(UILevelSelectButton), "nameText");


        [HarmonyPostfix]
        public static void Postfix(UILevelSelectButton __instance)
        {
            try
            {
                if (__instance == null)
                    return;

                // Get the TMP component via cached FieldInfo
                TMP_Text nameText = nameTextField?.GetValue(__instance) as TMP_Text;
                if (nameText == null)
                    return;

                Dictionary<string, string> levelNames = null;
                if (LanguageManager.IsLoaded)
                {
                    levelNames = LanguageManager.CurrentLanguage.levelNames;
                    if (levelNames == null)
                        levelNames = LanguageManager.CurrentLanguage.levelNames = new Dictionary<string, string>();
                }

                string originalName = ResolveOriginalTranslationKey(nameText.text, levelNames);
                TranslateTextAndSaveIfMissing(nameText, originalName, levelNames, "[UILevelSelectButton]");

                // If global font is set — use it directly (fast path)
                if (Plugin.GlobalTMPFont != null)
                {
                    if (nameText.font == Plugin.GlobalTMPFont)
                        return;

                    TMPFontReplacer.ApplyFontToTMP(nameText, Plugin.GlobalTMPFont);
                    Logging.Info($"[UILevelSelectButton] Applied global font '{Plugin.GlobalTMPFont.name}' to '{nameText.name}'.");
                    return;
                }

                // Use centralized font cache (Plugin.GlobalTMPFont or Plugin.GlobalFontPath)
                var font = TMPFontReplacer.GetCachedFont(Plugin.GlobalFontPath);
                if (font == null)
                    return;

                if (nameText.font == font)
                    return;

                TMPFontReplacer.ApplyFontToTMP(nameText, font);
                Logging.Info($"[UILevelSelectButton] Applied cached font '{font.name}' to '{nameText.name}'.");
            }
            catch (System.Exception ex)
            {
                Logging.Warn($"[UILevelSelectButton_FontPatch] Font replace failed: {ex.Message}");
            }
        }
    }
}
