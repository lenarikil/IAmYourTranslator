using HarmonyLib;
using TMPro;
using static IAmYourTranslator.CommonFunctions;
using IAmYourTranslator.json;

namespace IAmYourTranslator.Patches
{
    [HarmonyPatch(typeof(UILevelIntro), "Initialize")]
    public static class UILevelIntro_FontPatch
    {
        [HarmonyPostfix]
        public static void Postfix(UILevelIntro __instance)
        {
            PatchHelper.SafeExecute(nameof(UILevelIntro_FontPatch), () =>
            {
                var font = Plugin.GlobalTMPFont;
                if (font == null)
                    return;

                var levelNamesField = AccessTools.Field(typeof(UILevelIntro), "levelNames");
                var levelNames = levelNamesField?.GetValue(__instance) as TMP_Text[];
                if (levelNames == null)
                    return;

                foreach (var tmp in levelNames)
                {
                    if (tmp == null) continue;
                    TMPFontReplacer.ApplyFontToTMP(tmp, font);
                }
            });
        }
    }
}
