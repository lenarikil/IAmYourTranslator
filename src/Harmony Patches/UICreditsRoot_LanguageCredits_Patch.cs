using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using IAmYourTranslator.json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.HarmonyPatches
{
    [HarmonyPatch(typeof(UICreditsRoot))]
    public static class UICreditsRoot_LanguageCredits_Patch
    {
        private const string DefaultHeaderText = "Translator Credits";

        private static readonly FieldInfo ListField =
            AccessTools.Field(typeof(UICreditsRoot), "list");

        private static readonly FieldInfo CreditsItemsField =
            AccessTools.Field(typeof(UICreditsRoot), "creditsItems");

        private static readonly FieldInfo CurrentItemField =
            AccessTools.Field(typeof(UICreditsRoot), "currentItem");

        private static readonly FieldInfo FadingOutField =
            AccessTools.Field(typeof(UICreditsRoot), "fadingOut");

        private static readonly FieldInfo ImageRefField =
            AccessTools.Field(typeof(UICreditsRoot), "imageRef");

        private static readonly FieldInfo FeatureImageField =
            AccessTools.Field(typeof(UICreditsRoot), "featureImage");

        private static readonly FieldInfo ListImageRefField =
            AccessTools.Field(typeof(UICreditsList), "imageRef");

        private static readonly FieldInfo ListHeaderTextRefField =
            AccessTools.Field(typeof(UICreditsList), "headerText");

        private static readonly MethodInfo StartFadeOutMethod =
            AccessTools.Method(typeof(UICreditsRoot), "StartFadeOut");

        private static UICreditItem LangCreditsImageItem;
        private static UICreditItemNames LangCreditsNamesItem;

        private sealed class State
        {
            public bool SpaceQueued;
        }

        private static readonly Dictionary<int, State> States = new Dictionary<int, State>();

        [HarmonyPatch(typeof(UICreditsList), "LateUpdate")]
        public static class ListLateUpdatePatch
        {
            [HarmonyPrefix]
            public static bool Prefix(UICreditsList __instance)
            {
                UICreditsRoot root = __instance.GetComponentInParent<UICreditsRoot>();
                if (root == null)
                    return true;

                bool fadingOut = (bool)(FadingOutField?.GetValue(root) ?? false);
                if (fadingOut)
                    return true;

                ForceImageAlpha(ListImageRefField?.GetValue(__instance) as RawImage);
                ForceImageAlpha(ImageRefField?.GetValue(root) as RawImage);

                RawImage featureImage = FeatureImageField?.GetValue(root) as RawImage;
                if (featureImage != null)
                    ForceImageAlpha(featureImage);

                TMP_Text headerText = ListHeaderTextRefField?.GetValue(__instance) as TMP_Text;
                if (headerText != null)
                {
                    Transform headerParent = headerText.transform.parent;
                    if (headerParent != null)
                    {
                        foreach (Image img in headerParent.GetComponentsInChildren<Image>(true))
                        {
                            ForceImageColor(img);
                        }
                    }
                }

                return true;
            }

            private static void ForceImageAlpha(RawImage img)
            {
                if (img != null && img.color.a < 1f)
                {
                    Color c = img.color;
                    c.a = 1f;
                    img.color = c;
                }
            }

            private static void ForceImageColor(Image img)
            {
                if (img != null && img.color.a < 1f)
                {
                    Color c = img.color;
                    c.a = 1f;
                    img.color = c;
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch("PlayFromStart")]
        public static bool PlayFromStartPrefix(UICreditsRoot __instance)
        {
            var markers = AccessTools.Field(typeof(UICreditsRoot), "progressMarkers")?.GetValue(__instance);
            return markers != null;
        }

        [HarmonyPrefix]
        [HarmonyPatch("Start")]
        public static void StartPrefix(UICreditsRoot __instance)
        {
            try
            {
                if (__instance == null)
                    return;

                if (!TryGetLangCredits(out string creditsText, out _))
                    return;

                UICreditItem[] items = CreditsItemsField?.GetValue(__instance) as UICreditItem[];
                if (items == null)
                    return;

                string[] lines = creditsText.Split('\n');
                Texture2D texture = LoadLanguageCreditsTexture();

                UICreditItem[] newItems;

                if (texture != null)
                {
                    LangCreditsImageItem = CreateLangCreditsImageItem(texture);
                    LangCreditsNamesItem = CreateLangCreditsNamesItem(lines);

                    newItems = new UICreditItem[items.Length + 2];
                    newItems[0] = LangCreditsImageItem;
                    newItems[1] = LangCreditsNamesItem;
                    Array.Copy(items, 0, newItems, 2, items.Length);
                    Logging.Info($"[UICreditsRoot] Injected langCredits image + names ({lines.Length} lines).");
                }
                else
                {
                    LangCreditsNamesItem = CreateLangCreditsNamesItem(lines);

                    newItems = new UICreditItem[items.Length + 1];
                    newItems[0] = LangCreditsNamesItem;
                    Array.Copy(items, 0, newItems, 1, items.Length);
                    Logging.Info($"[UICreditsRoot] Injected langCredits names ({lines.Length} lines), no image.");
                }

                CreditsItemsField?.SetValue(__instance, newItems);
            }
            catch (Exception e)
            {
                Logging.Warn($"[UICreditsRoot] StartPrefix error: {e}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("RefreshDisplay")]
        public static void RefreshDisplayPostfix(UICreditsRoot __instance)
        {
            try
            {
                if (__instance == null || LangCreditsNamesItem == null)
                    return;

                UICreditItem currentItem = CurrentItemField?.GetValue(__instance) as UICreditItem;
                if (currentItem != LangCreditsNamesItem)
                    return;

                UICreditsList list = ListField?.GetValue(__instance) as UICreditsList;
                if (list == null)
                    return;

                if (!TryGetLangCredits(out string creditsText, out string headerText))
                    return;

                string[] lines = creditsText.Split('\n');
                list.RefreshList("", lines, headerText);
            }
            catch (Exception e)
            {
                Logging.Warn($"[UICreditsRoot] RefreshDisplayPostfix error: {e}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("Update")]
        public static void UpdatePostfix(UICreditsRoot __instance)
        {
            try
            {
                if (__instance == null)
                    return;

                int id = __instance.GetInstanceID();
                if (!States.TryGetValue(id, out State state))
                {
                    state = new State();
                    States[id] = state;
                }

                if (UnityEngine.Input.GetKeyDown(KeyCode.Space))
                {
                    if (state.SpaceQueued)
                        return;
                    state.SpaceQueued = true;

                    bool fadingOut = (bool)(FadingOutField?.GetValue(__instance) ?? false);
                    if (!fadingOut)
                        StartFadeOutMethod?.Invoke(__instance, null);
                    return;
                }

                if (!UnityEngine.Input.GetKey(KeyCode.Space))
                    state.SpaceQueued = false;
            }
            catch (Exception e)
            {
                Logging.Warn($"[UICreditsRoot] UpdatePostfix error: {e}");
            }
        }

        private static UICreditItemImage CreateLangCreditsImageItem(Texture2D texture)
        {
            UICreditItemImage item = ScriptableObject.CreateInstance<UICreditItemImage>();
            typeof(UICreditItemImage)
                .GetField("texture", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(item, texture);
            return item;
        }

        private static UICreditItemNames CreateLangCreditsNamesItem(string[] names)
        {
            UICreditItemNames item = ScriptableObject.CreateInstance<UICreditItemNames>();

            UICreditItemNames.CreditNameSlot slot = new UICreditItemNames.CreditNameSlot();
            typeof(UICreditItemNames.CreditNameSlot)
                .GetField("names", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(slot, names);

            Type jumperType = typeof(UICreditItemNames).Assembly.GetType("Fleece.Jumper");
            if (jumperType != null)
            {
                object jumper = Activator.CreateInstance(jumperType);
                typeof(UICreditItemNames.CreditNameSlot)
                    .GetField("passageRoleTitle", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(slot, jumper);
                typeof(UICreditItemNames)
                    .GetField("passageSuperHeader", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(item, jumper);
            }

            typeof(UICreditItemNames)
                .GetField("slots", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(item, new[] { slot });

            typeof(UICreditItemNames)
                .GetField("nameGroup", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(item, false);

            return item;
        }

        private static Texture2D LoadLanguageCreditsTexture()
        {
            try
            {
                if (LanguageManager.CurrentSummary?.Paths == null || !Plugin.EnableTextureReplacementEntry.Value)
                    return null;

                string texturesDir = LanguageManager.CurrentSummary.Paths.TexturesDir;
                string[] candidates =
                {
                    Path.Combine(texturesDir, "langCredits.png"),
                    Path.Combine(texturesDir, "LangCredits.png"),
                    Path.Combine(texturesDir, "langCredits.jpg"),
                    Path.Combine(texturesDir, "LangCredits.jpg")
                };

                foreach (string candidate in candidates)
                {
                    if (File.Exists(candidate))
                        return UITextureReplacer.LoadTextureFromFile(candidate, false);
                }
            }
            catch (Exception e)
            {
                Logging.Warn($"[UICreditsRoot] Failed to load language credits image: {e.Message}");
            }

            return null;
        }

        private static bool TryGetLangCredits(out string creditsText, out string headerText)
        {
            creditsText = null;
            headerText = null;

            if (!LanguageManager.IsLoaded)
                return false;

            var meta = LanguageManager.CurrentMetadata;
            if (meta == null)
                return false;

            headerText = !string.IsNullOrWhiteSpace(meta.langCreditsHeader)
                ? meta.langCreditsHeader
                : DefaultHeaderText;

            if (!string.IsNullOrWhiteSpace(meta.langCredits))
            {
                creditsText = meta.langCredits;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(meta.langAuthor))
            {
                creditsText = meta.langAuthor;
                return true;
            }

            return false;
        }
    }
}
