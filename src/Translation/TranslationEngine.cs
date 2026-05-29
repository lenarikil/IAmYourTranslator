using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IAmYourTranslator.json;

namespace IAmYourTranslator.Translation
{
    public static class TranslationEngine
    {
        public static string PreviousHudMessage;
        internal static readonly Dictionary<Text, string> OriginalUITextByComponent = new Dictionary<Text, string>();
        internal static readonly Dictionary<TMP_Text, string> OriginalTMPTextByComponent = new Dictionary<TMP_Text, string>();
        internal static Dictionary<string, string> LastKnownReverseLookupMap = new Dictionary<string, string>(StringComparer.Ordinal);
        internal const string BonusTimeTagMarker = " <size=85%>(";
        internal static readonly Regex TrailingCounterRegex = new Regex(
            "^(.*?)(\\s*[:：]?\\s*(?:\\[[0-9]+\\/[0-9]+\\]|\\([0-9]+\\/[0-9]+\\)|[0-9]+\\/[0-9]+))$",
            RegexOptions.Compiled);

        // Cached reflection data for JsonFormat
        private static PropertyInfo[] _cachedDictProperties;
        private static PropertyInfo[] _cachedNestedDictProperties;
        private static FieldInfo[] _cachedDictFields;
        private static FieldInfo[] _cachedNestedDictFields;
        private static bool _reflectionCacheInitialized;

        // Batching for CleanupOriginalTextCaches - avoid O(n) cleanup every frame
        private static DateTime _lastCleanupTime = DateTime.UtcNow;
        private static readonly TimeSpan CLEANUP_BATCH_INTERVAL = TimeSpan.FromSeconds(1);

        private static void CleanupOriginalTextCaches()
        {
            var deadUi = new List<Text>();
            foreach (var kv in OriginalUITextByComponent)
            {
                if (kv.Key == null)
                    deadUi.Add(kv.Key);
            }
            foreach (var key in deadUi)
                OriginalUITextByComponent.Remove(key);

            var deadTmp = new List<TMP_Text>();
            foreach (var kv in OriginalTMPTextByComponent)
            {
                if (kv.Key == null)
                    deadTmp.Add(kv.Key);
            }
            foreach (var key in deadTmp)
                OriginalTMPTextByComponent.Remove(key);
        }

