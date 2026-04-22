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
        [SerializeField] private Image cardImage; // The ONLY background image (the full design)

        [Header("Animation Settings")]
        [SerializeField] private float hoverScale = 1.8f;
        [SerializeField] private float hoverMoveY = 120f;
        [SerializeField] private float animationDuration = 0.25f;

        private bool isHovered = false;
        private bool isSelected = false; // NEW: Selection state
        public bool IsSelected => isSelected;

        private Vector3 layoutPosition; // Controlled by HandUI
        private Vector3 layoutRotation; // Controlled by HandUI
        private float hoverYOffset;     // Controlled by HandCard
        
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
            if (cardNameText != null) cardNameText.text = data.GetBuffsText();
            if (cardImage != null && data.cardDesign != null) cardImage.sprite = data.cardDesign;
            else if (cardImage != null) cardImage.sprite = data.cardSprite; // Fallback
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
            
            // Animate layout targets
            DOTween.To(() => layoutPosition, x => layoutPosition = x, newPos, 0.4f).SetEase(Ease.OutCubic).SetId(this + "layout");
            DOTween.To(() => layoutRotation, x => layoutRotation = x, newRot, 0.4f).SetEase(Ease.OutCubic).SetId(this + "layout");
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            // No need for immediate animation here, Update() will handle the scale/pos
        }

        private void Update()
        {
            // Combine layout from HandUI + Hover from Card logic
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
            if (cardImage != null)
            {
                cardImage.color = isLocked ? new Color(1f, 0.4f, 0.4f, 1f) : Color.white;
            }
        }

        public void SetInteractionState(bool interactive)
        {
            CanvasGroup cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            
            cg.alpha = interactive ? 1f : 0.4f;
            cg.blocksRaycasts = interactive;
        }

        #endregion

        #region Interaction

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isSelected) return; // Don't hover if already selected
            isHovered = true;
            transform.SetAsLastSibling();

            float scale = HandUI.Instance != null ? HandUI.Instance.GlobalHoverScale : hoverScale;
            float moveY = HandUI.Instance != null ? HandUI.Instance.GlobalHoverMoveY : hoverMoveY;
            float duration = HandUI.Instance != null ? HandUI.Instance.GlobalHoverDuration : animationDuration;

            DOTween.To(() => hoverYOffset, x => hoverYOffset = x, moveY, duration).SetEase(Ease.OutCubic);
            // Scale handled in Update
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            if (!isSelected) transform.SetSiblingIndex(originalSiblingIndex);

            float duration = HandUI.Instance != null ? HandUI.Instance.GlobalHoverDuration : animationDuration;

            DOTween.To(() => hoverYOffset, x => hoverYOffset = x, 0f, duration).SetEase(Ease.OutCubic);
            // Scale handled in Update
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (HandUI.Instance != null)
            {
                HandUI.Instance.OnCardClicked(this);
            }
        }

        private void OnDestroy()
        {
            transform.DOKill();
        }

        #endregion
    }
}
