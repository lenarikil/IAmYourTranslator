using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using BepInEx;


namespace IAmYourTranslator
{
    public static class AudioClipRawExtractor
    {
        /// <summary>
        /// Searches for and saves original audio data (fsb, bank, resS, etc.) by AudioClip name.
        /// </summary>
        public static bool TryDumpRawAudioData(AudioClip clip, string outputDir)
        {
            if (clip == null)
            {
                Logging.Error("[AudioDump] clip == null");
                return false;
            }

            string clipName = clip.name;
            string safeName = SanitizeFileName(clipName);
            Directory.CreateDirectory(outputDir);

            try
            {
                // === 1. Searching through possible game paths ===
                string gameDir = Path.GetDirectoryName(Application.dataPath) ?? ".";
                string[] searchDirs = {
                    gameDir,
                    Path.Combine(gameDir, "StreamingAssets"),
                    Path.Combine(gameDir, "resources"),
                    Path.Combine(gameDir, "res"),
                    Path.Combine(gameDir, "Data"),
                    Path.Combine(gameDir, "AssetBundles"),
                };

                string[] patterns = { "*.bank", "*.resS", "*.assets", "*.fsb", "*.ogg", "*.wav" };

                foreach (string dir in searchDirs.Where(Directory.Exists))
                {
                    foreach (string pattern in patterns)
                    {
                        var files = Directory.GetFiles(dir, pattern, SearchOption.AllDirectories);

                        foreach (string file in files)
                        {
                            // === 2. Quick search for clip name in binary data ===
                            if (FileContainsName(file, clipName))
                            {
                                string outFile = Path.Combine(outputDir, $"{safeName}_{Path.GetFileName(file)}");
                                File.Copy(file, outFile, true);
                                Logging.Warn($"[AudioDump] 💾 File found and copied '{file}' → '{outFile}'");
                                return true;
                            }
                        }
                    }
                }

                Logging.Warn($"[AudioDump] ❌ Could not find file associated with '{clipName}'");
                return false;
            }
            catch (Exception ex)
            {
                Logging.Error($"[AudioDump] Error while searching for '{clip.name}': {ex}");
                return false;
            }
        }

        /// <summary>
        /// Checks if the clip name appears inside the binary file (limit up to 8 MB).
        /// </summary>
        private static bool FileContainsName(string filePath, string clipName)
        {
            try
            {
                byte[] buffer = File.ReadAllBytes(filePath);
                if (buffer.Length > 8 * 1024 * 1024)
                    buffer = buffer.Take(8 * 1024 * 1024).ToArray(); // don't read huge banks

                string text = Encoding.UTF8.GetString(buffer);
                return text.IndexOf(clipName, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }


        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
