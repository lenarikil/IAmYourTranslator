using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator
{
    public static class Core
    {

        //Encapsulation function to patch all of the front end.
        public static void HandleSceneSwitch(Scene scene, ref GameObject canvas)
        {
            string levelName = GetCurrentSceneName();
            Logging.Info($"Current scene: {levelName}");
        }

        public static async void ApplyPostInitFixes(GameObject canvasObj)
        {
            await Task.Delay(250); // Fix warning about async without await
        }
    }
}
