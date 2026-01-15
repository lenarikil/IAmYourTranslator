using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using IAmYourTranslator.json;
using static IAmYourTranslator.CommonFunctions;

namespace IAmYourTranslator.Harmony_Patches
{
    [HarmonyPatch(typeof(UILevelCompleteTimeScoreBar))]
    public static class UILevelCompleteTimeScoreBar_Patch
    {
        // track instances we've already initialized to avoid repeating work/logs every frame
        private static readonly System.Collections.Generic.HashSet<int> _seenInstances = new System.Collections.Generic.HashSet<int>();
        // track keys we've already added to avoid saving JSON every frame
        private static readonly System.Collections.Generic.HashSet<string> _addedFinalScreenKeys = new System.Collections.Generic.HashSet<string>();
        // DIAGNOSTICS: ensure that the patch has been applied
        private static bool _patchApplied = false;

        // Translates the headers for time spent on the level and time that needs to be reduced to get a new rank on the results screen.
        // Category in JSON: "finalScreen"
        // Use Initialize() instead of Update() because Update() may be called only once or not called at all
        [HarmonyPostfix]
        [HarmonyPatch("Initialize")]
        public static void InitializePostfix(UILevelCompleteTimeScoreBar __instance)
        {
            try
            {
                if (__instance == null) return;

                // DIAGNOSTICS: check if this method is called at all
                if (!_patchApplied)
                {
                    Logging.Info("[UILevelCompleteTimeScoreBar_Patch] *** PATCH IS EXECUTING ON INITIALIZE! ***");
                    _patchApplied = true;
                }

                var dict = LanguageManager.CurrentLanguage.finalScreen;
                if (dict == null)
                {
                    dict = new System.Collections.Generic.Dictionary<string, string>();
                    LanguageManager.CurrentLanguage.finalScreen = dict;
                    Logging.Info("[UILevelCompleteTimeScoreBar] Initialized finalScreen dictionary");
                }

                int iid = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(__instance);
                bool firstTimeForInstance = false;
                lock (_seenInstances)
                {
                    if (!_seenInstances.Contains(iid))
                    {
                        _seenInstances.Add(iid);
                        firstTimeForInstance = true;
                    }
                }
                if (firstTimeForInstance)
                {
                    Logging.Info($"[UILevelCompleteTimeScoreBar] Patch attached to instance (id={iid})");
                }

                // timeTextHeader
                try
                {
                    var originalTime = string.Empty;
                    var timeField = Traverse.Create(__instance).Field("passageTime").GetValue<object>();
                    Logging.Info($"[UILevelCompleteTimeScoreBar] timeField is null: {timeField == null}");
                    if (timeField != null)
                    {
                        // Try as property first, then as field
                        var parsed = Traverse.Create(timeField).Property("passage").GetValue<object>();
                        if (parsed == null)
                            parsed = Traverse.Create(timeField).Field("passage").GetValue<object>();
                        
                        Logging.Info($"[UILevelCompleteTimeScoreBar] parsed is null: {parsed == null}");
                        if (parsed != null)
                        {
                            var parsedText = Traverse.Create(parsed).Property("parsedText").GetValue<string>();
                            if (parsedText == null)
                                parsedText = Traverse.Create(parsed).Field("parsedText").GetValue<string>();
                            
                            Logging.Info($"[UILevelCompleteTimeScoreBar] parsedText: '{parsedText}' (null: {parsedText == null})");
                            originalTime = parsedText ?? string.Empty;
                        }
                    }

                    Logging.Info($"[UILevelCompleteTimeScoreBar] originalTime: '{originalTime}' (empty: {string.IsNullOrEmpty(originalTime)})");

                    if (!string.IsNullOrEmpty(originalTime))
                    {
                        if (dict.TryGetValue(originalTime, out var trans) && !string.IsNullOrEmpty(trans) && trans != originalTime)
                        {
                            var timeHeaderField = GetFieldTMPText(__instance, "timeTextHeader");
                            if (timeHeaderField != null) timeHeaderField.text = trans + ": ";
                        }
                        else if (!dict.ContainsKey(originalTime))
                        {
                            dict[originalTime] = originalTime;
                            Logging.Info($"[UILevelCompleteTimeScoreBar] Added key to dict: '{originalTime}' (dict now has {dict.Count} keys)");
                            // save once per key to avoid frequent disk writes
                            lock (_addedFinalScreenKeys)
                            {
                                if (!_addedFinalScreenKeys.Contains(originalTime))
                                {
                                    _addedFinalScreenKeys.Add(originalTime);
                                    Logging.Info($"[UILevelCompleteTimeScoreBar] Saving JSON with finalScreen key: '{originalTime}' (total keys: {dict.Count})");
                                    LanguageManager.SaveCurrentLanguage();
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Logging.Warn($"[UILevelCompleteTimeScoreBar] timeTextHeader translation error: {e}");
                }

                // nextTextHeader - Next Grade / Past S Rank
                try
                {
                    var originalNext = string.Empty;
                    var nextField = Traverse.Create(__instance).Field("passageToNext").GetValue<object>();
                    Logging.Info($"[UILevelCompleteTimeScoreBar] nextField (passageToNext) is null: {nextField == null}");
                    if (nextField == null)
                    {
                        nextField = Traverse.Create(__instance).Field("passagePastS").GetValue<object>();
                        Logging.Info($"[UILevelCompleteTimeScoreBar] nextField (passagePastS) is null: {nextField == null}");
                    }
                    if (nextField != null)
                    {
                        // Try as property first, then as field
                        var parsed = Traverse.Create(nextField).Property("passage").GetValue<object>();
                        if (parsed == null)
                            parsed = Traverse.Create(nextField).Field("passage").GetValue<object>();
                        
                        Logging.Info($"[UILevelCompleteTimeScoreBar] nextField.passage is null: {parsed == null}");
                        if (parsed != null)
                        {
                            var parsedText = Traverse.Create(parsed).Property("parsedText").GetValue<string>();
                            if (parsedText == null)
                                parsedText = Traverse.Create(parsed).Field("parsedText").GetValue<string>();
                            
                            Logging.Info($"[UILevelCompleteTimeScoreBar] nextField parsedText: '{parsedText}' (null: {parsedText == null})");
                            originalNext = parsedText ?? string.Empty;
                        }
                    }

                    Logging.Info($"[UILevelCompleteTimeScoreBar] originalNext: '{originalNext}' (empty: {string.IsNullOrEmpty(originalNext)})");

                    if (!string.IsNullOrEmpty(originalNext))
                    {
                        if (dict.TryGetValue(originalNext, out var trans2) && !string.IsNullOrEmpty(trans2) && trans2 != originalNext)
                        {
                            var nextHeaderField = GetFieldTMPText(__instance, "nextTextHeader");
                            if (nextHeaderField != null) nextHeaderField.text = trans2 + ": ";
                        }
                        else if (!dict.ContainsKey(originalNext))
                        {
                            dict[originalNext] = originalNext;
                            Logging.Info($"[UILevelCompleteTimeScoreBar] Added key to dict: '{originalNext}' (dict now has {dict.Count} keys)");
                            lock (_addedFinalScreenKeys)
                            {
                                if (!_addedFinalScreenKeys.Contains(originalNext))
                                {
                                    _addedFinalScreenKeys.Add(originalNext);
                                    Logging.Info($"[UILevelCompleteTimeScoreBar] Saving JSON with finalScreen key: '{originalNext}' (total keys: {dict.Count})");
                                    LanguageManager.SaveCurrentLanguage();
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Logging.Warn($"[UILevelCompleteTimeScoreBar] nextTextHeader translation error: {e}");
                }

                // bestText - Best: X.XX
                try
                {
                    var originalBest = string.Empty;
                    var bestField = Traverse.Create(__instance).Field("passageBest").GetValue<object>();
                    Logging.Info($"[UILevelCompleteTimeScoreBar] bestField is null: {bestField == null}");
                    if (bestField != null)
                    {
                        // Try as property first, then as field
                        var parsed = Traverse.Create(bestField).Property("passage").GetValue<object>();
                        if (parsed == null)
                            parsed = Traverse.Create(bestField).Field("passage").GetValue<object>();
                        
                        Logging.Info($"[UILevelCompleteTimeScoreBar] bestField.passage is null: {parsed == null}");
                        if (parsed != null)
                        {
                            var parsedText = Traverse.Create(parsed).Property("parsedText").GetValue<string>();
                            if (parsedText == null)
                                parsedText = Traverse.Create(parsed).Field("parsedText").GetValue<string>();
                            
                            Logging.Info($"[UILevelCompleteTimeScoreBar] bestField parsedText: '{parsedText}' (null: {parsedText == null})");
                            originalBest = parsedText ?? string.Empty;
                        }
                    }

                    Logging.Info($"[UILevelCompleteTimeScoreBar] originalBest: '{originalBest}' (empty: {string.IsNullOrEmpty(originalBest)})");

                    if (!string.IsNullOrEmpty(originalBest))
                    {
                        if (dict.TryGetValue(originalBest, out var trans3) && !string.IsNullOrEmpty(trans3) && trans3 != originalBest)
                        {
                            var bestTextField = GetFieldTMPText(__instance, "bestText");
                            if (bestTextField != null)
                            {
                                // bestText contains "Best: X.XX", need to replace only "Best"
                                string fullText = bestTextField.text;
                                if (fullText.Contains(": "))
                                {
                                    int colonIndex = fullText.IndexOf(": ");
                                    if (colonIndex > 0)
                                    {
                                        string valuePart = fullText.Substring(colonIndex);
                                        bestTextField.text = trans3 + valuePart;
                                    }
                                }
                            }
                        }
                        else if (!dict.ContainsKey(originalBest))
                        {
                            dict[originalBest] = originalBest;
                            Logging.Info($"[UILevelCompleteTimeScoreBar] Added key to dict: '{originalBest}' (dict now has {dict.Count} keys)");
                            lock (_addedFinalScreenKeys)
                            {
                                if (!_addedFinalScreenKeys.Contains(originalBest))
                                {
                                    _addedFinalScreenKeys.Add(originalBest);
                                    Logging.Info($"[UILevelCompleteTimeScoreBar] Saving JSON with finalScreen key: '{originalBest}' (total keys: {dict.Count})");
                                    LanguageManager.SaveCurrentLanguage();
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Logging.Warn($"[UILevelCompleteTimeScoreBar] bestText translation error: {e}");
                }

                // Apply global TMP font to headers
                try
                {
                    var tmpFont = Plugin.GlobalTMPFont;
                    if (tmpFont != null)
                    {
                        var timeHeader = GetFieldTMPText(__instance, "timeTextHeader");
                        var nextHeader = GetFieldTMPText(__instance, "nextTextHeader");
                        if (timeHeader != null) TMPFontReplacer.ApplyFontToTMP(timeHeader, tmpFont);
                        if (nextHeader != null) TMPFontReplacer.ApplyFontToTMP(nextHeader, tmpFont);
                    }
                }
                catch { }
            }
            catch (Exception e)
            {
                Logging.Error($"[UILevelCompleteTimeScoreBar] InitializePostfix error: {e}");
            }
        }

        // Postfix on Update() to move text AFTER the game has set it
        [HarmonyPostfix]
        [HarmonyPatch("Update")]
        public static void UpdatePostfix(UILevelCompleteTimeScoreBar __instance)
        {
            try
            {
                if (__instance == null) return;
                var dict = LanguageManager.CurrentLanguage.finalScreen;
                if (dict == null || dict.Count == 0) return;

                // timeTextHeader - extract text WITHOUT ": " at the end and apply translation
                try
                {
                    var timeHeader = GetFieldTMPText(__instance, "timeTextHeader");
                    if (timeHeader != null && timeHeader.text.EndsWith(": "))
                    {
                        string currentText = timeHeader.text.Substring(0, timeHeader.text.Length - 2); // remove ": "
                        if (dict.TryGetValue(currentText, out var translated) && !string.IsNullOrEmpty(translated) && translated != currentText)
                        {
                            timeHeader.text = translated + ": ";
                        }
                    }
                }
                catch { }
            }
            catch { }
        }

        // Postfix on EndFill() to apply translation for nextTextHeader and pastSHeader
        [HarmonyPostfix]
        [HarmonyPatch("EndFill")]
        public static void EndFillPostfix(UILevelCompleteTimeScoreBar __instance)
        {
            try
            {
                if (__instance == null) return;
                var dict = LanguageManager.CurrentLanguage.finalScreen;
                if (dict == null || dict.Count == 0) return;

                // nextTextHeader and pastSHeader - apply translation
                try
                {
                    var nextHeader = GetFieldTMPText(__instance, "nextTextHeader");
                    if (nextHeader != null && nextHeader.text.EndsWith(": "))
                    {
                        string currentText = nextHeader.text.Substring(0, nextHeader.text.Length - 2); // remove ": "
                        if (dict.TryGetValue(currentText, out var translated) && !string.IsNullOrEmpty(translated) && translated != currentText)
                        {
                            nextHeader.text = translated + ": ";
                            Logging.Info($"[UILevelCompleteTimeScoreBar] Applied translation in EndFill: '{currentText}' → '{translated}'");
                        }
                        else if (!dict.ContainsKey(currentText))
                        {
                            // If key doesn't exist — add and save once (like for other finalScreen keys)
                            dict[currentText] = currentText;
                            Logging.Info($"[UILevelCompleteTimeScoreBar] Added finalScreen key from EndFill: '{currentText}' (dict now has {dict.Count} keys)");
                            lock (_addedFinalScreenKeys)
                            {
                                if (!_addedFinalScreenKeys.Contains(currentText))
                                {
                                    _addedFinalScreenKeys.Add(currentText);
                                    Logging.Info($"[UILevelCompleteTimeScoreBar] Saving JSON with finalScreen key from EndFill: '{currentText}' (total keys: {dict.Count})");
                                    LanguageManager.SaveCurrentLanguage();
                                }
                            }
                        }
                    }
                }
                catch { }
            }
            catch { }
        }

        // Postfix on UpdateBestMarker() to apply translation for bestText
        [HarmonyPostfix]
        [HarmonyPatch("UpdateBestMarker")]
        public static void UpdateBestMarkerPostfix(UILevelCompleteTimeScoreBar __instance)
        {
            try
            {
                if (__instance == null) return;
                var dict = LanguageManager.CurrentLanguage.finalScreen;
                if (dict == null || dict.Count == 0) return;

                // bestText - contains "Best: X.XX", need to catch and translate only "Best"
                try
                {
                    var bestText = GetFieldTMPText(__instance, "bestText");
                    if (bestText != null && bestText.text.Contains(": "))
                    {
                        string fullText = bestText.text;
                        int colonIndex = fullText.IndexOf(": ");
                        if (colonIndex > 0)
                        {
                            string headerPart = fullText.Substring(0, colonIndex);
                            string valuePart = fullText.Substring(colonIndex); // ": X.XX"
                            
                            if (dict.TryGetValue(headerPart, out var translated) && !string.IsNullOrEmpty(translated) && translated != headerPart)
                            {
                                bestText.text = translated + valuePart;
                                Logging.Info($"[UILevelCompleteTimeScoreBar] Applied translation in UpdateBestMarker: '{headerPart}' → '{translated}'");
                            }
                        }
                    }
                }
                catch { }
            }
            catch { }
        }

        private static TMPro.TMP_Text GetFieldTMPText(object instance, string fieldName)
        {
            try
            {
                var fi = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (fi == null) return null;
                var val = fi.GetValue(instance);
                return val as TMPro.TMP_Text;
            }
            catch { return null; }
        }
    }
}
