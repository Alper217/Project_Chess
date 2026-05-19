using System;
using AlperKocasalih.Chess.Grid;
using UnityEngine;
using TMPro;

public class TooltipManager : MonoBehaviour
{ 
    public static TooltipManager instance;
    
    [Header("UI References")]
    public GameObject TooltipWindow;
    public TextMeshProUGUI nameText; 
    public TextMeshProUGUI descriptionText;
    
    [Header("Settings")]
    public Vector2 mouseOffset = new Vector2(15f, -15f);

    private void Awake()
    {
        if (instance == null) instance = this;
        HideTooltip();
    }

    private void Update()
    {
        if (TooltipWindow.activeSelf)
        {
            TooltipWindow.transform.position =  (Vector2)Input.mousePosition + mouseOffset;
        }
    }

    public void ShowTooltip(string name, string description)
    {
        nameText.text = name;
        descriptionText.text = description;
        TooltipWindow.SetActive(true); 

        // Get the actual width of the tooltip window (e.g. 200) to feed into TMPro wrap calculations
        float targetWidth = 200f;
        if (TooltipWindow.transform is RectTransform parentRect)
        {
            targetWidth = parentRect.rect.width;
        }

        // 1. Force the text RectTransforms to match the target width first
        nameText.rectTransform.sizeDelta = new Vector2(targetWidth, nameText.rectTransform.sizeDelta.y);
        descriptionText.rectTransform.sizeDelta = new Vector2(targetWidth, descriptionText.rectTransform.sizeDelta.y);

        // 2. Force TMPro to wrap the text and generate geometry at this specific width
        nameText.ForceMeshUpdate();
        descriptionText.ForceMeshUpdate();

        // 3. Set the height of the RectTransforms to the newly calculated correct preferred heights
        nameText.rectTransform.sizeDelta = new Vector2(targetWidth, nameText.preferredHeight);
        descriptionText.rectTransform.sizeDelta = new Vector2(targetWidth, descriptionText.preferredHeight);

        // 4. Force the tooltip panel and its layout components to resize instantly
        if (TooltipWindow.transform is RectTransform rectTransform)
        {
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }
    }
    
    public void HideTooltip()
    {
        TooltipWindow.SetActive(false);
    }
}
