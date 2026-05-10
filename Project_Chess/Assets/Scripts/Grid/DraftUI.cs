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
        [SerializeField] private Transform[] iconContainers;
        [SerializeField] private GameObject iconPrefab;
        [SerializeField] private Image[] cardImages;
        [SerializeField] private Transform opponentCardSlot;
        [SerializeField] private Transform playerCardSlot;
        [SerializeField] private GameObject[] reRollButtons;
        [SerializeField] private TextMeshProUGUI[] reRollButtonTexts;

        private Vector3[]     slotInitialPositions;
        private Vector3[]     slotInitialScales;
        private Quaternion[]  slotInitialRotations;

        [Header("Choice Buttons Parent")]
        [SerializeField] private GameObject choicePanel;
        [SerializeField] private Button keepButton;
        [SerializeField] private Button giveButton;
        [SerializeField] private Button burnButton;

        [Header("Animation Settings")]
        [SerializeField] private float cardStaggerDelay  = 0.06f;
        [SerializeField] private float cardEnterDuration = 0.35f;
        [SerializeField] private float actionDuration    = 0.40f;

        private int  currentPendingCardIndex = -1;
        private bool isBurnBlockingDraft     = false;

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

            slotInitialPositions = new Vector3[cardSlots.Length];
            slotInitialScales    = new Vector3[cardSlots.Length];
            slotInitialRotations = new Quaternion[cardSlots.Length];

            for (int i = 0; i < cardSlots.Length; i++)
            {
                slotInitialPositions[i] = cardSlots[i].transform.localPosition;
                slotInitialScales[i]    = cardSlots[i].transform.localScale;
                slotInitialRotations[i] = cardSlots[i].transform.localRotation;
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
            if (TurnManager.Instance != null) TurnManager.Instance.RefreshTurnInfoUI();
            if (draftPanel != null)
                draftPanel.DOFade(0f, 0.4f).SetEase(Ease.InCubic).OnComplete(() => draftPanel.gameObject.SetActive(false));
        }

        private void UpdateDraftUI(int playerID, List<CardData> cards)
        {
            ShowDraftUI();
            int localPlayerID = GetLocalPlayerID();
            bool isMyTurn = (localPlayerID == playerID);
            int rolls = DraftManager.Instance.GetReRolls(localPlayerID);

            foreach (var slot in cardSlots) slot.SetActive(false);
            
            for (int i = 0; i < reRollButtons.Length; i++)
            {
                if (reRollButtons[i] != null)
                {
                    bool show = isMyTurn && i < cards.Count && DraftManager.Instance.EnableReRollSystem;
                    reRollButtons[i].SetActive(show);
                    if (reRollButtonTexts.Length > i && reRollButtonTexts[i] != null) reRollButtonTexts[i].text = rolls.ToString();
                }
            }

            if (!isMyTurn) { if (choicePanel != null) choicePanel.SetActive(false); return; }

            for (int i = 0; i < cards.Count; i++)
            {
                if (i >= cardSlots.Length) break;

                GameObject slot = cardSlots[i];
                slot.transform.DOKill();
                slot.transform.localPosition = slotInitialPositions[i] + new Vector3(0f, -50f, 0f);
                slot.transform.localScale = Vector3.zero;
                slot.transform.localRotation = slotInitialRotations[i];

                CanvasGroup slotCG = slot.GetComponent<CanvasGroup>();
                if (slotCG != null) slotCG.alpha = 1f;
                slot.SetActive(true);

                // --- IKON SISTEMI ---
                if (cardNameTexts.Length > i && cardNameTexts[i] != null) 
                {
                    cardNameTexts[i].text = "";
                    cardNameTexts[i].raycastTarget = false;
                } 

                if (iconContainers != null && i < iconContainers.Length && iconContainers[i] != null)
                {
                    foreach (Transform child in iconContainers[i]) Destroy(child.gameObject);
                    iconContainers[i].SetAsLastSibling();
                    if (cards[i].runtimeBuffs != null && iconPrefab != null)
                    {
                        foreach (var buff in cards[i].runtimeBuffs)
                        {
                            if (buff != null && buff.effectIcon != null)
                            {
                                GameObject iconObj = Instantiate(iconPrefab, iconContainers[i]);
                                Image img = iconObj.GetComponent<Image>();
                                if (img != null) img.sprite = buff.effectIcon;

                                BuffTooltipTrigger trigger = iconObj.GetComponent<BuffTooltipTrigger>();
                                if (trigger == null) trigger = iconObj.AddComponent<BuffTooltipTrigger>();
                                trigger.SetData(buff);
                            }
                        }
                    }
                }
                // --------------------

                if (cardImages.Length > i)
                {
                    cardImages[i].sprite = cards[i].cardDesign != null ? cards[i].cardDesign : cards[i].cardSprite;
                    cardImages[i].color = Color.white;
                }

                int ci = i;
                float delay = i * cardStaggerDelay;
                Sequence enterSeq = DOTween.Sequence();
                enterSeq.AppendInterval(delay);
                enterSeq.Append(slot.transform.DOLocalMove(slotInitialPositions[ci], cardEnterDuration).SetEase(Ease.OutCubic));
                enterSeq.Join(slot.transform.DOScale(slotInitialScales[ci], cardEnterDuration).SetEase(Ease.OutBack, 1.8f));
                enterSeq.Join(slot.transform.DOLocalRotateQuaternion(slotInitialRotations[ci], cardEnterDuration).SetEase(Ease.OutCubic));
                enterSeq.Play();
            }
            if (choicePanel != null) choicePanel.SetActive(false);
        }

        public void OnReRollClicked(int index) { if (DraftManager.Instance != null) DraftManager.Instance.ReRollDraftCardServerRpc(index); }

        private void UpdateTurnStatus(int playerID)
        {
            int localPlayerID = GetLocalPlayerID();
            if (turnStatusText == null) return;
            turnStatusText.text = (localPlayerID == playerID) ? "Senin Sıran" : "Rakibin Sırası";
        }

        public void OnCardClicked(int index)
        {
            if (isBurnBlockingDraft) return;
            Transform t = cardSlots[index].transform;
            t.DOKill();
            Vector3 baseScale = slotInitialScales[index];
            Sequence clickSeq = DOTween.Sequence();
            clickSeq.Append(t.DOScale(baseScale * 1.12f, 0.08f).SetEase(Ease.OutQuad));
            clickSeq.Append(t.DOScale(baseScale, 0.12f).SetEase(Ease.OutCubic));
            clickSeq.Play();
            currentPendingCardIndex = index;
            if (choicePanel != null) choicePanel.SetActive(true);
        }

        public void SelectAction(int actionInt)
        {
            if (isBurnBlockingDraft) return;
            DraftAction action = (DraftAction)actionInt;
            if (currentPendingCardIndex < 0 || currentPendingCardIndex >= cardSlots.Length) return;
            int capturedIndex = currentPendingCardIndex;
            GameObject selectedCard = cardSlots[capturedIndex];
            if (choicePanel != null) choicePanel.SetActive(false);
            currentPendingCardIndex = -1;
            switch (action) { case DraftAction.Keep: PlayKeepAnimation(selectedCard, capturedIndex); break; case DraftAction.Give: PlayGiveAnimation(selectedCard, capturedIndex); break; case DraftAction.Burn: PlayBurnAnimation(selectedCard, capturedIndex); break; }
        }

        private void PlayKeepAnimation(GameObject card, int cardIndex)
        {
            Transform t = card.transform;
            t.DOKill();
            CanvasGroup cg = card.GetComponent<CanvasGroup>();
            if (cg == null) cg = card.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            Vector3 baseScale = slotInitialScales[cardIndex];
            float fly = actionDuration;
            Sequence seq = DOTween.Sequence();
            seq.Append(t.DOScale(baseScale * 1.15f, 0.08f).SetEase(Ease.OutQuad));
            seq.Append(t.DOMove(playerCardSlot.position, fly).SetEase(Ease.InCubic));
            float keepLean = GetLeanMagnitude(cardIndex);
            seq.Join(t.DOLocalRotate(new Vector3(0f, 0f, keepLean), fly * 0.55f).SetEase(Ease.OutCubic));
            seq.Join(t.DOScale(Vector3.zero, fly).SetEase(Ease.InQuad));
            seq.Join(cg.DOFade(0f, fly * 0.70f).SetDelay(fly * 0.30f).SetEase(Ease.InQuad));
            seq.OnComplete(() => SendActionToServer(cardIndex, DraftAction.Keep));
            seq.Play();
        }

        private void PlayGiveAnimation(GameObject card, int cardIndex)
        {
            Transform t = card.transform;
            t.DOKill();
            CanvasGroup cg = card.GetComponent<CanvasGroup>();
            if (cg == null) cg = card.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            float fly = actionDuration;
            Sequence seq = DOTween.Sequence();
            float giveLean = (cardIndex == 0) ? -18f : (cardIndex == 1 ? 0f : 25f);
            float giveWindup = -giveLean * 0.45f;
            seq.Append(t.DOLocalRotate(new Vector3(0f, 0f, giveWindup), 0.09f).SetEase(Ease.OutQuad));
            seq.Append(t.DOMove(opponentCardSlot.position, fly).SetEase(Ease.InQuad));
            seq.Join(t.DOLocalRotate(new Vector3(0f, 0f, giveLean), fly * 0.65f).SetEase(Ease.OutCubic));
            seq.Join(t.DOScale(Vector3.zero, fly * 0.80f).SetDelay(fly * 0.20f).SetEase(Ease.InCubic));
            seq.Join(cg.DOFade(0f, fly * 0.80f).SetDelay(fly * 0.20f).SetEase(Ease.InQuad));
            seq.OnComplete(() => SendActionToServer(cardIndex, DraftAction.Give));
            seq.Play();
        }

        private void PlayBurnAnimation(GameObject card, int cardIndex)
        {
            Transform t = card.transform;
            Image img = cardIndex < cardImages.Length ? cardImages[cardIndex] : null;
            t.DOKill();
            Sequence seq = DOTween.Sequence();
            seq.Append(t.DOShakePosition(0.30f, new Vector3(16f, 8f, 0f), 22, 90f, false, true));
            seq.Join(t.DOShakeRotation(0.30f, new Vector3(0f, 0f, 10f), 18, 90f, true));
            if (img != null) seq.Join(img.DOColor(new Color(0.9f, 0.12f, 0.04f, 1f), 0.30f).SetEase(Ease.InQuad));
            seq.AppendInterval(0.04f);
            seq.Append(t.DOScale(Vector3.zero, 0.28f).SetEase(Ease.InBack, 2.8f));
            if (img != null) seq.Join(img.DOColor(new Color(0f, 0f, 0f, 0f), 0.28f).SetEase(Ease.InQuad));
            seq.OnComplete(() => SendActionToServer(cardIndex, DraftAction.Burn));
            seq.Play();
        }

        private void SendActionToServer(int cardIndex, DraftAction action) { if (DraftManager.Instance != null) DraftManager.Instance.HandleChoiceServerRpc(cardIndex, action); }

        private void UpdateActionButtons(HashSet<DraftAction> usedActions)
        {
            if (keepButton != null) keepButton.interactable = !usedActions.Contains(DraftAction.Keep);
            if (giveButton != null) giveButton.interactable = !usedActions.Contains(DraftAction.Give);
            if (burnButton != null) burnButton.interactable = !usedActions.Contains(DraftAction.Burn);
        }

        #endregion

        #region Overflow Burn

        private void HandleOverflowBurnRequested(int playerID, int burnCount)
        {
            if (playerID != GetLocalPlayerID()) return;
            isBurnBlockingDraft = burnCount > 0;
            if (draftPanel != null) { draftPanel.blocksRaycasts = !isBurnBlockingDraft; draftPanel.alpha = isBurnBlockingDraft ? 0.2f : 1f; }
            if (choicePanel != null) choicePanel.SetActive(false);
            if (turnStatusText != null && isBurnBlockingDraft) turnStatusText.text = $"Kart yak: {burnCount}";
            if (!isBurnBlockingDraft) RefreshDraftUIAfterBurn();
        }

        private void RefreshDraftUIAfterBurn() { if (DraftManager.Instance == null || !DraftManager.Instance.IsDraftingActive) return; int localPlayerID = GetLocalPlayerID(); if (DraftManager.Instance.DraftingPlayerID == localPlayerID) { List<CardData> choices = DraftManager.Instance.GetCurrentChoices(); UpdateDraftUI(DraftManager.Instance.DraftingPlayerID, choices); } }

        #endregion

        #region Helpers

        private int GetLocalPlayerID() { if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2; return 1; }

        private float GetLeanMagnitude(int slotIndex)
        {
            if (cardSlots.Length < 2) return 0f;
            float center = (cardSlots.Length - 1) / 2f;
            float offset = slotIndex - center;
            if (offset < 0f) { float t = -offset / center; return t * 18f; }
            else if (offset > 0f) { float t = offset / center; return t * 25f; }
            return 0f;
        }

        #endregion
    }
}






