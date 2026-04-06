using System;
using HarmonyLib;
using UnityEngine;
using Enemy;
using IAmYourTranslator.json;
using TMPro;
using static IAmYourTranslator.CommonFunctions;
using BepInEx;
using UnityEngine.SceneManagement;
using System.Reflection;

namespace IAmYourTranslator.HarmonyPatches
{
    [HarmonyPatch]
    public static class EnemyRadioPatches
    {
        private static readonly FieldInfo DialogueField = AccessTools.Field(typeof(HUDEnemyRadio), "dialogue");

        // ----------------------------
        // DisplayLine — visual text replacement
        // ----------------------------
        [HarmonyPatch(typeof(HUDEnemyRadio), "DisplayLine")]
        [HarmonyPostfix]
        public static void DisplayLinePostfix(HUDEnemyRadio __instance, string dialogueString, EnemyHuman enemy, Color displayColor, bool scripted)
        {
            try
            {
                if (string.IsNullOrEmpty(dialogueString)) return;
                if (!LanguageManager.IsLoaded || LanguageManager.CurrentLanguage == null) return;

                string translatedText;

                if (scripted)
                {
                    // Get the level name from the current scene
                    string levelName = GetCurrentSceneName();
                    var dict = LanguageManager.CurrentLanguage.enemyRadioScripted;
                    if (dict == null)
                        dict = LanguageManager.CurrentLanguage.enemyRadioScripted = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>();

                    // Check if the level exists in the dictionary
                    if (!dict.TryGetValue(levelName, out var levelDict))
                    {
                        levelDict = new System.Collections.Generic.Dictionary<string, string>();
                        dict[levelName] = levelDict;
                    }

                    if (!levelDict.TryGetValue(dialogueString, out translatedText))
                    {
                        levelDict[dialogueString] = dialogueString;
                        LanguageManager.SaveCurrentLanguage();
                        translatedText = dialogueString;
                    }
                }
                else
                {
                    if (!LanguageManager.CurrentLanguage.enemyRadio.TryGetValue(dialogueString, out translatedText))
                    {
                        LanguageManager.CurrentLanguage.enemyRadio[dialogueString] = dialogueString;
                        LanguageManager.SaveCurrentLanguage();
                        translatedText = dialogueString;
                    }
                }

                // Apply translated text only after original DisplayLine logic is done,
                // so we do not alter any potential vanilla audio flow.
                var tmp = DialogueField?.GetValue(__instance) as TMP_Text;
                if (tmp == null)
                    tmp = __instance.GetComponentInChildren<TextMeshProUGUI>(true);

                if (tmp != null)
                {
                    tmp.text = translatedText;
                    var tmpFont = TMPFontReplacer.GetCachedFont(Plugin.GlobalFontPath);
                    if (tmpFont != null)
                    {
                        CommonFunctions.TMPFontReplacer.ApplyFontToTMP(tmp, tmpFont);
                    }
                    else
                    {
                        Logging.Warn("[HUDEnemyRadio] Failed to load custom font!");
                    }
                }
            }
            catch (Exception e)
            {
                Logging.Error($"[HUDEnemyRadio] Error in DisplayLinePostfix: {e}");
            }
        }
    }
}
