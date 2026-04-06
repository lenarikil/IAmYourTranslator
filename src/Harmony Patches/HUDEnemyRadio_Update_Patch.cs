using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Enemy;

namespace IAmYourTranslator.HarmonyPatches
{
    [HarmonyPatch(typeof(HUDEnemyRadio), "Update")]
    public static class HUDEnemyRadio_Update_Patch
    {
        private static readonly FieldInfo FieldStaticCheck = AccessTools.Field(typeof(HUDEnemyRadio), "staticCheck");
        private static readonly FieldInfo FieldSpawnedHelmetCamera = AccessTools.Field(typeof(HUDEnemyRadio), "spawnedHelmetCamera");
        private static readonly FieldInfo FieldEnemy = AccessTools.Field(typeof(HUDEnemyRadio), "enemy");
        private static readonly FieldInfo FieldDontInterrupt = AccessTools.Field(typeof(HUDEnemyRadio), "dontInterrupt");
        private static readonly FieldInfo FieldCameraAudioSource = AccessTools.Field(typeof(HUDEnemyRadioCamera), "audioSource");
        
        private static bool IsExperimentalEnabled()
        {
            return Plugin.EnableExperimentalRadioAudioPatchesEntry != null &&
                   Plugin.EnableExperimentalRadioAudioPatchesEntry.Value;
        }

        [HarmonyPrefix]
        private static void Prefix(HUDEnemyRadio __instance, ref bool __state)
        {
            __state = false;
            if (!IsExperimentalEnabled())
                return;

            if (__instance == null)
                return;

            bool staticCheck = GetBool(FieldStaticCheck, __instance);
            if (!staticCheck)
                return;

            var camera = FieldSpawnedHelmetCamera?.GetValue(__instance) as HUDEnemyRadioCamera;
            if (camera == null || !camera.isActiveAndEnabled)
                return;

            var enemy = FieldEnemy?.GetValue(__instance) as EnemyHuman;
            if (enemy == null || enemy.IsAlive())
                return;

            bool dontInterrupt = GetBool(FieldDontInterrupt, __instance);
            if (dontInterrupt)
                return;

            if (IsCameraAudioPlaying(camera))
            {
                FieldDontInterrupt?.SetValue(__instance, true);
                __state = true;
            }
        }

        [HarmonyPostfix]
        private static void Postfix(HUDEnemyRadio __instance, bool __state)
        {
            if (!IsExperimentalEnabled())
                return;

            if (!__state || __instance == null)
                return;

            FieldDontInterrupt?.SetValue(__instance, false);
        }

        private static bool IsCameraAudioPlaying(HUDEnemyRadioCamera camera)
        {
            if (camera == null)
                return false;

            var source = FieldCameraAudioSource?.GetValue(camera) as AudioSource;
            if (source == null)
                source = camera.GetComponentInChildren<AudioSource>();

            return source != null && source.isPlaying;
        }

        private static bool GetBool(FieldInfo field, object instance)
        {
            if (field == null || instance == null)
                return false;

            object value = field.GetValue(instance);
            return value is bool b && b;
        }
    }
}
