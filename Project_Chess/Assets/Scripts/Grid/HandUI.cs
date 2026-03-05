using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Unity.Netcode;

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
            }
        }

        private void OnHandUpdated(int playerID, List<CardData> hand)
        {
            int localPlayerID = 1;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                localPlayerID = NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;
            }

            // Only update the hand UI if the updated hand belongs to the local player
            if (playerID == localPlayerID)
            {
                RefreshHand(hand);
            }
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

            if (PlayerInputController.Instance != null)
            {
                PlayerInputController.Instance.SelectMovementCard(card.CardData);
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
