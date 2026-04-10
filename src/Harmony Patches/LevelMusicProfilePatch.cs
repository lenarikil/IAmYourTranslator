using System;
using HarmonyLib;
using UnityEngine;
using BepInEx;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using static IAmYourTranslator.Logging;
using static IAmYourTranslator.CommonFunctions;
using IAmYourTranslator.json;

namespace IAmYourTranslator.HarmonyPatches
{
    [HarmonyPatch]
    public static class LevelMusicProfilePatch
    {
        // Кэш загруженных аудиоклипов
        private static readonly Dictionary<string, AudioClip> _clipCache = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
        
        // Отслеживание файлов в процессе загрузки
        private static readonly HashSet<string> _loadingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        // Хост для корутин (аналогично TextSynchronizerPatch)
        private static MonoBehaviour _coroutineHost;
        
        // Флаг для оптимизации проверок
        private static bool? _cachedShouldReplaceAudio;
        private static string _cachedAudioDir;

        private static bool IsDebugLoggingEnabled()
        {
            return Plugin.EnableAudioDebugLogsEntry != null && Plugin.EnableAudioDebugLogsEntry.Value;
        }

        private static bool ShouldReplaceAudio()
        {
            // Используем кэшированный результат, если он есть
            if (_cachedShouldReplaceAudio.HasValue)
                return _cachedShouldReplaceAudio.Value;

            bool enabled = Plugin.EnableAudioReplacementEntry != null && Plugin.EnableAudioReplacementEntry.Value;
            bool languageLoaded = LanguageManager.CurrentSummary != null;
            
            if (IsDebugLoggingEnabled())
                Debug($"[MusicReplace] ShouldReplaceAudio: enabled={enabled}, languageLoaded={languageLoaded}");
            
            bool result = enabled && languageLoaded;
            _cachedShouldReplaceAudio = result;
            return result;
        }

        private static string GetAudioDir()
        {
            if (_cachedAudioDir != null)
                return _cachedAudioDir;

            if (LanguageManager.CurrentSummary == null)
                return null;

            _cachedAudioDir = LanguageManager.CurrentSummary.Paths.AudioDir;
            return _cachedAudioDir;
        }

        private static void InvalidateCache()
        {
            _cachedShouldReplaceAudio = null;
            _cachedAudioDir = null;
        }

        public static void SetCoroutineHost(MonoBehaviour host)
        {
            if (host != null)
                _coroutineHost = host;
        }

        private static bool EnsureCoroutineHost(string context, bool logWarning = true)
        {
            if (_coroutineHost != null)
                return true;

            var plugin = Plugin.GetOrRecoverInstance();
            if (plugin != null)
            {
                _coroutineHost = plugin;
                return true;
            }

            if (logWarning)
                Warn($"[LevelMusicProfilePatch] coroutineHost is null ({context}), skipping async operations.");

            return false;
        }

