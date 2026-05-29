using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IAmYourTranslator.json;
using IAmYourTranslator;
using IAmYourTranslator.Patches;
using IAmYourTranslator.Core;
using Fleece;
using static IAmYourTranslator.CommonFunctions;

// Custom settings submenu for language selection built from duplicated "Toggle V Sync" rows.
public partial class TranslatorSettingsMenu : UISettingsSubMenu
{
    private TMP_Text currentLangText;
    private Toggle audioToggle;
    private Toggle textureToggle;
    private GameObject rowLanguageButton;
    private GameObject rowRemoteButton;
    private GameObject rowAudioToggle;
    private GameObject rowTextureToggle;
    private TMP_Text remoteOverlayText;
    private GameObject overlayRemote;
    private RectTransform listAnchor;

    private string pendingLanguage;
    private bool pendingAudio;
    private bool pendingTextures;
    private bool suppressToggleCallbacks;

    private const string DefaultTitle = "Languages";
    private const string CurrentLanguageLabel = "LANGUAGE:";
    private const string OriginalLanguageCode = "__ORIGINAL__";
    private const string OriginalLanguageDisplayName = "English (Original)";

    public void InitializeSelf(string title = DefaultTitle)
    {
        // Ensure menuName (Fleece.Jumper) has a passage title.
        try
        {
            string display = title;
            
            // Use langDisplayName from metadata if language is loaded
            if (LanguageManager.IsLoaded && LanguageManager.CurrentMetadata != null)
            {
                string langDisplayName = LanguageManager.CurrentMetadata.langDisplayName;
                if (!string.IsNullOrEmpty(langDisplayName))
                {
                    display = langDisplayName;
                }
            }
            // Fallback to settings translation if metadata doesn't have displayName
            else if (LanguageManager.IsLoaded && LanguageManager.CurrentLanguage?.settings != null &&
                LanguageManager.CurrentLanguage.settings.TryGetValue("Languages", out var translated) &&
                !string.IsNullOrEmpty(translated))
            {
                display = translated;
            }

            var jumper = new Fleece.Jumper();
            var passage = ScriptableObject.CreateInstance<Fleece.Passage>();
            {
                passage.title = display;
                passage.text = display;
                passage.id = 900001;
            }
            jumper.passage = passage;
            var field = typeof(UISettingsSubMenu).GetField("menuName", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(this, jumper);
        }
        catch { }
    }

    private void Awake()
    {
        pendingLanguage = Plugin.SelectedLanguageEntry?.Value ?? string.Empty;
        pendingAudio = Plugin.EnableAudioReplacementEntry?.Value ?? true;
        pendingTextures = Plugin.EnableTextureReplacementEntry?.Value ?? true;

        listAnchor = ResolveListingAnchor();
        if (listAnchor == null)
        {
            Logging.Error("[TranslatorSettingsMenu] Listing Anchor not found, UI build aborted.");
            return;
        }
        AlignListingAnchor(listAnchor);

        ResolveTemplatesFromSelf(listAnchor);
        var preservedRow = PrepareTemplateRow(listAnchor);
        EnsureBacking(listAnchor);
        EnsureLayout(listAnchor);

        var rows = BuildRows(preservedRow, 4);
        if (rows.Count == 4)
        {
            rowLanguageButton = rows[0];
            rowRemoteButton = rows[1];
            rowAudioToggle = rows[2];
            rowTextureToggle = rows[3];
            ConfigureMainRows();
        }
        else
        {
            Logging.Error("[TranslatorSettingsMenu] Failed to build 4 rows; menu will be empty.");
        }

        BuildRemoteOverlay();
        HideOverlays();

        BuildLanguagePage();
        HideLanguagePage();

        var font = CommonFunctions.TMPFontReplacer.GetCachedFont();
        if (font != null)
            CommonFunctions.ApplyFontToAllChildrenTMP(this, font);
    }

    private void ConfigureMainRows()
    {
        if (rowLanguageButton != null)
        {
            currentLangText = ConfigureRow(rowLanguageButton, CurrentLanguageLabel, "OPEN", false, false, () => ShowLanguagePage(), null, out _);
            UpdateCurrentLanguageText();
        }

        if (rowRemoteButton != null)
        {
            ConfigureRow(rowRemoteButton, "GET LANGUAGES ONLINE", "OPEN", false, false, () => OpenRemoteOverlay(), null, out _);
        }

        if (rowAudioToggle != null)
        {
            ConfigureRow(rowAudioToggle, "ENABLE AUDIO REPLACEMENTS", null, true, pendingAudio, null, v =>
            {
                pendingAudio = v;
                if (Plugin.EnableAudioReplacementEntry != null)
                    Plugin.EnableAudioReplacementEntry.Value = v;
                var plugin = Plugin.GetOrRecoverInstance();
                if (plugin != null)
                {
                    SceneRefresh.Refresh(plugin);
                }
                else
                {
                    // Fallback when plugin instance is temporarily unavailable.
                    CommonFunctions.RefreshAllSceneTexts(skipTranslatorSettingsMenu: false);
                    Canvas.ForceUpdateCanvases();
                }
            }, out audioToggle);
        }

        if (rowTextureToggle != null)
        {
            ConfigureRow(rowTextureToggle, "ENABLE TEXTURES REPLACEMENTS", null, true, pendingTextures, null, v =>
            {
                pendingTextures = v;
                if (Plugin.EnableTextureReplacementEntry != null)
                    Plugin.EnableTextureReplacementEntry.Value = v;
                var plugin = Plugin.GetOrRecoverInstance();
                if (plugin != null)
                {
                    SceneRefresh.Refresh(plugin);
                }
                else
                {
                    // Fallback when plugin instance is temporarily unavailable.
                    TextureLifecycle.RefreshTexturesInCurrentScene();
                    CommonFunctions.RefreshAllSceneTexts(skipTranslatorSettingsMenu: false);
                    Canvas.ForceUpdateCanvases();
                }
            }, out textureToggle);
        }
    }

    private void UpdateCurrentLanguageText()
    {
        if (currentLangText == null)
            return;

        // Always start with base label, not current text
        string baseLabel = CurrentLanguageLabel;
        
        // Translate the base label from settings
        var settings = LanguageManager.CurrentLanguage?.settings;
        if (settings != null && settings.TryGetValue(baseLabel, out var translatedLabel))
        {
            baseLabel = translatedLabel;
        }
        
        // Append language display name
        string displayName = GetDisplayName(pendingLanguage);
        currentLangText.text = baseLabel + " " + displayName;
    }

    private string GetDisplayName(string code)
    {
        if (string.IsNullOrEmpty(code) || string.Equals(code, OriginalLanguageCode, StringComparison.Ordinal))
            return OriginalLanguageDisplayName;
        var summary = LanguageManager.GetAvailableLanguages().FirstOrDefault(l => l.Code == code);
        return summary?.DisplayName ?? code;
    }

    public void RefreshLiveTextsAndState()
    {
        // Use "Languages" as default title for English (Original)
        string title = LanguageManager.IsLoaded && LanguageManager.CurrentMetadata != null && !string.IsNullOrEmpty(LanguageManager.CurrentMetadata.langDisplayName)
            ? LanguageManager.CurrentMetadata.langDisplayName
            : DefaultTitle;
            
        InitializeSelf(title);
        pendingLanguage = Plugin.SelectedLanguageEntry?.Value ?? string.Empty;
        pendingAudio = Plugin.EnableAudioReplacementEntry?.Value ?? pendingAudio;
        pendingTextures = Plugin.EnableTextureReplacementEntry?.Value ?? pendingTextures;

        var settings = LanguageManager.CurrentLanguage?.settings;

        suppressToggleCallbacks = true;
        try
        {
            ConfigureMainRows();

            // Force refresh all row texts
            RefreshAllRowTexts();

            if (remoteOverlayText != null)
            {
                const string remoteLabel = "Remote catalog is not available yet.";
                if (settings != null && settings.TryGetValue(remoteLabel, out var translatedRemote))
                {
                    remoteOverlayText.text = translatedRemote;
                }
                else
                {
                    remoteOverlayText.text = remoteLabel;
                }
            }

            if (backButtonText != null)
            {
                const string backLabel = "BACK";
                if (settings != null && settings.TryGetValue(backLabel, out var translatedBack))
                {
                    backButtonText.text = translatedBack;
                }
                else
                {
                    backButtonText.text = backLabel;
                }
            }

            if (audioToggle != null && audioToggle.isOn != pendingAudio)
                audioToggle.isOn = pendingAudio;
            if (textureToggle != null && textureToggle.isOn != pendingTextures)
                textureToggle.isOn = pendingTextures;

            UpdateCurrentLanguageText();
        }
        finally
        {
            suppressToggleCallbacks = false;
        }
    }

    private void RefreshAllRowTexts()
    {
        var settings = LanguageManager.CurrentLanguage?.settings;
        
        // Refresh language row
        if (rowLanguageButton != null && currentLangText != null)
        {
            // Clear cached original text to force refresh
            CommonFunctions.ClearOriginalTextCache(currentLangText);
            
            // Translate base label
            if (settings != null && settings.TryGetValue(CurrentLanguageLabel, out var translatedLabel))
            {
                currentLangText.text = translatedLabel;
            }
            else
            {
                currentLangText.text = CurrentLanguageLabel;
            }
            
            // Append language display name
            string displayName = GetDisplayName(pendingLanguage);
            currentLangText.text = currentLangText.text + " " + displayName;
        }

        // Refresh remote row
        if (rowRemoteButton != null)
        {
            var remoteText = FindTextByName(rowRemoteButton.transform, "Text (TMP)");
            if (remoteText != null)
            {
                CommonFunctions.ClearOriginalTextCache(remoteText);
                if (settings != null && settings.TryGetValue("GET LANGUAGES ONLINE", out var translatedRemote))
                {
                    remoteText.text = translatedRemote;
                }
                else
                {
                    remoteText.text = "GET LANGUAGES ONLINE";
                }
            }
            var remoteRightText = FindAlternateText(rowRemoteButton.transform, remoteText);
            if (remoteRightText != null)
            {
                CommonFunctions.ClearOriginalTextCache(remoteRightText);
                if (settings != null && settings.TryGetValue("OPEN", out var translatedOpen))
                {
                    remoteRightText.text = translatedOpen;
                }
                else
                {
                    remoteRightText.text = "OPEN";
                }
            }
        }

        // Refresh audio row
        if (rowAudioToggle != null)
        {
            var audioText = FindTextByName(rowAudioToggle.transform, "Text (TMP)");
            if (audioText != null)
            {
                CommonFunctions.ClearOriginalTextCache(audioText);
                if (settings != null && settings.TryGetValue("ENABLE AUDIO REPLACEMENTS", out var translatedAudio))
                {
                    audioText.text = translatedAudio;
                }
                else
                {
                    audioText.text = "ENABLE AUDIO REPLACEMENTS";
                }
            }
        }

        // Refresh texture row
        if (rowTextureToggle != null)
        {
            var textureText = FindTextByName(rowTextureToggle.transform, "Text (TMP)");
            if (textureText != null)
            {
                CommonFunctions.ClearOriginalTextCache(textureText);
                if (settings != null && settings.TryGetValue("ENABLE TEXTURES REPLACEMENTS", out var translatedTexture))
                {
                    textureText.text = translatedTexture;
                }
                else
                {
                    textureText.text = "ENABLE TEXTURES REPLACEMENTS";
                }
            }
        }
    }

    public override void SaveSettings()
    {
        Plugin.SelectedLanguageEntry.Value = pendingLanguage ?? string.Empty;
        Plugin.EnableAudioReplacementEntry.Value = pendingAudio;
        Plugin.EnableTextureReplacementEntry.Value = pendingTextures;

        if (!string.IsNullOrEmpty(pendingLanguage))
        {
            if (LanguageManager.LoadLanguage(pendingLanguage))
            {
                FontLifecycle.TryApplyLanguageFont();
            }
        }
    }

    public override void RevertSettings()
    {
        pendingLanguage = Plugin.SelectedLanguageEntry.Value;
        pendingAudio = Plugin.EnableAudioReplacementEntry.Value;
        pendingTextures = Plugin.EnableTextureReplacementEntry.Value;

        if (audioToggle != null) audioToggle.isOn = pendingAudio;
        if (textureToggle != null) textureToggle.isOn = pendingTextures;
        if (currentLangText != null) UpdateCurrentLanguageText();

        HideOverlays();
    }

    public override void SetToDefault()
    {
        pendingLanguage = string.Empty;
        pendingAudio = true;
        pendingTextures = true;
        RevertSettings();
    }

    public override bool ShowApplyButton()
    {
        return true;
    }

    public override void OptionAltered()
    {
        // Not used; we handle state directly.
    }

    // ---------- Overlay ----------
    private void BuildRemoteOverlay()
    {
        overlayRemote = CreateOverlayRoot("RemoteOverlay");
        var remoteLabel = "Remote catalog is not available yet.";
        remoteOverlayText = CreateOverlayText(overlayRemote.transform, remoteLabel);
        remoteOverlayText.fontSize = 32;
        TranslateTextAndSaveIfMissing(remoteOverlayText, remoteLabel, LanguageManager.CurrentLanguage?.settings, "[TranslatorSettingsMenu]");
    }

    private GameObject CreateOverlayRoot(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 1);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var le = go.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        var image = go.AddComponent<Image>();
        image.color = new Color(0, 0, 0, 0.75f);

        var cg = go.AddComponent<CanvasGroup>();
        cg.interactable = false;
        cg.blocksRaycasts = false;
        cg.alpha = 0f;

        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 10f;
        layout.padding = new RectOffset(20, 20, 20, 20);

        go.SetActive(false);
        return go;
    }

