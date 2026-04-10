using BepInEx;
using HarmonyLib;
using System;
using System.IO;
using IAmYourTranslator.json;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using IAmYourTranslator.Harmony_Patches;
using IAmYourTranslator.HarmonyPatches;
using static IAmYourTranslator.CommonFunctions;
using BepInEx.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace IAmYourTranslator
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance;
        public bool ready;
        // Globally cached font for the entire mod
        public static TMP_FontAsset GlobalTMPFont;
        public static string GlobalFontPath;

        // Config entries
        public static ConfigEntry<string> SelectedLanguageEntry;
        public static ConfigEntry<bool> EnableAudioReplacementEntry;
        public static ConfigEntry<bool> EnableTextureReplacementEntry;
        public static ConfigEntry<bool> EnableAudioDebugLogsEntry;
        public static ConfigEntry<bool> EnableExperimentalRadioAudioPatchesEntry;
        private static readonly Dictionary<int, AudioClip> OriginalClipBySourceId = new Dictionary<int, AudioClip>();

        // Logo paths for texture replacement
        private static readonly (string path, bool invertAlpha)[] LogoPaths = new[]
        {
            ("Canvas/Logo", false),
            ("Canvas/Corner Anchor/Logo", false),
            ("Canvas/UI - Level Name Border/Logo", false),
            ("[LEVEL DEPENDENCIES]/Credit Anchor/Credits UI/Fade In/Logo", true)
        };

        public static Plugin GetOrRecoverInstance()
        {
            if (Instance != null)
                return Instance;

            try
            {
                var plugins = UnityEngine.Object.FindObjectsOfType<Plugin>(true);
                if (plugins != null && plugins.Length > 0)
                {
                    Instance = plugins[0];
                    return Instance;
                }
            }
            catch
            {
            }

            try
            {
                var all = Resources.FindObjectsOfTypeAll<Plugin>();
                if (all != null && all.Length > 0)
                {
                    Instance = all[0];
                    return Instance;
                }
            }
            catch
            {
            }

            return Instance;
        }

        public void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Recover static references if they were lost after domain/runtime events.
            if (Instance == null)
            {
                Instance = this;
                Logging.Warn("[Plugin] Instance was null in OnSceneLoaded, recovered.");
            }
            TextSynchronizerPatch.SetCoroutineHost(this);
            LevelMusicProfilePatch.SetCoroutineHost(this);

            // Safety check: ensure plugin is fully initialized
            if (!ready)
            {
                Logging.Warn($"[Plugin] OnSceneLoaded called before plugin ready (ready={ready}). Skipping.");
                return;
            }

            Logging.Info($"[Plugin] OnSceneLoaded: {scene.name}");

            // Invalidate FindObjects cache on scene change
            CommonFunctions.InvalidateFindObjectsCache();

            // Apply language font first, fallback if needed
            ApplyFontImmediateWithFallback();

            GameObject canvasObj = GetInactiveRootObject("Canvas");
            Core.HandleSceneSwitch(scene, ref canvasObj);
            TextSynchronizerPatch.PreloadSceneReplacements();
            LevelMusicProfilePatch.PreloadLevelMusic();

            string sceneName = GetCurrentSceneName();
            if (sceneName != "Bootstrap")
            {
                if (sceneName == "Start Screen")
                {
                    Logging.Info("Start Screen detected, initializing StartScreenHandler");
                    StartScreenHandler.HandleStartScreen(this);
                }
                if (sceneName == "TitleReveal")
                {
                    RefreshTexturesInCurrentScene();
                }
                if (sceneName == "#027_Special_EndCredits")
                {
                    RefreshTexturesInCurrentScene();
                }

                // Apply post-init fixes immediately (not via coroutine)
                Core.ApplyPostInitFixes(canvasObj);
            }

            // Re-apply language/global font after UI is instantiated in the new scene
            try
            {
                // Apply immediately without coroutines
                ApplyFontImmediateWithFallback();
                RefreshLocalizationInCurrentScene();
            }
            catch (Exception e)
            {
                Logging.Warn($"Failed to apply localization: {e.Message}");
                ApplyFontImmediateWithFallback();
                RefreshLocalizationInCurrentScene();
            }
        }

        public static void RefreshTexturesInCurrentScene()
        {
            try
            {
                // Apply all logo paths
                foreach (var (path, invertAlpha) in LogoPaths)
                {
                    TryApplyTextureTo(path, invertAlpha);
                }

                TryApplyTextureToCommonLogoNodes();

                if (!LanguageManager.IsLoaded || !IsTextureReplacementEnabled())
                    UITextureReplacer.RestoreAll();
            }
            catch (Exception e)
            {
                Logging.Warn($"[Plugin] RefreshTexturesInCurrentScene error: {e.Message}");
            }
        }

        private void LoadGlobalFontFallback()
        {
            try
            {
                string fontsDir = Path.Combine(Paths.ConfigPath, "IAmYourTranslator", "fonts");
                string fontFile = Path.Combine(fontsDir, "Jovanny Lemonad - Bender-Bold.otf");
                GlobalFontPath = fontFile;
                GlobalTMPFont = CommonFunctions.TMPFontReplacer.LoadFontFromFile(fontFile);
                if (GlobalTMPFont != null)
                    Logging.Warn($"Loaded global TMP font: {GlobalTMPFont.name}");
                else
                    Logging.Warn("Global TMP font not found or failed to load.");
            }
            catch (Exception ex)
            {
                Logging.Warn($"Failed to load global TMP font: {ex.Message}");
            }
        }

        public static bool TryApplyLanguageFont()
        {
            var meta = LanguageManager.CurrentMetadata;
            var summary = LanguageManager.CurrentSummary;
            if (meta == null || summary?.Paths == null)
                return false;

            if (string.IsNullOrEmpty(meta.fontFile))
                return false;

            string fontPath = Path.Combine(summary.Paths.FontsDir, meta.fontFile);
            try
            {
                var font = CommonFunctions.TMPFontReplacer.LoadFontFromFile(fontPath);
                if (font != null)
                {
                    GlobalTMPFont = font;
                    GlobalFontPath = fontPath;
                    CommonFunctions.TMPFontReplacer.ApplyFontToAllTMP(font);
                    Logging.Info($"Applied language font: {font.name}");
                    return true;
                }
                else
                {
                    ToastNotifier.Show("Problem loading font. See BepInEx log.", 5f);
                    Logging.Warn($"Language font file not found or failed to load: {fontPath}");
                    return false;
                }
            }
            catch (Exception e)
            {
                ToastNotifier.Show("Problem loading font. See BepInEx log.", 5f);
                Logging.Error($"Error loading language font '{fontPath}': {e}");
                return false;
            }
        }

        public void ApplyFontImmediateWithFallback()
        {
            try
            {
                // Vanilla/original mode: restore fonts captured before any replacements.
                if (!LanguageManager.IsLoaded || LanguageManager.CurrentSummary == null)
                {
                    CommonFunctions.TMPFontReplacer.RestoreOriginalFonts();
                    return;
                }

                bool ok = TryApplyLanguageFont();
                if (!ok)
                {
                    // Always refresh fallback to avoid reusing stale previous-language font.
                    LoadGlobalFontFallback();
                    if (GlobalTMPFont != null)
                        CommonFunctions.TMPFontReplacer.ReplaceFont(GlobalFontPath);
                    else
                        Logging.Warn("No fallback font available to apply.");
                }
            }
            catch (Exception e)
            {
                Logging.Warn($"Failed to apply font immediately: {e.Message}");
            }
        }

        public void RefreshLocalizationInCurrentScene()
        {
            try
            {
                Logging.Info($"[Plugin] RefreshLocalizationInCurrentScene started. Language loaded: {LanguageManager.IsLoaded}");

                // Refresh FleeceTextSetter components first (they override texts in EveryCall mode)
                RefreshAllFleeceTextSetters();
                
                // Refresh TextSynchronizer components (for cutscenes, tutorials, etc.)
                TextSynchronizerPatch.RefreshAllSynchronizers();
                
                // Then refresh other UI
                UISettingsTabPatch.RefreshAllTabs();
                RefreshTranslatorSettingsMenus();
                RefreshLevelSelectUiIfPresent();

                // Apply fonts immediately
                ApplyFontImmediateWithFallback();

                // Apply textures immediately (not via coroutine)
                RefreshTexturesInCurrentScene();

                if (!LanguageManager.IsLoaded || !EnableTextureReplacementEntry.Value)
                {
                    Logging.Info("[Plugin] Restoring original textures (no language or disabled)");
                    UITextureReplacer.RestoreAll();
                }

                if (!LanguageManager.IsLoaded || !EnableAudioReplacementEntry.Value || LanguageManager.CurrentSummary == null)
                {
                    RestoreReplacedAudioSources();
                }
                else
                {
                    TextSynchronizerPatch.PreloadSceneReplacements();
                }

                // Очищаем кэш музыки при смене языка
                LevelMusicProfilePatch.ClearCache();

                Canvas.ForceUpdateCanvases();
                Logging.Info("[Plugin] RefreshLocalizationInCurrentScene completed successfully");
            }
            catch (Exception e)
            {
                Logging.Warn($"Failed to refresh localization in current scene: {e}");
            }
        }

        private static bool IsTextureReplacementEnabled()
        {
            return EnableTextureReplacementEntry != null && EnableTextureReplacementEntry.Value;
        }

        private static void TryApplyTextureToCommonLogoNodes()
        {
            try
            {
                var roots = SceneManager.GetActiveScene().GetRootGameObjects();
                if (roots == null || roots.Length == 0)
                    return;

                bool isEndCreditsScene = string.Equals(GetCurrentSceneName(), "#027_Special_EndCredits", StringComparison.Ordinal);

                foreach (var root in roots)
                {
                    if (root == null)
                        continue;

                    foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (tr == null)
                            continue;

                        string name = tr.name;
                        if (!string.Equals(name, "Logo", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(name, "Title Image", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        bool invertAlpha = isEndCreditsScene && IsEndCreditsFadeInLogoNode(tr);
                        TryApplyTextureToByTarget(tr.gameObject, invertAlpha);
                    }
                }
            }
            catch (Exception e)
            {
                Logging.Warn($"Failed to refresh common logo nodes: {e.Message}");
            }
        }

        private static bool IsEndCreditsFadeInLogoNode(Transform tr)
        {
            if (tr == null || !string.Equals(tr.name, "Logo", StringComparison.OrdinalIgnoreCase))
                return false;

            Transform fadeIn = tr.parent;
            if (fadeIn == null || !string.Equals(fadeIn.name, "Fade In", StringComparison.OrdinalIgnoreCase))
                return false;

            Transform creditsUi = fadeIn.parent;
            if (creditsUi == null || !string.Equals(creditsUi.name, "Credits UI", StringComparison.OrdinalIgnoreCase))
                return false;

            Transform creditAnchor = creditsUi.parent;
            if (creditAnchor == null || !string.Equals(creditAnchor.name, "Credit Anchor", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        private static void TryApplyTextureToByTarget(GameObject target, bool invertAlpha)
        {
            if (target == null)
                return;

            if (LanguageManager.CurrentSummary == null || !IsTextureReplacementEnabled())
            {
                UITextureReplacer.ApplyTo(target, null, invertAlpha);
                return;
            }

            string textureFile = Path.Combine(LanguageManager.CurrentSummary.Paths.TexturesDir, "UILogoText.png");
            UITextureReplacer.ApplyTo(target, File.Exists(textureFile) ? textureFile : null, invertAlpha);
        }

        private void RefreshAllFleeceTextSetters()
        {
            try
            {
                int refreshed = FleeceTextSetterPatch.RefreshAll(skipTranslatorMenu: true);
                Logging.Info($"[Plugin] Refreshed {refreshed} FleeceTextSetter components after language switch.");
            }
            catch (Exception e)
            {
                Logging.Warn($"[Plugin] RefreshAllFleeceTextSetters failed: {e.Message}");
            }
        }

        private static object FindFirstValueByType(object instance, Type wantedType)
        {
            if (instance == null || wantedType == null)
                return null;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var instanceType = instance.GetType();

            foreach (var field in instanceType.GetFields(flags))
            {
                if (!wantedType.IsAssignableFrom(field.FieldType))
                    continue;

                var value = field.GetValue(instance);
                if (value != null)
                    return value;
            }

            foreach (var property in instanceType.GetProperties(flags))
            {
                if (!property.CanRead || property.GetIndexParameters().Length != 0)
                    continue;
                if (!wantedType.IsAssignableFrom(property.PropertyType))
                    continue;

                try
                {
                    var value = property.GetValue(instance, null);
                    if (value != null)
                        return value;
                }
                catch
                {
                }
            }

            return null;
        }

        private void RefreshLevelSelectUiIfPresent()
        {
            try
            {
                Dictionary<string, string> levelNames = null;
                if (LanguageManager.IsLoaded)
                {
                    levelNames = LanguageManager.CurrentLanguage.levelNames;
                    if (levelNames == null)
                        levelNames = LanguageManager.CurrentLanguage.levelNames = new Dictionary<string, string>();
                }

                int refreshedButtons = 0;
                int refreshedButtonLabels = 0;
                var refreshInformationMethod = AccessTools.Method(typeof(UILevelSelectButton), "RefreshInformation");
                var nameTextField = AccessTools.Field(typeof(UILevelSelectButton), "nameText");

                // Refresh UILevelSelectButton to rebuild labels from source LevelInformation.
                foreach (var button in CommonFunctions.FindObjectsOfTypeCached<UILevelSelectButton>(true))
                {
                    if (button == null)
                        continue;

                    if (refreshInformationMethod != null)
                    {
                        try
                        {
                            refreshInformationMethod.Invoke(button, null);
                            refreshedButtons++;
                        }
                        catch
                        {
                        }
                    }

                    var nameText = nameTextField?.GetValue(button) as TMP_Text;
                    if (nameText == null || string.IsNullOrEmpty(nameText.text))
                        continue;

                    string source = ResolveOriginalTranslationKey(nameText.text, levelNames);
                    string before = nameText.text;
                    TranslateTextAndSaveIfMissing(nameText, source, levelNames, "[Plugin][LevelSelectButton]");
                    if (!string.Equals(before, nameText.text, StringComparison.Ordinal))
                        refreshedButtonLabels++;
                }

                // Run a second pass to cover stale labels when reflection invoke is unavailable.
                if (refreshInformationMethod == null)
                {
                    foreach (var button in CommonFunctions.FindObjectsOfTypeCached<UILevelSelectButton>(true))
                    {
                        if (button == null)
                            continue;

                        var nameText = nameTextField?.GetValue(button) as TMP_Text;
                        if (nameText == null || string.IsNullOrEmpty(nameText.text))
                            continue;

                        string source = ResolveOriginalTranslationKey(nameText.text, levelNames);
                        string before = nameText.text;
                        TranslateTextAndSaveIfMissing(nameText, source, levelNames, "[Plugin][LevelSelectButton]");
                        if (!string.Equals(before, nameText.text, StringComparison.Ordinal))
                            refreshedButtonLabels++;
                    }
                }

                int refreshedRoots = 0;
                var selectCategoryMethod = AccessTools.Method(typeof(UILevelSelectionRoot), "SelectCategory");
                var parameter = selectCategoryMethod?.GetParameters().FirstOrDefault();
                var parameterType = parameter?.ParameterType;

                if (selectCategoryMethod != null && parameterType != null)
                {
                    foreach (var root in CommonFunctions.FindObjectsOfTypeCached<UILevelSelectionRoot>(true))
                    {
                        if (root == null)
                            continue;

                        object argument = FindFirstValueByType(root, parameterType);
                        if (argument == null)
                            continue;

                        try
                        {
                            selectCategoryMethod.Invoke(root, new[] { argument });
                            refreshedRoots++;
                        }
                        catch
                        {
                        }
                    }
                }

                if (refreshedButtons > 0 || refreshedRoots > 0 || refreshedButtonLabels > 0)
                    Logging.Info($"[Plugin] Refreshed LevelSelect UI (buttons={refreshedButtons}, buttonLabels={refreshedButtonLabels}, roots={refreshedRoots}).");
            }
            catch (Exception e)
            {
                Logging.Warn($"[Plugin] RefreshLevelSelectUiIfPresent failed: {e.Message}");
            }
        }

        private void RefreshTranslatorSettingsMenus()
        {
            try
            {
                var menus = CommonFunctions.FindObjectsOfTypeCached<TranslatorSettingsMenu>(true);
                if (menus == null || menus.Length == 0)
                    return;

                foreach (var menu in menus)
                {
                    if (menu != null)
                        menu.RefreshLiveTextsAndState();
                }
            }
            catch (Exception e)
            {
                Logging.Warn($"Failed to refresh translator settings menus: {e.Message}");
            }
        }

        public static void RegisterReplacedAudioSource(AudioSource source, AudioClip originalClip)
        {
            if (source == null || originalClip == null)
                return;

            int id = source.GetInstanceID();
            if (!OriginalClipBySourceId.ContainsKey(id))
                OriginalClipBySourceId[id] = originalClip;
        }

        private static void RestoreReplacedAudioSources()
        {
            try
            {
                if (OriginalClipBySourceId.Count == 0)
                    return;

                var allSources = CommonFunctions.FindObjectsOfTypeCached<AudioSource>(true);
                if (allSources == null || allSources.Length == 0)
                    return;

                int restored = 0;
                var liveIds = new HashSet<int>();
                foreach (var source in allSources)
                {
                    if (source == null)
                        continue;

                    int id = source.GetInstanceID();
                    liveIds.Add(id);
                    if (!OriginalClipBySourceId.TryGetValue(id, out var originalClip) || originalClip == null)
                        continue;

                    if (source.clip == originalClip)
                        continue;

                    bool wasPlaying = source.isPlaying;
                    float previousTime = source.time;
                    source.clip = originalClip;
                    if (wasPlaying)
                    {
                        float clamped = Mathf.Clamp(previousTime, 0f, Mathf.Max(0f, originalClip.length - 0.01f));
                        source.time = clamped;
                        source.Play();
                    }
                    restored++;
                }

                var staleKeys = new List<int>();
                foreach (var id in OriginalClipBySourceId.Keys)
                {
                    if (!liveIds.Contains(id))
                        staleKeys.Add(id);
                }
                foreach (var id in staleKeys)
                    OriginalClipBySourceId.Remove(id);

                if (restored > 0)
                    Logging.Info($"[Plugin] Restored original audio clip on {restored} active AudioSource components.");
            }
            catch (Exception e)
            {
                Logging.Warn($"Failed to restore replaced audio sources: {e.Message}");
            }
        }

        private void Awake()
        {
            Instance = this;
            Debug.unityLogger.filterLogType = LogType.Exception;
            Logging.Warn($"I Am Your Translator Loading... | Version v.{PluginInfo.PLUGIN_VERSION}");

            try
            {
                // Config bootstrap
                SelectedLanguageEntry = Config.Bind("General", "SelectedLanguage", "", "Language code to load (folder name inside languages/). Leave empty for vanilla.");
                EnableAudioReplacementEntry = Config.Bind("General", "EnableAudioReplacement", true, "If true, custom language audio will replace originals when available.");
                EnableTextureReplacementEntry = Config.Bind("General", "EnableTextureReplacement", true, "If true, custom language textures will replace originals when available.");
                EnableAudioDebugLogsEntry = Config.Bind("Debug", "EnableAudioDebugLogs", false, "If true, verbose AudioSource.Play/OneShot logging is enabled.");
                EnableExperimentalRadioAudioPatchesEntry = Config.Bind("Experimental", "EnableExperimentalRadioAudioPatches", false, "If true, enables aggressive radio-camera audio patches (may cause instability).");

                TextSynchronizerPatch.SetCoroutineHost(this);
                Logging.Warn("--- Initializing Language Manager ---");

                // Ensure folder exists
                LanguageManager.EnsureLanguagesDirectory();

                // Load and cache the global font (once)
                LoadGlobalFontFallback();

                // Load selected language if set
                if (!string.IsNullOrEmpty(SelectedLanguageEntry.Value))
                {
                    if (!LanguageManager.LoadLanguage(SelectedLanguageEntry.Value))
                    {
                        Logging.Warn($"Failed to load language '{SelectedLanguageEntry.Value}', continuing with vanilla text.");
                    }
                    else
                    {
                        // Очищаем кэш музыки при загрузке нового языка
                        LevelMusicProfilePatch.ClearCache();
                    }
                }

                Logging.Warn("--- Patching vanilla game functions ---");

                // ✓ Important: specify assembly explicitly
                Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
                harmony.PatchAll(typeof(Plugin).Assembly);
                Logging.Info("[Plugin] Harmony patches applied successfully");
                Logging.Info("[Plugin] Expected patches: UISettingsTabPatch, SaveSystem_Patch, HUDTimerIncrease_Patch, UILevelCompletePopUpListing_Patch, UILevelCompleteTimeScoreBar_Patch, etc.");

                Logging.Warn("--- All done. Enjoy! ---");

                // Mark as ready BEFORE subscribing to sceneLoaded
                ready = true;
                SceneManager.sceneLoaded += OnSceneLoaded;

                // Font will be applied in OnSceneLoaded
                // Textures will be applied in OnSceneLoaded when Canvas is available
            }
            catch (Exception e)
            {
                Logging.Fatal("An error occurred while initialising!");
                Logging.Fatal(e.ToString());
                ready = false;
            }
        }

        private static void TryApplyTextureTo(string objectPath, bool invertAlpha = false)
        {
            GameObject target = GetObject(objectPath);
            if (target == null)
                return;

            TryApplyTextureToByTarget(target, invertAlpha);
        }
    }
}