        /// <summary>
        /// Пытается получить заменённый аудиоклип из кэша или начать асинхронную загрузку
        /// </summary>
        /// <returns>true если клип уже в кэше (replacementClip будет установлен), false в противном случае</returns>
        private static bool TryGetReplacementClip(string clipName, out AudioClip replacementClip)
        {
            replacementClip = null;
            
            if (!ShouldReplaceAudio())
                return false;

            string audioDir = GetAudioDir();
            if (string.IsNullOrEmpty(audioDir))
                return false;

            if (IsDebugLoggingEnabled())
                Debug($"[MusicReplace] Looking for replacement in '{audioDir}' for clip '{clipName}'");
            
            if (!AudioClipReplacer.TryFindReplacementAudioFile(audioDir, clipName, out string replacementPath))
            {
                if (IsDebugLoggingEnabled())
                    Debug($"[MusicReplace] TryFindReplacementAudioFile returned false for '{clipName}'");
                return false;
            }

            if (IsDebugLoggingEnabled())
                Debug($"[MusicReplace] Found replacement path: '{replacementPath}'");

            // Проверяем кэш
            if (_clipCache.TryGetValue(replacementPath, out replacementClip))
            {
                if (replacementClip != null)
                {
                    if (IsDebugLoggingEnabled())
                        Info($"[MusicReplace] Using cached clip for '{clipName}' -> '{replacementClip.name}'");
                    return true;
                }
                else
                {
                    // Некорректный null в кэше - удаляем
                    _clipCache.Remove(replacementPath);
                }
            }

            // Если файл уже в процессе загрузки, возвращаем false - клип ещё не готов
            if (_loadingPaths.Contains(replacementPath))
                return false;

            // Начинаем асинхронную загрузку в фоне
            if (EnsureCoroutineHost("TryGetReplacementClip", logWarning: false))
            {
                _coroutineHost.StartCoroutine(LoadAudioClipAsync(replacementPath, clipName));
            }
            else
            {
                // Если нет хоста для корутин, пробуем синхронную загрузку как fallback
                replacementClip = AudioClipReplacer.LoadAudioClip(replacementPath);
                if (replacementClip != null)
                {
                    _clipCache[replacementPath] = replacementClip;
                    Info($"[MusicReplace] Replacing '{clipName}' with '{replacementClip.name}' (sync fallback)");
                    return true;
                }
                else
                {
                    Warn($"[MusicReplace] Failed to load replacement audio for '{clipName}' from '{replacementPath}'");
                    return false;
                }
            }

            return false;
        }

        private static IEnumerator LoadAudioClipAsync(string filePath, string originalClipName)
        {
            if (!File.Exists(filePath))
            {
                Warn($"[MusicReplace] File not found: {filePath}");
                yield break;
            }

            _loadingPaths.Add(filePath);
            
            AudioType type = filePath.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)
                ? AudioType.OGGVORBIS
                : AudioType.WAV;

            using (var www = UnityWebRequestMultimedia.GetAudioClip("file://" + filePath, type))
            {
                var request = www.SendWebRequest();
                while (!request.isDone)
                {
                    // Проверяем, жив ли хост
                    if (_coroutineHost == null)
                    {
                        www.Abort();
                        _loadingPaths.Remove(filePath);
                        yield break;
                    }
                    yield return null;
                }

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Warn($"[MusicReplace] Loading error for '{originalClipName}': {www.error}");
                    _loadingPaths.Remove(filePath);
                    yield break;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                if (clip != null)
                {
                    clip.name = Path.GetFileNameWithoutExtension(filePath);
                    _clipCache[filePath] = clip;
                    
                    if (IsDebugLoggingEnabled())
                        Info($"[MusicReplace] Async loaded replacement for '{originalClipName}' -> '{clip.name}'");
                }
                else
                {
                    Warn($"[MusicReplace] Failed to load audio clip from '{filePath}'");
                }
            }

            _loadingPaths.Remove(filePath);
        }

        /// <summary>
        /// Предзагрузка музыки для всех LevelMusicProfile в текущей сцене
        /// </summary>
        public static void PreloadLevelMusic()
        {
            if (!EnsureCoroutineHost("PreloadLevelMusic"))
                return;

            _coroutineHost.StartCoroutine(PreloadLevelMusicCoroutine());
        }

