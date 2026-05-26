using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HealthUIManager : MonoBehaviour
{
    public static HealthUIManager Instance;

    public GameObject healthCanvas;
    public Slider healthSlider;
    
    [Header("Buff/Debuff Texts")]
    public TextMeshProUGUI buffText;
    public TextMeshProUGUI debuffText;
    
    [Header("Single Combined Panel")]
    [Tooltip("Hem Buff hem Debuff metinlerini içinde barındıran tek ana panel")]
    public RectTransform infoPanel; 

    [Header("Main Layout Container")]
    public RectTransform mainLayoutContainer;

    [Header("Settings")]
    public float heightOffsetMultiplier = 2f;
    public float zOffset = 10f;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI pawnNameText;

    private float originalBuffFontSize;
    private float originalDebuffFontSize;
    private bool hasOriginalFontSizes = false;

    private void Awake()
    {
        Instance = this;
        if (healthCanvas != null)
        {
            healthCanvas.SetActive(false);
            CanvasGroup group = healthCanvas.GetComponent<CanvasGroup>();
            if (group == null) group = healthCanvas.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        if (buffText != null) originalBuffFontSize = buffText.fontSize;
        if (debuffText != null) originalDebuffFontSize = debuffText.fontSize;
        hasOriginalFontSizes = true;
    }

    private int GetCleanLength(string rawText)
    {
        if (string.IsNullOrEmpty(rawText)) return 0;
        // Remove HTML/sprite tags to get actual text length
        string clean = System.Text.RegularExpressions.Regex.Replace(rawText, "<.*?>", "");
        return clean.Trim().Length;
    }

    public void ShowHealthBar(Transform target, int current, int max, string pawnName, string buffs = "", string debuffs = "")
    {
        if (pawnNameText != null) pawnNameText.text = AlperKocasalih.Chess.Grid.LocalizationManager.GetTranslation(pawnName);
        if (healthSlider != null) { healthSlider.maxValue = max; healthSlider.value = current; }
        if (healthText != null) healthText.text = $"{current} / {max}";

        // 1. Metinleri güncelle
        string bText = AlperKocasalih.Chess.Grid.LocalizationManager.LocalizeSummary(buffs);
        string dText = AlperKocasalih.Chess.Grid.LocalizationManager.LocalizeSummary(debuffs);

        if (buffText != null) {
            string finalBText = string.IsNullOrWhiteSpace(bText) ? AlperKocasalih.Chess.Grid.LocalizationManager.GetTranslation("No buffs") : bText;
            buffText.text = finalBText;
            buffText.enableWordWrapping = false;
            
            if (hasOriginalFontSizes)
            {
                int len = GetCleanLength(finalBText);
                if (len > 12)
                {
                    float scale = Mathf.Clamp(12f / len, 0.6f, 1f);
                    buffText.fontSize = originalBuffFontSize * scale;
                }
                else
                {
                    buffText.fontSize = originalBuffFontSize;
                }
            }
        }
        if (debuffText != null) {
            string finalDText = string.IsNullOrWhiteSpace(dText) ? AlperKocasalih.Chess.Grid.LocalizationManager.GetTranslation("No debuffs") : dText;
            debuffText.text = finalDText;
            debuffText.enableWordWrapping = false;

            if (hasOriginalFontSizes)
            {
                int len = GetCleanLength(finalDText);
                if (len > 12)
                {
                    float scale = Mathf.Clamp(12f / len, 0.6f, 1f);
                    debuffText.fontSize = originalDebuffFontSize * scale;
                }
                else
                {
                    debuffText.fontSize = originalDebuffFontSize;
                }
            }
        }

        // 2. Paneli en uzun metne göre boyutlandır
        ResizePanelToLongestText();

        // 3. Ana yapıyı güncelle
        if (mainLayoutContainer != null)
        {
            mainLayoutContainer.localScale = Vector3.one;
            LayoutRebuilder.ForceRebuildLayoutImmediate(mainLayoutContainer);
            float totalNeededWidth = LayoutUtility.GetPreferredWidth(mainLayoutContainer);
            mainLayoutContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, totalNeededWidth);
            LayoutRebuilder.ForceRebuildLayoutImmediate(mainLayoutContainer);
        }

        if (healthCanvas != null)
        {
            healthCanvas.SetActive(true);
            currentTarget = target;
            UpdateHealthCanvasPosition();
        }
    }

    private void ResizePanelToLongestText()
    {
        if (infoPanel == null) return;
        infoPanel.localScale = Vector3.one;

        float maxPreferredWidth = 0;

        // Buff metninin ihtiyacını ölç
        if (buffText != null) {
            maxPreferredWidth = Mathf.Max(maxPreferredWidth, buffText.GetPreferredValues(buffText.text, 0, 0).x);
        }

        // Debuff metninin ihtiyacını ölç (Hangisi daha büyükse o kalsın)
        if (debuffText != null) {
            maxPreferredWidth = Mathf.Max(maxPreferredWidth, debuffText.GetPreferredValues(debuffText.text, 0, 0).x);
        }

        float targetWidth = maxPreferredWidth + 50f; // Extra padding

        // Paneli güncelle
        LayoutElement le = infoPanel.GetComponent<LayoutElement>();
        if (le != null) le.preferredWidth = targetWidth;
        else infoPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);

        LayoutRebuilder.ForceRebuildLayoutImmediate(infoPanel);
    }

    private Transform currentTarget;

    public void HideHealthBar()
    {
        currentTarget = null;
        if (healthCanvas != null) healthCanvas.SetActive(false);
    }

    private void LateUpdate()
    {
        UpdateHealthCanvasPosition();
    }

    private void UpdateHealthCanvasPosition()
    {
        if (currentTarget == null)
        {
            if (healthCanvas != null && healthCanvas.activeSelf) HideHealthBar();
            return;
        }

        if (healthCanvas != null && healthCanvas.activeSelf)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                Vector3 dirToCam = (mainCam.transform.position - healthCanvas.transform.position).normalized;
                healthCanvas.transform.position = currentTarget.position 
                                                  + (mainCam.transform.up * heightOffsetMultiplier) 
                                                  + (dirToCam * zOffset);
                healthCanvas.transform.rotation = mainCam.transform.rotation;
            }
        }
    }
}
