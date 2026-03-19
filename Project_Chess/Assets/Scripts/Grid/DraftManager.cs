using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Netcode;

namespace AlperKocasalih.Chess.Grid
{
    public class DraftManager : NetworkBehaviour
    {
        public static DraftManager Instance { get; private set; }

        #region Fields

        [Header("State")]
        [SerializeField, ReadOnly] private int draftingPlayerID = 1;
        [SerializeField, ReadOnly] private int roundCount = 1;
        [SerializeField, ReadOnly] private bool isDraftingActive = false;
        [SerializeField, ReadOnly] private bool isActionDraftActive = false;
        [SerializeField, ReadOnly] private bool skipTurnOnActionDraftComplete = false;

        [Header("Player Hands")]
        [SerializeField, ReadOnly] private List<CardData> p1Hand = new List<CardData>();
        [SerializeField, ReadOnly] private List<CardData> p2Hand = new List<CardData>();

        [Header("Hand Limits")]
        [SerializeField] private int maxHandSize = 6;
        [SerializeField, ReadOnly] private int lastSkipDrawTurnP1 = -1;
        [SerializeField, ReadOnly] private int lastSkipDrawTurnP2 = -1;
        [SerializeField, ReadOnly] private int pendingOverflowBurnPlayerID = 0;
        [SerializeField, ReadOnly] private int pendingOverflowBurnCount = 0;
        [SerializeField, ReadOnly] private List<CardData> p1PendingIncoming = new List<CardData>();
        [SerializeField, ReadOnly] private List<CardData> p2PendingIncoming = new List<CardData>();

        [Header("Early Round Restrictions")]
        [SerializeField, Min(0)] private int blockDrawAndBurnForRounds = 2;

        private List<CardData> currentChoices = new List<CardData>();
        private HashSet<DraftAction> usedActionsThisRound = new HashSet<DraftAction>();
        
        #endregion

        #region Events

        public event Action<int, List<CardData>> OnCardsDrawn;
        public event Action<int> OnDraftTurnChanged; // current player ID
        public event Action<HashSet<DraftAction>> OnUsedActionsChanged;
        public event Action<int, List<CardData>> OnHandUpdated; // playerID, hand
        public event Action OnDraftFinished;
        public event Action<int, int> OnOverflowBurnRequested; // playerID, burnCount

        #endregion

        #region Unity Methods

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        #endregion

        #region Logic

        public void StartDraft()
        {
            isDraftingActive = true;
            isActionDraftActive = false;
            skipTurnOnActionDraftComplete = false;
            pendingOverflowBurnPlayerID = 0;
            pendingOverflowBurnCount = 0;
            roundCount = 1;
            draftingPlayerID = TurnManager.Instance.ActivePlayerID;
            
            p1Hand.Clear();
            p2Hand.Clear();
            p1PendingIncoming.Clear();
            p2PendingIncoming.Clear();

            StartDraftRound();
        }

        private void StartDraftRound()
        {
            usedActionsThisRound.Clear();
            NotifyUsedActionsClientRpc(new DraftAction[0]);
            
            if (DeckManager.Instance == null)
            {
                Debug.LogError("DraftManager: DeckManager Instance not found!");
                return;
            }

            if (!IsServer) return;

            currentChoices = DeckManager.Instance.DrawCards(3);

            // Send card indices to clients instead of ScriptableObjects
            int[] drawnCardIndices = new int[currentChoices.Count];
            for(int i = 0; i < currentChoices.Count; ++i)
            {
                drawnCardIndices[i] = DeckManager.Instance.GetCardIndex(currentChoices[i]);
            }

            NotifyCardsDrawnClientRpc(drawnCardIndices, draftingPlayerID);
            
            Debug.Log($"DraftManager: Player {draftingPlayerID} drafting. Round {roundCount}/3.");
        }

        private void StartActionDraftRound()
        {
            usedActionsThisRound.Clear();
            NotifyUsedActionsClientRpc(new DraftAction[0]);

            if (DeckManager.Instance == null)
            {
                Debug.LogError("DraftManager: DeckManager Instance not found!");
                return;
            }

            if (!IsServer) return;

            currentChoices = DeckManager.Instance.DrawCards(3);

            int[] drawnCardIndices = new int[currentChoices.Count];
            for (int i = 0; i < currentChoices.Count; ++i)
            {
                drawnCardIndices[i] = DeckManager.Instance.GetCardIndex(currentChoices[i]);
            }

            NotifyCardsDrawnClientRpc(drawnCardIndices, draftingPlayerID);

            Debug.Log($"DraftManager: Action draft for Player {draftingPlayerID}.");
        }

