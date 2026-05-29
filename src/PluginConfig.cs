using BepInEx.Configuration;

namespace IAmYourTranslator
{
    public sealed class PluginConfig
    {
        public ConfigEntry<string> SelectedLanguage { get; }
        public ConfigEntry<bool> EnableAudioReplacement { get; }
        public ConfigEntry<bool> EnableTextureReplacement { get; }
        public ConfigEntry<bool> EnableAudioDebugLogs { get; }
        public ConfigEntry<bool> EnableMusicProfileDebugLogs { get; }
        public ConfigEntry<bool> EnableExperimentalRadioAudioPatches { get; }

        public PluginConfig(ConfigFile config)
        {
            SelectedLanguage = config.Bind("General", "SelectedLanguage", "", "Language code to load (folder name inside languages/). Leave empty for vanilla.");
            EnableAudioReplacement = config.Bind("General", "EnableAudioReplacement", true, "If true, custom language audio will replace originals when available.");
            EnableTextureReplacement = config.Bind("General", "EnableTextureReplacement", true, "If true, custom language textures will replace originals when available.");
            EnableAudioDebugLogs = config.Bind("Debug", "EnableAudioDebugLogs", false, "If true, verbose AudioSource.Play/OneShot logging is enabled.");
            EnableMusicProfileDebugLogs = config.Bind("Debug", "EnableMusicProfileDebugLogs", false, "If true, verbose LevelMusicProfile (start/combat/dim music) logging is enabled.");
            EnableExperimentalRadioAudioPatches = config.Bind("Experimental", "EnableExperimentalRadioAudioPatches", false, "If true, enables aggressive radio-camera audio patches (may cause instability).");
        }
    }
}
