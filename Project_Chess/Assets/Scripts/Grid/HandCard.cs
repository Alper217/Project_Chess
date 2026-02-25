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

        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI cardNameText;
        [SerializeField] private Image cardImage;
        [SerializeField] private TextMeshProUGUI descriptionText;

        [Header("Animation Settings")]
        [SerializeField] private float hoverScale = 1.1f;
        [SerializeField] private float hoverMoveY = 30f;
        [SerializeField] private float animationDuration = 0.2f;

        private Vector3 originalPosition;
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
            if (cardNameText != null) cardNameText.text = data.cardName;
            if (cardImage != null) cardImage.sprite = data.cardSprite;
            if (descriptionText != null) descriptionText.text = data.description;
        }

        public void SetOriginalState(Vector3 pos, int siblingIndex)
        {
            originalPosition = pos;
            originalSiblingIndex = siblingIndex;
            rectTransform.localPosition = pos;
        }

        #endregion

        #region Interaction

        public void OnPointerEnter(PointerEventData eventData)
        {
            // Bring to front
            transform.SetAsLastSibling();

            // Animate
            rectTransform.DOComplete();
            rectTransform.DOScale(originalScale * hoverScale, animationDuration);
            rectTransform.DOLocalMoveY(originalPosition.y + hoverMoveY, animationDuration);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Restore sibling index
            transform.SetSiblingIndex(originalSiblingIndex);

            // Animate back
            rectTransform.DOComplete();
            rectTransform.DOScale(originalScale, animationDuration);
            rectTransform.DOLocalMove(originalPosition, animationDuration);
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
