using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace IAmYourTranslator.Textures
{
    public static class TextureManager
    {
        private sealed class CachedTexture
        {
            public Texture2D Texture;
            public Sprite Sprite;
            public DateTime LastWriteTimeUtc;
        }

        private sealed class OriginalImageState
        {
            public Sprite Sprite;
            public Image.Type Type;
            public bool PreserveAspect;
            public bool FillCenter;
            public Image.FillMethod FillMethod;
            public float FillAmount;
            public bool FillClockwise;
            public int FillOrigin;
        }

        private static readonly Dictionary<string, CachedTexture> textureCache = new Dictionary<string, CachedTexture>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<int, WeakReference<Texture>> originalRawTexturesByInstId = new Dictionary<int, WeakReference<Texture>>();
        private static readonly Dictionary<int, WeakReference<OriginalImageState>> originalImageStatesByInstId = new Dictionary<int, WeakReference<OriginalImageState>>();

        public static Texture2D LoadTextureFromFile(string filePath, bool invertAlpha = false)
        {
            if (!File.Exists(filePath))
            {
                Logging.Warn($"[TextureManager] File not found: {filePath}");
                return null;
            }

            var lastWriteTimeUtc = File.GetLastWriteTimeUtc(filePath);
            string cacheKey = $"{filePath}:{invertAlpha}";
            if (textureCache.TryGetValue(cacheKey, out var cached))
            {
                if (cached.Texture != null && cached.LastWriteTimeUtc == lastWriteTimeUtc)
                {
                    Logging.Info($"[TextureManager] Texture cache hit: {Path.GetFileName(filePath)} ({cached.Texture.width}x{cached.Texture.height}, invertAlpha={invertAlpha})");
                    return cached.Texture;
                }

                DestroyCachedTexture(cached, destroyTexture: true, onlyIfUnused: true,
                    allImages: UnityEngine.Object.FindObjectsOfType<Image>(true),
                    allRawImages: UnityEngine.Object.FindObjectsOfType<RawImage>(true));
                textureCache.Remove(cacheKey);
                Logging.Info($"[TextureManager] Texture cache stale: {Path.GetFileName(filePath)} (invertAlpha={invertAlpha})");
            }

            byte[] data = File.ReadAllBytes(filePath);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(data))
            {
                Logging.Warn($"[TextureManager] Failed to load image: {filePath}");
                return null;
            }

            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = Path.GetFileNameWithoutExtension(filePath);

            if (invertAlpha)
                InvertAlphaMakeWhiteBackground(tex);

            textureCache[cacheKey] = new CachedTexture
            {
                Texture = tex,
                LastWriteTimeUtc = lastWriteTimeUtc
            };
            Logging.Info($"[TextureManager] Texture cache miss/load: {Path.GetFileName(filePath)} ({tex.width}x{tex.height}, invertAlpha={invertAlpha})");
            return tex;
        }

        private static Sprite GetOrCreateSprite(string filePath, bool invertAlpha, Texture2D tex)
        {
            if (tex == null)
                return null;

            string cacheKey = $"{filePath}:{invertAlpha}";
            if (textureCache.TryGetValue(cacheKey, out var cached) && cached.Texture == tex && cached.Sprite != null)
                return cached.Sprite;

            Sprite sprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f
            );

            sprite.name = tex.name;

            if (textureCache.TryGetValue(cacheKey, out cached))
                cached.Sprite = sprite;

            return sprite;
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
            if (originalImageStatesByInstId.ContainsKey(instId))
                return;

            originalImageStatesByInstId[instId] = new WeakReference<OriginalImageState>(new OriginalImageState
            {
                Sprite = img.sprite,
                Type = img.type,
                PreserveAspect = img.preserveAspect,
                FillCenter = img.fillCenter,
                FillMethod = img.fillMethod,
                FillAmount = img.fillAmount,
                FillClockwise = img.fillClockwise,
                FillOrigin = img.fillOrigin
            });
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
            foreach (var kv in originalImageStatesByInstId)
            {
                if (!kv.Value.TryGetTarget(out _))
                    deadImgIds.Add(kv.Key);
            }

            foreach (var id in deadImgIds)
                originalImageStatesByInstId.Remove(id);
        }

        public static void ClearCache(bool destroyUnityObjects = true)
        {
            if (destroyUnityObjects)
            {
                // Fetch UI component arrays once for all DestroyCachedTexture calls
                var allImages = UnityEngine.Object.FindObjectsOfType<Image>(true);
                var allRawImages = UnityEngine.Object.FindObjectsOfType<RawImage>(true);
                foreach (var cached in textureCache.Values)
                    DestroyCachedTexture(cached, destroyTexture: true, onlyIfUnused: true, allImages, allRawImages);
            }

            Logging.Info($"[TextureManager] Cleared texture cache (entries={textureCache.Count}, destroyUnityObjects={destroyUnityObjects}).");
            textureCache.Clear();
        }

        private static void DestroyCachedTexture(CachedTexture cached, bool destroyTexture, bool onlyIfUnused,
            Image[] allImages = null, RawImage[] allRawImages = null)
        {
            if (cached == null)
                return;

            if (cached.Sprite != null)
            {
                if (!onlyIfUnused || !IsSpriteInUse(cached.Sprite, allImages))
                {
                    string spriteName = cached.Sprite.name;
                    UnityEngine.Object.Destroy(cached.Sprite);
                    Logging.Info($"[TextureManager] Destroyed cached sprite '{spriteName}'.");
                    cached.Sprite = null;
                }
                else
                {
                    Logging.Info($"[TextureManager] Kept cached sprite '{cached.Sprite.name}' alive because it is still assigned to UI.");
                }
            }

            if (destroyTexture && cached.Texture != null)
            {
                if (!onlyIfUnused || !IsTextureInUse(cached.Texture, allImages, allRawImages))
                {
                    string textureName = cached.Texture.name;
                    UnityEngine.Object.Destroy(cached.Texture);
                    Logging.Info($"[TextureManager] Destroyed cached texture '{textureName}'.");
                    cached.Texture = null;
                }
                else
                {
                    Logging.Info($"[TextureManager] Kept cached texture '{cached.Texture.name}' alive because it is still assigned to UI.");
                }
            }
        }

        private static bool IsSpriteInUse(Sprite sprite, Image[] allImages = null)
        {
            if (sprite == null)
                return false;

            var images = allImages ?? UnityEngine.Object.FindObjectsOfType<Image>(true);
            foreach (var img in images)
            {
                if (img != null && img.sprite == sprite)
                    return true;
            }

            return false;
        }

        private static bool IsTextureInUse(Texture2D texture, Image[] allImages = null, RawImage[] allRawImages = null)
        {
            if (texture == null)
                return false;

            var rawImages = allRawImages ?? UnityEngine.Object.FindObjectsOfType<RawImage>(true);
            foreach (var raw in rawImages)
            {
                if (raw != null && raw.texture == texture)
                    return true;
            }

            var images = allImages ?? UnityEngine.Object.FindObjectsOfType<Image>(true);
            foreach (var img in images)
            {
                if (img != null && img.sprite != null && img.sprite.texture == texture)
                    return true;
            }

            return false;
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
            if (img != null && originalImageStatesByInstId.TryGetValue(img.GetInstanceID(), out var imgWeakRef) && imgWeakRef.TryGetTarget(out var originalState))
            {
                RestoreImageState(img, originalState);
                restored = true;
            }

            if (restored)
                Logging.Info($"[TextureManager] Restored original texture/sprite on '{GetHierarchyPath(target.transform)}'.");

            return restored;
        }

        public static void RestoreAll()
        {
            CleanupTextureCaches();
            int restored = 0;

            var rawImagesById = UnityEngine.Object.FindObjectsOfType<RawImage>(true)
                .Where(raw => raw != null)
                .ToDictionary(raw => raw.GetInstanceID());
            var imagesById = UnityEngine.Object.FindObjectsOfType<Image>(true)
                .Where(img => img != null)
                .ToDictionary(img => img.GetInstanceID());

            foreach (var kv in originalRawTexturesByInstId.ToList())
            {
                if (!kv.Value.TryGetTarget(out var originalTexture))
                    continue;

                if (rawImagesById.TryGetValue(kv.Key, out var raw))
                {
                    raw.texture = originalTexture;
                    restored++;
                }
            }

            foreach (var kv in originalImageStatesByInstId.ToList())
            {
                if (!kv.Value.TryGetTarget(out var originalState))
                    continue;

                if (imagesById.TryGetValue(kv.Key, out var img))
                {
                    RestoreImageState(img, originalState);
                    restored++;
                }
            }

            Logging.Info($"[TextureManager] Restored original UI textures/sprites on {restored} components.");
        }

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
                Logging.Warn("[TextureManager] Target GameObject is null.");
                return;
            }

            RawImage raw = target.GetComponent<RawImage>();
            Image img = target.GetComponent<Image>();
            if (raw == null && img == null)
            {
                Logging.Warn($"[TextureManager] GameObject '{GetHierarchyPath(target.transform)}' has no Image or RawImage component.");
                return;
            }

            if (string.IsNullOrEmpty(filePath))
            {
                RestoreOn(target);
                return;
            }

            if (!File.Exists(filePath))
            {
                Logging.Warn($"[TextureManager] Texture file not found for '{GetHierarchyPath(target.transform)}': {filePath}. Restoring original.");
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
                Logging.Info($"[TextureManager] Applied texture '{tex.name}' ({tex.width}x{tex.height}) to RawImage '{GetHierarchyPath(target.transform)}' rect={FormatRect(raw.rectTransform.rect)} invertAlpha={invertAlpha}.");
                return;
            }

            CacheOriginal(img);

            Sprite sprite = GetOrCreateSprite(filePath, invertAlpha, tex);
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.fillCenter = true;
            img.sprite = sprite;

            Logging.Info($"[TextureManager] Applied sprite '{sprite.name}' ({tex.width}x{tex.height}) to Image '{GetHierarchyPath(target.transform)}' type={img.type} preserveAspect={img.preserveAspect} rect={FormatRect(img.rectTransform.rect)} invertAlpha={invertAlpha}.");
        }

        private static void RestoreImageState(Image img, OriginalImageState state)
        {
            if (img == null || state == null)
                return;

            img.sprite = state.Sprite;
            img.type = state.Type;
            img.preserveAspect = state.PreserveAspect;
            img.fillCenter = state.FillCenter;
            img.fillMethod = state.FillMethod;
            img.fillAmount = state.FillAmount;
            img.fillClockwise = state.FillClockwise;
            img.fillOrigin = state.FillOrigin;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
                return "<null>";

            var names = new Stack<string>();
            var current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names.ToArray());
        }

        private static string FormatRect(Rect rect)
        {
            return $"{rect.width:0.##}x{rect.height:0.##}";
        }
    }
}
