using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;
using Unity.Netcode;

namespace AlperKocasalih.Chess.Grid
{
    public class FusionUI : MonoBehaviour
    {
        public static FusionUI Instance { get; private set; }

        [Header("Interaction")]
        [SerializeField] private CanvasGroup mainCanvasGroup;

        [Header("Confirmation Panel")]
        [SerializeField] private GameObject confirmationPanel;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        [Header("Result Panel")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Image resultCardImage;
        [SerializeField] private TextMeshProUGUI resultCardBuffs;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button reRollButton;
        [SerializeField] private TextMeshProUGUI reRollCountText;

        [Header("Preview")]
        [SerializeField] private GameObject[] confirmationCardSlots;

        [Header("Animation")]
        [SerializeField] private Transform cardGraphic;
        [SerializeField] private float flipDuration = 0.6f;

        private List<HandCard> selectedCards = new List<HandCard>();
        private CardData currentResultCard;
        private int localPlayerID;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            if (mainCanvasGroup != null)
            {
                mainCanvasGroup.blocksRaycasts = false;
                mainCanvasGroup.interactable = false;
            }

            if (confirmationPanel) confirmationPanel.SetActive(false);
            if (resultPanel) resultPanel.SetActive(false);
        }

        private void Start()
        {
            // Removed automatic listeners to prevent double-triggering 
            // since buttons are manually assigned in the Inspector.
        }

        public void ShowConfirmation(List<HandCard> cards)
        {
            selectedCards = new List<HandCard>(cards);
            if (confirmationPanel) confirmationPanel.SetActive(true);
            transform.SetAsLastSibling();

            if (mainCanvasGroup != null)
            {
                mainCanvasGroup.blocksRaycasts = true;
                mainCanvasGroup.interactable = true;
            }

            // Populate preview images
            for (int i = 0; i < confirmationCardSlots.Length; i++)
            {
                if (confirmationCardSlots[i] != null)
                {
                    if (i < selectedCards.Count)
                    {
                        confirmationCardSlots[i].SetActive(true);
                        Image img = confirmationCardSlots[i].GetComponentInChildren<Image>();
                        if (img != null)
                        {
                            img.sprite = selectedCards[i].CardData.cardDesign != null 
                                ? selectedCards[i].CardData.cardDesign 
                                : selectedCards[i].CardData.cardSprite;
                        }

                        // NEW: Find text area for buffs and fill it
                        TextMeshProUGUI buffsTxt = confirmationCardSlots[i].GetComponentInChildren<TextMeshProUGUI>();
                        if (buffsTxt != null)
                        {
                            buffsTxt.text = selectedCards[i].CardData.GetBuffsText();
                        }
                    }
                    else
                    {
                        confirmationCardSlots[i].SetActive(false);
                    }
                }
            }
        }

        public void OnConfirmFusion()
        {
            if (confirmationPanel) confirmationPanel.SetActive(false);
            
            localPlayerID = GetLocalPlayerID();
            int[] indices = new int[selectedCards.Count];
            for (int i = 0; i < selectedCards.Count; i++)
            {
                indices[i] = selectedCards[i].HandIndex;
            }

            DraftManager.Instance.PerformFusionServerRpc(localPlayerID, indices);
            HandUI.Instance.CancelFusionSelection();
        }

        public void OnCancelFusion()
        {
            if (confirmationPanel) confirmationPanel.SetActive(false);
            
            if (mainCanvasGroup != null)
            {
                mainCanvasGroup.blocksRaycasts = false;
                mainCanvasGroup.interactable = false;
            }

            HandUI.Instance.CancelFusionSelection();
        }

        public void ShowResult(int playerID, ulong pawnNetID, CardData card)
        {
            if (playerID != GetLocalPlayerID()) return;

            currentResultCard = card;
            if (resultPanel) resultPanel.SetActive(true);
            transform.SetAsLastSibling();

            if (mainCanvasGroup != null)
            {
                mainCanvasGroup.blocksRaycasts = true;
                mainCanvasGroup.interactable = true;
            }
            
            // Set UI
            if (resultCardBuffs) resultCardBuffs.text = card.GetBuffsText();
            
            if (resultCardImage)
            {
                resultCardImage.sprite = card.cardDesign != null ? card.cardDesign : card.cardSprite;
            }

            UpdateReRollUI();

            // Animation: Start face-down (or rotated)
            if (cardGraphic)
            {
                cardGraphic.localRotation = Quaternion.Euler(0, 180, 0);
                cardGraphic.DORotate(Vector3.zero, flipDuration).SetEase(Ease.OutBack);
            }
        }

        private void UpdateReRollUI()
        {
            int rolls = DraftManager.Instance.GetReRolls(localPlayerID);
            if (reRollCountText) reRollCountText.text = rolls.ToString();
            if (reRollButton) reRollButton.interactable = (rolls > 0);
        }

        public void OnReRollFusion()
        {
            DraftManager.Instance.ReRollFusionServerRpc(localPlayerID);
        }

        public void OnAcceptResult()
        {
            DraftManager.Instance.AcceptFusionResultServerRpc(localPlayerID, DeckManager.Instance.GetCardIndex(currentResultCard));
            if (resultPanel) resultPanel.SetActive(false);
            
            if (mainCanvasGroup != null)
            {
                mainCanvasGroup.blocksRaycasts = false;
                mainCanvasGroup.interactable = false;
            }
        }

        private int GetLocalPlayerID()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                return NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;
            return 1;
        }
    }
}
