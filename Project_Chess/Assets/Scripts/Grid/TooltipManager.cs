using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager instance;

    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Vector2 offset = new Vector2(20, 20);

    private RectTransform rectTransform;

    private void Awake()
    {
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }

        if (tooltipPanel != null)
        {
            rectTransform = tooltipPanel.GetComponent<RectTransform>();
            HideTooltip();
        }
    }

    public void ShowTooltip(string header, string description)
    {
        if (tooltipPanel == null) return;
        tooltipPanel.SetActive(true);
        if (headerText != null) headerText.text = header;
        if (descriptionText != null) descriptionText.text = description;
        UpdatePosition();
    }

    public void HideTooltip()
    {
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }

    private void Update()
    {
        if (tooltipPanel != null && tooltipPanel.activeSelf)
        {
            UpdatePosition();
        }
    }

    private void UpdatePosition()
    {
        if (rectTransform == null) return;
        Vector2 mousePos = Input.mousePosition;
        float pivotX = mousePos.x + rectTransform.sizeDelta.x > Screen.width ? 1 : 0;
        float pivotY = mousePos.y + rectTransform.sizeDelta.y > Screen.height ? 1 : 0;
        rectTransform.pivot = new Vector2(pivotX, pivotY);
        tooltipPanel.transform.position = mousePos + (new Vector2(pivotX == 1 ? -offset.x : offset.x, pivotY == 1 ? -offset.y : offset.y));
    }
}
