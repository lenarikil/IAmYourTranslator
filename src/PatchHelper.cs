using System;

namespace IAmYourTranslator
{
    public static class PatchHelper
    {
        public static void SafeExecute(string patchName, Action work)
        {
            try
            {
                work?.Invoke();
            }
            catch (Exception ex)
            {
                Logging.Warn($"[{patchName}] Error: {ex}");
            }
        }
    }
}
