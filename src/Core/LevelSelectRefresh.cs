using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TMPro;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.Core
{
    internal static class LevelSelectRefresh
    {
        public static void Refresh()
        {
            try
            {
                Dictionary<string, string> levelNames = null;
                if (json.LanguageManager.IsLoaded)
                {
                    levelNames = json.LanguageManager.CurrentLanguage.levelNames;
                    if (levelNames == null)
                        levelNames = json.LanguageManager.CurrentLanguage.levelNames = new Dictionary<string, string>();
                }

                int refreshedButtons = 0;
                int refreshedButtonLabels = 0;
                var refreshInformationMethod = AccessTools.Method(typeof(UILevelSelectButton), "RefreshInformation");
                var nameTextField = AccessTools.Field(typeof(UILevelSelectButton), "nameText");

                foreach (var button in FindObjectsOfTypeCached<UILevelSelectButton>(true))
                {
                    if (button == null)
                        continue;

                    if (refreshInformationMethod != null)
                    {
                        try
                        {
                            refreshInformationMethod.Invoke(button, null);
                            refreshedButtons++;
                        }
                        catch
                        {
                        }
                    }

                    var nameText = nameTextField?.GetValue(button) as TMP_Text;
                    if (nameText == null || string.IsNullOrEmpty(nameText.text))
                        continue;

                    string source = ResolveOriginalTranslationKey(nameText.text, levelNames);
                    string before = nameText.text;
                    TranslateTextAndSaveIfMissing(nameText, source, levelNames, "[Plugin][LevelSelectButton]");
                    if (!string.Equals(before, nameText.text, StringComparison.Ordinal))
                        refreshedButtonLabels++;
                }

                if (refreshInformationMethod == null)
                {
                    foreach (var button in FindObjectsOfTypeCached<UILevelSelectButton>(true))
                    {
                        if (button == null)
                            continue;

                        var nameText = nameTextField?.GetValue(button) as TMP_Text;
                        if (nameText == null || string.IsNullOrEmpty(nameText.text))
                            continue;

                        string source = ResolveOriginalTranslationKey(nameText.text, levelNames);
                        string before = nameText.text;
                        TranslateTextAndSaveIfMissing(nameText, source, levelNames, "[Plugin][LevelSelectButton]");
                        if (!string.Equals(before, nameText.text, StringComparison.Ordinal))
                            refreshedButtonLabels++;
                    }
                }

                int refreshedRoots = 0;
                var selectCategoryMethod = AccessTools.Method(typeof(UILevelSelectionRoot), "SelectCategory");
                var parameter = selectCategoryMethod?.GetParameters().FirstOrDefault();
                var parameterType = parameter?.ParameterType;

                if (selectCategoryMethod != null && parameterType != null)
                {
                    foreach (var root in FindObjectsOfTypeCached<UILevelSelectionRoot>(true))
                    {
                        if (root == null)
                            continue;

                        object argument = FindFirstValueByType(root, parameterType);
                        if (argument == null)
                            continue;

                        try
                        {
                            selectCategoryMethod.Invoke(root, new[] { argument });
                            refreshedRoots++;
                        }
                        catch
                        {
                        }
                    }
                }

                if (refreshedButtons > 0 || refreshedRoots > 0 || refreshedButtonLabels > 0)
                    Logging.Info($"[Plugin] Refreshed LevelSelect UI (buttons={refreshedButtons}, buttonLabels={refreshedButtonLabels}, roots={refreshedRoots}).");
            }
            catch (Exception e)
            {
                Logging.Warn($"[Plugin] RefreshLevelSelectUiIfPresent failed: {e.Message}");
            }
        }

        private static object FindFirstValueByType(object instance, Type wantedType)
        {
            if (instance == null || wantedType == null)
                return null;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var instanceType = instance.GetType();

            foreach (var field in instanceType.GetFields(flags))
            {
                if (!wantedType.IsAssignableFrom(field.FieldType))
                    continue;

                var value = field.GetValue(instance);
                if (value != null)
                    return value;
            }

            foreach (var property in instanceType.GetProperties(flags))
            {
                if (!property.CanRead || property.GetIndexParameters().Length != 0)
                    continue;
                if (!wantedType.IsAssignableFrom(property.PropertyType))
                    continue;

                try
                {
                    var value = property.GetValue(instance, null);
                    if (value != null)
                        return value;
                }
                catch
                {
                }
            }

            return null;
        }
    }
}
