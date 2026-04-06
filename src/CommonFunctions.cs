using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using TMPro;
using UnityEngine.UI;
using System;
using Sounds;
using UnityEngine.Networking;
using BepInEx;
using IAmYourTranslator.json;

namespace IAmYourTranslator
{
    public static class CommonFunctions
    {
        // Static caches using DateTime for reliable timing (Time.time = 0 in first frames)
        private static Dictionary<string, GameObject> rootObjectCache = new Dictionary<string, GameObject>();
        private static DateTime lastRootCacheTime = DateTime.MinValue;
        private static readonly TimeSpan ROOT_CACHE_DURATION = TimeSpan.FromSeconds(1f);

        private static Dictionary<(GameObject, string), GameObject> childCache = new Dictionary<(GameObject, string), GameObject>();
        private static DateTime lastChildCacheTime = DateTime.MinValue;
        private static readonly TimeSpan CHILD_CACHE_DURATION = TimeSpan.FromSeconds(0.5f);

        public static string PreviousHudMessage;
        private static readonly Dictionary<Text, string> OriginalUITextByComponent = new Dictionary<Text, string>();
        private static readonly Dictionary<TMP_Text, string> OriginalTMPTextByComponent = new Dictionary<TMP_Text, string>();
        private static Dictionary<string, string> LastKnownReverseLookupMap = new Dictionary<string, string>(StringComparer.Ordinal);
        private const string BonusTimeTagMarker = " <size=85%>(";
        private static readonly Regex TrailingCounterRegex = new Regex(
            "^(.*?)(\\s*[:：]?\\s*(?:\\[[0-9]+\\/[0-9]+\\]|\\([0-9]+\\/[0-9]+\\)|[0-9]+\\/[0-9]+))$",
            RegexOptions.Compiled);

        // Cache for FindObjectsOfType to avoid expensive calls
        private static readonly Dictionary<Type, UnityEngine.Object[]> findObjectsCache = new Dictionary<Type, UnityEngine.Object[]>();
        private static DateTime findObjectsCacheTime = DateTime.MinValue;
        private static readonly TimeSpan FIND_OBJECTS_CACHE_DURATION = TimeSpan.FromMilliseconds(500);

        public static T[] FindObjectsOfTypeCached<T>(bool includeInactive = false) where T : UnityEngine.Object
        {
            var type = typeof(T);
            var now = DateTime.UtcNow;

            if ((now - findObjectsCacheTime) < FIND_OBJECTS_CACHE_DURATION && findObjectsCache.TryGetValue(type, out var cached))
            {
                if (cached != null)
                    return cached as T[];
            }

            var result = UnityEngine.Object.FindObjectsOfType<T>(includeInactive);
            findObjectsCache[type] = result;
            findObjectsCacheTime = now;
            return result;
        }

        public static void InvalidateFindObjectsCache()
        {
            findObjectsCache.Clear();
            findObjectsCacheTime = DateTime.MinValue;
        }

        public static IEnumerator WaitforSeconds(float seconds)
        {
            yield return new WaitForSeconds(seconds);
        }

        public static GameObject GetInactiveRootObject(string objectName)
        {
            var now = DateTime.UtcNow;

            // Check cache first
            if ((now - lastRootCacheTime) < ROOT_CACHE_DURATION && rootObjectCache.TryGetValue(objectName, out GameObject cached))
            {
                if (cached != null)
                {
                    Logging.Info($"[CommonFunctions] Cache hit for GetInactiveRootObject: {objectName}");
                    return cached;
                }
                else
                    rootObjectCache.Remove(objectName);
            }

            // If the cache is outdated, clear it
            if ((now - lastRootCacheTime) >= ROOT_CACHE_DURATION)
            {
                rootObjectCache.Clear();
                lastRootCacheTime = now;
            }

            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in roots)
            {
                if (root.name == objectName)
                {
                    // Save it to the cache
                    rootObjectCache[objectName] = root;
                    return root;
                }
            }

            // If the object is not found, store null in the cache
            rootObjectCache[objectName] = null;
            return null;
        }

        public static string GetCurrentSceneName()
        {
            return SceneManager.GetActiveScene().name;
        }

        public static Scene GetCurrentScene()
        {
            return SceneManager.GetActiveScene();
        }

