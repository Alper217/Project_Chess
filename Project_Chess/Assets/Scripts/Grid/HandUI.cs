using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace AlperKocasalih.Chess.Grid
{
    public class HandUI : MonoBehaviour
    {
        public static HandUI Instance { get; private set; }

        #region Fields

        [Header("References")]
        [SerializeField] private GameObject handCardPrefab;
        [SerializeField] private Transform handParent;

        [Header("Layout Settings")]
        [SerializeField] private float radius = 500f;
        [SerializeField] private float angleStep = 10f;
        [SerializeField] private float yOffset = -450f;

        private List<HandCard> spawnedCards = new List<HandCard>();

        #endregion

        #region Unity Methods

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            if (DraftManager.Instance != null)
            {
                DraftManager.Instance.OnHandUpdated += OnHandUpdated;
                DraftManager.Instance.OnDraftTurnChanged += (playerID) => RefreshHand(DraftManager.Instance.GetHand(playerID));
            }

            if (TurnManager.Instance != null)
            {
                // Refresh hand when turn changes
                TurnManager.Instance.OnTurnChanged += (playerID) => {
                    if (DraftManager.Instance != null)
                        RefreshHand(DraftManager.Instance.GetHand(playerID));
                };
            }
        }

        private void OnHandUpdated(int playerID, List<CardData> hand)
        {
            // If the updated hand belongs to the current drafting player (or the active player in action phase)
            // Update the UI
            int activeID = 1;
            if (DraftManager.Instance != null && GameManager.Instance.CurrentState == GameState.DraftPhase)
            {
                // During draft, we might want to only show the CURRENT drafting player's hand?
                // Or maybe the hand of the player who received the card.
                // Let's assume for now we show the hand of the current player.
                // activeID = DraftManager.Instance.DraftingPlayerID; // Need to expose this property
                // But for simplicity, let's just refresh if it belongs to playerID = draftingPlayerID
            }
            
            RefreshHand(hand); // Simplified for local multiplayer
        }

        public void OnCardClicked(HandCard card)
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.ActionPhase)
            {
                Debug.Log("HandUI: Cards can only be used in Action Phase.");
                return;
            }

            if (TurnManager.Instance == null) return;

            // Optional: Check if card belongs to the active player
            // In local multiplayer, we assume the person clicking is the active player 
            // but we can add a check if HandCard stores playerID

            if (PawnMovementManager.Instance != null)
            {
                PawnMovementManager.Instance.SelectMovementCard(card.CardData);
            }
        }

        #endregion

        #region Hand Management

        public void RefreshHand(List<CardData> hand)
        {
            // Clear existing
            foreach (var card in spawnedCards) 
            {
                if (card != null)
                {
                    card.transform.DOKill();
                    Destroy(card.gameObject);
                }
            }
            spawnedCards.Clear();

            int count = hand.Count;
            float startAngle = -(count - 1) * angleStep / 2f;

            for (int i = 0; i < count; i++)
            {
                GameObject cardObj = Instantiate(handCardPrefab, handParent);
                HandCard handCard = cardObj.GetComponent<HandCard>();
                
                handCard.Setup(hand[i]);
                
                // Calculate position on arc
                float angle = startAngle + (i * angleStep);
                float x = Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
                float y = Mathf.Cos(angle * Mathf.Deg2Rad) * radius + yOffset;
                
                Vector3 pos = new Vector3(x, y, 0);
                Quaternion rot = Quaternion.Euler(0, 0, -angle);
                
                cardObj.transform.localPosition = pos;
                cardObj.transform.localRotation = rot;
                
                handCard.SetOriginalState(pos, i);
                spawnedCards.Add(handCard);
            }
        }

        #endregion
    }
}
