using System;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
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
                if (LanguageManager.IsLoaded)
                {
                    var dict = LanguageManager.CurrentLanguage.lockedDescriptions;
                    if (dict == null)
                        dict = LanguageManager.CurrentLanguage.lockedDescriptions = new System.Collections.Generic.Dictionary<string, string>();

                    for (int i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        if (string.IsNullOrEmpty(line)) continue;
                        if (dict.TryGetValue(line, out var val) && !string.IsNullOrEmpty(val) && val != line)
                        {
                            lines[i] = val;
                            changed = true;
                        }
                        else if (!dict.ContainsKey(line))
                        {
                            dict[line] = line;
                            LanguageManager.SaveCurrentLanguage();
                            Logging.Info($"[UILevelSelectFeature_Patch] Added missing lockedDescription key: '{line}'");
                        }
                    }
                }

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
