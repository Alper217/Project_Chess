using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AlperKocasalih.Chess.Grid;

public class HealthUIManager : MonoBehaviour
{
    public static HealthUIManager Instance;

    public GameObject healthCanvas;
    public Slider healthSlider;
    public Transform buffIconContainer;
    public Transform debuffIconContainer;
    public GameObject iconPrefab;

    [Tooltip("Vertical offset for the hover UI relative to the pawn.")]
    public float heightOffsetMultiplier = 2f;
    public float zOffset = 10f;
    public TextMeshProUGUI healthText;

    private Transform currentTarget;

    private void Awake()
    {
        Instance = this;
        if (healthCanvas != null)
        {
            healthCanvas.SetActive(false);
            CanvasGroup group = healthCanvas.GetComponent<CanvasGroup>();
            if (group == null) group = healthCanvas.AddComponent<CanvasGroup>();
            // Ikonlara hover yapabilmek icin artik raycastleri aciyoruz
            group.blocksRaycasts = true;
            group.interactable = true;
            if (healthText == null) healthText = healthCanvas.GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }

    public void ShowHealthBar(Transform target, int current, int max, System.Collections.Generic.List<ServerActiveBuff> activeBuffs = null)
    {
        currentTarget = target;
        if (healthSlider != null) { healthSlider.maxValue = max; healthSlider.value = current; }
        if (healthText != null) healthText.text = $"{current} / {max}";

        if (buffIconContainer != null) foreach (Transform child in buffIconContainer) Destroy(child.gameObject);
        if (debuffIconContainer != null) foreach (Transform child in debuffIconContainer) Destroy(child.gameObject);

        if (activeBuffs != null && iconPrefab != null)
        {
            foreach (var buff in activeBuffs)
            {
                if (buff == null || buff.buffData == null || buff.buffData.effectIcon == null) continue;
                Transform container = buff.buffData.isPositiveEffect ? buffIconContainer : debuffIconContainer;
                if (container != null)
                {
                    GameObject iconObj = Instantiate(iconPrefab, container);
                    Image img = iconObj.GetComponent<Image>();
                    if (img != null) img.sprite = buff.buffData.effectIcon;
                    BuffTooltipTrigger trigger = iconObj.GetComponent<BuffTooltipTrigger>();
                    if (trigger == null) trigger = iconObj.AddComponent<BuffTooltipTrigger>();
                    trigger.SetData(buff.buffData);
                }
            }
        }
        if (healthCanvas != null) { healthCanvas.SetActive(true); UpdateHealthCanvasPosition(); }
    }

    public void HideHealthBar()
    {
        currentTarget = null;
        if (buffIconContainer != null) foreach (Transform child in buffIconContainer) Destroy(child.gameObject);
        if (debuffIconContainer != null) foreach (Transform child in debuffIconContainer) Destroy(child.gameObject);
        if (healthCanvas != null) healthCanvas.SetActive(false);
    }

    private void LateUpdate() { UpdateHealthCanvasPosition(); }

    private void UpdateHealthCanvasPosition()
    {
        if (currentTarget == null) { if (healthCanvas != null && healthCanvas.activeSelf) HideHealthBar(); return; }
        if (healthCanvas != null && healthCanvas.activeSelf)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                Vector3 dirToCam = (mainCam.transform.position - healthCanvas.transform.position).normalized;
                healthCanvas.transform.position = currentTarget.position + (mainCam.transform.up * heightOffsetMultiplier) + (dirToCam * zOffset);
                healthCanvas.transform.rotation = mainCam.transform.rotation;
            }
            else healthCanvas.transform.position = currentTarget.position + new Vector3(0f, heightOffsetMultiplier, 0f);
        }
    }
}

