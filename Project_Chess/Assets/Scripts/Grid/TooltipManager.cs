using System;
using AlperKocasalih.Chess.Grid;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TooltipManager : MonoBehaviour
{ 
    public static TooltipManager instance;
    
    [Header("UI References")]
    public GameObject TooltipWindow;
    public TextMeshProUGUI nameText; 
    public TextMeshProUGUI descriptionText;
    
    [Header("Settings")]
    public Vector2 mouseOffset = new Vector2(15f, -15f);

    private RectTransform rectTransform;
    private VerticalLayoutGroup layoutGroup;

    private void Awake()
    {
        if (instance == null) instance = this;
        rectTransform = TooltipWindow.GetComponent<RectTransform>();
        layoutGroup = TooltipWindow.GetComponent<VerticalLayoutGroup>();
        HideTooltip();
    }

    private void Update()
    {
        if (TooltipWindow.activeSelf)
        {
            TooltipWindow.transform.position = (Vector2)Input.mousePosition + mouseOffset;
        }
    }

    public void ShowTooltip(string name, string description)
    {
        // 1. Yazıları hazırla ve temizle
        nameText.text = name?.Trim();
        descriptionText.text = description?.Trim();
        
        bool hasDesc = !string.IsNullOrWhiteSpace(descriptionText.text);
        descriptionText.gameObject.SetActive(hasDesc);
        
        TooltipWindow.SetActive(true); 

        // 2. Content Size Fitter'ı devre dışı bırakıyoruz (Manuel hesaplama ile çakışır)
        ContentSizeFitter fitter = TooltipWindow.GetComponent<ContentSizeFitter>();
        if (fitter != null) fitter.enabled = false;

        // 3. Genişliği koru. Eğer genişlik çok küçükse dikey yazı olur.
        // Mevcut genişliği baz alıyoruz.
        float width = rectTransform.rect.width;
        if (width < 50) width = 250f;

        // 4. TMPro'nun o genişlikte ne kadar yüksekliğe ihtiyacı olduğunu bulalım
        // Bu metod mesh oluşturmadan en doğru yüksekliği döndürür.
        float nameHeight = nameText.GetPreferredValues(nameText.text, width, 0).y;
        float descHeight = hasDesc ? descriptionText.GetPreferredValues(descriptionText.text, width, 0).y : 0;

        // 5. Toplam yüksekliği hesapla (Padding + Spacing + Metinler)
        float totalHeight = 0;
        if (layoutGroup != null)
        {
            totalHeight += layoutGroup.padding.top + layoutGroup.padding.bottom;
            totalHeight += nameHeight;
            if (hasDesc)
            {
                totalHeight += layoutGroup.spacing;
                totalHeight += descHeight;
            }
        }
        else
        {
            totalHeight = nameHeight + descHeight + 20;
        }

        // 6. Boyutu uygula (Zorla set et)
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, totalHeight);
        
        // 7. İçerideki objeleri (metinleri) yerlerine dizmek için Layout Group'u tetikle
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
    }
    
    public void HideTooltip()
    {
        TooltipWindow.SetActive(false);
    }
}
