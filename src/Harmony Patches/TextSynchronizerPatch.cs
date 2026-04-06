using System;
using System.IO;
using System.Collections;
using HarmonyLib;
using AudioTextSynchronizer;
using AudioTextSynchronizer.Core;
using IAmYourTranslator.json;
using UnityEngine;
using TMPro;
using BepInEx;
using static IAmYourTranslator.CommonFunctions;
using System.Collections.Generic;
using UnityEngine.Networking;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(TextSynchronizer))]
    public static class TextSynchronizerPatch
    {
        private static readonly Dictionary<string, AudioClip> ClipCache = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> LoadingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<int> AllowOriginalPlay = new HashSet<int>();
        private static readonly HashSet<int> PendingPlay = new HashSet<int>();
        private static MonoBehaviour coroutineHost;

        public static void SetCoroutineHost(MonoBehaviour host)
        {
            if (host != null)
                coroutineHost = host;
        }

        private static bool EnsureCoroutineHost(string context, bool logWarning = true)
        {
            if (coroutineHost != null)
                return true;

            var plugin = Plugin.GetOrRecoverInstance();
            if (plugin != null)
            {
                coroutineHost = plugin;
                return true;
            }

            if (logWarning)
                Logging.Warn($"[TextSynchronizerPatch] coroutineHost is null ({context}), skipping preload.");

            return false;
        }

        public static void PreloadSceneReplacements()
        {
            if (!EnsureCoroutineHost("PreloadSceneReplacements"))
                return;

            coroutineHost.StartCoroutine(PreloadSceneReplacementsCoroutine());
        }

        /// <summary>
        /// Force refresh all TextSynchronizer components after language switch.
        /// Call this from Plugin.RefreshLocalizationInCurrentScene().
        /// </summary>
        public static void RefreshAllSynchronizers()
        {
            try
            {
                var synchronizers = CommonFunctions.FindObjectsOfTypeCached<TextSynchronizer>(true);
                if (synchronizers == null || synchronizers.Length == 0)
                    return;

                Logging.Info($"[TextSynchronizerPatch] Refreshing {synchronizers.Length} synchronizers");

                foreach (var sync in synchronizers)
                {
                    if (sync == null)
                        continue;

                    // Force re-apply font to all text components
                    var allTmpTexts = sync.GetComponentsInChildren<TMP_Text>(true);
                    foreach (var tmp in allTmpTexts)
                    {
                        if (tmp != null)
                        {
                            var font = CommonFunctions.TMPFontReplacer.GetCachedFont(Plugin.GlobalFontPath);
                            if (font != null)
                            {
                                CommonFunctions.TMPFontReplacer.ApplyFontToTMP(tmp, font);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logging.Warn($"[TextSynchronizerPatch] RefreshAllSynchronizers failed: {e.Message}");
            }
        }

        private static IEnumerator PreloadSceneReplacementsCoroutine()
        {
            // Allow scene objects to initialize first
            yield return null;

            TextSynchronizer[] synchronizers = CommonFunctions.FindObjectsOfTypeCached<TextSynchronizer>(true);
            if (synchronizers == null || synchronizers.Length == 0)
                yield break;

            HashSet<string> uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var sync in synchronizers)
            {
                string phraseName = sync?.Timings?.name;
                if (string.IsNullOrEmpty(phraseName))
                    continue;

                string replacementPath = GetExistingReplacementPath(phraseName);
                if (!string.IsNullOrEmpty(replacementPath))
                    uniquePaths.Add(replacementPath);
            }

            foreach (var path in uniquePaths)
            {
                if (ClipCache.ContainsKey(path) || LoadingPaths.Contains(path))
                    continue;

                LoadingPaths.Add(path);
                AudioClip clip = null;
                yield return LoadAudioClipAsync(path, c => clip = c, coroutineHost);
                if (clip != null)
                    ClipCache[path] = clip;
                LoadingPaths.Remove(path);

                // Spread work across frames
                yield return null;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch("set_Timings")]
        public static void OnTimingsSet_Prefix(TextSynchronizer __instance, ref PhraseAsset value)
        {
            try
            {
                if (value == null || value.Timings == null)
                    return;
                if (!LanguageManager.IsLoaded || LanguageManager.CurrentLanguage == null)
                    return;

                string phraseName = value.name;
                var translationsDict = LanguageManager.GetTranslations();

                if (translationsDict.TryGetValue(phraseName, out var translations))
                {
                    // translations exist -> just apply them
                    for (int i = 0; i < value.Timings.Count; i++)
                    {
                        if (i < translations.Count)
                            value.Timings[i].Text = translations[i];
                    }
                }
                else
                {
                    // === no translation -> add original to CurrentLanguage.timings ===
                    List<string> originalTimings = value.Timings.ConvertAll(t => t.Text);
                    LanguageManager.CurrentLanguage.timings[phraseName] = originalTimings;

                    // save to JSON
                    LanguageManager.SaveCurrentLanguage();

                    Logging.Warn($"[TextSynchronizerPatch] Added missing translation for '{phraseName}'");
                }
            }
            catch (Exception e)
            {
                Logging.Error($"[TextSynchronizerPatch] Prefix error: {e}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("set_Timings")]
        public static void OnTimingsSet_Postfix(TextSynchronizer __instance, PhraseAsset value)
        {
            if (__instance == null || value == null)
                return;

            // Apply font to all text synchronizer text components
            var tmpFont = TMPFontReplacer.GetCachedFont(Plugin.GlobalFontPath);
            if (tmpFont != null)
            {
                var allTmpTexts = __instance.GetComponentsInChildren<TMP_Text>(true);
                foreach (var tmp in allTmpTexts)
                {
                    if (tmp != null)
                    {
                        TMPFontReplacer.ApplyFontToTMP(tmp, tmpFont);
                    }
                }
            }

            __instance.StartCoroutine(DelayedAudioProcess(__instance, value));
        }

        [HarmonyPrefix]
        [HarmonyPatch("Play")]
        public static bool Play_Prefix(TextSynchronizer __instance, bool initializeEffect)
        {
            if (__instance == null)
                return true;

            int id = __instance.GetInstanceID();
            if (AllowOriginalPlay.Remove(id))
                return true;

            if (PendingPlay.Contains(id))
                return false;

            string wavPath = GetReplacementPathForInstance(__instance);
            if (string.IsNullOrEmpty(wavPath))
                return true;

            if (ClipCache.TryGetValue(wavPath, out var cached) && cached != null)
            {
                ApplyReplacementClip(__instance.Source, cached);
                return true;
            }

            PendingPlay.Add(id);
            __instance.StartCoroutine(LoadAndPlayReplacement(__instance, wavPath, initializeEffect));
            return false;
        }

        private static IEnumerator LoadAndPlayReplacement(TextSynchronizer instance, string wavPath, bool initializeEffect)
        {
            if (instance == null)
                yield break;

            int id = instance.GetInstanceID();
            try
            {
                if (!LoadingPaths.Contains(wavPath))
                {
                    LoadingPaths.Add(wavPath);
                    AudioClip clip = null;
                    yield return LoadAudioClipAsync(wavPath, c => clip = c, coroutineHost);
                    if (clip != null)
                        ClipCache[wavPath] = clip;
                    LoadingPaths.Remove(wavPath);
                }
                else
                {
                    while (LoadingPaths.Contains(wavPath))
                        yield return null;
                }

                if (instance == null)
                    yield break;

                if (ClipCache.TryGetValue(wavPath, out var cached) && cached != null)
                    ApplyReplacementClip(instance.Source, cached);

                AllowOriginalPlay.Add(id);
                instance.Play(initializeEffect);
            }
            finally
            {
                PendingPlay.Remove(id);
            }
        }

        private static IEnumerator DelayedAudioProcess(TextSynchronizer instance, PhraseAsset value)
        {
            if (instance?.Source == null)
            {
                Logging.Warn($"[TextSynchronizerPatch] No AudioSource for '{value?.name}'");
                yield break;
            }

            string phraseName = value?.name ?? "Unknown";
            string replacementPath = GetExistingReplacementPath(phraseName);
            if (!string.IsNullOrEmpty(replacementPath))
            {
                QueuePreload(replacementPath);
                yield break;
            }

            string wavPath = GetDefaultExportPath(phraseName);
            if (string.IsNullOrEmpty(wavPath))
                yield break;

            // Wait briefly for the game to assign Source.clip
            float waitUntil = Time.realtimeSinceStartup + 0.2f;
            while (instance.Source.clip == null && Time.realtimeSinceStartup < waitUntil)
                yield return null;

            AudioClip originalClip = instance.Source.clip;
            if (originalClip == null)
            {
                Logging.Info($"[TextSynchronizerPatch] No clip to export for '{phraseName}' (source.clip == null)");
                yield break;
            }

            AudioClipReplacer.ExportAudioClipToWav(originalClip, wavPath);
            Logging.Info($"[TextSynchronizerPatch] Exported original audio for '{phraseName}'");
        }

        private static void QueuePreload(string wavPath)
        {
            if (!EnsureCoroutineHost("QueuePreload", logWarning: false))
                return;

            if (ClipCache.ContainsKey(wavPath) || LoadingPaths.Contains(wavPath))
                return;

            coroutineHost.StartCoroutine(PreloadSingle(wavPath));
        }

        private static IEnumerator PreloadSingle(string wavPath)
        {
            if (LoadingPaths.Contains(wavPath))
                yield break;

            LoadingPaths.Add(wavPath);
            AudioClip clip = null;
            yield return LoadAudioClipAsync(wavPath, c => clip = c, coroutineHost);
            if (clip != null)
                ClipCache[wavPath] = clip;
            LoadingPaths.Remove(wavPath);
        }

        private static string GetReplacementPathForInstance(TextSynchronizer instance)
        {
            string phraseName = instance?.Timings?.name;
            if (string.IsNullOrEmpty(phraseName))
            {
                string clipName = instance?.Source?.clip != null ? instance.Source.clip.name : null;
                phraseName = clipName;
            }

            if (string.IsNullOrEmpty(phraseName))
                return null;

            return GetExistingReplacementPath(phraseName);
        }

        private static string GetExistingReplacementPath(string phraseName)
        {
            if (!Plugin.EnableAudioReplacementEntry.Value || LanguageManager.CurrentSummary == null)
                return null;

            string basePath = LanguageManager.CurrentSummary.Paths.AudioDir;
            if (!Directory.Exists(basePath))
                Directory.CreateDirectory(basePath);

            return AudioClipReplacer.TryFindReplacementAudioFile(basePath, phraseName, out string replacementPath)
                ? replacementPath
                : null;
        }

        private static string GetDefaultExportPath(string phraseName)
        {
            if (LanguageManager.CurrentSummary == null || string.IsNullOrWhiteSpace(phraseName))
                return null;

            string basePath = LanguageManager.CurrentSummary.Paths.AudioDir;
            if (!Directory.Exists(basePath))
                Directory.CreateDirectory(basePath);

            string safeName = SanitizeFileName(phraseName);
            return Path.Combine(basePath, safeName + ".wav");
        }

        private static void ApplyReplacementClip(AudioSource source, AudioClip clip)
        {
            if (source == null || clip == null)
                return;

            Plugin.RegisterReplacedAudioSource(source, source.clip);
            source.clip = clip;
            source.time = 0f;
        }

        private static IEnumerator LoadAudioClipAsync(string filePath, Action<AudioClip> onLoaded, MonoBehaviour instance = null)
        {
            if (!File.Exists(filePath))
            {
                onLoaded?.Invoke(null);
                yield break;
            }

            AudioType type = filePath.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)
                ? AudioType.OGGVORBIS
                : AudioType.WAV;

            using (var www = UnityWebRequestMultimedia.GetAudioClip("file://" + filePath, type))
            {
                var request = www.SendWebRequest();
                while (!request.isDone)
                {
                    // Check if the instance is still alive
                    if (instance != null && instance == null)
                    {
                        Logging.Warn("[TextSynchronizerPatch] Instance destroyed, cancelling audio load.");
                        www.Abort();
                        onLoaded?.Invoke(null);
                        yield break;
                    }
                    yield return null;
                }

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Logging.Warn("[TextSynchronizerPatch] Loading error: " + www.error);
                    onLoaded?.Invoke(null);
                    yield break;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                if (clip != null)
                    clip.name = Path.GetFileNameWithoutExtension(filePath);
                onLoaded?.Invoke(clip);
            }
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
