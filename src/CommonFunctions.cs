using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using IAmYourTranslator.Translation;
using IAmYourTranslator.Fonts;
using IAmYourTranslator.Textures;
using IAmYourTranslator.Audio;
using IAmYourTranslator.Utils;

namespace IAmYourTranslator
{
    public static class CommonFunctions
    {
        // --- TranslationEngine forwarding ---
        public static string PreviousHudMessage { get => TranslationEngine.PreviousHudMessage; set => TranslationEngine.PreviousHudMessage = value; }
        public static void TranslateTextAndSaveIfMissing(Text textComponent, string originalText, Dictionary<string, string> translationDict, string logPrefix = "")
            => TranslationEngine.TranslateTextAndSaveIfMissing(textComponent, originalText, translationDict, logPrefix);
        public static void TranslateTextAndSaveIfMissing(TMP_Text tmpComponent, string originalText, Dictionary<string, string> translationDict, string logPrefix = "")
            => TranslationEngine.TranslateTextAndSaveIfMissing(tmpComponent, originalText, translationDict, logPrefix);
        public static string ResolveOriginalTranslationKey(string currentText, Dictionary<string, string> translationDict)
            => TranslationEngine.ResolveOriginalTranslationKey(currentText, translationDict);
        public static void CaptureCurrentReverseLookupMap()
            => TranslationEngine.CaptureCurrentReverseLookupMap();
        public static void RefreshAllSceneTexts(bool skipTranslatorSettingsMenu = true)
            => TranslationEngine.RefreshAllSceneTexts(skipTranslatorSettingsMenu);
        public static void ClearOriginalTextCache(TMP_Text tmpComponent)
            => TranslationEngine.ClearOriginalTextCache(tmpComponent);

        // --- FontManager forwarding (inner classes for backward compatibility) ---
        public static class TMPFontReplacer
        {
            public static TMP_FontAsset LoadFontFromFile(string fontPath)
                => FontManager.LoadFontFromFile(fontPath);
            public static TMP_FontAsset GetCachedFont(string explicitPath = null)
                => FontManager.GetCachedFont(explicitPath);
            public static void CacheOriginalFontState(TMP_Text tmp)
                => FontManager.CacheOriginalFontState(tmp);
            public static void CleanupOriginalFontCache()
                => FontManager.CleanupOriginalFontCache();
            public static void RestoreOriginalFonts()
                => FontManager.RestoreOriginalFonts();
            public static void ApplyFontToTMP(TMP_Text tmp, TMP_FontAsset newFont)
                => FontManager.ApplyFontToTMP(tmp, newFont);
            public static void ApplyFontToAllTMP(TMP_FontAsset newFont)
                => FontManager.ApplyFontToAllTMP(newFont);
            public static void ReplaceFont(string fontPath)
                => FontManager.ReplaceFont(fontPath);
        }

        // --- TextureManager forwarding (inner classes for backward compatibility) ---
        public static class UITextureReplacer
        {
            public static Texture2D LoadTextureFromFile(string filePath, bool invertAlpha = false)
                => TextureManager.LoadTextureFromFile(filePath, invertAlpha);
            public static void ClearCache(bool destroyUnityObjects = true)
                => TextureManager.ClearCache(destroyUnityObjects);
            public static bool RestoreOn(GameObject target)
                => TextureManager.RestoreOn(target);
            public static void RestoreAll()
                => TextureManager.RestoreAll();
            public static void ApplyTo(GameObject target, string filePath, bool invertAlpha = false)
                => TextureManager.ApplyTo(target, filePath, invertAlpha);
        }

        // --- AudioClipReplacer forwarding ---
        public static class AudioClipReplacer
        {
            public static bool TryFindReplacementAudioFile(string audioDir, string clipName, out string filePath)
                => IAmYourTranslator.Audio.AudioClipReplacer.TryFindReplacementAudioFile(audioDir, clipName, out filePath);
            public static AudioClip LoadAudioClip(string filePath)
                => IAmYourTranslator.Audio.AudioClipReplacer.LoadAudioClip(filePath);
            public static void ReplaceAudioClip(UnityEngine.AudioSource source, string filePath)
                => IAmYourTranslator.Audio.AudioClipReplacer.ReplaceAudioClip(source, filePath);
            public static void ReplaceSoundObjectClip(Sounds.SoundObject soundObj, string filePath, string name)
                => IAmYourTranslator.Audio.AudioClipReplacer.ReplaceSoundObjectClip(soundObj, filePath, name);
            public static void ExportAudioClipToWav(AudioClip clip, string filePath)
                => IAmYourTranslator.Audio.AudioClipReplacer.ExportAudioClipToWav(clip, filePath);
            public static void ExportAudioClipToOgg(AudioClip clip, string filePath)
                => IAmYourTranslator.Audio.AudioClipReplacer.ExportAudioClipToOgg(clip, filePath);
        }

        // --- CachingHelpers forwarding ---
        public static T[] FindObjectsOfTypeCached<T>(bool includeInactive = false) where T : UnityEngine.Object
            => CachingHelpers.FindObjectsOfTypeCached<T>(includeInactive);
        public static void InvalidateFindObjectsCache()
            => CachingHelpers.InvalidateFindObjectsCache();
        public static void ClearAllCaches(bool clearReverseLookup = true, bool destroyTextureAssets = false)
        {
            CachingHelpers.ClearAllCaches();
            TranslationEngine.OriginalUITextByComponent.Clear();
            TranslationEngine.OriginalTMPTextByComponent.Clear();
            PreviousHudMessage = null;
            if (clearReverseLookup)
                TranslationEngine.LastKnownReverseLookupMap = new Dictionary<string, string>(StringComparer.Ordinal);
            TextureManager.ClearCache(destroyTextureAssets);
        }
        public static GameObject GetInactiveRootObject(string objectName)
            => CachingHelpers.GetInactiveRootObject(objectName);
        public static GameObject GetGameObjectChild(GameObject parent, string childName)
            => CachingHelpers.GetGameObjectChild(parent, childName);
        public static IEnumerator WaitforSeconds(float seconds)
            => CachingHelpers.WaitforSeconds(seconds);

        // --- SceneHelpers forwarding ---
        public static string GetCurrentSceneName()
            => SceneHelpers.GetCurrentSceneName();
        public static Scene GetCurrentScene()
            => SceneHelpers.GetCurrentScene();
        public static GameObject GetObject(string path)
            => SceneHelpers.GetObject(path);
        public static Transform RecursiveFindChild(Transform parent, string childName)
            => SceneHelpers.RecursiveFindChild(parent, childName);
        public static IEnumerable<CodeInstruction> IL(params (OpCode, object)[] instructions)
            => SceneHelpers.IL(instructions);

        // --- UIHelpers forwarding ---
        public static void ApplyFontToAllChildrenTMP(Component target, TMP_FontAsset font, string logPrefix = "")
            => UIHelpers.ApplyFontToAllChildrenTMP(target, font, logPrefix);
        public static void DisableGameObjectPanels(params GameObject[] panels)
            => UIHelpers.DisableGameObjectPanels(panels);
        public static void StretchRectTransformHorizontal(RectTransform rt)
            => UIHelpers.StretchRectTransformHorizontal(rt);
        public static T FindComponentWithFallback<T>(Component startComponent, params string[] fallbackPaths) where T : Component
            => UIHelpers.FindComponentWithFallback<T>(startComponent, fallbackPaths);
    }
}
