using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HealthUIManager : MonoBehaviour
{
    public static HealthUIManager Instance;

    public GameObject healthCanvas;
    public Slider healthSlider;
    public TextMeshProUGUI buffText;
    public TextMeshProUGUI debuffText;
    [Tooltip("Vertical offset for the hover UI relative to the pawn.")]
    public float heightOffsetMultiplier = 2f;
    public float zOffset = 10f;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI pawnNameText;

    private Transform currentTarget;

    private void Awake()
    {
        Instance = this;
        if (healthCanvas != null)
        {
            healthCanvas.SetActive(false);

            // Ensure Health UI does not block raycasts/clicks
            CanvasGroup group = healthCanvas.GetComponent<CanvasGroup>();
            if (group == null) group = healthCanvas.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            if (healthText == null)
            {
                healthText = healthCanvas.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }
    }

    public void ShowHealthBar(Transform target, int current, int max, string pawnName, string buffs = "", string debuffs = "")
    {
        currentTarget = target;

        if (pawnNameText != null)
        {
            pawnNameText.text = AlperKocasalih.Chess.Grid.LocalizationManager.GetTranslation(pawnName);
        }

        if (healthSlider != null)
        {
            healthSlider.maxValue = max;
            healthSlider.value = current;
        }

        if (healthText != null)
        {
            healthText.text = $"{current} / {max}";
        }

        if (buffText != null)
        {
            string localizedBuffs = AlperKocasalih.Chess.Grid.LocalizationManager.LocalizeSummary(buffs);
            buffText.text = string.IsNullOrWhiteSpace(localizedBuffs) 
                ? AlperKocasalih.Chess.Grid.LocalizationManager.GetTranslation("No buffs") 
                : localizedBuffs;
        }

        if (debuffText != null)
        {
            string localizedDebuffs = AlperKocasalih.Chess.Grid.LocalizationManager.LocalizeSummary(debuffs);
            debuffText.text = string.IsNullOrWhiteSpace(localizedDebuffs) 
                ? AlperKocasalih.Chess.Grid.LocalizationManager.GetTranslation("No debuffs") 
                : localizedDebuffs;
        }

        if (healthCanvas != null)
        {
            healthCanvas.SetActive(true);
            UpdateHealthCanvasPosition();
        }
    }

    public void HideHealthBar()
    {
        currentTarget = null;

        if (buffText != null)
        {
            buffText.text = string.Empty;
        }

        if (debuffText != null)
        {
            debuffText.text = string.Empty;
        }

        if (healthCanvas != null)
        {
            healthCanvas.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        UpdateHealthCanvasPosition();
    }

    private void UpdateHealthCanvasPosition()
    {
        if (currentTarget == null)
        {
            if (healthCanvas != null && healthCanvas.activeSelf)
            {
                HideHealthBar();
            }
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
            else
            {
                healthCanvas.transform.position = currentTarget.position + new Vector3(0f, heightOffsetMultiplier, 0f);
            }
        }
    }
}