        [ClientRpc]
        private void NotifyCardsDrawnClientRpc(int[] cardIndices, int playerID)
        {
            isDraftingActive = true;

            // On clients, reconstruct currentChoices from indices
            currentChoices.Clear();
            foreach(int idx in cardIndices)
            {
                currentChoices.Add(DeckManager.Instance.GetCardByIndex(idx));
            }

            OnCardsDrawn?.Invoke(playerID, currentChoices);
            OnDraftTurnChanged?.Invoke(playerID);
        }

        /// <summary>
        /// Action chosen for a specific card index (0-2) in currentChoices.
        /// </summary>
        public void HandleChoice(int cardIndex, DraftAction action)
        {
            if (!isDraftingActive || currentChoices.Count <= cardIndex) return;
            if (pendingOverflowBurnCount > 0)
            {
                Debug.LogWarning("DraftManager: Resolve overflow burn before continuing.");
                return;
            }
            if (action == DraftAction.Burn && isActionDraftActive && IsEarlyRoundLocked())
            {
                Debug.LogWarning("DraftManager: Burn is disabled for the first rounds.");
                return;
            }
            if (usedActionsThisRound.Contains(action))
            {
                Debug.LogWarning($"DraftManager: Action {action} already used this round!");
                return;
            }

            CardData selected = currentChoices[cardIndex];
            
            switch (action)
            {
                case DraftAction.Keep:
                    if (draftingPlayerID == 1) AddCardToHandOrQueue(1, selected);
                    else AddCardToHandOrQueue(2, selected);
                    Debug.Log($"DraftManager: Player {draftingPlayerID} kept {selected.cardName}");
                    break;
                case DraftAction.Give:
                    if (draftingPlayerID == 1) AddCardToHandOrQueue(2, selected);
                    else AddCardToHandOrQueue(1, selected);
                    Debug.Log($"DraftManager: Player {draftingPlayerID} gave {selected.cardName} to opponent");
                    break;
                case DraftAction.Burn:
                    Debug.Log($"DraftManager: Player {draftingPlayerID} burned {selected.cardName}");
                    break;
            }

            usedActionsThisRound.Add(action);
            
            DraftAction[] actionsArray = new DraftAction[usedActionsThisRound.Count];
            usedActionsThisRound.CopyTo(actionsArray);
            NotifyUsedActionsClientRpc(actionsArray);
            
            currentChoices.RemoveAt(cardIndex);

            // If all 3 cards from the draw are processed, move to next round/player
            if (currentChoices.Count == 0)
            {
                EndCurrentDraftTurn();
            }
            else
            {
                // UI should probably refresh to show remaining cards
                int[] remainingIndices = new int[currentChoices.Count];
                for(int i = 0; i < currentChoices.Count; ++i)
                {
                    remainingIndices[i] = DeckManager.Instance.GetCardIndex(currentChoices[i]);
                }
                NotifyCardsDrawnClientRpc(remainingIndices, draftingPlayerID);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void HandleChoiceServerRpc(int cardIndex, DraftAction action)
        {
            HandleChoice(cardIndex, action);
        }

        [ClientRpc]
        private void NotifyHandUpdatedClientRpc(int playerID, int cardIndex, bool isAdd)
        {
            CardData card = DeckManager.Instance.GetCardByIndex(cardIndex);
            List<CardData> hand = playerID == 1 ? p1Hand : p2Hand;

            if (isAdd && !NetworkManager.Singleton.IsServer) // Server already added it
            {
                hand.Add(card);
            }
            else if (!isAdd && !NetworkManager.Singleton.IsServer)
            {
                hand.Remove(card);
            }

            OnHandUpdated?.Invoke(playerID, hand);
        }

        [ClientRpc]
        private void NotifyOverflowBurnRequestedClientRpc(int playerID, int burnCount)
        {
            if (!IsServer)
            {
                pendingOverflowBurnPlayerID = playerID;
                pendingOverflowBurnCount = burnCount;
            }

            OnOverflowBurnRequested?.Invoke(playerID, burnCount);
        }

        private void EndCurrentDraftTurn()
        {
            if (isActionDraftActive)
            {
                FinishDraft();
                return;
            }

            // Turn order: 1 -> 2 -> 1 -> 2 -> 1 -> 2 (3 rounds each)
            if (draftingPlayerID == 1)
            {
                draftingPlayerID = 2;
                StartDraftRound();
            }
            else
            {
                if (roundCount < 3)
                {
                    roundCount++;
                    draftingPlayerID = 1;
                    StartDraftRound();
                }
                else
                {
                    FinishDraft();
                }
            }
        }

        private void FinishDraft()
        {
            isDraftingActive = false;
            Debug.Log(isActionDraftActive
                ? $"DraftManager: Action draft finished for Player {draftingPlayerID}."
                : "DraftManager: Draft finished. Each player should have 6 cards.");
            
            NotifyDraftFinishedClientRpc();

            if (isActionDraftActive)
            {
                if (skipTurnOnActionDraftComplete && TurnManager.Instance != null)
                {
                    TurnManager.Instance.NextTurn();
                }

                isActionDraftActive = false;
                skipTurnOnActionDraftComplete = false;
                return;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeState(GameState.ActionPhase);
            }
        }

        [ClientRpc]
        private void NotifyDraftFinishedClientRpc()
        {
            if (!IsServer)
            {
                isDraftingActive = false;
                isActionDraftActive = false;
                skipTurnOnActionDraftComplete = false;
            }
            OnDraftFinished?.Invoke();
        }

        [ClientRpc]
        private void NotifyUsedActionsClientRpc(DraftAction[] actions)
        {
            if (!IsServer)
            {
                usedActionsThisRound.Clear();
                foreach (var act in actions)
                {
                    usedActionsThisRound.Add(act);
                }
            }
            OnUsedActionsChanged?.Invoke(usedActionsThisRound);
        }

        public List<CardData> GetHand(int playerID) => playerID == 1 ? p1Hand : p2Hand;
        public bool IsDraftingActive => isDraftingActive;
        public bool IsOverflowBurnPending => pendingOverflowBurnCount > 0;
        public int DraftingPlayerID => draftingPlayerID;
        public bool IsDrawAllowed => !IsEarlyRoundLocked();
        public bool IsBurnAllowed => !isActionDraftActive || !IsEarlyRoundLocked();

        public List<CardData> GetCurrentChoices()
        {
            return new List<CardData>(currentChoices);
        }

        private void AddCardToHandOrQueue(int playerID, CardData card)
        {
            if (maxHandSize <= 0)
            {
                AddCardToHandNow(playerID, card);
                return;
            }

            List<CardData> hand = GetHand(playerID);
            List<CardData> pending = GetPendingIncoming(playerID);

            if (hand.Count + pending.Count + 1 > maxHandSize)
            {
                pending.Add(card);
                RequestOverflowBurn(playerID);
                return;
            }

            AddCardToHandNow(playerID, card);
        }

        private void AddCardToHandNow(int playerID, CardData card)
        {
            List<CardData> hand = GetHand(playerID);
            hand.Add(card);
            NotifyHandUpdatedClientRpc(playerID, DeckManager.Instance.GetCardIndex(card), true);
        }

        private List<CardData> GetPendingIncoming(int playerID)
        {
            return playerID == 1 ? p1PendingIncoming : p2PendingIncoming;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void DrawOneAndSkipTurnServerRpc(RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.ActionPhase) return;
            if (TurnManager.Instance == null || DeckManager.Instance == null) return;
            if (isDraftingActive) return;
            if (IsEarlyRoundLocked())
            {
                Debug.LogWarning("DraftManager: Draw is disabled for the first rounds.");
                return;
            }

            int playerID = GetPlayerIdFromClientId(rpcParams.Receive.SenderClientId);
            if (TurnManager.Instance.ActivePlayerID != playerID) return;

            int currentTurn = TurnManager.Instance.TurnCount;
            if (playerID == 1 && lastSkipDrawTurnP1 == currentTurn) return;
            if (playerID == 2 && lastSkipDrawTurnP2 == currentTurn) return;

            if (playerID == 1) lastSkipDrawTurnP1 = currentTurn;
            else lastSkipDrawTurnP2 = currentTurn;

            isDraftingActive = true;
            isActionDraftActive = true;
            skipTurnOnActionDraftComplete = true;
            draftingPlayerID = playerID;

            StartActionDraftRound();
        }

        private int GetPlayerIdFromClientId(ulong clientId)
        {
            if (NetworkManager.Singleton == null) return 1;
            return clientId == NetworkManager.ServerClientId ? 1 : 2;
        }

        private bool IsEarlyRoundLocked()
        {
            if (blockDrawAndBurnForRounds <= 0) return false;
            if (TurnManager.Instance == null) return false;

            int currentRound = (TurnManager.Instance.TurnCount + 1) / 2;
            return currentRound <= blockDrawAndBurnForRounds;
        }

        private void RequestOverflowBurn(int playerID)
        {
            List<CardData> hand = GetHand(playerID);
            List<CardData> pending = GetPendingIncoming(playerID);
            int overflow = (hand.Count + pending.Count) - maxHandSize;
            if (overflow <= 0) return;

            pendingOverflowBurnPlayerID = playerID;
            pendingOverflowBurnCount = overflow;

            NotifyOverflowBurnRequestedClientRpc(playerID, pendingOverflowBurnCount);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void BurnOverflowCardAtIndexServerRpc(int playerID, int handIndex)
        {
            if (!IsServer) return;
            if (pendingOverflowBurnCount <= 0 || pendingOverflowBurnPlayerID != playerID) return;

            List<CardData> hand = GetHand(playerID);
            if (handIndex < 0 || handIndex >= hand.Count) return;

            CardData burned = hand[handIndex];
            hand.RemoveAt(handIndex);
            NotifyHandUpdatedClientRpc(playerID, DeckManager.Instance.GetCardIndex(burned), false);

            ResolveOverflowAndApplyPending(playerID);
        }

        private void ResolveOverflowAndApplyPending(int playerID)
        {
            int overflow = 0;
            if (maxHandSize > 0)
            {
                List<CardData> hand = GetHand(playerID);
                List<CardData> pending = GetPendingIncoming(playerID);
                overflow = (hand.Count + pending.Count) - maxHandSize;
            }

            if (overflow > 0)
            {
                pendingOverflowBurnPlayerID = playerID;
                pendingOverflowBurnCount = overflow;
                NotifyOverflowBurnRequestedClientRpc(playerID, pendingOverflowBurnCount);
                return;
            }

            pendingOverflowBurnPlayerID = 0;
            pendingOverflowBurnCount = 0;
            NotifyOverflowBurnRequestedClientRpc(playerID, 0);

            ApplyPendingIncoming(playerID);

            // If the other player has pending overflow, request it now.
            int otherPlayerID = playerID == 1 ? 2 : 1;
            if (GetPendingIncoming(otherPlayerID).Count > 0)
            {
                RequestOverflowBurn(otherPlayerID);
            }
        }

        private void ApplyPendingIncoming(int playerID)
        {
            List<CardData> pending = GetPendingIncoming(playerID);
            if (pending.Count == 0) return;

            foreach (var card in pending)
            {
                AddCardToHandNow(playerID, card);
            }

            pending.Clear();
        }

        public void RemoveCardFromHand(int playerID, CardData card)
        {
            if (IsServer)
            {
                List<CardData> hand = GetHand(playerID);
                if (hand.Contains(card))
                {
                    hand.Remove(card);
                    NotifyHandUpdatedClientRpc(playerID, DeckManager.Instance.GetCardIndex(card), false);
                    Debug.Log($"DraftManager: Removed {card.cardName} from Player {playerID}'s hand.");

                    // If both hands are empty, go back to drafting phase
                    if (p1Hand.Count == 0 && p2Hand.Count == 0)
                    {
                        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.ActionPhase)
                        {
                            Debug.Log("DraftManager: Both hands empty. Returning to DraftPhase.");
                            GameManager.Instance.ChangeState(GameState.DraftPhase);
                        }
                    }
                }
            }
            else
            {
                RemoveCardFromHandServerRpc(playerID, DeckManager.Instance.GetCardIndex(card));
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RemoveCardFromHandServerRpc(int playerID, int cardIndex)
        {
            CardData card = DeckManager.Instance.GetCardByIndex(cardIndex);
            RemoveCardFromHand(playerID, card);
        }

        public void ResetManager()
        {
            isDraftingActive = false;
            isActionDraftActive = false;
            skipTurnOnActionDraftComplete = false;
            p1Hand.Clear();
            p2Hand.Clear();
            currentChoices.Clear();
            usedActionsThisRound.Clear();
            lastSkipDrawTurnP1 = -1;
            lastSkipDrawTurnP2 = -1;
            pendingOverflowBurnPlayerID = 0;
            pendingOverflowBurnCount = 0;
            p1PendingIncoming.Clear();
            p2PendingIncoming.Clear();
            
            OnHandUpdated?.Invoke(1, p1Hand);
            OnHandUpdated?.Invoke(2, p2Hand);
            
            Debug.Log("DraftManager: Reset complete.");
        }

        #endregion
    }

    public enum DraftAction { Keep, Give, Burn }
}
