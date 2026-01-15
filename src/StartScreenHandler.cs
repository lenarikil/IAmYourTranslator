using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using static IAmYourTranslator.CommonFunctions;
using System.IO;
using BepInEx;

namespace IAmYourTranslator
{
    public class StartScreenHandler
    {
        public static void HandleStartScreen()
        {
            Logging.Info("Handling Start Screen scene");

            // Just call the method immediately, without delay
            //PatchMainMenuButtons();
            string TexturesDir = Path.Combine(Paths.ConfigPath, "IAmYourTranslator", "textures");
            string TextureFile = Path.Combine(TexturesDir, "UILogoText.png");
            GameObject TitleImage = GameObject.Find("Start Screen UI/Title Anchor/Title Image");

            UITextureReplacer.ApplyTo(TitleImage, TextureFile);
            
            Logging.Info("Start Screen handling completed");
        }
        
        public static void PatchMainMenuButtons()
        {
            try
            {
                Logging.Info("Patching main menu buttons");

                // Find the button by name
                GameObject buttonStart = GameObject.Find("Button Start");
                
                if (buttonStart != null)
                {
                    Logging.Info("Found Button Start object");

                    // Disable the FleeceTextSetter script
                    FleeceTextSetter textSetter = buttonStart.GetComponent<FleeceTextSetter>();
                    if (textSetter != null)
                    {
                        Logging.Info("Disabling FleeceTextSetter component");
                        textSetter.enabled = false; // Disable the script
                    }
                    else
                    {
                        Logging.Warn("FleeceTextSetter component not found");
                    }

                    // Now change the text directly
                    TextMeshProUGUI tmpText = buttonStart.GetComponentInChildren<TextMeshProUGUI>();
                    if (tmpText != null)
                    {
                        tmpText.text = "Start Game";
                        Logging.Info("Text changed successfully");
                    }
                    else
                    {
                        Logging.Warn("TextMeshProUGUI component not found");
                    }
                }
                else
                {
                    Logging.Warn("Button Start object not found");
                }
            }
            catch (System.Exception e)
            {
                Logging.Error($"Error in PatchMainMenuButtons: {e.Message}");
            }
        }
    }
}