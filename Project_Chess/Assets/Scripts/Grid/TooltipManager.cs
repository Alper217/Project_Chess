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
    }
    
    public void HideTooltip()
    {
        TooltipWindow.SetActive(false);
    }
}
