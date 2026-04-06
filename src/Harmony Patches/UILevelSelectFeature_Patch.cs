using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using IAmYourTranslator.json;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(UILevelSelectFeature), "Refresh")]
    public static class UILevelSelectFeature_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(UILevelSelectFeature __instance, SceneInformation info)
        {
            try
            {
                if (__instance == null) return;

                var field = AccessTools.Field(typeof(UILevelSelectFeature), "lockedDescription");
                var tmp = field?.GetValue(__instance) as TMP_Text;
                if (tmp == null) return;

                string original = tmp.text ?? string.Empty;
                if (string.IsNullOrEmpty(original)) return;

                // Break into lines and translate each line using lockedDescriptions
                var lines = original.Split(new[] { '\n' }, StringSplitOptions.None).Select(s => s.TrimEnd()).ToArray();
                bool changed = false;
                bool addedMissing = false;

                Dictionary<string, string> dict = null;
                if (LanguageManager.IsLoaded)
                {
                    dict = LanguageManager.CurrentLanguage.lockedDescriptions;
                    if (dict == null)
                        dict = LanguageManager.CurrentLanguage.lockedDescriptions = new Dictionary<string, string>();
                }

                if (dict != null || !LanguageManager.IsLoaded)
                {
                    for (int i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        if (string.IsNullOrEmpty(line)) continue;

                        string sourceLine = ResolveOriginalTranslationKey(line, dict);
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
                                Logging.Info($"[UILevelSelectFeature_Patch] Added missing lockedDescription key: '{sourceLine}'");
                            }
                        }

                        if (!string.Equals(lines[i], translated, StringComparison.Ordinal))
                        {
                            lines[i] = translated;
                            changed = true;
                        }
                    }
                }

                if (addedMissing)
                    LanguageManager.SaveCurrentLanguage();

                if (changed)
                {
                    tmp.text = string.Join("\n", lines);
                }

                // Apply centralized font to the lockedDescription
                var font = TMPFontReplacer.GetCachedFont(Plugin.GlobalFontPath);
                if (font != null)
                    TMPFontReplacer.ApplyFontToTMP(tmp, font);
            }
            catch (Exception e)
            {
                Logging.Warn($"[UILevelSelectFeature_Patch] Error: {e}");
            }
        }
    }
}
