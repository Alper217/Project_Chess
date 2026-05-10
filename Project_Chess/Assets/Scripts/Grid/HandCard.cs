using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace AlperKocasalih.Chess.Grid
{
    public class HandCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        #region Fields

        private CardData cardData;
        public CardData CardData => cardData;
        private int handIndex;
        public int HandIndex => handIndex;

        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI cardNameText;
        [SerializeField] private Image cardImage;
        [SerializeField] private Transform iconContainer;
        [SerializeField] private GameObject iconPrefab;

        [Header("Animation Settings")]
        [SerializeField] private float hoverScale = 1.8f;
        [SerializeField] private float hoverMoveY = 120f;
        [SerializeField] private float animationDuration = 0.25f;

        private bool isHovered = false;
        private bool isSelected = false;
        public bool IsSelected => isSelected;

        private Vector3 layoutPosition;
        private Vector3 layoutRotation;
        private float hoverYOffset;
        
        private Vector3 originalScale;
        private int originalSiblingIndex;
        private RectTransform rectTransform;

        #endregion

        #region Unity Methods

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            originalScale = rectTransform.localScale;
        }

        #endregion

        #region Public Methods

        public void Setup(CardData data)
        {
            this.cardData = data;
            
            // Metni gizle ve tiklanabilirligini kapat (Ikonlara engel olmasin)
            if (cardNameText != null) 
            {
                cardNameText.text = "";
                cardNameText.raycastTarget = false;
            }

            // Eski ikonlari temizle
            if (iconContainer != null)
            {
                // Ensure icons are always in front of everything else on the card
                foreach (Transform child in iconContainer) Destroy(child.gameObject);
                iconContainer.SetAsLastSibling();

                // Yeni ikonlari ekle
                if (data.runtimeBuffs != null)
                {
                    foreach (var buff in data.runtimeBuffs)
                    {
                        if (buff != null && buff.effectIcon != null && iconPrefab != null)
                        {
                            GameObject iconObj = Instantiate(iconPrefab, iconContainer);
                            Image img = iconObj.GetComponent<Image>();
                            if (img != null) img.sprite = buff.effectIcon;
                            
                            BuffTooltipTrigger trigger = iconObj.GetComponent<BuffTooltipTrigger>();
                            if (trigger == null) trigger = iconObj.AddComponent<BuffTooltipTrigger>();
                            trigger.SetData(buff);
                        }
                    }
                }
            }

            if (cardImage != null && data.cardDesign != null) cardImage.sprite = data.cardDesign;
            else if (cardImage != null) cardImage.sprite = data.cardSprite;
        }

        public void SetOriginalState(Vector3 pos, Vector3 rot, int siblingIndex)
        {
            layoutPosition = pos;
            layoutRotation = rot;
            originalSiblingIndex = siblingIndex;
            
            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
            rectTransform.localPosition = pos;
            rectTransform.localRotation = Quaternion.Euler(rot);
        }

        public void UpdateLayoutState(Vector3 newPos, Vector3 newRot, int siblingIndex)
        {
            originalSiblingIndex = siblingIndex;
            DOTween.To(() => layoutPosition, x => layoutPosition = x, newPos, 0.4f).SetEase(Ease.OutCubic).SetId(this + "layout");
            DOTween.To(() => layoutRotation, x => layoutRotation = x, newRot, 0.4f).SetEase(Ease.OutCubic).SetId(this + "layout");
        }

        public void PlaySpawnPop(float delay = 0f)
        {
            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
            rectTransform.localScale = Vector3.zero;
            Vector3 finalPos = layoutPosition;
            rectTransform.localPosition = finalPos + new Vector3(0, -30f, 0);

            Sequence popSeq = DOTween.Sequence();
            popSeq.AppendInterval(delay);
            popSeq.Append(rectTransform.DOLocalMove(finalPos, 0.4f).SetEase(Ease.OutCubic));
            popSeq.Join(rectTransform.DOScale(originalScale, 0.4f).SetEase(Ease.OutBack, 2.2f));
            popSeq.Play();
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
        }

        private void Update()
        {
            float selMoveY = HandUI.Instance != null ? HandUI.Instance.GlobalSelectedMoveY : 40f;
            float targetY = isSelected ? selMoveY : hoverYOffset; 
            
            rectTransform.localPosition = layoutPosition + new Vector3(0, targetY, 0);
            rectTransform.localRotation = Quaternion.Euler(layoutRotation);

            float selScale = HandUI.Instance != null ? HandUI.Instance.GlobalSelectedScale : 1.2f;
            float targetScale = isSelected ? selScale : (isHovered ? (HandUI.Instance != null ? HandUI.Instance.GlobalHoverScale : 1.8f) : 1.0f);
            rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, originalScale * targetScale, Time.deltaTime * 10f);
        }

        public void SetHandIndex(int index)
        {
            handIndex = index;
        }

        public void SetBurnLockedUI(bool isLocked)
        {
            if (cardImage != null) cardImage.color = isLocked ? new Color(1f, 0.4f, 0.4f, 1f) : Color.white;
        }

        public void SetInteractionState(bool interactive)
        {
            CanvasGroup cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            cg.alpha = interactive ? 1f : 0.8f;
            cg.blocksRaycasts = interactive;
            if (cardImage != null) cardImage.color = interactive ? Color.white : new Color(0.25f, 0.25f, 0.25f, 1f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isSelected) return;
            isHovered = true;
            transform.SetAsLastSibling();
            float moveY = HandUI.Instance != null ? HandUI.Instance.GlobalHoverMoveY : hoverMoveY;
            float duration = HandUI.Instance != null ? HandUI.Instance.GlobalHoverDuration : animationDuration;
            DOTween.To(() => hoverYOffset, x => hoverYOffset = x, moveY, duration).SetEase(Ease.OutCubic);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            if (!isSelected) transform.SetSiblingIndex(originalSiblingIndex);
            float duration = HandUI.Instance != null ? HandUI.Instance.GlobalHoverDuration : animationDuration;
            DOTween.To(() => hoverYOffset, x => hoverYOffset = x, 0f, duration).SetEase(Ease.OutCubic);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (HandUI.Instance != null) HandUI.Instance.OnCardClicked(this);
        }

        private void OnDestroy()
        {
            transform.DOKill();
        }

        #endregion
    }
}






