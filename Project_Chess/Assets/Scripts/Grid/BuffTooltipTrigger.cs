using UnityEngine;
using UnityEngine.EventSystems;
using AlperKocasalih.Chess.Grid;
using System.Collections.Generic;

public class BuffTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private string header;
    private string description;

    public void SetData(BuffData data)
    {
        if (data == null) return;
        header = string.IsNullOrEmpty(data.buffName) ? data.effectType.ToString() : data.buffName;
        string sign = data.amount > 0 ? "+" : "";
        string percent = data.isPercentage ? "%" : "";
        string valueText = data.amount != 0 ? $"({sign}{data.amount}{percent}) " : "";
        description = valueText + data.effectDescription;
        
        // Raycast garantisi
        UnityEngine.UI.Image img = GetComponent<UnityEngine.UI.Image>();
        if (img != null) img.raycastTarget = true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipManager.instance != null)
            TooltipManager.instance.ShowTooltip(header, description);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (TooltipManager.instance != null)
            TooltipManager.instance.HideTooltip();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Tiklamayi ikonun arkasindaki karta veya slot'a pasla
        ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, eventData, ExecuteEvents.pointerClickHandler);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current);
            eventDataCurrentPosition.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventDataCurrentPosition, results);
            if (results.Count > 0)
            {
                Debug.Log("--- UI RAYCAST INSPECTOR ---");
                foreach (var res in results)
                    Debug.Log($"Hit: {res.gameObject.name} (Sibling: {res.gameObject.transform.GetSiblingIndex()}) | Parent: {res.gameObject.transform.parent.name}");
            }
        }
    }

    private void OnDisable()
    {
        if (TooltipManager.instance != null)
            TooltipManager.instance.HideTooltip();
    }
}

