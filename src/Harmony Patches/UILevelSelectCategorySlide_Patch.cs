using System;
using System.Linq;
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

                // Translate category name using levelNames
                var catField = AccessTools.Field(typeof(UILevelSelectCategorySlide), "categoryName");
                var catTmp = catField?.GetValue(__instance) as TMP_Text;
                if (catTmp != null)
                {
                    string original = catTmp.text ?? string.Empty;
                    string translated = original;
                    if (LanguageManager.IsLoaded)
                    {
                        var dict = LanguageManager.CurrentLanguage.categorySlideTexts;
                        if (dict == null)
                            dict = LanguageManager.CurrentLanguage.categorySlideTexts = new System.Collections.Generic.Dictionary<string, string>();

                        if (dict.TryGetValue(original, out var val) && !string.IsNullOrEmpty(val) && val != original)
                        {
                            translated = val;
                        }
                        else if (!dict.ContainsKey(original))
                        {
                            dict[original] = original;
                            LanguageManager.SaveCurrentLanguage();
                            Logging.Info($"[UILevelSelectCategorySlide_Patch] Added missing categorySlideTexts key: '{original}'");
                        }
                    }
                    catTmp.text = translated;
                }

                // Translate lockedText lines using mainObjectives (unlock conditions may be arbitrary strings)
                var lockedField = AccessTools.Field(typeof(UILevelSelectCategorySlide), "lockedText");
                var lockedTmp = lockedField?.GetValue(__instance) as TMP_Text;
                if (lockedTmp != null)
                {
                    string originalAll = lockedTmp.text ?? string.Empty;
                    var lines = originalAll.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
                    bool changed = false;
                    for (int i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        if (string.IsNullOrEmpty(line)) continue;
                        string translated = line;
                        if (LanguageManager.IsLoaded)
                        {
                                var dict = LanguageManager.CurrentLanguage.categorySlideTexts;
                                if (dict == null)
                                    dict = LanguageManager.CurrentLanguage.categorySlideTexts = new System.Collections.Generic.Dictionary<string, string>();

                                if (dict.TryGetValue(line, out var val) && !string.IsNullOrEmpty(val) && val != line)
                                {
                                    translated = val;
                                }
                                else if (!dict.ContainsKey(line))
                                {
                                    dict[line] = line;
                                    LanguageManager.SaveCurrentLanguage();
                                    Logging.Info($"[UILevelSelectCategorySlide_Patch] Added missing categorySlideTexts key: '{line}'");
                                }
                        }
                        if (translated != line)
                        {
                            lines[i] = translated;
                            changed = true;
                        }
                    }
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
