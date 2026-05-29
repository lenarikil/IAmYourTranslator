using System;
using UnityEngine;
using IAmYourTranslator.Patches;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.Core
{
    internal static class SceneRefresh
    {
        public static void Refresh(Plugin plugin)
        {
            try
            {
                Logging.Info($"[Plugin] RefreshLocalizationInCurrentScene started. Language loaded: {json.LanguageManager.IsLoaded}");

                RefreshAllFleeceTextSetters();

                TextSynchronizerPatch.RefreshAllSynchronizers();

                UISettingsTabPatch.RefreshAllTabs();
                RefreshTranslatorSettingsMenus();
                LevelSelectRefresh.Refresh();

                FontLifecycle.ApplyFontImmediateWithFallback();

                TextureLifecycle.RefreshTexturesInCurrentScene();

                if (!json.LanguageManager.IsLoaded || !Plugin.EnableTextureReplacementEntry.Value)
                {
                    Logging.Info("[Plugin] Restoring original textures (no language or disabled)");
                    UITextureReplacer.RestoreAll();
                }

                if (!json.LanguageManager.IsLoaded || !Plugin.EnableAudioReplacementEntry.Value || json.LanguageManager.CurrentSummary == null)
                {
                    AudioLifecycle.RestoreReplacedAudioSources();
                }
                else
                {
                    TextSynchronizerPatch.PreloadSceneReplacements();
                }

                LevelMusicProfilePatch.ClearCache();

                Canvas.ForceUpdateCanvases();
                Logging.Info("[Plugin] RefreshLocalizationInCurrentScene completed successfully");
            }
            catch (Exception e)
            {
                Logging.Warn($"Failed to refresh localization in current scene: {e}");
            }
        }

        private static void RefreshAllFleeceTextSetters()
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

        private static void RefreshTranslatorSettingsMenus()
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
    }
}
