using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using IAmYourTranslator.json;
using static IAmYourTranslator.CommonFunctions;
using BepInEx;

namespace IAmYourTranslator.HarmonyPatches
{
    [HarmonyPatch]
    public static class UISettingsTabPatch
    {
        private const string TranslatorTabDefaultName = "Languages";
        private static readonly string[] TranslatorTabLegacyKeys =
        {
            "Languages",
            "Languages*",
            "LanguagesModMenu",
            "LanguagesModMenu*",
            "LanguagesMenu"
        };

        private static bool IsTranslatorSubMenu(UISettingsSubMenu subMenu)
        {
            if (subMenu is TranslatorSettingsMenu)
                return true;

            string goName = subMenu != null && subMenu.gameObject != null ? subMenu.gameObject.name : string.Empty;
            return !string.IsNullOrEmpty(goName) &&
                   goName.IndexOf("languages", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetTabNameForLookup(UISettingsSubMenu subMenu, out bool hadDirtySuffix)
        {
            hadDirtySuffix = false;
            if (subMenu == null)
                return string.Empty;

            // For TranslatorSettingsMenu, always use "Languages" as the key
            if (IsTranslatorSubMenu(subMenu))
                return TranslatorTabDefaultName;

            string raw = subMenu.GetMenuName();
            if (string.IsNullOrWhiteSpace(raw) && subMenu.gameObject != null)
                raw = subMenu.gameObject.name;
            raw = raw?.Trim() ?? string.Empty;

            if (raw.EndsWith("*", StringComparison.Ordinal))
            {
                hadDirtySuffix = true;
                raw = raw.Substring(0, raw.Length - 1).TrimEnd();
            }

            return raw;
        }

        private static string ResolveTabLabel(UISettingsSubMenu subMenu, string originalName, bool persistMissingKeys)
        {
            if (string.IsNullOrEmpty(originalName))
                return string.Empty;

            var settingsDict = LanguageManager.CurrentLanguage?.settings;
            if (IsTranslatorSubMenu(subMenu))
            {
                if (settingsDict != null)
                {
                    foreach (var key in TranslatorTabLegacyKeys)
                    {
                        if (settingsDict.TryGetValue(key, out var translated) && !string.IsNullOrEmpty(translated) && translated != key)
                            return translated;
                    }

                    if (persistMissingKeys && !settingsDict.ContainsKey(TranslatorTabDefaultName))
                    {
                        settingsDict[TranslatorTabDefaultName] = TranslatorTabDefaultName;
                        LanguageManager.SaveCurrentLanguage();
                        Logging.Info($"[UISettingsTab] Added new key to settings: '{TranslatorTabDefaultName}'");
                    }
                }

                return TranslatorTabDefaultName;
            }

            if (settingsDict == null)
                return originalName;

            if (!settingsDict.TryGetValue(originalName, out var foundTranslation))
            {
                if (persistMissingKeys)
                {
                    settingsDict[originalName] = originalName;
                    LanguageManager.SaveCurrentLanguage();
                    Logging.Info($"[UISettingsTab] Added new key to settings: '{originalName}'");
                }
                return originalName;
            }

            if (!string.IsNullOrEmpty(foundTranslation) && foundTranslation != originalName)
            {
                Logging.Info($"[UISettingsTab] Applied translation: '{originalName}' -> '{foundTranslation}'");
                return foundTranslation;
            }

            return originalName;
        }

        private static void ApplyTabLabel(UISettingsTab tab, UISettingsSubMenu subMenu, bool persistMissingKeys)
        {
            if (tab == null || subMenu == null)
                return;

            bool hadDirtySuffix;
            string originalName = GetTabNameForLookup(subMenu, out hadDirtySuffix);
            string resolvedText = ResolveTabLabel(subMenu, originalName, persistMissingKeys);
            if (!IsTranslatorSubMenu(subMenu) && hadDirtySuffix && !resolvedText.EndsWith("*", StringComparison.Ordinal))
                resolvedText += "*";

            var nameTextField = HarmonyLib.Traverse.Create(tab).Field("nameText").GetValue<TMP_Text>();
            if (nameTextField == null)
            {
                Logging.Warn("[UISettingsTab] nameText was null, skipping translation application");
                return;
            }

            nameTextField.text = resolvedText;

            var tmpFont = Plugin.GlobalTMPFont;
            if (tmpFont != null)
                TMPFontReplacer.ApplyFontToTMP(nameTextField, tmpFont);
        }

        public static void RefreshAllTabs()
        {
            try
            {
                var tabs = CommonFunctions.FindObjectsOfTypeCached<UISettingsTab>(true);
                if (tabs == null || tabs.Length == 0)
                    return;

                int refreshed = 0;
                foreach (var tab in tabs)
                {
                    if (tab == null)
                        continue;

                    var tr = HarmonyLib.Traverse.Create(tab);
                    var subMenu = tr.Field("subMenu").GetValue<UISettingsSubMenu>();
                    if (subMenu == null)
                        subMenu = tr.Field("menu").GetValue<UISettingsSubMenu>();
                    if (subMenu == null)
                        continue;

                    ApplyTabLabel(tab, subMenu, false);
                    refreshed++;
                }

                if (refreshed > 0)
                {
                    Canvas.ForceUpdateCanvases();
                    Logging.Info($"[UISettingsTab] Refreshed {refreshed} settings tabs for current language.");
                }
            }
            catch (Exception e)
            {
                Logging.Warn($"[UISettingsTab] RefreshAllTabs failed: {e.Message}");
            }
        }

        // ----------------------------
        // Initialize - translate menu name and apply font.
        // ----------------------------
        [HarmonyPatch(typeof(UISettingsTab), "Initialize")]
        [HarmonyPostfix]
        public static void InitializePostfix(UISettingsTab __instance, UISettingsSubMenu subMenu)
        {
            try
            {
                if (__instance == null || subMenu == null) return;
                ApplyTabLabel(__instance, subMenu, true);
            }
            catch (Exception e)
            {
                Logging.Error($"[UISettingsTab] Error in InitializePostfix: {e}");
            }
        }
    }
}
