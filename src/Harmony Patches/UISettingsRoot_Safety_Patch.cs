using System;
using HarmonyLib;
using UnityEngine;

namespace IAmYourTranslator.Harmony_Patches
{
    // Safety clamps for subMenuIndex after dynamic tab injection
    [HarmonyPatch(typeof(UISettingsRoot))]
    public static class UISettingsRoot_Safety_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch("OpenSubMenu")]
        public static bool OpenSubMenuPrefix(UISettingsRoot __instance, UISettingsSubMenu menu)
        {
            try
            {
                var tr = Traverse.Create(__instance);
                var subMenus = tr.Field("subMenus").GetValue<UISettingsSubMenu[]>() ?? Array.Empty<UISettingsSubMenu>();
                if (subMenus.Length == 0 || menu == null)
                    return false;

                foreach (var subMenu in subMenus)
                {
                    if (subMenu != null)
                        subMenu.gameObject.SetActive(false);
                }

                menu.gameObject.SetActive(true);

                int idx = Array.IndexOf(subMenus, menu);
                if (idx < 0 || idx >= subMenus.Length)
                    idx = 0;

                tr.Field("subMenuIndex").SetValue(idx);

                var selected = subMenus[idx];
                if (selected != null)
                    selected.SelectTopOption();
            }
            catch (Exception e)
            {
                Logging.Warn($"[UISettingsRoot_Safety] OpenSubMenu safe routing failed: {e.Message}");
            }

            // Skip original OpenSubMenu: it relies on transform sibling index and can go out of range.
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("Update")]
        public static void UpdatePrefix(UISettingsRoot __instance)
        {
            var tr = Traverse.Create(__instance);
            var subMenus = tr.Field("subMenus").GetValue<UISettingsSubMenu[]>();
            int idx = tr.Field("subMenuIndex").GetValue<int>();
            if (subMenus == null || subMenus.Length == 0)
                return;
            if (idx < 0 || idx >= subMenus.Length)
                tr.Field("subMenuIndex").SetValue(0);
        }
    }
}
