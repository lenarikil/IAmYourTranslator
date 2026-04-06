using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using IAmYourTranslator.json;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(UILevelSelectCategorySlide), "RefreshValues")]
    public static class UILevelSelectCategorySlide_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(UILevelSelectCategorySlide __instance, LevelCollection collection, UILevelSelectionRoot root)
        {
            try
            {
                if (__instance == null) return;
                Dictionary<string, string> dict = null;
                if (LanguageManager.IsLoaded)
                {
                    dict = LanguageManager.CurrentLanguage.categorySlideTexts;
                    if (dict == null)
                        dict = LanguageManager.CurrentLanguage.categorySlideTexts = new Dictionary<string, string>();
                }

                // Translate category name using levelNames
                var catField = AccessTools.Field(typeof(UILevelSelectCategorySlide), "categoryName");
                var catTmp = catField?.GetValue(__instance) as TMP_Text;
                if (catTmp != null)
                {
                    string original = ResolveOriginalTranslationKey(catTmp.text ?? string.Empty, dict);
                    TranslateTextAndSaveIfMissing(catTmp, original, dict, "[UILevelSelectCategorySlide_Patch]");
                }

                // Translate lockedText lines using mainObjectives (unlock conditions may be arbitrary strings)
                var lockedField = AccessTools.Field(typeof(UILevelSelectCategorySlide), "lockedText");
                var lockedTmp = lockedField?.GetValue(__instance) as TMP_Text;
                if (lockedTmp != null)
                {
                    string originalAll = lockedTmp.text ?? string.Empty;
                    var lines = originalAll.Split(new[] { '\n' }, StringSplitOptions.None);
                    bool changed = false;
                    bool addedMissing = false;
                    for (int i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i] ?? string.Empty;
                        bool hasCr = line.EndsWith("\r", StringComparison.Ordinal);
                        string lineCore = hasCr ? line.Substring(0, line.Length - 1) : line;
                        string trimmedLine = lineCore.Trim();
                        if (string.IsNullOrEmpty(trimmedLine))
                            continue;

                        string sourceLine = ResolveOriginalTranslationKey(trimmedLine, dict);
                        string translated = sourceLine;

                        if (dict != null)
                        {
                            if (dict.TryGetValue(sourceLine, out var val) && !string.IsNullOrEmpty(val) && val != sourceLine)
                            {
                                translated = val;
                            }
                            else if (!dict.ContainsKey(sourceLine))
                            {
                                dict[sourceLine] = sourceLine;
                                addedMissing = true;
                                Logging.Info($"[UILevelSelectCategorySlide_Patch] Added missing categorySlideTexts key: '{sourceLine}'");
                            }
                        }

                        string finalLine = hasCr ? translated + "\r" : translated;
                        if (!string.Equals(lines[i], finalLine, StringComparison.Ordinal))
                        {
                            lines[i] = finalLine;
                            changed = true;
                        }
                    }

                    if (addedMissing)
                        LanguageManager.SaveCurrentLanguage();

                    if (changed)
                        lockedTmp.text = string.Join("\n", lines);
                }

                // Apply centralized font to all TMP_Text children
                var font = TMPFontReplacer.GetCachedFont(Plugin.GlobalFontPath);
                if (font != null)
                {
                    var allTexts = __instance.GetComponentsInChildren<TMP_Text>(true);
                    foreach (var tmp in allTexts)
                    {
                        if (tmp == null) continue;
                        if (!Equals(tmp.font, font))
                            TMPFontReplacer.ApplyFontToTMP(tmp, font);
                    }
                }
            }
            catch (Exception e)
            {
                Logging.Warn($"[UILevelSelectCategorySlide_Patch] Error: {e}");
            }
        }
    }
}
