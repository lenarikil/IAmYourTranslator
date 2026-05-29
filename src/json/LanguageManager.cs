using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using IAmYourTranslator;
using IAmYourTranslator.Patches;

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

            // Lazy-cached: only scan directories when the property is first accessed
            private bool? _hasAudio;
            private bool? _hasFonts;
            private bool? _hasTextures;

            public bool HasAudio
            {
                get
                {
                    if (!_hasAudio.HasValue)
                        _hasAudio = Directory.Exists(AudioDir) && Directory.EnumerateFiles(AudioDir, "*.*", SearchOption.AllDirectories).Any();
                    return _hasAudio.Value;
                }
            }

            public bool HasFonts
            {
                get
                {
                    if (!_hasFonts.HasValue)
                        _hasFonts = Directory.Exists(FontsDir) && Directory.EnumerateFiles(FontsDir, "*.*", SearchOption.AllDirectories).Any();
                    return _hasFonts.Value;
                }
            }

            public bool HasTextures
            {
                get
                {
                    if (!_hasTextures.HasValue)
                        _hasTextures = Directory.Exists(TexturesDir) && Directory.EnumerateFiles(TexturesDir, "*.*", SearchOption.AllDirectories).Any();
                    return _hasTextures.Value;
                }
            }

            public void InvalidateAssetCache()
            {
                _hasAudio = null;
                _hasFonts = null;
                _hasTextures = null;
            }
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

        public static event Action<string> OnLanguageLoaded;
        public static event Action OnLanguageUnloaded;

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

        // Batching for SaveCurrentLanguage - avoid frequent I/O operations
        private static bool _isDirty = false;
        private static DateTime _lastSaveTime = DateTime.UtcNow;
        private static readonly TimeSpan SAVE_BATCH_INTERVAL = TimeSpan.FromSeconds(2);

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
            // Invalidate lazy asset caches on existing summaries before discarding
            if (_cachedLanguages != null)
            {
                foreach (var lang in _cachedLanguages)
                {
                    lang?.Paths?.InvalidateAssetCache();
                }
            }
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

            // Mark as dirty instead of saving immediately - save will be batched
            _isDirty = true;
        }

        /// <summary>
        /// Force immediate save - used during plugin unload or emergency scenarios
        /// </summary>
        public static void SaveCurrentLanguageImmediate()
        {
            if (CurrentLanguage == null || string.IsNullOrEmpty(CurrentLanguageName))
            {
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
                Logging.Info($"[LanguageManager] Language '{CurrentLanguageName}' saved (immediate) to {paths.JsonPath}");
                _isDirty = false;
                _lastSaveTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                Logging.Error($"[LanguageManager] Error saving language: {ex}");
            }
        }

        /// <summary>
        /// Check if batched save is needed and perform if interval elapsed
        /// Call this from Plugin.Update() every frame
        /// </summary>
        public static void ProcessBatchedSave()
        {
            if (!_isDirty)
                return;

            var now = DateTime.UtcNow;
            if ((now - _lastSaveTime) < SAVE_BATCH_INTERVAL)
                return;

            SaveCurrentLanguageImmediate();
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

                CommonFunctions.ClearAllCaches(clearReverseLookup: false, destroyTextureAssets: false);
                ClearLanguageScopedPatchCaches();
                InvalidateLanguagesCache();
                
                // Rebuild reverse lookup map for faster O(1) lookups instead of O(n) foreach
                CommonFunctions.CaptureCurrentReverseLookupMap();
                
                RaiseLanguageLoaded(CurrentLanguageName);

                // Re-apply language font immediately after successful load
                Core.FontLifecycle.ApplyFontImmediateWithFallback();
                return true;
            }
            catch (Exception ex)
            {
                Logging.Error($"[LanguageManager] Error loading language from '{paths.JsonPath}': {ex}");
                return false;
            }
        }

        /// <summary>
        /// Reads only the metadata field from a JSON file without deserializing the entire object.
        /// Much faster than full deserialization for large JSON files.
        /// </summary>
        private static JsonFormat.Metadata ReadMetadataOnly(string jsonPath)
        {
            try
            {
                if (!File.Exists(jsonPath))
                    return null;

                using (var stream = File.OpenRead(jsonPath))
                using (var reader = new StreamReader(stream))
                using (var jsonReader = new JsonTextReader(reader))
                {
                    var serializer = JsonSerializer.Create(new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        MissingMemberHandling = MissingMemberHandling.Ignore
                    });

                    while (jsonReader.Read())
                    {
                        if (jsonReader.TokenType != JsonToken.PropertyName)
                            continue;

                        if (!string.Equals((string)jsonReader.Value, "metadata", StringComparison.Ordinal))
                        {
                            jsonReader.Skip();
                            continue;
                        }

                        if (!jsonReader.Read())
                            return null;

                        return serializer.Deserialize<JsonFormat.Metadata>(jsonReader);
                    }

                    return null;
                }
            }
            catch (Exception ex)
            {
                Logging.Warn($"[LanguageManager] Failed to read metadata from {jsonPath}: {ex.Message}");
                return null;
            }
        }

        public static void UnloadLanguage()
        {
            CommonFunctions.CaptureCurrentReverseLookupMap();
            CurrentLanguage = null;
            CurrentLanguageName = null;
            CurrentMetadata = null;
            CurrentSummary = null;
            CommonFunctions.UITextureReplacer.RestoreAll();
            CommonFunctions.ClearAllCaches(clearReverseLookup: false, destroyTextureAssets: true);
            ClearLanguageScopedPatchCaches();
            InvalidateLanguagesCache();
            RaiseLanguageUnloaded();
            Logging.Info("[LanguageManager] Language unloaded. Using original game texts.");
        }

        private static void ClearLanguageScopedPatchCaches()
        {
            UILevelCompleteOverviewDetails_Patch.ClearLanguageCache();
            UILevelCompleteOverviewCameraOption_Patch.ClearLanguageCache();
            UILevelCompleteTimeScoreBar_Patch.ClearLanguageCache();
            UILevelOverviewStartEndTag_Patch.ClearLanguageCache();
            LevelMusicProfilePatch.ClearCache();
        }

        private static void RaiseLanguageLoaded(string langCode)
        {
            try
            {
                OnLanguageLoaded?.Invoke(langCode);
            }
            catch (Exception ex)
            {
                Logging.Warn($"[LanguageManager] OnLanguageLoaded handler failed: {ex.Message}");
            }
        }

        private static void RaiseLanguageUnloaded()
        {
            try
            {
                OnLanguageUnloaded?.Invoke();
            }
            catch (Exception ex)
            {
                Logging.Warn($"[LanguageManager] OnLanguageUnloaded handler failed: {ex.Message}");
            }
        }

        private static LanguageSummary BuildSummary(LanguagePaths paths, JsonFormat.Metadata metaOverride = null)
        {
            try
            {
                JsonFormat.Metadata meta = metaOverride;
                if (meta == null && paths.HasJson)
                {
                    meta = ReadMetadataOnly(paths.JsonPath);
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
