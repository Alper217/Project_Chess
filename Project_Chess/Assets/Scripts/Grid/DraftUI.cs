using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;
using Unity.Netcode;

namespace AlperKocasalih.Chess.Grid
{
    public class DraftUI : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private CanvasGroup draftPanel;
        [SerializeField] private TextMeshProUGUI turnStatusText;

        [Header("Card Slots")]
        [SerializeField] private GameObject[] cardSlots; // Should have 3
        [SerializeField] private TextMeshProUGUI[] cardNameTexts;
        [SerializeField] private Image[] cardImages;

        [Header("Choice Buttons Parent")]
        [SerializeField] private GameObject choicePanel;
        [SerializeField] private Button keepButton;
        [SerializeField] private Button giveButton;
        [SerializeField] private Button burnButton;
        
        private int currentPendingCardIndex = -1;

        #region Unity Methods

        private void Start()
        {
            if (DraftManager.Instance != null)
            {
                DraftManager.Instance.OnCardsDrawn += UpdateDraftUI;
                DraftManager.Instance.OnDraftTurnChanged += UpdateTurnStatus;
                DraftManager.Instance.OnUsedActionsChanged += UpdateActionButtons;
                DraftManager.Instance.OnDraftFinished += HideDraftUI;
            }

            // Initially hide the Draft UI
            if (draftPanel != null)
            {
                draftPanel.alpha = 0;
                draftPanel.gameObject.SetActive(false);
            }

            if (choicePanel != null) choicePanel.SetActive(false);
        }

        #endregion

        #region Draft UI Logic

        public void ShowDraftUI()
        {
            if (draftPanel != null)
            {
                draftPanel.gameObject.SetActive(true);
                draftPanel.DOFade(1, 0.5f);
            }
        }

        private void HideDraftUI()
        {
            if (draftPanel != null)
            {
                draftPanel.DOFade(0, 0.5f).OnComplete(() => draftPanel.gameObject.SetActive(false));
            }
        }

        private void UpdateDraftUI(int playerID, List<CardData> cards)
        {
            ShowDraftUI();

            int localPlayerID = 1;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                localPlayerID = NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;
            }

            bool isMyTurn = (localPlayerID == playerID);

            // Disable all slots first
            foreach (var slot in cardSlots) slot.SetActive(false);

            if (isMyTurn)
            {
                for (int i = 0; i < cards.Count; i++)
                {
                    if (i >= cardSlots.Length) break;

                    cardSlots[i].SetActive(true);
                    if (cardNameTexts.Length > i) cardNameTexts[i].text = cards[i].cardName;
                    if (cardImages.Length > i) cardImages[i].sprite = cards[i].cardSprite;
                    
                    // Add simple animation
                    cardSlots[i].transform.DOPunchScale(Vector3.one * 0.1f, 0.2f);
                }
            }
            
            if (choicePanel != null) choicePanel.SetActive(false);
        }

        private void UpdateTurnStatus(int playerID)
        {
            int localPlayerID = 1;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                localPlayerID = NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;
            }

            if (turnStatusText != null)
            {
                if (localPlayerID == playerID)
                {
                    turnStatusText.text = "Senin S\u0131ran - Kart Se\u00E7";
                    turnStatusText.color = Color.green;
                }
                else
                {
                    turnStatusText.text = "Rakibin S\u0131ras\u0131...";
                    turnStatusText.color = Color.gray;
                }
            }
        }

        /// <summary>
        /// Called when a card slot is clicked.
        /// </summary>
        public void OnCardClicked(int index)
        {
            Debug.Log($"DraftUI: Card slot {index} clicked.");
            currentPendingCardIndex = index;
            if (choicePanel != null) choicePanel.SetActive(true);
        }

        /// <summary>
        /// Called from Buttons (Keep, Give, Burn).
        /// </summary>
        public void SelectAction(int actionInt)
        {
            DraftAction action = (DraftAction)actionInt;
            Debug.Log($"DraftUI: Action {action} selected for card index {currentPendingCardIndex}.");
            
            if (DraftManager.Instance != null && currentPendingCardIndex != -1)
            {
                DraftManager.Instance.HandleChoiceServerRpc(currentPendingCardIndex, action);
                currentPendingCardIndex = -1;
                if (choicePanel != null) choicePanel.SetActive(false);
            }
        }

        private void UpdateActionButtons(HashSet<DraftAction> usedActions)
        {
            if (keepButton != null) keepButton.interactable = !usedActions.Contains(DraftAction.Keep);
            if (giveButton != null) giveButton.interactable = !usedActions.Contains(DraftAction.Give);
            if (burnButton != null) burnButton.interactable = !usedActions.Contains(DraftAction.Burn);
        }

        #endregion
    }
}
