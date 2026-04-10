using System.Collections.Generic;

namespace IAmYourTranslator.json
{
    [System.Serializable]
    public class JsonFormat
    {
        [System.Serializable]
        public class Metadata
        {
            public string langName { get; set; }
            public string langAuthor { get; set; }
            public string langVersion { get; set; }
            public string langDisplayName { get; set; }
            public string minimumModVersion { get; set; }
            // Optional font file inside fonts/ folder of the language
            public string fontFile { get; set; }
        }

        // General metadata used for UI and compatibility checks
        public Metadata metadata { get; set; } = new Metadata();

        // Here we store translations for timings (for cutscenes)
        public Dictionary<string, List<string>> timings = new Dictionary<string, List<string>>();

        // Translations for prompts
        public Dictionary<string, string> tutorialPrompts { get; set; } = new Dictionary<string, string>();

        // Regular NPC radio messages (TriggerRadio)
        public Dictionary<string, string> enemyRadio { get; set; } = new Dictionary<string, string>();

        // Scripted radio messages (EnemyRadioMessageEvent)
        public Dictionary<string, Dictionary<string, string>> enemyRadioScripted { get; set; } = new Dictionary<string, Dictionary<string, string>>();

        // Translations of level names
        public Dictionary<string, string> levelNames { get; set; } = new Dictionary<string, string>();
    // NOTE: categoryNames removed — use categorySlideTexts for all UILevelSelectCategorySlide translations
        // Translations for bonus objectives (UILevelCompleteBonusObjectiveListing)
        public Dictionary<string, string> bonusObjectives { get; set; } = new Dictionary<string, string>();
        // Translations for main objectives (HUD / main UI)
        public Dictionary<string, string> mainObjectives { get; set; } = new Dictionary<string, string>();
        // Translations for HUD notification pop-ups (HUDNotificationPopUp)
        public Dictionary<string, string> hudNotificationPopups { get; set; } = new Dictionary<string, string>();
        // Translations for HUD interaction prompts (HUDInteractionPrompt)
        public Dictionary<string, string> interactionPrompts { get; set; } = new Dictionary<string, string>();
        // Translations of level failure screen headers (UILevelFailed.headerText)
        public Dictionary<string, string> levelFailedHeaders { get; set; } = new Dictionary<string, string>();
        // Translations related to the category slide in level select (UILevelSelectCategorySlide)
        public Dictionary<string, string> categorySlideTexts { get; set; } = new Dictionary<string, string>();
        // Translations of headers in category stats (UILevelSelectCategoryStat)
        public Dictionary<string, string> categoryStatHeaders { get; set; } = new Dictionary<string, string>();
    // Translations of level lock descriptions (UILevelSelectFeature.lockedDescription)
    public Dictionary<string, string> lockedDescriptions { get; set; } = new Dictionary<string, string>();
        // Translations for settings (UISettingsTab)
        public Dictionary<string, string> settings { get; set; } = new Dictionary<string, string>();
        // Translations related to save messages/warnings (SaveSystem)
        public Dictionary<string, string> saveSystem { get; set; } = new Dictionary<string, string>();
        // Translations of kill types in HUD (HUDKillConfirmation)
        public Dictionary<string, string> killConfirmation { get; set; } = new Dictionary<string, string>();
        // Translations of time and score bonus descriptions (HUDTimerIncrease)
        public Dictionary<string, string> timerIncrease { get; set; } = new Dictionary<string, string>();
        // Translations for HUDLevelThreatTimer texts (header and warning) - hierarchical: "headerText" and "warningText" sub-dictionaries
        public Dictionary<string, Dictionary<string, string>> threatTimerTexts { get; set; } = new Dictionary<string, Dictionary<string, string>>();
        // Translations of headers on the level completion screen (Final Screen)
        public Dictionary<string, string> finalScreen { get; set; } = new Dictionary<string, string>();

        // Translations of level overview screen elements (Start, End)
        public Dictionary<string, string> overviewScreen { get; set; } = new Dictionary<string, string>();
        // Translations for UI hints
        public Dictionary<string, string> Hints { get; set; } = new Dictionary<string, string>();
        // Translations of hardcoded texts (FleeceTextSetter)
        public Dictionary<string, string> hardCoded { get; set; } = new Dictionary<string, string>();
    }
}
