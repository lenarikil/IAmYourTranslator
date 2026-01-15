using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using BepInEx;
using BepInEx.Logging;

namespace IAmYourTranslator.json
{
    public static class LanguageManager
    {

        // Current language data
        public static JsonFormat CurrentLanguage { get; private set; }
        public static string CurrentLanguageName { get; private set; }

        // Folder where we store JSONs (under Paths.ConfigPath)
        // By default: <BepInEx>/config/IAmYourTranslator/languages/
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

        // Returns the names of all available languages (without extension)
        public static IEnumerable<string> GetAvailableLanguageNames()
        {
            EnsureLanguagesDirectory();
            if (!Directory.Exists(LanguagesDir))
                yield break;

            foreach (var file in Directory.GetFiles(LanguagesDir, "*.json"))
                yield return Path.GetFileNameWithoutExtension(file);
        }

        // Load language by name (e.g. "en-GB")
        public static bool LoadLanguage(string langName)
        {
            EnsureLanguagesDirectory();
            string filePath = Path.Combine(LanguagesDir, $"{langName}.json");
            return LoadLanguageFromFile(filePath);
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
                string filePath = Path.Combine(LanguagesDir, CurrentLanguageName + ".json");
                string json = JsonConvert.SerializeObject(CurrentLanguage, Formatting.Indented);
                File.WriteAllText(filePath, json);
                Logging.Info($"[LanguageManager] Language '{CurrentLanguageName}' saved to {filePath}");
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
        public static bool LoadLanguageFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Logging.Warn($"[LanguageManager] Language file not found: {filePath}");
                    return false;
                }

                string json = File.ReadAllText(filePath);
                JsonFormat data = JsonConvert.DeserializeObject<JsonFormat>(json);

                if (data == null)
                {
                    Logging.Error($"[LanguageManager] Deserialization returned null for file: {filePath}");
                    return false;
                }

                CurrentLanguage = data;
                CurrentLanguageName = Path.GetFileNameWithoutExtension(filePath);
                Logging.Info($"[LanguageManager] Loaded language '{CurrentLanguageName}' from {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                Logging.Error($"[LanguageManager] Error loading language from '{filePath}': {ex}");
                return false;
            }
        }

        // Check if language is loaded
        public static bool IsLoaded => CurrentLanguage != null;
    }
}
