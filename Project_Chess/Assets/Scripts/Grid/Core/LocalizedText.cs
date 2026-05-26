using TMPro;
using UnityEngine;

namespace AlperKocasalih.Chess.Grid
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class LocalizedText : MonoBehaviour
    {
        [Header("Localization Setting")]
        [SerializeField] private string localizationKey;

        private TextMeshProUGUI textMeshPro;
        private float originalFontSize;
        private bool hasOriginalFontSize = false;
        private int originalTextLength = 12;

        private void Awake()
        {
            textMeshPro = GetComponent<TextMeshProUGUI>();
            if (textMeshPro != null)
            {
                originalFontSize = textMeshPro.fontSize;
                hasOriginalFontSize = true;
                if (!string.IsNullOrEmpty(textMeshPro.text))
                {
                    // Cache the original designed text length as the layout reference
                    originalTextLength = textMeshPro.text.Trim().Length;
                }
            }
            
            // Fallback: Use the default text in TMPro as the key if none is specified
            if (string.IsNullOrEmpty(localizationKey) && textMeshPro != null)
            {
                localizationKey = textMeshPro.text.Trim();
            }
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += UpdateText;
            UpdateText(); // Set text immediately on enable
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= UpdateText;
        }

        /// <summary>
        /// Translates and updates the text mesh pro component dynamically.
        /// </summary>
        public void UpdateText()
        {
            if (textMeshPro != null && !string.IsNullOrEmpty(localizationKey))
            {
                string translatedText = LocalizationManager.GetTranslation(localizationKey);
                textMeshPro.text = translatedText;

                if (hasOriginalFontSize)
                {
                    // Strip rich text/sprite tags to get actual text character length
                    string cleanText = System.Text.RegularExpressions.Regex.Replace(translatedText, "<.*?>", "");
                    int cleanTranslatedLength = cleanText.Trim().Length;

                    // Reference threshold is at least 12 or the original designed text length
                    int referenceLength = Mathf.Max(12, originalTextLength);

                    if (cleanTranslatedLength > referenceLength)
                    {
                        // Scale ratio = referenceLength / cleanTranslatedLength, minimum clamp 50%
                        float scale = Mathf.Clamp((float)referenceLength / cleanTranslatedLength, 0.5f, 1f);
                        textMeshPro.fontSize = originalFontSize * scale;
                    }
                    else
                    {
                        textMeshPro.fontSize = originalFontSize;
                    }
                }
            }
        }

        /// <summary>
        /// Allows changing the localization key dynamically via code.
        /// </summary>
        public void SetKey(string newKey)
        {
            localizationKey = newKey;
            UpdateText();
        }
    }
}
