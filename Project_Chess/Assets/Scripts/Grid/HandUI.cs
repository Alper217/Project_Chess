using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Unity.Netcode;

namespace AlperKocasalih.Chess.Grid
{
    public class HandUI : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
        {
            SetHandExpanded(true);
        }

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
        {
            SetHandExpanded(false);
        }

        public static HandUI Instance { get; private set; }

        #region Fields

        [Header("References")]
        [SerializeField] private GameObject handCardPrefab;
        [SerializeField] private Transform handParent;

        [Header("Layout Settings")]
        [SerializeField] private float radius = 400f; // Normal: 400
        [SerializeField] private float angleStep = 20f; // Normal: 20
        [SerializeField] private float expandedRadius = 1200f; // NEW: Hover Radius
        [SerializeField] private float expandedAngleStep = 10f; // NEW: Hover Angle
        [SerializeField] private float yOffsetNormal = -300f;
        [SerializeField] private float yOffsetExpanded = -260f;
        [SerializeField] private float yOffsetHidden = -350f;

        [Header("Global Hover Settings (Applied to Cards)")]
        public float GlobalHoverScale = 1.8f;
        public float GlobalHoverMoveY = 120f;
        public float GlobalHoverDuration = 0.3f;

        [Header("Global Selection Settings")]
        public float GlobalSelectedScale = 1.3f; // NEW: Scaling when selected
        public float GlobalSelectedMoveY = 80f;  // NEW: Lift when selected

        [Header("Audio")]
        [SerializeField] private AudioClip drawSound;
        [SerializeField] private AudioClip selectSound;
        [SerializeField] private AudioClip burnSound;

        private List<HandCard> spawnedCards = new List<HandCard>();
        private bool isBurnSelectionActive = false;
        private int pendingBurnCount = 0;
        private bool isHandVisible = true;
        private bool isHandExpanded = false;
        private bool isFusionSelectionActive = false;
        private List<HandCard> selectedFusionCards = new List<HandCard>();
        private float currentYOffset;

        private float lastClickTime = 0f;
        private const float CLICK_DEBOUNCE = 0.1f;
        private int lastCardCount = 0;

        #endregion

        #region Unity Methods

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            // Fix DOTween capacity warnings
            DOTween.SetTweensCapacity(800, 50);

            currentYOffset = yOffsetNormal;
            if (DraftManager.Instance != null)
            {
                DraftManager.Instance.OnHandUpdated += OnHandUpdated;
                DraftManager.Instance.OnOverflowBurnRequested += OnOverflowBurnRequested;

                // Sync initial count to prevent sound on first refresh if already has cards
                int localID = GetLocalPlayerID();
                lastCardCount = DraftManager.Instance.GetHand(localID).Count;
            }

