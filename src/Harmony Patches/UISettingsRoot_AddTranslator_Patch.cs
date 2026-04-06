using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using TMPro;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.HarmonyPatches
{
    // Injects the translator settings tab and page by cloning an existing submenu template.
    [HarmonyPatch(typeof(UISettingsRoot), "Start")]
    public static class UISettingsRoot_AddTranslator_Patch
    {
        private static void Prefix(UISettingsRoot __instance)
        {
            try
            {
                if (__instance == null)
                    return;

                var traverse = Traverse.Create(__instance);
                var subMenus = traverse.Field("subMenus").GetValue<UISettingsSubMenu[]>() ?? Array.Empty<UISettingsSubMenu>();

                if (subMenus.Any(m => m is TranslatorSettingsMenu))
                    return;

                var template = subMenus.FirstOrDefault(m => m != null);
                if (template == null)
                    return;

                var cloned = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
                cloned.name = "LanguagesMenu";

                var templateListing = RecursiveFindChild(template.transform, "Listing Anchor");
                var togglePrefab = FindRowTemplate<UISettingsOptionToggle>(templateListing);
                var buttonPrefab = FindRowTemplate<UISettingsOptionList>(templateListing);
                var backingPrefab = templateListing != null ? templateListing.Cast<Transform>().FirstOrDefault(t => t.name == "Backing") : null;

                TMP_Text sampleText = template.GetComponentInChildren<TMP_Text>(true);
                TMP_FontAsset sampleFont = sampleText != null ? sampleText.font : null;
                Material sampleMat = sampleText != null ? sampleText.fontMaterial : null;

                foreach (var comp in cloned.GetComponents<UISettingsSubMenu>())
                    UnityEngine.Object.DestroyImmediate(comp);

                var translator = cloned.AddComponent<TranslatorSettingsMenu>();
                translator.InitializeSelf("Languages");
                translator.TemplateFont = sampleFont;
                translator.TemplateFontMaterial = sampleMat;
                translator.TemplateTogglePrefab = togglePrefab != null ? togglePrefab.gameObject : null;
                translator.TemplateButtonPrefab = buttonPrefab != null ? buttonPrefab.gameObject : null;
                translator.TemplateBacking = backingPrefab != null ? backingPrefab.gameObject : null;

                int insertIndex = Mathf.Clamp(1, 0, subMenus.Length);
                cloned.transform.SetSiblingIndex(Mathf.Clamp(template.transform.GetSiblingIndex() + 1, 0, template.transform.parent.childCount));

                var list = subMenus.ToList();
                list.Insert(insertIndex, translator);
                traverse.Field("subMenus").SetValue(list.ToArray());
            }
            catch (Exception e)
            {
                Logging.Error($"[UISettingsRoot_AddTranslator] Failed to inject Languages tab: {e}");
            }
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
    }
}
