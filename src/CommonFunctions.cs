using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
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

namespace IAmYourTranslator
{
    public static class CommonFunctions
    {


        public static string PreviousHudMessage;

        public static IEnumerator WaitforSeconds(float seconds)
        {
            yield return new WaitForSeconds(seconds);
        }

        public static GameObject GetInactiveRootObject(string objectName)
        {
            // Caching search results to improve performance
            Dictionary<string, GameObject> rootObjectCache = new Dictionary<string, GameObject>();
            float lastCacheTime = 0f;
            const float CACHE_DURATION = 1f; // The cache is valid for 1 second

            // The cache is valid for 1 second
            if (Time.time - lastCacheTime < CACHE_DURATION && rootObjectCache.TryGetValue(objectName, out GameObject cached))
            {
                if (cached != null)
                    return cached;
                else
                    rootObjectCache.Remove(objectName);
            }

            // If the cache is outdated, clear it
            if (Time.time - lastCacheTime >= CACHE_DURATION)
            {
                rootObjectCache.Clear();
                lastCacheTime = Time.time;
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

            // Caching search results to improve performance
            Dictionary<(GameObject, string), GameObject> childCache = new Dictionary<(GameObject, string), GameObject>();
            float lastChildCacheTime = 0f;
            const float CHILD_CACHE_DURATION = 0.5f; // Cache is valid for 0.5 seconds

            var cacheKey = (parent, childName);

            // Checking the cache
            if (Time.time - lastChildCacheTime < CHILD_CACHE_DURATION && childCache.TryGetValue(cacheKey, out GameObject cached))
            {
                if (cached != null)
                    return cached;
                else
                    childCache.Remove(cacheKey);
            }

            // If the cache is outdated, clear it
            if (Time.time - lastChildCacheTime >= CHILD_CACHE_DURATION)
            {
                childCache.Clear();
                lastChildCacheTime = Time.time;
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
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();

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

            public static TMP_FontAsset GetCachedFont(string explicitPath = null)
            {
                // If the plugin has preloaded the global font, use it
                if (Plugin.GlobalTMPFont != null)
                    return Plugin.GlobalTMPFont;

                // Select path: explicit, from Plugin.GlobalFontPath or default
                string path = explicitPath ?? Plugin.GlobalFontPath ?? Path.Combine(Paths.ConfigPath, "IAmYourTranslator", "fonts", "Jovanny Lemonad - Bender-Bold.otf");

                if (!File.Exists(path))
                    return null;

                var fi = new FileInfo(path);
                var lastWrite = fi.LastWriteTimeUtc;

                if (cachedFileFont == null || lastWrite > cachedFileWriteTime)
                {
                    var tmpFont = LoadFontFromFile(path);
                    if (tmpFont == null)
                        return null;

                    cachedFileFont = tmpFont;
                    cachedFileWriteTime = lastWrite;
                }

                return cachedFileFont;
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

                if (tmp.font == newFont)
                    return; // already applied

                // Save the original material (it stores effects — shadow, outline, underlay)
                Material originalMaterial = tmp.fontMaterial;

                // Assign a new font
                tmp.font = newFont;

                // Create a copy of the material to preserve visual styles
                Material newMaterial = new Material(originalMaterial);

                // Change the font texture in the material
                newMaterial.SetTexture("_MainTex", newFont.atlasTexture);

                // Assign the updated material
                tmp.fontMaterial = newMaterial;

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
                    if (tmp.font == null)
                        continue;

                    // Save the original material
                    var originalMaterial = tmp.fontMaterial;

                    // Assigning a new font
                    tmp.font = newFont;

                    // Creating a copy of the material to save the styles
                    Material newMat = new Material(originalMaterial);

                    // Updating the font inside the material
                    newMat.SetTexture("_MainTex", newFont.atlasTexture);

                    // Assigning updated material
                    tmp.fontMaterial = newMat;

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

            Texture2D tex = LoadTextureFromFile(filePath, invertAlpha);
            if (tex == null)
                return;

            // Try to find the RawImage component.
            RawImage raw = target.GetComponent<RawImage>();
            if (raw != null)
            {
                raw.texture = tex;
                Debug.Log($"[UITextureReplacer] Applied texture '{tex.name}' to RawImage on '{target.name}' (invertAlpha={invertAlpha}).");
                return;
            }

            // Or a regular Image
            Image img = target.GetComponent<Image>();
            if (img != null)
            {
                Sprite sprite = Sprite.Create(
                    tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    100f
                );

                sprite.name = tex.name;
                img.sprite = sprite;

                Debug.Log($"[UITextureReplacer] Applied sprite '{sprite.name}' to Image on '{target.name}' (invertAlpha={invertAlpha}).");
                return;
            }

            Debug.LogWarning($"[UITextureReplacer] GameObject '{target.name}' has no Image or RawImage component.");
        }
    }


    public static class AudioClipReplacer
        {
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

            public static void ExportAudioClipToOgg(AudioClip clip, string filePath)
            {
                if (clip == null) return;

                clip = CreateDecompressedCopy(clip);
                string tempWav = Path.Combine(Path.GetTempPath(), clip.name + ".wav");
                ExportAudioClipToWav(clip, tempWav);

                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                try
                {
                    var ffmpeg = new System.Diagnostics.Process();
                    ffmpeg.StartInfo.FileName = "ffmpeg";
                    ffmpeg.StartInfo.Arguments = $"-y -i \"{tempWav}\" -c:a libvorbis \"{filePath}\"";
                    ffmpeg.StartInfo.CreateNoWindow = true;
                    ffmpeg.StartInfo.UseShellExecute = false;
                    ffmpeg.Start();
                    ffmpeg.WaitForExit(); // safe in C# 7.3

                    Debug.Log("[AudioClipReplacer] OGG exported: " + filePath);
                }
                catch (Exception e)
                {
                    Debug.LogError("[AudioClipReplacer] Error exporting OGG: " + e);
                }
                finally
                {
                    if (File.Exists(tempWav))
                        File.Delete(tempWav);
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
    }
}
