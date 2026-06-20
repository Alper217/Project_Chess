using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AlperKocasalih.Chess.Grid
{
    public enum Language
    {
        EN,
        DE,
        FR,
        PT,
        ES
    }

    public static class LocalizationManager
    {
        public static event Action OnLanguageChanged;

        private static Language currentLanguage = Language.EN;
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
            // One-time migration/reset to force default English on this version
            if (PlayerPrefs.GetInt("LanguageInitialized_v2", 0) == 0)
            {
                PlayerPrefs.SetString(PrefKeyLanguage, "EN");
                PlayerPrefs.SetInt("LanguageInitialized_v2", 1);
                PlayerPrefs.Save();
            }

            string savedLang = PlayerPrefs.GetString(PrefKeyLanguage, "EN");
            if (Enum.TryParse(savedLang, out Language parsedLang))
            {
                currentLanguage = parsedLang;
            }
            else
            {
                currentLanguage = Language.EN;
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

            if (TryGetBuiltInTranslation(key, out string builtInValue))
            {
                return builtInValue;
            }
            
            // Return key as fallback
            return key;
        }

        private static bool TryGetBuiltInTranslation(string key, out string value)
        {
            value = key;

            switch (currentLanguage)
            {
                case Language.DE:
                    switch (key)
                    {
                        case "Damage":
                            value = "Schaden";
                            return true;
                        case "Cooldown":
                            value = "Abklingzeit";
                            return true;
                        case "Turn":
                            value = "Zug";
                            return true;
                        case "Turns":
                            value = "Züge";
                            return true;
                    }
                    break;
                case Language.FR:
                    switch (key)
                    {
                        case "Damage":
                            value = "Dégâts";
                            return true;
                        case "Cooldown":
                            value = "Recharge";
                            return true;
                        case "Turn":
                            value = "tour";
                            return true;
                        case "Turns":
                            value = "tours";
                            return true;
                    }
                    break;
                case Language.PT:
                    switch (key)
                    {
                        case "Damage":
                            value = "Dano";
                            return true;
                        case "Cooldown":
                            value = "Recarga";
                            return true;
                        case "Turn":
                            value = "turno";
                            return true;
                        case "Turns":
                            value = "turnos";
                            return true;
                    }
                    break;
                case Language.ES:
                    switch (key)
                    {
                        case "Damage":
                            value = "Daño";
                            return true;
                        case "Cooldown":
                            value = "Enfriamiento";
                            return true;
                        case "Turn":
                            value = "turno";
                            return true;
                        case "Turns":
                            value = "turnos";
                            return true;
                    }
                    break;
                case Language.EN:
                default:
                    switch (key)
                    {
                        case "Damage":
                            value = "Damage";
                            return true;
                        case "Cooldown":
                            value = "Cooldown";
                            return true;
                        case "Turn":
                            value = "turn";
                            return true;
                        case "Turns":
                            value = "turns";
                            return true;
                    }
                    break;
            }

            return false;
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

                // Check if it already has LocalizedText
                var localizedComp = tmp.GetComponent<LocalizedText>();
                if (localizedComp == null)
                {
                    // Only add if the text matches a key in our localization dictionary
                    string textToLookUp = tmp.text.Trim();
                    if (localizedStrings.ContainsKey(textToLookUp) || TryGetBuiltInTranslation(textToLookUp, out _))
                    {
                        // Dynamically add LocalizedText so it registers for future changes and preserves the key
                        localizedComp = tmp.gameObject.AddComponent<LocalizedText>();
                    }
                }
                else
                {
                    // If it already has LocalizedText, trigger its update
                    localizedComp.UpdateText();
                }
            }
        }

        /// <summary>
        /// Highly robust utility to set a localized card text and automatically adjust its font size
        /// based on the character length of the translated string, ignoring rich text tags.
        /// </summary>
        public static void SetLocalizedCardText(TMPro.TextMeshProUGUI tmp, string rawText)
        {
            if (tmp == null) return;
            tmp.text = rawText;

            var cache = tmp.GetComponent<TextSizeCache>();
            if (cache == null)
            {
                cache = tmp.gameObject.AddComponent<TextSizeCache>();
                cache.originalFontSize = tmp.fontSize;
            }

            // Strip rich text/sprite tags
            string cleanText = System.Text.RegularExpressions.Regex.Replace(rawText, "<.*?>", "");
            int cleanLength = cleanText.Trim().Length;

            // Reference length is 16 for cards
            int referenceLength = 16;
            if (cleanLength > referenceLength)
            {
                float scale = UnityEngine.Mathf.Clamp((float)referenceLength / cleanLength, 0.5f, 1f);
                tmp.fontSize = cache.originalFontSize * scale;
            }
            else
            {
                tmp.fontSize = cache.originalFontSize;
            }
        }
    }

    /// <summary>
    /// Simple utility component to cache the original font size of a TMPro component.
    /// </summary>
    public class TextSizeCache : MonoBehaviour
    {
        public float originalFontSize;
    }
}