        private static IEnumerator PreloadLevelMusicCoroutine()
        {
            // Даём сцене инициализироваться
            yield return null;

            if (!ShouldReplaceAudio())
                yield break;

            var profiles = CommonFunctions.FindObjectsOfTypeCached<LevelMusicProfile>(true);
            if (profiles == null || profiles.Length == 0)
                yield break;

            HashSet<string> uniqueClipNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string audioDir = GetAudioDir();

            // Собираем уникальные имена клипов из всех профилей
            foreach (var profile in profiles)
            {
                if (profile == null)
                    continue;

                // Получаем музыку через рефлексию или кэшируем вызовы
                // Упрощённый подход: предполагаем, что имена клипов можно получить из имени профиля
                // В реальности может потребоваться более сложная логика
                string profileName = profile.name;
                // Добавляем возможные варианты имён клипов
                uniqueClipNames.Add(profileName + "_Start");
                uniqueClipNames.Add(profileName + "_Combat");
                uniqueClipNames.Add(profileName + "_Dim");
            }

            // Для каждого уникального имени ищем файлы и предзагружаем
            foreach (string clipName in uniqueClipNames)
            {
                if (!AudioClipReplacer.TryFindReplacementAudioFile(audioDir, clipName, out string replacementPath))
                    continue;

                if (_clipCache.ContainsKey(replacementPath) || _loadingPaths.Contains(replacementPath))
                    continue;

                _loadingPaths.Add(replacementPath);
                yield return LoadAudioClipAsync(replacementPath, clipName);
                
                // Распределяем нагрузку по кадрам
                yield return null;
            }
        }

        /// <summary>
        /// Очистка кэша при смене языка или выгрузке сцены
        /// </summary>
        public static void ClearCache()
        {
            _clipCache.Clear();
            _loadingPaths.Clear();
            InvalidateCache();
            
            if (IsDebugLoggingEnabled())
                Info("[MusicReplace] Cache cleared");
        }

        // Общий метод для обработки замены музыки
        private static void ProcessMusicReplacement(LevelMusicProfile instance, ref AudioClip result, string methodName)
        {
            try
            {
                if (instance == null)
                    return;

                string clipName = result ? result.name : "null";
                string profileName = instance.name;

                // Пытаемся получить заменённый клип (из кэша или начинаем загрузку)
                if (result != null && TryGetReplacementClip(result.name, out AudioClip replacement))
                {
                    result = replacement;
                    clipName = replacement.name;
                }

                if (IsDebugLoggingEnabled())
                {
                    Info($"[MusicLog] LevelMusicProfile.{methodName}() called | Profile='{profileName}' | Clip='{clipName}'");
                }
            }
            catch (Exception e)
            {
                Error($"[MusicLog] Error while logging/replacing {methodName}: {e}");
            }
        }

        // === LevelMusicProfile.GetStartMusic() ===
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LevelMusicProfile), nameof(LevelMusicProfile.GetStartMusic))]
        static void Postfix_GetStartMusic(LevelMusicProfile __instance, ref AudioClip __result)
        {
            ProcessMusicReplacement(__instance, ref __result, "GetStartMusic");
        }

        // === LevelMusicProfile.GetCombatMusic() ===
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LevelMusicProfile), nameof(LevelMusicProfile.GetCombatMusic))]
        static void Postfix_GetCombatMusic(LevelMusicProfile __instance, ref AudioClip __result)
        {
            ProcessMusicReplacement(__instance, ref __result, "GetCombatMusic");
        }

        // === LevelMusicProfile.GetDimMusic() ===
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LevelMusicProfile), nameof(LevelMusicProfile.GetDimMusic))]
        static void Postfix_GetDimMusic(LevelMusicProfile __instance, ref AudioClip __result)
        {
            ProcessMusicReplacement(__instance, ref __result, "GetDimMusic");
        }

        // === LevelMusicProfile.GetPlayCombatMusicImmediately() ===
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LevelMusicProfile), nameof(LevelMusicProfile.GetPlayCombatMusicImmediately))]
        static void Postfix_GetPlayCombatMusicImmediately(LevelMusicProfile __instance, bool __result)
        {
            try
            {
                if (!IsDebugLoggingEnabled())
                    return;
                if (__instance == null)
                    return;

                string profileName = __instance.name;
                Info($"[MusicLog] LevelMusicProfile.GetPlayCombatMusicImmediately() called | Profile='{profileName}' | Immediate={__result}");
            }
            catch (Exception e)
            {
                Error($"[MusicLog] Error while logging GetPlayCombatMusicImmediately: {e}");
            }
        }
    }
}