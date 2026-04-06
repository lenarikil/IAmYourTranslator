using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using IAmYourTranslator;

namespace IAmYourTranslator.json
{
    public static class LanguageManager
    {
        public class LanguagePaths
        {
            public string LangCode { get; set; }
            public string BaseDir => Path.Combine(Paths.ConfigPath, "IAmYourTranslator", "languages", LangCode);
            public string JsonPath => Path.Combine(BaseDir, $"{LangCode}.json");
            public string AudioDir => Path.Combine(BaseDir, "audio");
            public string FontsDir => Path.Combine(BaseDir, "fonts");
            public string TexturesDir => Path.Combine(BaseDir, "textures");
            public bool HasJson => File.Exists(JsonPath);
            public bool HasAudio => Directory.Exists(AudioDir) && Directory.EnumerateFiles(AudioDir, "*.*", SearchOption.AllDirectories).Any();
            public bool HasFonts => Directory.Exists(FontsDir) && Directory.EnumerateFiles(FontsDir, "*.*", SearchOption.AllDirectories).Any();
            public bool HasTextures => Directory.Exists(TexturesDir) && Directory.EnumerateFiles(TexturesDir, "*.*", SearchOption.AllDirectories).Any();
        }

        public class LanguageSummary
        {
            public string Code;
            public string DisplayName;
            public string Author;
            public string Version;
            public string MinimumModVersion;
            public string FontFile;
            public bool WarnIncompatible;
            public LanguagePaths Paths;
            public JsonFormat.Metadata Metadata;
        }

        // Current language data
        public static JsonFormat CurrentLanguage { get; private set; }
        public static string CurrentLanguageName { get; private set; }
        public static JsonFormat.Metadata CurrentMetadata { get; private set; }
        public static LanguageSummary CurrentSummary { get; private set; }

        // Folder where we store JSONs (under Paths.ConfigPath)
        // By default: <BepInEx>/config/IAmYourTranslator/languages/<code>/
        public static string LanguagesDir => Path.Combine(Paths.ConfigPath, "IAmYourTranslator", "languages");

        // Ensure that the folder exists
        public static void EnsureLanguagesDirectory()
        {
            try
            {
                if (!Directory.Exists(LanguagesDir))
                    Directory.CreateDirectory(LanguagesDir);
            }
            catch (Exception e)
            {
                Logging.Error($"[LanguageManager] Failed to create languages folder: {e}");
            }
        }

        // Cache for available languages to avoid repeated file system scans
        private static List<LanguageSummary> _cachedLanguages;
        private static DateTime _cacheTime;
        private static readonly TimeSpan LANGUAGES_CACHE_DURATION = TimeSpan.FromSeconds(5);

        public static IEnumerable<LanguageSummary> GetAvailableLanguages()
        {
            var now = DateTime.UtcNow;

            // Return cached results if still valid
            if (_cachedLanguages != null && (now - _cacheTime) < LANGUAGES_CACHE_DURATION)
            {
                foreach (var lang in _cachedLanguages)
                    yield return lang;
                yield break;
            }

            // Rebuild cache
            _cachedLanguages = new List<LanguageSummary>();
            EnsureLanguagesDirectory();
            if (!Directory.Exists(LanguagesDir))
                yield break;

            foreach (var dir in Directory.GetDirectories(LanguagesDir))
            {
                var code = Path.GetFileName(dir);
                var paths = new LanguagePaths { LangCode = code };
                var summary = BuildSummary(paths);
                if (summary != null)
                    _cachedLanguages.Add(summary);
            }

            _cacheTime = now;

            foreach (var lang in _cachedLanguages)
                yield return lang;
        }

        public static void InvalidateLanguagesCache()
        {
            _cachedLanguages = null;
            _cacheTime = DateTime.MinValue;
        }

        public static bool LoadLanguage(string langCode)
        {
            EnsureLanguagesDirectory();
            CommonFunctions.CaptureCurrentReverseLookupMap();

            var paths = new LanguagePaths { LangCode = langCode };
            if (!paths.HasJson)
            {
                Logging.Warn($"[LanguageManager] Language file not found for '{langCode}' at {paths.JsonPath}");
                return false;
            }

            // Invalidate cache since we're loading a new language
            InvalidateLanguagesCache();
            return LoadLanguageFromFile(paths);
        }

