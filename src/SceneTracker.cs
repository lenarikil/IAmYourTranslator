using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator
{
    public class SceneTracker : MonoBehaviour
    {
        private Scene lastScene;
        private bool isInitialized = false;

        public void Start()
        {
            // Initialize log from plugin
            Logging.Info("SceneTracker initialized");

            // Get the current scene
            lastScene = GetCurrentScene();
            Logging.Info($"Initial scene: {lastScene.name}");
            
            // Subscribe to events
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            
            // Also subscribe to active scene change (more reliable method)
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            
            isInitialized = true;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Logging.Info($"Scene loaded: {scene.name}");
            Logging.Info($"Scene buildIndex: {scene.buildIndex}");
            Logging.Info($"Scene path: {scene.path}");
        }

        private void OnSceneUnloaded(Scene scene)
        {
            Logging.Info($"Scene unloaded: {scene.name}");
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene currentScene)
        {
            Logging.Info($"Active scene changed from '{previousScene.name}' to '{currentScene.name}'");
            Logging.Info($"Scene buildIndex: {currentScene.buildIndex}");
        }

        private void Update()
        {
            if (!isInitialized) return;

            // Check if the scene has changed
            Scene currentScene = SceneManager.GetActiveScene();
            if (currentScene != lastScene)
            {
                Logging.Info($"Scene changed from '{lastScene.name}' to '{currentScene.name}'");
                Logging.Info($"Scene buildIndex: {currentScene.buildIndex}");
                lastScene = currentScene;
            }
        }

        public void OnDestroy()
        {
            // Remove event subscriptions
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            
            Logging.Info("SceneTracker destroyed");
        }
    }
}