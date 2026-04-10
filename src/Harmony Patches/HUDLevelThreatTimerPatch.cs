using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IAmYourTranslator.json;
using static IAmYourTranslator.Logging;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(HUDLevelThreatTimer))]
    public static class HUDLevelThreatTimerPatch
    {
        // Для отслеживания изменений текста и предотвращения спама
        private static string _lastValueText = "";
        private static bool _lastWarningEnabled = false;
        private static float _lastFillAmount = -1f;
        private static int _updateCounter = 0;
        private const int LOG_EVERY_N_UPDATES = 60; // Логировать раз в секунду (при 60 FPS)

        // === Initialize ===
        [HarmonyPostfix]
        [HarmonyPatch("Initialize")]
        static void Initialize_Postfix(
            HUDLevelThreatTimer __instance,
            HUDLevelThreatTimer.PlayerGoal goal,
            HUDLevelThreatTimer.DisplayType displayType,
            float scaledMaxValue,
            float defaultFillPercent,
            string headerText,
            string warningText)
        {
            try
            {
                if (__instance == null)
                    return;

                string instanceInfo = GetInstanceInfo(__instance);
                Debug($"[HUDLevelThreatTimer] Initialize called: {instanceInfo}");
                Debug($"[HUDLevelThreatTimer]   Goal: {goal}, DisplayType: {displayType}, ScaledMaxValue: {scaledMaxValue:F2}, DefaultFill: {defaultFillPercent:P}");
                
                // Сброс отслеживаемых значений
                _lastValueText = "";
                _lastWarningEnabled = false;
                _lastFillAmount = -1f;

                // Если язык загружен, применяем переводы
                if (!LanguageManager.IsLoaded)
                {
                    Debug($"[HUDLevelThreatTimer] Language not loaded, skipping translation.");
                    return;
                }

                var dict = LanguageManager.CurrentLanguage.threatTimerTexts;
                if (dict == null)
                    dict = LanguageManager.CurrentLanguage.threatTimerTexts = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>();

                // Переводим headerText
                if (!string.IsNullOrEmpty(headerText))
                {
                    if (!dict.TryGetValue("headerText", out var headerDict))
                    {
                        headerDict = new System.Collections.Generic.Dictionary<string, string>();
                        dict["headerText"] = headerDict;
                    }

                    if (headerDict.TryGetValue(headerText, out string translatedHeader) && translatedHeader != headerText)
                    {
                        // Применяем перевод
                        var headerTextField = GetPrivateField<TMP_Text>(__instance, "headerText");
                        if (headerTextField != null)
                        {
                            headerTextField.text = translatedHeader;
                            Info($"[HUDLevelThreatTimer] Translated header: '{headerText}' -> '{translatedHeader}'");
                        }
                    }
                    else
                    {
                        // Добавляем ключ в подсловарь
                        headerDict[headerText] = headerText;
                        LanguageManager.SaveCurrentLanguage();
                        Info($"[HUDLevelThreatTimer] Added missing translation key for header: '{headerText}'");
                    }
                }

                // Переводим warningText (если не пустой)
                if (!string.IsNullOrEmpty(warningText))
                {
                    if (!dict.TryGetValue("warningText", out var warningDict))
                    {
                        warningDict = new System.Collections.Generic.Dictionary<string, string>();
                        dict["warningText"] = warningDict;
                    }

                    if (warningDict.TryGetValue(warningText, out string translatedWarning) && translatedWarning != warningText)
                    {
                        var warningTextField = GetPrivateField<TMP_Text>(__instance, "warningText");
                        if (warningTextField != null)
                        {
                            warningTextField.text = translatedWarning;
                            Info($"[HUDLevelThreatTimer] Translated warning: '{warningText}' -> '{translatedWarning}'");
                        }
                    }
                    else
                    {
                        warningDict[warningText] = warningText;
                        LanguageManager.SaveCurrentLanguage();
                        Info($"[HUDLevelThreatTimer] Added missing translation key for warning: '{warningText}'");
                    }
                }

                // Применяем шрифт, если требуется
                ApplyFontIfNeeded(__instance);
            }
            catch (Exception e)
            {
                Error($"[HUDLevelThreatTimer] Error in Initialize_Postfix: {e}");
            }
        }

        // === UpdateFill ===
        [HarmonyPostfix]
        [HarmonyPatch("UpdateFill")]
        static void UpdateFill_Postfix(HUDLevelThreatTimer __instance, float newFill)
        {
            try
            {
                if (__instance == null)
                    return;

                float oldFill = _lastFillAmount;
                if (Mathf.Abs(newFill - oldFill) > 0.001f)
                {
                    string instanceInfo = GetInstanceInfo(__instance);
                    Debug($"[HUDLevelThreatTimer] UpdateFill: {instanceInfo} | {oldFill:P} -> {newFill:P} (delta: {newFill - oldFill:P})");
                    _lastFillAmount = newFill;
                }
            }
            catch (Exception e)
            {
                Error($"[HUDLevelThreatTimer] Error in UpdateFill_Postfix: {e}");
            }
        }

        // === Update ===
        [HarmonyPostfix]
        [HarmonyPatch("Update")]
        static void Update_Postfix(HUDLevelThreatTimer __instance)
        {
            try
            {
                if (__instance == null)
                    return;

                _updateCounter++;
                if (_updateCounter < LOG_EVERY_N_UPDATES)
                    return;
                _updateCounter = 0;

                // Получаем текстовые компоненты через рефлексию
                var valueText = GetPrivateField<TMP_Text>(__instance, "valueText");
                var warningText = GetPrivateField<TMP_Text>(__instance, "warningText");
                var fillAmount = GetPrivateField<float>(__instance, "fillAmount");
                var goal = GetPrivateField<HUDLevelThreatTimer.PlayerGoal>(__instance, "goal");
                var idle = GetPrivateField<bool>(__instance, "idle");

                if (valueText != null)
                {
                    string currentValueText = valueText.text;
                    if (currentValueText != _lastValueText)
                    {
                        string instanceInfo = GetInstanceInfo(__instance);
                        Debug($"[HUDLevelThreatTimer] ValueText changed: {instanceInfo} | '{_lastValueText}' -> '{currentValueText}'");
                        _lastValueText = currentValueText;
                    }
                }

                if (warningText != null)
                {
                    bool warningEnabled = warningText.enabled;
                    if (warningEnabled != _lastWarningEnabled)
                    {
                        string instanceInfo = GetInstanceInfo(__instance);
                        Debug($"[HUDLevelThreatTimer] WarningText enabled: {instanceInfo} | {warningEnabled} (fillAmount={fillAmount:P}, goal={goal})");
                        _lastWarningEnabled = warningEnabled;
                    }
                }

                // Логирование состояния idle (только отладка)
                string instanceInfo2 = GetInstanceInfo(__instance);
                Debug($"[HUDLevelThreatTimer] Update snapshot: {instanceInfo2} | fillAmount={fillAmount:P}, goal={goal}, idle={idle}");
            }
            catch (Exception e)
            {
                Error($"[HUDLevelThreatTimer] Error in Update_Postfix: {e}");
            }
        }

        // === Start === (опционально, для логирования отключения)
        [HarmonyPostfix]
        [HarmonyPatch("Start")]
        static void Start_Postfix(HUDLevelThreatTimer __instance)
        {
            try
            {
                if (__instance == null)
                    return;

                var initialized = GetPrivateField<bool>(__instance, "initialized");
                if (!initialized)
                {
                    string instanceInfo = GetInstanceInfo(__instance);
                    Debug($"[HUDLevelThreatTimer] Start: component not initialized, mainAnchor disabled. {instanceInfo}");
                }
            }
            catch (Exception e)
            {
                Error($"[HUDLevelThreatTimer] Error in Start_Postfix: {e}");
            }
        }

        // === Вспомогательные методы ===
        private static string GetInstanceInfo(HUDLevelThreatTimer instance)
        {
            if (instance == null)
                return "null";
            
            string name = instance.gameObject?.name ?? "unnamed";
            string hierarchy = GetHierarchy(instance.gameObject, 3);
            return $"{name} ({hierarchy})";
        }

        private static string GetHierarchy(GameObject go, int maxDepth)
        {
            if (go == null)
                return "null";
            
            var current = go.transform;
            var parts = new System.Collections.Generic.List<string>();
            int depth = 0;
            while (current != null && depth < maxDepth)
            {
                parts.Insert(0, current.name);
                current = current.parent;
                depth++;
            }
            if (depth >= maxDepth && current != null)
                parts.Insert(0, "...");
            
            return string.Join("/", parts);
        }

        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            if (instance == null)
                return default(T);
            
            var field = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field == null)
                return default(T);
            
            return (T)field.GetValue(instance);
        }

        private static void ApplyFontIfNeeded(HUDLevelThreatTimer instance)
        {
            try
            {
                // Проверяем, содержит ли текст кириллицу (или другой не-латинский алфавит)
                var headerText = GetPrivateField<TMP_Text>(instance, "headerText");
                var warningText = GetPrivateField<TMP_Text>(instance, "warningText");
                var valueText = GetPrivateField<TMP_Text>(instance, "valueText");

                bool containsCyrillic = false;
                if (headerText != null && ContainsCyrillic(headerText.text))
                    containsCyrillic = true;
                if (!containsCyrillic && warningText != null && ContainsCyrillic(warningText.text))
                    containsCyrillic = true;
                if (!containsCyrillic && valueText != null && ContainsCyrillic(valueText.text))
                    containsCyrillic = true;

                if (containsCyrillic)
                {
                    var font = CommonFunctions.TMPFontReplacer.GetCachedFont(Plugin.GlobalFontPath);
                    if (font != null)
                    {
                        if (headerText != null) CommonFunctions.TMPFontReplacer.ApplyFontToTMP(headerText, font);
                        if (warningText != null) CommonFunctions.TMPFontReplacer.ApplyFontToTMP(warningText, font);
                        if (valueText != null) CommonFunctions.TMPFontReplacer.ApplyFontToTMP(valueText, font);
                        Debug($"[HUDLevelThreatTimer] Applied custom font for Cyrillic text.");
                    }
                }
            }
            catch (Exception e)
            {
                Warn($"[HUDLevelThreatTimer] Error applying font: {e.Message}");
            }
        }

        private static bool ContainsCyrillic(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if ((c >= '\u0400' && c <= '\u052F') || c == '\u2DE0' || c == '\u2DE1')
                    return true;
            }

            return false;
        }
    }
}