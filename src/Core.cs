using UnityEngine;
using UnityEngine.SceneManagement;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator
{
    public static class Core
    {
        // Encapsulation function to patch all of the front end.
        public static void HandleSceneSwitch(Scene scene, ref GameObject canvas)
        {
            string levelName = GetCurrentSceneName();
            Logging.Info($"Current scene: {levelName}");
        }

        public static void ApplyPostInitFixes(GameObject canvasObj)
        {
            // Post-init fixes can be added here
        }
    }
}
