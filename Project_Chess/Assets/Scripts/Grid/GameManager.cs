using System;
using UnityEngine;

namespace AlperKocasalih.Chess.Grid
{
    public enum GameState
    {
        Setup,
        RollDice,
        DraftPhase,
        ActionPhase,
        EndGame
    }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        #region Fields

        [Header("Game State")]
        [SerializeField, ReadOnly] private GameState currentState = GameState.Setup;

        #endregion

        #region Events

        public event Action<GameState> OnStateChanged;
        public event Action<int> OnGameEnded;

        #endregion

        #region Properties

        public GameState CurrentState => currentState;

        #endregion

        #region Unity Methods

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            ChangeState(GameState.Setup);
        }

        #endregion

        #region State Management

        public void ChangeState(GameState newState)
        {
            if (currentState == newState) return;

            currentState = newState;
            Debug.Log($"GameManager: State changed to {currentState}");

            HandleStateChange(newState);
            OnStateChanged?.Invoke(newState);
        }

        private void HandleStateChange(GameState newState)
        {
            switch (newState)
            {
                case GameState.Setup:
                    // Piyon dizilimi PawnPlacementManager tarafından yapılıyor olabilir
                    // Hazır olduğunda RollDice'a geç
                    break;
                case GameState.RollDice:
                    if (TurnManager.Instance != null)
                    {
                        TurnManager.Instance.RollForTurn();
                    }
                    else
                    {
                        Debug.LogError("GameManager: TurnManager not found!");
                    }
                    break;
                case GameState.DraftPhase:
                    Debug.Log("GameManager: Draft phase started.");
                    if (DraftManager.Instance != null)
                    {
                        DraftManager.Instance.StartDraft();
                    }
                    else
                    {
                        Debug.LogError("GameManager: DraftManager not found! Skipping to ActionPhase.");
                        ChangeState(GameState.ActionPhase);
                    }
                    break;
                case GameState.ActionPhase:
                    Debug.Log("GameManager: Action phase started.");
                    break;
                case GameState.EndGame:
                    Debug.Log("GameManager: Game Over!");
                    break;
            }
        }

        public void EndGame(int winnerID)
        {
            ChangeState(GameState.EndGame);
            OnGameEnded?.Invoke(winnerID);
        }

        public void RestartGame()
        {
            Debug.Log("GameManager: Restarting Game...");

            // 1. Clear Pawns
            Pawn[] allPawns = FindObjectsOfType<Pawn>();
            foreach (var pawn in allPawns)
            {
                if (pawn.OccupiedCell != null)
                {
                    pawn.OccupiedCell.IsOccupied = false;
                    pawn.OccupiedCell.ClearOccupiedPawn();
                }
                Destroy(pawn.gameObject);
            }

            // 2. Reset Managers
            if (TurnManager.Instance != null) TurnManager.Instance.ResetManager();
            if (PawnPlacementManager.Instance != null) PawnPlacementManager.Instance.ResetTracking();
            if (DraftManager.Instance != null) DraftManager.Instance.ResetManager();
            // DraftManager hands are already cleared in StartDraft/Restart logic usually, 
            // but let's ensure we have a clean slate if needed.

            // 3. Reset Deck
            if (DeckManager.Instance != null) DeckManager.Instance.InitializeDeck();

            // 4. Return to Setup
            ChangeState(GameState.Setup);
        }

        #endregion
    }
}
