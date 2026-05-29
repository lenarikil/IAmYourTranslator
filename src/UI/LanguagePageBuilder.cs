using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IAmYourTranslator;
using IAmYourTranslator.json;
using IAmYourTranslator.Core;
using Fleece;
using static IAmYourTranslator.CommonFunctions;

// Language page construction, row creation, indicator boxes, and row state management.
public partial class TranslatorSettingsMenu
{
    private Dictionary<string, LanguageRowRefs> languageRows = new Dictionary<string, LanguageRowRefs>();
    private GameObject languagePage;
    private RectTransform languageContent;
    private Button backButton;
    private TMP_Text backButtonText;

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
}
