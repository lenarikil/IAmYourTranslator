using BepInEx;
using HarmonyLib;
using System;
using System.IO;
using IAmYourTranslator.json;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        
        public bool ready;
        // Globally cached font for the entire mod
        public static TMP_FontAsset GlobalTMPFont;
    public static string GlobalFontPath;

        public void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            string fontsDir = Path.Combine(Paths.ConfigPath, "IAmYourTranslator", "fonts");
            string fontFile = Path.Combine(fontsDir, "Jovanny Lemonad - Bender-Bold.otf");
            // Apply the global font to all TMP in the scene (fallback — ReplaceFont uses cache)
            try
            {
                CommonFunctions.TMPFontReplacer.ReplaceFont(GlobalFontPath ?? fontFile);
            }
            catch (Exception e)
            {
                Logging.Warn($"Failed to apply global font on scene load: {e.Message}");
            }
            GameObject canvasObj = GetInactiveRootObject("Canvas");
            Core.HandleSceneSwitch(scene, ref canvasObj);

            string sceneName = GetCurrentSceneName();
            if (sceneName != "Bootstrap")
            {
                if (sceneName == "Start Screen")
                {
                    Logging.Info("Start Screen detected, initializing StartScreenHandler");
                    StartScreenHandler.HandleStartScreen();
                }
                if (sceneName == "TitleReveal")
                {
                    string TexturesDir = Path.Combine(Paths.ConfigPath, "IAmYourTranslator", "textures");
                    string TextureFile = Path.Combine(TexturesDir, "UILogoText.png");
                    GameObject TitleImage = GameObject.Find("Canvas/Logo");

                    UITextureReplacer.ApplyTo(TitleImage, TextureFile);
                }
                if (sceneName == "#027_Special_EndCredits")
                {
                    string TexturesDir = Path.Combine(Paths.ConfigPath, "IAmYourTranslator", "textures");
                    string TextureFile = Path.Combine(TexturesDir, "UILogoText.png");
                    GameObject TitleImage = GameObject.Find("[LEVEL DEPENDENCIES]/Credit Anchor/Credits UI/Fade In/Logo");
                    UITextureReplacer.ApplyTo(TitleImage, TextureFile, true);
                }

                PostInitPatches(canvasObj);
            }
        }

        public async void PostInitPatches(GameObject canvasObj)
        {
            await Task.Delay(500);
            Core.ApplyPostInitFixes(canvasObj);
        }

        private void Awake()
        {
            Debug.unityLogger.filterLogType = LogType.Exception;
            Logging.Warn($"I Am Your Translator Loading... | Version v.{PluginInfo.PLUGIN_VERSION}");

            try
            {
                Logging.Warn("--- Initializing Language Manager ---");

                // Minimal manager initialization
                // Path can be made fixed or through Config later
                string langFolder = Path.Combine(Paths.ConfigPath, "IAmYourTranslator", "languages");
                LanguageManager.LoadLanguage("en-GB");

                // Load and cache the global font (once)
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

                Logging.Warn("--- Patching vanilla game functions ---");

                // ✅ Important: specify assembly explicitly
                Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
                harmony.PatchAll(typeof(Plugin).Assembly);
                Logging.Info("[Plugin] Harmony patches applied successfully");
                Logging.Info("[Plugin] Expected patches: UISettingsTabPatch, SaveSystem_Patch, HUDTimerIncrease_Patch, UILevelCompletePopUpListing_Patch, UILevelCompleteTimeScoreBar_Patch, etc.");

                Logging.Warn("--- All done. Enjoy! ---");

                SceneManager.sceneLoaded += OnSceneLoaded;
                ready = true;
            }
            catch (Exception e)
            {
                Logging.Fatal("An error occurred while initialising!");
                Logging.Fatal(e.ToString());
                ready = false;
            }
        }

    }
}
