using System;
using System.IO;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IAmYourTranslator.json;
using static IAmYourTranslator.CommonFunctions;
using BepInEx;

namespace IAmYourTranslator.Harmony_Patches
{
    /// <summary>
    /// Patches UILevelCompleteOverviewDetails to translate overview headers and apply custom resources.
    /// Translates textHeaderTotal, textHeaderExtra (multi-line), replaces logo texture, and applies global font.
    /// </summary>
    [HarmonyPatch(typeof(UILevelCompleteOverviewDetails), "Start")]
    public static class UILevelCompleteOverviewDetails_Patch
    {
        // Track added keys to avoid saving repeatedly
        private static readonly System.Collections.Generic.HashSet<string> _addedOverviewDetailsKeys = new System.Collections.Generic.HashSet<string>();

        [HarmonyPostfix]
        public static void StartPostfix(UILevelCompleteOverviewDetails __instance)
        {
            try
            {
                if (__instance == null)
                    return;
                if (!LanguageManager.IsLoaded || LanguageManager.CurrentLanguage == null)
                    return;

                var dict = LanguageManager.CurrentLanguage.overviewScreen;
                if (dict == null)
                    dict = LanguageManager.CurrentLanguage.overviewScreen = new System.Collections.Generic.Dictionary<string, string>();

                // Translate textHeaderTotal
                var textHeaderTotal = Traverse.Create(__instance).Field("textHeaderTotal").GetValue<TMP_Text>();
                if (textHeaderTotal != null && !string.IsNullOrEmpty(textHeaderTotal.text))
                {
                    TranslateAndSaveIfMissingWithCache(textHeaderTotal, textHeaderTotal.text, dict, "[UILevelCompleteOverviewDetails.Header]");
                    
                    // Apply font
                    var tmpFont = Plugin.GlobalTMPFont;
                    if (tmpFont != null)
                        TMPFontReplacer.ApplyFontToTMP(textHeaderTotal, tmpFont);
                }

                // Translate textHeaderExtra (contains "Base\nReclaimed", need to translate both parts)
                var textHeaderExtra = Traverse.Create(__instance).Field("textHeaderExtra").GetValue<TMP_Text>();
                if (textHeaderExtra != null && !string.IsNullOrEmpty(textHeaderExtra.text))
                {
                    string fullText = textHeaderExtra.text;
                    string[] parts = fullText.Split('\n');
                    string[] translatedParts = new string[parts.Length];

                    for (int i = 0; i < parts.Length; i++)
                    {
                        string part = parts[i].Trim();
                        if (string.IsNullOrEmpty(part))
                        {
                            translatedParts[i] = parts[i];
                            continue;
                        }

                        // Translate individual parts
                        if (dict.TryGetValue(part, out var trans) && !string.IsNullOrEmpty(trans) && trans != part)
                        {
                            translatedParts[i] = trans;
                        }
                        else if (!dict.ContainsKey(part))
                        {
                            dict[part] = part;
                            lock (_addedOverviewDetailsKeys)
                            {
                                if (!_addedOverviewDetailsKeys.Contains(part))
                                {
                                    _addedOverviewDetailsKeys.Add(part);
                                    LanguageManager.SaveCurrentLanguage();
                                }
                            }
                            translatedParts[i] = part;
                        }
                        else
                        {
                            translatedParts[i] = part;
                        }
                    }

                    textHeaderExtra.text = string.Join("\n", translatedParts);
                    
                    // Apply font
                    var tmpFont = Plugin.GlobalTMPFont;
                    if (tmpFont != null)
                        TMPFontReplacer.ApplyFontToTMP(textHeaderExtra, tmpFont);
                }

                // Apply custom logo texture
                Transform logoTransform = __instance.transform.Find("logo");
                if (logoTransform != null)
                {
                    GameObject logoObj = logoTransform.gameObject;
                    if (Plugin.EnableTextureReplacementEntry.Value && LanguageManager.CurrentSummary != null)
                    {
                        string logoFile = Path.Combine(LanguageManager.CurrentSummary.Paths.TexturesDir, "UILogoText.png");
                        UITextureReplacer.ApplyTo(logoObj, logoFile, false);
                        Logging.Info($"[UILevelCompleteOverviewDetails] Applied logo texture (exists={File.Exists(logoFile)})");
                    }
                    else
                    {
                        Logging.Info("[UILevelCompleteOverviewDetails] Texture replacement disabled or no language loaded");
                    }
                }
                else
                {
                    Logging.Warn("[UILevelCompleteOverviewDetails] Logo object not found");
                }

                // Apply global font to ALL TMP_Text children (including kill types list)
                // Uses centralized helper from CommonFunctions
                ApplyFontToAllChildrenTMP(__instance, Plugin.GlobalTMPFont, "[UILevelCompleteOverviewDetails]");
            }
            catch (Exception e)
            {
                Logging.Warn($"[UILevelCompleteOverviewDetails] StartPostfix error: {e}");
            }
        }

        /// <summary>
        /// Translates text and saves to dictionary with cache to avoid duplicate saves.
        /// </summary>
        private static void TranslateAndSaveIfMissingWithCache(TMP_Text tmpComponent, string originalText, System.Collections.Generic.Dictionary<string, string> dict, string logPrefix)
        {
            if (string.IsNullOrEmpty(originalText) || dict == null)
                return;

            if (dict.TryGetValue(originalText, out var trans) && !string.IsNullOrEmpty(trans) && trans != originalText)
            {
                tmpComponent.text = trans;
                Logging.Info($"{logPrefix} Translated: '{originalText}' -> '{trans}'");
                return;
            }

            // Add original as placeholder if missing
            if (!dict.ContainsKey(originalText))
            {
                dict[originalText] = originalText;
                lock (_addedOverviewDetailsKeys)
                {
                    if (!_addedOverviewDetailsKeys.Contains(originalText))
                    {
                        _addedOverviewDetailsKeys.Add(originalText);
                        LanguageManager.SaveCurrentLanguage();
                    }
                }
            }
        }
    }
}