        public static GameObject GetGameObjectChild(GameObject parent, string childName)
        {
            if (parent == null) return null;

            var cacheKey = (parent, childName);
            var now = DateTime.UtcNow;

            // Checking the cache
            if ((now - lastChildCacheTime) < CHILD_CACHE_DURATION && childCache.TryGetValue(cacheKey, out GameObject cached))
            {
                if (cached != null)
                {
                    Logging.Info($"[CommonFunctions] Cache hit for GetGameObjectChild: {childName}");
                    return cached;
                }
                else
                    childCache.Remove(cacheKey);
            }

            // If the cache is outdated, clear it
            if ((now - lastChildCacheTime) >= CHILD_CACHE_DURATION)
            {
                childCache.Clear();
                lastChildCacheTime = now;
            }

            Transform child = parent.transform.Find(childName);
            if (child != null)
            {
                // Save it to the cache
                childCache[cacheKey] = child.gameObject;
                return child.gameObject;
            }

            // If the object is not found, store null in the cache
            childCache[cacheKey] = null;
            return null;
        }

        public static Transform RecursiveFindChild(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                    return child;

                Transform result = RecursiveFindChild(child, childName);
                if (result != null)
                    return result;
            }
            return null;
        }

        public static IEnumerable<CodeInstruction> IL(params (OpCode, object)[] instructions)
        {
            return instructions.Select(i => new CodeInstruction(i.Item1, i.Item2)).ToList();
        }

        public static GameObject GetObject(string path)
        {
            string rootPath, restPath = null;

            if (!path.Contains('/'))
                rootPath = path;
            else
            {
                var pathParts = path.Split(new[] { '/' }, 2);
                rootPath = pathParts[0];
                restPath = pathParts[1];
            }

            // Get ALL root objects, even inactive ones
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                Logging.Warn($"[CommonFunctions] GetObject: Scene is not valid, returning null for '{path}'");
                return null;
            }

            GameObject[] roots;
            try
            {
                roots = activeScene.GetRootGameObjects();
            }
            catch (Exception e)
            {
                Logging.Warn($"[CommonFunctions] GetObject: Failed to get root objects: {e.Message}");
                return null;
            }

            foreach (var root in roots)
            {
                if (root.name != rootPath)
                    continue;

                if (restPath == null)
                    return root;

                var result = FindChildRecursive(root.transform, restPath);
                if (result != null)
                    return result.gameObject;
            }

            return null;
        }

        private static Transform FindChildRecursive(Transform parent, string path)
        {

            // Support for paths of the form "A/B/C"
            var split = path.Split('/');
            return FindRecursiveInternal(parent, split, 0);
        }

        private static Transform FindRecursiveInternal(Transform current, string[] split, int index)
        {
            if (current == null || index >= split.Length)
                return current;

            var child = current.Find(split[index]);
            if (child == null)
            {

                // Trying to manually traverse all children (in case the object is disabled)
                foreach (Transform t in current)
                {
                    if (t.name == split[index])
                    {
                        child = t;
                        break;
                    }
                }
            }

            return FindRecursiveInternal(child, split, index + 1);
        }

        public static class TMPFontReplacer
        {
            private sealed class OriginalTMPFontState
            {
                public TMP_FontAsset Font;
                public Material FontMaterial;
            }

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

            // Cached access to the font considering the global font from Plugin
            private static TMP_FontAsset cachedFileFont;
            private static System.DateTime cachedFileWriteTime = System.DateTime.MinValue;
            private static string cachedFilePath;

            // Original state snapshot for restoration in "English (Original)" mode.
            // Using WeakReference to allow Unity objects to be garbage collected
            private static readonly Dictionary<int, WeakReference<OriginalTMPFontState>> originalFontStatesByInstId = new Dictionary<int, WeakReference<OriginalTMPFontState>>();
            private static readonly Dictionary<int, TMP_Text> originalTextsByInstId = new Dictionary<int, TMP_Text>();

            public static TMP_FontAsset GetCachedFont(string explicitPath = null)
            {
                // If the plugin has preloaded the global font, use it (unless explicit override)
                if (Plugin.GlobalTMPFont != null && explicitPath == null)
                    return Plugin.GlobalTMPFont;

                // Prefer language-specific font file when present
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
                        // Fallback font is allowed only in localized mode.
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

            private static void CacheOriginalFontState(TMP_Text tmp)
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

            private static void CleanupOriginalFontCache()
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

                Debug.Log($"[TMPFontReplacer] Restored original fonts for {restored} TMP_Text components.");
            }

            public static void ApplyFontToTMP(TMP_Text tmp, TMP_FontAsset newFont)
            {
                if (tmp == null)
                {
                    Debug.LogWarning("[TMPFontReplacer] Target TMP_Text is null.");
                    return;
                }

                if (newFont == null)
                {
                    Debug.LogWarning("[TMPFontReplacer] New TMP_FontAsset is null.");
                    return;
                }

                CacheOriginalFontState(tmp);

                if (tmp.font == newFont)
                    return; // already applied

                // Save the original material (it stores effects — shadow, outline, underlay)
                Material originalMaterial = tmp.fontMaterial;

                // Assign a new font
                tmp.font = newFont;

                if (originalMaterial != null)
                {
                    // Create a copy of the material to preserve visual styles
                    Material newMaterial = new Material(originalMaterial);

                    // Change the font texture in the material
                    if (newFont.atlasTexture != null)
                        newMaterial.SetTexture("_MainTex", newFont.atlasTexture);

                    // Assign the updated material
                    tmp.fontMaterial = newMaterial;
                }

                // Force update the text
                tmp.ForceMeshUpdate();

                Debug.Log($"[TMPFontReplacer] Applied font '{newFont.name}' to '{tmp.name}' (preserved styles).");
            }


            public static void ApplyFontToAllTMP(TMP_FontAsset newFont)
            {
                if (newFont == null)
                {
                    Debug.LogError("TMP_FontAsset is null, cannot apply.");
                    return;
                }

                TextMeshProUGUI[] allTMP = UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>(true);
                int count = 0;

                foreach (var tmp in allTMP)
                {
                    if (tmp == null || tmp.font == null)
                        continue;

                    CacheOriginalFontState(tmp);

                    // Save the original material
                    var originalMaterial = tmp.fontMaterial;

                    // Assigning a new font
                    tmp.font = newFont;

                    if (originalMaterial != null)
                    {
                        // Creating a copy of the material to save the styles
                        Material newMat = new Material(originalMaterial);

                        // Updating the font inside the material
                        if (newFont.atlasTexture != null)
                            newMat.SetTexture("_MainTex", newFont.atlasTexture);

                        // Assigning updated material
                        tmp.fontMaterial = newMat;
                    }

                    tmp.ForceMeshUpdate();
                    count++;
                }

                Debug.Log($"[TMPFontReplacer] Applied font '{newFont.name}' to {count} TextMeshProUGUI components (preserved styles).");
            }

            public static void ReplaceFont(string fontPath)
            {
                var tmpFont = GetCachedFont(fontPath);
                if (tmpFont != null)
                    ApplyFontToAllTMP(tmpFont);
            }
        }

        public static class UITextureReplacer
        {
            // Using WeakReference to allow Unity objects to be garbage collected
            private static readonly Dictionary<int, WeakReference<Texture>> originalRawTexturesByInstId = new Dictionary<int, WeakReference<Texture>>();
            private static readonly Dictionary<int, WeakReference<Sprite>> originalImageSpritesByInstId = new Dictionary<int, WeakReference<Sprite>>();

            public static Texture2D LoadTextureFromFile(string filePath, bool invertAlpha = false)
            {
                if (!File.Exists(filePath))
                {
                    Debug.LogError($"[UITextureReplacer] File not found: {filePath}");
                    return null;
                }

                byte[] data = File.ReadAllBytes(filePath);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(data))
                {
                    Debug.LogError($"[UITextureReplacer] Failed to load image: {filePath}");
                    return null;
                }

                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.name = Path.GetFileNameWithoutExtension(filePath);

                if (invertAlpha)
                    InvertAlphaMakeWhiteBackground(tex);

                return tex;
            }

            private static void CacheOriginal(RawImage raw)
            {
                if (raw == null)
                    return;

                int instId = raw.GetInstanceID();
                if (originalRawTexturesByInstId.ContainsKey(instId))
                    return;

                originalRawTexturesByInstId[instId] = new WeakReference<Texture>(raw.texture);
            }

            private static void CacheOriginal(Image img)
            {
                if (img == null)
                    return;

                int instId = img.GetInstanceID();
                if (originalImageSpritesByInstId.ContainsKey(instId))
                    return;

                originalImageSpritesByInstId[instId] = new WeakReference<Sprite>(img.sprite);
            }

            private static void CleanupTextureCaches()
            {
                var deadRawIds = new List<int>();
                foreach (var kv in originalRawTexturesByInstId)
                {
                    if (!kv.Value.TryGetTarget(out _))
                        deadRawIds.Add(kv.Key);
                }

                foreach (var id in deadRawIds)
                    originalRawTexturesByInstId.Remove(id);

                var deadImgIds = new List<int>();
                foreach (var kv in originalImageSpritesByInstId)
                {
                    if (!kv.Value.TryGetTarget(out _))
                        deadImgIds.Add(kv.Key);
                }

                foreach (var id in deadImgIds)
                    originalImageSpritesByInstId.Remove(id);
            }

            public static bool RestoreOn(GameObject target)
            {
                if (target == null)
                    return false;

                CleanupTextureCaches();
                bool restored = false;

                RawImage raw = target.GetComponent<RawImage>();
                if (raw != null && originalRawTexturesByInstId.TryGetValue(raw.GetInstanceID(), out var rawWeakRef) && rawWeakRef.TryGetTarget(out var originalTexture))
                {
                    raw.texture = originalTexture;
                    restored = true;
                }

                Image img = target.GetComponent<Image>();
                if (img != null && originalImageSpritesByInstId.TryGetValue(img.GetInstanceID(), out var imgWeakRef) && imgWeakRef.TryGetTarget(out var originalSprite))
                {
                    img.sprite = originalSprite;
                    restored = true;
                }

                if (restored)
                    Debug.Log($"[UITextureReplacer] Restored original texture/sprite on '{target.name}'.");

                return restored;
            }

            public static void RestoreAll()
            {
                CleanupTextureCaches();
                int restored = 0;

                foreach (var kv in originalRawTexturesByInstId.ToList())
                {
                    if (!kv.Value.TryGetTarget(out var originalTexture))
                        continue;

                    // Find the RawImage by instance ID
                    var rawImages = UnityEngine.Object.FindObjectsOfType<RawImage>(true);
                    foreach (var raw in rawImages)
                    {
                        if (raw.GetInstanceID() == kv.Key)
                        {
                            raw.texture = originalTexture;
                            restored++;
                            break;
                        }
                    }
                }

                foreach (var kv in originalImageSpritesByInstId.ToList())
                {
                    if (!kv.Value.TryGetTarget(out var originalSprite))
                        continue;

                    // Find the Image by instance ID
                    var images = UnityEngine.Object.FindObjectsOfType<Image>(true);
                    foreach (var img in images)
                    {
                        if (img.GetInstanceID() == kv.Key)
                        {
                            img.sprite = originalSprite;
                            restored++;
                            break;
                        }
                    }
                }

                Debug.Log($"[UITextureReplacer] Restored original UI textures/sprites on {restored} components.");
            }

            // Inverts alpha and turns the background white
            private static void InvertAlphaMakeWhiteBackground(Texture2D tex)
            {
                if (tex == null) return;

                Color32[] pixels = tex.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                {
                    byte oldA = pixels[i].a;
                    byte newA = (byte)(255 - oldA);

                    pixels[i].r = 255;
                    pixels[i].g = 255;
                    pixels[i].b = 255;
                    pixels[i].a = newA;
                }

                tex.SetPixels32(pixels);
                tex.Apply();
            }

            public static void ApplyTo(GameObject target, string filePath, bool invertAlpha = false)
            {
                if (target == null)
                {
                    Debug.LogWarning("[UITextureReplacer] Target GameObject is null.");
                    return;
                }

                // Try to find the RawImage component.
                RawImage raw = target.GetComponent<RawImage>();

                // Or a regular Image
                Image img = target.GetComponent<Image>();
                if (raw == null && img == null)
                {
                    Debug.LogWarning($"[UITextureReplacer] GameObject '{target.name}' has no Image or RawImage component.");
                    return;
                }

                if (string.IsNullOrEmpty(filePath))
                {
                    RestoreOn(target);
                    return;
                }

                if (!File.Exists(filePath))
                {
                    Debug.LogWarning($"[UITextureReplacer] Texture file not found for '{target.name}': {filePath}. Restoring original.");
                    RestoreOn(target);
                    return;
                }

                Texture2D tex = LoadTextureFromFile(filePath, invertAlpha);
                if (tex == null)
                {
                    RestoreOn(target);
                    return;
                }

                if (raw != null)
                {
                    CacheOriginal(raw);
                    raw.texture = tex;
                    Debug.Log($"[UITextureReplacer] Applied texture '{tex.name}' to RawImage on '{target.name}' (invertAlpha={invertAlpha}).");
                    return;
                }

                CacheOriginal(img);

                Sprite sprite = Sprite.Create(
                    tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    100f
                );

                sprite.name = tex.name;
                img.sprite = sprite;

                Debug.Log($"[UITextureReplacer] Applied sprite '{sprite.name}' to Image on '{target.name}' (invertAlpha={invertAlpha}).");
            }
        }


    public static class AudioClipReplacer
        {
            private static readonly string[] ReplacementExtensions = { ".wav", ".ogg" };
            private static readonly Dictionary<string, Dictionary<string, string>> ReplacementIndexByDirectory =
                new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            public static bool TryFindReplacementAudioFile(string audioDir, string clipName, out string filePath)
            {
                filePath = null;

                if (string.IsNullOrWhiteSpace(audioDir) || string.IsNullOrWhiteSpace(clipName))
                    return false;
                if (!Directory.Exists(audioDir))
                    return false;

                string safeName = SanitizeFileNameForPath(clipName);
                if (TryFindByBaseName(audioDir, clipName, out filePath))
                    return true;
                if (!string.Equals(safeName, clipName, StringComparison.Ordinal) &&
                    TryFindByBaseName(audioDir, safeName, out filePath))
                {
                    return true;
                }

                var index = GetOrBuildReplacementIndex(audioDir);
                string normalized = NormalizeAudioKey(clipName);
                if (!string.IsNullOrEmpty(normalized) && index.TryGetValue(normalized, out filePath))
                    return true;

                if (!string.Equals(safeName, clipName, StringComparison.Ordinal))
                {
                    string normalizedSafe = NormalizeAudioKey(safeName);
                    if (!string.IsNullOrEmpty(normalizedSafe) && index.TryGetValue(normalizedSafe, out filePath))
                        return true;
                }

                return false;
            }

            private static bool TryFindByBaseName(string audioDir, string baseName, out string filePath)
            {
                filePath = null;
                if (string.IsNullOrWhiteSpace(baseName))
                    return false;

                foreach (var ext in ReplacementExtensions)
                {
                    string candidate = Path.Combine(audioDir, baseName + ext);
                    if (File.Exists(candidate))
                    {
                        filePath = candidate;
                        return true;
                    }
                }

                return false;
            }

            private static Dictionary<string, string> GetOrBuildReplacementIndex(string audioDir)
            {
                if (ReplacementIndexByDirectory.TryGetValue(audioDir, out var cached))
                    return cached;

                var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    foreach (var file in Directory.EnumerateFiles(audioDir, "*.*", SearchOption.AllDirectories))
                    {
                        string ext = Path.GetExtension(file);
                        if (!ext.Equals(".wav", StringComparison.OrdinalIgnoreCase) &&
                            !ext.Equals(".ogg", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        string name = Path.GetFileNameWithoutExtension(file);
                        if (string.IsNullOrWhiteSpace(name))
                            continue;

                        string key = NormalizeAudioKey(name);
                        if (string.IsNullOrEmpty(key) || index.ContainsKey(key))
                            continue;

                        index[key] = file;
                    }
                }
                catch (Exception e)
                {
                    Logging.Warn($"[AudioClipReplacer] Failed to build replacement index for '{audioDir}': {e.Message}");
                }

                ReplacementIndexByDirectory[audioDir] = index;
                return index;
            }

            private static string NormalizeAudioKey(string input)
            {
                if (string.IsNullOrWhiteSpace(input))
                    return null;

                var sb = new System.Text.StringBuilder(input.Length);
                foreach (char ch in input)
                {
                    if (char.IsLetterOrDigit(ch))
                        sb.Append(char.ToUpperInvariant(ch));
                }

                return sb.Length > 0 ? sb.ToString() : null;
            }

            private static string SanitizeFileNameForPath(string name)
            {
                if (string.IsNullOrEmpty(name))
                    return name;

                foreach (char c in Path.GetInvalidFileNameChars())
                    name = name.Replace(c, '_');

                return name;
            }

            public static AudioClip CreateDecompressedCopy(AudioClip original)
            {
                if (original == null) return null;

                if (original.loadType == AudioClipLoadType.DecompressOnLoad)
                    return original;

                float[] samples = new float[original.samples * original.channels];
                original.GetData(samples, 0);

                AudioClip decompressed = AudioClip.Create(
                    original.name + "_decompressed",
                    original.samples,
                    original.channels,
                    original.frequency,
                    false
                );
                decompressed.SetData(samples, 0);
                return decompressed;
            }

            public static void ExportAudioClipToWav(AudioClip clip, string filePath)
            {
                if (clip == null) return;

                try
                {
                    clip = CreateDecompressedCopy(clip);

                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                    using (var fs = new FileStream(filePath, FileMode.Create))
                    {
                        int headerSize = 44;
                        fs.Seek(headerSize, SeekOrigin.Begin);

                        float[] samples = new float[clip.samples * clip.channels];
                        clip.GetData(samples, 0);

                        short[] intData = new short[samples.Length];
                        byte[] bytesData = new byte[samples.Length * 2];

                        for (int i = 0; i < samples.Length; i++)
                        {
                            intData[i] = (short)(Mathf.Clamp(samples[i], -1f, 1f) * 32767);
                            BitConverter.GetBytes(intData[i]).CopyTo(bytesData, i * 2);
                        }

                        fs.Write(bytesData, 0, bytesData.Length);

                        fs.Seek(0, SeekOrigin.Begin);
                        byte[] header = CreateWavHeader(clip, bytesData.Length);
                        fs.Write(header, 0, header.Length);
                    }

                    Debug.Log("[AudioClipReplacer] WAV exported: " + filePath);
                }
                catch (Exception e)
                {
                    Logging.Error($"[AudioClipReplacer] Failed to export WAV to {filePath}: {e}");
                }
            }

            public static void ExportAudioClipToOgg(AudioClip clip, string filePath)
            {
                if (clip == null) return;

                string tempWav = null;
                try
                {
                    clip = CreateDecompressedCopy(clip);
                    tempWav = Path.Combine(Path.GetTempPath(), clip.name + ".wav");
                    ExportAudioClipToWav(clip, tempWav);

                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                    var ffmpeg = new System.Diagnostics.Process();
                    ffmpeg.StartInfo.FileName = "ffmpeg";
                    ffmpeg.StartInfo.Arguments = $"-y -i \"{tempWav}\" -c:a libvorbis \"{filePath}\"";
                    ffmpeg.StartInfo.CreateNoWindow = true;
                    ffmpeg.StartInfo.UseShellExecute = false;
                    ffmpeg.Start();
                    ffmpeg.WaitForExit();

                    Debug.Log("[AudioClipReplacer] OGG exported: " + filePath);
                }
                catch (Exception e)
                {
                    Logging.Error($"[AudioClipReplacer] Error exporting OGG: {e}");
                }
                finally
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(tempWav) && File.Exists(tempWav))
                            File.Delete(tempWav);
                    }
                    catch { }
                }
            }

            public static AudioClip LoadAudioClip(string filePath)
            {
                if (!File.Exists(filePath))
                {
                    Debug.LogWarning("[AudioClipReplacer] File not found: " + filePath);
                    return null;
                }

                AudioType type = filePath.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)
                    ? AudioType.OGGVORBIS
                    : AudioType.WAV;

                using (var www = UnityWebRequestMultimedia.GetAudioClip("file://" + filePath, type))
                {
                    var request = www.SendWebRequest();
                    while (!request.isDone) { }

                    if (www.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError("[AudioClipReplacer] Loading error: " + www.error);
                        return null;
                    }

                    AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                    clip.name = Path.GetFileNameWithoutExtension(filePath);
                    return clip;
                }
            }

            public static void ReplaceAudioClip(UnityEngine.AudioSource source, string filePath)
            {
                if (source == null) return;
                AudioClip clip = LoadAudioClip(filePath);
                if (clip == null) return;

                source.clip = clip;
                source.Play();
                Debug.Log("[AudioClipReplacer] AudioSource playing: " + clip.name);
            }

            public static void ReplaceSoundObjectClip(SoundObject soundObj, string filePath, string name)
            {
                if (soundObj == null)
                {
                    Debug.LogWarning("[AudioClipReplacer] SoundObject " + name + " == null");
                    return;
                }

                AudioClip clip = LoadAudioClip(filePath);
                if (clip == null) return;

                soundObj.SetClip(clip);
                Debug.Log("[AudioClipReplacer] " + name + " replaced with: " + clip.name);
            }

            private static byte[] CreateWavHeader(AudioClip clip, int dataLength)
            {
                int hz = clip.frequency;
                int channels = clip.channels;
                int byteRate = hz * channels * 2;
                byte[] header = new byte[44];

                System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(header, 0);
                BitConverter.GetBytes(dataLength + 36).CopyTo(header, 4);
                System.Text.Encoding.ASCII.GetBytes("WAVE").CopyTo(header, 8);
                System.Text.Encoding.ASCII.GetBytes("fmt ").CopyTo(header, 12);
                BitConverter.GetBytes(16).CopyTo(header, 16);
                BitConverter.GetBytes((short)1).CopyTo(header, 20);
                BitConverter.GetBytes((short)channels).CopyTo(header, 22);
                BitConverter.GetBytes(hz).CopyTo(header, 24);
                BitConverter.GetBytes(byteRate).CopyTo(header, 28);
                BitConverter.GetBytes((short)(channels * 2)).CopyTo(header, 32);
                BitConverter.GetBytes((short)16).CopyTo(header, 34);
                System.Text.Encoding.ASCII.GetBytes("data").CopyTo(header, 36);
                BitConverter.GetBytes(dataLength).CopyTo(header, 40);

                return header;
            }
        }

        /// <summary>
        /// Translates text content and saves missing translations to JSON.
        /// Works with UnityEngine.UI.Text components.
        /// </summary>
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
        /// Clears the cached original text for a specific TMP_Text component.
        /// Use this when switching languages to force re-translation.
        /// </summary>
        public static void ClearOriginalTextCache(TMP_Text tmpComponent)
        {
            if (tmpComponent == null)
                return;
            OriginalTMPTextByComponent.Remove(tmpComponent);
        }

        private static bool TryResolveOriginalKeyFromValue(string currentText, Dictionary<string, string> translationDict, out string resolvedOriginal)
        {
            resolvedOriginal = null;
            if (string.IsNullOrEmpty(currentText) || translationDict == null || translationDict.Count == 0)
                return false;

            // If text already equals a known source key, keep it as source.
            if (translationDict.ContainsKey(currentText))
            {
                resolvedOriginal = currentText;
                return true;
            }

            // Fallback: reverse-lookup translated value -> original key.
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

        /// <summary>
        /// Resolves the canonical source key for a currently displayed localized value.
        /// Returns the input text when reverse resolution is not possible.
        /// </summary>
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

        /// <summary>
        /// Captures reverse lookup map for the currently loaded language.
        /// Keeps previous-language source key resolution available during live language switches.
        /// </summary>
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
                Logging.Warn($"[CommonFunctions] CaptureCurrentReverseLookupMap failed: {e.Message}");
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

            CleanupOriginalTextCaches();
            string sourceText = ResolveOriginalText(textComponent, originalText, translationDict);
            if (string.IsNullOrEmpty(sourceText))
                return;

            if (translationDict == null)
            {
                // Original mode: force immediate reset to source text.
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

            // Add original as placeholder if missing
            if (!translationDict.ContainsKey(sourceText))
            {
                translationDict[sourceText] = sourceText;
                LanguageManager.SaveCurrentLanguage();
                Logging.Warn($"{logPrefix} Added missing translation key: '{sourceText}'");
            }

            if (textComponent.text != sourceText)
                textComponent.text = sourceText;
        }

        /// <summary>
        /// Translates text content and saves missing translations to JSON.
        /// Works with TextMeshPro.TextMeshProUGUI components.
        /// </summary>
        public static void TranslateTextAndSaveIfMissing(TMP_Text tmpComponent, string originalText, Dictionary<string, string> translationDict, string logPrefix = "")
        {
            if (tmpComponent == null)
                return;

            CleanupOriginalTextCaches();
            string sourceText = ResolveOriginalText(tmpComponent, originalText, translationDict);
            if (string.IsNullOrEmpty(sourceText))
                return;

            if (translationDict == null)
            {
                // Original mode: force immediate reset to source text.
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

            // Add original as placeholder if missing
            if (!translationDict.ContainsKey(sourceText))
            {
                translationDict[sourceText] = sourceText;
                LanguageManager.SaveCurrentLanguage();
                Logging.Warn($"{logPrefix} Added missing TMP translation key: '{sourceText}'");
            }

            // Always set text to sourceText (original English) if translation not found
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

        private static List<Dictionary<string, string>> GetActiveLookupDictionaries()
        {
            var lookup = new List<Dictionary<string, string>>(24);
            var lang = LanguageManager.CurrentLanguage;
            if (lang == null)
                return lookup;

            var seen = new HashSet<Dictionary<string, string>>();

            // Keep a stable priority for the most frequently visible UI text buckets.
            AddLookupDictionary(lookup, seen, lang.settings);
            AddLookupDictionary(lookup, seen, lang.hardCoded);
            AddLookupDictionary(lookup, seen, lang.Hints);

            var type = lang.GetType();

            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!prop.CanRead || prop.GetIndexParameters().Length != 0)
                    continue;

                if (prop.PropertyType == typeof(Dictionary<string, string>))
                {
                    var dict = prop.GetValue(lang, null) as Dictionary<string, string>;
                    AddLookupDictionary(lookup, seen, dict);
                    continue;
                }

                if (prop.PropertyType == typeof(Dictionary<string, Dictionary<string, string>>))
                {
                    var nested = prop.GetValue(lang, null) as Dictionary<string, Dictionary<string, string>>;
                    AddNestedLookupDictionaries(lookup, seen, nested);
                }
            }

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.FieldType == typeof(Dictionary<string, string>))
                {
                    var dict = field.GetValue(lang) as Dictionary<string, string>;
                    AddLookupDictionary(lookup, seen, dict);
                    continue;
                }

                if (field.FieldType == typeof(Dictionary<string, Dictionary<string, string>>))
                {
                    var nested = field.GetValue(lang) as Dictionary<string, Dictionary<string, string>>;
                    AddNestedLookupDictionaries(lookup, seen, nested);
                }
            }

            return lookup;
        }

        private static Dictionary<string, string> BuildForwardLookupMap(List<Dictionary<string, string>> lookupDicts)
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

        private static Dictionary<string, string> BuildReverseLookupMap(List<Dictionary<string, string>> lookupDicts)
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

        private static string TranslateUsingLookupMap(string sourceText, Dictionary<string, string> forwardLookup)
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

        private static string ResolveOriginalUsingLookupMap(string displayedText, Dictionary<string, string> reverseLookup)
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
                // Clear all caches to force re-capture of source texts
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

                // During live language switches the scene can still contain old translated values.
                // Merge previous reverse map so we can resolve old language text back to source keys.
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

                    // Get source text from reverse lookup or current text
                    string sourceText = tmp.text;
                    if (!string.IsNullOrEmpty(sourceText) && reverseLookup != null && reverseLookup.Count > 0)
                        sourceText = ResolveOriginalUsingLookupMap(sourceText, reverseLookup);

                    if (string.IsNullOrEmpty(sourceText))
                        continue;

                    // Translate if language is loaded
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

                    // Get source text from reverse lookup or current text
                    string sourceText = text.text;
                    if (!string.IsNullOrEmpty(sourceText) && reverseLookup != null && reverseLookup.Count > 0)
                        sourceText = ResolveOriginalUsingLookupMap(sourceText, reverseLookup);

                    if (string.IsNullOrEmpty(sourceText))
                        continue;

                    // Translate if language is loaded
                    string targetText = languageLoaded ? TranslateUsingLookupMap(sourceText, forwardLookup) : sourceText;
                    if (text.text != targetText)
                        text.text = targetText;
                }
            }
            catch (Exception e)
            {
                Logging.Warn($"[CommonFunctions] RefreshAllSceneTexts failed: {e.Message}");
            }
        }
	
        /// <summary>
        /// Applies TMP font to all TextMeshProUGUI components in a target and its children.
        /// </summary>
        public static void ApplyFontToAllChildrenTMP(Component target, TMP_FontAsset font, string logPrefix = "")
        {
            if (target == null || font == null)
            {
                if (font == null)
                    Logging.Warn($"{logPrefix} Font is null, cannot apply");
                return;
            }

            try
            {
                var allTMPs = target.GetComponentsInChildren<TMP_Text>(true);
                if (allTMPs == null || allTMPs.Length == 0)
                    return;

                int appliedCount = 0;
                foreach (var tmp in allTMPs)
                {
                    if (tmp != null && tmp.font != font)
                    {
                        TMPFontReplacer.ApplyFontToTMP(tmp, font);
                        appliedCount++;
                    }
                }

                Logging.Info($"{logPrefix} Applied global font to {appliedCount} TMP_Text children");
            }
            catch (Exception e)
            {
                Logging.Error($"{logPrefix} Error in ApplyFontToAllChildrenTMP: {e}");
            }
        }

        /// <summary>
        /// Disables multiple GameObjects (e.g., UI panels).
        /// </summary>
        public static void DisableGameObjectPanels(params GameObject[] panels)
        {
            if (panels == null || panels.Length == 0)
                return;

            foreach (var panel in panels)
            {
                if (panel != null)
                {
                    panel.SetActive(false);
                    Logging.Info($"[CommonFunctions] Disabled panel: {panel.name}");
                }
            }
        }

        /// <summary>
        /// Stretches a RectTransform horizontally to full width while preserving Y-axis anchors/offsets.
        /// </summary>
        public static void StretchRectTransformHorizontal(RectTransform rt)
        {
            if (rt == null)
                return;

            try
            {
                rt.anchorMin = new Vector2(0f, rt.anchorMin.y);
                rt.anchorMax = new Vector2(1f, rt.anchorMax.y);
                rt.offsetMin = new Vector2(0f, rt.offsetMin.y);
                rt.offsetMax = new Vector2(0f, rt.offsetMax.y);
                rt.sizeDelta = new Vector2(0f, rt.sizeDelta.y);
                Logging.Info($"[CommonFunctions] Stretched RectTransform horizontally: {rt.name}");
            }
            catch (Exception e)
            {
                Logging.Error($"[CommonFunctions] Error stretching RectTransform: {e}");
            }
        }

        /// <summary>
        /// Finds a component by trying multiple fallback paths.
        /// Returns the first component found, or null if none match.
        /// </summary>
        public static T FindComponentWithFallback<T>(Component startComponent, params string[] fallbackPaths) where T : Component
        {
            if (startComponent == null || fallbackPaths == null || fallbackPaths.Length == 0)
                return null;

            foreach (var path in fallbackPaths)
            {
                if (string.IsNullOrEmpty(path))
                    continue;

                try
                {
                    Transform found = RecursiveFindChild(startComponent.transform, path);
                    if (found != null)
                    {
                        var component = found.GetComponent<T>();
                        if (component != null)
                        {
                            Logging.Info($"[CommonFunctions] Found {typeof(T).Name} at path: {path}");
                            return component;
                        }
                    }
                }
                catch (Exception e)
                {
                    Logging.Warn($"[CommonFunctions] Error searching path '{path}': {e.Message}");
                }
            }

            Logging.Warn($"[CommonFunctions] Could not find {typeof(T).Name} in any fallback paths");
            return null;
        }
    }
}