            // Sync with PlayerInputController to hide hand during targeting
            if (PlayerInputController.Instance != null)
            {
                PlayerInputController.Instance.OnSelectionCancelled += () => SetHandVisibility(true);
            }
        }

        private void OnDestroy()
        {
            if (DraftManager.Instance != null)
            {
                DraftManager.Instance.OnHandUpdated -= OnHandUpdated;
                DraftManager.Instance.OnOverflowBurnRequested -= OnOverflowBurnRequested;
            }
        }

        private int GetLocalPlayerID()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                return NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;
            return 1;
        }

        private void OnHandUpdated(int playerID, List<CardData> hand)
        {
            int localPlayerID = GetLocalPlayerID();

            Debug.Log($"[HandUI TETİKLENDİ] Yenileme İstenen ID: {playerID} | Benim ID: {localPlayerID}");
            if (playerID == localPlayerID)
            {
                RefreshHand(hand);
            }
        }

        public void OnCardClicked(HandCard card)
        {
            if (Time.time - lastClickTime < CLICK_DEBOUNCE) return;
            lastClickTime = Time.time;

            if (PlayerInputController.Instance != null && PlayerInputController.Instance.IsMultiActionInProgress)
            {
                Debug.Log("HandUI: Cannot change card during a multi-action sequence.");
                return;
            }

            if (isBurnSelectionActive)
            {
                TryBurnSelectedCard(card);
                return;
            }

            if (isFusionSelectionActive)
            {
                ToggleFusionSelection(card);
                return;
            }

            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.ActionPhase)
            {
                Debug.Log("HandUI: Cards can only be used in Action Phase.");
                return;
            }

            if (TurnManager.Instance == null) return;

            if (PlayerInputController.Instance != null)
            {
                if (selectSound != null && AudioManager.instance != null)
                {
                    AudioManager.instance.PlaySfx(selectSound);
                }

                // Reset all other selections first
                foreach (var c in spawnedCards) c.SetSelected(false);
                card.SetSelected(true); // Mark current card as selected

                PlayerInputController.Instance.SelectMovementCard(card.CardData);
                // Push down/Hide hand when card is selected to clear the board
                SetHandVisibility(false);
            }
        }

        public void SetHandVisibility(bool visible)
        {
            isHandVisible = visible;
            
            // Force retract and reset selections if becoming visible again (ESC/Cancel fix)
            if (visible) 
            {
                isHandExpanded = false;
                
                // Only reset selections if we are NOT in the middle of a multi-action (Double Use)
                bool isMulti = PlayerInputController.Instance != null && PlayerInputController.Instance.IsMultiActionInProgress;
                if (!isMulti)
                {
                    foreach (var card in spawnedCards) card.SetSelected(false);
                }
            }
            
            float targetY = visible ? (isHandExpanded ? yOffsetExpanded : yOffsetNormal) : yOffsetHidden;
            
            handParent.DOLocalMoveY(targetY, 0.4f).SetEase(Ease.OutCubic);
            currentYOffset = targetY;

            // Immediately refresh layout to match the new visibility state (Normal Arc)
            if (visible) 
            {
                // Multi-action lockout check: Dim other cards if we are in a sequence
                bool isMulti = PlayerInputController.Instance != null && PlayerInputController.Instance.IsMultiActionInProgress;
                foreach (var c in spawnedCards)
                {
                    bool shouldBeInteractive = !isMulti || c.IsSelected;
                    c.SetInteractionState(shouldBeInteractive);
                }

                if (!isMulti) SetHandExpanded(false);
            }
        }

        public void SetHandExpanded(bool expanded)
        {
            if (!isHandVisible) return;
            isHandExpanded = expanded;
            float targetY = expanded ? yOffsetExpanded : yOffsetNormal;
            
            handParent.DOKill();
            handParent.DOLocalMoveY(targetY, 0.35f).SetEase(Ease.OutCubic);
            
            // Dynamic Radius and Angle
            float currentRadius = expanded ? expandedRadius : radius;
            float currentAngleStep = expanded ? expandedAngleStep : angleStep;

            // Arrange cards
            int count = spawnedCards.Count;
            float startAngle = -(count - 1) * currentAngleStep / 2f;

            for (int i = 0; i < count; i++)
            {
                if (spawnedCards[i] == null) continue;

                float angle = startAngle + (i * currentAngleStep);
                float x = Mathf.Sin(angle * Mathf.Deg2Rad) * currentRadius;
                float y = Mathf.Cos(angle * Mathf.Deg2Rad) * currentRadius;

                Vector3 targetPos = new Vector3(x, y, 0);
                Vector3 targetRot = new Vector3(0, 0, -angle);

                spawnedCards[i].UpdateLayoutState(targetPos, targetRot, i);
            }
            currentYOffset = targetY;
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

            // --- ÇAKIŞMA ÖNLEME ---
            // Sadece şu durumlarda ses çal:
            // 1. Kart sayısı arttıysa VE Draft (Seçim) aşamasında değilsek.
            // (Draft aşamasında 'Keep' sesi zaten SelectSound olarak çalıyor, HandUI susturulmalı.)
            bool isDrafting = DraftManager.Instance != null && DraftManager.Instance.IsDraftingActive;
            
            if (count > lastCardCount && !isDrafting && drawSound != null && AudioManager.instance != null)
            {
                AudioManager.instance.PlaySfx(drawSound);
            }
            lastCardCount = count;

            float startAngle = -(count - 1) * angleStep / 2f;

            // Reset parent position before spawning so pop positions are correct
            handParent.localPosition = new Vector3(0, isHandVisible ? (isHandExpanded ? yOffsetExpanded : yOffsetNormal) : yOffsetHidden, 0);

            for (int i = 0; i < count; i++)
            {
                GameObject cardObj = Instantiate(handCardPrefab, handParent);
                HandCard handCard = cardObj.GetComponent<HandCard>();
                
                handCard.Setup(hand[i]);

                // --- FIX: Remember selection if this card is part of a multi-action ---
                if (PlayerInputController.Instance != null && PlayerInputController.Instance.activeCardData == hand[i])
                {
                    handCard.SetSelected(true);
                }

                handCard.SetHandIndex(i);
                
                int localPlayerID = GetLocalPlayerID();
                bool isLocked = DraftManager.Instance != null && DraftManager.Instance.IsBurnLocked(localPlayerID, i);
                handCard.SetBurnLockedUI(isBurnSelectionActive && isLocked);
                
                // Calculate position on arc
                float angle = startAngle + (i * angleStep);
                float x = Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
                float y = Mathf.Cos(angle * Mathf.Deg2Rad) * radius; 
                
                Vector3 pos = new Vector3(x, y, 0);
                Vector3 rot = new Vector3(0, 0, -angle);
                
                // SetOriginalState first so PlaySpawnPop knows the target position
                handCard.SetOriginalState(pos, rot, i);

                // ── Staggered Pop-in Entrance (OutBack spring feel) ───────────────
                handCard.PlaySpawnPop(i * 0.07f);

                spawnedCards.Add(handCard);
            }

            // Re-apply current state if we are already hovered
            if (isHandExpanded) SetHandExpanded(true);
            else SetHandExpanded(false);
        }

        #endregion

        private void OnOverflowBurnRequested(int playerID, int burnCount)
        {
            int localPlayerID = GetLocalPlayerID();

            if (playerID != localPlayerID) return;

            pendingBurnCount = burnCount;
            isBurnSelectionActive = burnCount > 0;

            for (int i = 0; i < spawnedCards.Count; i++)
            {
                if (spawnedCards[i] != null)
                {
                    bool isLocked = DraftManager.Instance != null && DraftManager.Instance.IsBurnLocked(localPlayerID, i);
                    spawnedCards[i].SetBurnLockedUI(isBurnSelectionActive && isLocked);
                }
            }
        }

        private void TryBurnSelectedCard(HandCard card)
        {
            int localPlayerID = GetLocalPlayerID();

            if (DraftManager.Instance != null)
            {
                if (DraftManager.Instance.IsBurnLocked(localPlayerID, card.HandIndex))
                {
                    card.transform.DOShakePosition(0.2f, 10f, 20, 90f, false, true);
                    return;
                }

                // Play Burn Sound
                if (burnSound != null && AudioManager.instance != null)
                {
                    AudioManager.instance.PlaySfx(burnSound);
                }

                DraftManager.Instance.BurnOverflowCardAtIndexServerRpc(localPlayerID, card.HandIndex);
                isBurnSelectionActive = false; 
            }
        }

        public void StartFusionSelection()
        {
            if (DraftManager.Instance != null && !DraftManager.Instance.EnableFusionSystem) return;

            isFusionSelectionActive = true;
            selectedFusionCards.Clear();
            foreach (var card in spawnedCards) card.SetSelected(false);
            
            // Show some UI message maybe?
            Debug.Log("Fusion Selection Active: Select 3 cards to burn.");
        }

        private void ToggleFusionSelection(HandCard card)
        {
            if (selectedFusionCards.Contains(card))
            {
                selectedFusionCards.Remove(card);
                card.SetSelected(false);
            }
            else
            {
                if (selectedFusionCards.Count < 3)
                {
                    selectedFusionCards.Add(card);
                    card.SetSelected(true);
                    
                    // Optional: Play a selection or small burn sound for fusion pick
                }
            }

            if (selectedFusionCards.Count == 3)
            {
                if (FusionUI.Instance == null)
                {
                    Debug.LogError("HATA: Sahne üzerinde FusionUI scriptine sahip bir obje bulunamadı veya kapalı (Inactive)! FusionUI objesi sahnede aktif olmalı, sadece içindeki paneller kapalı durmalı.");
                }
                else
                {
                    // Trigger Fusion Confirmation UI
                    FusionUI.Instance.ShowConfirmation(selectedFusionCards);
                }
            }
        }

        public void CancelFusionSelection()
        {
            isFusionSelectionActive = false;
            foreach (var card in selectedFusionCards) card.SetSelected(false);
            selectedFusionCards.Clear();
        }
    }
}
