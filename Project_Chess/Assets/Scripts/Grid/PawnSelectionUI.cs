using System;
using AlperKocasalih.Chess.Grid;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class PawnSelectionUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public static  PawnSelectionUI instance;
    [Header("PawnSelection")]
    public int pawnTypeIndex;
    
    [Header("Tooltip Info")]
    public string pawnName;
    [TextArea] public string pawnDescription;
    
    [Header("UI Visuals")]
    private Image pawnIcon;
    [SerializeField]private GameObject highlightOutline;
    private Color disabledColor = Color.grey;
    private Color orgColor = Color.white;
    
    public bool isPlaced { get; private set; } = false;

    private void Start()
    {
        pawnIcon = GetComponent<Image>();
        if(isPlaced) return;
        SetSelected(false);
    }

    public void SetSelected(bool isSelected)
    {
        if(highlightOutline != null)
            highlightOutline.SetActive(isSelected);
    }

    public void MarkAsPlaced()
    {
        isPlaced = true;
        SetSelected(false);

        if (pawnIcon != null)
        {
            pawnIcon.color = disabledColor;
            pawnIcon.raycastTarget = false;
        }
    }

    public void ResetItem()
    {
        isPlaced = false;
        SetSelected(false);
        if (pawnIcon != null)
        {
            pawnIcon.color = orgColor;
            Debug.Log("PawnSelectionUI: ResetItem() called");
            Debug.Log($"IndexNumber: {pawnTypeIndex}");
            pawnIcon.raycastTarget = true;
        }
    }


    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isPlaced)  return;
        if(TooltipManager.instance != null)
        {
            TooltipManager.instance.ShowTooltip(pawnName, pawnDescription);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isPlaced)  return;
        if (TooltipManager.instance != null)
        {
            TooltipManager.instance.HideTooltip();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        PawnPlacementManager.Instance.SelectedPawn(this);
    }
}
