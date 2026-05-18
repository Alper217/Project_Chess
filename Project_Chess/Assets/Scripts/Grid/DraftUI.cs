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
        [SerializeField] private GameObject[] cardSlots;
        [SerializeField] private TextMeshProUGUI[] cardNameTexts;
        [SerializeField] private Image[] cardImages;
        [SerializeField] private Transform opponentCardSlot;
        [SerializeField] private Transform playerCardSlot;
        [SerializeField] private GameObject[] reRollButtons;
        [SerializeField] private TextMeshProUGUI[] reRollButtonTexts;

        // Cached initial transforms (set once in Start)
        private Vector3[]     slotInitialPositions;
        private Vector3[]     slotInitialScales;
        private Quaternion[]  slotInitialRotations;   // ← NEW: store initial rotation

        [Header("Choice Buttons Parent")]
        [SerializeField] private GameObject choicePanel;
        [SerializeField] private Button keepButton;
        [SerializeField] private Button giveButton;
        [SerializeField] private Button burnButton;

        [Header("Animation Settings")]
        [Tooltip("Delay between each card appearing (seconds)")]
        [SerializeField] private float cardStaggerDelay  = 0.06f;
        [Tooltip("Card entrance slide + scale duration")]
        [SerializeField] private float cardEnterDuration = 0.35f;
        [Tooltip("Keep / Give / Burn action duration")]
        [SerializeField] private float actionDuration    = 0.40f;
        [Tooltip("Arc height when throwing a card to a slot")]
        [SerializeField] private float arcJumpPower      = 100f;

        [Header("Audio")]
        [SerializeField] private AudioClip drawSound;
        [SerializeField] private AudioClip selectSound;
        [SerializeField] private AudioClip burnSound;

        private int  currentPendingCardIndex = -1;
        private bool isBurnBlockingDraft     = false;

        private float lastActionTime = 0f;
        private const float ACTION_DEBOUNCE = 0.15f;
        private int lastChoicesCount = 0;

        // ─────────────────────────────────────────────────────────────────────────
        #region Unity Methods

        private void Start()
        {
            if (DraftManager.Instance != null)
            {
                DraftManager.Instance.OnCardsDrawn           += UpdateDraftUI;
                DraftManager.Instance.OnDraftTurnChanged     += UpdateTurnStatus;
                DraftManager.Instance.OnUsedActionsChanged   += UpdateActionButtons;
                DraftManager.Instance.OnDraftFinished        += HideDraftUI;
                DraftManager.Instance.OnOverflowBurnRequested += HandleOverflowBurnRequested;
            }

            // Cache transforms ONCE so they are never altered by a previous animation
            slotInitialPositions = new Vector3[cardSlots.Length];
            slotInitialScales    = new Vector3[cardSlots.Length];
            slotInitialRotations = new Quaternion[cardSlots.Length];

            for (int i = 0; i < cardSlots.Length; i++)
            {
                slotInitialPositions[i] = cardSlots[i].transform.localPosition;
                slotInitialScales[i]    = cardSlots[i].transform.localScale;
                slotInitialRotations[i] = cardSlots[i].transform.localRotation;   // ← cache
            }

            if (draftPanel != null)
            {
                draftPanel.alpha = 0;
                draftPanel.gameObject.SetActive(false);
            }

            if (choicePanel != null) choicePanel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (DraftManager.Instance != null)
            {
                DraftManager.Instance.OnCardsDrawn           -= UpdateDraftUI;
                DraftManager.Instance.OnDraftTurnChanged     -= UpdateTurnStatus;
                DraftManager.Instance.OnUsedActionsChanged   -= UpdateActionButtons;
                DraftManager.Instance.OnDraftFinished        -= HideDraftUI;
                DraftManager.Instance.OnOverflowBurnRequested -= HandleOverflowBurnRequested;
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────────
        #region Draft UI Logic

        public void ShowDraftUI()
        {
            if (draftPanel == null) return;
            draftPanel.gameObject.SetActive(true);
            draftPanel.DOFade(1f, 0.4f).SetEase(Ease.OutCubic);
            draftPanel.blocksRaycasts = !isBurnBlockingDraft;
        }

        private void HideDraftUI()
        {
            if (TurnManager.Instance != null)
                TurnManager.Instance.RefreshTurnInfoUI();

            ResetCardScales();
            lastChoicesCount = 0; // Reset for next round

            if (draftPanel != null)
                draftPanel.DOFade(0f, 0.4f).SetEase(Ease.InCubic)
                          .OnComplete(() => draftPanel.gameObject.SetActive(false));
        }

        // ── Called when new cards are drawn ──────────────────────────────────────
        private void UpdateDraftUI(int playerID, List<CardData> cards)
        {
            ShowDraftUI();

            int  localPlayerID = GetLocalPlayerID();
            bool isMyTurn      = (localPlayerID == playerID);

            int rolls = DraftManager.Instance.GetReRolls(localPlayerID);

            foreach (var slot in cardSlots) slot.SetActive(false);
            
            for (int i = 0; i < reRollButtons.Length; i++)
            {
                if (reRollButtons[i] != null)
                {
                    bool show = isMyTurn && i < cards.Count && DraftManager.Instance.EnableReRollSystem;
                    reRollButtons[i].SetActive(show);
                    if (reRollButtonTexts.Length > i && reRollButtonTexts[i] != null)
                    {
                        reRollButtonTexts[i].text = rolls.ToString();
                    }
                }
            }

            if (!isMyTurn) { if (choicePanel != null) choicePanel.SetActive(false); return; }

            ResetCardScales();

            // ONLY play draw sound if we have MORE cards than before (new turn/round)
            // If count decreased, it means we just made a choice and the UI is refreshing.
            if (cards.Count > lastChoicesCount && drawSound != null && AudioManager.instance != null)
            {
                AudioManager.instance.PlaySfx(drawSound);
            }
            lastChoicesCount = cards.Count;

            for (int i = 0; i < cards.Count; i++)
            {
                if (i >= cardSlots.Length) break;

                GameObject slot = cardSlots[i];

                slot.transform.DOKill();
                slot.transform.localPosition = slotInitialPositions[i] + new Vector3(0f, -50f, 0f);
                slot.transform.localScale    = Vector3.zero;
                slot.transform.localRotation = slotInitialRotations[i];

                // Reset CanvasGroup alpha (from a previous Keep/Give fade)
                CanvasGroup slotCG = slot.GetComponent<CanvasGroup>();
                if (slotCG != null) slotCG.alpha = 1f;

                slot.SetActive(true);

                if (cardNameTexts.Length > i)
                    cardNameTexts[i].text = cards[i].GetBuffsText();

                if (cardImages.Length > i)
                {
                    cardImages[i].sprite = cards[i].cardDesign != null
                        ? cards[i].cardDesign
                        : cards[i].cardSprite;
                    cardImages[i].color = Color.white;
                }

                // ── Staggered Entrance: slide up + OutBack spring ─────────────────
                int   ci    = i;
                float delay = i * cardStaggerDelay;

                Sequence enterSeq = DOTween.Sequence();
                enterSeq.AppendInterval(delay);
                enterSeq.Append(slot.transform
                    .DOLocalMove(slotInitialPositions[ci], cardEnterDuration)
                    .SetEase(Ease.OutCubic));
                enterSeq.Join(slot.transform
                    .DOScale(slotInitialScales[ci], cardEnterDuration)
                    .SetEase(Ease.OutBack, 1.8f));
                enterSeq.Join(slot.transform
                    .DOLocalRotateQuaternion(slotInitialRotations[ci], cardEnterDuration)
                    .SetEase(Ease.OutCubic));
                enterSeq.Play();
            }

            if (choicePanel != null) choicePanel.SetActive(false);
        }

        public void OnReRollClicked(int index)
        {
            if (DraftManager.Instance != null)
            {
                DraftManager.Instance.ReRollDraftCardServerRpc(index);
            }
        }

        private void UpdateTurnStatus(int playerID)
        {
            int localPlayerID = GetLocalPlayerID();

            if (turnStatusText == null) return;

            if (localPlayerID == playerID)
            {
                turnStatusText.text  = "Senin Sıran";
            }
            else
            {
                turnStatusText.text  = "Rakibin Sırası";
            }
        }

        // ── Card slot clicked ─────────────────────────────────────────────────────
        public void OnCardClicked(int index)
        {
            if (isBurnBlockingDraft) return;

            // Reset previous selection scales
            ResetCardScales();

            currentPendingCardIndex = index;
            Transform t = cardSlots[index].transform;
            t.DOKill();

            // Highlight selection with a 10% scale up and "OutBack" spring feel
            Vector3 targetScale = slotInitialScales[index] * 1.1f;
            t.DOScale(targetScale, 0.25f).SetEase(Ease.OutBack);

            if (choicePanel != null) choicePanel.SetActive(true);
        }

        private void ResetCardScales()
        {
            for (int i = 0; i < cardSlots.Length; i++)
            {
                if (cardSlots[i] != null && cardSlots[i].activeSelf)
                {
                    cardSlots[i].transform.DOKill();
                    cardSlots[i].transform.DOScale(slotInitialScales[i], 0.2f).SetEase(Ease.OutCubic);
                }
            }
        }

        // ── Action buttons ────────────────────────────────────────────────────────
        public void SelectAction(int actionInt)
        {
            if (isBurnBlockingDraft) return;
            if (Time.time - lastActionTime < ACTION_DEBOUNCE) return;
            lastActionTime = Time.time;

            DraftAction action = (DraftAction)actionInt;

            if (currentPendingCardIndex < 0 || currentPendingCardIndex >= cardSlots.Length)
            {
                Debug.LogWarning("DraftUI: SelectAction called with invalid card index.");
                return;
            }

            // Play appropriate sound
            if (action == DraftAction.Burn)
            {
                if (burnSound != null && AudioManager.instance != null)
                    AudioManager.instance.PlaySfx(burnSound);
            }
            else
            {
                if (selectSound != null && AudioManager.instance != null)
                    AudioManager.instance.PlaySfx(selectSound);
            }

            int         capturedIndex = currentPendingCardIndex;
            GameObject  selectedCard  = cardSlots[capturedIndex];

            if (choicePanel != null) choicePanel.SetActive(false);
            currentPendingCardIndex = -1;

            switch (action)
            {
                case DraftAction.Keep:  PlayKeepAnimation(selectedCard, capturedIndex); break;
                case DraftAction.Give:  PlayGiveAnimation(selectedCard, capturedIndex); break;
                case DraftAction.Burn:  PlayBurnAnimation(selectedCard, capturedIndex); break;
            }

            Debug.Log($"DraftUI: Action {action} on card {capturedIndex}.");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // ── Keep: card sweeps toward player — magnetic pull + directional lean ──────
        private void PlayKeepAnimation(GameObject card, int cardIndex)
        {
            Transform t = card.transform;
            t.DOKill();

            CanvasGroup cg = card.GetComponent<CanvasGroup>();
            if (cg == null) cg = card.AddComponent<CanvasGroup>();
            cg.alpha = 1f;

            Vector3 baseScale = slotInitialScales[cardIndex];
            float   fly       = actionDuration;

            Sequence seq = DOTween.Sequence();

            // 1. Brief anticipation scale pop
            seq.Append(t.DOScale(baseScale * 1.15f, 0.08f).SetEase(Ease.OutQuad));

            // 2. Magnetic pull toward player slot (InCubic = accelerates, "sucked in")
            seq.Append(t.DOMove(playerCardSlot.position, fly).SetEase(Ease.InCubic));

            // 3. Directional lean: magnitude depends on slot position.
            //    Keep always leans POSITIVE (card tips backward, like being swept toward you).
            //    Center card gets 0°, left/right scale with distance from center.
            float keepLean = GetLeanMagnitude(cardIndex);
            seq.Join(t.DOLocalRotate(new Vector3(0f, 0f, keepLean), fly * 0.55f)
                      .SetEase(Ease.OutCubic));

            // 4. Progressive shrink — card feels like it’s flying into the distance
            seq.Join(t.DOScale(Vector3.zero, fly).SetEase(Ease.InQuad));

            // 5. Alpha fade starts after 30% of the flight
            seq.Join(cg.DOFade(0f, fly * 0.70f)
                       .SetDelay(fly * 0.30f)
                       .SetEase(Ease.InQuad));

            seq.OnComplete(() => SendActionToServer(cardIndex, DraftAction.Keep));
            seq.Play();
        }

        // ── Give: windup → deal to opponent with forward lean + shrink + fade ──────
        private void PlayGiveAnimation(GameObject card, int cardIndex)
        {
            Transform t = card.transform;
            t.DOKill();

            CanvasGroup cg = card.GetComponent<CanvasGroup>();
            if (cg == null) cg = card.AddComponent<CanvasGroup>();
            cg.alpha = 1f;

            float fly = actionDuration;

            Sequence seq = DOTween.Sequence();

            // Per-slot Give rotation — edit each value independently to test:
            float giveLean;
            if      (cardIndex == 0) giveLean = -18f;  // Sol  kart
            else if (cardIndex == 1) giveLean =   0f;  // Orta kart
            else                    giveLean = 25f;    // Sağ  kart  ← slot 2  (+25° = test edilen değer)

            float giveWindup = -giveLean * 0.45f;      // Windup: giveLean'in karşı yönü, yarı kuvvet
            seq.Append(t.DOLocalRotate(new Vector3(0f, 0f, giveWindup), 0.09f).SetEase(Ease.OutQuad));

            // 2. Deal motion: InQuad = card accelerates, feels like a deliberate push
            seq.Append(t.DOMove(opponentCardSlot.position, fly).SetEase(Ease.InQuad));

            // 3. Forward lean into travel direction — slot-aware angle, no full spin
            seq.Join(t.DOLocalRotate(new Vector3(0f, 0f, giveLean), fly * 0.65f)
                      .SetEase(Ease.OutCubic));

            // 4. Shrink starts after 20% of the throw
            seq.Join(t.DOScale(Vector3.zero, fly * 0.80f)
                       .SetDelay(fly * 0.20f)
                       .SetEase(Ease.InCubic));

            // 5. Fade starts after 20% of the throw
            seq.Join(cg.DOFade(0f, fly * 0.80f)
                       .SetDelay(fly * 0.20f)
                       .SetEase(Ease.InQuad));

            seq.OnComplete(() => SendActionToServer(cardIndex, DraftAction.Give));
            seq.Play();
        }

        // ── Burn: shake → red → implode ──────────────────────────────────────────
        private void PlayBurnAnimation(GameObject card, int cardIndex)
        {
            Transform t   = card.transform;
            Image     img = cardIndex < cardImages.Length ? cardImages[cardIndex] : null;
            t.DOKill();

            Sequence seq = DOTween.Sequence();

            // 1. Shake position + rotation
            seq.Append(t.DOShakePosition(0.30f, new Vector3(16f, 8f, 0f), 22, 90f, false, true));
            seq.Join(t.DOShakeRotation(0.30f,   new Vector3(0f, 0f, 10f),  18, 90f, true));

            // 2. Color → dark ember
            if (img != null)
                seq.Join(img.DOColor(new Color(0.9f, 0.12f, 0.04f, 1f), 0.30f)
                            .SetEase(Ease.InQuad));

            // 3. Brief beat
            seq.AppendInterval(0.04f);

            // 4. Implode: scale + fade to black
            seq.Append(t.DOScale(Vector3.zero, 0.28f).SetEase(Ease.InBack, 2.8f));
            if (img != null)
                seq.Join(img.DOColor(new Color(0f, 0f, 0f, 0f), 0.28f).SetEase(Ease.InQuad));

            seq.OnComplete(() => SendActionToServer(cardIndex, DraftAction.Burn));
            seq.Play();
        }

        // ── Dispatch to server ────────────────────────────────────────────────────
        private void SendActionToServer(int cardIndex, DraftAction action)
        {
            if (DraftManager.Instance != null)
                DraftManager.Instance.HandleChoiceServerRpc(cardIndex, action);
        }

        private void UpdateActionButtons(HashSet<DraftAction> usedActions)
        {
            if (keepButton != null) keepButton.interactable = !usedActions.Contains(DraftAction.Keep);
            if (giveButton != null) giveButton.interactable = !usedActions.Contains(DraftAction.Give);
            if (burnButton != null) burnButton.interactable = !usedActions.Contains(DraftAction.Burn);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────────
        #region Overflow Burn

        private void HandleOverflowBurnRequested(int playerID, int burnCount)
        {
            if (playerID != GetLocalPlayerID()) return;

            isBurnBlockingDraft = burnCount > 0;

            if (draftPanel != null)
            {
                draftPanel.blocksRaycasts = !isBurnBlockingDraft;
                draftPanel.alpha          = isBurnBlockingDraft ? 0.2f : 1f;
            }

            if (choicePanel != null) choicePanel.SetActive(false);

            if (turnStatusText != null && isBurnBlockingDraft)
            {
                turnStatusText.text  = $"Kart yak: {burnCount}";
                //turnStatusText.color = Color.red;
            }

            if (!isBurnBlockingDraft)
                RefreshDraftUIAfterBurn();
        }

        private void RefreshDraftUIAfterBurn()
        {
            if (DraftManager.Instance == null || !DraftManager.Instance.IsDraftingActive) return;

            int localPlayerID = GetLocalPlayerID();

            if (DraftManager.Instance.DraftingPlayerID == localPlayerID)
            {
                List<CardData> choices = DraftManager.Instance.GetCurrentChoices();
                UpdateDraftUI(DraftManager.Instance.DraftingPlayerID, choices);
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────────
        #region Helpers

        private int GetLocalPlayerID()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                return NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;
            return 1;
        }

        /// <summary>
        /// Returns the lean angle MAGNITUDE for a card slot.
        /// Left slot  (0) → 18° | Center slot (1) → 0° | Right slot (2) → 25°
        /// Keep uses +magnitude, Give uses -magnitude.
        /// For N slots: left half scales to 18°, right half scales to 25°.
        /// </summary>
        private float GetLeanMagnitude(int slotIndex)
        {
            if (cardSlots.Length < 2) return 0f;

            float center = (cardSlots.Length - 1) / 2f;
            float offset = slotIndex - center;  // negative = left, 0 = center, positive = right

            if (offset < 0f)
            {
                // Left side: t=1 at leftmost → 18°
                float t = -offset / center;
                return t * 18f;
            }
            else if (offset > 0f)
            {
                // Right side: t=1 at rightmost → 25°
                float t = offset / center;
                return t * 25f;  // magnitude always positive; callers apply the sign
            }

            return 0f; // center
        }

        #endregion
    }
}
