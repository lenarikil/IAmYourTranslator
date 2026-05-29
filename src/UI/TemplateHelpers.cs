using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IAmYourTranslator;
using IAmYourTranslator.Core;
using static IAmYourTranslator.CommonFunctions;

// Template discovery, cloning, and layout helpers for the settings menu.
public partial class TranslatorSettingsMenu
{
    public TMP_FontAsset TemplateFont;
    public Material TemplateFontMaterial;
    public GameObject TemplateTogglePrefab;
    public GameObject TemplateButtonPrefab;
    public GameObject TemplateBacking;

    private Vector2 templateAnchorMin;
    private Vector2 templateAnchorMax;
    private Vector2 templatePivot;
    private Vector2 templateSizeDelta;
    private Vector2 templateAnchoredPos;
    private float templateFontSize = 26f;
    private float templateRightWidth = 140f;
    private float templateOutlineHeight = 0f;
    private Transform templatesRoot;

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
}
