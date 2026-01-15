using BepInEx.Logging;

namespace IAmYourTranslator
{
    public static class Logging 
    {
        public static ManualLogSource IAYTLogger = Logger.CreateLogSource("IAmYourTranslator LOGGING");

        public static void Debug(string text)
        {
            IAYTLogger.LogDebug(text);
        }
        
        public static void Message(string text)
        {
            IAYTLogger.LogMessage(text);
        }
        
        public static void Warn(string text)
        {
            IAYTLogger.LogWarning(text);
        }
        
        public static void Error(string text)
        {
            IAYTLogger.LogError(text);
        }
        
        public static void Fatal(string text)
        {
            IAYTLogger.LogFatal(text);
        }
        
        public static void Info(string text)
        {
            IAYTLogger.LogInfo(text);
        }

        
    }
}