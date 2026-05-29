using BepInEx;
using HarmonyLib;
using System;
using System.IO;
using IAmYourTranslator.json;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using IAmYourTranslator.Patches;
using BepInEx.Configuration;
using IAmYourTranslator.Core;

namespace IAmYourTranslator
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance;
        public bool ready;
        public static TMP_FontAsset GlobalTMPFont;
        public static string GlobalFontPath;

        public static ConfigEntry<string> SelectedLanguageEntry;
        public static ConfigEntry<bool> EnableAudioReplacementEntry;
        public static ConfigEntry<bool> EnableTextureReplacementEntry;
        public static ConfigEntry<bool> EnableAudioDebugLogsEntry;
        public static ConfigEntry<bool> EnableMusicProfileDebugLogsEntry;
        public static ConfigEntry<bool> EnableExperimentalRadioAudioPatchesEntry;
        public static PluginConfig ConfigEntries;

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
            if (Instance == null)
            {
                Instance = this;
                Logging.Warn("[Plugin] Instance was null in OnSceneLoaded, recovered.");
            }
            TextSynchronizerPatch.SetCoroutineHost(this);
            LevelMusicProfilePatch.SetCoroutineHost(this);

            if (!ready)
            {
                Logging.Warn($"[Plugin] OnSceneLoaded called before plugin ready (ready={ready}). Skipping.");
                return;
            }

            Logging.Info($"[Plugin] OnSceneLoaded: {scene.name}");

            CommonFunctions.InvalidateFindObjectsCache();

            FontLifecycle.ApplyFontImmediateWithFallback();

            GameObject canvasObj = CommonFunctions.GetInactiveRootObject("Canvas");
            Logging.Info($"Current scene: {CommonFunctions.GetCurrentSceneName()}");
            TextSynchronizerPatch.PreloadSceneReplacements();
            LevelMusicProfilePatch.PreloadLevelMusic();

            string sceneName = CommonFunctions.GetCurrentSceneName();
            if (sceneName != "Bootstrap")
            {
                if (sceneName == "Start Screen")
                {
                    Logging.Info("Start Screen detected, initializing StartScreenHandler");
                    StartScreenHandler.HandleStartScreen(this);
                }
                if (sceneName == "TitleReveal")
                {
                    TextureLifecycle.RefreshTexturesInCurrentScene();
                }
                if (sceneName == "#027_Special_EndCredits")
                {
                    TextureLifecycle.RefreshTexturesInCurrentScene();
                }

            }

            try
            {
                FontLifecycle.ApplyFontImmediateWithFallback();
                SceneRefresh.Refresh(this);
            }
            catch (Exception e)
            {
                Logging.Warn($"Failed to apply localization: {e.Message}");
                FontLifecycle.ApplyFontImmediateWithFallback();
                SceneRefresh.Refresh(this);
            }
        }

        private void Awake()
        {
            Instance = this;
            Debug.unityLogger.filterLogType = LogType.Exception;
            Logging.Warn($"I Am Your Translator Loading... | Version v.{PluginInfo.PLUGIN_VERSION}");

            try
            {
                ConfigEntries = new PluginConfig(Config);
                SelectedLanguageEntry = ConfigEntries.SelectedLanguage;
                EnableAudioReplacementEntry = ConfigEntries.EnableAudioReplacement;
                EnableTextureReplacementEntry = ConfigEntries.EnableTextureReplacement;
                EnableAudioDebugLogsEntry = ConfigEntries.EnableAudioDebugLogs;
                EnableMusicProfileDebugLogsEntry = ConfigEntries.EnableMusicProfileDebugLogs;
                EnableExperimentalRadioAudioPatchesEntry = ConfigEntries.EnableExperimentalRadioAudioPatches;

                TextSynchronizerPatch.SetCoroutineHost(this);
                Logging.Warn("--- Initializing Language Manager ---");

                LanguageManager.EnsureLanguagesDirectory();

                FontLifecycle.LoadGlobalFontFallback();

                if (!string.IsNullOrEmpty(SelectedLanguageEntry.Value))
                {
                    if (!LanguageManager.LoadLanguage(SelectedLanguageEntry.Value))
                    {
                        Logging.Warn($"Failed to load language '{SelectedLanguageEntry.Value}', continuing with vanilla text.");
                    }
                    else
                    {
                        LevelMusicProfilePatch.ClearCache();
                    }
                }

                Logging.Warn("--- Patching vanilla game functions ---");

                Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
                harmony.PatchAll(typeof(Plugin).Assembly);
                Logging.Info("[Plugin] Harmony patches applied successfully");
                Logging.Info("[Plugin] Expected patches: UISettingsTabPatch, SaveSystem_Patch, HUDTimerIncrease_Patch, UILevelCompletePopUpListing_Patch, UILevelCompleteTimeScoreBar_Patch, etc.");

                Logging.Warn("--- All done. Enjoy! ---");

                ready = true;
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
            catch (Exception e)
            {
                Logging.Fatal("An error occurred while initialising!");
                Logging.Fatal(e.ToString());
                ready = false;
            }
        }

        public void Update()
        {
            // Process batched language saves every frame
            LanguageManager.ProcessBatchedSave();
        }

        private void OnDestroy()
        {
            // Force save any pending language changes before unload
            LanguageManager.SaveCurrentLanguageImmediate();
        }
    }
}
