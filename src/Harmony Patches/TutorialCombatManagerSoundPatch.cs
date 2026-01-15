using System;
using HarmonyLib;
using UnityEngine;
using Sounds;
using static IAmYourTranslator.CommonFunctions;
using BepInEx;
using System.IO;

namespace IAmYourTranslator.HarmonyPatches
{
    [HarmonyPatch(typeof(TutorialCombatManager), "Start")]
    public static class TutorialCombatManagerSoundPatch
    {
        [HarmonyPostfix]
        public static void Postfix(TutorialCombatManager __instance)
        {
            try
            {
                if (__instance == null)
                    return;

                // Paths to custom sounds
                string customOpeningPath = Path.Combine(Paths.ConfigPath, "IAmYourTranslator", "audio", "6 - arguing soldiers2.wav");
                string customHesHerePath = Path.Combine(Paths.ConfigPath, "IAmYourTranslator", "audio", "8 - he's here.wav");

                // Getting private SoundObject fields
                var fieldOpening = AccessTools.Field(typeof(TutorialCombatManager), "SFXOpeningDialogue");
                var fieldHesHere = AccessTools.Field(typeof(TutorialCombatManager), "SFXHesHere");

                if (fieldOpening != null)
                {
                    SoundObject openingSound = fieldOpening.GetValue(__instance) as SoundObject;
                    AudioClipReplacer.ReplaceSoundObjectClip(openingSound, customOpeningPath, "SFXOpeningDialogue");
                }

                if (fieldHesHere != null)
                {
                    SoundObject hesHereSound = fieldHesHere.GetValue(__instance) as SoundObject;
                    AudioClipReplacer.ReplaceSoundObjectClip(hesHereSound, customHesHerePath, "SFXHesHere");
                }
            }
            catch (Exception e)
            {
                Logging.Error($"[TutorialSoundPatch] Error when replacing sounds: {e}");
            }
        }

        
    }
}
