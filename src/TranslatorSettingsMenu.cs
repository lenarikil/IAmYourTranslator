using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IAmYourTranslator.json;
using IAmYourTranslator;
using IAmYourTranslator.Harmony_Patches;
using IAmYourTranslator.HarmonyPatches;
using Fleece;
using static IAmYourTranslator.CommonFunctions;

// Custom settings submenu for language selection built from duplicated "Toggle V Sync" rows.
public class TranslatorSettingsMenu : UISettingsSubMenu
{
    // Style refs copied from template.
    public TMP_FontAsset TemplateFont;
    public Material TemplateFontMaterial;
    public GameObject TemplateTogglePrefab;
    public GameObject TemplateButtonPrefab;
    public GameObject TemplateBacking;

    private TMP_Text currentLangText;
    private Toggle audioToggle;
    private Toggle textureToggle;
    private GameObject rowLanguageButton;
    private GameObject rowRemoteButton;
    private GameObject rowAudioToggle;
    private GameObject rowTextureToggle;
    private TMP_Text remoteOverlayText;
    private TMP_Text backButtonText;
    private GameObject overlayRemote;
    private RectTransform listAnchor;
    private GameObject languagePage;
    private RectTransform languageContent;
    private Button backButton;
    private Dictionary<string, LanguageRowRefs> languageRows = new Dictionary<string, LanguageRowRefs>();
    private Transform templatesRoot;

    private Vector2 templateAnchorMin;
    private Vector2 templateAnchorMax;
    private Vector2 templatePivot;
    private Vector2 templateSizeDelta;
    private Vector2 templateAnchoredPos;
    private float templateFontSize = 26f;
    private float templateRightWidth = 140f;
    private float templateOutlineHeight = 0f;
    private int uiLayer = -1;
    private bool suppressToggleCallbacks;

    private string pendingLanguage;
    private bool pendingAudio;
    private bool pendingTextures;

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

    private TMP_Text CreateLabelRow(string text)
    {
        if (listAnchor == null)
            return null;

        var go = InstantiateButtonPrefab();
        if (go == null)
            go = new GameObject("Label");

        var btn = go.GetComponent<Button>();
        if (btn != null) btn.interactable = false;
        foreach (var img in go.GetComponentsInChildren<Image>(true))
        {
            var n = img.name.ToLowerInvariant();
            if (n.Contains("arrow") || n.Contains("left") || n.Contains("right"))
                img.enabled = false;
        }

        DisableOptionComponents(go);

        var tmp = go.GetComponentInChildren<TMP_Text>(true) ?? go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 28;
        tmp.alignment = TextAlignmentOptions.Left;
        ApplyTemplateFont(tmp);

        go.transform.SetParent(listAnchor, false);
        return tmp;
    }

    private int ResolveUILayer()
    {
        if (uiLayer >= 0)
            return uiLayer;
        uiLayer = LayerMask.NameToLayer("UI");
        return uiLayer;
    }

