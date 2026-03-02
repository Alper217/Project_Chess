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

        [Header("Player Hands")]
        [SerializeField, ReadOnly] private List<CardData> p1Hand = new List<CardData>();
        [SerializeField, ReadOnly] private List<CardData> p2Hand = new List<CardData>();

        private List<CardData> currentChoices = new List<CardData>();
        private HashSet<DraftAction> usedActionsThisRound = new HashSet<DraftAction>();
        
        #endregion

        #region Events

        public event Action<int, List<CardData>> OnCardsDrawn;
        public event Action<int> OnDraftTurnChanged; // current player ID
        public event Action<HashSet<DraftAction>> OnUsedActionsChanged;
        public event Action<int, List<CardData>> OnHandUpdated; // playerID, hand
        public event Action OnDraftFinished;

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
            roundCount = 1;
            draftingPlayerID = TurnManager.Instance.ActivePlayerID;
            
            p1Hand.Clear();
            p2Hand.Clear();

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

        [ClientRpc]
        private void NotifyCardsDrawnClientRpc(int[] cardIndices, int playerID)
        {
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
            if (usedActionsThisRound.Contains(action))
            {
                Debug.LogWarning($"DraftManager: Action {action} already used this round!");
                return;
            }

            CardData selected = currentChoices[cardIndex];
            
            switch (action)
            {
                case DraftAction.Keep:
                    if (draftingPlayerID == 1)
                    {
                        p1Hand.Add(selected);
                        NotifyHandUpdatedClientRpc(1, DeckManager.Instance.GetCardIndex(selected), true);
                    }
                    else
                    {
                        p2Hand.Add(selected);
                        NotifyHandUpdatedClientRpc(2, DeckManager.Instance.GetCardIndex(selected), true);
                    }
                    Debug.Log($"DraftManager: Player {draftingPlayerID} kept {selected.cardName}");
                    break;
                case DraftAction.Give:
                    if (draftingPlayerID == 1)
                    {
                        p2Hand.Add(selected);
                        NotifyHandUpdatedClientRpc(2, DeckManager.Instance.GetCardIndex(selected), true);
                    }
                    else
                    {
                        p1Hand.Add(selected);
                        NotifyHandUpdatedClientRpc(1, DeckManager.Instance.GetCardIndex(selected), true);
                    }
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

        [ServerRpc(RequireOwnership = false)]
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

        private void EndCurrentDraftTurn()
        {
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
            Debug.Log("DraftManager: Draft finished. Each player should have 6 cards.");
            
            NotifyDraftFinishedClientRpc();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeState(GameState.ActionPhase);
            }
        }

        [ClientRpc]
        private void NotifyDraftFinishedClientRpc()
        {
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

        [ServerRpc(RequireOwnership = false)]
        private void RemoveCardFromHandServerRpc(int playerID, int cardIndex)
        {
            CardData card = DeckManager.Instance.GetCardByIndex(cardIndex);
            RemoveCardFromHand(playerID, card);
        }

        public void ResetManager()
        {
            isDraftingActive = false;
            p1Hand.Clear();
            p2Hand.Clear();
            currentChoices.Clear();
            usedActionsThisRound.Clear();
            
            OnHandUpdated?.Invoke(1, p1Hand);
            OnHandUpdated?.Invoke(2, p2Hand);
            
            Debug.Log("DraftManager: Reset complete.");
        }

        #endregion
    }

    public enum DraftAction { Keep, Give, Burn }
}
