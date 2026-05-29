using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IAmYourTranslator;
using IAmYourTranslator.Core;
using IAmYourTranslator.json;
using Fleece;
using static IAmYourTranslator.CommonFunctions;

// Row configuration, UI element finding, and visual setup helpers.
public partial class TranslatorSettingsMenu
{
    private int uiLayer = -1;

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

    private void ApplyTemplateFont(TMP_Text tmp)
    {
        if (tmp == null)
            return;

        if (TemplateFont != null)
            tmp.font = TemplateFont;
        if (TemplateFontMaterial != null)
            tmp.fontMaterial = new Material(TemplateFontMaterial);
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

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        if (go == null)
            return null;
        return go.GetComponent<T>() ?? go.AddComponent<T>();
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
}