    private void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null || layer < 0)
            return;
        root.gameObject.layer = layer;
        foreach (Transform child in root)
            SetLayerRecursively(child, layer);
    }

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

    private void ApplyTemplateFont(TMP_Text tmp)
    {
        if (tmp == null)
            return;

        if (TemplateFont != null)
            tmp.font = TemplateFont;
        if (TemplateFontMaterial != null)
            tmp.fontMaterial = new Material(TemplateFontMaterial);
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

    private string GetDisplayName(string code)
    {
        if (string.IsNullOrEmpty(code) || string.Equals(code, OriginalLanguageCode, StringComparison.Ordinal))
            return OriginalLanguageDisplayName;
        var summary = LanguageManager.GetAvailableLanguages().FirstOrDefault(l => l.Code == code);
        return summary?.DisplayName ?? code;
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
                    plugin.RefreshLocalizationInCurrentScene();
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
                    plugin.RefreshLocalizationInCurrentScene();
                }
                else
                {
                    // Fallback when plugin instance is temporarily unavailable.
                    Plugin.RefreshTexturesInCurrentScene();
                    CommonFunctions.RefreshAllSceneTexts(skipTranslatorSettingsMenu: false);
                    Canvas.ForceUpdateCanvases();
                }
            }, out textureToggle);
        }
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
                Plugin.TryApplyLanguageFont();
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

    // ---------- Helpers ----------
    private RectTransform ResolveListingAnchor()
    {
        var listingTransform = RecursiveFindChild(transform, "Listing Anchor");
        if (listingTransform == null)
            listingTransform = transform; // fall back to root of cloned menu

        var rtFound = listingTransform as RectTransform;
        if (rtFound == null)
            rtFound = listingTransform.GetComponent<RectTransform>();
        if (rtFound == null)
            rtFound = listingTransform.gameObject.AddComponent<RectTransform>();
        return rtFound;
    }

    private void AlignListingAnchor(RectTransform rt)
    {
        if (rt == null)
            return;
        if (rt.transform == transform)
            return;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.offsetMin = new Vector2(0f, rt.offsetMin.y);
        rt.offsetMax = new Vector2(0f, rt.offsetMax.y);
    }

    private void ResolveTemplatesFromSelf(Transform listingAnchor)
    {
        if (listingAnchor == null)
            return;

        if (TemplateButtonPrefab == null)
        {
            var template = FindRowTemplate<UISettingsOptionList>(listingAnchor);
            if (template != null)
                TemplateButtonPrefab = CreateTemplateClone(template, "TemplateButtonRow");
        }
        if (TemplateTogglePrefab == null)
        {
            var template = FindRowTemplate<UISettingsOptionToggle>(listingAnchor);
            if (template != null)
                TemplateTogglePrefab = CreateTemplateClone(template, "TemplateToggleRow");
        }
        if (TemplateBacking == null)
        {
            var backing = listingAnchor.Find("Backing");
            if (backing != null)
                TemplateBacking = CreateTemplateClone(backing.gameObject, "TemplateBacking");
        }

        if (TemplateFont == null || TemplateFontMaterial == null)
        {
            var sampleText = GetComponentInChildren<TMP_Text>(true);
            if (sampleText != null)
            {
                if (TemplateFont == null)
                    TemplateFont = sampleText.font;
                if (TemplateFontMaterial == null)
                    TemplateFontMaterial = sampleText.fontMaterial;
            }
        }
    }

    private GameObject CreateTemplateClone(GameObject source, string name)
    {
        if (source == null)
            return null;

        var clone = Instantiate(source, EnsureTemplatesRoot());
        clone.name = name;
        clone.SetActive(false);
        return clone;
    }

    private Transform EnsureTemplatesRoot()
    {
        if (templatesRoot != null)
            return templatesRoot;

        var existing = transform.Find("TranslatorTemplates");
        if (existing != null)
        {
            templatesRoot = existing;
            return templatesRoot;
        }

        var go = new GameObject("TranslatorTemplates");
        go.SetActive(false);
        go.transform.SetParent(transform, false);

        var le = go.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        templatesRoot = go.transform;
        return templatesRoot;
    }

    private static GameObject FindRowTemplate<T>(Transform listingAnchor) where T : Component
    {
        if (listingAnchor == null)
            return null;

        var comp = listingAnchor.GetComponentInChildren<T>(true);
        if (comp == null)
            return null;

        var current = comp.transform;
        while (current.parent != null && current.parent != listingAnchor)
            current = current.parent;
        return current.gameObject;
    }

    private void EnsureBacking(Transform listingAnchor)
    {
        if (listingAnchor == null)
            return;

        var backing = listingAnchor.Find("Backing");
        if (backing == null && TemplateBacking != null)
        {
            var newBacking = Instantiate(TemplateBacking, listingAnchor);
            newBacking.name = "Backing";
            backing = newBacking.transform;
        }
        if (backing != null)
        {
            backing.gameObject.SetActive(true);
            backing.SetAsFirstSibling();
        }
    }

    private void EnsureLayout(RectTransform listingAnchor)
    {
        if (listingAnchor == null)
            return;

        var layout = listingAnchor.gameObject.GetComponent<VerticalLayoutGroup>() ??
                     listingAnchor.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.spacing = 6f;
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        var fitterExisting = listingAnchor.gameObject.GetComponent<ContentSizeFitter>();
        if (listingAnchor.transform == transform)
        {
            if (fitterExisting != null)
                DestroyImmediate(fitterExisting);
        }
        else
        {
            var fitter = fitterExisting ?? listingAnchor.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    private static Transform FindCheckObject(Transform root)
    {
        if (root == null)
            return null;

        var direct = root.Find("Check");
        if (direct != null)
            return direct;

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == null) continue;
            var name = t.name.ToLowerInvariant();
            if (name.Contains("check"))
                return t;
        }
        return null;
    }

    private TMP_Text CreateRightLabel(GameObject row, Transform check)
    {
        if (row == null)
            return null;

        Transform parent = row.transform;
        RectTransform sourceRt = null;
        if (check != null)
        {
            parent = check.parent != null ? check.parent : row.transform;
            sourceRt = check.GetComponent<RectTransform>();
        }

        var go = new GameObject("RightLabel");
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        var rt = tmp.rectTransform;

        if (sourceRt != null)
        {
            rt.anchorMin = sourceRt.anchorMin;
            rt.anchorMax = sourceRt.anchorMax;
            rt.pivot = sourceRt.pivot;
            rt.anchoredPosition = sourceRt.anchoredPosition;
            rt.sizeDelta = sourceRt.sizeDelta;
        }
        else
        {
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(140f, 32f);
            rt.anchoredPosition = new Vector2(-16f, 0f);
        }

        // Center align the text
        tmp.alignment = TextAlignmentOptions.Center;
        
        return tmp;
    }

    private TMP_Text ConfigureRow(GameObject row, string leftLabel, string rightLabel, bool isCheckbox, bool initialValue, Action onClick, Action<bool> onToggleChanged, out Toggle toggleOut)
    {
        if (row == null)
        {
            toggleOut = null;
            return null;
        }

        DisableOptionComponents(row);
        StripLegacyUIToggleButtons(row.transform);

        // Prefer to keep Toggle only for checkboxes; for buttons use nested "UI ToggleButton" child Button
        var toggle = row.GetComponent<Toggle>();
        toggleOut = toggle;

        // Find nested container that actually has Button visuals
        Transform buttonContainer = RecursiveFindByName(row.transform, "UI ToggleButton");
        var btn = buttonContainer != null ? buttonContainer.GetComponent<Button>() : row.GetComponent<Button>();
        if (btn != null)
            btn.onClick.RemoveAllListeners();

        var check = FindCheckObject(row.transform);

        var mainText = FindTextByName(row.transform, "Text (TMP)");
        if (mainText == null)
        {
            Logging.Warn("[TranslatorSettingsMenu] Text (TMP) missing; creating new TMP component.");
            var txtGO = new GameObject("Text (TMP)");
            txtGO.transform.SetParent(row.transform, false);
            mainText = txtGO.AddComponent<TextMeshProUGUI>();
        }

        RemoveFleeceSetter(mainText);

        mainText.text = leftLabel;
        mainText.fontSize = templateFontSize;
        mainText.alignment = TextAlignmentOptions.Left;
        mainText.enableWordWrapping = false;
        mainText.overflowMode = TextOverflowModes.Truncate;
        mainText.maxVisibleLines = 1;
        mainText.raycastTarget = false;
        ApplyTemplateFont(mainText);
        // Always try to translate, even if language is not loaded
        TranslateTextAndSaveIfMissing(mainText, leftLabel, LanguageManager.CurrentLanguage?.settings, "[TranslatorSettingsMenu]");

        if (string.IsNullOrEmpty(rightLabel))
        {
            var maybeRight = FindAlternateText(row.transform, mainText);
            if (maybeRight != null)
                maybeRight.gameObject.SetActive(false);
        }
        else
        {
            var rightText = FindAlternateText(row.transform, mainText) ?? CreateRightLabel(row, check);
            if (rightText == null)
            {
                var rtGo = new GameObject("RightLabel");
                rtGo.transform.SetParent(row.transform, false);
                rightText = rtGo.AddComponent<TextMeshProUGUI>();
            }
            RemoveFleeceSetter(rightText);
            rightText.gameObject.SetActive(true);
            rightText.text = rightLabel;
            rightText.fontSize = templateFontSize;
            rightText.alignment = TextAlignmentOptions.Center;
            rightText.enableWordWrapping = false;
            rightText.overflowMode = TextOverflowModes.Truncate;
            rightText.maxVisibleLines = 1;
            rightText.raycastTarget = true;
            rightText.color = Color.white;
            ApplyTemplateFont(rightText);
            // Always try to translate, even if language is not loaded
            TranslateTextAndSaveIfMissing(rightText, rightLabel, LanguageManager.CurrentLanguage?.settings, "[TranslatorSettingsMenu]");
            var rtRight = rightText.GetComponent<RectTransform>();
            if (rtRight != null)
            {
                float padX = 4f;
                float padY = 2f;
                float paddedWidth = rightText.preferredWidth + padX * 2f;
                float paddedHeight = rightText.preferredHeight + padY * 2f;
                float outlineHeight = templateOutlineHeight > 0f ? templateOutlineHeight : paddedHeight;
                paddedHeight = Mathf.Min(paddedHeight, Mathf.Max(0f, outlineHeight - 2f));
                rtRight.sizeDelta = new Vector2(paddedWidth, paddedHeight);
                rtRight.anchorMin = new Vector2(1f, 0.5f);
                rtRight.anchorMax = new Vector2(1f, 0.5f);
                rtRight.pivot = new Vector2(1f, 0.5f);
                rtRight.anchoredPosition = Vector2.zero;
            }
            AdjustOutlineToRightLabel(rtRight, row.transform);
        }

        if (toggle != null)
            toggle.onValueChanged.RemoveAllListeners();

        if (isCheckbox)
        {
            if (toggle == null)
                toggle = row.GetComponent<Toggle>() ?? row.AddComponent<Toggle>();
            toggleOut = toggle;

            var checkImage = check != null ? check.GetComponent<Image>() : null;
            var checkRaw = check != null ? check.GetComponent<RawImage>() : null;
            Graphic checkGraphic = null;
            if (checkImage != null)
                checkGraphic = checkImage;
            else if (checkRaw != null)
                checkGraphic = checkRaw;
            if (toggle.targetGraphic == null)
            {
                var outline = FindOutlineImage(row.transform);
                toggle.targetGraphic = outline != null ? outline : row.GetComponentInChildren<Image>(true);
            }
            if (toggle.graphic == null && checkGraphic != null)
                toggle.graphic = checkGraphic;
            toggle.navigation = new Navigation { mode = Navigation.Mode.None };
            toggle.group = null;
            if (checkImage != null)
            {
                checkImage.enabled = true;
                checkImage.raycastTarget = false;
            }
            if (checkRaw != null)
            {
                checkRaw.enabled = true;
                checkRaw.raycastTarget = false;
            }

            toggle.onValueChanged.RemoveAllListeners();
            toggle.isOn = initialValue;
            toggle.onValueChanged.AddListener(v =>
            {
                if (check != null)
                    check.gameObject.SetActive(v);
                if (checkImage != null && !checkImage.enabled)
                    checkImage.enabled = true;
                if (checkRaw != null && !checkRaw.enabled)
                    checkRaw.enabled = true;
                if (!suppressToggleCallbacks)
                    onToggleChanged?.Invoke(v);
            });

            BindToggleToRowClicks(row, toggle);
            if (check != null)
                check.gameObject.SetActive(initialValue);
            if (checkImage != null && !checkImage.enabled)
                checkImage.enabled = true;
            if (checkRaw != null && !checkRaw.enabled)
                checkRaw.enabled = true;
        }
        else
        {
            if (check != null) check.gameObject.SetActive(false);
            // Use Button on the visual container instead of Toggle
            if (toggle != null)
            {
                UnityEngine.Object.DestroyImmediate(toggle);
                toggleOut = null;
            }
            if (buttonContainer == null)
                buttonContainer = row.transform;
            btn = buttonContainer.GetComponent<Button>() ?? buttonContainer.gameObject.AddComponent<Button>();
            var uiToggleButton = buttonContainer.GetComponent("UIToggleButton") as Component;
            if (uiToggleButton != null)
                UnityEngine.Object.DestroyImmediate(uiToggleButton);
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => onClick?.Invoke());
            if (btn.targetGraphic == null)
            {
                var outlineImg = FindOutlineImage(row.transform);
                if (outlineImg != null)
                {
                    btn.targetGraphic = outlineImg;
                }
            }

            // Hide large backing for button rows to avoid oversized hit/outline areas
            var backing = RecursiveFindByName(row.transform, "Backing");
            if (backing != null)
                backing.gameObject.SetActive(false);
        }

        if (toggleOut != null && toggleOut.targetGraphic == null)
            toggleOut.targetGraphic = row.GetComponentInChildren<Image>(true);

        return mainText;
    }

    private void BindToggleToRowClicks(GameObject row, Toggle toggle)
    {
        if (row == null || toggle == null)
            return;

        bool hasAnyButton = false;
        foreach (var button in row.GetComponentsInChildren<Button>(true))
        {
            if (button == null)
                continue;

            hasAnyButton = true;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => { toggle.isOn = !toggle.isOn; });
        }

        if (hasAnyButton)
            return;

        var rowButton = row.GetComponent<Button>() ?? row.AddComponent<Button>();
        rowButton.navigation = new Navigation { mode = Navigation.Mode.None };
        rowButton.onClick.RemoveAllListeners();
        rowButton.onClick.AddListener(() => { toggle.isOn = !toggle.isOn; });

        if (rowButton.targetGraphic == null)
        {
            var outline = FindOutlineImage(row.transform);
            if (outline != null)
                rowButton.targetGraphic = outline;
        }
    }

    private void StripLegacyUIToggleButtons(Transform root)
    {
        if (root == null)
            return;

        foreach (var tr in root.GetComponentsInChildren<Transform>(true))
        {
            if (tr == null)
                continue;

            var legacy = tr.GetComponent("UIToggleButton") as Component;
            if (legacy != null)
                DestroyImmediate(legacy);
        }
    }

    private void RemoveFleeceSetter(Component c)
    {
        if (c == null)
            return;
        var fleece = c.GetComponent("FleeceTextSetter") as Component;
        if (fleece != null)
            DestroyImmediate(fleece);
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        if (go == null)
            return null;
        return go.GetComponent<T>() ?? go.AddComponent<T>();
    }

    private static void CopyImageStyle(Image source, Image target)
    {
        if (source == null || target == null)
            return;

        target.sprite = source.sprite;
        target.type = source.type;
        target.material = source.material;
        target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        target.preserveAspect = source.preserveAspect;
    }

    private void RemoveComponentByName(GameObject go, string typeName)
    {
        if (go == null || string.IsNullOrEmpty(typeName))
            return;

        var comp = go.GetComponent(typeName) as Component;
        if (comp != null)
            DestroyImmediate(comp);
    }

    private Image ResolveOutlineReference(Image preferred = null)
    {
        if (preferred != null && preferred.sprite != null)
            return preferred;

        var templateBacking = TemplateBacking != null ? TemplateBacking.GetComponent<Image>() : null;
        if (templateBacking != null && templateBacking.sprite != null)
            return templateBacking;

        var backOutline = backButton != null ? FindOutlineImage(backButton.transform) : null;
        if (backOutline != null && backOutline.sprite != null)
            return backOutline;

        var templateOutline = TemplateTogglePrefab != null ? FindOutlineImage(TemplateTogglePrefab.transform) : null;
        if (templateOutline != null && templateOutline.sprite != null)
            return templateOutline;

        return preferred ?? backOutline ?? templateOutline;
    }

    private Image ResolveFillReference(Image preferred = null)
    {
        if (preferred != null && preferred.sprite != null)
            return preferred;

        var backBacking = backButton != null ? RecursiveFindByName(backButton.transform, "Backing") : null;
        var backBackingImg = backBacking != null ? backBacking.GetComponent<Image>() : null;
        if (backBackingImg != null && backBackingImg.sprite != null)
            return backBackingImg;

        var templateBacking = TemplateTogglePrefab != null
            ? (RecursiveFindByName(TemplateTogglePrefab.transform, "Backing") ?? RecursiveFindByName(TemplateTogglePrefab.transform, "Background"))
            : null;
        var templateBackingImg = templateBacking != null ? templateBacking.GetComponent<Image>() : null;
        if (templateBackingImg != null && templateBackingImg.sprite != null)
            return templateBackingImg;

        return ResolveOutlineReference(preferred);
    }

    private void StripInteractables(Transform root)
    {
        if (root == null)
            return;

        foreach (var selectable in root.GetComponentsInChildren<Selectable>(true))
        {
            if (selectable != null)
                DestroyImmediate(selectable);
        }

        foreach (var tr in root.GetComponentsInChildren<Transform>(true))
        {
            if (tr == null)
                continue;
            var uiToggleButton = tr.GetComponent("UIToggleButton") as Component;
            if (uiToggleButton != null)
                DestroyImmediate(uiToggleButton);
        }
    }

    private TMP_Text FindTextByName(Transform root, string nameExact)
    {
        if (root == null)
            return null;
        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t != null && string.Equals(t.gameObject.name, nameExact, StringComparison.OrdinalIgnoreCase))
                return t;
        }
        return null;
    }

    private TMP_Text FindAlternateText(Transform root, TMP_Text primary)
    {
        if (root == null)
            return null;
        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t == null) continue;
            if (primary != null && t == primary) continue;
            return t;
        }
        return null;
    }

    private Image FindOutlineImage(Transform root)
    {
        if (root == null)
            return null;
        var outline = RecursiveFindByName(root, "Outline");
        return outline != null ? outline.GetComponent<Image>() : null;
    }

    private RectTransform RecursiveFindByName(Transform root, string name)
    {
        if (root == null) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t != null && string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase))
                return t as RectTransform;
        }
        return null;
    }

    private void AdjustOutlineToRightLabel(RectTransform rightRT, Transform rowRoot)
    {
        if (rightRT == null || rowRoot == null)
            return;

        var outlineRt = RecursiveFindByName(rowRoot, "Outline");
        if (outlineRt == null)
            return;

        outlineRt.gameObject.SetActive(true);
        outlineRt.anchorMin = rightRT.anchorMin;
        outlineRt.anchorMax = rightRT.anchorMax;
        outlineRt.pivot = rightRT.pivot;
        outlineRt.anchoredPosition = rightRT.anchoredPosition;

        float padX = 2f;
        float outlineHeight = templateOutlineHeight > 0f ? templateOutlineHeight : rightRT.sizeDelta.y;
        outlineRt.sizeDelta = new Vector2(rightRT.sizeDelta.x + padX * 2f, outlineHeight);
    }

    // ---------- Language Page ----------
    private class LanguageRowRefs
    {
        public GameObject Root;
        public TMP_Text Name;
        public Image AudioBox;
        public Image TextureBox;
        public Image FontBox;
        public Button SelectButton;
        public TMP_Text SelectText;
        public Image SelectOutline;
        public Image SelectBacking;
        public string Code;
    }

    private void BuildLanguagePage()
    {
        languagePage = new GameObject("LanguagePage");
        languagePage.transform.SetParent(transform, false);
        var layer = ResolveUILayer();
        if (layer >= 0)
            SetLayerRecursively(languagePage.transform, layer);
        var rt = languagePage.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);

        var le = languagePage.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        var bgTemplateImage = TemplateBacking != null ? TemplateBacking.GetComponent<Image>() : null;
        var pageOutlineRef = ResolveOutlineReference(TemplateTogglePrefab != null ? FindOutlineImage(TemplateTogglePrefab.transform) : null);
        var pageFillRef = ResolveFillReference(bgTemplateImage);

        // Backdrop fill lives inside shape mask so it does not bleed outside cut corners.
        var bg = languagePage.AddComponent<Image>();
        bg.color = Color.clear;
        bg.raycastTarget = false;

        if (pageOutlineRef != null && pageOutlineRef.sprite != null)
        {
            var fillMaskGO = new GameObject("PageFillMask");
            fillMaskGO.transform.SetParent(languagePage.transform, false);
            if (layer >= 0)
                SetLayerRecursively(fillMaskGO.transform, layer);

            var fillMaskRT = fillMaskGO.AddComponent<RectTransform>();
            fillMaskRT.anchorMin = new Vector2(0f, 0f);
            fillMaskRT.anchorMax = new Vector2(1f, 1f);
            fillMaskRT.offsetMin = new Vector2(8f, 8f);
            fillMaskRT.offsetMax = new Vector2(-8f, -8f);

            var fillMaskImage = fillMaskGO.AddComponent<Image>();
            CopyImageStyle(pageFillRef != null ? pageFillRef : pageOutlineRef, fillMaskImage);
            fillMaskImage.color = Color.white;
            fillMaskImage.raycastTarget = false;

            var fillMask = fillMaskGO.AddComponent<Mask>();
            fillMask.showMaskGraphic = false;

            var fillGO = new GameObject("PageFill");
            fillGO.transform.SetParent(fillMaskGO.transform, false);
            if (layer >= 0)
                SetLayerRecursively(fillGO.transform, layer);

            var fillRT = fillGO.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;

            var fillImage = fillGO.AddComponent<Image>();
            CopyImageStyle(pageFillRef != null ? pageFillRef : pageOutlineRef, fillImage);
            fillImage.color = new Color(0f, 0f, 0f, 0.6f);
            fillImage.raycastTarget = false;
        }

        // Outer border for the whole language page (same visual family as OPEN/BACK outline).
        if (pageOutlineRef != null && pageOutlineRef.sprite != null)
        {
            var frameGO = new GameObject("PageOutline");
            frameGO.transform.SetParent(languagePage.transform, false);
            if (layer >= 0)
                SetLayerRecursively(frameGO.transform, layer);

            var frameRT = frameGO.AddComponent<RectTransform>();
            frameRT.anchorMin = new Vector2(0f, 0f);
            frameRT.anchorMax = new Vector2(1f, 1f);
            frameRT.offsetMin = new Vector2(8f, 8f);
            frameRT.offsetMax = new Vector2(-8f, -8f);

            var frameImage = frameGO.AddComponent<Image>();
            CopyImageStyle(pageOutlineRef, frameImage);
            frameImage.color = Color.black;
            frameImage.raycastTarget = false;
        }

        if (templateSizeDelta == Vector2.zero)
            templateSizeDelta = new Vector2(400f, 40f);
        if (templateFontSize <= 0f)
            templateFontSize = 26f;

        // Scroll area
        var scrollGO = new GameObject("LanguageScroll");
        scrollGO.transform.SetParent(languagePage.transform, false);
        var scrollRT = scrollGO.AddComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0f, 0.15f);
        scrollRT.anchorMax = new Vector2(1f, 1f);
        scrollRT.offsetMin = new Vector2(20f, 20f);
        scrollRT.offsetMax = new Vector2(-20f, -20f);

        var scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.vertical = true;
        scroll.horizontal = false;

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollGO.transform, false);
        if (layer >= 0)
            SetLayerRecursively(viewport.transform, layer);
        var viewportRT = viewport.AddComponent<RectTransform>();
        viewportRT.anchorMin = new Vector2(0, 0);
        viewportRT.anchorMax = new Vector2(1, 1);
        viewportRT.offsetMin = Vector2.zero;
        viewportRT.offsetMax = Vector2.zero;
        viewport.AddComponent<RectMask2D>();
        var vpImage = viewport.AddComponent<Image>();
        vpImage.color = Color.clear;
        vpImage.raycastTarget = true;

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        if (layer >= 0)
            SetLayerRecursively(content.transform, layer);
        languageContent = content.AddComponent<RectTransform>();
        languageContent.anchorMin = new Vector2(0f, 1f);
        languageContent.anchorMax = new Vector2(1f, 1f);
        languageContent.pivot = new Vector2(0.5f, 1f);
        languageContent.anchoredPosition = Vector2.zero;
        languageContent.offsetMin = Vector2.zero;
        languageContent.offsetMax = Vector2.zero;
        var contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.childAlignment = TextAnchor.UpperLeft;
        contentLayout.spacing = 6f;
        contentLayout.padding = new RectOffset(0, 22, 0, 0);
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        var contentFitter = content.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewportRT;
        scroll.content = languageContent;

        // Scrollbar (simple style; uses template outline sprite when available)
        var sbGO = new GameObject("Scrollbar");
        sbGO.transform.SetParent(scrollGO.transform, false);
        if (layer >= 0)
            SetLayerRecursively(sbGO.transform, layer);
        var sbRT = sbGO.AddComponent<RectTransform>();
        sbRT.anchorMin = new Vector2(1f, 0f);
        sbRT.anchorMax = new Vector2(1f, 1f);
        sbRT.pivot = new Vector2(1f, 1f);
        sbRT.sizeDelta = new Vector2(18f, 0f);
        sbRT.anchoredPosition = new Vector2(-2f, 0f);

        var sbBg = sbGO.AddComponent<Image>();
        sbBg.color = new Color(1f, 1f, 1f, 0.12f);
        sbBg.raycastTarget = true;

        var scrollbar = sbGO.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        var handleGO = new GameObject("Handle");
        handleGO.transform.SetParent(sbGO.transform, false);
        if (layer >= 0)
            SetLayerRecursively(handleGO.transform, layer);
        var handleRT = handleGO.AddComponent<RectTransform>();
        handleRT.anchorMin = new Vector2(0f, 0f);
        handleRT.anchorMax = new Vector2(1f, 1f);
        handleRT.offsetMin = new Vector2(2f, 2f);
        handleRT.offsetMax = new Vector2(-2f, -2f);
        var handleImg = handleGO.AddComponent<Image>();
        handleImg.color = Color.white;
        handleImg.raycastTarget = true;

        scrollbar.handleRect = handleRT;
        scrollbar.targetGraphic = handleImg;

        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scroll.verticalScrollbarSpacing = -2f;

        // Back button container
        var backGO = InstantiateTogglePrefab() ?? new GameObject("BackButton");
        backGO.name = "BackButton";
        backGO.transform.SetParent(languagePage.transform, false);
        if (layer >= 0)
            SetLayerRecursively(backGO.transform, layer);
        var backRT = backGO.GetComponent<RectTransform>() ?? backGO.AddComponent<RectTransform>();
        backRT.anchorMin = new Vector2(0.5f, 0f);
        backRT.anchorMax = new Vector2(0.5f, 0f);
        backRT.pivot = new Vector2(0.5f, 0f);
        backRT.anchoredPosition = new Vector2(0f, 20f);
        backRT.sizeDelta = templateSizeDelta;
        // Attach button to the visual right-side container so raycast works correctly.
        var backButtonContainer = RecursiveFindByName(backGO.transform, "UI ToggleButton");
        Transform backBtnRoot = backButtonContainer != null ? (Transform)backButtonContainer : backGO.transform;
        RemoveFleeceSetter(backBtnRoot as Component);
        var backBtnRt = backBtnRoot as RectTransform ?? backBtnRoot.GetComponent<RectTransform>();
        if (backBtnRt != null)
        {
            backBtnRt.anchorMin = new Vector2(0.5f, 0.5f);
            backBtnRt.anchorMax = new Vector2(0.5f, 0.5f);
            backBtnRt.pivot = new Vector2(0.5f, 0.5f);
            backBtnRt.anchoredPosition = Vector2.zero;
        }
        foreach (var sel in backBtnRoot.GetComponents<Selectable>())
            DestroyImmediate(sel);
        var uiToggleButton = backBtnRoot.GetComponent("UIToggleButton") as Component;
        if (uiToggleButton != null)
            DestroyImmediate(uiToggleButton);
        var backBtn = backBtnRoot.GetComponent<Button>() ?? backBtnRoot.gameObject.AddComponent<Button>();
        backBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        backBtn.onClick.RemoveAllListeners();
        backBtn.onClick.AddListener(HideLanguagePage);
        TMP_Text backTxt = null;
        var existingGraphic = backGO.GetComponent<Graphic>();
        if (existingGraphic != null && existingGraphic is TMP_Text tmpExisting)
            backTxt = tmpExisting;
        else
        {
            // Ensure we do not add TMP on same GO with Image; create child for text if needed
            Transform textChild = FindTextByName(backGO.transform, "Text (TMP)")?.transform;
            if (textChild == null)
            {
                var txtGO = new GameObject("Text (TMP)");
                txtGO.transform.SetParent(backGO.transform, false);
                backTxt = txtGO.AddComponent<TextMeshProUGUI>();
            }
            else
            {
                backTxt = textChild.GetComponent<TMP_Text>();
                if (backTxt == null)
                    backTxt = textChild.gameObject.AddComponent<TextMeshProUGUI>();
            }
        }
        var backLeft = FindTextByName(backGO.transform, "Text (TMP)");
        if (backLeft != null && backLeft != backTxt)
            backLeft.gameObject.SetActive(false);
        if (backTxt == backLeft)
            backTxt.gameObject.SetActive(false);
        backTxt = FindAlternateText(backGO.transform, backLeft) ?? backTxt;
        if (backTxt == null)
        {
            var txtGO = new GameObject("BackLabel");
            txtGO.transform.SetParent(backBtnRoot, false);
            backTxt = txtGO.AddComponent<TextMeshProUGUI>();
        }
        if (backTxt != null)
            backTxt.gameObject.SetActive(true);
        backButtonText = backTxt;
        RemoveFleeceSetter(backTxt);
        backTxt.text = "BACK";
        backTxt.fontSize = templateFontSize;
        backTxt.enableAutoSizing = false;
        backTxt.alignment = TextAlignmentOptions.Center;
        ApplyTemplateFont(backTxt);
        TranslateTextAndSaveIfMissing(backTxt, "BACK", LanguageManager.CurrentLanguage?.settings, "[TranslatorSettingsMenu]");
        var backTxtRt = backTxt.GetComponent<RectTransform>();
        if (backTxtRt != null)
        {
            backTxtRt.anchorMin = new Vector2(0.5f, 0.5f);
            backTxtRt.anchorMax = new Vector2(0.5f, 0.5f);
            backTxtRt.pivot = new Vector2(0.5f, 0.5f);
            backTxtRt.anchoredPosition = Vector2.zero;
            backTxtRt.sizeDelta = new Vector2(Mathf.Max(100f, backTxt.preferredWidth + 12f), Mathf.Max(16f, backTxt.preferredHeight + 4f));
        }
        var backOutline = FindOutlineImage(backBtnRoot);
        if (backOutline != null)
        {
            var oRT = backOutline.rectTransform;
            oRT.anchorMin = new Vector2(0.5f, 0.5f);
            oRT.anchorMax = new Vector2(0.5f, 0.5f);
            oRT.pivot = new Vector2(0.5f, 0.5f);
            oRT.anchoredPosition = Vector2.zero;
            oRT.sizeDelta = new Vector2(backTxt.preferredWidth + 16f, templateOutlineHeight > 0f ? templateOutlineHeight : backRT.sizeDelta.y);
            backOutline.color = Color.white;
            backOutline.gameObject.SetActive(true);
            backBtn.targetGraphic = backOutline;
        }
        var backBacking = RecursiveFindByName(backBtnRoot, "Backing");
        if (backBacking != null)
            backBacking.gameObject.SetActive(false);
        var backCheck = FindCheckObject(backGO.transform);
        if (backCheck != null)
            backCheck.gameObject.SetActive(false);
        backButton = backBtn;

        RebuildLanguageList();
    }

    private void RebuildLanguageList()
    {
        if (languageContent == null)
            return;

        // Destroy from tail to head; safe for in-place hierarchy mutation.
        for (int i = languageContent.childCount - 1; i >= 0; i--)
        {
            var child = languageContent.GetChild(i);
            if (child != null)
                DestroyImmediate(child.gameObject);
        }
        languageRows.Clear();

        var originalSummary = new LanguageManager.LanguageSummary
        {
            Code = OriginalLanguageCode,
            DisplayName = OriginalLanguageDisplayName,
            Metadata = new JsonFormat.Metadata { langName = OriginalLanguageDisplayName, langDisplayName = OriginalLanguageDisplayName }
        };
        var originalRow = CreateLanguageRow(languageContent, originalSummary, false);
        if (originalRow != null && originalRow.Root != null)
            languageRows[OriginalLanguageCode] = originalRow;

        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { OriginalLanguageCode };
        var langs = LanguageManager.GetAvailableLanguages() ?? Enumerable.Empty<LanguageManager.LanguageSummary>();
        foreach (var summary in langs)
        {
            if (summary?.Paths == null || !summary.Paths.HasJson)
                continue;

            if (IsOriginalLikeSummary(summary))
                continue;

            var normalizedCode = summary.Code?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedCode))
                continue;
            if (string.Equals(normalizedCode, OriginalLanguageCode, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!seenCodes.Add(normalizedCode))
                continue;

            summary.Code = normalizedCode;

            var row = CreateLanguageRow(languageContent, summary, true);
            if (row == null || row.Root == null)
                continue;

            row.Code = normalizedCode;
            languageRows[normalizedCode] = row;
        }

        Canvas.ForceUpdateCanvases();

        UpdateLanguageRowStates(NormalizeSelectedCode(Plugin.SelectedLanguageEntry?.Value));
    }

    private static string NormalizeSelectedCode(string code)
    {
        var normalized = code?.Trim();
        return string.IsNullOrEmpty(normalized) ? OriginalLanguageCode : normalized;
    }

    private static bool IsOriginalLikeSummary(LanguageManager.LanguageSummary summary)
    {
        if (summary == null)
            return false;

        var code = summary.Code?.Trim();
        if (string.Equals(code, OriginalLanguageCode, StringComparison.OrdinalIgnoreCase))
            return true;

        var name = summary.Metadata?.langName?.Trim();
        if (string.IsNullOrEmpty(name))
            name = summary.DisplayName?.Trim();

        return string.Equals(name, OriginalLanguageDisplayName, StringComparison.OrdinalIgnoreCase);
    }

    private LanguageRowRefs CreateLanguageRow(Transform parent, LanguageManager.LanguageSummary summary, bool showIndicators)
    {
        if (parent == null || summary == null)
            return null;

        try
        {
            var rowGO = InstantiateTogglePrefab() ?? new GameObject($"Lang_{summary.Code}");
            rowGO.name = $"Lang_{summary.Code}";
            rowGO.transform.SetParent(parent, false);
            rowGO.SetActive(true);

            var layer = ResolveUILayer();
            if (layer >= 0)
                SetLayerRecursively(rowGO.transform, layer);

            DisableOptionComponents(rowGO);
            RemoveComponentByName(rowGO, "UIToggleButton");

            var rowRT = rowGO.GetComponent<RectTransform>() ?? rowGO.AddComponent<RectTransform>();
            foreach (var layoutGroup in rowGO.GetComponents<LayoutGroup>())
                DestroyImmediate(layoutGroup);
            foreach (var fitter in rowGO.GetComponents<ContentSizeFitter>())
                DestroyImmediate(fitter);
            foreach (var sel in rowGO.GetComponents<Selectable>())
                DestroyImmediate(sel);

            var rowImage = rowGO.GetComponent<Image>();
            if (rowImage != null)
                DestroyImmediate(rowImage);

            var highlight = RecursiveFindByName(rowGO.transform, "HighlightBorder");
            if (highlight != null)
                highlight.gameObject.SetActive(false);

            var rootBackground = rowGO.transform.Find("Background");
            if (rootBackground != null)
                DestroyImmediate(rootBackground.gameObject);

            float rowHeight = templateSizeDelta.y > 0f ? templateSizeDelta.y : 40f;
            rowRT.anchorMin = new Vector2(0f, 1f);
            rowRT.anchorMax = new Vector2(1f, 1f);
            rowRT.pivot = new Vector2(0.5f, 1f);
            rowRT.anchoredPosition = Vector2.zero;
            rowRT.sizeDelta = new Vector2(0f, rowHeight);
            rowRT.localScale = Vector3.one;

            var leRow = rowGO.GetComponent<LayoutElement>() ?? rowGO.AddComponent<LayoutElement>();
            leRow.minHeight = rowHeight;
            leRow.preferredHeight = rowHeight;
            leRow.flexibleHeight = 0f;
            leRow.ignoreLayout = false;

            // Left text (language name from metadata.langName, then display name, then code).
            var nameText = FindTextByName(rowGO.transform, "Text (TMP)");
            if (nameText == null)
            {
                var txtGO = new GameObject("Text (TMP)");
                txtGO.transform.SetParent(rowGO.transform, false);
                nameText = txtGO.AddComponent<TextMeshProUGUI>();
            }

            RemoveFleeceSetter(nameText);

            // Use langDisplayName from metadata, fallback to DisplayName, then Code
            string langName = summary.Metadata?.langDisplayName;
            if (string.IsNullOrWhiteSpace(langName))
                langName = summary.DisplayName;
            if (string.IsNullOrWhiteSpace(langName))
                langName = summary.Code;

            nameText.text = langName;
            nameText.fontSize = templateFontSize;
            nameText.enableAutoSizing = false;
            nameText.alignment = TextAlignmentOptions.Left;
            nameText.enableWordWrapping = false;
            nameText.overflowMode = TextOverflowModes.Truncate;
            nameText.maxVisibleLines = 1;
            nameText.raycastTarget = false;
            nameText.color = Color.white;
            ApplyTemplateFont(nameText);

            // Prepare template style refs before rewriting right side.
            var templateOutline = ResolveOutlineReference(FindOutlineImage(rowGO.transform));
            var templateCheckObj = FindCheckObject(rowGO.transform);
            var templateCheckImg = templateCheckObj != null ? templateCheckObj.GetComponent<Image>() : null;
            var templateIndicatorOutline = templateOutline;
            var templateIndicatorFill = templateCheckImg;
            if (templateCheckObj != null && templateCheckObj.parent != null)
            {
                var indicatorOutlineRt = RecursiveFindByName(templateCheckObj.parent, "Outline");
                if (indicatorOutlineRt != null)
                {
                    var indicatorOutlineImg = indicatorOutlineRt.GetComponent<Image>();
                    if (indicatorOutlineImg != null)
                        templateIndicatorOutline = indicatorOutlineImg;
                }

                var indicatorBackingRt = RecursiveFindByName(templateCheckObj.parent, "Backing") ??
                                         RecursiveFindByName(templateCheckObj.parent, "Background");
                if (indicatorBackingRt != null)
                {
                    var indicatorBackingImg = indicatorBackingRt.GetComponent<Image>();
                    if (indicatorBackingImg != null)
                        templateIndicatorFill = indicatorBackingImg;
                }
            }
            templateIndicatorOutline = ResolveOutlineReference(templateIndicatorOutline);
            templateIndicatorFill = ResolveFillReference(templateIndicatorFill);
            var templateButtonFill = ResolveFillReference(null);

            // Right button container.
            var buttonContainerRt = RecursiveFindByName(rowGO.transform, "UI ToggleButton");
            if (buttonContainerRt == null)
            {
                var containerGO = new GameObject("UI ToggleButton");
                containerGO.transform.SetParent(rowGO.transform, false);
                if (layer >= 0)
                    SetLayerRecursively(containerGO.transform, layer);
                buttonContainerRt = containerGO.AddComponent<RectTransform>();
            }

            if (buttonContainerRt == null)
            {
                Logging.Error("[TranslatorSettingsMenu] Failed to create button container for language row.");
                return null;
            }

            var buttonContainer = buttonContainerRt.transform;
            RemoveComponentByName(buttonContainer.gameObject, "UIToggleButton");
            foreach (var sel in buttonContainer.GetComponents<Selectable>())
                DestroyImmediate(sel);
            foreach (var sel in buttonContainer.GetComponentsInChildren<Selectable>(true))
            {
                if (sel != null && sel.gameObject != buttonContainer.gameObject)
                    DestroyImmediate(sel);
            }

            float buttonWidth = Mathf.Max(templateRightWidth + 16f, 104f);
            float buttonHeight = templateOutlineHeight > 0f ? templateOutlineHeight : Mathf.Max(20f, rowHeight - 8f);

            buttonContainerRt.anchorMin = new Vector2(1f, 0.5f);
            buttonContainerRt.anchorMax = new Vector2(1f, 0.5f);
            buttonContainerRt.pivot = new Vector2(1f, 0.5f);
            buttonContainerRt.anchoredPosition = new Vector2(-6f, 0f);
            buttonContainerRt.sizeDelta = new Vector2(buttonWidth, buttonHeight);
            buttonContainerRt.localScale = Vector3.one;

            var fillMaskRt = RecursiveFindByName(buttonContainer, "FillMask");
            if (fillMaskRt != null)
            {
                // Old mask-based version can hide labels; unwrap and remove it.
                var moveChildren = new List<Transform>();
                foreach (Transform child in fillMaskRt)
                    moveChildren.Add(child);
                foreach (var child in moveChildren)
                    child.SetParent(buttonContainer, false);
                DestroyImmediate(fillMaskRt.gameObject);
            }

            var backingRt = RecursiveFindByName(buttonContainer, "Backing") ?? RecursiveFindByName(buttonContainer, "Background");
            if (backingRt == null)
            {
                var backingGO = new GameObject("Backing");
                backingGO.transform.SetParent(buttonContainer, false);
                if (layer >= 0)
                    SetLayerRecursively(backingGO.transform, layer);
                backingRt = backingGO.AddComponent<RectTransform>();
            }
            else if (backingRt.parent != buttonContainer)
            {
                backingRt.SetParent(buttonContainer, false);
            }

            var backingImg = EnsureComponent<Image>(backingRt.gameObject);
            backingRt.anchorMin = new Vector2(0.5f, 0.5f);
            backingRt.anchorMax = new Vector2(0.5f, 0.5f);
            backingRt.pivot = new Vector2(0.5f, 0.5f);
            backingRt.anchoredPosition = Vector2.zero;
            backingRt.sizeDelta = new Vector2(Mathf.Max(8f, buttonWidth - 6f), Mathf.Max(8f, buttonHeight - 6f));
            CopyImageStyle(templateButtonFill != null ? templateButtonFill : templateOutline, backingImg);
            backingImg.color = Color.black;
            backingImg.raycastTarget = false;
            backingRt.SetAsFirstSibling();

            var outlineRt = RecursiveFindByName(buttonContainer, "Outline");
            if (outlineRt == null)
            {
                var outlineGO = new GameObject("Outline");
                outlineGO.transform.SetParent(buttonContainer, false);
                if (layer >= 0)
                    SetLayerRecursively(outlineGO.transform, layer);
                outlineRt = outlineGO.AddComponent<RectTransform>();
            }
            else if (outlineRt.parent != buttonContainer)
            {
                outlineRt.SetParent(buttonContainer, false);
            }

            var outlineImg = EnsureComponent<Image>(outlineRt.gameObject);
            outlineRt.anchorMin = new Vector2(0.5f, 0.5f);
            outlineRt.anchorMax = new Vector2(0.5f, 0.5f);
            outlineRt.pivot = new Vector2(0.5f, 0.5f);
            outlineRt.anchoredPosition = Vector2.zero;
            outlineRt.sizeDelta = new Vector2(buttonWidth, buttonHeight);
            CopyImageStyle(templateOutline, outlineImg);
            outlineImg.color = Color.white;
            outlineImg.raycastTarget = true;
            outlineImg.gameObject.SetActive(true);
            outlineRt.SetAsLastSibling();

            var checkObj = FindCheckObject(buttonContainer);
            if (checkObj != null)
                checkObj.gameObject.SetActive(false);

            var rightText = FindTextByName(buttonContainer, "RightLabel");
            if (rightText == null)
            {
                var txtGO = new GameObject("RightLabel");
                txtGO.transform.SetParent(buttonContainer, false);
                rightText = txtGO.AddComponent<TextMeshProUGUI>();
            }
            else if (rightText.transform.parent != buttonContainer)
            {
                rightText.transform.SetParent(buttonContainer, false);
            }

            RemoveFleeceSetter(rightText);
            rightText.gameObject.SetActive(true);
            rightText.text = "SELECT";
            rightText.fontSize = templateFontSize;
            rightText.enableAutoSizing = false;
            rightText.alignment = TextAlignmentOptions.Center;
            rightText.enableWordWrapping = false;
            rightText.overflowMode = TextOverflowModes.Truncate;
            rightText.maxVisibleLines = 1;
            rightText.raycastTarget = false;
            rightText.color = Color.white;
            ApplyTemplateFont(rightText);

            var rightRT = rightText.rectTransform;
            rightRT.anchorMin = new Vector2(0.5f, 0.5f);
            rightRT.anchorMax = new Vector2(0.5f, 0.5f);
            rightRT.pivot = new Vector2(0.5f, 0.5f);
            rightRT.anchoredPosition = Vector2.zero;
            rightRT.sizeDelta = new Vector2(Mathf.Max(8f, buttonWidth - 8f), Mathf.Max(8f, buttonHeight - 4f));
            rightRT.localScale = Vector3.one;
            rightRT.SetAsLastSibling();

            var btn = buttonContainer.GetComponent<Button>() ?? buttonContainer.gameObject.AddComponent<Button>();
            if (btn == null)
            {
                Logging.Error("[TranslatorSettingsMenu] Failed to create Select button.");
                return null;
            }

            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.RemoveAllListeners();
            btn.targetGraphic = outlineImg;

            Image audioBox = null;
            Image texBox = null;
            Image fontBox = null;

            // Indicators between name and Select button.
            var oldIndicators = RecursiveFindByName(rowGO.transform, "Indicators");
            if (oldIndicators != null)
                DestroyImmediate(oldIndicators.gameObject);

            float indicatorAreaWidth = 0f;
            const float rowRightPadding = 6f;
            const float indicatorGapToButton = 4f;
            const float textGapToIndicators = 8f;
            const float textGapToButton = 12f;
            if (showIndicators)
            {
                var indRoot = new GameObject("Indicators");
                indRoot.transform.SetParent(rowGO.transform, false);
                if (layer >= 0)
                    SetLayerRecursively(indRoot.transform, layer);

                indicatorAreaWidth = 96f;
                var indRT = indRoot.AddComponent<RectTransform>();
                indRT.anchorMin = new Vector2(1f, 0.5f);
                indRT.anchorMax = new Vector2(1f, 0.5f);
                indRT.pivot = new Vector2(1f, 0.5f);
                indRT.anchoredPosition = new Vector2(-(rowRightPadding + buttonWidth + indicatorGapToButton), 0f);
                indRT.sizeDelta = new Vector2(indicatorAreaWidth, buttonHeight);

                var indHLG = indRoot.AddComponent<HorizontalLayoutGroup>();
                indHLG.spacing = 4f;
                indHLG.childAlignment = TextAnchor.MiddleRight;
                indHLG.childForceExpandHeight = false;
                indHLG.childForceExpandWidth = false;

                audioBox = CreateIndicator(indRoot.transform, "A", summary.Paths?.HasAudio == true, templateIndicatorOutline, templateIndicatorFill);
                texBox = CreateIndicator(indRoot.transform, "T", summary.Paths?.HasTextures == true, templateIndicatorOutline, templateIndicatorFill);
                fontBox = CreateIndicator(indRoot.transform, "F", !string.IsNullOrEmpty(summary.FontFile), templateIndicatorOutline, templateIndicatorFill);
            }

            var nameRT = nameText.rectTransform;
            nameRT.anchorMin = new Vector2(0f, 0.5f);
            nameRT.anchorMax = new Vector2(1f, 0.5f);
            nameRT.pivot = new Vector2(0f, 0.5f);
            nameRT.anchoredPosition = Vector2.zero;
            nameRT.localScale = Vector3.one;
            nameRT.offsetMin = new Vector2(0f, -rowHeight * 0.5f);
            float nameRightPadding = showIndicators
                ? (rowRightPadding + buttonWidth + indicatorGapToButton + indicatorAreaWidth + textGapToIndicators)
                : (rowRightPadding + buttonWidth + textGapToButton);
            nameRT.offsetMax = new Vector2(-nameRightPadding, rowHeight * 0.5f);

            var row = new LanguageRowRefs
            {
                Root = rowGO,
                Name = nameText,
                AudioBox = audioBox,
                TextureBox = texBox,
                FontBox = fontBox,
                SelectButton = btn,
                SelectText = rightText,
                SelectOutline = outlineImg,
                SelectBacking = backingImg,
                Code = summary.Code
            };

            btn.onClick.AddListener(() => OnSelectLanguage(row));
            return row;
        }
        catch (Exception e)
        {
            Logging.Error($"[TranslatorSettingsMenu] CreateLanguageRow failed for '{summary?.Code}': {e}");
            return null;
        }
    }

    private Image CreateIndicator(Transform parent, string label, bool enabled, Image templateOutline, Image templateFill)
    {
        if (parent == null)
            return null;

        templateOutline = ResolveOutlineReference(templateOutline);
        templateFill = ResolveFillReference(templateFill);

        var boxGO = new GameObject("Indicator");
        boxGO.transform.SetParent(parent, false);

        var layer = ResolveUILayer();
        if (layer >= 0)
            SetLayerRecursively(boxGO.transform, layer);

        var rt = boxGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);

        float boxSize = templateOutlineHeight > 0f ? templateOutlineHeight : 20f;
        if (templateOutline != null)
        {
            float refSize = templateOutline.rectTransform.sizeDelta.y;
            if (refSize <= 0f)
                refSize = templateOutline.rectTransform.rect.height;
            if (refSize > 0f)
                boxSize = Mathf.Max(14f, refSize);
        }
        boxSize = Mathf.Max(14f, boxSize - 8f);
        rt.sizeDelta = new Vector2(boxSize, boxSize);
        rt.localScale = Vector3.one;

        var le = boxGO.AddComponent<LayoutElement>();
        le.minWidth = boxSize;
        le.preferredWidth = boxSize;
        le.minHeight = boxSize;
        le.preferredHeight = boxSize;
        le.flexibleWidth = 0f;
        le.flexibleHeight = 0f;

        var stateColor = enabled ? new Color(0.1f, 0.78f, 0.28f, 1f) : new Color(0.85f, 0.2f, 0.2f, 1f);

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(boxGO.transform, false);
        if (layer >= 0)
            SetLayerRecursively(fillGO.transform, layer);
        var fillRT = fillGO.AddComponent<RectTransform>();
        fillRT.anchorMin = new Vector2(0.5f, 0.5f);
        fillRT.anchorMax = new Vector2(0.5f, 0.5f);
        fillRT.pivot = new Vector2(0.5f, 0.5f);
        fillRT.anchoredPosition = Vector2.zero;
        fillRT.sizeDelta = new Vector2(Mathf.Max(8f, boxSize - 6f), Mathf.Max(8f, boxSize - 6f));
        var fill = fillGO.AddComponent<Image>();
        CopyImageStyle(templateFill != null ? templateFill : templateOutline, fill);
        fill.color = stateColor;
        fill.raycastTarget = false;

        var outline = boxGO.AddComponent<Image>();
        outline.color = stateColor;
        outline.raycastTarget = false;
        CopyImageStyle(templateOutline, outline);

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(boxGO.transform, false);
        if (layer >= 0)
            SetLayerRecursively(labelGO.transform, layer);
        var labelText = labelGO.AddComponent<TextMeshProUGUI>();
        RemoveFleeceSetter(labelText);
        labelText.text = string.IsNullOrEmpty(label) ? "?" : label;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.fontSize = Mathf.Max(9f, boxSize * 0.5f);
        labelText.enableAutoSizing = false;
        labelText.enableWordWrapping = false;
        labelText.overflowMode = TextOverflowModes.Truncate;
        labelText.color = Color.black;
        labelText.raycastTarget = false;
        ApplyTemplateFont(labelText);
        var labelRT = labelText.rectTransform;
        labelRT.anchorMin = new Vector2(0.5f, 0.5f);
        labelRT.anchorMax = new Vector2(0.5f, 0.5f);
        labelRT.pivot = new Vector2(0.5f, 0.5f);
        labelRT.anchoredPosition = Vector2.zero;
        labelRT.sizeDelta = new Vector2(Mathf.Max(8f, boxSize - 4f), Mathf.Max(8f, boxSize - 4f));
        labelRT.localScale = Vector3.one;

        return outline;
    }

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
                plugin.RefreshLocalizationInCurrentScene();
            }
            else
            {
                // Fallback path: apply texture/font/text refresh even without plugin instance.
                Plugin.RefreshTexturesInCurrentScene();
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
            pluginApply.ApplyFontImmediateWithFallback();
        }
        else
        {
            // Fallback: still try to apply language font even if plugin instance was not recovered yet.
            Plugin.TryApplyLanguageFont();
            Plugin.RefreshTexturesInCurrentScene();
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
            pluginRefresh.RefreshLocalizationInCurrentScene();
        }
        else
        {
            // Fallback path: apply texture/font/text refresh even without plugin instance.
            Plugin.RefreshTexturesInCurrentScene();
            CommonFunctions.RefreshAllSceneTexts(skipTranslatorSettingsMenu: false);
            Canvas.ForceUpdateCanvases();
            Logging.Warn("[TranslatorSettingsMenu] Plugin instance not found; used static fallback refresh.");
        }
    }

    private void UpdateLanguageRowStates(string selectedCode)
    {
        foreach (var kv in languageRows)
        {
            var r = kv.Value;
            if (r == null)
                continue;
            bool selected = kv.Key == selectedCode;
            if (r.SelectText != null)
            {
                string selectKey = selected ? "SELECTED" : "SELECT";
                r.SelectText.text = selectKey;
                // Apply translation if available
                if (LanguageManager.IsLoaded && LanguageManager.CurrentLanguage?.settings != null)
                {
                    var settings = LanguageManager.CurrentLanguage.settings;
                    if (settings.TryGetValue(selectKey, out var translated) && !string.IsNullOrEmpty(translated))
                    {
                        r.SelectText.text = translated;
                    }
                    else if (!settings.ContainsKey(selectKey))
                    {
                        settings[selectKey] = selectKey;
                        LanguageManager.SaveCurrentLanguage();
                    }
                }
                r.SelectText.color = selected ? Color.black : Color.white;
            }
            if (r.SelectOutline != null)
            {
                r.SelectOutline.color = selected ? Color.black : Color.white;
                r.SelectOutline.gameObject.SetActive(true);
            }
            if (r.SelectBacking != null)
            {
                r.SelectBacking.color = selected ? Color.white : Color.black;
            }
            else
            {
                var btnImage = r.SelectButton?.GetComponent<Image>();
                if (btnImage != null)
                    btnImage.color = selected ? Color.white : Color.black;
            }
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

    private GameObject InstantiateButtonPrefab()
    {
        if (TemplateButtonPrefab == null)
            return null;
        var go = Instantiate(TemplateButtonPrefab);
        go.name = "ButtonRow";
        return go;
    }

    private GameObject InstantiateTogglePrefab()
    {
        if (TemplateTogglePrefab == null)
        {
            // Build a minimal fallback toggle hierarchy.
            var go = new GameObject("Toggle");
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(20, 20);

            var toggle = go.AddComponent<Toggle>();
            var image = go.AddComponent<Image>();
            image.color = Color.white;

            var background = new GameObject("Background");
            background.transform.SetParent(go.transform, false);
            var bgImage = background.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f);

            var check = new GameObject("Check");
            check.transform.SetParent(background.transform, false);
            var cmImage = check.AddComponent<Image>();
            cmImage.color = Color.white;

            toggle.targetGraphic = image;
            toggle.graphic = cmImage;

            return go;
        }

        var instance = Instantiate(TemplateTogglePrefab);
        instance.name = "ToggleRow";
        if (!instance.activeSelf)
            instance.SetActive(true);
        return instance;
    }

    private void DisableOptionComponents(GameObject go)
    {
        if (go == null) return;
        foreach (var comp in go.GetComponentsInChildren<UISettingsOptionBase>(true))
            DestroyImmediate(comp);
        foreach (var comp in go.GetComponentsInChildren<UISettingsOptionToggle>(true))
            DestroyImmediate(comp);
        foreach (var comp in go.GetComponentsInChildren<UISettingsOptionList>(true))
            DestroyImmediate(comp);
    }

    private GameObject PrepareTemplateRow(Transform listingAnchor)
    {
        if (listingAnchor == null)
            return null;

        var backing = listingAnchor.Find("Backing");
        var vsync = listingAnchor.Cast<Transform>()
            .FirstOrDefault(t => t != null && t.name.IndexOf("toggle v sync", StringComparison.OrdinalIgnoreCase) >= 0);

        if (vsync == null)
        {
            vsync = listingAnchor.Cast<Transform>()
                .FirstOrDefault(t => t != null && t.GetComponent<UISettingsOptionToggle>() != null);
            if (vsync == null)
                Logging.Error("[TranslatorSettingsMenu] Failed to find Toggle V Sync template; list will be empty.");
        }

        if (vsync != null)
        {
            var rt = vsync.GetComponent<RectTransform>();
            if (rt != null)
            {
                templateAnchorMin = rt.anchorMin;
                templateAnchorMax = rt.anchorMax;
                templatePivot = rt.pivot;
                templateSizeDelta = rt.sizeDelta;
                templateAnchoredPos = rt.anchoredPosition;
            }
            if (templateSizeDelta == Vector2.zero)
                templateSizeDelta = new Vector2(400f, 40f);

            var leftTmp = FindTextByName(vsync, "Text (TMP)");
            if (leftTmp != null)
                templateFontSize = leftTmp.fontSize;

            var rightTmp = FindAlternateText(vsync, leftTmp);
            if (rightTmp != null)
            {
                var rtR = rightTmp.GetComponent<RectTransform>();
                if (rtR != null && rtR.sizeDelta.x > 0f)
                    templateRightWidth = rtR.sizeDelta.x;
            }
            if (templateRightWidth <= 0f)
                templateRightWidth = 140f;

            var outlineRef = FindOutlineImage(vsync);
            if (outlineRef != null)
            {
                var ort = outlineRef.rectTransform;
                templateOutlineHeight = ort.sizeDelta.y;
                if (templateOutlineHeight <= 0f)
                    templateOutlineHeight = ort.rect.height;
            }
            if (templateOutlineHeight <= 0f)
                templateOutlineHeight = 32f;

            // Keep a persistent prefab clone for later instantiation (language page, back button, etc.).
            if (TemplateTogglePrefab == null)
                TemplateTogglePrefab = CreateTemplateClone(vsync.gameObject, "TemplateToggleRow");
            if (TemplateTogglePrefab != null)
                DisableOptionComponents(TemplateTogglePrefab);
        }

        var toRemove = listingAnchor.Cast<Transform>()
            .Where(t => t != null &&
                        (backing == null || t != backing) &&
                        (templatesRoot == null || t != templatesRoot))
            .ToList();
        foreach (var child in toRemove)
            DestroyImmediate(child.gameObject);

        if (backing != null)
            backing.SetAsFirstSibling();

        return TemplateTogglePrefab;
    }

    private List<GameObject> BuildRows(GameObject preservedRow, int totalRows)
    {
        var rows = new List<GameObject>();
        var backing = listAnchor != null ? listAnchor.Find("Backing") : null;

        if (TemplateTogglePrefab == null)
        {
            Logging.Error("[TranslatorSettingsMenu] TemplateTogglePrefab is null; cannot build rows.");
            return rows;
        }

        int startIndex = backing != null ? 1 : 0;
        for (int i = 0; i < totalRows; i++)
        {
            var clone = Instantiate(TemplateTogglePrefab);
            clone.name = $"TranslatorRow_{i + 1}";
            clone.transform.SetParent(listAnchor, false);
            clone.SetActive(true);
            var rt = clone.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = templateAnchorMin;
                rt.anchorMax = templateAnchorMax;
                rt.pivot = templatePivot;
                rt.sizeDelta = templateSizeDelta;
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = Vector3.one;
            }
            var le = clone.GetComponent<LayoutElement>() ?? clone.AddComponent<LayoutElement>();
            le.minHeight = templateSizeDelta.y;
            le.preferredHeight = templateSizeDelta.y;
            le.flexibleHeight = 0f;
            le.preferredWidth = -1f;
            clone.transform.SetSiblingIndex(startIndex + i);
            rows.Add(clone);
        }

        if (backing != null)
            backing.SetAsFirstSibling();

        return rows;
    }
}
