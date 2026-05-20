using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AlperKocasalih.Chess.Grid
{
    public enum Language
    {
        TR,
        EN,
        DE,
        FR,
        PT,
        ES
    }

    public static class LocalizationManager
    {
        public static event Action OnLanguageChanged;

        private static Language currentLanguage = Language.TR;
        private static Dictionary<string, string> localizedStrings = new Dictionary<string, string>();

        private const string PrefKeyLanguage = "SelectedLanguage";

        static LocalizationManager()
        {
            LoadLanguagePreference();
        }

        public static Language CurrentLanguage
        {
            get => currentLanguage;
            set
            {
                if (currentLanguage != value)
                {
                    currentLanguage = value;
                    PlayerPrefs.SetString(PrefKeyLanguage, currentLanguage.ToString());
                    PlayerPrefs.Save();
                    LoadLanguageFile();
                    TriggerSceneWideRefresh();
                }
            }
        }

        private static void LoadLanguagePreference()
        {
            string savedLang = PlayerPrefs.GetString(PrefKeyLanguage, "TR");
            if (Enum.TryParse(savedLang, out Language parsedLang))
            {
                currentLanguage = parsedLang;
            }
            else
            {
                currentLanguage = Language.TR;
            }
            LoadLanguageFile();
        }

        private static void LoadLanguageFile()
        {
            localizedStrings.Clear();
            string fileName = $"Localization/{currentLanguage.ToString().ToLower()}";
            TextAsset textAsset = Resources.Load<TextAsset>(fileName);

            if (textAsset != null)
            {
                localizedStrings = ParseFlatJson(textAsset.text);
                Debug.Log($"[LocalizationManager] Loaded language: {currentLanguage} with {localizedStrings.Count} keys.");
            }
            else
            {
                Debug.LogWarning($"[LocalizationManager] Could not find localization file: Resources/{fileName}.json");
            }
        }

        public static string GetTranslation(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            if (localizedStrings.TryGetValue(key, out string value))
            {
                return value;
            }
            
            // Return key as fallback
            return key;
        }

        /// <summary>
        /// Highly robust parser for flat JSON structures (key-value strings) using Regex.
        /// Avoids any dependencies on third-party JSON libraries and supports escaped quotes.
        /// </summary>
        private static Dictionary<string, string> ParseFlatJson(string jsonText)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(jsonText)) return dict;

            // Matches "key" : "value" patterns including backslash-escaped characters
            var matches = Regex.Matches(jsonText, @"\""([^\""\\]*(?:\\.[^\""\\]*)*)\""\s*:\s*\""([^\""\\]*(?:\\.[^\""\\]*)*)\""");
            foreach (Match match in matches)
            {
                string key = match.Groups[1].Value.Replace("\\\"", "\"").Replace("\\n", "\n");
                string value = match.Groups[2].Value.Replace("\\\"", "\"").Replace("\\n", "\n");
                dict[key] = value;
            }
            return dict;
        }

        /// <summary>
        /// Parses a formatted multi-line buff summary on the fly and translates the buff names.
        /// Keeps the icons and remaining turn count intact.
        /// </summary>
        public static string LocalizeSummary(string summary)
        {
            if (string.IsNullOrWhiteSpace(summary)) return summary;

            string[] lines = summary.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string spriteTag = "";

                if (line.StartsWith("<sprite"))
                {
                    int endIdx = line.IndexOf('>');
                    if (endIdx != -1)
                    {
                        spriteTag = line.Substring(0, endIdx + 1) + " ";
                        line = line.Substring(endIdx + 1).Trim();
                    }
                }

                string turnsSuffix = "";
                if (line.EndsWith(")"))
                {
                    int startIdx = line.LastIndexOf('(');
                    if (startIdx != -1)
                    {
                        turnsSuffix = " " + line.Substring(startIdx);
                        line = line.Substring(0, startIdx).Trim();
                    }
                }

                // Translate clean buff name
                string translatedName = GetTranslation(line);
                lines[i] = $"{spriteTag}{translatedName}{turnsSuffix}";
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Nuclear option: Invokes the event and scans the active scene for ALL TextMeshProUGUI elements.
        /// If an element's text matches a key in our localization dictionary, it translates it instantly.
        /// </summary>
        private static void TriggerSceneWideRefresh()
        {
            // 1. Invoke event for scripts that have explicitly registered listeners (e.g. TurnManager, DraftUI)
            OnLanguageChanged?.Invoke();

            // 2. Find and translate all TextMeshProUGUI components in the active scene
            var allTMPs = GameObject.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None);
            if (allTMPs == null) return;

            foreach (var tmp in allTMPs)
            {
                if (tmp == null) continue;

                // Try to find direct translation matching the trimmed text
                string textToLookUp = tmp.text.Trim();
                if (localizedStrings.TryGetValue(textToLookUp, out string translatedValue))
                {
                    tmp.text = translatedValue;
                }
            }
        }
    }
}