        /// <summary>
        /// Batched cleanup - only cleans if interval elapsed since last cleanup
        /// </summary>
        private static void CleanupOriginalTextCachesBatched()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastCleanupTime) < CLEANUP_BATCH_INTERVAL)
                return;

            CleanupOriginalTextCaches();
            _lastCleanupTime = now;
        }

        public static void ClearOriginalTextCache(TMP_Text tmpComponent)
        {
            if (tmpComponent == null)
                return;
            OriginalTMPTextByComponent.Remove(tmpComponent);
        }

        internal static bool TryResolveOriginalKeyFromValue(string currentText, Dictionary<string, string> translationDict, out string resolvedOriginal)
        {
            resolvedOriginal = null;
            if (string.IsNullOrEmpty(currentText) || translationDict == null || translationDict.Count == 0)
                return false;

            if (translationDict.ContainsKey(currentText))
            {
                resolvedOriginal = currentText;
                return true;
            }

            foreach (var kv in translationDict)
            {
                if (string.IsNullOrEmpty(kv.Key))
                    continue;
                if (string.Equals(kv.Value, currentText, StringComparison.Ordinal))
                {
                    resolvedOriginal = kv.Key;
                    return true;
                }
            }

            return false;
        }

        public static string ResolveOriginalTranslationKey(string currentText, Dictionary<string, string> translationDict)
        {
            if (string.IsNullOrEmpty(currentText))
                return currentText;

            if (TryResolveOriginalKeyFromValue(currentText, translationDict, out var resolved) && !string.IsNullOrEmpty(resolved))
                return resolved;

            if (LastKnownReverseLookupMap != null && LastKnownReverseLookupMap.Count > 0)
            {
                string resolvedByPrevious = ResolveOriginalUsingLookupMap(currentText, LastKnownReverseLookupMap);
                if (!string.Equals(resolvedByPrevious, currentText, StringComparison.Ordinal))
                    return resolvedByPrevious;
            }

            return currentText;
        }

        public static void CaptureCurrentReverseLookupMap()
        {
            try
            {
                if (LanguageManager.CurrentLanguage == null)
                    return;

                var lookupDicts = GetActiveLookupDictionaries();
                var reverse = BuildReverseLookupMap(lookupDicts);
                if (reverse != null && reverse.Count > 0)
                    LastKnownReverseLookupMap = new Dictionary<string, string>(reverse, StringComparer.Ordinal);
            }
            catch (Exception e)
            {
                Logging.Warn($"[TranslationEngine] CaptureCurrentReverseLookupMap failed: {e.Message}");
            }
        }

        private static string ResolveOriginalText(Text textComponent, string originalText, Dictionary<string, string> translationDict)
        {
            if (textComponent == null)
                return null;

            if (OriginalUITextByComponent.TryGetValue(textComponent, out var cached) && !string.IsNullOrEmpty(cached))
                return cached;

            string candidate = string.IsNullOrEmpty(originalText) ? textComponent.text : originalText;
            if (TryResolveOriginalKeyFromValue(textComponent.text, translationDict, out var resolved))
                candidate = resolved;
            else if (translationDict == null && LastKnownReverseLookupMap.Count > 0)
                candidate = ResolveOriginalUsingLookupMap(textComponent.text, LastKnownReverseLookupMap);

            if (!string.IsNullOrEmpty(candidate))
                OriginalUITextByComponent[textComponent] = candidate;

            return candidate;
        }

        private static string ResolveOriginalText(TMP_Text tmpComponent, string originalText, Dictionary<string, string> translationDict)
        {
            if (tmpComponent == null)
                return null;

            if (OriginalTMPTextByComponent.TryGetValue(tmpComponent, out var cached) && !string.IsNullOrEmpty(cached))
                return cached;

            string candidate = string.IsNullOrEmpty(originalText) ? tmpComponent.text : originalText;
            if (TryResolveOriginalKeyFromValue(tmpComponent.text, translationDict, out var resolved))
                candidate = resolved;
            else if (translationDict == null && LastKnownReverseLookupMap.Count > 0)
                candidate = ResolveOriginalUsingLookupMap(tmpComponent.text, LastKnownReverseLookupMap);

            if (!string.IsNullOrEmpty(candidate))
                OriginalTMPTextByComponent[tmpComponent] = candidate;

            return candidate;
        }

        public static void TranslateTextAndSaveIfMissing(Text textComponent, string originalText, Dictionary<string, string> translationDict, string logPrefix = "")
        {
            if (textComponent == null)
                return;

            CleanupOriginalTextCachesBatched();
            string sourceText = ResolveOriginalText(textComponent, originalText, translationDict);
            if (string.IsNullOrEmpty(sourceText))
                return;

            if (translationDict == null)
            {
                if (textComponent.text != sourceText)
                    textComponent.text = sourceText;
                return;
            }

            if (translationDict.TryGetValue(sourceText, out var translated) && !string.IsNullOrEmpty(translated))
            {
                if (!string.Equals(textComponent.text, translated, StringComparison.Ordinal))
                {
                    textComponent.text = translated;
                    Logging.Info($"{logPrefix} Translated: '{sourceText}' -> '{translated}'");
                }
                return;
            }

            if (!translationDict.ContainsKey(sourceText))
            {
                translationDict[sourceText] = sourceText;
                LanguageManager.SaveCurrentLanguage();
                Logging.Warn($"{logPrefix} Added missing translation key: '{sourceText}'");
            }

            if (textComponent.text != sourceText)
                textComponent.text = sourceText;
        }

        public static void TranslateTextAndSaveIfMissing(TMP_Text tmpComponent, string originalText, Dictionary<string, string> translationDict, string logPrefix = "")
        {
            if (tmpComponent == null)
                return;

            CleanupOriginalTextCachesBatched();
            string sourceText = ResolveOriginalText(tmpComponent, originalText, translationDict);
            if (string.IsNullOrEmpty(sourceText))
                return;

            if (translationDict == null)
            {
                if (tmpComponent.text != sourceText)
                    tmpComponent.text = sourceText;
                return;
            }

            if (translationDict.TryGetValue(sourceText, out var translated) && !string.IsNullOrEmpty(translated))
            {
                if (!string.Equals(tmpComponent.text, translated, StringComparison.Ordinal))
                {
                    tmpComponent.text = translated;
                    Logging.Info($"{logPrefix} Translated (TMP): '{sourceText}' -> '{translated}'");
                }
                return;
            }

            if (!translationDict.ContainsKey(sourceText))
            {
                translationDict[sourceText] = sourceText;
                LanguageManager.SaveCurrentLanguage();
                Logging.Warn($"{logPrefix} Added missing TMP translation key: '{sourceText}'");
            }

            if (tmpComponent.text != sourceText)
                tmpComponent.text = sourceText;
        }

        private static void AddLookupDictionary(List<Dictionary<string, string>> lookup, HashSet<Dictionary<string, string>> seen, Dictionary<string, string> dict)
        {
            if (dict == null || dict.Count == 0)
                return;
            if (seen.Add(dict))
                lookup.Add(dict);
        }

        private static void AddNestedLookupDictionaries(List<Dictionary<string, string>> lookup, HashSet<Dictionary<string, string>> seen, Dictionary<string, Dictionary<string, string>> nested)
        {
            if (nested == null || nested.Count == 0)
                return;

            foreach (var kv in nested)
                AddLookupDictionary(lookup, seen, kv.Value);
        }

        private static void EnsureReflectionCache()
        {
            if (_reflectionCacheInitialized)
                return;

            _reflectionCacheInitialized = true;
            var type = typeof(JsonFormat);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

            var dictProps = new List<PropertyInfo>();
            var nestedProps = new List<PropertyInfo>();
            foreach (var prop in type.GetProperties(flags))
            {
                if (!prop.CanRead || prop.GetIndexParameters().Length != 0)
                    continue;

                if (prop.PropertyType == typeof(Dictionary<string, string>))
                    dictProps.Add(prop);
                else if (prop.PropertyType == typeof(Dictionary<string, Dictionary<string, string>>))
                    nestedProps.Add(prop);
            }
            _cachedDictProperties = dictProps.ToArray();
            _cachedNestedDictProperties = nestedProps.ToArray();

            var dictFields = new List<FieldInfo>();
            var nestedFields = new List<FieldInfo>();
            foreach (var field in type.GetFields(flags))
            {
                if (field.FieldType == typeof(Dictionary<string, string>))
                    dictFields.Add(field);
                else if (field.FieldType == typeof(Dictionary<string, Dictionary<string, string>>))
                    nestedFields.Add(field);
            }
            _cachedDictFields = dictFields.ToArray();
            _cachedNestedDictFields = nestedFields.ToArray();
        }

        internal static List<Dictionary<string, string>> GetActiveLookupDictionaries()
        {
            var lookup = new List<Dictionary<string, string>>(24);
            var lang = LanguageManager.CurrentLanguage;
            if (lang == null)
                return lookup;

            var seen = new HashSet<Dictionary<string, string>>();

            AddLookupDictionary(lookup, seen, lang.settings);
            AddLookupDictionary(lookup, seen, lang.hardCoded);
            AddLookupDictionary(lookup, seen, lang.Hints);

            EnsureReflectionCache();

            foreach (var prop in _cachedDictProperties)
            {
                var dict = prop.GetValue(lang, null) as Dictionary<string, string>;
                AddLookupDictionary(lookup, seen, dict);
            }

            foreach (var prop in _cachedNestedDictProperties)
            {
                var nested = prop.GetValue(lang, null) as Dictionary<string, Dictionary<string, string>>;
                AddNestedLookupDictionaries(lookup, seen, nested);
            }

            foreach (var field in _cachedDictFields)
            {
                var dict = field.GetValue(lang) as Dictionary<string, string>;
                AddLookupDictionary(lookup, seen, dict);
            }

            foreach (var field in _cachedNestedDictFields)
            {
                var nested = field.GetValue(lang) as Dictionary<string, Dictionary<string, string>>;
                AddNestedLookupDictionaries(lookup, seen, nested);
            }

            return lookup;
        }

        internal static Dictionary<string, string> BuildForwardLookupMap(List<Dictionary<string, string>> lookupDicts)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (lookupDicts == null || lookupDicts.Count == 0)
                return map;

            foreach (var dict in lookupDicts)
            {
                if (dict == null || dict.Count == 0)
                    continue;

                foreach (var kv in dict)
                {
                    if (string.IsNullOrEmpty(kv.Key) || string.IsNullOrEmpty(kv.Value))
                        continue;
                    if (map.ContainsKey(kv.Key))
                        continue;
                    map[kv.Key] = kv.Value;
                }
            }

            return map;
        }

        internal static Dictionary<string, string> BuildReverseLookupMap(List<Dictionary<string, string>> lookupDicts)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (lookupDicts == null || lookupDicts.Count == 0)
                return map;

            foreach (var dict in lookupDicts)
            {
                if (dict == null || dict.Count == 0)
                    continue;

                foreach (var kv in dict)
                {
                    if (string.IsNullOrEmpty(kv.Key) || string.IsNullOrEmpty(kv.Value))
                        continue;
                    if (map.ContainsKey(kv.Value))
                        continue;
                    map[kv.Value] = kv.Key;
                }
            }

            return map;
        }

        internal static string TranslateUsingLookupMap(string sourceText, Dictionary<string, string> forwardLookup)
        {
            if (string.IsNullOrEmpty(sourceText) || forwardLookup == null || forwardLookup.Count == 0)
                return sourceText;

            if (forwardLookup.TryGetValue(sourceText, out var translatedExact) && !string.IsNullOrEmpty(translatedExact))
                return translatedExact;

            int bonusTagIndex = sourceText.IndexOf(BonusTimeTagMarker, StringComparison.Ordinal);
            if (bonusTagIndex > 0)
            {
                string baseText = sourceText.Substring(0, bonusTagIndex);
                string suffix = sourceText.Substring(bonusTagIndex);
                if (forwardLookup.TryGetValue(baseText, out var translatedBase) && !string.IsNullOrEmpty(translatedBase))
                    return translatedBase + suffix;
            }

            var counterMatch = TrailingCounterRegex.Match(sourceText);
            if (counterMatch.Success)
            {
                string baseText = counterMatch.Groups[1].Value.TrimEnd();
                string suffix = counterMatch.Groups[2].Value;
                if (forwardLookup.TryGetValue(baseText, out var translatedBase) && !string.IsNullOrEmpty(translatedBase))
                    return translatedBase + suffix;
            }

            if (sourceText.IndexOf('\n') >= 0)
            {
                var lines = sourceText.Split('\n');
                bool changed = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    bool hasCr = line.EndsWith("\r", StringComparison.Ordinal);
                    string lineCore = hasCr ? line.Substring(0, line.Length - 1) : line;
                    string lineTrimmed = lineCore.Trim();

                    if (string.IsNullOrEmpty(lineTrimmed))
                        continue;

                    if (forwardLookup.TryGetValue(lineCore, out var translatedLine) && !string.IsNullOrEmpty(translatedLine))
                    {
                        lines[i] = hasCr ? translatedLine + "\r" : translatedLine;
                        changed = true;
                        continue;
                    }

                    if (forwardLookup.TryGetValue(lineTrimmed, out translatedLine) && !string.IsNullOrEmpty(translatedLine))
                    {
                        lines[i] = hasCr ? translatedLine + "\r" : translatedLine;
                        changed = true;
                    }
                }

                if (changed)
                    return string.Join("\n", lines);
            }

            return sourceText;
        }

        internal static string ResolveOriginalUsingLookupMap(string displayedText, Dictionary<string, string> reverseLookup)
        {
            if (string.IsNullOrEmpty(displayedText) || reverseLookup == null || reverseLookup.Count == 0)
                return displayedText;

            if (reverseLookup.TryGetValue(displayedText, out var originalExact) && !string.IsNullOrEmpty(originalExact))
                return originalExact;

            int bonusTagIndex = displayedText.IndexOf(BonusTimeTagMarker, StringComparison.Ordinal);
            if (bonusTagIndex > 0)
            {
                string baseText = displayedText.Substring(0, bonusTagIndex);
                string suffix = displayedText.Substring(bonusTagIndex);
                if (reverseLookup.TryGetValue(baseText, out var originalBase) && !string.IsNullOrEmpty(originalBase))
                    return originalBase + suffix;
            }

            var counterMatch = TrailingCounterRegex.Match(displayedText);
            if (counterMatch.Success)
            {
                string baseText = counterMatch.Groups[1].Value.TrimEnd();
                string suffix = counterMatch.Groups[2].Value;
                if (reverseLookup.TryGetValue(baseText, out var originalBase) && !string.IsNullOrEmpty(originalBase))
                    return originalBase + suffix;
            }

            if (displayedText.IndexOf('\n') >= 0)
            {
                var lines = displayedText.Split('\n');
                bool changed = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    bool hasCr = line.EndsWith("\r", StringComparison.Ordinal);
                    string lineCore = hasCr ? line.Substring(0, line.Length - 1) : line;
                    string lineTrimmed = lineCore.Trim();

                    if (string.IsNullOrEmpty(lineTrimmed))
                        continue;

                    if (reverseLookup.TryGetValue(lineCore, out var originalLine) && !string.IsNullOrEmpty(originalLine))
                    {
                        lines[i] = hasCr ? originalLine + "\r" : originalLine;
                        changed = true;
                        continue;
                    }

                    if (reverseLookup.TryGetValue(lineTrimmed, out originalLine) && !string.IsNullOrEmpty(originalLine))
                    {
                        lines[i] = hasCr ? originalLine + "\r" : originalLine;
                        changed = true;
                    }
                }

                if (changed)
                    return string.Join("\n", lines);
            }

            return displayedText;
        }

        public static void RefreshAllSceneTexts(bool skipTranslatorSettingsMenu = true)
        {
            try
            {
                CleanupOriginalTextCaches();
                OriginalTMPTextByComponent.Clear();
                OriginalUITextByComponent.Clear();

                bool languageLoaded = LanguageManager.IsLoaded && LanguageManager.CurrentLanguage != null;
                var lookupDicts = GetActiveLookupDictionaries();
                var forwardLookup = languageLoaded ? BuildForwardLookupMap(lookupDicts) : null;
                Dictionary<string, string> previousReverseLookup = null;
                if (LastKnownReverseLookupMap != null && LastKnownReverseLookupMap.Count > 0)
                    previousReverseLookup = new Dictionary<string, string>(LastKnownReverseLookupMap, StringComparer.Ordinal);

                var currentReverseLookup = languageLoaded ? BuildReverseLookupMap(lookupDicts) : null;
                Dictionary<string, string> reverseLookup = currentReverseLookup;

                if (languageLoaded && previousReverseLookup != null && previousReverseLookup.Count > 0)
                {
                    if (reverseLookup == null)
                    {
                        reverseLookup = new Dictionary<string, string>(previousReverseLookup, StringComparer.Ordinal);
                    }
                    else
                    {
                        foreach (var kv in previousReverseLookup)
                        {
                            if (!reverseLookup.ContainsKey(kv.Key))
                                reverseLookup[kv.Key] = kv.Value;
                        }
                    }
                }

                if (languageLoaded && currentReverseLookup != null && currentReverseLookup.Count > 0)
                {
                    LastKnownReverseLookupMap = new Dictionary<string, string>(currentReverseLookup, StringComparer.Ordinal);
                }
                else if (!languageLoaded && previousReverseLookup != null && previousReverseLookup.Count > 0)
                {
                    reverseLookup = previousReverseLookup;
                }

                var tmps = UnityEngine.Object.FindObjectsOfType<TMP_Text>(true);
                foreach (var tmp in tmps)
                {
                    if (tmp == null)
                        continue;
                    if (skipTranslatorSettingsMenu && tmp.GetComponentInParent<TranslatorSettingsMenu>(true) != null)
                        continue;

                    string sourceText = tmp.text;
                    if (!string.IsNullOrEmpty(sourceText) && reverseLookup != null && reverseLookup.Count > 0)
                        sourceText = ResolveOriginalUsingLookupMap(sourceText, reverseLookup);

                    if (string.IsNullOrEmpty(sourceText))
                        continue;

                    string targetText = languageLoaded ? TranslateUsingLookupMap(sourceText, forwardLookup) : sourceText;
                    if (tmp.text != targetText)
                        tmp.text = targetText;
                }

                var uiTexts = UnityEngine.Object.FindObjectsOfType<Text>(true);
                foreach (var text in uiTexts)
                {
                    if (text == null)
                        continue;
                    if (skipTranslatorSettingsMenu && text.GetComponentInParent<TranslatorSettingsMenu>(true) != null)
                        continue;

                    string sourceText = text.text;
                    if (!string.IsNullOrEmpty(sourceText) && reverseLookup != null && reverseLookup.Count > 0)
                        sourceText = ResolveOriginalUsingLookupMap(sourceText, reverseLookup);

                    if (string.IsNullOrEmpty(sourceText))
                        continue;

                    string targetText = languageLoaded ? TranslateUsingLookupMap(sourceText, forwardLookup) : sourceText;
                    if (text.text != targetText)
                        text.text = targetText;
                }
            }
            catch (Exception e)
            {
                Logging.Warn($"[TranslationEngine] RefreshAllSceneTexts failed: {e.Message}");
            }
        }
    }
}
