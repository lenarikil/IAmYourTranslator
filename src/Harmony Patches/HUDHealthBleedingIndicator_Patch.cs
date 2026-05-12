using System;
using HarmonyLib;
using TMPro;
using IAmYourTranslator.json;
using static IAmYourTranslator.CommonFunctions;
using static IAmYourTranslator.Logging;
using Fleece;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(HUDHealthBleedingIndicator))]
    public static class HUDHealthBleedingIndicator_Patch
    {
        // Флаг для отладки - временно отключаем кеширование
        private const bool EnableCaching = false;
        
        [HarmonyPostfix]
        [HarmonyPatch("Update")]
        public static void Update_Postfix(HUDHealthBleedingIndicator __instance)
        {
            try
            {
                if (__instance == null || !LanguageManager.IsLoaded) return;
                
                // Получить TMP_Text компонент
                var bleedingText = Traverse.Create(__instance).Field("bleedingText").GetValue<TMP_Text>();
                if (bleedingText == null) return;
                
                // Логирование состояния полей passage
                var bleedingPassageField = Traverse.Create(__instance).Field("bleedingPassage").GetValue<Jumper>();
                var superStrengthPassageField = Traverse.Create(__instance).Field("superStrengthPassage").GetValue<Jumper>();
                Debug($"[HUDHealthBleedingIndicator_Patch] bleedingPassage is null: {bleedingPassageField == null}");
                Debug($"[HUDHealthBleedingIndicator_Patch] superStrengthPassage is null: {superStrengthPassageField == null}");
                Debug($"[HUDHealthBleedingIndicator_Patch] Current bleedingText.text: '{bleedingText.text}'");
                
                // Получить словарь healthStatus
                var dict = LanguageManager.CurrentLanguage.healthStatus;
                if (dict == null)
                    dict = LanguageManager.CurrentLanguage.healthStatus = new System.Collections.Generic.Dictionary<string, string>();
                
                // Определить текущее состояние (суперсила или кровотечение)
                bool isSuperStrength = GetSuperStrengthState();
                Debug($"[HUDHealthBleedingIndicator_Patch] Super strength state: {isSuperStrength}");
                
                // Получить оригинальные тексты из passage
                string bleedingOriginal = GetBleedingOriginalText(__instance);
                string superStrengthOriginal = GetSuperStrengthOriginalText(__instance);
                
                Debug($"[HUDHealthBleedingIndicator_Patch] Bleeding original: '{bleedingOriginal}'");
                Debug($"[HUDHealthBleedingIndicator_Patch] Super strength original: '{superStrengthOriginal}'");
                
                // Выбрать соответствующий оригинальный текст и ключ
                string originalText = isSuperStrength ? superStrengthOriginal : bleedingOriginal;
                string fixedKey = isSuperStrength ? "superStrengthText" : "bleedingText";
                
                Debug($"[HUDHealthBleedingIndicator_Patch] Selected key: '{fixedKey}', original text: '{originalText}'");
                
                if (string.IsNullOrEmpty(originalText))
                {
                    // Если не удалось получить оригинальный текст, используем текущий текст как запасной вариант
                    originalText = bleedingText.text;
                    Warn($"[HUDHealthBleedingIndicator_Patch] Failed to get original text for {(isSuperStrength ? "super strength" : "bleeding")}, using current text: '{originalText}'");
                }
                
                
                Debug($"[HUDHealthBleedingIndicator_Patch] Before TranslateWithFixedKey: text='{bleedingText.text}', original='{originalText}', key='{fixedKey}'");
                // Применить перевод с фиксированным ключом
                TranslateWithFixedKey(bleedingText, originalText, dict, fixedKey, "[HUDHealthBleedingIndicator]");
            }
            catch (Exception e)
            {
                Warn($"[HUDHealthBleedingIndicator_Patch] Error in Update_Postfix: {e}");
            }
        }
        
        private static bool GetSuperStrengthState()
        {
            try
            {
                // Прямой доступ к GameManager.instance, как в оригинальном коде
                if (GameManager.instance != null && GameManager.instance.player != null)
                {
                    bool superStrength = GameManager.instance.player.GetSuperStrength();
                    Debug($"[HUDHealthBleedingIndicator_Patch] GetSuperStrength returned: {superStrength}");
                    return superStrength;
                }
                else
                {
                    Debug($"[HUDHealthBleedingIndicator_Patch] GameManager.instance or player is null");
                }
            }
            catch (Exception ex)
            {
                Warn($"[HUDHealthBleedingIndicator_Patch] Failed to get super strength state: {ex.Message}");
            }
            return false;
        }
        
        private static string GetBleedingOriginalText(HUDHealthBleedingIndicator instance)
        {
            try
            {
                var bleedingPassage = Traverse.Create(instance).Field("bleedingPassage").GetValue<Jumper>();
                if (bleedingPassage != null)
                {
                    // Используем рефлексию для вызова GetPassageParsedTextCached, если метод доступен
                    var method = bleedingPassage.GetType().GetMethod("GetPassageParsedTextCached");
                    if (method != null)
                    {
                        var result = method.Invoke(bleedingPassage, null) as string;
                        Debug($"[HUDHealthBleedingIndicator_Patch] GetBleedingOriginalText via GetPassageParsedTextCached: '{result}'");
                        return result;
                    }
                    // Альтернатива: получить parsedText из passage, если доступно
                    var passageProp = bleedingPassage.GetType().GetProperty("passage");
                    if (passageProp != null)
                    {
                        var passage = passageProp.GetValue(bleedingPassage);
                        if (passage != null)
                        {
                            var parsedTextProp = passage.GetType().GetProperty("parsedText");
                            if (parsedTextProp != null)
                            {
                                var result = parsedTextProp.GetValue(passage) as string;
                                Debug($"[HUDHealthBleedingIndicator_Patch] GetBleedingOriginalText via passage.parsedText: '{result}'");
                                return result;
                            }
                        }
                    }
                }
                else
                {
                    Debug($"[HUDHealthBleedingIndicator_Patch] bleedingPassage is null");
                }
            }
            catch (Exception ex)
            {
                Warn($"[HUDHealthBleedingIndicator_Patch] Failed to get bleeding original text: {ex.Message}");
            }
            return null;
        }
        
        private static string GetSuperStrengthOriginalText(HUDHealthBleedingIndicator instance)
        {
            try
            {
                var superStrengthPassage = Traverse.Create(instance).Field("superStrengthPassage").GetValue<Jumper>();
                if (superStrengthPassage != null)
                {
                    // Получить parsedText из passage
                    var passageProp = superStrengthPassage.GetType().GetProperty("passage");
                    if (passageProp != null)
                    {
                        var passage = passageProp.GetValue(superStrengthPassage);
                        if (passage != null)
                        {
                            var parsedTextProp = passage.GetType().GetProperty("parsedText");
                            if (parsedTextProp != null)
                            {
                                var result = parsedTextProp.GetValue(passage) as string;
                                Debug($"[HUDHealthBleedingIndicator_Patch] GetSuperStrengthOriginalText via passage.parsedText: '{result}'");
                                return result;
                            }
                        }
                    }
                    // Попробовать получить текст через метод GetPassageParsedTextCached, если есть
                    var method = superStrengthPassage.GetType().GetMethod("GetPassageParsedTextCached");
                    if (method != null)
                    {
                        var result = method.Invoke(superStrengthPassage, null) as string;
                        Debug($"[HUDHealthBleedingIndicator_Patch] GetSuperStrengthOriginalText via GetPassageParsedTextCached: '{result}'");
                        return result;
                    }
                }
                else
                {
                    Debug($"[HUDHealthBleedingIndicator_Patch] superStrengthPassage is null");
                }
            }
            catch (Exception ex)
            {
                Warn($"[HUDHealthBleedingIndicator_Patch] Failed to get super strength original text: {ex.Message}");
            }
            return null;
        }
        
        private static void TranslateWithFixedKey(TMP_Text textComponent, string originalText,
            System.Collections.Generic.Dictionary<string, string> dict, string fixedKey, string logPrefix)
        {
            if (textComponent == null || dict == null || string.IsNullOrEmpty(originalText))
                return;
            
            Debug($"[HUDHealthBleedingIndicator_Patch] TranslateWithFixedKey: key='{fixedKey}', original='{originalText}', current text='{textComponent.text}'");
            
            // Проверяем, есть ли перевод для фиксированного ключа
            if (dict.TryGetValue(fixedKey, out var translated) && !string.IsNullOrEmpty(translated))
            {
                Debug($"[HUDHealthBleedingIndicator_Patch] Found translation for key '{fixedKey}': '{translated}'");
                if (!string.Equals(textComponent.text, translated, StringComparison.Ordinal))
                {
                    textComponent.text = translated;
                    Info($"{logPrefix} Translated via fixed key '{fixedKey}': '{originalText}' -> '{translated}'");
                }
                else
                {
                    Debug($"[HUDHealthBleedingIndicator_Patch] Text already matches translation");
                }
                return;
            }
            
            // Если перевода нет, добавляем оригинальный текст как значение для фиксированного ключа
            if (!dict.ContainsKey(fixedKey))
            {
                dict[fixedKey] = originalText;
                LanguageManager.SaveCurrentLanguage();
                Warn($"{logPrefix} Added missing translation for fixed key '{fixedKey}': '{originalText}'");
                Debug($"[HUDHealthBleedingIndicator_Patch] Added key '{fixedKey}' with value '{originalText}' to dictionary");
            }
            else
            {
                Debug($"[HUDHealthBleedingIndicator_Patch] Key '{fixedKey}' already exists in dictionary with value '{dict[fixedKey]}'");
            }
            
            // Если текущий текст не совпадает с оригиналом, устанавливаем оригинал
            if (textComponent.text != originalText)
            {
                Debug($"[HUDHealthBleedingIndicator_Patch] Setting text to original: '{originalText}'");
                textComponent.text = originalText;
            }
        }
    }
}