        public static void SaveCurrentLanguage()
        {
            if (CurrentLanguage == null || string.IsNullOrEmpty(CurrentLanguageName))
            {
                Logging.Warn("[LanguageManager] Nothing to save");
                return;
            }

            try
            {
                EnsureLanguagesDirectory();
                var paths = new LanguagePaths { LangCode = CurrentLanguageName };
                if (CurrentLanguage.metadata == null)
                    CurrentLanguage.metadata = new JsonFormat.Metadata { langName = CurrentLanguageName, langDisplayName = CurrentLanguageName };
                Directory.CreateDirectory(paths.BaseDir);
                string json = JsonConvert.SerializeObject(CurrentLanguage, Formatting.Indented);
                File.WriteAllText(paths.JsonPath, json);
                Logging.Info($"[LanguageManager] Language '{CurrentLanguageName}' saved to {paths.JsonPath}");
            }
            catch (Exception ex)
            {
                Logging.Error($"[LanguageManager] Error saving language: {ex}");
            }
        }
        
        public static Dictionary<string, List<string>> GetTranslations()
        {
            if (CurrentLanguage == null)
                return new Dictionary<string, List<string>>();

            return CurrentLanguage.timings ?? new Dictionary<string, List<string>>();
        }

        // Load language from a specific file
        public static bool LoadLanguageFromFile(LanguagePaths paths)
        {
            try
            {
                if (!File.Exists(paths.JsonPath))
                {
                    Logging.Warn($"[LanguageManager] Language file not found: {paths.JsonPath}");
                    return false;
                }

                string json = File.ReadAllText(paths.JsonPath);
                JsonFormat data = JsonConvert.DeserializeObject<JsonFormat>(json);

                if (data == null)
                {
                    Logging.Error($"[LanguageManager] Deserialization returned null for file: {paths.JsonPath}");
                    return false;
                }

                CurrentLanguage = data;
                CurrentLanguageName = paths.LangCode;
                CurrentMetadata = data.metadata ?? new JsonFormat.Metadata { langName = paths.LangCode, langDisplayName = paths.LangCode };
                CurrentSummary = BuildSummary(paths, data.metadata);

                Logging.Info($"[LanguageManager] Loaded language '{CurrentLanguageName}' from {paths.JsonPath}");

                // Invalidate cache after successful load
                InvalidateLanguagesCache();

                // Re-apply language font immediately after successful load
                Plugin.GetOrRecoverInstance()?.ApplyFontImmediateWithFallback();
                return true;
            }
            catch (Exception ex)
            {
                Logging.Error($"[LanguageManager] Error loading language from '{paths.JsonPath}': {ex}");
                return false;
            }
        }

        public static void UnloadLanguage()
        {
            CommonFunctions.CaptureCurrentReverseLookupMap();
            CurrentLanguage = null;
            CurrentLanguageName = null;
            CurrentMetadata = null;
            CurrentSummary = null;
            Logging.Info("[LanguageManager] Language unloaded. Using original game texts.");
        }

        private static LanguageSummary BuildSummary(LanguagePaths paths, JsonFormat.Metadata metaOverride = null)
        {
            try
            {
                JsonFormat.Metadata meta = metaOverride;
                if (meta == null && paths.HasJson)
                {
                    var raw = File.ReadAllText(paths.JsonPath);
                    meta = JsonConvert.DeserializeObject<JsonFormat>(raw)?.metadata;
                }

                meta ??= new JsonFormat.Metadata();
                if (string.IsNullOrEmpty(meta.langName))
                    meta.langName = paths.LangCode;
                // Use langDisplayName if available, otherwise use langName
                if (string.IsNullOrEmpty(meta.langDisplayName))
                    meta.langDisplayName = !string.IsNullOrEmpty(meta.langName) ? meta.langName : paths.LangCode;

                bool warn = false;
                if (!string.IsNullOrEmpty(meta.minimumModVersion))
                {
                    warn = CompareVersions(meta.minimumModVersion, PluginInfo.PLUGIN_VERSION) > 0;
                }

                return new LanguageSummary
                {
                    Code = paths.LangCode,
                    DisplayName = meta.langDisplayName,
                    Author = meta.langAuthor,
                    Version = meta.langVersion,
                    MinimumModVersion = meta.minimumModVersion,
                    FontFile = meta.fontFile,
                    WarnIncompatible = warn,
                    Paths = paths,
                    Metadata = meta
                };
            }
            catch (Exception e)
            {
                Logging.Warn($"[LanguageManager] Failed to build summary for {paths.LangCode}: {e.Message}");
                return null;
            }
        }

        private static int CompareVersions(string required, string current)
        {
            try
            {
                var vReq = new Version(required);
                var vCur = new Version(current);
                return vReq.CompareTo(vCur);
            }
            catch
            {
                // Fallback to string compare
                return string.Compare(required, current, StringComparison.Ordinal);
            }
        }

        // Check if language is loaded
        public static bool IsLoaded => CurrentLanguage != null;
    }
}