    private TMP_Text CreateOverlayText(Transform parent, string text)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 28;
        ApplyTemplateFont(tmp);
        return tmp;
    }

    private Button CreateOverlayButton(Transform parent, LanguageManager.LanguageSummary summary)
    {
        var go = new GameObject(summary.DisplayName ?? "entry");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.18f, 0.18f, 0.18f, 0.9f);
        var btn = go.AddComponent<Button>();

        var txtGO = new GameObject("Text");
        txtGO.transform.SetParent(go.transform, false);
        var tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.fontSize = 24;
        tmp.text = BuildSummaryLine(summary);
        ApplyTemplateFont(tmp);
        var rect = tmp.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(1, 1);
        rect.offsetMin = new Vector2(12, 6);
        rect.offsetMax = new Vector2(-12, -6);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 44);
        return btn;
    }

    private string BuildSummaryLine(LanguageManager.LanguageSummary summary)
    {
        if (summary == null)
            return string.Empty;
        if (string.IsNullOrEmpty(summary.Code))
            return summary.DisplayName ?? "Close";

        // Use DisplayName from metadata, not code
        string displayName = summary.DisplayName ?? summary.Code;
        
        string statusAudio = summary.Paths?.HasAudio == true ? "<color=green>[A]</color>" : "<color=red>[A]</color>";
        string statusFont = !string.IsNullOrEmpty(summary.FontFile) ? "<color=green>[F]</color>" : "<color=red>[F]</color>";
        string statusTex = summary.Paths?.HasTextures == true ? "<color=green>[T]</color>" : "<color=red>[T]</color>";
        string warn = summary.WarnIncompatible ? "<color=yellow>[!]</color> " : "";
        return $"{warn}{displayName} ({summary.Code}) v{summary.Version ?? "1.0"} {statusAudio}{statusFont}{statusTex}";
    }

    private void ShowOverlay(GameObject overlay)
    {
        if (overlay == null)
            return;
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
        var cg = overlay.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.blocksRaycasts = true;
            cg.interactable = true;
        }
    }

    private void HideOverlays()
    {
        HideOverlay(overlayRemote);
        HideLanguagePage();
    }

    private void SetMainMenuVisible(bool visible)
    {
        if (listAnchor == null)
            return;

        // If listAnchor is the same object as this component, we must not deactivate it,
        // otherwise we also deactivate the language page which is a child of the same root.
        if (listAnchor.transform == transform)
        {
            foreach (Transform child in transform)
            {
                if (child == null)
                    continue;

                if (languagePage != null && child == languagePage.transform)
                    continue;
                if (overlayRemote != null && child == overlayRemote.transform)
                    continue;

                // Keep hidden template clones untouched.
                if (child.name.StartsWith("Template", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.Equals(child.name, "TranslatorTemplates", StringComparison.OrdinalIgnoreCase))
                    continue;

                child.gameObject.SetActive(visible);
            }
            return;
        }

        listAnchor.gameObject.SetActive(visible);
    }

    private void HideOverlay(GameObject overlay)
    {
        if (overlay == null) return;
        var cg = overlay.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }
        overlay.SetActive(false);
    }

    private void OpenRemoteOverlay()
    {
        HideOverlays();
        ShowOverlay(overlayRemote);
    }

    // ---------- Language Selection ----------
    private void OnSelectLanguage(LanguageRowRefs row)
    {
        if (row == null || string.IsNullOrEmpty(row.Code))
            return;

        // ensure dictionary has persistent row ref
        languageRows[row.Code] = row;

        if (string.Equals(row.Code, OriginalLanguageCode, StringComparison.Ordinal))
        {
            Logging.Info("[TranslatorSettingsMenu] Switching to English (Original) - restoring original fonts and textures");
            
            // Reset global font so patches use original fonts
            Plugin.GlobalTMPFont = null;
            Plugin.GlobalFontPath = null;
            
            // Unload language first
            LanguageManager.UnloadLanguage();
            Plugin.SelectedLanguageEntry.Value = string.Empty;
            
            // Force restore original fonts
            Logging.Info("[TranslatorSettingsMenu] Calling RestoreOriginalFonts()...");
            CommonFunctions.TMPFontReplacer.RestoreOriginalFonts();
            
            // Force restore original textures
            CommonFunctions.UITextureReplacer.RestoreAll();
            
            UpdateLanguageRowStates(OriginalLanguageCode);
            pendingLanguage = string.Empty;
            UpdateCurrentLanguageText();
            
            // Refresh FleeceTextSetter components - they will use original text now
            Logging.Info("[TranslatorSettingsMenu] Refreshing FleeceTextSetter components...");
            FleeceTextSetterPatch.RefreshAll(skipTranslatorMenu: false);
            
            // Refresh settings tabs
            UISettingsTabPatch.RefreshAllTabs();
            
            // Force refresh all TMP text in scene
            Logging.Info("[TranslatorSettingsMenu] Refreshing all TMP text in scene...");
            var allTmpTexts = UnityEngine.Object.FindObjectsOfType<TMP_Text>(true);
            int refreshedCount = 0;
            foreach (var tmp in allTmpTexts)
            {
                if (tmp != null)
                {
                    tmp.ForceMeshUpdate();
                    refreshedCount++;
                }
            }
            Logging.Info($"[TranslatorSettingsMenu] Refreshed {refreshedCount} TMP_Text components");
            
            // Re-open settings menu to refresh all texts
            Logging.Info("[TranslatorSettingsMenu] Re-opening settings menu to refresh texts...");
            try
            {
                // Force refresh all tabs
                var tabs = UnityEngine.Object.FindObjectsOfType<UISettingsTab>(true);
                foreach (var tab in tabs)
                {
                    if (tab != null)
                    {
                        var subMenu = HarmonyLib.Traverse.Create(tab).Field("subMenu").GetValue<UISettingsSubMenu>();
                        if (subMenu != null)
                        {
                            // Force re-initialize tab name
                            var nameTextField = HarmonyLib.Traverse.Create(tab).Field("nameText").GetValue<TMP_Text>();
                            if (nameTextField != null)
                            {
                                // For TranslatorSettingsMenu, use "Languages" as default name
                                if (subMenu is TranslatorSettingsMenu)
                                {
                                    nameTextField.text = "Languages";
                                }
                                else
                                {
                                    // Get original name from subMenu
                                    var menuName = subMenu.GetMenuName();
                                    nameTextField.text = menuName;
                                }
                                nameTextField.ForceMeshUpdate();
                            }
                        }
                    }
                }
                Logging.Info("[TranslatorSettingsMenu] Settings menu refreshed");
            }
            catch (Exception e)
            {
                Logging.Warn($"[TranslatorSettingsMenu] Failed to refresh settings menu: {e.Message}");
            }

            // Refresh all row texts in this menu
            RefreshAllRowTexts();

            // Also refresh this menu's texts
            RefreshLiveTextsAndState();

            // Force full scene refresh so already translated UI (e.g. Level Select / tabs)
            // is resolved back to original English immediately.
            CommonFunctions.RefreshAllSceneTexts(skipTranslatorSettingsMenu: false);
            var plugin = Plugin.GetOrRecoverInstance();
                if (plugin != null)
                {
                    SceneRefresh.Refresh(plugin);
                }
            else
            {
                // Fallback path: apply texture/font/text refresh even without plugin instance.
                TextureLifecycle.RefreshTexturesInCurrentScene();
                CommonFunctions.RefreshAllSceneTexts(skipTranslatorSettingsMenu: false);
                Canvas.ForceUpdateCanvases();
                Logging.Warn("[TranslatorSettingsMenu] Plugin.Instance is null, used static fallback refresh after unload.");
            }

            Logging.Info("[TranslatorSettingsMenu] English (Original) applied - original fonts and textures restored");
            return;
        }

        if (!LanguageManager.LoadLanguage(row.Code))
        {
            ToastNotifier.Show("Failed to load language", 3f);
            return;
        }

        Plugin.SelectedLanguageEntry.Value = row.Code;
        var pluginApply = Plugin.GetOrRecoverInstance();
        if (pluginApply != null)
        {
            FontLifecycle.ApplyFontImmediateWithFallback();
        }
        else
        {
            // Fallback: still try to apply language font even if plugin instance was not recovered yet.
            FontLifecycle.TryApplyLanguageFont();
            TextureLifecycle.RefreshTexturesInCurrentScene();
            Logging.Warn("[TranslatorSettingsMenu] Plugin instance not found while applying selected language.");
        }

        UpdateLanguageRowStates(row.Code);
        pendingLanguage = row.Code;
        
        // First refresh this menu's texts
        RefreshLiveTextsAndState();

        // Full text refresh is needed only on live language switch.
        CommonFunctions.RefreshAllSceneTexts(skipTranslatorSettingsMenu: false);
        
        // Then refresh all other UI in the scene
        var pluginRefresh = Plugin.GetOrRecoverInstance();
        if (pluginRefresh != null)
        {
            SceneRefresh.Refresh(pluginRefresh);
        }
        else
        {
            // Fallback path: apply texture/font/text refresh even without plugin instance.
            TextureLifecycle.RefreshTexturesInCurrentScene();
            CommonFunctions.RefreshAllSceneTexts(skipTranslatorSettingsMenu: false);
            Canvas.ForceUpdateCanvases();
            Logging.Warn("[TranslatorSettingsMenu] Plugin instance not found; used static fallback refresh.");
        }
    }

    private void ShowLanguagePage()
    {
        HideOverlay(overlayRemote);

        RebuildLanguageList();
        transform.SetAsLastSibling();
        if (languagePage != null)
        {
            languagePage.SetActive(true);
            languagePage.transform.SetAsLastSibling();
        }
        SetMainMenuVisible(false);
        var scroll = languagePage != null ? languagePage.GetComponentInChildren<ScrollRect>(true) : null;
        if (scroll != null)
            scroll.verticalNormalizedPosition = 1f;
    }

    private void HideLanguagePage()
    {
        if (languagePage != null)
            languagePage.SetActive(false);
        SetMainMenuVisible(true);
    }
}
