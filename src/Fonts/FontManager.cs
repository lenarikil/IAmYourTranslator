using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using TMPro;
using UnityEngine;
using IAmYourTranslator.Utils;
using IAmYourTranslator.json;

namespace IAmYourTranslator.Fonts
{
    public static class FontManager
    {
        private sealed class OriginalTMPFontState
        {
            public TMP_FontAsset Font;
            public Material FontMaterial;
        }

        private static TMP_FontAsset cachedFileFont;
        private static DateTime cachedFileWriteTime = DateTime.MinValue;
        private static string cachedFilePath;

        private static readonly Dictionary<int, WeakReference<OriginalTMPFontState>> originalFontStatesByInstId = new Dictionary<int, WeakReference<OriginalTMPFontState>>();
        private static readonly Dictionary<int, TMP_Text> originalTextsByInstId = new Dictionary<int, TMP_Text>();

        public static TMP_FontAsset LoadFontFromFile(string fontPath)
        {
            if (!File.Exists(fontPath))
            {
                Debug.LogError($"TTF/OTF file not found: {fontPath}");
                return null;
            }

            Font systemFont = new Font(fontPath);
            if (systemFont == null)
            {
                Debug.LogError($"Failed to create Font from {fontPath}");
                return null;
            }

            TMP_FontAsset tmpFont = TMP_FontAsset.CreateFontAsset(systemFont);
            if (tmpFont == null)
            {
                Debug.LogError($"Failed to create TMP_FontAsset from {fontPath}");
                return null;
            }

            tmpFont.name = Path.GetFileNameWithoutExtension(fontPath);
            return tmpFont;
        }

        public static TMP_FontAsset GetCachedFont(string explicitPath = null)
        {
            if (Plugin.GlobalTMPFont != null && explicitPath == null)
                return Plugin.GlobalTMPFont;

            string langFontPath = null;
            if (LanguageManager.CurrentSummary?.Paths != null && !string.IsNullOrEmpty(LanguageManager.CurrentSummary.FontFile))
            {
                langFontPath = Path.Combine(LanguageManager.CurrentSummary.Paths.FontsDir, LanguageManager.CurrentSummary.FontFile);
            }

            string path = explicitPath;
            if (string.IsNullOrEmpty(path))
            {
                if (!string.IsNullOrEmpty(langFontPath) && File.Exists(langFontPath))
                {
                    path = langFontPath;
                }
                else if (!string.IsNullOrEmpty(Plugin.GlobalFontPath))
                {
                    path = Plugin.GlobalFontPath;
                }
                else if (LanguageManager.IsLoaded)
                {
                    path = Path.Combine(Paths.ConfigPath, "IAmYourTranslator", "fonts", "Jovanny Lemonad - Bender-Bold.otf");
                }
            }

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;

            var fi = new FileInfo(path);
            var lastWrite = fi.LastWriteTimeUtc;

            bool pathChanged = !string.Equals(cachedFilePath, path, StringComparison.OrdinalIgnoreCase);

            if (cachedFileFont == null || pathChanged || lastWrite > cachedFileWriteTime)
            {
                var tmpFont = LoadFontFromFile(path);
                if (tmpFont == null)
                    return null;

                cachedFileFont = tmpFont;
                cachedFileWriteTime = lastWrite;
                cachedFilePath = path;
            }

            return cachedFileFont;
        }

        public static void CacheOriginalFontState(TMP_Text tmp)
        {
            if (tmp == null)
                return;

            int instId = tmp.GetInstanceID();
            if (originalFontStatesByInstId.ContainsKey(instId))
                return;

            Material originalMaterialCopy = null;
            if (tmp.fontMaterial != null)
                originalMaterialCopy = new Material(tmp.fontMaterial);

            originalFontStatesByInstId[instId] = new WeakReference<OriginalTMPFontState>(new OriginalTMPFontState
            {
                Font = tmp.font,
                FontMaterial = originalMaterialCopy
            });
            originalTextsByInstId[instId] = tmp;
        }

        public static void CleanupOriginalFontCache()
        {
            var deadIds = new List<int>();
            foreach (var kv in originalFontStatesByInstId)
            {
                if (!kv.Value.TryGetTarget(out _))
                    deadIds.Add(kv.Key);
            }

            foreach (var id in deadIds)
            {
                originalFontStatesByInstId.Remove(id);
                originalTextsByInstId.Remove(id);
            }
        }

        public static void RestoreOriginalFonts()
        {
            CleanupOriginalFontCache();
            int restored = 0;

            foreach (var kv in originalFontStatesByInstId.ToList())
            {
                if (!kv.Value.TryGetTarget(out var state))
                    continue;

                if (!originalTextsByInstId.TryGetValue(kv.Key, out var tmp) || tmp == null)
                    continue;

                if (state.Font != null)
                    tmp.font = state.Font;
                if (state.FontMaterial != null)
                    tmp.fontMaterial = new Material(state.FontMaterial);

                tmp.ForceMeshUpdate();
                restored++;
            }

            Debug.Log($"[FontManager] Restored original fonts for {restored} TMP_Text components.");
        }

        public static void ApplyFontToTMP(TMP_Text tmp, TMP_FontAsset newFont)
        {
            if (tmp == null)
            {
                Debug.LogWarning("[FontManager] Target TMP_Text is null.");
                return;
            }

            if (newFont == null)
            {
                Debug.LogWarning("[FontManager] New TMP_FontAsset is null.");
                return;
            }

            CacheOriginalFontState(tmp);

            if (tmp.font == newFont)
                return;

            Material originalMaterial = tmp.fontMaterial;

            tmp.font = newFont;

            if (originalMaterial != null)
            {
                Material newMaterial = new Material(originalMaterial);

                if (newFont.atlasTexture != null)
                    newMaterial.SetTexture("_MainTex", newFont.atlasTexture);

                tmp.fontMaterial = newMaterial;
            }

            tmp.ForceMeshUpdate();

            Debug.Log($"[FontManager] Applied font '{newFont.name}' to '{tmp.name}' (preserved styles).");
        }

        public static void ApplyFontToAllTMP(TMP_FontAsset newFont)
        {
            if (newFont == null)
            {
                Debug.LogError("TMP_FontAsset is null, cannot apply.");
                return;
            }

            TextMeshProUGUI[] allTMP = CachingHelpers.FindObjectsOfTypeCached<TextMeshProUGUI>(true);
            int count = 0;

            foreach (var tmp in allTMP)
            {
                if (tmp == null || tmp.font == null)
                    continue;

                CacheOriginalFontState(tmp);

                var originalMaterial = tmp.fontMaterial;

                tmp.font = newFont;

                if (originalMaterial != null)
                {
                    Material newMat = new Material(originalMaterial);

                    if (newFont.atlasTexture != null)
                        newMat.SetTexture("_MainTex", newFont.atlasTexture);

                    tmp.fontMaterial = newMat;
                }

                tmp.ForceMeshUpdate();
                count++;
            }

            Debug.Log($"[FontManager] Applied font '{newFont.name}' to {count} TextMeshProUGUI components (preserved styles).");
        }

        public static void ReplaceFont(string fontPath)
        {
            var tmpFont = GetCachedFont(fontPath);
            if (tmpFont != null)
                ApplyFontToAllTMP(tmpFont);
        }
    }
}
