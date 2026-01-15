using System;
using System.IO;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IAmYourTranslator.json;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(UILevelCompleteOverviewDetails), "Start")]
    public static class UILevelCompleteOverviewDetails_Patch
    {
        // track added keys to avoid saving repeatedly
        private static readonly System.Collections.Generic.HashSet<string> _addedOverviewDetailsKeys = new System.Collections.Generic.HashSet<string>();

        [HarmonyPostfix]
        public static void StartPostfix(UILevelCompleteOverviewDetails __instance)
        {
            try
            {
                if (__instance == null) return;

                var dict = LanguageManager.CurrentLanguage.overviewScreen;
                if (dict == null) dict = LanguageManager.CurrentLanguage.overviewScreen = new System.Collections.Generic.Dictionary<string, string>();

                // Get textHeaderTotal
                var textHeaderTotal = Traverse.Create(__instance).Field("textHeaderTotal").GetValue<TMP_Text>();
                if (textHeaderTotal != null && !string.IsNullOrEmpty(textHeaderTotal.text))
                {
                    string originalText = textHeaderTotal.text;
                    if (dict.TryGetValue(originalText, out var trans) && !string.IsNullOrEmpty(trans) && trans != originalText)
                    {
                        textHeaderTotal.text = trans;
                    }
                    else if (!dict.ContainsKey(originalText))
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
                    // Apply font
                    var tmpFont = Plugin.GlobalTMPFont;
                    if (tmpFont != null)
                        TMPFontReplacer.ApplyFontToTMP(textHeaderTotal, tmpFont);
                }

                // Get textHeaderExtra (contains "Base\nReclaimed", need to translate both parts)
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
                    string texturesDir = Path.Combine(BepInEx.Paths.ConfigPath, "IAmYourTranslator", "textures");
                    string logoFile = Path.Combine(texturesDir, "UILogoText.png");
                    UITextureReplacer.ApplyTo(logoObj, logoFile, false);
                }
                else
                {
                    Logging.Warn("[UILevelCompleteOverviewDetails] Logo object not found");
                }
            }
            catch (Exception e)
            {
                Logging.Warn($"[UILevelCompleteOverviewDetails] StartPostfix error: {e}");
            }
        }
    }
}
