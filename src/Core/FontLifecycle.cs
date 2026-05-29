using System;
using System.IO;
using TMPro;
using BepInEx;
using IAmYourTranslator.json;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.Core
{
    internal static class FontLifecycle
    {
        public static void LoadGlobalFontFallback()
        {
            try
            {
                string fontsDir = Path.Combine(Paths.ConfigPath, "IAmYourTranslator", "fonts");
                string fontFile = Path.Combine(fontsDir, "Jovanny Lemonad - Bender-Bold.otf");
                Plugin.GlobalFontPath = fontFile;
                Plugin.GlobalTMPFont = TMPFontReplacer.LoadFontFromFile(fontFile);
                if (Plugin.GlobalTMPFont != null)
                    Logging.Warn($"Loaded global TMP font: {Plugin.GlobalTMPFont.name}");
                else
                    Logging.Warn("Global TMP font not found or failed to load.");
            }
            catch (Exception ex)
            {
                Logging.Warn($"Failed to load global TMP font: {ex.Message}");
            }
        }

        public static bool TryApplyLanguageFont()
        {
            var meta = LanguageManager.CurrentMetadata;
            var summary = LanguageManager.CurrentSummary;
            if (meta == null || summary?.Paths == null)
                return false;

            if (string.IsNullOrEmpty(meta.fontFile))
                return false;

            string fontPath = Path.Combine(summary.Paths.FontsDir, meta.fontFile);
            try
            {
                var font = TMPFontReplacer.LoadFontFromFile(fontPath);
                if (font != null)
                {
                    Plugin.GlobalTMPFont = font;
                    Plugin.GlobalFontPath = fontPath;
                    TMPFontReplacer.ApplyFontToAllTMP(font);
                    Logging.Info($"Applied language font: {font.name}");
                    return true;
                }
                else
                {
                    ToastNotifier.Show("Problem loading font. See BepInEx log.", 5f);
                    Logging.Warn($"Language font file not found or failed to load: {fontPath}");
                    return false;
                }
            }
            catch (Exception e)
            {
                ToastNotifier.Show("Problem loading font. See BepInEx log.", 5f);
                Logging.Error($"Error loading language font '{fontPath}': {e}");
                return false;
            }
        }

        public static void ApplyFontImmediateWithFallback()
        {
            try
            {
                if (!LanguageManager.IsLoaded || LanguageManager.CurrentSummary == null)
                {
                    TMPFontReplacer.RestoreOriginalFonts();
                    return;
                }

                bool ok = TryApplyLanguageFont();
                if (!ok)
                {
                    LoadGlobalFontFallback();
                    if (Plugin.GlobalTMPFont != null)
                        TMPFontReplacer.ReplaceFont(Plugin.GlobalFontPath);
                    else
                        Logging.Warn("No fallback font available to apply.");
                }
            }
            catch (Exception e)
            {
                Logging.Warn($"Failed to apply font immediately: {e.Message}");
            }
        }
    }
}
