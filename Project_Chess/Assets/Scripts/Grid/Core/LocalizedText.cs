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

        private void Awake()
        {
            textMeshPro = GetComponent<TextMeshProUGUI>();
            
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
                textMeshPro.text = LocalizationManager.GetTranslation(localizationKey);
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
